package com.kingdomtycoon.server.idempotency;

import java.sql.Timestamp;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcIdempotencyRepository implements IdempotencyRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcIdempotencyRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public Decision begin(String scope, Long playerId, UUID key, String requestHash, Instant expiresAt) {
        List<Long> inserted = jdbcTemplate.query(
            """
                INSERT INTO audit.idempotency_request
                    (scope, player_id, idempotency_key, request_hash, status, expires_at)
                VALUES (?, ?, ?, ?, 'PROCESSING', ?)
                ON CONFLICT ON CONSTRAINT uk_idempotency_request__scope_player_key DO NOTHING
                RETURNING idempotency_id
                """,
            (resultSet, rowNumber) -> resultSet.getLong(1),
            scope,
            playerId,
            key,
            requestHash,
            Timestamp.from(expiresAt)
        );

        if (!inserted.isEmpty()) {
            return new Decision(inserted.getFirst(), Outcome.ACQUIRED);
        }

        ExistingRequest existing = jdbcTemplate.queryForObject(
            """
                SELECT idempotency_id, request_hash, status
                  FROM audit.idempotency_request
                 WHERE scope = ?
                   AND player_id IS NOT DISTINCT FROM ?
                   AND idempotency_key = ?
                """,
            (resultSet, rowNumber) -> new ExistingRequest(
                resultSet.getLong("idempotency_id"),
                resultSet.getString("request_hash"),
                resultSet.getString("status")
            ),
            scope,
            playerId,
            key
        );

        if (existing == null) {
            throw new IllegalStateException("idempotency row disappeared");
        }
        if (!existing.requestHash().equals(requestHash)) {
            throw new IdempotencyConflictException("same idempotency key was used with a different payload");
        }
        return new Decision(
            existing.idempotencyId(),
            "SUCCEEDED".equals(existing.status()) ? Outcome.REPLAY : Outcome.IN_PROGRESS
        );
    }

    @Override
    public void succeed(long idempotencyId, int responseCode, String responseBody) {
        complete(idempotencyId, "SUCCEEDED", responseCode, responseBody);
    }

    @Override
    public void fail(long idempotencyId, int responseCode, String responseBody) {
        complete(idempotencyId, "FAILED", responseCode, responseBody);
    }

    private void complete(long idempotencyId, String status, int responseCode, String responseBody) {
        int updated = jdbcTemplate.update(
            """
                UPDATE audit.idempotency_request
                   SET status = ?,
                       response_code = ?,
                       response_body = CAST(? AS jsonb),
                       completed_at = now()
                 WHERE idempotency_id = ?
                   AND status = 'PROCESSING'
                """,
            status,
            responseCode,
            responseBody,
            idempotencyId
        );
        if (updated != 1) {
            throw new IllegalStateException("idempotency request was already completed");
        }
    }

    private record ExistingRequest(long idempotencyId, String requestHash, String status) {
    }
}
