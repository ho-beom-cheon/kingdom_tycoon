SET ROLE tycoon_owner;

CREATE TABLE game.mercenary (
    mercenary_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    player_id bigint NOT NULL,
    mercenary_template_id bigint NOT NULL,
    acquired_release_id bigint NOT NULL,
    rarity varchar(10) NOT NULL,
    level integer NOT NULL DEFAULT 1,
    exp bigint NOT NULL DEFAULT 0,
    awakening integer NOT NULL DEFAULT 0,
    status varchar(20) NOT NULL DEFAULT 'OWNED',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_mercenary PRIMARY KEY (mercenary_id),
    CONSTRAINT uk_mercenary__public_id UNIQUE (public_id),
    CONSTRAINT fk_mercenary__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_mercenary__template FOREIGN KEY (mercenary_template_id) REFERENCES master.mercenary_template(mercenary_template_id),
    CONSTRAINT fk_mercenary__acquired_release FOREIGN KEY (acquired_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT ck_mercenary__rarity CHECK (rarity IN ('C', 'B', 'A', 'S', 'SS')),
    CONSTRAINT ck_mercenary__progress CHECK (level >= 1 AND exp >= 0 AND awakening >= 0 AND version >= 0),
    CONSTRAINT ck_mercenary__status CHECK (status IN ('OWNED', 'DISPATCHED', 'LOCKED', 'DISMISSED'))
);

CREATE TABLE game.mercenary_skill (
    mercenary_id bigint NOT NULL,
    skill_id bigint NOT NULL,
    skill_level integer NOT NULL DEFAULT 1,
    unlocked_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_mercenary_skill PRIMARY KEY (mercenary_id, skill_id),
    CONSTRAINT fk_mercenary_skill__mercenary FOREIGN KEY (mercenary_id) REFERENCES game.mercenary(mercenary_id),
    CONSTRAINT fk_mercenary_skill__skill FOREIGN KEY (skill_id) REFERENCES master.skill(skill_id),
    CONSTRAINT ck_mercenary_skill__level CHECK (skill_level >= 1)
);

CREATE TABLE game.roster_slot (
    player_id bigint NOT NULL,
    slot_no smallint NOT NULL,
    mercenary_id bigint NOT NULL,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_roster_slot PRIMARY KEY (player_id, slot_no),
    CONSTRAINT uk_roster_slot__mercenary UNIQUE (mercenary_id),
    CONSTRAINT fk_roster_slot__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_roster_slot__mercenary FOREIGN KEY (mercenary_id) REFERENCES game.mercenary(mercenary_id),
    CONSTRAINT ck_roster_slot__slot CHECK (slot_no BETWEEN 1 AND 16)
);

CREATE TABLE game.party (
    party_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    player_id bigint NOT NULL,
    party_type varchar(20) NOT NULL,
    party_name varchar(40) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_party PRIMARY KEY (party_id),
    CONSTRAINT uk_party__public_id UNIQUE (public_id),
    CONSTRAINT uk_party__player_type_name UNIQUE (player_id, party_type, party_name),
    CONSTRAINT fk_party__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT ck_party__type CHECK (party_type IN ('HUNT', 'RAID', 'DEFENSE')),
    CONSTRAINT ck_party__version CHECK (version >= 0)
);

CREATE TABLE game.party_member (
    party_id bigint NOT NULL,
    slot_no smallint NOT NULL,
    mercenary_id bigint NOT NULL,
    position_code varchar(20) NOT NULL,
    CONSTRAINT pk_party_member PRIMARY KEY (party_id, slot_no),
    CONSTRAINT uk_party_member__party_mercenary UNIQUE (party_id, mercenary_id),
    CONSTRAINT fk_party_member__party FOREIGN KEY (party_id) REFERENCES game.party(party_id),
    CONSTRAINT fk_party_member__mercenary FOREIGN KEY (mercenary_id) REFERENCES game.mercenary(mercenary_id),
    CONSTRAINT ck_party_member__slot CHECK (slot_no BETWEEN 1 AND 16)
);

CREATE TABLE game.item_stack (
    player_id bigint NOT NULL,
    item_id bigint NOT NULL,
    quantity bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_item_stack PRIMARY KEY (player_id, item_id),
    CONSTRAINT fk_item_stack__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_item_stack__item FOREIGN KEY (item_id) REFERENCES master.item(item_id),
    CONSTRAINT ck_item_stack__quantity CHECK (quantity >= 0 AND version >= 0)
);

CREATE TABLE game.equipment (
    equipment_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    player_id bigint NOT NULL,
    equipment_template_id bigint NOT NULL,
    acquired_release_id bigint NOT NULL,
    grade varchar(10) NOT NULL,
    enhancement_level integer NOT NULL DEFAULT 0,
    status varchar(20) NOT NULL DEFAULT 'INVENTORY',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT pk_equipment PRIMARY KEY (equipment_id),
    CONSTRAINT uk_equipment__public_id UNIQUE (public_id),
    CONSTRAINT fk_equipment__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_equipment__template FOREIGN KEY (equipment_template_id) REFERENCES master.equipment_template(equipment_template_id),
    CONSTRAINT fk_equipment__acquired_release FOREIGN KEY (acquired_release_id) REFERENCES master.content_release(release_id),
    CONSTRAINT ck_equipment__grade CHECK (grade IN ('C', 'B', 'A', 'S', 'SS')),
    CONSTRAINT ck_equipment__enhancement CHECK (enhancement_level BETWEEN 0 AND 10 AND version >= 0),
    CONSTRAINT ck_equipment__status CHECK (status IN ('INVENTORY', 'EQUIPPED', 'LOCKED', 'CONSUMED', 'DISMANTLED'))
);

CREATE TABLE game.equipment_option (
    equipment_id bigint NOT NULL,
    option_no smallint NOT NULL,
    option_definition_id bigint NOT NULL,
    option_value numeric(19,4) NOT NULL,
    CONSTRAINT pk_equipment_option PRIMARY KEY (equipment_id, option_no),
    CONSTRAINT fk_equipment_option__equipment FOREIGN KEY (equipment_id) REFERENCES game.equipment(equipment_id),
    CONSTRAINT fk_equipment_option__definition FOREIGN KEY (option_definition_id) REFERENCES master.equipment_option_definition(option_definition_id),
    CONSTRAINT ck_equipment_option__number CHECK (option_no >= 1)
);

CREATE TABLE game.item_transaction (
    item_transaction_id bigint GENERATED ALWAYS AS IDENTITY,
    public_id uuid NOT NULL DEFAULT uuidv7(),
    request_id uuid NOT NULL,
    line_no smallint NOT NULL,
    player_id bigint NOT NULL,
    item_id bigint,
    equipment_id bigint,
    delta_quantity bigint NOT NULL,
    balance_after bigint,
    reason_code varchar(64) NOT NULL,
    reference_type varchar(40),
    reference_id varchar(128),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_item_transaction PRIMARY KEY (item_transaction_id),
    CONSTRAINT uk_item_transaction__public_id UNIQUE (public_id),
    CONSTRAINT uk_item_transaction__player_request_line UNIQUE (player_id, request_id, line_no),
    CONSTRAINT fk_item_transaction__player FOREIGN KEY (player_id) REFERENCES game.player_account(player_id),
    CONSTRAINT fk_item_transaction__item FOREIGN KEY (item_id) REFERENCES master.item(item_id),
    CONSTRAINT fk_item_transaction__equipment FOREIGN KEY (equipment_id) REFERENCES game.equipment(equipment_id),
    CONSTRAINT ck_item_transaction__target CHECK ((item_id IS NULL) <> (equipment_id IS NULL)),
    CONSTRAINT ck_item_transaction__delta CHECK (delta_quantity <> 0 AND (balance_after IS NULL OR balance_after >= 0))
);
