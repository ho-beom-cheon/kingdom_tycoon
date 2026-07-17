package com.kingdomtycoon.server.summon;

import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcSummonRepository implements SummonRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcSummonRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public Banner findActiveBanner(String bannerKey, String channelCode) {
        Banner banner = jdbcTemplate.queryForObject(
            """
                SELECT banner.banner_id,
                       banner.banner_key,
                       banner.release_id,
                       banner.pity_group_key,
                       banner.currency_key,
                       banner.cost_amount,
                       banner.pull_unit,
                       banner.pity_threshold,
                       banner.guaranteed_rarity
                  FROM ops.summon_banner banner
                  JOIN master.content_channel channel
                    ON channel.active_release_id = banner.release_id
                   AND channel.channel_code = ?
                 WHERE banner.banner_key = ?
                   AND banner.status = 'ACTIVE'
                   AND banner.enabled
                   AND now() >= banner.starts_at
                   AND now() < banner.ends_at
                """,
            (resultSet, rowNumber) -> new Banner(
                resultSet.getLong("banner_id"),
                resultSet.getString("banner_key"),
                resultSet.getLong("release_id"),
                resultSet.getString("pity_group_key"),
                resultSet.getString("currency_key"),
                resultSet.getLong("cost_amount"),
                resultSet.getShort("pull_unit"),
                resultSet.getInt("pity_threshold"),
                resultSet.getString("guaranteed_rarity")
            ),
            channelCode,
            bannerKey
        );
        if (banner == null) {
            throw new IllegalArgumentException("active summon banner does not exist: " + bannerKey);
        }
        return banner;
    }

    @Override
    public Pity lockPity(long playerId, String pityGroupKey) {
        jdbcTemplate.update(
            """
                INSERT INTO game.summon_pity (player_id, pity_group_key)
                VALUES (?, ?)
                ON CONFLICT (player_id, pity_group_key) DO NOTHING
                """,
            playerId,
            pityGroupKey
        );
        Pity pity = jdbcTemplate.queryForObject(
            """
                SELECT player_id, pity_group_key, pull_count, guaranteed_state, version
                  FROM game.summon_pity
                 WHERE player_id = ?
                   AND pity_group_key = ?
                 FOR UPDATE
                """,
            (resultSet, rowNumber) -> new Pity(
                resultSet.getLong("player_id"),
                resultSet.getString("pity_group_key"),
                resultSet.getInt("pull_count"),
                resultSet.getString("guaranteed_state"),
                resultSet.getLong("version")
            ),
            playerId,
            pityGroupKey
        );
        if (pity == null) {
            throw new IllegalStateException("summon pity row disappeared");
        }
        return pity;
    }

    @Override
    public void savePity(Pity pity) {
        int updated = jdbcTemplate.update(
            """
                UPDATE game.summon_pity
                   SET pull_count = ?,
                       guaranteed_state = ?,
                       version = version + 1,
                       updated_at = now()
                 WHERE player_id = ?
                   AND pity_group_key = ?
                   AND version = ?
                """,
            pity.pullCount(),
            pity.guaranteedState(),
            pity.playerId(),
            pity.pityGroupKey(),
            pity.version()
        );
        if (updated != 1) {
            throw new IllegalStateException("summon pity optimistic lock failed");
        }
    }

    @Override
    public List<PoolEntry> loadPool(long bannerId, boolean guaranteed, String guaranteedRarity) {
        return jdbcTemplate.query(
            """
                SELECT pool.mercenary_template_id,
                       template.mercenary_key,
                       pool.rarity,
                       pool.weight
                  FROM ops.summon_pool pool
                  JOIN master.mercenary_template template
                    ON template.mercenary_template_id = pool.mercenary_template_id
                 WHERE pool.banner_id = ?
                   AND pool.enabled
                   AND (NOT ? OR pool.rarity = ?)
                 ORDER BY pool.pool_group, pool.result_no
                """,
            (resultSet, rowNumber) -> new PoolEntry(
                resultSet.getLong("mercenary_template_id"),
                resultSet.getString("mercenary_key"),
                resultSet.getString("rarity"),
                resultSet.getLong("weight")
            ),
            bannerId,
            guaranteed,
            guaranteedRarity
        );
    }

    @Override
    public CreatedMercenary createMercenary(long playerId, long templateId, long releaseId, String rarity) {
        CreatedMercenary mercenary = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.mercenary
                    (player_id, mercenary_template_id, acquired_release_id, rarity)
                VALUES (?, ?, ?, ?)
                RETURNING mercenary_id, public_id
                """,
            (resultSet, rowNumber) -> new CreatedMercenary(
                resultSet.getLong("mercenary_id"),
                resultSet.getObject("public_id", UUID.class)
            ),
            playerId,
            templateId,
            releaseId,
            rarity
        );
        if (mercenary == null) {
            throw new IllegalStateException("mercenary insert returned no row");
        }
        return mercenary;
    }

    @Override
    public long insertTransaction(
        UUID requestId,
        long playerId,
        long bannerId,
        long releaseId,
        short pullCount,
        long walletTransactionId,
        String randomTraceHash
    ) {
        Long id = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.summon_transaction (
                    request_id,
                    player_id,
                    banner_id,
                    release_id,
                    pull_count,
                    cost_wallet_transaction_id,
                    random_trace_hash
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                RETURNING summon_transaction_id
                """,
            Long.class,
            requestId,
            playerId,
            bannerId,
            releaseId,
            pullCount,
            walletTransactionId,
            randomTraceHash
        );
        if (id == null) {
            throw new IllegalStateException("summon transaction insert returned no id");
        }
        return id;
    }

    @Override
    public void insertResult(
        long summonTransactionId,
        short resultNo,
        PoolEntry poolEntry,
        boolean guaranteed,
        UUID createdEntityPublicId
    ) {
        jdbcTemplate.update(
            """
                INSERT INTO game.summon_result (
                    summon_transaction_id,
                    result_no,
                    result_type,
                    result_key,
                    rarity,
                    guaranteed,
                    created_entity_public_id
                )
                VALUES (?, ?, 'MERCENARY', ?, ?, ?, ?)
                """,
            summonTransactionId,
            resultNo,
            poolEntry.mercenaryKey(),
            poolEntry.rarity(),
            guaranteed,
            createdEntityPublicId
        );
    }

    @Override
    public Optional<SummonTransaction> findByRequestId(long playerId, UUID requestId) {
        List<SummonTransaction> transactions = jdbcTemplate.query(
            """
                SELECT summon_transaction_id, pull_count
                  FROM game.summon_transaction
                 WHERE player_id = ?
                   AND request_id = ?
                """,
            (resultSet, rowNumber) -> new SummonTransaction(
                resultSet.getLong("summon_transaction_id"),
                resultSet.getShort("pull_count")
            ),
            playerId,
            requestId
        );
        return transactions.stream().findFirst();
    }
}
