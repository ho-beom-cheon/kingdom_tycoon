package com.kingdomtycoon.server.session;

import com.kingdomtycoon.server.player.PlayerAccountApplicationService;
import com.kingdomtycoon.server.support.RequestHash;
import java.time.Duration;
import java.time.Instant;
import java.sql.Timestamp;
import java.util.List;
import java.util.UUID;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class DevelopmentSessionService {

    private final JdbcTemplate jdbcTemplate;
    private final PlayerAccountApplicationService playerAccountService;
    private final boolean enabled;
    private final Duration ttl;

    public DevelopmentSessionService(
        JdbcTemplate jdbcTemplate,
        PlayerAccountApplicationService playerAccountService,
        @Value("${tycoon.dev-session.enabled:false}") boolean enabled,
        @Value("${tycoon.dev-session.ttl:PT24H}") Duration ttl
    ) {
        this.jdbcTemplate = jdbcTemplate;
        this.playerAccountService = playerAccountService;
        this.enabled = enabled;
        this.ttl = ttl;
    }

    @Transactional
    public Session create(String deviceInstallId, String locale, String nickname) {
        if (!enabled) {
            throw new DevelopmentSessionDisabledException();
        }
        String deviceHash = RequestHash.sha256(deviceInstallId);
        List<PlayerRef> matches = jdbcTemplate.query(
            """
                SELECT account.player_id, account.public_id
                  FROM game.account_identity identity
                  JOIN game.player_account account ON account.player_id = identity.player_id
                 WHERE identity.provider = 'DEV_DEVICE'
                   AND identity.provider_subject_hash = ?
                   AND account.status = 'ACTIVE'
                """,
            (resultSet, rowNumber) -> new PlayerRef(
                resultSet.getLong("player_id"),
                resultSet.getObject("public_id", UUID.class)
            ),
            deviceHash
        );
        PlayerRef player;
        if (matches.isEmpty()) {
            var created = playerAccountService.create(UUID.randomUUID(), locale, nickname);
            jdbcTemplate.update(
                "INSERT INTO game.account_identity (player_id, provider, provider_subject_hash) VALUES (?, 'DEV_DEVICE', ?)",
                created.playerId(),
                deviceHash
            );
            player = new PlayerRef(created.playerId(), created.publicId());
        } else {
            player = matches.getFirst();
        }

        String token = UUID.randomUUID() + "." + UUID.randomUUID();
        Instant expiresAt = Instant.now().plus(ttl);
        jdbcTemplate.update(
            "UPDATE game.refresh_token SET revoked_at = now() WHERE player_id = ? AND device_id = ? AND revoked_at IS NULL",
            player.playerId(),
            deviceHash
        );
        jdbcTemplate.update(
            "INSERT INTO game.refresh_token (player_id, token_hash, device_id, expires_at) VALUES (?, ?, ?, ?)",
            player.playerId(),
            RequestHash.sha256(token),
            deviceHash,
            Timestamp.from(expiresAt)
        );
        jdbcTemplate.update("UPDATE game.player_account SET last_login_at = now(), updated_at = now() WHERE player_id = ?", player.playerId());
        String contentVersion = jdbcTemplate.queryForObject(
            """
                SELECT release.release_version
                  FROM master.content_channel channel
                  JOIN master.content_release release ON release.release_id = channel.active_release_id
                 WHERE channel.channel_code = 'PRODUCTION'
                """,
            String.class
        );
        return new Session(player.playerId(), player.publicId(), token, expiresAt, contentVersion);
    }

    public AuthenticatedPlayer authenticate(String authorization) {
        if (authorization == null || !authorization.startsWith("Bearer ")) {
            throw new AuthenticationException("bearer token is required");
        }
        String rawToken = authorization.substring("Bearer ".length()).trim();
        if (rawToken.isEmpty()) {
            throw new AuthenticationException("bearer token is required");
        }
        List<AuthenticatedPlayer> players = jdbcTemplate.query(
            """
                SELECT account.player_id, account.public_id
                  FROM game.refresh_token token
                  JOIN game.player_account account ON account.player_id = token.player_id
                 WHERE token.token_hash = ?
                   AND token.revoked_at IS NULL
                   AND token.expires_at > now()
                   AND account.status = 'ACTIVE'
                """,
            (resultSet, rowNumber) -> new AuthenticatedPlayer(
                resultSet.getLong("player_id"),
                resultSet.getObject("public_id", UUID.class)
            ),
            RequestHash.sha256(rawToken)
        );
        if (players.size() != 1) {
            throw new AuthenticationException("session token is invalid or expired");
        }
        return players.getFirst();
    }

    private record PlayerRef(long playerId, UUID publicId) {
    }

    public record Session(
        long internalPlayerId,
        UUID playerId,
        String accessToken,
        Instant expiresAtUtc,
        String contentVersion
    ) {
    }

    public record AuthenticatedPlayer(long internalPlayerId, UUID playerId) {
    }
}
