package com.kingdomtycoon.server;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.kingdomtycoon.server.content.ActiveContentRepository;
import com.kingdomtycoon.server.content.ContentReleaseApplicationService;
import com.kingdomtycoon.server.player.PlayerAccountApplicationService;
import com.kingdomtycoon.server.reward.RewardGrantApplicationService;
import com.kingdomtycoon.server.summon.SummonApplicationService;
import com.kingdomtycoon.server.support.RequestHash;
import com.kingdomtycoon.server.wallet.WalletApplicationService;
import java.sql.Connection;
import java.sql.DriverManager;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataAccessException;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.jdbc.core.JdbcTemplate;

class RewardSummonContentIntegrationTest extends PostgresIntegrationTest {

    private static final long SYSTEM_ACTOR_ID = 1L;

    @Autowired
    PlayerAccountApplicationService playerAccountService;

    @Autowired
    RewardGrantApplicationService rewardGrantService;

    @Autowired
    WalletApplicationService walletService;

    @Autowired
    ContentReleaseApplicationService contentReleaseService;

    @Autowired
    ActiveContentRepository activeContentRepository;

    @Autowired
    SummonApplicationService summonService;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    void sameRewardRequestIsGrantedOnceAndWritesWalletLedger() {
        var player = createPlayer("REWARD");
        UUID requestId = UUID.randomUUID();

        var first = rewardGrantService.grantCurrency(
            player.playerId(), requestId, "FREE_GEM", 25, "ADMIN", "TEST_REWARD"
        );
        var replay = rewardGrantService.grantCurrency(
            player.playerId(), requestId, "FREE_GEM", 25, "ADMIN", "TEST_REWARD"
        );

        assertThat(first.replayed()).isFalse();
        assertThat(replay.replayed()).isTrue();
        assertThat(replay.rewardGrantId()).isEqualTo(first.rewardGrantId());
        Long balance = jdbcTemplate.queryForObject(
            "SELECT balance FROM game.wallet WHERE player_id = ? AND currency_key = 'FREE_GEM'",
            Long.class,
            player.playerId()
        );
        Integer grants = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.reward_grant WHERE player_id = ? AND request_id = ?",
            Integer.class,
            player.playerId(),
            requestId
        );
        Integer ledgers = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.wallet_transaction WHERE player_id = ? AND request_id = ?",
            Integer.class,
            player.playerId(),
            requestId
        );
        assertThat(balance).isEqualTo(25);
        assertThat(grants).isOne();
        assertThat(ledgers).isOne();
    }

    @Test
    void validationErrorAndExpectedVersionMismatchBlockPublish() {
        var active = activeContentRepository.findActive("PRODUCTION");
        String suffix = suffix();
        var draft = contentReleaseService.createDraft(
            active.releaseId(),
            "1.0.0-bad-" + suffix,
            "Invalid Test Draft",
            "1.0.0",
            SYSTEM_ACTOR_ID
        );

        int errors = contentReleaseService.validate(draft.releaseId());
        assertThat(errors).isGreaterThan(0);
        assertThatThrownBy(() -> contentReleaseService.approve(draft.releaseId(), SYSTEM_ACTOR_ID))
            .isInstanceOf(IllegalStateException.class)
            .hasMessageContaining("validation errors");
        assertThatThrownBy(() -> contentReleaseService.publish(
            "PRODUCTION",
            draft.releaseId(),
            active.channelVersion() + 1,
            SYSTEM_ACTOR_ID,
            "잘못된 expected version 검증"
        )).isInstanceOf(IllegalStateException.class)
            .hasMessageContaining("version mismatch");
    }

    @Test
    void publishedContentIsImmutableAndRollbackRestoresPreviousReleaseWithAudit() {
        PublishedFixture fixture = publishSummonFixture();
        try {
            assertThatThrownBy(() -> jdbcTemplate.update(
                """
                    UPDATE master.runtime_config_value
                       SET config_value = '15'::jsonb
                     WHERE release_id = ?
                       AND config_key = 'ACTIVE_MERCENARY_LIMIT'
                    """,
                fixture.releaseId()
            )).isInstanceOf(DataAccessException.class)
                .hasMessageContaining("immutable");

            var snapshot = activeContentRepository.loadSnapshot(fixture.releaseId());
            assertThat(snapshot.runtimeConfig()).containsEntry("ACTIVE_MERCENARY_LIMIT", "16");
            assertThat(snapshot.checksumRows())
                .anyMatch(row -> row.startsWith("job_balance|"))
                .anyMatch(row -> row.startsWith("mercenary_balance|"))
                .anyMatch(row -> row.startsWith("summon_banner|"))
                .anyMatch(row -> row.startsWith("summon_pool|"));
            assertThat(jdbcTemplate.queryForObject(
                "SELECT checksum FROM master.content_release WHERE release_id = ?",
                String.class,
                fixture.releaseId()
            )).isEqualTo(RequestHash.sha256(snapshot.checksumMaterial()));

            assertThatThrownBy(() -> jdbcTemplate.update(
                "UPDATE ops.summon_banner SET cost_amount = cost_amount + 1 WHERE banner_id = ?",
                fixture.bannerId()
            )).isInstanceOf(DataAccessException.class)
                .hasMessageContaining("immutable");
            assertThatThrownBy(() -> jdbcTemplate.update(
                "UPDATE ops.summon_pool SET weight = weight + 1 WHERE banner_id = ?",
                fixture.bannerId()
            )).isInstanceOf(DataAccessException.class)
                .hasMessageContaining("immutable");
        } finally {
            rollbackFixture(fixture);
        }

        var activeAfterRollback = activeContentRepository.findActive("PRODUCTION");
        assertThat(activeAfterRollback.releaseId()).isEqualTo(fixture.previousReleaseId());
        Integer histories = jdbcTemplate.queryForObject(
            """
                SELECT count(*)
                  FROM master.content_publish_history
                 WHERE to_release_id IN (?, ?)
                   AND action_type IN ('PUBLISH', 'ROLLBACK')
                """,
            Integer.class,
            fixture.releaseId(),
            fixture.previousReleaseId()
        );
        Integer audits = jdbcTemplate.queryForObject(
            """
                SELECT count(*)
                  FROM audit.admin_action_log
                 WHERE target_id = 'PRODUCTION'
                   AND reason IS NOT NULL
                """,
            Integer.class
        );
        assertThat(histories).isGreaterThanOrEqualTo(2);
        assertThat(audits).isGreaterThanOrEqualTo(2);
    }

    @Test
    void summonCostPityResultAndMercenaryAreAtomicAndIdempotent() {
        PublishedFixture fixture = publishSummonFixture();
        var player = createPlayer("SUMMON");
        try {
            walletService.change(
                player.playerId(),
                "SPECIAL_TICKET",
                100,
                "TEST_CREDIT",
                "TEST",
                "SUMMON",
                UUID.randomUUID()
            );
            jdbcTemplate.update(
                """
                    INSERT INTO game.summon_pity (player_id, pity_group_key, pull_count)
                    VALUES (?, ?, 1)
                    """,
                player.playerId(),
                fixture.pityGroupKey()
            );

            UUID requestId = UUID.randomUUID();
            var first = summonService.summon(player.playerId(), fixture.bannerKey(), (short) 1, requestId);
            var replay = summonService.summon(player.playerId(), fixture.bannerKey(), (short) 1, requestId);

            assertThat(first.replayed()).isFalse();
            assertThat(replay.replayed()).isTrue();
            assertThat(replay.summonTransactionId()).isEqualTo(first.summonTransactionId());
            Long balance = walletBalance(player.playerId(), "SPECIAL_TICKET");
            Integer pity = jdbcTemplate.queryForObject(
                "SELECT pull_count FROM game.summon_pity WHERE player_id = ? AND pity_group_key = ?",
                Integer.class,
                player.playerId(),
                fixture.pityGroupKey()
            );
            Boolean guaranteed = jdbcTemplate.queryForObject(
                "SELECT guaranteed FROM game.summon_result WHERE summon_transaction_id = ?",
                Boolean.class,
                first.summonTransactionId()
            );
            Integer transactionCount = jdbcTemplate.queryForObject(
                "SELECT count(*) FROM game.summon_transaction WHERE player_id = ? AND request_id = ?",
                Integer.class,
                player.playerId(),
                requestId
            );
            assertThat(balance).isEqualTo(90);
            assertThat(pity).isZero();
            assertThat(guaranteed).isTrue();
            assertThat(transactionCount).isOne();

            assertSummonFailureRollsBackAllState(player.playerId(), fixture);
            assertRosterConstraints(player.playerId(), fixture);
        } finally {
            rollbackFixture(fixture);
        }
    }

    @Test
    void outboxSkipLockedPreventsTwoPublishersFromClaimingSameEvent() throws Exception {
        var player = createPlayer("OUTBOX");
        rewardGrantService.grantCurrency(
            player.playerId(), UUID.randomUUID(), "GOLD", 1, "ADMIN", "OUTBOX_TEST"
        );

        try (
            Connection first = DriverManager.getConnection(
                POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword()
            );
            Connection second = DriverManager.getConnection(
                POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword()
            )
        ) {
            first.setAutoCommit(false);
            second.setAutoCommit(false);
            Long firstId = lockOnePendingOutbox(first);
            Long secondId = lockOnePendingOutbox(second);
            assertThat(firstId).isNotNull();
            assertThat(secondId).isNotNull().isNotEqualTo(firstId);
            first.rollback();
            second.rollback();
        }
    }

    private void assertSummonFailureRollsBackAllState(long playerId, PublishedFixture fixture) {
        long balanceBefore = walletBalance(playerId, "SPECIAL_TICKET");
        Integer pityBefore = jdbcTemplate.queryForObject(
            "SELECT pull_count FROM game.summon_pity WHERE player_id = ? AND pity_group_key = ?",
            Integer.class,
            playerId,
            fixture.pityGroupKey()
        );
        Integer mercenaryCountBefore = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.mercenary WHERE player_id = ?",
            Integer.class,
            playerId
        );

        jdbcTemplate.execute(
            """
                CREATE OR REPLACE FUNCTION audit.test_fail_summon_result()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'forced summon result failure';
                END
                $$
                """
        );
        jdbcTemplate.execute(
            """
                CREATE TRIGGER trg_test_fail_summon_result
                BEFORE INSERT ON game.summon_result
                FOR EACH ROW EXECUTE FUNCTION audit.test_fail_summon_result()
                """
        );
        try {
            assertThatThrownBy(() -> summonService.summon(
                playerId,
                fixture.bannerKey(),
                (short) 1,
                UUID.randomUUID()
            )).isInstanceOf(DataAccessException.class)
                .hasMessageContaining("forced summon result failure");
        } finally {
            jdbcTemplate.execute("DROP TRIGGER trg_test_fail_summon_result ON game.summon_result");
            jdbcTemplate.execute("DROP FUNCTION audit.test_fail_summon_result()");
        }

        assertThat(walletBalance(playerId, "SPECIAL_TICKET")).isEqualTo(balanceBefore);
        assertThat(jdbcTemplate.queryForObject(
            "SELECT pull_count FROM game.summon_pity WHERE player_id = ? AND pity_group_key = ?",
            Integer.class,
            playerId,
            fixture.pityGroupKey()
        )).isEqualTo(pityBefore);
        assertThat(jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.mercenary WHERE player_id = ?",
            Integer.class,
            playerId
        )).isEqualTo(mercenaryCountBefore);
    }

    private void assertRosterConstraints(long playerId, PublishedFixture fixture) {
        Long firstMercenaryId = jdbcTemplate.queryForObject(
            "SELECT min(mercenary_id) FROM game.mercenary WHERE player_id = ?",
            Long.class,
            playerId
        );
        Long secondMercenaryId = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.mercenary
                    (player_id, mercenary_template_id, acquired_release_id, rarity)
                VALUES (?, ?, ?, 'SS')
                RETURNING mercenary_id
                """,
            Long.class,
            playerId,
            fixture.mercenaryTemplateId(),
            fixture.releaseId()
        );
        jdbcTemplate.update(
            "INSERT INTO game.roster_slot (player_id, slot_no, mercenary_id) VALUES (?, 1, ?)",
            playerId,
            firstMercenaryId
        );
        assertThatThrownBy(() -> jdbcTemplate.update(
            "INSERT INTO game.roster_slot (player_id, slot_no, mercenary_id) VALUES (?, 17, ?)",
            playerId,
            secondMercenaryId
        )).isInstanceOf(DataIntegrityViolationException.class);
        assertThatThrownBy(() -> jdbcTemplate.update(
            "INSERT INTO game.roster_slot (player_id, slot_no, mercenary_id) VALUES (?, 2, ?)",
            playerId,
            firstMercenaryId
        )).isInstanceOf(DataIntegrityViolationException.class);
    }

    private PublishedFixture publishSummonFixture() {
        var active = activeContentRepository.findActive("PRODUCTION");
        String suffix = suffix();
        var draft = contentReleaseService.createDraft(
            active.releaseId(),
            "1.0.0-ok-" + suffix,
            "Valid Summon Test Draft",
            "1.0.0",
            SYSTEM_ACTOR_ID
        );

        Long firstJobId = null;
        for (int index = 1; index <= 5; index++) {
            Long jobId = jdbcTemplate.queryForObject(
                "INSERT INTO master.job (job_key) VALUES (?) RETURNING job_id",
                Long.class,
                "JOB_" + suffix + "_" + index
            );
            if (firstJobId == null) {
                firstJobId = jobId;
            }
            jdbcTemplate.update(
                """
                    INSERT INTO master.job_balance (
                        release_id, job_id, base_hp, base_attack, base_defense,
                        base_speed, critical_rate, growth_rate, created_by
                    )
                    VALUES (?, ?, 100, 10, 5, 1.0, 0.1, 1.0, ?)
                    """,
                draft.releaseId(),
                jobId,
                SYSTEM_ACTOR_ID
            );
        }

        Long templateId = jdbcTemplate.queryForObject(
            """
                INSERT INTO master.mercenary_template (mercenary_key, job_id)
                VALUES (?, ?)
                RETURNING mercenary_template_id
                """,
            Long.class,
            "MERC_" + suffix,
            firstJobId
        );
        jdbcTemplate.update(
            """
                INSERT INTO master.mercenary_balance (
                    release_id, mercenary_template_id, rarity,
                    base_hp, base_attack, base_defense, growth_rate,
                    summonable, created_by
                )
                VALUES (?, ?, 'SS', 120, 15, 7, 1.1, true, ?)
                """,
            draft.releaseId(),
            templateId,
            SYSTEM_ACTOR_ID
        );

        String bannerKey = "BANNER_" + suffix;
        String pityGroupKey = "PITY_" + suffix;
        Long bannerId = jdbcTemplate.queryForObject(
            """
                INSERT INTO ops.summon_banner (
                    banner_key, release_id, pity_group_key, status,
                    starts_at, ends_at, currency_key, cost_amount,
                    pull_unit, pity_threshold, guaranteed_rarity,
                    minimum_client_version, enabled
                )
                VALUES (?, ?, ?, 'ACTIVE', now() - interval '1 hour', now() + interval '1 day',
                        'SPECIAL_TICKET', 10, 1, 2, 'SS', '1.0.0', true)
                RETURNING banner_id
                """,
            Long.class,
            bannerKey,
            draft.releaseId(),
            pityGroupKey
        );
        jdbcTemplate.update(
            """
                INSERT INTO ops.summon_pool
                    (banner_id, pool_group, result_no, mercenary_template_id, rarity, weight)
                VALUES (?, 'DEFAULT', 1, ?, 'SS', 100)
                """,
            bannerId,
            templateId
        );

        assertThat(contentReleaseService.validate(draft.releaseId())).isZero();
        contentReleaseService.approve(draft.releaseId(), SYSTEM_ACTOR_ID);
        contentReleaseService.publish(
            "PRODUCTION",
            draft.releaseId(),
            active.channelVersion(),
            SYSTEM_ACTOR_ID,
            "Testcontainers 모집 release 배포"
        );
        return new PublishedFixture(
            draft.releaseId(),
            active.releaseId(),
            active.channelVersion() + 1,
            bannerId,
            bannerKey,
            pityGroupKey,
            templateId
        );
    }

    private void rollbackFixture(PublishedFixture fixture) {
        var active = activeContentRepository.findActive("PRODUCTION");
        if (active.releaseId() == fixture.releaseId()) {
            contentReleaseService.rollback(
                "PRODUCTION",
                fixture.previousReleaseId(),
                active.channelVersion(),
                SYSTEM_ACTOR_ID,
                "Testcontainers fixture rollback"
            );
        }
    }

    private Long lockOnePendingOutbox(Connection connection) throws Exception {
        try (var statement = connection.prepareStatement(
            """
                SELECT outbox_event_id
                  FROM audit.outbox_event
                 WHERE published_at IS NULL
                 ORDER BY outbox_event_id
                 FOR UPDATE SKIP LOCKED
                 LIMIT 1
                """
        ); var resultSet = statement.executeQuery()) {
            return resultSet.next() ? resultSet.getLong(1) : null;
        }
    }

    private long walletBalance(long playerId, String currencyKey) {
        Long balance = jdbcTemplate.queryForObject(
            "SELECT balance FROM game.wallet WHERE player_id = ? AND currency_key = ?",
            Long.class,
            playerId,
            currencyKey
        );
        if (balance == null) {
            throw new IllegalStateException("wallet balance missing");
        }
        return balance;
    }

    private PlayerAccountApplicationService.PlayerAccount createPlayer(String prefix) {
        String suffix = suffix().toLowerCase();
        return playerAccountService.create(UUID.randomUUID(), "ko-KR", prefix + "_" + suffix);
    }

    private String suffix() {
        return UUID.randomUUID().toString().replace("-", "").substring(0, 8).toUpperCase();
    }

    private record PublishedFixture(
        long releaseId,
        long previousReleaseId,
        long publishedChannelVersion,
        long bannerId,
        String bannerKey,
        String pityGroupKey,
        long mercenaryTemplateId
    ) {
    }
}
