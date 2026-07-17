SET ROLE tycoon_owner;

DO $seed$
DECLARE
    system_actor_id bigint;
    bootstrap_release_id bigint;
    draft_release_id bigint;
BEGIN
    SELECT actor_id INTO STRICT system_actor_id
      FROM audit.admin_actor
     WHERE actor_key = 'SYSTEM_BOOTSTRAP';

    INSERT INTO master.content_release (
        release_version,
        release_name,
        status,
        minimum_client_version,
        published_at,
        checksum,
        description,
        created_by,
        approved_by,
        approved_at
    )
    VALUES (
        '0.0.0-content.0',
        'Bootstrap Empty Release',
        'PUBLISHED',
        '0.0.0',
        now(),
        repeat('0', 64),
        '채널 포인터 초기화를 위한 불변 bootstrap release',
        system_actor_id,
        system_actor_id,
        now()
    )
    RETURNING release_id INTO bootstrap_release_id;

    INSERT INTO master.runtime_config_value
        (release_id, config_key, config_value, enabled, created_by)
    VALUES
        (bootstrap_release_id, 'OFFLINE_REWARD_MAX_HOURS', '8'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'BATTLE_MAX_SPEED', '2'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'ACTIVE_MERCENARY_LIMIT', '16'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'EQUIPMENT_INVENTORY_LIMIT', '200'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'ITEM_INVENTORY_LIMIT', '300'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'DAILY_RESET_HOUR_UTC', '0'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'SAVE_CHECKPOINT_INTERVAL_SECONDS', '60'::jsonb, true, system_actor_id),
        (bootstrap_release_id, 'MAX_CLIENT_CLOCK_SKEW_SECONDS', '300'::jsonb, true, system_actor_id);

    INSERT INTO master.content_release (
        release_version,
        release_name,
        status,
        base_release_id,
        minimum_client_version,
        description,
        created_by
    )
    VALUES (
        '1.0.0-content.1',
        'Initial Content Draft',
        'DRAFT',
        bootstrap_release_id,
        '1.0.0',
        '최초 게임 콘텐츠 import 대상 draft',
        system_actor_id
    )
    RETURNING release_id INTO draft_release_id;

    INSERT INTO master.runtime_config_value
        (release_id, config_key, config_value, enabled, created_by)
    SELECT draft_release_id, config_key, config_value, enabled, system_actor_id
      FROM master.runtime_config_value
     WHERE release_id = bootstrap_release_id;

    INSERT INTO master.content_channel
        (channel_code, active_release_id, previous_release_id, version, updated_by)
    VALUES
        ('DEV', bootstrap_release_id, NULL, 0, system_actor_id),
        ('STAGING', bootstrap_release_id, NULL, 0, system_actor_id),
        ('PRODUCTION', bootstrap_release_id, NULL, 0, system_actor_id);
END
$seed$;
