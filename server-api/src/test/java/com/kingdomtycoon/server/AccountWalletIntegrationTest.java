package com.kingdomtycoon.server;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.kingdomtycoon.server.content.ActiveContentRepository;
import com.kingdomtycoon.server.idempotency.IdempotencyConflictException;
import com.kingdomtycoon.server.player.PlayerAccountApplicationService;
import com.kingdomtycoon.server.player.PlayerStateRepository;
import com.kingdomtycoon.server.support.RequestHash;
import com.kingdomtycoon.server.wallet.InsufficientBalanceException;
import com.kingdomtycoon.server.wallet.WalletApplicationService;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;

class AccountWalletIntegrationTest extends PostgresIntegrationTest {

    @Autowired
    PlayerAccountApplicationService playerAccountService;

    @Autowired
    PlayerStateRepository playerStateRepository;

    @Autowired
    ActiveContentRepository activeContentRepository;

    @Autowired
    WalletApplicationService walletService;

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    void accountProfileSettingsWalletsAndSaveCheckpointAreCreatedWithVersions() {
        var player = createPlayer("ACCOUNT");

        Integer profileCount = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.player_profile WHERE player_id = ?",
            Integer.class,
            player.playerId()
        );
        Integer settingCount = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.player_setting WHERE player_id = ?",
            Integer.class,
            player.playerId()
        );
        Integer walletCount = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.wallet WHERE player_id = ?",
            Integer.class,
            player.playerId()
        );
        assertThat(profileCount).isOne();
        assertThat(settingCount).isOne();
        assertThat(walletCount).isEqualTo(4);

        var state = playerStateRepository.loadPlayerState(player.playerId());
        assertThat(state.nickname()).isEqualTo(player.nickname());
        long releaseId = activeContentRepository.findActive("PRODUCTION").releaseId();
        String snapshot = "{\"playerId\":%d}".formatted(player.playerId());
        var saved = playerStateRepository.saveCheckpoint(
            new PlayerStateRepository.SaveCheckpoint(
                player.playerId(),
                1,
                "0.1.0",
                releaseId,
                1,
                snapshot,
                RequestHash.sha256(snapshot)
            )
        );
        assertThat(saved.publicId()).isNotNull();
        assertThat(saved.saveVersion()).isEqualTo(1);
        assertThat(saved.contentReleaseId()).isEqualTo(releaseId);
        assertThat(saved.checksum()).hasSize(64);
    }

    @Test
    void sameWalletRequestIsAppliedOnceAndPayloadMismatchIsRejected() {
        var player = createPlayer("IDEMPOTENCY");
        UUID requestId = UUID.randomUUID();

        var first = walletService.change(
            player.playerId(), "GOLD", 100, "TEST_CREDIT", "TEST", "same", requestId
        );
        var replay = walletService.change(
            player.playerId(), "GOLD", 100, "TEST_CREDIT", "TEST", "same", requestId
        );

        assertThat(first.balanceAfter()).isEqualTo(100);
        assertThat(replay.replayed()).isTrue();
        Integer ledgerCount = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM game.wallet_transaction WHERE player_id = ? AND request_id = ?",
            Integer.class,
            player.playerId(),
            requestId
        );
        assertThat(ledgerCount).isOne();

        assertThatThrownBy(() -> walletService.change(
            player.playerId(), "GOLD", 101, "TEST_CREDIT", "TEST", "same", requestId
        )).isInstanceOf(IdempotencyConflictException.class);
    }

    @Test
    void twentyConcurrentDebitsNeverMakeBalanceNegativeAndLedgerReconciles() throws Exception {
        var player = createPlayer("CONCURRENCY");
        walletService.change(
            player.playerId(), "GOLD", 100, "TEST_CREDIT", "TEST", "concurrency", UUID.randomUUID()
        );

        List<Future<Boolean>> futures = new ArrayList<>();
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            for (int index = 0; index < 20; index++) {
                futures.add(executor.submit(() -> {
                    try {
                        walletService.change(
                            player.playerId(),
                            "GOLD",
                            -10,
                            "TEST_DEBIT",
                            "TEST",
                            "concurrency",
                            UUID.randomUUID()
                        );
                        return true;
                    } catch (InsufficientBalanceException expected) {
                        return false;
                    }
                }));
            }
        }

        int successes = 0;
        for (Future<Boolean> future : futures) {
            if (future.get()) {
                successes++;
            }
        }
        assertThat(successes).isEqualTo(10);

        Long balance = jdbcTemplate.queryForObject(
            "SELECT balance FROM game.wallet WHERE player_id = ? AND currency_key = 'GOLD'",
            Long.class,
            player.playerId()
        );
        Long latestLedgerBalance = jdbcTemplate.queryForObject(
            """
                SELECT balance_after
                  FROM game.wallet_transaction
                 WHERE player_id = ? AND currency_key = 'GOLD'
                 ORDER BY created_at DESC, wallet_transaction_id DESC
                 LIMIT 1
                """,
            Long.class,
            player.playerId()
        );
        assertThat(balance).isZero();
        assertThat(latestLedgerBalance).isEqualTo(balance);
    }

    private PlayerAccountApplicationService.PlayerAccount createPlayer(String prefix) {
        String suffix = UUID.randomUUID().toString().replace("-", "").substring(0, 8);
        return playerAccountService.create(UUID.randomUUID(), "ko-KR", prefix + "_" + suffix);
    }
}
