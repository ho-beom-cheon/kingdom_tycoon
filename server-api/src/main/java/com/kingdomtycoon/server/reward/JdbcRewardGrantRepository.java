package com.kingdomtycoon.server.reward;

import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcRewardGrantRepository implements RewardGrantRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcRewardGrantRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public Optional<RewardGrant> findByRequestId(long playerId, UUID requestId) {
        List<RewardGrant> grants = jdbcTemplate.query(
            """
                SELECT reward_grant_id, player_id, request_id, status
                  FROM game.reward_grant
                 WHERE player_id = ?
                   AND request_id = ?
                """,
            (resultSet, rowNumber) -> new RewardGrant(
                resultSet.getLong("reward_grant_id"),
                resultSet.getLong("player_id"),
                resultSet.getObject("request_id", UUID.class),
                resultSet.getString("status")
            ),
            playerId,
            requestId
        );
        return grants.stream().findFirst();
    }

    @Override
    public long begin(RewardGrantCommand command) {
        Long id = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.reward_grant
                    (request_id, player_id, release_id, reward_group_id, source_type, source_id)
                VALUES (?, ?, ?, ?, ?, ?)
                RETURNING reward_grant_id
                """,
            Long.class,
            command.requestId(),
            command.playerId(),
            command.releaseId(),
            command.rewardGroupId(),
            command.sourceType(),
            command.sourceId()
        );
        if (id == null) {
            throw new IllegalStateException("reward grant insert returned no id");
        }
        return id;
    }

    @Override
    public void appendResult(long rewardGrantId, short lineNo, String rewardType, String rewardKey, long quantity) {
        jdbcTemplate.update(
            """
                INSERT INTO game.reward_grant_item
                    (reward_grant_id, line_no, reward_type, reward_key, quantity)
                VALUES (?, ?, ?, ?, ?)
                """,
            rewardGrantId,
            lineNo,
            rewardType,
            rewardKey,
            quantity
        );
    }

    @Override
    public void complete(long rewardGrantId) {
        int updated = jdbcTemplate.update(
            """
                UPDATE game.reward_grant
                   SET status = 'GRANTED', completed_at = now()
                 WHERE reward_grant_id = ?
                   AND status = 'PROCESSING'
                """,
            rewardGrantId
        );
        if (updated != 1) {
            throw new IllegalStateException("reward grant was not processing");
        }
    }
}
