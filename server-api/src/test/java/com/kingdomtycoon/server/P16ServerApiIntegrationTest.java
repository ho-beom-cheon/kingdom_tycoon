package com.kingdomtycoon.server;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.kingdomtycoon.server.content.ActiveContentRepository;
import com.kingdomtycoon.server.content.ContentReleaseApplicationService;
import com.kingdomtycoon.server.wallet.WalletApplicationService;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.MOCK,
    properties = "tycoon.dev-session.enabled=true"
)
@AutoConfigureMockMvc
class P16ServerApiIntegrationTest extends PostgresIntegrationTest {

    private static final long SYSTEM_ACTOR_ID = 1L;

    @Autowired
    MockMvc mockMvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Autowired
    WalletApplicationService walletService;

    @Autowired
    ActiveContentRepository activeContentRepository;

    @Autowired
    ContentReleaseApplicationService contentReleaseService;

    @Test
    void developmentSessionReusesPlayerAndWalletRequiresBearerToken() throws Exception {
        String deviceId = "p16-device-" + UUID.randomUUID();
        Session first = createSession(deviceId);
        Session second = createSession(deviceId);

        assertThat(second.playerId()).isEqualTo(first.playerId());
        assertThat(second.accessToken()).isNotEqualTo(first.accessToken());

        mockMvc.perform(get("/players/me/wallet"))
            .andExpect(status().isUnauthorized())
            .andExpect(jsonPath("$.code").value("AUTH_INVALID"))
            .andExpect(jsonPath("$.messageKo").isNotEmpty())
            .andExpect(jsonPath("$.traceId").isNotEmpty());

        mockMvc.perform(get("/players/me/wallet").header("Authorization", "Bearer " + first.accessToken()))
            .andExpect(status().isUnauthorized());

        mockMvc.perform(get("/players/me/wallet").header("Authorization", "Bearer " + second.accessToken()))
            .andExpect(status().isOk())
            .andExpect(jsonPath("$.data.playerId").value(first.playerId()))
            .andExpect(jsonPath("$.data.balances.length()").value(4))
            .andExpect(jsonPath("$.data.balances[0].currencyKey").value("FREE_GEM"))
            .andExpect(header().exists("X-Trace-Id"));
    }

    @Test
    void specialRecruitmentIsAtomicAndIdempotentThroughHttpBoundary() throws Exception {
        Session session = createSession("p16-recruit-" + UUID.randomUUID());
        long internalPlayerId = jdbcTemplate.queryForObject(
            "SELECT player_id FROM game.player_account WHERE public_id = ?",
            Long.class,
            UUID.fromString(session.playerId())
        );
        Fixture fixture = publishSummonFixture();
        try {
            walletService.change(
                internalPlayerId,
                "SPECIAL_TICKET",
                20,
                "TEST_CREDIT",
                "P16_API_TEST",
                fixture.bannerKey(),
                UUID.randomUUID()
            );
            jdbcTemplate.update(
                "INSERT INTO game.summon_pity (player_id, pity_group_key, pull_count) VALUES (?, ?, 1)",
                internalPlayerId,
                fixture.pityGroupKey()
            );

            UUID requestId = UUID.randomUUID();
            String body = "{\"bannerKey\":\"" + fixture.bannerKey() + "\",\"pullCount\":1}";
            String first = mockMvc.perform(post("/players/me/recruitments/special")
                    .header("Authorization", "Bearer " + session.accessToken())
                    .header("Idempotency-Key", requestId)
                    .contentType("application/json")
                    .content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data.replayed").value(false))
                .andExpect(jsonPath("$.data.results[0].jobKey").isNotEmpty())
                .andExpect(jsonPath("$.data.results[0].guaranteed").value(true))
                .andExpect(jsonPath("$.data.walletAfter[3].currencyKey").value("SPECIAL_TICKET"))
                .andExpect(jsonPath("$.data.walletAfter[3].balance").value(10))
                .andReturn().getResponse().getContentAsString();
            String receiptId = objectMapper.readTree(first).at("/data/receiptId").asText();

            mockMvc.perform(post("/players/me/recruitments/special")
                    .header("Authorization", "Bearer " + session.accessToken())
                    .header("Idempotency-Key", requestId)
                    .contentType("application/json")
                    .content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.data.replayed").value(true))
                .andExpect(jsonPath("$.data.receiptId").value(receiptId));

            mockMvc.perform(post("/players/me/recruitments/special")
                    .header("Authorization", "Bearer " + session.accessToken())
                    .header("Idempotency-Key", requestId)
                    .contentType("application/json")
                    .content("{\"bannerKey\":\"" + fixture.bannerKey() + "\",\"pullCount\":2}"))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.code").value("IDEMPOTENCY_CONFLICT"));

            Integer transactions = jdbcTemplate.queryForObject(
                "SELECT count(*) FROM game.summon_transaction WHERE player_id = ? AND request_id = ?",
                Integer.class,
                internalPlayerId,
                requestId
            );
            assertThat(transactions).isOne();
        } finally {
            rollbackFixture(fixture);
        }
    }

    @Test
    void missingIdempotencyKeyAndInvalidPayloadUseStableKoreanErrors() throws Exception {
        Session session = createSession("p16-error-" + UUID.randomUUID());
        mockMvc.perform(post("/players/me/recruitments/special")
                .header("Authorization", "Bearer " + session.accessToken())
                .contentType("application/json")
                .content("{\"bannerKey\":\"invalid-key\",\"pullCount\":0}"))
            .andExpect(status().isBadRequest())
            .andExpect(jsonPath("$.code").value("VALIDATION_FAILED"))
            .andExpect(jsonPath("$.messageKo").value("입력값을 확인해 주세요."))
            .andExpect(jsonPath("$.fieldErrors.length()").value(2));
    }

    private Session createSession(String deviceId) throws Exception {
        String nickname = "영주" + Integer.toUnsignedString(deviceId.hashCode(), 36);
        String response = mockMvc.perform(post("/dev/sessions")
                .contentType("application/json")
                .content("{\"deviceInstallId\":\"" + deviceId + "\",\"locale\":\"ko-KR\",\"nickname\":\"" + nickname + "\"}"))
            .andExpect(status().isCreated())
            .andExpect(jsonPath("$.data.playerId").isNotEmpty())
            .andExpect(jsonPath("$.data.accessToken").isNotEmpty())
            .andExpect(jsonPath("$.data.contentVersion").isNotEmpty())
            .andReturn().getResponse().getContentAsString();
        JsonNode data = objectMapper.readTree(response).path("data");
        return new Session(data.path("playerId").asText(), data.path("accessToken").asText());
    }

    private Fixture publishSummonFixture() {
        var active = activeContentRepository.findActive("PRODUCTION");
        String suffix = UUID.randomUUID().toString().replace("-", "").substring(0, 8).toUpperCase();
        var draft = contentReleaseService.createDraft(
            active.releaseId(),
            "1.0.0-p16-" + suffix,
            "P16 API Test Draft",
            "1.0.0",
            SYSTEM_ACTOR_ID
        );

        Long firstJobId = null;
        for (int index = 1; index <= 5; index++) {
            Long jobId = jdbcTemplate.queryForObject(
                "INSERT INTO master.job (job_key) VALUES (?) RETURNING job_id",
                Long.class,
                "JOB_P16_" + suffix + "_" + index
            );
            if (firstJobId == null) {
                firstJobId = jobId;
            }
            jdbcTemplate.update(
                """
                    INSERT INTO master.job_balance (
                        release_id, job_id, base_hp, base_attack, base_defense,
                        base_speed, critical_rate, growth_rate, created_by
                    ) VALUES (?, ?, 100, 10, 5, 1.0, 0.1, 1.0, ?)
                    """,
                draft.releaseId(), jobId, SYSTEM_ACTOR_ID
            );
        }

        String mercenaryKey = "MERC_P16_" + suffix;
        Long templateId = jdbcTemplate.queryForObject(
            "INSERT INTO master.mercenary_template (mercenary_key, job_id) VALUES (?, ?) RETURNING mercenary_template_id",
            Long.class,
            mercenaryKey,
            firstJobId
        );
        jdbcTemplate.update(
            """
                INSERT INTO master.mercenary_balance (
                    release_id, mercenary_template_id, rarity, base_hp, base_attack,
                    base_defense, growth_rate, summonable, created_by
                ) VALUES (?, ?, 'SS', 120, 15, 7, 1.1, true, ?)
                """,
            draft.releaseId(), templateId, SYSTEM_ACTOR_ID
        );

        String bannerKey = "BANNER_P16_" + suffix;
        String pityGroupKey = "PITY_P16_" + suffix;
        Long bannerId = jdbcTemplate.queryForObject(
            """
                INSERT INTO ops.summon_banner (
                    banner_key, release_id, pity_group_key, status, starts_at, ends_at,
                    currency_key, cost_amount, pull_unit, pity_threshold,
                    guaranteed_rarity, minimum_client_version, enabled
                ) VALUES (?, ?, ?, 'ACTIVE', now() - interval '1 hour', now() + interval '1 day',
                          'SPECIAL_TICKET', 10, 1, 2, 'SS', '1.0.0', true)
                RETURNING banner_id
                """,
            Long.class,
            bannerKey,
            draft.releaseId(),
            pityGroupKey
        );
        jdbcTemplate.update(
            "INSERT INTO ops.summon_pool (banner_id, pool_group, result_no, mercenary_template_id, rarity, weight) VALUES (?, 'DEFAULT', 1, ?, 'SS', 100)",
            bannerId,
            templateId
        );
        assertThat(contentReleaseService.validate(draft.releaseId())).isZero();
        contentReleaseService.approve(draft.releaseId(), SYSTEM_ACTOR_ID);
        contentReleaseService.publish(
            "PRODUCTION", draft.releaseId(), active.channelVersion(), SYSTEM_ACTOR_ID, "P16 API 통합 테스트"
        );
        return new Fixture(draft.releaseId(), active.releaseId(), bannerKey, pityGroupKey);
    }

    private void rollbackFixture(Fixture fixture) {
        var active = activeContentRepository.findActive("PRODUCTION");
        if (active.releaseId() == fixture.releaseId()) {
            contentReleaseService.rollback(
                "PRODUCTION", fixture.previousReleaseId(), active.channelVersion(), SYSTEM_ACTOR_ID, "P16 API fixture rollback"
            );
        }
    }

    private record Session(String playerId, String accessToken) {
    }

    private record Fixture(long releaseId, long previousReleaseId, String bannerKey, String pityGroupKey) {
    }
}
