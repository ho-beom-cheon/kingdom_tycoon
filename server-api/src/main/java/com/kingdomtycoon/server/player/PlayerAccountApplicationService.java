package com.kingdomtycoon.server.player;

import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class PlayerAccountApplicationService {

    private final JdbcTemplate jdbcTemplate;

    public PlayerAccountApplicationService(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Transactional
    public PlayerAccount create(UUID publicId, String locale, String nickname) {
        Long playerId = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.player_account (public_id, locale)
                VALUES (?, ?)
                RETURNING player_id
                """,
            Long.class,
            publicId,
            locale
        );
        if (playerId == null) {
            throw new IllegalStateException("player insert returned no id");
        }

        jdbcTemplate.update(
            "INSERT INTO game.player_profile (player_id, nickname) VALUES (?, ?)",
            playerId,
            nickname
        );
        jdbcTemplate.update(
            "INSERT INTO game.player_setting (player_id, locale) VALUES (?, ?)",
            playerId,
            locale
        );
        jdbcTemplate.update(
            """
                INSERT INTO game.wallet (player_id, currency_key, balance)
                SELECT ?, currency_key, 0
                  FROM master.currency
                 ORDER BY currency_key
                """,
            playerId
        );

        return new PlayerAccount(playerId, publicId, nickname);
    }

    public record PlayerAccount(long playerId, UUID publicId, String nickname) {
    }
}
