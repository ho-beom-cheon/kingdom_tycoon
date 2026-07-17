SET ROLE tycoon_owner;

CREATE TABLE master.reward_group (
    reward_group_id bigint GENERATED ALWAYS AS IDENTITY,
    reward_group_key varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_reward_group PRIMARY KEY (reward_group_id),
    CONSTRAINT uk_reward_group__reward_group_key UNIQUE (reward_group_key),
    CONSTRAINT ck_reward_group__key CHECK (reward_group_key ~ '^[A-Z0-9_]+$')
);

CREATE TABLE master.reward_entry (
    release_id bigint NOT NULL,
    reward_group_id bigint NOT NULL,
    entry_no smallint NOT NULL,
    reward_type varchar(20) NOT NULL,
    reward_key varchar(64) NOT NULL,
    quantity bigint NOT NULL,
    probability numeric(12,8),
    weight bigint,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_reward_entry PRIMARY KEY (release_id, reward_group_id, entry_no),
    CONSTRAINT fk_reward_entry__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_reward_entry__group FOREIGN KEY (reward_group_id) REFERENCES master.reward_group(reward_group_id),
    CONSTRAINT fk_reward_entry__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_reward_entry__type CHECK (reward_type IN ('CURRENCY', 'ITEM', 'EQUIPMENT', 'MERCENARY', 'EXP')),
    CONSTRAINT ck_reward_entry__quantity CHECK (quantity > 0),
    CONSTRAINT ck_reward_entry__chance CHECK (
        (probability IS NULL AND weight IS NULL)
        OR (probability BETWEEN 0 AND 1 AND weight IS NULL)
        OR (probability IS NULL AND weight > 0)
    )
);
