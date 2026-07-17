package com.kingdomtycoon.server.content;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcActiveContentRepository implements ActiveContentRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcActiveContentRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public ActiveContentVersion findActive(String channelCode) {
        ActiveContentVersion version = jdbcTemplate.queryForObject(
            """
                SELECT channel.channel_code,
                       channel.active_release_id,
                       release.release_version,
                       channel.version
                  FROM master.content_channel channel
                  JOIN master.content_release release
                    ON release.release_id = channel.active_release_id
                 WHERE channel.channel_code = ?
                """,
            (resultSet, rowNumber) -> new ActiveContentVersion(
                resultSet.getString("channel_code"),
                resultSet.getLong("active_release_id"),
                resultSet.getString("release_version"),
                resultSet.getLong("version")
            ),
            channelCode
        );
        if (version == null) {
            throw new IllegalArgumentException("content channel does not exist: " + channelCode);
        }
        return version;
    }

    @Override
    public ContentSnapshot loadSnapshot(long releaseId) {
        String releaseVersion = jdbcTemplate.queryForObject(
            "SELECT release_version FROM master.content_release WHERE release_id = ?",
            String.class,
            releaseId
        );
        if (releaseVersion == null) {
            throw new IllegalArgumentException("content release does not exist: " + releaseId);
        }
        List<Map.Entry<String, String>> configEntries = jdbcTemplate.query(
            """
                SELECT config_key, config_value::text
                  FROM master.runtime_config_value
                 WHERE release_id = ?
                   AND enabled
                 ORDER BY config_key
                """,
            (resultSet, rowNumber) -> Map.entry(resultSet.getString(1), resultSet.getString(2)),
            releaseId
        );
        Map<String, String> configs = new LinkedHashMap<>();
        configEntries.forEach(entry -> configs.put(entry.getKey(), entry.getValue()));

        List<String> checksumRows = new ArrayList<>();
        appendChecksumRows(checksumRows, "runtime_config_value", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT config_key, config_value, enabled
                      FROM master.runtime_config_value
                     WHERE release_id = ?
                     ORDER BY config_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "job_balance", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT job.job_key, balance.base_hp, balance.base_attack,
                           balance.base_defense, balance.base_speed,
                           balance.critical_rate, balance.growth_rate, balance.enabled
                      FROM master.job_balance balance
                      JOIN master.job job ON job.job_id = balance.job_id
                     WHERE balance.release_id = ?
                     ORDER BY job.job_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "mercenary_balance", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT template.mercenary_key, job.job_key, balance.rarity,
                           balance.base_hp, balance.base_attack, balance.base_defense,
                           balance.growth_rate, balance.summonable, balance.enabled
                      FROM master.mercenary_balance balance
                      JOIN master.mercenary_template template
                        ON template.mercenary_template_id = balance.mercenary_template_id
                      JOIN master.job job ON job.job_id = template.job_id
                     WHERE balance.release_id = ?
                     ORDER BY template.mercenary_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "skill_balance", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT skill.skill_key, skill.skill_type, balance.skill_level,
                           balance.cooldown_seconds, balance.power_rate,
                           balance.range_value, balance.duration_seconds,
                           balance.cost_amount, balance.enabled
                      FROM master.skill_balance balance
                      JOIN master.skill skill ON skill.skill_id = balance.skill_id
                     WHERE balance.release_id = ?
                     ORDER BY skill.skill_key, balance.skill_level
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "mercenary_skill_map", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT template.mercenary_key, skill.skill_key,
                           mapping.unlock_level, mapping.slot_no, mapping.enabled
                      FROM master.mercenary_skill_map mapping
                      JOIN master.mercenary_template template
                        ON template.mercenary_template_id = mapping.mercenary_template_id
                      JOIN master.skill skill ON skill.skill_id = mapping.skill_id
                     WHERE mapping.release_id = ?
                     ORDER BY template.mercenary_key, skill.skill_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "item_balance", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT item.item_key, item.item_type, balance.max_stack,
                           balance.sell_currency_key, balance.sell_amount,
                           balance.usable, balance.enabled
                      FROM master.item_balance balance
                      JOIN master.item item ON item.item_id = balance.item_id
                     WHERE balance.release_id = ?
                     ORDER BY item.item_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "equipment_balance", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT template.equipment_key, template.equipment_slot,
                           balance.grade, balance.base_stat_code,
                           balance.base_stat_value, balance.level_requirement,
                           balance.enabled
                      FROM master.equipment_balance balance
                      JOIN master.equipment_template template
                        ON template.equipment_template_id = balance.equipment_template_id
                     WHERE balance.release_id = ?
                     ORDER BY template.equipment_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "reward_entry", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT reward_group.reward_group_key, entry.entry_no,
                           entry.reward_type, entry.reward_key, entry.quantity,
                           entry.probability, entry.weight, entry.enabled
                      FROM master.reward_entry entry
                      JOIN master.reward_group reward_group
                        ON reward_group.reward_group_id = entry.reward_group_id
                     WHERE entry.release_id = ?
                     ORDER BY reward_group.reward_group_key, entry.entry_no
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "summon_banner", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT banner_key, pity_group_key, status, starts_at, ends_at,
                           currency_key, cost_amount, pull_unit, pity_threshold,
                           guaranteed_rarity, minimum_client_version, enabled
                      FROM ops.summon_banner
                     WHERE release_id = ?
                     ORDER BY banner_key
                   ) content_row
            """, releaseId);
        appendChecksumRows(checksumRows, "summon_pool", """
            SELECT to_jsonb(content_row)::text AS checksum_row
              FROM (
                    SELECT banner.banner_key, pool.pool_group, pool.result_no,
                           template.mercenary_key, pool.rarity, pool.weight, pool.enabled
                      FROM ops.summon_pool pool
                      JOIN ops.summon_banner banner ON banner.banner_id = pool.banner_id
                      JOIN master.mercenary_template template
                        ON template.mercenary_template_id = pool.mercenary_template_id
                     WHERE banner.release_id = ?
                     ORDER BY banner.banner_key, pool.pool_group, pool.result_no
                   ) content_row
            """, releaseId);

        return new ContentSnapshot(releaseId, releaseVersion, configs, checksumRows);
    }

    private void appendChecksumRows(
        List<String> checksumRows,
        String contentType,
        String query,
        long releaseId
    ) {
        jdbcTemplate.query(
            query,
            (resultSet, rowNumber) -> resultSet.getString("checksum_row"),
            releaseId
        ).forEach(row -> checksumRows.add(contentType + "|" + row));
    }
}
