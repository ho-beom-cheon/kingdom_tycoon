package com.kingdomtycoon.server;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.sql.DriverManager;
import java.sql.SQLException;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataAccessException;
import org.springframework.jdbc.core.JdbcTemplate;

class MigrationIntegrityIntegrationTest extends PostgresIntegrationTest {

    @Autowired
    JdbcTemplate jdbcTemplate;

    @Test
    void migrationsCreateSchemasRolesAndSeedsOnPostgresql18() {
        String version = jdbcTemplate.queryForObject("SHOW server_version", String.class);
        assertThat(version).startsWith("18.4");

        List<String> schemas = jdbcTemplate.queryForList(
            """
                SELECT nspname
                  FROM pg_namespace
                 WHERE nspname IN ('game', 'master', 'ops', 'billing', 'audit')
                 ORDER BY nspname
                """,
            String.class
        );
        assertThat(schemas).containsExactly("audit", "billing", "game", "master", "ops");

        Integer publicObjects = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relkind IN ('r', 'v', 'm', 'S')",
            Integer.class
        );
        assertThat(publicObjects).isZero();

        Integer successfulMigrations = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM audit.flyway_schema_history WHERE success",
            Integer.class
        );
        assertThat(successfulMigrations).isEqualTo(17);

        List<String> loginRoles = jdbcTemplate.queryForList(
            """
                SELECT rolname
                  FROM pg_roles
                 WHERE rolname LIKE 'tycoon_%' AND rolcanlogin
                 ORDER BY rolname
                """,
            String.class
        );
        assertThat(loginRoles).containsExactly(
            "tycoon_app",
            "tycoon_migrator",
            "tycoon_ops",
            "tycoon_readonly"
        );
        Boolean ownerCanLogin = jdbcTemplate.queryForObject(
            "SELECT rolcanlogin FROM pg_roles WHERE rolname = 'tycoon_owner'",
            Boolean.class
        );
        assertThat(ownerCanLogin).isFalse();

        Integer wrongOwners = jdbcTemplate.queryForObject(
            """
                SELECT count(*)
                  FROM pg_class object
                  JOIN pg_namespace namespace ON namespace.oid = object.relnamespace
                  JOIN pg_roles owner_role ON owner_role.oid = object.relowner
                 WHERE namespace.nspname IN ('game', 'master', 'ops', 'billing', 'audit')
                   AND object.relkind IN ('r', 'v', 'm', 'S')
                   AND object.relname <> 'flyway_schema_history'
                   AND owner_role.rolname <> 'tycoon_owner'
                """,
            Integer.class
        );
        assertThat(wrongOwners).isZero();

        Integer channels = jdbcTemplate.queryForObject(
            "SELECT count(*) FROM master.content_channel",
            Integer.class
        );
        assertThat(channels).isEqualTo(3);
        String productionStatus = jdbcTemplate.queryForObject(
            """
                SELECT release.status
                  FROM master.content_channel channel
                  JOIN master.content_release release ON release.release_id = channel.active_release_id
                 WHERE channel.channel_code = 'PRODUCTION'
                """,
            String.class
        );
        assertThat(productionStatus).isEqualTo("PUBLISHED");
    }

    @Test
    void publishedRevisionIsImmutableAndApplicationCannotRewriteLedger() throws Exception {
        Long bootstrapReleaseId = jdbcTemplate.queryForObject(
            "SELECT release_id FROM master.content_release WHERE release_version = '0.0.0-content.0'",
            Long.class
        );
        assertThatThrownBy(() -> jdbcTemplate.update(
            """
                UPDATE master.runtime_config_value
                   SET config_value = '15'::jsonb
                 WHERE release_id = ?
                   AND config_key = 'ACTIVE_MERCENARY_LIMIT'
                """,
            bootstrapReleaseId
        )).isInstanceOf(DataAccessException.class)
            .hasMessageContaining("immutable");

        try (var connection = DriverManager.getConnection(
            POSTGRES.getJdbcUrl(),
            POSTGRES.getUsername(),
            POSTGRES.getPassword()
        ); var statement = connection.createStatement()) {
            statement.execute("SET ROLE tycoon_app");
            assertThatThrownBy(() -> statement.executeUpdate(
                "UPDATE game.wallet_transaction SET reason_code = 'TAMPERED'"
            )).isInstanceOf(SQLException.class)
                .extracting(exception -> ((SQLException) exception).getSQLState())
                .isEqualTo("42501");
        }
    }
}
