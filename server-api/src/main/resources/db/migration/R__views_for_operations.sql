SET ROLE tycoon_owner;

CREATE OR REPLACE VIEW ops.active_content_release
WITH (security_invoker = true)
AS
SELECT
    channel.channel_code,
    channel.version AS channel_version,
    release.release_id,
    release.release_version,
    release.minimum_client_version,
    release.checksum,
    release.published_at
FROM master.content_channel channel
JOIN master.content_release release
  ON release.release_id = channel.active_release_id;

CREATE OR REPLACE VIEW audit.wallet_reconciliation
WITH (security_invoker = true)
AS
SELECT
    wallet.player_id,
    wallet.currency_key,
    wallet.balance,
    ledger.balance_after AS latest_ledger_balance,
    wallet.balance = ledger.balance_after AS reconciled
FROM game.wallet wallet
LEFT JOIN LATERAL (
    SELECT transaction.balance_after
      FROM game.wallet_transaction transaction
     WHERE transaction.player_id = wallet.player_id
       AND transaction.currency_key = wallet.currency_key
     ORDER BY transaction.created_at DESC, transaction.wallet_transaction_id DESC
     LIMIT 1
) ledger ON true;

GRANT SELECT ON ops.active_content_release TO tycoon_app, tycoon_ops, tycoon_readonly;
GRANT SELECT ON audit.wallet_reconciliation TO tycoon_readonly;
