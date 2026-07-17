package com.kingdomtycoon.server.content;

import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcContentReleaseRepository implements ContentReleaseRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcContentReleaseRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public ContentRelease createDraft(
        long baseReleaseId,
        String releaseVersion,
        String releaseName,
        String minimumClientVersion,
        long actorId
    ) {
        Long releaseId = jdbcTemplate.queryForObject(
            """
                INSERT INTO master.content_release (
                    release_version,
                    release_name,
                    status,
                    base_release_id,
                    minimum_client_version,
                    created_by
                )
                VALUES (?, ?, 'DRAFT', ?, ?, ?)
                RETURNING release_id
                """,
            Long.class,
            releaseVersion,
            releaseName,
            baseReleaseId,
            minimumClientVersion,
            actorId
        );
        if (releaseId == null) {
            throw new IllegalStateException("content release insert returned no id");
        }

        copyRevision("runtime_config_value", "config_key, config_value, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("job_balance", "job_id, base_hp, base_attack, base_defense, base_speed, critical_rate, growth_rate, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("mercenary_balance", "mercenary_template_id, rarity, base_hp, base_attack, base_defense, growth_rate, summonable, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("skill_balance", "skill_id, skill_level, cooldown_seconds, power_rate, range_value, duration_seconds, cost_amount, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("mercenary_skill_map", "mercenary_template_id, skill_id, unlock_level, slot_no, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("item_balance", "item_id, max_stack, sell_currency_key, sell_amount, usable, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("equipment_balance", "equipment_template_id, grade, base_stat_code, base_stat_value, level_requirement, enabled", releaseId, baseReleaseId, actorId);
        copyRevision("reward_entry", "reward_group_id, entry_no, reward_type, reward_key, quantity, probability, weight, enabled", releaseId, baseReleaseId, actorId);

        return find(releaseId);
    }

    private void copyRevision(String tableName, String copiedColumns, long releaseId, long baseReleaseId, long actorId) {
        jdbcTemplate.update(
            "INSERT INTO master.%s (release_id, %s, created_by) SELECT ?, %s, ? FROM master.%s WHERE release_id = ?"
                .formatted(tableName, copiedColumns, copiedColumns, tableName),
            releaseId,
            actorId,
            baseReleaseId
        );
    }

    @Override
    public int validate(long releaseId) {
        Integer errors = jdbcTemplate.queryForObject(
            "SELECT master.validate_content_release(?)",
            Integer.class,
            releaseId
        );
        return errors == null ? 0 : errors;
    }

    @Override
    public void approve(long releaseId, long actorId, String checksum) {
        int updated = jdbcTemplate.update(
            """
                UPDATE master.content_release
                   SET status = 'APPROVED',
                       approved_by = ?,
                       approved_at = now(),
                       checksum = ?
                 WHERE release_id = ?
                   AND status = 'DRAFT'
                """,
            actorId,
            checksum,
            releaseId
        );
        if (updated != 1) {
            throw new IllegalStateException("release is not approvable");
        }
    }

    @Override
    public ContentRelease find(long releaseId) {
        ContentRelease release = jdbcTemplate.queryForObject(
            """
                SELECT release_id, release_version, status, minimum_client_version, base_release_id, checksum
                  FROM master.content_release
                 WHERE release_id = ?
                """,
            (resultSet, rowNumber) -> new ContentRelease(
                resultSet.getLong("release_id"),
                resultSet.getString("release_version"),
                resultSet.getString("status"),
                resultSet.getString("minimum_client_version"),
                resultSet.getObject("base_release_id", Long.class),
                resultSet.getString("checksum")
            ),
            releaseId
        );
        if (release == null) {
            throw new IllegalArgumentException("release does not exist: " + releaseId);
        }
        return release;
    }

    @Override
    public ChannelState lockChannel(String channelCode) {
        ChannelState state = jdbcTemplate.queryForObject(
            """
                SELECT channel_code, active_release_id, previous_release_id, version
                  FROM master.content_channel
                 WHERE channel_code = ?
                 FOR UPDATE
                """,
            (resultSet, rowNumber) -> new ChannelState(
                resultSet.getString("channel_code"),
                resultSet.getLong("active_release_id"),
                resultSet.getObject("previous_release_id", Long.class),
                resultSet.getLong("version")
            ),
            channelCode
        );
        if (state == null) {
            throw new IllegalArgumentException("channel does not exist: " + channelCode);
        }
        return state;
    }

    @Override
    public int validationErrorCount(long releaseId) {
        Integer count = jdbcTemplate.queryForObject(
            """
                SELECT count(*)
                  FROM master.content_validation_result
                 WHERE release_id = ?
                   AND severity = 'ERROR'
                   AND NOT passed
                """,
            Integer.class,
            releaseId
        );
        return count == null ? 0 : count;
    }

    @Override
    public boolean validationCompleted(long releaseId) {
        Boolean completed = jdbcTemplate.queryForObject(
            """
                SELECT EXISTS (
                    SELECT 1
                      FROM master.content_validation_result
                     WHERE release_id = ?
                       AND validation_code = 'VALIDATION_COMPLETED'
                       AND passed
                )
                """,
            Boolean.class,
            releaseId
        );
        return Boolean.TRUE.equals(completed);
    }

    @Override
    public void archive(long releaseId) {
        jdbcTemplate.update(
            "UPDATE master.content_release SET status = 'ARCHIVED' WHERE release_id = ? AND status = 'PUBLISHED'",
            releaseId
        );
    }

    @Override
    public void markPublished(long releaseId) {
        int updated = jdbcTemplate.update(
            """
                UPDATE master.content_release
                   SET status = 'PUBLISHED', published_at = COALESCE(published_at, now())
                 WHERE release_id = ?
                   AND status IN ('APPROVED', 'SCHEDULED', 'ARCHIVED', 'ROLLED_BACK')
                """,
            releaseId
        );
        if (updated != 1) {
            throw new IllegalStateException("release cannot be published");
        }
    }

    @Override
    public void markRolledBack(long releaseId) {
        jdbcTemplate.update(
            "UPDATE master.content_release SET status = 'ROLLED_BACK' WHERE release_id = ? AND status = 'PUBLISHED'",
            releaseId
        );
    }

    @Override
    public void switchChannel(
        String channelCode,
        long fromReleaseId,
        long toReleaseId,
        long expectedVersion,
        long actorId
    ) {
        int updated = jdbcTemplate.update(
            """
                UPDATE master.content_channel
                   SET previous_release_id = ?,
                       active_release_id = ?,
                       version = version + 1,
                       updated_by = ?,
                       updated_at = now()
                 WHERE channel_code = ?
                   AND active_release_id = ?
                   AND version = ?
                """,
            fromReleaseId,
            toReleaseId,
            actorId,
            channelCode,
            fromReleaseId,
            expectedVersion
        );
        if (updated != 1) {
            throw new IllegalStateException("content channel optimistic lock failed");
        }
    }

    @Override
    public void appendPublishHistory(
        String channelCode,
        Long fromReleaseId,
        long toReleaseId,
        String actionType,
        String reason,
        long actorId
    ) {
        jdbcTemplate.update(
            """
                INSERT INTO master.content_publish_history
                    (channel_code, from_release_id, to_release_id, action_type, reason, actor_id)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
            channelCode,
            fromReleaseId,
            toReleaseId,
            actionType,
            reason,
            actorId
        );
    }

    @Override
    public void appendAdminAudit(
        long actorId,
        String actionType,
        String targetId,
        String reason,
        String beforeState,
        String afterState
    ) {
        jdbcTemplate.update(
            """
                INSERT INTO audit.admin_action_log (
                    actor_id,
                    action_type,
                    target_type,
                    target_id,
                    reason,
                    before_state,
                    after_state,
                    correlation_id
                )
                VALUES (?, ?, 'CONTENT_CHANNEL', ?, ?, CAST(? AS jsonb), CAST(? AS jsonb), ?)
                """,
            actorId,
            actionType,
            targetId,
            reason,
            beforeState,
            afterState,
            UUID.randomUUID()
        );
    }
}
