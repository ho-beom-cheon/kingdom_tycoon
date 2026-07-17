SET ROLE tycoon_owner;

CREATE TABLE master.item (
    item_id bigint GENERATED ALWAYS AS IDENTITY,
    item_key varchar(64) NOT NULL,
    item_type varchar(20) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_item PRIMARY KEY (item_id),
    CONSTRAINT uk_item__item_key UNIQUE (item_key),
    CONSTRAINT ck_item__key CHECK (item_key ~ '^[A-Z0-9_]+$')
);

CREATE TABLE master.item_balance (
    release_id bigint NOT NULL,
    item_id bigint NOT NULL,
    max_stack bigint NOT NULL,
    sell_currency_key varchar(64),
    sell_amount bigint,
    usable boolean NOT NULL DEFAULT false,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_item_balance PRIMARY KEY (release_id, item_id),
    CONSTRAINT fk_item_balance__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_item_balance__item FOREIGN KEY (item_id) REFERENCES master.item(item_id),
    CONSTRAINT fk_item_balance__sell_currency FOREIGN KEY (sell_currency_key) REFERENCES master.currency(currency_key),
    CONSTRAINT fk_item_balance__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_item_balance__values CHECK (
        max_stack > 0 AND sell_amount >= 0 AND ((sell_currency_key IS NULL) = (sell_amount IS NULL))
    )
);

CREATE TABLE master.equipment_template (
    equipment_template_id bigint GENERATED ALWAYS AS IDENTITY,
    equipment_key varchar(64) NOT NULL,
    equipment_slot varchar(20) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_equipment_template PRIMARY KEY (equipment_template_id),
    CONSTRAINT uk_equipment_template__equipment_key UNIQUE (equipment_key),
    CONSTRAINT ck_equipment_template__key CHECK (equipment_key ~ '^[A-Z0-9_]+$'),
    CONSTRAINT ck_equipment_template__slot CHECK (equipment_slot IN ('WEAPON', 'ARMOR', 'ACCESSORY', 'BOOTS'))
);

CREATE TABLE master.equipment_balance (
    release_id bigint NOT NULL,
    equipment_template_id bigint NOT NULL,
    grade varchar(10) NOT NULL,
    base_stat_code varchar(32) NOT NULL,
    base_stat_value numeric(19,4) NOT NULL,
    level_requirement integer NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint NOT NULL,
    CONSTRAINT pk_equipment_balance PRIMARY KEY (release_id, equipment_template_id),
    CONSTRAINT fk_equipment_balance__release FOREIGN KEY (release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT fk_equipment_balance__template FOREIGN KEY (equipment_template_id) REFERENCES master.equipment_template(equipment_template_id),
    CONSTRAINT fk_equipment_balance__creator FOREIGN KEY (created_by) REFERENCES audit.admin_actor(actor_id),
    CONSTRAINT ck_equipment_balance__grade CHECK (grade IN ('C', 'B', 'A', 'S', 'SS')),
    CONSTRAINT ck_equipment_balance__values CHECK (base_stat_value >= 0 AND level_requirement >= 1)
);

CREATE TABLE master.equipment_option_definition (
    option_definition_id bigint GENERATED ALWAYS AS IDENTITY,
    option_key varchar(64) NOT NULL,
    stat_code varchar(32) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_equipment_option_definition PRIMARY KEY (option_definition_id),
    CONSTRAINT uk_equipment_option_definition__option_key UNIQUE (option_key),
    CONSTRAINT ck_equipment_option_definition__key CHECK (option_key ~ '^[A-Z0-9_]+$')
);
