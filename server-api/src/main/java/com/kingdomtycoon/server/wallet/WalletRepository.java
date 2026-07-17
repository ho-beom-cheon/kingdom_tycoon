package com.kingdomtycoon.server.wallet;

import java.util.Optional;
import java.util.UUID;

public interface WalletRepository {

    WalletBalance lockByPlayerAndCurrency(long playerId, String currencyKey);

    long insertLedger(WalletLedgerEntry entry);

    void updateBalance(long playerId, String currencyKey, long newBalance, long expectedVersion);

    Optional<WalletLedgerEntry> findLedger(long playerId, UUID requestId, short lineNo);

    record WalletBalance(long playerId, String currencyKey, long balance, long version) {
    }

    record WalletLedgerEntry(
        long walletTransactionId,
        UUID requestId,
        short lineNo,
        long playerId,
        String currencyKey,
        long deltaAmount,
        long balanceBefore,
        long balanceAfter,
        String reasonCode,
        String referenceType,
        String referenceId
    ) {
        public WalletLedgerEntry(
            UUID requestId,
            short lineNo,
            long playerId,
            String currencyKey,
            long deltaAmount,
            long balanceBefore,
            long balanceAfter,
            String reasonCode,
            String referenceType,
            String referenceId
        ) {
            this(
                0L,
                requestId,
                lineNo,
                playerId,
                currencyKey,
                deltaAmount,
                balanceBefore,
                balanceAfter,
                reasonCode,
                referenceType,
                referenceId
            );
        }
    }
}
