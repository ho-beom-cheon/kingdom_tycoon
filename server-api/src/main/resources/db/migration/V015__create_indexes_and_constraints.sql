SET ROLE tycoon_owner;

ALTER TABLE audit.idempotency_request
    ADD CONSTRAINT fk_idempotency_request__player
    FOREIGN KEY (player_id) REFERENCES game.player_account(player_id);
ALTER TABLE audit.security_event
    ADD CONSTRAINT fk_security_event__player
    FOREIGN KEY (player_id) REFERENCES game.player_account(player_id);

CREATE INDEX ix_idempotency_request__status_expires_at
    ON audit.idempotency_request(status, expires_at);
CREATE INDEX ix_admin_action_log__actor_created_at
    ON audit.admin_action_log(actor_id, created_at DESC);
CREATE INDEX ix_security_event__player_created_at
    ON audit.security_event(player_id, created_at DESC);
CREATE INDEX ix_outbox_event__pending
    ON audit.outbox_event(next_attempt_at, outbox_event_id)
    WHERE published_at IS NULL;

CREATE INDEX ix_content_release__status_scheduled_at
    ON master.content_release(status, scheduled_at);
CREATE INDEX ix_content_validation_result__release_severity
    ON master.content_validation_result(release_id, severity, passed);
CREATE INDEX ix_content_publish_history__channel_created_at
    ON master.content_publish_history(channel_code, created_at DESC);

CREATE INDEX ix_account_identity__player ON game.account_identity(player_id);
CREATE INDEX ix_refresh_token__player ON game.refresh_token(player_id);
CREATE INDEX ix_save_checkpoint__player_created_at
    ON game.save_checkpoint(player_id, created_at DESC);
CREATE INDEX ix_mercenary__player_status ON game.mercenary(player_id, status);
CREATE INDEX ix_party__player ON game.party(player_id);
CREATE INDEX ix_party_member__mercenary ON game.party_member(mercenary_id);
CREATE INDEX ix_equipment__player_status ON game.equipment(player_id, status);
CREATE INDEX ix_item_transaction__player_created_at
    ON game.item_transaction(player_id, created_at DESC);
CREATE INDEX ix_wallet_transaction__player_created_at
    ON game.wallet_transaction(player_id, created_at DESC);
CREATE INDEX ix_reward_grant__player_created_at
    ON game.reward_grant(player_id, created_at DESC);
CREATE INDEX ix_summon_transaction__player_created_at
    ON game.summon_transaction(player_id, created_at DESC);
CREATE INDEX ix_summon_banner__enabled_period
    ON ops.summon_banner(enabled, starts_at, ends_at);

GRANT SELECT ON ALL TABLES IN SCHEMA master, ops TO tycoon_app;
GRANT SELECT ON ALL TABLES IN SCHEMA game, master, ops TO tycoon_readonly;
GRANT SELECT ON
    audit.admin_actor,
    audit.idempotency_request,
    audit.admin_action_log,
    audit.security_event,
    audit.outbox_event
TO tycoon_readonly;

GRANT SELECT, INSERT, UPDATE ON
    game.player_account,
    game.account_identity,
    game.refresh_token,
    game.player_profile,
    game.player_setting,
    game.mercenary,
    game.mercenary_skill,
    game.roster_slot,
    game.party,
    game.party_member,
    game.item_stack,
    game.equipment,
    game.equipment_option,
    game.wallet,
    game.reward_grant,
    game.reward_grant_item,
    game.summon_pity,
    game.summon_transaction,
    game.summon_result
TO tycoon_app;

GRANT SELECT, INSERT ON
    game.save_checkpoint,
    game.item_transaction,
    game.wallet_transaction
TO tycoon_app;

GRANT SELECT, INSERT, UPDATE ON
    audit.idempotency_request,
    audit.outbox_event
TO tycoon_app;
GRANT SELECT, INSERT ON audit.security_event TO tycoon_app;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA master, ops TO tycoon_ops;
GRANT SELECT, INSERT ON audit.admin_action_log, audit.outbox_event, audit.security_event TO tycoon_ops;

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA game, audit TO tycoon_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA master, ops, audit TO tycoon_ops;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA game, master, ops, billing, audit TO tycoon_readonly;

ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA master, ops
    GRANT SELECT ON TABLES TO tycoon_app;
ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA game, master, ops, billing, audit
    GRANT SELECT ON TABLES TO tycoon_readonly;
ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA master, ops
    GRANT SELECT, INSERT, UPDATE ON TABLES TO tycoon_ops;
ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA master, ops, audit
    GRANT USAGE, SELECT ON SEQUENCES TO tycoon_ops;
ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA game, audit
    GRANT USAGE, SELECT ON SEQUENCES TO tycoon_app;
