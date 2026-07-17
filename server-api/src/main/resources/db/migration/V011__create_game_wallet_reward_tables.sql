SET ROLE tycoon_owner;

CREATE TABLE game.wallet (
    player_id bigint NOT NULL,
    currency_key varchar(64) NOT NULL,
    balance bigint NOT NULL DEFAULT 0,
    version bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_wallet PRIMARY KEY (player_id, currency_key),
    CONSTRAINT fk_wallet__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_wallet__currency FOREIGN KEY (currency_key) REFERENCES master.currency(currency_key),
    CONSTRAINT ck_wallet__balance CHECK (balance >= 0 AND version >= 0)
);

CREATE TABLE game.wallet_transaction (
    wallet_transaction_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    request_id uuid NOT NULL,
    line_no smallint NOT NULL,
    player_id bigint NOT NULL,
    currency_key varchar(64) NOT NULL,
    delta_amount bigint NOT NULL,
    balance_before bigint NOT NULL,
    balance_after bigint NOT NULL,
    reason_code varchar(64) NOT NULL,
    reference_type varchar(40),
    reference_id varchar(128),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_wallet_transaction PRIMARY KEY (wallet_transaction_id),
    CONSTRAINT uk_wallet_transaction__public_id UNIQUE (public_id),
    CONSTRAINT uk_wallet_transaction__player_request_line UNIQUE (player_id, request_id, line_no),
    CONSTRAINT fk_wallet_transaction__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_wallet_transaction__wallet FOREIGN KEY (player_id, currency_key) REFERENCES game.wallet(player_id, currency_key),
    CONSTRAINT ck_wallet_transaction__delta CHECK (delta_amount <> 0),
    CONSTRAINT ck_wallet_transaction__balances CHECK (
        balance_before >= 0 AND balance_after >= 0 AND balance_after = balance_before + delta_amount
    )
);

CREATE TABLE game.reward_grant (
    reward_grant_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    request_id uuid NOT NULL,
    player_id bigint NOT NULL,
    release_id bigint NOT NULL,
    reward_group_id bigint,
    source_type varchar(40) NOT NULL,
    source_id varchar(128) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'PROCESSING',
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    CONSTRAINT pk_reward_grant PRIMARY KEY (reward_grant_id),
    CONSTRAINT uk_reward_grant__public_id UNIQUE (public_id),
    CONSTRAINT uk_reward_grant__player_request UNIQUE (player_id, request_id),
    CONSTRAINT fk_reward_grant__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_reward_grant__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_reward_grant__group FOREIGN KEY (reward_group_id) REFERENCES master.reward_group(reward_group_id),
    CONSTRAINT ck_reward_grant__status CHECK (status IN ('PROCESSING', 'GRANTED', 'FAILED', 'REVERSED')),
    CONSTRAINT ck_reward_grant__completion CHECK (
        (status = 'PROCESSING' AND completed_at IS NULL)
        OR (status <> 'PROCESSING' AND completed_at IS NOT NULL)
    )
);

CREATE TABLE game.reward_grant_item (
    reward_grant_id bigint NOT NULL,
    line_no smallint NOT NULL,
    reward_type varchar(20) NOT NULL,
    reward_key varchar(64) NOT NULL,
    quantity bigint NOT NULL,
    created_entity_public_id uuid,
    CONSTRAINT pk_reward_grant_item PRIMARY KEY (reward_grant_id, line_no),
    CONSTRAINT fk_reward_grant_item__grant FOREIGN KEY (reward_grant_id) REFERENCES game.reward_grant(reward_grant_id),
    CONSTRAINT ck_reward_grant_item__type CHECK (reward_type IN ('CURRENCY', 'ITEM', 'EQUIPMENT', 'MERCENARY', 'EXP')),
    CONSTRAINT ck_reward_grant_item__quantity CHECK (quantity > 0)
);
