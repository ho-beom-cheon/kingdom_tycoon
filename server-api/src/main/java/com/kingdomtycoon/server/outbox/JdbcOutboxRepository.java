package com.kingdomtycoon.server.outbox;

import java.sql.Timestamp;
import java.time.Instant;
import java.util.List;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcOutboxRepository implements OutboxRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcOutboxRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public long append(String aggregateType, String aggregateId, String eventType, String payload) {
        Long id = jdbcTemplate.queryForObject(
            """
                INSERT INTO audit.outbox_event
                    (aggregate_type, aggregate_id, event_type, payload)
                VALUES (?, ?, ?, CAST(? AS jsonb))
                RETURNING outbox_event_id
                """,
            Long.class,
            aggregateType,
            aggregateId,
            eventType,
            payload
        );
        if (id == null) {
            throw new IllegalStateException("outbox insert returned no id");
        }
        return id;
    }

    @Override
    public List<OutboxEvent> lockPendingBatch(int batchSize) {
        return jdbcTemplate.query(
            """
                SELECT outbox_event_id, aggregate_type, aggregate_id, event_type, payload::text
                  FROM audit.outbox_event
                 WHERE published_at IS NULL
                   AND next_attempt_at <= now()
                 ORDER BY outbox_event_id
                 FOR UPDATE SKIP LOCKED
                 LIMIT ?
                """,
            (resultSet, rowNumber) -> new OutboxEvent(
                resultSet.getLong("outbox_event_id"),
                resultSet.getString("aggregate_type"),
                resultSet.getString("aggregate_id"),
                resultSet.getString("event_type"),
                resultSet.getString("payload")
            ),
            batchSize
        );
    }

    @Override
    public void markPublished(long outboxEventId) {
        jdbcTemplate.update(
            "UPDATE audit.outbox_event SET published_at = now(), last_error = NULL WHERE outbox_event_id = ?",
            outboxEventId
        );
    }

    @Override
    public void markFailed(long outboxEventId, String error, Instant nextAttemptAt) {
        jdbcTemplate.update(
            """
                UPDATE audit.outbox_event
                   SET attempt_count = attempt_count + 1,
                       last_error = ?,
                       next_attempt_at = ?
                 WHERE outbox_event_id = ?
                """,
            error,
            Timestamp.from(nextAttemptAt),
            outboxEventId
        );
    }
}
