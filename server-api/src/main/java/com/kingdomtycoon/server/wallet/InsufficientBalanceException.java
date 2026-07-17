package com.kingdomtycoon.server.wallet;

public class InsufficientBalanceException extends RuntimeException {

    public InsufficientBalanceException(long playerId, String currencyKey, long balance, long delta) {
        super("insufficient balance: player=%d currency=%s balance=%d delta=%d"
            .formatted(playerId, currencyKey, balance, delta));
    }
}
