package com.kingdomtycoon.server.content;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcMasterDataProvider implements MasterDataProvider {

    private final ActiveContentRepository activeContentRepository;
    private final JdbcTemplate jdbcTemplate;

    public JdbcMasterDataProvider(ActiveContentRepository activeContentRepository, JdbcTemplate jdbcTemplate) {
        this.activeContentRepository = activeContentRepository;
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public long activeReleaseId() {
        return activeContentRepository.findActive("PRODUCTION").releaseId();
    }

    @Override
    public String getConfig(String configKey) {
        String value = jdbcTemplate.queryForObject(
            """
                SELECT config_value::text
                  FROM master.runtime_config_value
                 WHERE release_id = ?
                   AND config_key = ?
                   AND enabled
                """,
            String.class,
            activeReleaseId(),
            configKey
        );
        if (value == null) {
            throw new IllegalArgumentException("runtime config does not exist: " + configKey);
        }
        return value;
    }

    @Override
    public JobDefinition getJob(String jobKey) {
        return required(
            jdbcTemplate.queryForObject(
                """
                    SELECT job.job_key, balance.base_hp, balance.base_attack, balance.base_defense
                      FROM master.job job
                      JOIN master.job_balance balance ON balance.job_id = job.job_id
                     WHERE balance.release_id = ? AND job.job_key = ? AND balance.enabled
                    """,
                (resultSet, rowNumber) -> new JobDefinition(
                    resultSet.getString("job_key"),
                    resultSet.getLong("base_hp"),
                    resultSet.getLong("base_attack"),
                    resultSet.getLong("base_defense")
                ),
                activeReleaseId(),
                jobKey
            ),
            "job",
            jobKey
        );
    }

    @Override
    public MercenaryDefinition getMercenary(String mercenaryKey) {
        return required(
            jdbcTemplate.queryForObject(
                """
                    SELECT template.mercenary_key, job.job_key, balance.rarity, balance.summonable
                      FROM master.mercenary_template template
                      JOIN master.job job ON job.job_id = template.job_id
                      JOIN master.mercenary_balance balance
                        ON balance.mercenary_template_id = template.mercenary_template_id
                     WHERE balance.release_id = ?
                       AND template.mercenary_key = ?
                       AND balance.enabled
                    """,
                (resultSet, rowNumber) -> new MercenaryDefinition(
                    resultSet.getString("mercenary_key"),
                    resultSet.getString("job_key"),
                    resultSet.getString("rarity"),
                    resultSet.getBoolean("summonable")
                ),
                activeReleaseId(),
                mercenaryKey
            ),
            "mercenary",
            mercenaryKey
        );
    }

    @Override
    public ItemDefinition getItem(String itemKey) {
        return required(
            jdbcTemplate.queryForObject(
                """
                    SELECT item.item_key, item.item_type, balance.max_stack
                      FROM master.item item
                      JOIN master.item_balance balance ON balance.item_id = item.item_id
                     WHERE balance.release_id = ? AND item.item_key = ? AND balance.enabled
                    """,
                (resultSet, rowNumber) -> new ItemDefinition(
                    resultSet.getString("item_key"),
                    resultSet.getString("item_type"),
                    resultSet.getLong("max_stack")
                ),
                activeReleaseId(),
                itemKey
            ),
            "item",
            itemKey
        );
    }

    @Override
    public EquipmentDefinition getEquipment(String equipmentKey) {
        return required(
            jdbcTemplate.queryForObject(
                """
                    SELECT template.equipment_key,
                           template.equipment_slot,
                           balance.grade,
                           balance.base_stat_code
                      FROM master.equipment_template template
                      JOIN master.equipment_balance balance
                        ON balance.equipment_template_id = template.equipment_template_id
                     WHERE balance.release_id = ?
                       AND template.equipment_key = ?
                       AND balance.enabled
                    """,
                (resultSet, rowNumber) -> new EquipmentDefinition(
                    resultSet.getString("equipment_key"),
                    resultSet.getString("equipment_slot"),
                    resultSet.getString("grade"),
                    resultSet.getString("base_stat_code")
                ),
                activeReleaseId(),
                equipmentKey
            ),
            "equipment",
            equipmentKey
        );
    }

    private static <T> T required(T value, String type, String key) {
        if (value == null) {
            throw new IllegalArgumentException(type + " does not exist: " + key);
        }
        return value;
    }
}
