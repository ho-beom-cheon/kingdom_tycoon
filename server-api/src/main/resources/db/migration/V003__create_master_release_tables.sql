SET ROLE tycoon_owner;

CREATE TABLE master.content_release (
    release_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    release_version varchar(30) NOT NULL,
    release_name varchar(100) NOT NULL,
    status varchar(20) NOT NULL,
    base_release_id bigint,
    minimum_client_version varchar(30) NOT NULL,
    scheduled_at timestamptz,
    published_at timestamptz,
    checksum varchar(64),
    description text,
    created_by bigint NOT NULL,
    approved_by bigint,
    created_at timestamptz NOT NULL DEFAULT now(),
    approved_at timestamptz,
    CONSTRAINT pk_content_release PRIMARY KEY (release_id),
    CONSTRAINT uk_content_release__public_id UNIQUE (public_id),
    CONSTRAINT uk_content_release__release_version UNIQUE (release_version),
    CONSTRAINT fk_content_release__base FOREIGN KEY (base_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_content_release__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT fk_content_release__approver FOREIGN KEY (approved_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_content_release__status CHECK (
        status IN ('DRAFT', 'VALIDATING', 'APPROVED', 'SCHEDULED', 'PUBLISHED', 'ROLLED_BACK', 'ARCHIVED')
    ),
    CONSTRAINT ck_content_release__approval CHECK (
        (status IN ('DRAFT', 'VALIDATING') AND approved_by IS NULL AND approved_at IS NULL)
        OR status NOT IN ('DRAFT', 'VALIDATING')
    ),
    CONSTRAINT ck_content_release__publish_time CHECK (status <> 'PUBLISHED' OR published_at IS NOT NULL)
);

CREATE TABLE master.content_channel (
    channel_code varchar(20) NOT NULL,
    active_release_id bigint NOT NULL,
    previous_release_id bigint,
    version bigint NOT NULL DEFAULT 0,
    updated_by bigint NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_content_channel PRIMARY KEY (channel_code),
    CONSTRAINT fk_content_channel__active_release FOREIGN KEY (active_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_content_channel__previous_release FOREIGN KEY (previous_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_content_channel__updater FOREIGN KEY (updated_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_content_channel__code CHECK (channel_code IN ('DEV', 'STAGING', 'PRODUCTION')),
    CONSTRAINT ck_content_channel__version CHECK (version >= 0),
    CONSTRAINT ck_content_channel__different_releases CHECK (active_release_id IS DISTINCT FROM previous_release_id)
);

CREATE TABLE master.content_validation_result (
    validation_result_id bigint GENERATED ALWAYS AS IDENTITY,
    release_id bigint NOT NULL,
    validation_code varchar(64) NOT NULL,
    severity varchar(10) NOT NULL,
    target_type varchar(40) NOT NULL,
    target_key varchar(128),
    message text NOT NULL,
    passed boolean NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_content_validation_result PRIMARY KEY (validation_result_id),
    CONSTRAINT fk_content_validation_result__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT ck_content_validation_result__severity CHECK (severity IN ('INFO', 'WARN', 'ERROR'))
);

CREATE TABLE master.content_publish_history (
    publish_history_id bigint GENERATED ALWAYS AS IDENTITY,
    channel_code varchar(20) NOT NULL,
    from_release_id bigint,
    to_release_id bigint NOT NULL,
    action_type varchar(20) NOT NULL,
    reason text NOT NULL,
    actor_id bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_content_publish_history PRIMARY KEY (publish_history_id),
    CONSTRAINT fk_content_publish_history__channel FOREIGN KEY (channel_code) REFERENCES master.content_channel(channel_code),
    CONSTRAINT fk_content_publish_history__from_release FOREIGN KEY (from_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_content_publish_history__to_release FOREIGN KEY (to_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_content_publish_history__actor FOREIGN KEY (actor_id) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_content_publish_history__action CHECK (action_type IN ('PUBLISH', 'ROLLBACK')),
    CONSTRAINT ck_content_publish_history__reason CHECK (length(btrim(reason)) > 0)
);

CREATE TABLE master.runtime_config_definition (
    config_key varchar(100) NOT NULL,
    value_type varchar(20) NOT NULL,
    validation_rule jsonb NOT NULL DEFAULT '{}'::jsonb,
    description text NOT NULL,
    sensitive boolean NOT NULL DEFAULT false,
    CONSTRAINT pk_runtime_config_definition PRIMARY KEY (config_key),
    CONSTRAINT ck_runtime_config_definition__value_type CHECK (
        value_type IN ('INTEGER', 'DECIMAL', 'BOOLEAN', 'STRING', 'JSON')
    )
);

CREATE TABLE master.runtime_config_value (
    release_id bigint NOT NULL,
    config_key varchar(100) NOT NULL,
    config_value jsonb NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_by bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_runtime_config_value PRIMARY KEY (release_id, config_key),
    CONSTRAINT fk_runtime_config_value__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_runtime_config_value__definition FOREIGN KEY (config_key) REFERENCES master.runtime_config_definition(config_key),
    CONSTRAINT fk_runtime_config_value__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id)
);

CREATE TABLE master.currency (
    currency_id bigint GENERATED ALWAYS AS IDENTITY,
    currency_key varchar(64) NOT NULL,
    currency_type varchar(20) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_currency PRIMARY KEY (currency_id),
    CONSTRAINT uk_currency__currency_key UNIQUE (currency_key),
    CONSTRAINT ck_currency__type CHECK (currency_type IN ('SOFT', 'PREMIUM_FREE', 'PREMIUM_PAID', 'TICKET'))
);
