package com.kingdomtycoon.server.summon;

import com.kingdomtycoon.server.wallet.WalletRepository;
import java.util.List;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;

@Service
public class SummonReceiptQueryService {

    private final JdbcTemplate jdbcTemplate;
    private final WalletRepository walletRepository;

    public SummonReceiptQueryService(JdbcTemplate jdbcTemplate, WalletRepository walletRepository) {
        this.jdbcTemplate = jdbcTemplate;
        this.walletRepository = walletRepository;
    }

    public Receipt load(long playerId, long summonTransactionId, boolean replayed) {
        Header header = jdbcTemplate.queryForObject(
            """
                SELECT transaction.public_id AS receipt_id,
                       release.release_version AS content_version,
                       banner.pity_group_key
                  FROM game.summon_transaction transaction
                  JOIN master.content_release release ON release.release_id = transaction.release_id
                  JOIN ops.summon_banner banner ON banner.banner_id = transaction.banner_id
                 WHERE transaction.summon_transaction_id = ?
                   AND transaction.player_id = ?
                """,
            (resultSet, rowNumber) -> new Header(
                resultSet.getObject("receipt_id", UUID.class),
                resultSet.getString("content_version"),
                resultSet.getString("pity_group_key")
            ),
            summonTransactionId,
            playerId
        );
        if (header == null) {
            throw new IllegalArgumentException("summon receipt does not exist");
        }
        List<Result> results = jdbcTemplate.query(
            """
                SELECT result.result_no,
                       result.created_entity_public_id,
                       template.mercenary_key,
                       job.job_key,
                       result.rarity,
                       result.guaranteed
                  FROM game.summon_result result
                  JOIN master.mercenary_template template ON template.mercenary_key = result.result_key
                  JOIN master.job job ON job.job_id = template.job_id
                 WHERE result.summon_transaction_id = ?
                 ORDER BY result.result_no
                """,
            (resultSet, rowNumber) -> new Result(
                resultSet.getShort("result_no"),
                resultSet.getObject("created_entity_public_id", UUID.class),
                resultSet.getString("mercenary_key"),
                resultSet.getString("job_key"),
                resultSet.getString("rarity"),
                resultSet.getBoolean("guaranteed")
            ),
            summonTransactionId
        );
        List<Balance> wallet = walletRepository.findAll(playerId).stream()
            .map(value -> new Balance(value.currencyKey(), value.balance(), value.version()))
            .toList();
        Integer pity = jdbcTemplate.queryForObject(
            "SELECT pull_count FROM game.summon_pity WHERE player_id = ? AND pity_group_key = ?",
            Integer.class,
            playerId,
            header.pityGroupKey()
        );
        return new Receipt(
            header.receiptId(),
            replayed,
            header.contentVersion(),
            wallet,
            List.of(new Pity(header.pityGroupKey(), pity == null ? 0 : pity)),
            results
        );
    }

    private record Header(UUID receiptId, String contentVersion, String pityGroupKey) {
    }

    public record Receipt(
        UUID receiptId,
        boolean replayed,
        String contentVersion,
        List<Balance> walletAfter,
        List<Pity> pityAfter,
        List<Result> results
    ) {
    }

    public record Balance(String currencyKey, long balance, long version) {
    }

    public record Pity(String pityGroupKey, int pullCount) {
    }

    public record Result(
        short resultNo,
        UUID instanceId,
        String templateKey,
        String jobKey,
        String rarity,
        boolean guaranteed
    ) {
    }
}
