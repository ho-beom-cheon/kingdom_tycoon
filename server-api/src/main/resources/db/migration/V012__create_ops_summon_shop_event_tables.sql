SET ROLE tycoon_owner;

CREATE TABLE ops.summon_banner (
    banner_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    banner_key varchar(64) NOT NULL,
    release_id bigint NOT NULL,
    pity_group_key varchar(64) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'DRAFT',
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NOT NULL,
    currency_key varchar(64) NOT NULL,
    cost_amount bigint NOT NULL,
    pull_unit smallint NOT NULL,
    pity_threshold integer NOT NULL,
    guaranteed_rarity varchar(10) NOT NULL DEFAULT 'SS',
    minimum_client_version varchar(30) NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_summon_banner PRIMARY KEY (banner_id),
    CONSTRAINT uk_summon_banner__public_id UNIQUE (public_id),
    CONSTRAINT uk_summon_banner__banner_key UNIQUE (banner_key),
    CONSTRAINT fk_summon_banner__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_summon_banner__currency FOREIGN KEY (currency_key) REFERENCES master.currency(currency_key),
    CONSTRAINT ck_summon_banner__key CHECK (banner_key ~ '^[A-Z0-9_]+$' AND pity_group_key ~ '^[A-Z0-9_]+$'),
    CONSTRAINT ck_summon_banner__status CHECK (status IN ('DRAFT', 'ACTIVE', 'INACTIVE', 'ENDED')),
    CONSTRAINT ck_summon_banner__period CHECK (starts_at < ends_at),
    CONSTRAINT ck_summon_banner__cost CHECK (cost_amount > 0 AND pull_unit > 0 AND pity_threshold > 0),
    CONSTRAINT ck_summon_banner__rarity CHECK (guaranteed_rarity IN ('A', 'S', 'SS')),
    CONSTRAINT ck_summon_banner__version CHECK (version >= 0)
);

CREATE TABLE ops.summon_pool (
    banner_id bigint NOT NULL,
    pool_group varchar(32) NOT NULL,
    result_no smallint NOT NULL,
    mercenary_template_id bigint NOT NULL,
    rarity varchar(10) NOT NULL,
    weight bigint NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_summon_pool PRIMARY KEY (banner_id, pool_group, result_no),
    CONSTRAINT fk_summon_pool__banner FOREIGN KEY (banner_id) REFERENCES ops.summon_banner(banner_id),
    CONSTRAINT fk_summon_pool__template FOREIGN KEY (mercenary_template_id) REFERENCES master.mercenary_template(mercenary_template_id),
    CONSTRAINT ck_summon_pool__rarity CHECK (rarity IN ('C', 'B', 'A', 'S', 'SS')),
    CONSTRAINT ck_summon_pool__weight CHECK (weight > 0)
);
