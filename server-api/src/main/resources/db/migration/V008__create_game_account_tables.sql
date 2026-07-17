SET ROLE tycoon_owner;

CREATE TABLE game.player_account (
    player_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    status varchar(20) NOT NULL DEFAULT 'ACTIVE',
    locale varchar(10) NOT NULL DEFAULT 'ko-KR',
    last_login_at timestamptz,
    deleted_at timestamptz,
    anonymized_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_player_account PRIMARY KEY (player_id),
    CONSTRAINT uk_player_account__public_id UNIQUE (public_id),
    CONSTRAINT ck_player_account__status CHECK (status IN ('ACTIVE', 'SUSPENDED', 'WITHDRAWN')),
    CONSTRAINT ck_player_account__version CHECK (version >= 0),
    CONSTRAINT ck_player_account__withdrawal CHECK (status <> 'WITHDRAWN' OR deleted_at IS NOT NULL)
);

CREATE TABLE game.account_identity (
    identity_id bigint GENERATED ALWAYS AS IDENTITY,
    player_id bigint NOT NULL,
    provider varchar(32) NOT NULL,
    provider_subject_hash varchar(128) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_account_identity PRIMARY KEY (identity_id),
    CONSTRAINT uk_account_identity__provider_subject UNIQUE (provider, provider_subject_hash),
    CONSTRAINT fk_account_identity__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id)
);

CREATE TABLE game.refresh_token (
    refresh_token_id bigint GENERATED ALWAYS AS IDENTITY,
    player_id bigint NOT NULL,
    token_hash varchar(128) NOT NULL,
    device_id varchar(128) NOT NULL,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_refresh_token PRIMARY KEY (refresh_token_id),
    CONSTRAINT uk_refresh_token__token_hash UNIQUE (token_hash),
    CONSTRAINT fk_refresh_token__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT ck_refresh_token__expiry CHECK (expires_at > created_at)
);

CREATE TABLE game.player_profile (
    player_id bigint NOT NULL,
    nickname varchar(32) NOT NULL,
    level integer NOT NULL DEFAULT 1,
    exp bigint NOT NULL DEFAULT 0,
    tutorial_step varchar(64) NOT NULL DEFAULT 'TUTORIAL_START',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_player_profile PRIMARY KEY (player_id),
    CONSTRAINT uk_player_profile__nickname UNIQUE (nickname),
    CONSTRAINT fk_player_profile__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT ck_player_profile__progress CHECK (level >= 1 AND exp >= 0 AND version >= 0)
);

CREATE TABLE game.player_setting (
    player_id bigint NOT NULL,
    locale varchar(10) NOT NULL DEFAULT 'ko-KR',
    sound_enabled boolean NOT NULL DEFAULT true,
    music_enabled boolean NOT NULL DEFAULT true,
    vibration_enabled boolean NOT NULL DEFAULT true,
    notifications_enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_player_setting PRIMARY KEY (player_id),
    CONSTRAINT fk_player_setting__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT ck_player_setting__version CHECK (version >= 0)
);

CREATE TABLE game.save_checkpoint (
    checkpoint_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    player_id bigint NOT NULL,
    save_version integer NOT NULL,
    game_version varchar(30) NOT NULL,
    content_release_id bigint NOT NULL,
    revision bigint NOT NULL,
    snapshot_json jsonb NOT NULL,
    checksum varchar(128) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_save_checkpoint PRIMARY KEY (checkpoint_id),
    CONSTRAINT uk_save_checkpoint__public_id UNIQUE (public_id),
    CONSTRAINT uk_save_checkpoint__player_revision UNIQUE (player_id, revision),
    CONSTRAINT fk_save_checkpoint__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_save_checkpoint__release FOREIGN KEY (content_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT ck_save_checkpoint__versions CHECK (save_version >= 1 AND revision >= 0)
);
