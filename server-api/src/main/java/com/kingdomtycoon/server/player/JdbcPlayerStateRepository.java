package com.kingdomtycoon.server.player;

import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcPlayerStateRepository implements PlayerStateRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcPlayerStateRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public PlayerStateProjection loadPlayerState(long playerId) {
        PlayerStateProjection projection = jdbcTemplate.queryForObject(
            """
                SELECT account.player_id,
                       account.public_id,
                       account.status,
                       profile.nickname,
                       profile.level,
                       profile.exp,
                       profile.version
                  FROM game.player_account account
                  JOIN game.player_profile profile
                    ON profile.player_id = account.player_id
                 WHERE account.player_id = ?
                """,
            (resultSet, rowNumber) -> new PlayerStateProjection(
                resultSet.getLong("player_id"),
                resultSet.getObject("public_id", UUID.class),
                resultSet.getString("status"),
                resultSet.getString("nickname"),
                resultSet.getInt("level"),
                resultSet.getLong("exp"),
                resultSet.getLong("version")
            ),
            playerId
        );
        if (projection == null) {
            throw new IllegalArgumentException("player does not exist: " + playerId);
        }
        return projection;
    }

    @Override
    public SaveCheckpoint saveCheckpoint(SaveCheckpoint checkpoint) {
        SaveCheckpoint saved = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.save_checkpoint (
                    player_id,
                    save_version,
                    game_version,
                    content_release_id,
                    revision,
                    snapshot_json,
                    checksum
                )
                VALUES (?, ?, ?, ?, ?, CAST(? AS jsonb), ?)
                RETURNING checkpoint_id, public_id
                """,
            (resultSet, rowNumber) -> new SaveCheckpoint(
                resultSet.getLong("checkpoint_id"),
                resultSet.getObject("public_id", UUID.class),
                checkpoint.playerId(),
                checkpoint.saveVersion(),
                checkpoint.gameVersion(),
                checkpoint.contentReleaseId(),
                checkpoint.revision(),
                checkpoint.snapshotJson(),
                checkpoint.checksum()
            ),
            checkpoint.playerId(),
            checkpoint.saveVersion(),
            checkpoint.gameVersion(),
            checkpoint.contentReleaseId(),
            checkpoint.revision(),
            checkpoint.snapshotJson(),
            checkpoint.checksum()
        );
        if (saved == null) {
            throw new IllegalStateException("save checkpoint insert returned no row");
        }
        return saved;
    }
}
