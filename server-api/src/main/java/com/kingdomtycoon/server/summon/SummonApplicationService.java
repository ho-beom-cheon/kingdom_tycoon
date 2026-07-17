package com.kingdomtycoon.server.summon;

import com.kingdomtycoon.server.idempotency.IdempotencyRepository;
import com.kingdomtycoon.server.outbox.OutboxRepository;
import com.kingdomtycoon.server.support.RequestHash;
import com.kingdomtycoon.server.wallet.InsufficientBalanceException;
import com.kingdomtycoon.server.wallet.WalletRepository;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.HexFormat;
import java.util.List;
import java.util.UUID;
import java.util.random.RandomGenerator;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class SummonApplicationService {

    private final IdempotencyRepository idempotencyRepository;
    private final WalletRepository walletRepository;
    private final SummonRepository summonRepository;
    private final OutboxRepository outboxRepository;
    private final RandomGenerator randomGenerator;

    public SummonApplicationService(
        IdempotencyRepository idempotencyRepository,
        WalletRepository walletRepository,
        SummonRepository summonRepository,
        OutboxRepository outboxRepository,
        RandomGenerator randomGenerator
    ) {
        this.idempotencyRepository = idempotencyRepository;
        this.walletRepository = walletRepository;
        this.summonRepository = summonRepository;
        this.outboxRepository = outboxRepository;
        this.randomGenerator = randomGenerator;
    }

    @Transactional
    public SummonResult summon(
        long playerId,
        String bannerKey,
        short pullCount,
        UUID idempotencyKey
    ) {
        String requestHash = RequestHash.sha256("%d|%s|%d".formatted(playerId, bannerKey, pullCount));
        IdempotencyRepository.Decision decision = idempotencyRepository.begin(
            "SUMMON",
            playerId,
            idempotencyKey,
            requestHash,
            Instant.now().plus(Duration.ofDays(7))
        );
        if (decision.outcome() == IdempotencyRepository.Outcome.REPLAY) {
            SummonRepository.SummonTransaction transaction = summonRepository
                .findByRequestId(playerId, idempotencyKey)
                .orElseThrow(() -> new IllegalStateException("successful summon request has no transaction"));
            return new SummonResult(transaction.summonTransactionId(), transaction.pullCount(), true);
        }
        if (decision.outcome() == IdempotencyRepository.Outcome.IN_PROGRESS) {
            throw new IllegalStateException("summon request is already in progress or failed");
        }

        SummonRepository.Banner banner = summonRepository.findActiveBanner(bannerKey, "PRODUCTION");
        if (pullCount != banner.pullUnit()) {
            throw new IllegalArgumentException("pull count must match the banner pull unit");
        }

        WalletRepository.WalletBalance wallet = walletRepository.lockByPlayerAndCurrency(
            playerId,
            banner.currencyKey()
        );
        long balanceAfter = Math.subtractExact(wallet.balance(), banner.costAmount());
        if (balanceAfter < 0) {
            throw new InsufficientBalanceException(
                playerId,
                banner.currencyKey(),
                wallet.balance(),
                -banner.costAmount()
            );
        }
        long walletTransactionId = walletRepository.insertLedger(
            new WalletRepository.WalletLedgerEntry(
                idempotencyKey,
                (short) 1,
                playerId,
                banner.currencyKey(),
                -banner.costAmount(),
                wallet.balance(),
                balanceAfter,
                "SUMMON_COST",
                "SUMMON_BANNER",
                banner.bannerKey()
            )
        );
        walletRepository.updateBalance(playerId, banner.currencyKey(), balanceAfter, wallet.version());

        SummonRepository.Pity pity = summonRepository.lockPity(playerId, banner.pityGroupKey());
        int pityCount = pity.pullCount();
        List<PendingResult> pendingResults = new ArrayList<>(pullCount);
        for (short resultNo = 1; resultNo <= pullCount; resultNo++) {
            boolean guaranteed = pityCount + 1 >= banner.pityThreshold();
            List<SummonRepository.PoolEntry> pool = summonRepository.loadPool(
                banner.bannerId(),
                guaranteed,
                banner.guaranteedRarity()
            );
            SummonRepository.PoolEntry selected = chooseWeighted(pool);
            SummonRepository.CreatedMercenary mercenary = summonRepository.createMercenary(
                playerId,
                selected.templateId(),
                banner.releaseId(),
                selected.rarity()
            );
            pendingResults.add(new PendingResult(resultNo, selected, guaranteed, mercenary.publicId()));
            pityCount = selected.rarity().equals(banner.guaranteedRarity()) ? 0 : pityCount + 1;
        }
        summonRepository.savePity(
            new SummonRepository.Pity(
                pity.playerId(),
                pity.pityGroupKey(),
                pityCount,
                "NONE",
                pity.version()
            )
        );

        byte[] randomTrace = new byte[32];
        randomGenerator.nextBytes(randomTrace);
        String randomTraceHash = RequestHash.sha256(HexFormat.of().formatHex(randomTrace));
        long summonTransactionId = summonRepository.insertTransaction(
            idempotencyKey,
            playerId,
            banner.bannerId(),
            banner.releaseId(),
            pullCount,
            walletTransactionId,
            randomTraceHash
        );
        for (PendingResult result : pendingResults) {
            summonRepository.insertResult(
                summonTransactionId,
                result.resultNo(),
                result.poolEntry(),
                result.guaranteed(),
                result.createdEntityPublicId()
            );
        }
        outboxRepository.append(
            "SUMMON",
            Long.toString(summonTransactionId),
            "SummonCompleted",
            "{\"summonTransactionId\":%d,\"playerId\":%d,\"pullCount\":%d}"
                .formatted(summonTransactionId, playerId, pullCount)
        );
        idempotencyRepository.succeed(
            decision.idempotencyId(),
            200,
            "{\"summonTransactionId\":%d}".formatted(summonTransactionId)
        );
        return new SummonResult(summonTransactionId, pullCount, false);
    }

    private SummonRepository.PoolEntry chooseWeighted(List<SummonRepository.PoolEntry> pool) {
        if (pool.isEmpty()) {
            throw new IllegalStateException("summon pool is empty");
        }
        long totalWeight = 0;
        for (SummonRepository.PoolEntry entry : pool) {
            totalWeight = Math.addExact(totalWeight, entry.weight());
        }
        long cursor = randomGenerator.nextLong(totalWeight);
        for (SummonRepository.PoolEntry entry : pool) {
            cursor -= entry.weight();
            if (cursor < 0) {
                return entry;
            }
        }
        throw new IllegalStateException("weighted selection failed");
    }

    private record PendingResult(
        short resultNo,
        SummonRepository.PoolEntry poolEntry,
        boolean guaranteed,
        UUID createdEntityPublicId
    ) {
    }

    public record SummonResult(long summonTransactionId, short pullCount, boolean replayed) {
    }
}
