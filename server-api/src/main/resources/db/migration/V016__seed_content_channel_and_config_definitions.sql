SET ROLE tycoon_owner;

INSERT INTO audit.admin_actor (actor_id, actor_key, display_name, status)
    OVERRIDING SYSTEM VALUE
VALUES (1, 'SYSTEM_BOOTSTRAP', 'System Bootstrap', 'ACTIVE');

SELECT setval(
    pg_get_serial_sequence('audit.admin_actor', 'actor_id'),
    (SELECT max(actor_id) FROM audit.admin_actor),
    true
);

INSERT INTO master.currency (currency_key, currency_type)
VALUES
    ('GOLD', 'SOFT'),
    ('FREE_GEM', 'PREMIUM_FREE'),
    ('PAID_GEM', 'PREMIUM_PAID'),
    ('SPECIAL_TICKET', 'TICKET');

INSERT INTO master.runtime_config_definition
    (config_key, value_type, validation_rule, description, sensitive)
VALUES
    ('OFFLINE_REWARD_MAX_HOURS', 'INTEGER', '{"minimum":1,"maximum":24}', '오프라인 보상 최대 누적 시간', false),
    ('BATTLE_MAX_SPEED', 'DECIMAL', '{"minimum":1,"maximum":4}', '전투 최대 배속', false),
    ('ACTIVE_MERCENARY_LIMIT', 'INTEGER', '{"minimum":1,"maximum":16}', '활동 용병 최대 수', false),
    ('EQUIPMENT_INVENTORY_LIMIT', 'INTEGER', '{"minimum":1}', '장비 인벤토리 최대 수', false),
    ('ITEM_INVENTORY_LIMIT', 'INTEGER', '{"minimum":1}', '아이템 인벤토리 최대 종류 수', false),
    ('DAILY_RESET_HOUR_UTC', 'INTEGER', '{"minimum":0,"maximum":23}', '일일 초기화 UTC 시각', false),
    ('SAVE_CHECKPOINT_INTERVAL_SECONDS', 'INTEGER', '{"minimum":10}', '세이브 체크포인트 간격', false),
    ('MAX_CLIENT_CLOCK_SKEW_SECONDS', 'INTEGER', '{"minimum":0}', '허용 클라이언트 시각 오차', false);
