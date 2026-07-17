SET ROLE tycoon_owner;

CREATE TABLE game.summon_pity (
    player_id bigint NOT NULL,
    pity_group_key varchar(64) NOT NULL,
    pull_count integer NOT NULL DEFAULT 0,
    guaranteed_state varchar(32) NOT NULL DEFAULT 'NONE',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_summon_pity PRIMARY KEY (player_id, pity_group_key),
    CONSTRAINT fk_summon_pity__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT ck_summon_pity__count CHECK (pull_count >= 0 AND version >= 0),
    CONSTRAINT ck_summon_pity__state CHECK (guaranteed_state IN ('NONE', 'RATE_UP_NEXT', 'GUARANTEED_NEXT'))
);

CREATE TABLE game.summon_transaction (
    summon_transaction_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    request_id uuid NOT NULL,
    player_id bigint NOT NULL,
    banner_id bigint NOT NULL,
    release_id bigint NOT NULL,
    pull_count smallint NOT NULL,
    cost_wallet_transaction_id bigint NOT NULL,
    random_trace_hash varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_summon_transaction PRIMARY KEY (summon_transaction_id),
    CONSTRAINT uk_summon_transaction__public_id UNIQUE (public_id),
    CONSTRAINT uk_summon_transaction__player_request UNIQUE (player_id, request_id),
    CONSTRAINT fk_summon_transaction__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_summon_transaction__banner FOREIGN KEY (banner_id) REFERENCES ops.summon_banner(banner_id),
    CONSTRAINT fk_summon_transaction__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_summon_transaction__wallet_transaction FOREIGN KEY (cost_wallet_transaction_id) REFERENCES game.wallet_transaction(wallet_transaction_id),
    CONSTRAINT ck_summon_transaction__pull_count CHECK (pull_count > 0)
);

CREATE TABLE game.summon_result (
    summon_transaction_id bigint NOT NULL,
    result_no smallint NOT NULL,
    result_type varchar(20) NOT NULL,
    result_key varchar(64) NOT NULL,
    rarity varchar(10) NOT NULL,
    guaranteed boolean NOT NULL DEFAULT false,
    created_entity_public_id uuid NOT NULL,
    CONSTRAINT pk_summon_result PRIMARY KEY (summon_transaction_id, result_no),
    CONSTRAINT fk_summon_result__transaction FOREIGN KEY (summon_transaction_id) REFERENCES game.summon_transaction(summon_transaction_id),
    CONSTRAINT ck_summon_result__type CHECK (result_type IN ('MERCENARY', 'ITEM', 'EQUIPMENT')),
    CONSTRAINT ck_summon_result__rarity CHECK (rarity IN ('C', 'B', 'A', 'S', 'SS'))
);
