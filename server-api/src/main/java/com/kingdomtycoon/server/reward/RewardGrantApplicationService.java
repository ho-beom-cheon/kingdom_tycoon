package com.kingdomtycoon.server.reward;

import com.kingdomtycoon.server.content.ActiveContentRepository;
import com.kingdomtycoon.server.idempotency.IdempotencyRepository;
import com.kingdomtycoon.server.outbox.OutboxRepository;
import com.kingdomtycoon.server.support.RequestHash;
import com.kingdomtycoon.server.wallet.WalletApplicationService;
import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class RewardGrantApplicationService {

    private final IdempotencyRepository idempotencyRepository;
    private final RewardGrantRepository rewardGrantRepository;
    private final ActiveContentRepository activeContentRepository;
    private final WalletApplicationService walletApplicationService;
    private final OutboxRepository outboxRepository;

    public RewardGrantApplicationService(
        IdempotencyRepository idempotencyRepository,
        RewardGrantRepository rewardGrantRepository,
        ActiveContentRepository activeContentRepository,
        WalletApplicationService walletApplicationService,
        OutboxRepository outboxRepository
    ) {
        this.idempotencyRepository = idempotencyRepository;
        this.rewardGrantRepository = rewardGrantRepository;
        this.activeContentRepository = activeContentRepository;
        this.walletApplicationService = walletApplicationService;
        this.outboxRepository = outboxRepository;
    }

    @Transactional
    public RewardGrantResult grantCurrency(
        long playerId,
        UUID requestId,
        String currencyKey,
        long quantity,
        String sourceType,
        String sourceId
    ) {
        if (quantity <= 0) {
            throw new IllegalArgumentException("reward quantity must be positive");
        }
        String requestHash = RequestHash.sha256(
            "%d|%s|%d|%s|%s".formatted(playerId, currencyKey, quantity, sourceType, sourceId)
        );
        IdempotencyRepository.Decision decision = idempotencyRepository.begin(
            "REWARD",
            playerId,
            requestId,
            requestHash,
            Instant.now().plus(Duration.ofDays(7))
        );
        if (decision.outcome() == IdempotencyRepository.Outcome.REPLAY) {
            RewardGrantRepository.RewardGrant grant = rewardGrantRepository
                .findByRequestId(playerId, requestId)
                .orElseThrow(() -> new IllegalStateException("successful reward request has no grant"));
            return new RewardGrantResult(grant.rewardGrantId(), true);
        }
        if (decision.outcome() == IdempotencyRepository.Outcome.IN_PROGRESS) {
            throw new IllegalStateException("reward request is already in progress or failed");
        }

        long releaseId = activeContentRepository.findActive("PRODUCTION").releaseId();
        long rewardGrantId = rewardGrantRepository.begin(
            new RewardGrantRepository.RewardGrantCommand(
                requestId,
                playerId,
                releaseId,
                null,
                sourceType,
                sourceId
            )
        );
        rewardGrantRepository.appendResult(rewardGrantId, (short) 1, "CURRENCY", currencyKey, quantity);
        walletApplicationService.change(
            playerId,
            currencyKey,
            quantity,
            "REWARD_GRANT",
            "REWARD_GRANT",
            Long.toString(rewardGrantId),
            requestId
        );
        rewardGrantRepository.complete(rewardGrantId);
        outboxRepository.append(
            "REWARD_GRANT",
            Long.toString(rewardGrantId),
            "RewardGranted",
            "{\"rewardGrantId\":%d,\"playerId\":%d}".formatted(rewardGrantId, playerId)
        );
        idempotencyRepository.succeed(
            decision.idempotencyId(),
            200,
            "{\"rewardGrantId\":%d}".formatted(rewardGrantId)
        );
        return new RewardGrantResult(rewardGrantId, false);
    }

    public record RewardGrantResult(long rewardGrantId, boolean replayed) {
    }
}
