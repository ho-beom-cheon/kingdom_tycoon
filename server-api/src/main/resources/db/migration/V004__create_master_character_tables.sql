SET ROLE tycoon_owner;

CREATE TABLE master.job (
    job_id bigint GENERATED ALWAYS AS IDENTITY,
    job_key varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_job PRIMARY KEY (job_id),
    CONSTRAINT uk_job__job_key UNIQUE (job_key),
    CONSTRAINT ck_job__key CHECK (job_key ~ '^[A-Z0-9_]+$')
);

CREATE TABLE master.job_balance (
    release_id bigint NOT NULL,
    job_id bigint NOT NULL,
    base_hp bigint NOT NULL,
    base_attack bigint NOT NULL,
    base_defense bigint NOT NULL,
    base_speed numeric(12,8) NOT NULL,
    critical_rate numeric(12,8) NOT NULL,
    growth_rate numeric(12,8) NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_job_balance PRIMARY KEY (release_id, job_id),
    CONSTRAINT fk_job_balance__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_job_balance__job FOREIGN KEY (job_id) REFERENCES master.job(job_id),
    CONSTRAINT fk_job_balance__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_job_balance__stats CHECK (
        base_hp > 0 AND base_attack >= 0 AND base_defense >= 0 AND base_speed > 0 AND growth_rate >= 0
    ),
    CONSTRAINT ck_job_balance__critical_rate CHECK (critical_rate BETWEEN 0 AND 1)
);

CREATE TABLE master.mercenary_template (
    mercenary_template_id bigint GENERATED ALWAYS AS IDENTITY,
    mercenary_key varchar(64) NOT NULL,
    job_id bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_mercenary_template PRIMARY KEY (mercenary_template_id),
    CONSTRAINT uk_mercenary_template__mercenary_key UNIQUE (mercenary_key),
    CONSTRAINT fk_mercenary_template__job FOREIGN KEY (job_id) REFERENCES master.job(job_id),
    CONSTRAINT ck_mercenary_template__key CHECK (mercenary_key ~ '^[A-Z0-9_]+$')
);

CREATE TABLE master.mercenary_balance (
    release_id bigint NOT NULL,
    mercenary_template_id bigint NOT NULL,
    rarity varchar(10) NOT NULL,
    base_hp bigint NOT NULL,
    base_attack bigint NOT NULL,
    base_defense bigint NOT NULL,
    growth_rate numeric(12,8) NOT NULL,
    summonable boolean NOT NULL DEFAULT true,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_mercenary_balance PRIMARY KEY (release_id, mercenary_template_id),
    CONSTRAINT fk_mercenary_balance__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_mercenary_balance__template FOREIGN KEY (mercenary_template_id) REFERENCES master.mercenary_template(mercenary_template_id),
    CONSTRAINT fk_mercenary_balance__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_mercenary_balance__rarity CHECK (rarity IN ('C', 'B', 'A', 'S', 'SS')),
    CONSTRAINT ck_mercenary_balance__stats CHECK (
        base_hp > 0 AND base_attack >= 0 AND base_defense >= 0 AND growth_rate >= 0
    )
);

CREATE TABLE master.skill (
    skill_id bigint GENERATED ALWAYS AS IDENTITY,
    skill_key varchar(64) NOT NULL,
    skill_type varchar(20) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_skill PRIMARY KEY (skill_id),
    CONSTRAINT uk_skill__skill_key UNIQUE (skill_key),
    CONSTRAINT ck_skill__key CHECK (skill_key ~ '^[A-Z0-9_]+$')
);

CREATE TABLE master.skill_balance (
    release_id bigint NOT NULL,
    skill_id bigint NOT NULL,
    skill_level integer NOT NULL,
    cooldown_seconds numeric(12,4) NOT NULL,
    power_rate numeric(12,8) NOT NULL,
    range_value numeric(12,4) NOT NULL,
    duration_seconds numeric(12,4) NOT NULL,
    cost_amount bigint NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_skill_balance PRIMARY KEY (release_id, skill_id, skill_level),
    CONSTRAINT fk_skill_balance__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_skill_balance__skill FOREIGN KEY (skill_id) REFERENCES master.skill(skill_id),
    CONSTRAINT fk_skill_balance__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_skill_balance__values CHECK (
        skill_level >= 1 AND cooldown_seconds >= 0 AND power_rate >= 0 AND range_value >= 0
        AND duration_seconds >= 0 AND cost_amount >= 0
    )
);

CREATE TABLE master.mercenary_skill_map (
    release_id bigint NOT NULL,
    mercenary_template_id bigint NOT NULL,
    skill_id bigint NOT NULL,
    unlock_level integer NOT NULL,
    slot_no smallint NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_mercenary_skill_map PRIMARY KEY (release_id, mercenary_template_id, skill_id),
    CONSTRAINT uk_mercenary_skill_map__slot UNIQUE (release_id, mercenary_template_id, slot_no),
    CONSTRAINT fk_mercenary_skill_map__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_mercenary_skill_map__template FOREIGN KEY (mercenary_template_id) REFERENCES master.mercenary_template(mercenary_template_id),
    CONSTRAINT fk_mercenary_skill_map__skill FOREIGN KEY (skill_id) REFERENCES master.skill(skill_id),
    CONSTRAINT fk_mercenary_skill_map__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_mercenary_skill_map__values CHECK (unlock_level >= 1 AND slot_no >= 1)
);
