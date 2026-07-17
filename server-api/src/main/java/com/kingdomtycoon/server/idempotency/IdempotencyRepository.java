package com.kingdomtycoon.server.idempotency;

import java.time.Instant;
import java.util.UUID;

public interface IdempotencyRepository {

    Decision begin(String scope, Long playerId, UUID key, String requestHash, Instant expiresAt);

    void succeed(long idempotencyId, int responseCode, String responseBody);

    void fail(long idempotencyId, int responseCode, String responseBody);

    enum Outcome {
        ACQUIRED,
        REPLAY,
        IN_PROGRESS
    }

    record Decision(long idempotencyId, Outcome outcome) {
    }
}
