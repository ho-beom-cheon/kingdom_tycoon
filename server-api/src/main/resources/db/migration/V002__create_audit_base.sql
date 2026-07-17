SET ROLE tycoon_owner;

CREATE TABLE audit.admin_actor (
    actor_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    actor_key varchar(64) NOT NULL,
    display_name varchar(100) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'ACTIVE',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_admin_actor PRIMARY KEY (actor_id),
    CONSTRAINT uk_admin_actor__public_id UNIQUE (public_id),
    CONSTRAINT uk_admin_actor__actor_key UNIQUE (actor_key),
    CONSTRAINT ck_admin_actor__status CHECK (status IN ('ACTIVE', 'DISABLED'))
);

CREATE TABLE audit.idempotency_request (
    idempotency_id bigint GENERATED ALWAYS AS IDENTITY,
    scope varchar(40) NOT NULL,
    player_id bigint,
    idempotency_key uuid NOT NULL,
    request_hash varchar(64) NOT NULL,
    status varchar(20) NOT NULL,
    response_code integer,
    response_body jsonb,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    CONSTRAINT pk_idempotency_request PRIMARY KEY (idempotency_id),
    CONSTRAINT uk_idempotency_request__scope_player_key
        UNIQUE NULLS NOT DISTINCT (scope, player_id, idempotency_key),
    CONSTRAINT ck_idempotency_request__scope CHECK (scope IN ('WALLET', 'SUMMON', 'PURCHASE', 'REWARD', 'CONTENT')),
    CONSTRAINT ck_idempotency_request__status CHECK (status IN ('PROCESSING', 'SUCCEEDED', 'FAILED')),
    CONSTRAINT ck_idempotency_request__completion CHECK (
        (status = 'PROCESSING' AND completed_at IS NULL)
        OR (status IN ('SUCCEEDED', 'FAILED') AND completed_at IS NOT NULL)
    )
);

CREATE TABLE audit.admin_action_log (
    admin_action_log_id bigint GENERATED ALWAYS AS IDENTITY,
    actor_id bigint NOT NULL,
    action_type varchar(64) NOT NULL,
    target_type varchar(64) NOT NULL,
    target_id varchar(128),
    reason text NOT NULL,
    before_state jsonb,
    after_state jsonb,
    correlation_id uuid NOT NULL,
    approved_by bigint,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_admin_action_log PRIMARY KEY (admin_action_log_id),
    CONSTRAINT fk_admin_action_log__actor FOREIGN KEY (actor_id) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT fk_admin_action_log__approver FOREIGN KEY (approved_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_admin_action_log__reason CHECK (length(btrim(reason)) > 0)
);

CREATE TABLE audit.security_event (
    security_event_id bigint GENERATED ALWAYS AS IDENTITY,
    player_id bigint,
    event_type varchar(64) NOT NULL,
    severity varchar(10) NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    correlation_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_security_event PRIMARY KEY (security_event_id),
    CONSTRAINT ck_security_event__severity CHECK (severity IN ('INFO', 'WARN', 'ERROR'))
);

CREATE TABLE audit.outbox_event (
    outbox_event_id bigint GENERATED ALWAYS AS IDENTITY,
    aggregate_type varchar(64) NOT NULL,
    aggregate_id varchar(128) NOT NULL,
    event_type varchar(100) NOT NULL,
    payload jsonb NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz,
    attempt_count integer NOT NULL DEFAULT 0,
    next_attempt_at timestamptz NOT NULL DEFAULT now(),
    last_error text,
    CONSTRAINT pk_outbox_event PRIMARY KEY (outbox_event_id),
    CONSTRAINT ck_outbox_event__attempt_count CHECK (attempt_count >= 0)
);
