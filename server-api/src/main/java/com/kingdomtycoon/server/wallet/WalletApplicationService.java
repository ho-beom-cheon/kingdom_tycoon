package com.kingdomtycoon.server.wallet;

import com.kingdomtycoon.server.idempotency.IdempotencyRepository;
import com.kingdomtycoon.server.outbox.OutboxRepository;
import com.kingdomtycoon.server.support.RequestHash;
import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class WalletApplicationService {

    private static final short LEDGER_LINE = 1;

    private final IdempotencyRepository idempotencyRepository;
    private final WalletRepository walletRepository;
    private final OutboxRepository outboxRepository;

    public WalletApplicationService(
        IdempotencyRepository idempotencyRepository,
        WalletRepository walletRepository,
        OutboxRepository outboxRepository
    ) {
        this.idempotencyRepository = idempotencyRepository;
        this.walletRepository = walletRepository;
        this.outboxRepository = outboxRepository;
    }

    @Transactional
    public WalletChangeResult change(
        long playerId,
        String currencyKey,
        long deltaAmount,
        String reasonCode,
        String referenceType,
        String referenceId,
        UUID idempotencyKey
    ) {
        if (deltaAmount == 0) {
            throw new IllegalArgumentException("wallet delta must not be zero");
        }
        String requestHash = RequestHash.sha256(
            "%d|%s|%d|%s|%s|%s".formatted(
                playerId,
                currencyKey,
                deltaAmount,
                reasonCode,
                referenceType,
                referenceId
            )
        );
        IdempotencyRepository.Decision decision = idempotencyRepository.begin(
            "WALLET",
            playerId,
            idempotencyKey,
            requestHash,
            Instant.now().plus(Duration.ofHours(24))
        );

        if (decision.outcome() == IdempotencyRepository.Outcome.REPLAY) {
            WalletRepository.WalletLedgerEntry ledger = walletRepository
                .findLedger(playerId, idempotencyKey, LEDGER_LINE)
                .orElseThrow(() -> new IllegalStateException("successful wallet request has no ledger"));
            return WalletChangeResult.from(ledger, true);
        }
        if (decision.outcome() == IdempotencyRepository.Outcome.IN_PROGRESS) {
            throw new IllegalStateException("idempotency request is already in progress or failed");
        }

        WalletRepository.WalletBalance wallet = walletRepository.lockByPlayerAndCurrency(playerId, currencyKey);
        long balanceAfter = Math.addExact(wallet.balance(), deltaAmount);
        if (balanceAfter < 0) {
            throw new InsufficientBalanceException(playerId, currencyKey, wallet.balance(), deltaAmount);
        }

        WalletRepository.WalletLedgerEntry ledger = new WalletRepository.WalletLedgerEntry(
            idempotencyKey,
            LEDGER_LINE,
            playerId,
            currencyKey,
            deltaAmount,
            wallet.balance(),
            balanceAfter,
            reasonCode,
            referenceType,
            referenceId
        );
        long walletTransactionId = walletRepository.insertLedger(ledger);
        walletRepository.updateBalance(playerId, currencyKey, balanceAfter, wallet.version());
        outboxRepository.append(
            "WALLET",
            "%d:%s".formatted(playerId, currencyKey),
            "WalletBalanceChanged",
            "{\"playerId\":%d,\"currencyKey\":\"%s\",\"deltaAmount\":%d,\"balanceAfter\":%d}"
                .formatted(playerId, currencyKey, deltaAmount, balanceAfter)
        );
        idempotencyRepository.succeed(
            decision.idempotencyId(),
            200,
            "{\"balanceAfter\":%d}".formatted(balanceAfter)
        );

        return new WalletChangeResult(
            walletTransactionId,
            playerId,
            currencyKey,
            deltaAmount,
            wallet.balance(),
            balanceAfter,
            false
        );
    }

    public record WalletChangeResult(
        long walletTransactionId,
        long playerId,
        String currencyKey,
        long deltaAmount,
        long balanceBefore,
        long balanceAfter,
        boolean replayed
    ) {
        static WalletChangeResult from(WalletRepository.WalletLedgerEntry entry, boolean replayed) {
            return new WalletChangeResult(
                entry.walletTransactionId(),
                entry.playerId(),
                entry.currencyKey(),
                entry.deltaAmount(),
                entry.balanceBefore(),
                entry.balanceAfter(),
                replayed
            );
        }
    }
}
