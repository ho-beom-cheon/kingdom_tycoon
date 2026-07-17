package com.kingdomtycoon.server.outbox;

import java.time.Instant;
import java.util.List;

public interface OutboxRepository {

    long append(String aggregateType, String aggregateId, String eventType, String payload);

    List<OutboxEvent> lockPendingBatch(int batchSize);

    void markPublished(long outboxEventId);

    void markFailed(long outboxEventId, String error, Instant nextAttemptAt);

    record OutboxEvent(long id, String aggregateType, String aggregateId, String eventType, String payload) {
    }
}
