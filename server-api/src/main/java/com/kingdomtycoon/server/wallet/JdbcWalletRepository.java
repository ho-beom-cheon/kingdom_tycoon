package com.kingdomtycoon.server.wallet;

import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
public class JdbcWalletRepository implements WalletRepository {

    private final JdbcTemplate jdbcTemplate;

    public JdbcWalletRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    @Override
    public WalletBalance lockByPlayerAndCurrency(long playerId, String currencyKey) {
        WalletBalance balance = jdbcTemplate.queryForObject(
            """
                SELECT player_id, currency_key, balance, version
                  FROM game.wallet
                 WHERE player_id = ?
                   AND currency_key = ?
                 FOR UPDATE
                """,
            (resultSet, rowNumber) -> new WalletBalance(
                resultSet.getLong("player_id"),
                resultSet.getString("currency_key"),
                resultSet.getLong("balance"),
                resultSet.getLong("version")
            ),
            playerId,
            currencyKey
        );
        if (balance == null) {
            throw new IllegalArgumentException("wallet does not exist");
        }
        return balance;
    }

    @Override
    public long insertLedger(WalletLedgerEntry entry) {
        Long id = jdbcTemplate.queryForObject(
            """
                INSERT INTO game.wallet_transaction (
                    request_id,
                    line_no,
                    player_id,
                    currency_key,
                    delta_amount,
                    balance_before,
                    balance_after,
                    reason_code,
                    reference_type,
                    reference_id
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                RETURNING wallet_transaction_id
                """,
            Long.class,
            entry.requestId(),
            entry.lineNo(),
            entry.playerId(),
            entry.currencyKey(),
            entry.deltaAmount(),
            entry.balanceBefore(),
            entry.balanceAfter(),
            entry.reasonCode(),
            entry.referenceType(),
            entry.referenceId()
        );
        if (id == null) {
            throw new IllegalStateException("wallet ledger insert returned no id");
        }
        return id;
    }

    @Override
    public void updateBalance(long playerId, String currencyKey, long newBalance, long expectedVersion) {
        int updated = jdbcTemplate.update(
            """
                UPDATE game.wallet
                   SET balance = ?,
                       version = version + 1,
                       updated_at = now()
                 WHERE player_id = ?
                   AND currency_key = ?
                   AND version = ?
                """,
            newBalance,
            playerId,
            currencyKey,
            expectedVersion
        );
        if (updated != 1) {
            throw new IllegalStateException("wallet optimistic lock failed");
        }
    }

    @Override
    public Optional<WalletLedgerEntry> findLedger(long playerId, UUID requestId, short lineNo) {
        List<WalletLedgerEntry> entries = jdbcTemplate.query(
            """
                SELECT wallet_transaction_id,
                       request_id,
                       line_no,
                       player_id,
                       currency_key,
                       delta_amount,
                       balance_before,
                       balance_after,
                       reason_code,
                       reference_type,
                       reference_id
                  FROM game.wallet_transaction
                 WHERE player_id = ?
                   AND request_id = ?
                   AND line_no = ?
                """,
            (resultSet, rowNumber) -> new WalletLedgerEntry(
                resultSet.getLong("wallet_transaction_id"),
                resultSet.getObject("request_id", UUID.class),
                resultSet.getShort("line_no"),
                resultSet.getLong("player_id"),
                resultSet.getString("currency_key"),
                resultSet.getLong("delta_amount"),
                resultSet.getLong("balance_before"),
                resultSet.getLong("balance_after"),
                resultSet.getString("reason_code"),
                resultSet.getString("reference_type"),
                resultSet.getString("reference_id")
            ),
            playerId,
            requestId,
            lineNo
        );
        return entries.stream().findFirst();
    }

    @Override
    public List<WalletBalance> findAll(long playerId) {
        return jdbcTemplate.query(
            """
                SELECT player_id, currency_key, balance, version
                  FROM game.wallet
                 WHERE player_id = ?
                 ORDER BY currency_key
                """,
            (resultSet, rowNumber) -> new WalletBalance(
                resultSet.getLong("player_id"),
                resultSet.getString("currency_key"),
                resultSet.getLong("balance"),
                resultSet.getLong("version")
            ),
            playerId
        );
    }
}
