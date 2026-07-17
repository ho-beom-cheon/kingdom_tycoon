# 타이쿤 서버 DB 인터페이스 설계서 v1.0

> Codex 구현 전달용 기준 문서
> 작성 기준일: 2026-07-17 (Asia/Seoul)
> 대상: Unity 클라이언트 + Java/Spring 서버 + PostgreSQL 18.4
> 상태: 구현 기준선(Baseline)

---

## 0. Codex 실행 지시

이 문서는 타이쿤 프로젝트의 PostgreSQL 스키마, 서버 저장소 인터페이스, 트랜잭션 경계, 운영용 마스터 데이터 배포 체계를 정의한다.

Codex는 다음 순서로 구현한다.

1. 현재 저장소의 기술 스택, 패키지 구조, 기존 엔티티와 Flyway 설정을 먼저 분석한다.
2. 기존 구현과 충돌하면 이 문서의 데이터 무결성·서버 권한·운영 버전 원칙은 유지하고, 파일 경로와 클래스 이름만 저장소 규칙에 맞춘다.
3. PostgreSQL 18.4용 Docker Compose 개발 환경을 구성한다.
4. Flyway 마이그레이션을 아래 단계 순서대로 작성한다.
5. Spring Repository/Service 인터페이스와 트랜잭션을 구현한다.
6. Testcontainers 기반 통합 테스트로 제약조건, 동시성, 중복 요청 방지를 검증한다.
7. 구현 완료 후 ERD, 마이그레이션 목록, 실행 명령, 테스트 결과를 문서화한다.

금지 사항:

- Unity 클라이언트가 PostgreSQL에 직접 접속하면 안 된다.
- 재화, 모집, 결제, 보상 결과를 클라이언트 값만 믿고 저장하면 안 된다.
- 운영 중 `PUBLISHED` 상태의 마스터 데이터를 직접 수정하거나 삭제하면 안 된다.
- 이미 실행된 Flyway versioned migration 파일을 수정하면 안 된다.
- 재화 잔액만 변경하고 원장을 남기지 않는 코드를 만들면 안 된다.
- 운영 편의를 이유로 모든 마스터 데이터를 하나의 범용 JSONB 테이블에 넣으면 안 된다.

---

## 1. 목표와 범위

### 1.1 목표

- 플레이어 상태를 서버 권한으로 안전하게 저장한다.
- 재화·보상·모집·결제 요청의 중복 처리를 방지한다.
- 직업, 용병, 장비, 스테이지, 드롭, 시설, 보상, 확률을 운영 중 DB에서 조정한다.
- 운영 데이터 변경 시 초안, 검증, 승인, 예약 배포, 즉시 배포, 롤백을 지원한다.
- 서버 재배포 없이 활성 콘텐츠 버전을 변경한다.
- 게임 상태와 콘텐츠 버전의 호환성을 추적한다.
- 초기 1.0 범위인 5직업, 5지역, 2레이드, 8시설, 활동 용병 16명을 지원한다.

### 1.2 이번 구현 범위

- PostgreSQL 데이터베이스 및 역할
- `game`, `master`, `ops`, `billing`, `audit` 스키마
- 플레이어 핵심 상태
- 버전형 마스터 데이터
- 재화 원장
- 보상 지급
- 특별 모집 및 천장
- 시설, 지역, 스테이지, 레이드 진행도
- 결제 저장 구조와 영수증 중복 방지 인터페이스
- 운영 콘텐츠 배포 및 롤백
- Flyway 및 Testcontainers 테스트

### 1.3 이번 구현에서 실제 외부 연동하지 않는 범위

다음 항목은 DB 인터페이스와 교체 가능한 서버 포트만 준비하고 실제 사업자·플랫폼은 1.0 개발 완료 후 결정한다.

- 로그인 사업자 및 인증 토큰 검증
- 클라우드 배포 플랫폼과 운영 리전
- Google Play/Apple 영수증 검증 호출
- 실제 결제 상품 ID
- 분석·크래시 수집 도구
- 외부 관리자 인증 체계
- Redis 도입 여부

---

## 2. 데이터베이스 토폴로지

### 2.1 PostgreSQL 버전

- 기준 버전: PostgreSQL 18.4
- 운영에서는 PostgreSQL 18 계열의 최신 minor 버전을 유지한다.
- PostgreSQL 19는 2026-07-17 현재 beta이므로 운영 기준으로 사용하지 않는다.

### 2.2 환경별 DB 이름

| 환경 | DB 이름 | 비고 |
|---|---|---|
| 로컬 | `tycoon_local` | 개발자 PC Docker |
| 공용 개발 | `tycoon_dev` | 팀 개발용 |
| 자동 테스트 | `tycoon_test` | Testcontainers 임시 DB |
| 스테이징 | `tycoon_stg` | 운영 전 검증 |
| 운영 | `tycoon_prod` | 별도 인스턴스/클러스터 원칙 |

DB 이름에 출시명을 사용하지 않는다. 게임 이름이 변경되어도 내부 DB 식별자는 유지한다.

### 2.3 스키마

| 스키마 | 책임 |
|---|---|
| `game` | 계정, 보유 자산, 진행도, 재화, 모집 결과 등 플레이어 상태 |
| `master` | 버전형 게임 정의와 밸런스 데이터 |
| `ops` | 기간형 이벤트, 모집 배너, 상점, 공지, 점검 |
| `billing` | 결제 주문, 영수증, 검증 이벤트, 환불 |
| `audit` | 관리자 감사, 보안 이벤트, idempotency, outbox |

`public` 스키마에는 애플리케이션 객체를 생성하지 않는다.

### 2.4 DB 역할

| 역할 | LOGIN | 책임 |
|---|---:|---|
| `tycoon_owner` | N | 모든 객체 소유자 |
| `tycoon_migrator` | Y | Flyway DDL 및 migration 실행 |
| `tycoon_app` | Y | 게임 서버 DML |
| `tycoon_ops` | Y | 관리자 서버의 master/ops 쓰기 |
| `tycoon_readonly` | Y | 장애 분석용 읽기 전용 |

규칙:

- 애플리케이션은 owner/superuser 계정으로 접속하지 않는다.
- `tycoon_app`은 `master`와 `ops`를 읽을 수 있지만 콘텐츠 배포 데이터를 직접 수정할 수 없다.
- `tycoon_ops`는 플레이어 재화·아이템을 직접 수정하지 않고 관리자 보상 서비스를 호출한다.
- `PUBLIC`의 스키마 객체 생성 권한을 제거한다.

---

## 3. 공통 설계 규칙

### 3.1 이름 규칙

- 테이블/컬럼/인덱스: `snake_case`
- PK: `<entity>_id`
- 외부 공개 식별자: `public_id`
- 콘텐츠 안정 키: `<entity>_key`
- FK: 참조 대상 PK 이름과 동일
- UNIQUE: `uk_<table>__<columns>`
- 일반 인덱스: `ix_<table>__<columns>`
- CHECK: `ck_<table>__<meaning>`
- FK: `fk_<table>__<target>`
- 타임스탬프: `_at`
- 날짜: `_date`
- 수량: `_quantity`
- 금액: `_amount`
- 확률/비율: `_rate`

### 3.2 자료형

| 데이터 | PostgreSQL 타입 | 규칙 |
|---|---|---|
| 내부 PK | `bigint generated always as identity` | 단일 DB 내부 관계용 |
| 외부 공개 ID | `uuid default uuidv7()` | API/로그/멱등 요청 ID |
| 콘텐츠 키 | `varchar(64)` | 영문 대문자+숫자+밑줄 권장 |
| 상태 코드 | `varchar(20~32)` | native enum 사용 금지, CHECK 적용 |
| 시각 | `timestamptz` | UTC 저장 |
| 날짜 | `date` | 일일 초기화 기준일 |
| 재화/수량 | `bigint` | 부동소수점 금지 |
| 확률 | `numeric(12,8)` | `0 <= rate <= 1` |
| 가중치 | `bigint` | 0 이상 |
| 실제 결제액 | `numeric(19,4)` | ISO 통화 코드와 함께 저장 |
| 유연 설정/스냅샷 | `jsonb` | 제한된 위치에서만 사용 |
| 해시 | `varchar(128)` 또는 `bytea` | 원문 토큰 저장 금지 |

PostgreSQL 18의 `uuidv7()`을 사용해 시간 정렬 가능한 UUID를 생성한다. 외부 API에서는 내부 bigint PK를 노출하지 않는다.

### 3.3 공통 컬럼

변경 가능한 aggregate 테이블:

```sql
created_at timestamptz NOT NULL DEFAULT now(),
updated_at timestamptz NOT NULL DEFAULT now(),
version bigint NOT NULL DEFAULT 0
```

운영 마스터 revision 테이블:

```sql
release_id bigint NOT NULL,
enabled boolean NOT NULL DEFAULT true,
created_at timestamptz NOT NULL DEFAULT now(),
created_by bigint NOT NULL
```

### 3.4 삭제 정책

- 플레이어 계정: 탈퇴 상태와 익명화 시각을 기록한다.
- 플레이어 보유 자산: 물리 삭제 대신 소비/분해 원장을 남기고 상태를 변경한다.
- 이미 참조된 마스터 키: 삭제하지 않고 `enabled=false`로 비활성화한다.
- 원장, 결제 이벤트, 관리자 감사: 애플리케이션 UPDATE/DELETE 금지.
- 보관 기간 종료에 따른 삭제는 별도 배치와 운영 정책으로 수행한다.

### 3.5 JSONB 허용 범위

허용:

- 세이브 복구용 snapshot
- 단순 runtime config 값
- 외부 영수증의 비민감 정규화 결과
- 감사 로그의 before/after
- outbox 이벤트 payload
- 전투 결과 요약

금지:

- 재화 잔액
- 모집 천장 상태
- 장비/용병 보유 목록 전체
- 시설/스테이지 진행도 전체
- 관계형 검증이 필요한 직업·아이템·드롭·강화 마스터

---

## 4. 콘텐츠 버전 및 운영 배포 모델

### 4.1 불변 원칙

1. `PUBLISHED` release의 revision 행은 수정하지 않는다.
2. 변경은 기존 release를 복제한 새 `DRAFT` release에서 수행한다.
3. 검증 오류가 1건이라도 `ERROR`이면 승인/배포할 수 없다.
4. 운영 배포는 `master.content_channel`의 활성 release 포인터를 원자적으로 변경한다.
5. 롤백은 과거 행을 되돌려 쓰지 않고 채널 포인터를 이전 release로 변경한다.
6. 배포와 롤백은 감사 로그를 남긴다.

### 4.2 핵심 테이블

#### `master.content_release`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `release_id` | bigint identity | PK |
| `public_id` | uuid | UNIQUE, default uuidv7() |
| `release_version` | varchar(30) | UNIQUE, 예: `1.0.0-content.12` |
| `release_name` | varchar(100) | NOT NULL |
| `status` | varchar(20) | DRAFT/VALIDATING/APPROVED/SCHEDULED/PUBLISHED/ROLLED_BACK/ARCHIVED |
| `base_release_id` | bigint | NULL, 자기참조 FK |
| `minimum_client_version` | varchar(30) | NOT NULL |
| `scheduled_at` | timestamptz | NULL |
| `published_at` | timestamptz | NULL |
| `checksum` | varchar(64) | 승인 시 계산 |
| `description` | text | NULL |
| `created_by` | bigint | 운영 관리자 FK |
| `approved_by` | bigint | NULL |
| `created_at` | timestamptz | NOT NULL |
| `approved_at` | timestamptz | NULL |

#### `master.content_channel`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `channel_code` | varchar(20) | PK: DEV/STAGING/PRODUCTION |
| `active_release_id` | bigint | FK, NOT NULL |
| `previous_release_id` | bigint | FK, NULL |
| `version` | bigint | 낙관적 잠금/캐시 변경 감지 |
| `updated_by` | bigint | 관리자 FK |
| `updated_at` | timestamptz | NOT NULL |

#### `master.content_validation_result`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `validation_result_id` | bigint identity | PK |
| `release_id` | bigint | FK |
| `validation_code` | varchar(64) | NOT NULL |
| `severity` | varchar(10) | INFO/WARN/ERROR |
| `target_type` | varchar(40) | NOT NULL |
| `target_key` | varchar(128) | NULL |
| `message` | text | NOT NULL |
| `passed` | boolean | NOT NULL |
| `created_at` | timestamptz | NOT NULL |

#### `master.content_publish_history`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `publish_history_id` | bigint identity | PK |
| `channel_code` | varchar(20) | FK |
| `from_release_id` | bigint | NULL |
| `to_release_id` | bigint | NOT NULL |
| `action_type` | varchar(20) | PUBLISH/ROLLBACK |
| `reason` | text | NOT NULL |
| `actor_id` | bigint | 관리자 FK |
| `created_at` | timestamptz | NOT NULL |

#### `master.runtime_config_definition`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `config_key` | varchar(100) | PK |
| `value_type` | varchar(20) | INTEGER/DECIMAL/BOOLEAN/STRING/JSON |
| `validation_rule` | jsonb | min/max/정규식/schema |
| `description` | text | NOT NULL |
| `sensitive` | boolean | NOT NULL default false |

#### `master.runtime_config_value`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `release_id` | bigint | PK 일부, FK |
| `config_key` | varchar(100) | PK 일부, FK |
| `config_value` | jsonb | NOT NULL |
| `enabled` | boolean | NOT NULL |
| `created_by` | bigint | NOT NULL |
| `created_at` | timestamptz | NOT NULL |

필수 초기 키:

```text
OFFLINE_REWARD_MAX_HOURS
BATTLE_MAX_SPEED
ACTIVE_MERCENARY_LIMIT = 16
EQUIPMENT_INVENTORY_LIMIT
ITEM_INVENTORY_LIMIT
DAILY_RESET_HOUR_UTC
SAVE_CHECKPOINT_INTERVAL_SECONDS
MAX_CLIENT_CLOCK_SKEW_SECONDS
```

---

## 5. 마스터 데이터 테이블 카탈로그

### 5.1 기본 패턴

안정 식별 테이블은 버전과 무관한 키를 가진다.

```text
master.job(job_id, job_key, created_at)
```

조정 가능한 값은 release별 revision 테이블에 둔다.

```text
master.job_balance(release_id, job_id, ..., enabled)
PK(release_id, job_id)
```

플레이어 데이터는 revision PK가 아니라 안정 키 또는 안정 ID를 참조한다. 획득 당시 버전은 `acquired_release_id`로 별도 기록한다.

### 5.2 직업·용병·스킬

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.job` | `job_id` | `job_key` UNIQUE |
| `master.job_balance` | `(release_id, job_id)` | hp/attack/defense/speed/critical/growth, enabled |
| `master.mercenary_template` | `mercenary_template_id` | `mercenary_key` UNIQUE, `job_id` FK |
| `master.mercenary_balance` | `(release_id, mercenary_template_id)` | rarity, base stats, growth coefficients, summonable |
| `master.skill` | `skill_id` | `skill_key` UNIQUE, skill_type |
| `master.skill_balance` | `(release_id, skill_id, skill_level)` | cooldown, power_rate, range, duration, cost |
| `master.mercenary_skill_map` | `(release_id, mercenary_template_id, skill_id)` | unlock_level, slot_no |
| `master.level_curve` | `(release_id, curve_type, level)` | required_exp, cumulative_exp |

제약:

- 초기 release에 활성 직업은 정확히 5개여야 한다.
- 용병 레벨/스킬 레벨에 중간 누락이 없어야 한다.
- 확률/critical rate는 0~1 범위다.
- 계산 가능한 최종 전투력은 저장하지 않는다.

### 5.3 아이템·장비·강화·제작

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.item` | `item_id` | `item_key` UNIQUE, item_type |
| `master.item_balance` | `(release_id, item_id)` | max_stack, sell_currency_key, sell_amount, usable |
| `master.equipment_template` | `equipment_template_id` | `equipment_key` UNIQUE, equipment_slot |
| `master.equipment_balance` | `(release_id, equipment_template_id)` | grade, base stat, level requirement |
| `master.equipment_option_definition` | `option_definition_id` | `option_key` UNIQUE, stat_code |
| `master.equipment_option_pool` | `(release_id, pool_key, option_definition_id)` | min_value, max_value, weight |
| `master.enhancement_rule` | `(release_id, equipment_grade, enhancement_level)` | success_rate, cost, fail_rule_key |
| `master.enhancement_failure_rule` | `(release_id, fail_rule_key)` | downgrade_count, destroy_rate, protection_allowed |
| `master.recipe` | `recipe_id` | `recipe_key` UNIQUE, facility_id |
| `master.recipe_balance` | `(release_id, recipe_id)` | duration_seconds, unlock_condition |
| `master.recipe_material` | `(release_id, recipe_id, item_id)` | required_quantity |
| `master.recipe_output` | `(release_id, recipe_id, output_no)` | reward_type/key/quantity |

### 5.4 지역·스테이지·몬스터·드롭

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.region` | `region_id` | `region_key` UNIQUE |
| `master.region_balance` | `(release_id, region_id)` | unlock condition, recommended power, sort_order |
| `master.stage` | `stage_id` | `stage_key` UNIQUE, region_id |
| `master.stage_balance` | `(release_id, stage_id)` | stamina_cost, time_limit, reward_group_id |
| `master.monster` | `monster_id` | `monster_key` UNIQUE, monster_type |
| `master.monster_balance` | `(release_id, monster_id)` | hp/attack/defense/speed, drop_table_id |
| `master.stage_monster` | `(release_id, stage_id, wave_no, spawn_no)` | monster_id, quantity, level |
| `master.spawn_wave` | `(release_id, stage_id, wave_no)` | starts_after_seconds, clear_condition |
| `master.drop_table` | `drop_table_id` | `drop_table_key` UNIQUE |
| `master.drop_table_entry` | `(release_id, drop_table_id, entry_no)` | reward_type/key, weight, min/max quantity |

제약:

- 초기 release의 활성 지역은 정확히 5개다.
- stage sort order와 wave_no는 콘텐츠 내부에서 연속되어야 한다.
- `minimum_quantity <= maximum_quantity`다.
- 가중치 합계가 0인 활성 드롭 테이블은 배포할 수 없다.

### 5.5 레이드

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.raid` | `raid_id` | `raid_key` UNIQUE |
| `master.raid_difficulty` | `(release_id, raid_id, difficulty_code)` | entry cost, recommended power, reward_group_id |
| `master.raid_phase` | `(release_id, raid_id, difficulty_code, phase_no)` | hp threshold, skill pattern key |
| `master.raid_reward_rank` | `(release_id, raid_id, difficulty_code, rank_code)` | min_score, max_score, reward_group_id |
| `master.raid_schedule_rule` | `(release_id, raid_id, rule_no)` | days_of_week, open/close time |

초기 release의 활성 raid는 정확히 2개다.

### 5.6 마을·시설

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.facility` | `facility_id` | `facility_key` UNIQUE, facility_type |
| `master.facility_level` | `(release_id, facility_id, facility_level)` | cost, duration, town requirement, slot count |
| `master.facility_production` | `(release_id, facility_id, facility_level, production_no)` | item/reward, cycle, quantity |
| `master.facility_worker_rule` | `(release_id, facility_id, job_id)` | bonus_type/value, max_workers |
| `master.town_level` | `(release_id, town_level)` | required_exp, unlocks, max facility level |

초기 release의 활성 시설은 정확히 8개다.

### 5.7 공통 보상·임무·업적

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `master.reward_group` | `reward_group_id` | `reward_group_key` UNIQUE |
| `master.reward_entry` | `(release_id, reward_group_id, entry_no)` | reward_type/key, quantity, probability/weight |
| `master.mission` | `mission_id` | `mission_key` UNIQUE, mission_type |
| `master.mission_revision` | `(release_id, mission_id)` | target_type/value, reset_type, reward_group_id |
| `master.achievement` | `achievement_id` | `achievement_key` UNIQUE |
| `master.achievement_revision` | `(release_id, achievement_id)` | target_type/value, reward_group_id |
| `master.offline_reward_rule` | `(release_id, rule_level)` | seconds_per_tick, cap, reward_group_id |
| `master.localization_text` | `(release_id, locale, text_key)` | text_value |

보상 규칙:

- 고정 보상은 `quantity`를 사용한다.
- 확률 보상은 `probability` 또는 `weight` 중 하나의 방식만 사용한다.
- 보상 지급 시 적용한 `release_id`와 `reward_group_id`를 지급 원장에 기록한다.

---

## 6. 플레이어 데이터 테이블 카탈로그

### 6.1 계정·인증·세이브

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.player_account` | `player_id` | public_id UNIQUE, status, locale, created/last_login/deleted_at |
| `game.account_identity` | `identity_id` | player_id, provider, provider_subject_hash, UNIQUE(provider, subject_hash) |
| `game.refresh_token` | `refresh_token_id` | player_id, token_hash UNIQUE, device_id, expires/revoked_at |
| `game.player_profile` | `player_id` | nickname UNIQUE, level, exp, tutorial_step, version |
| `game.player_setting` | `player_id` | locale, sound/music/vibration, notification flags, version |
| `game.save_checkpoint` | `checkpoint_id` | player_id, save/game/content version, revision, snapshot_json, checksum |

`save_checkpoint`는 복구용이고 진실 공급원(source of truth)이 아니다. 정상 요청은 정규화된 테이블을 사용한다.

### 6.2 용병·편성

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.mercenary` | `mercenary_id` | public_id, player_id, template_id, acquired_release_id, rarity, level, exp, awakening, status |
| `game.mercenary_skill` | `(mercenary_id, skill_id)` | skill_level, unlocked_at |
| `game.roster_slot` | `(player_id, slot_no)` | mercenary_id UNIQUE, `1 <= slot_no <= 16` |
| `game.party` | `party_id` | player_id, party_type, party_name, version |
| `game.party_member` | `(party_id, slot_no)` | mercenary_id, position_code |

FK는 플레이어 간 용병 참조를 완전히 방지하지 못하므로 Service 계층에서도 `mercenary.player_id == party.player_id`를 검증한다.

### 6.3 아이템·장비·제작

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.item_stack` | `(player_id, item_id)` | quantity >= 0, version |
| `game.equipment` | `equipment_id` | public_id, player_id, template_id, acquired_release_id, grade, enhance level, status |
| `game.equipment_option` | `(equipment_id, option_no)` | option_definition_id, option_value |
| `game.recipe_unlock` | `(player_id, recipe_id)` | unlocked_at |
| `game.crafting_job` | `crafting_job_id` | player_id, facility_instance_id, recipe_id, status, started/finishes/claimed_at |
| `game.item_transaction` | `item_transaction_id` | request_id, player_id, item/equipment ref, delta, reason, balance_after |

`item_transaction`의 `(player_id, request_id, line_no)`는 UNIQUE다.

### 6.4 마을·시설

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.town_state` | `player_id` | town_level, town_exp, version |
| `game.player_facility` | `facility_instance_id` | public_id, player_id, facility_id, level, slot_no, status |
| `game.facility_job` | `facility_job_id` | facility_instance_id, job_type, target_level, starts/finishes/claimed_at |
| `game.facility_worker` | `(facility_instance_id, worker_slot_no)` | mercenary_id UNIQUE |
| `game.facility_storage` | `(facility_instance_id, item_id)` | quantity >= 0, last_produced_at |

### 6.5 지역·전투·레이드·오프라인 보상

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.region_progress` | `(player_id, region_id)` | status, unlocked_at, highest_stage_id |
| `game.stage_progress` | `(player_id, stage_id)` | clear_count, best_time_ms, stars, first/last_clear_at |
| `game.raid_progress` | `(player_id, raid_id, difficulty_code)` | clear_count, best_score, entries_used, reset_date |
| `game.battle_record` | `battle_record_id` | request_id UNIQUE, player_id, content type/key, release_id, result, reward_grant_id |
| `game.offline_reward_claim` | `offline_claim_id` | request_id UNIQUE, player_id, from/to, capped_seconds, reward_grant_id |

전투의 프레임 단위 로그는 OLTP DB에 저장하지 않는다. 결과 검증에 필요한 seed, 편성 snapshot hash, 승패, 시간, 보상 요약만 저장한다.

### 6.6 재화·보상

#### `game.wallet`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `player_id` | bigint | PK 일부, FK |
| `currency_key` | varchar(64) | PK 일부 |
| `balance` | bigint | NOT NULL, `balance >= 0` |
| `version` | bigint | NOT NULL |
| `created_at` | timestamptz | NOT NULL |
| `updated_at` | timestamptz | NOT NULL |

#### `game.wallet_transaction`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `wallet_transaction_id` | bigint identity | PK |
| `public_id` | uuid | UNIQUE |
| `request_id` | uuid | 멱등 요청 ID |
| `line_no` | smallint | 복수 재화 처리 순서 |
| `player_id` | bigint | FK |
| `currency_key` | varchar(64) | NOT NULL |
| `delta_amount` | bigint | 0 금지 |
| `balance_before` | bigint | NOT NULL |
| `balance_after` | bigint | NOT NULL, 0 이상 |
| `reason_code` | varchar(64) | NOT NULL |
| `reference_type` | varchar(40) | NULL |
| `reference_id` | varchar(128) | NULL |
| `created_at` | timestamptz | NOT NULL |

UNIQUE: `(player_id, request_id, line_no)`.

`wallet_transaction`은 append-only다. 취소는 반대 방향 보정 거래를 추가한다.

#### `game.reward_grant`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `reward_grant_id` | bigint identity | PK |
| `public_id` | uuid | UNIQUE |
| `request_id` | uuid | UNIQUE(player_id, request_id) |
| `player_id` | bigint | FK |
| `release_id` | bigint | 적용한 콘텐츠 버전 |
| `reward_group_id` | bigint | NULL 가능 |
| `source_type` | varchar(40) | BATTLE/MAIL/MISSION/PURCHASE/ADMIN 등 |
| `source_id` | varchar(128) | NOT NULL |
| `status` | varchar(20) | PROCESSING/GRANTED/FAILED/REVERSED |
| `created_at` | timestamptz | NOT NULL |
| `completed_at` | timestamptz | NULL |

#### `game.reward_grant_item`

| 컬럼 | 타입 | 제약/설명 |
|---|---|---|
| `reward_grant_id` | bigint | PK 일부 |
| `line_no` | smallint | PK 일부 |
| `reward_type` | varchar(20) | CURRENCY/ITEM/EQUIPMENT/MERCENARY/EXP |
| `reward_key` | varchar(64) | NOT NULL |
| `quantity` | bigint | NOT NULL |
| `created_entity_public_id` | uuid | 생성형 보상일 때 저장 |

### 6.7 모집·천장

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.summon_pity` | `(player_id, pity_group_key)` | pull_count, guaranteed_state, version |
| `game.summon_transaction` | `summon_transaction_id` | request_id UNIQUE, player_id, banner_id, release_id, pull_count, cost grant/ref |
| `game.summon_result` | `(summon_transaction_id, result_no)` | result_type/key, rarity, guaranteed flag, created entity ID |

천장은 banner가 아니라 `pity_group_key` 단위로 설계한다. 배너 교체 시 천장 공유/초기화 정책을 운영에서 선택할 수 있어야 한다.

### 6.8 임무·업적·우편

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `game.daily_state` | `player_id` | reset_date, login streak, counters version |
| `game.mission_progress` | `(player_id, mission_id, period_key)` | progress, target, status, claimed_at |
| `game.achievement_progress` | `(player_id, achievement_id)` | progress, status, claimed_at |
| `game.player_mail` | `player_mail_id` | player_id, mail template/source, expires/read/claimed_at |
| `game.mail_attachment` | `(player_mail_id, line_no)` | reward type/key/quantity |

---

## 7. 기간형 운영 테이블

| 테이블 | 핵심 컬럼 |
|---|---|
| `ops.live_event` | event_key, status, starts_at, ends_at, priority, target condition |
| `ops.event_mission` | event_id, mission_id, order_no |
| `ops.event_reward` | event_id, condition, reward_group_id |
| `ops.summon_banner` | banner_key, pity_group_key, starts_at, ends_at, currency/cost, pull unit |
| `ops.summon_pool` | banner_id, pool group, result key, rarity, weight |
| `ops.shop_offer` | offer_key, product type, price type/value, starts_at, ends_at, limit rule |
| `ops.shop_offer_content` | offer_id, line_no, reward type/key/quantity |
| `ops.attendance_campaign` | campaign_key, starts_at, ends_at, reset rule |
| `ops.attendance_reward` | campaign_id, day_no, reward_group_id |
| `ops.notice` | locale, title, body, starts_at, ends_at, priority |
| `ops.maintenance` | starts_at, ends_at, allowlist, message_key |

공통 규칙:

- `starts_at < ends_at` CHECK를 둔다.
- `enabled`, `status`, `minimum_client_version`을 둔다.
- 대상 조건은 제한된 JSON 조건 DSL을 사용하며 서버 검증기를 둔다.
- 같은 key의 겹치는 기간 허용 여부를 검증한다.
- 모집 pool 가중치와 상품 구성은 활성화 전 검증한다.

---

## 8. 결제 테이블

### 8.1 테이블

| 테이블 | PK | 중요 컬럼/제약 |
|---|---|---|
| `billing.payment_product` | `payment_product_id` | internal product key, store, store_product_id UNIQUE |
| `billing.payment_order` | `payment_order_id` | public_id, player_id, product_id, expected price/currency, status |
| `billing.store_receipt` | `store_receipt_id` | store, transaction_id UNIQUE, purchase_token_hash UNIQUE, verification status |
| `billing.payment_event` | `payment_event_id` | order_id, event type, store status, normalized payload, created_at |
| `billing.refund_record` | `refund_record_id` | receipt_id, refund transaction ID UNIQUE, reason, processed_at |
| `game.shop_purchase` | `shop_purchase_id` | request_id UNIQUE, player_id, offer_id, payment_order/ref, reward_grant_id |
| `game.purchase_limit` | `(player_id, limit_key, period_key)` | purchase_count, version |

### 8.2 결제 원칙

- 클라이언트의 결제 성공 응답만으로 보상을 지급하지 않는다.
- 스토어 서버 검증 성공과 영수증 식별자 UNIQUE 확보 후 보상을 지급한다.
- 결제 주문 상태 변경은 `payment_event`에도 append-only로 기록한다.
- 동일 영수증은 다른 계정에서 재사용할 수 없다.
- 환불은 보상 회수 정책과 별개로 반드시 기록한다.
- 원문 영수증/토큰은 암호화 또는 해시하며 로그에 출력하지 않는다.

---

## 9. 감사·멱등·이벤트 전달

### 9.1 테이블

| 테이블 | 역할 |
|---|---|
| `audit.idempotency_request` | API 요청 중복 처리 방지와 동일 응답 재사용 |
| `audit.admin_action_log` | 관리자 변경 전/후, 사유, 승인자, correlation ID |
| `audit.security_event` | 로그인 이상, 영수증 재사용, 시간 변조 의심 |
| `audit.outbox_event` | DB commit과 이벤트 발행의 원자성 보장 |

### 9.2 `audit.idempotency_request`

| 컬럼 | 타입 | 설명 |
|---|---|---|
| `idempotency_id` | bigint identity | PK |
| `scope` | varchar(40) | WALLET/SUMMON/PURCHASE/REWARD 등 |
| `player_id` | bigint | NULL 가능 |
| `idempotency_key` | uuid | 요청 키 |
| `request_hash` | varchar(64) | 같은 키의 다른 payload 방지 |
| `status` | varchar(20) | PROCESSING/SUCCEEDED/FAILED |
| `response_code` | integer | NULL |
| `response_body` | jsonb | 민감정보 제외 |
| `expires_at` | timestamptz | 정리 기준 |
| `created_at` | timestamptz | NOT NULL |
| `completed_at` | timestamptz | NULL |

UNIQUE: `(scope, player_id, idempotency_key)`.

### 9.3 Outbox

게임 트랜잭션 안에서 `audit.outbox_event`를 INSERT한다. 별도 publisher가 미발행 행을 `FOR UPDATE SKIP LOCKED`로 읽어 전달한다.

필드:

```text
outbox_event_id
aggregate_type
aggregate_id
event_type
payload
occurred_at
published_at
attempt_count
next_attempt_at
last_error
```

---

## 10. 서버 DB 인터페이스

### 10.1 계층 원칙

```text
Controller/API
  -> Application Service
    -> Domain Service
      -> Repository Port
        -> JPA/JDBC Adapter
          -> PostgreSQL
```

- Controller에서 Repository를 직접 호출하지 않는다.
- critical transaction은 Application Service의 public method에 둔다.
- 조회 전용 projection은 JPA entity와 분리할 수 있다.
- wallet/summon/purchase처럼 잠금과 원장 insert가 중요한 부분은 명시적 query 또는 JDBC 사용을 허용한다.
- DB entity를 API 응답으로 직접 노출하지 않는다.

### 10.2 Repository Port

```java
public interface ActiveContentRepository {
    ActiveContentVersion findActive(String channelCode);
    ContentSnapshot loadSnapshot(long releaseId);
}

public interface ContentReleaseRepository {
    ContentRelease createDraft(long baseReleaseId, AdminActor actor);
    ValidationReport validate(long releaseId);
    void approve(long releaseId, AdminActor actor);
    PublishResult publish(String channelCode, long releaseId, long expectedChannelVersion, AdminActor actor);
    PublishResult rollback(String channelCode, long targetReleaseId, long expectedChannelVersion, AdminActor actor);
}

public interface WalletRepository {
    WalletBalance lockByPlayerAndCurrency(long playerId, String currencyKey);
    void insertLedger(WalletLedgerEntry entry);
    void updateBalance(long playerId, String currencyKey, long newBalance, long expectedVersion);
}

public interface RewardGrantRepository {
    Optional<RewardGrant> findByRequestId(long playerId, UUID requestId);
    RewardGrant begin(RewardGrantCommand command);
    void appendResult(RewardGrantLine line);
    void complete(long rewardGrantId);
}

public interface SummonRepository {
    SummonPity lockPity(long playerId, String pityGroupKey);
    void savePity(SummonPity pity);
    SummonTransaction insertTransaction(SummonTransaction transaction);
    void insertResults(long transactionId, List<SummonResult> results);
}

public interface PlayerStateRepository {
    PlayerStateProjection loadPlayerState(long playerId);
    SaveCheckpoint saveCheckpoint(SaveCheckpoint checkpoint);
}

public interface PaymentRepository {
    PaymentOrder createOrder(CreatePaymentOrder command);
    Optional<StoreReceipt> findByStoreTransaction(String store, String transactionId);
    StoreReceipt insertVerifiedReceipt(VerifiedReceipt receipt);
    void appendPaymentEvent(PaymentEvent event);
}

public interface IdempotencyRepository {
    IdempotencyDecision begin(String scope, Long playerId, UUID key, String requestHash);
    void succeed(long id, int responseCode, Object responseBody);
    void fail(long id, int responseCode, Object responseBody);
}

public interface OutboxRepository {
    void append(OutboxEvent event);
    List<OutboxEvent> lockPendingBatch(int batchSize);
    void markPublished(long outboxEventId);
    void markFailed(long outboxEventId, String error, Instant nextAttemptAt);
}
```

### 10.3 MasterDataProvider

게임 로직은 master JPA entity를 직접 참조하지 않고 다음 읽기 인터페이스를 사용한다.

```java
public interface MasterDataProvider {
    long activeReleaseId();
    RuntimeConfigValue getConfig(String configKey);
    JobDefinition getJob(String jobKey);
    MercenaryDefinition getMercenary(String mercenaryKey);
    SkillDefinition getSkill(String skillKey, int level);
    ItemDefinition getItem(String itemKey);
    EquipmentDefinition getEquipment(String equipmentKey);
    StageDefinition getStage(String stageKey);
    RaidDefinition getRaid(String raidKey, String difficultyCode);
    FacilityLevelDefinition getFacilityLevel(String facilityKey, int level);
    RewardDefinition getRewardGroup(String rewardGroupKey);
    SummonBannerDefinition getActiveBanner(String bannerKey, Instant now);
    ShopOfferDefinition getActiveOffer(String offerKey, Instant now);
}
```

이를 통해 DB, 메모리 캐시, 향후 Redis 캐시를 교체 가능하게 한다.

---

## 11. 트랜잭션 경계

### 11.1 기본 격리 수준

- 기본: `READ COMMITTED`
- wallet, pity, purchase limit: `SELECT ... FOR UPDATE`
- 콘텐츠 publish: transaction-level advisory lock 또는 channel 행 `FOR UPDATE`
- 장시간 외부 API 호출 중 DB transaction을 열어두지 않는다.
- 외부 영수증 검증 호출과 DB 확정 단계를 분리하고 상태 머신으로 연결한다.

### 11.2 재화 변경

한 트랜잭션:

1. idempotency 획득
2. wallet 행을 currency_key 오름차순으로 잠금
3. 잔액 검증
4. wallet_transaction INSERT
5. wallet balance/version UPDATE
6. outbox INSERT
7. idempotency 성공 기록
8. commit

잔액이 부족하면 어떤 행도 변경하지 않는다.

### 11.3 모집

한 트랜잭션:

1. idempotency 획득
2. 활성 banner와 content release 확인
3. 비용 wallet 잠금 및 차감
4. pity 행 잠금
5. 서버 RNG로 결과 결정
6. pity 갱신
7. mercenary/item/equipment 생성
8. summon transaction/result 기록
9. reward/wallet/item 원장 기록
10. outbox 기록
11. commit

RNG seed 또는 검증용 random trace hash를 `summon_transaction`에 저장하되 클라이언트에 seed를 사전 노출하지 않는다.

### 11.4 보상 지급

1. `(player_id, request_id)` 중복 확인
2. 활성/지정 release의 reward group 조회
3. 확률 결과 서버 결정
4. currency/item/entity별 잠금 순서 준수
5. reward_grant와 line 기록
6. 각 자산 및 원장 갱신
7. grant 완료
8. commit

### 11.5 콘텐츠 배포

1. PRODUCTION channel 행 잠금
2. expected version 확인
3. release 상태 APPROVED/SCHEDULED 확인
4. validation ERROR 없음 확인
5. minimum client version 확인
6. previous/active release pointer 변경
7. release 상태 및 publish history 기록
8. audit/outbox 기록
9. commit
10. 서버가 새 snapshot을 로딩·검증한 뒤 캐시를 원자 교체

### 11.6 Deadlock 대응

모든 자산 잠금 순서:

```text
player_account
-> wallet(currency_key ASC)
-> summon_pity(pity_group_key ASC)
-> item_stack(item_id ASC)
-> equipment(equipment_id ASC)
-> purchase_limit(limit_key ASC)
```

deadlock/serialization failure는 최대 3회 제한 재시도한다. 사용자 입력을 기다리며 transaction을 유지하지 않는다.

---

## 12. 콘텐츠 캐시 인터페이스

### 12.1 요구사항

- 게임 요청마다 master 전체를 DB에서 조회하지 않는다.
- 서버 시작 시 active release의 immutable snapshot을 생성한다.
- 캐시 key는 `(channel_code, release_id)`다.
- snapshot 로딩 실패 시 기존 active snapshot을 유지한다.
- 새 snapshot 검증 성공 후 reference를 원자 교체한다.
- 기본 구현은 in-memory, Redis는 선택 어댑터다.

### 12.2 갱신 방식

초기 구현:

- `master.content_channel.version`을 5~10초 주기로 polling
- version 변경 시 새 release snapshot 로딩

선택 구현:

- outbox publisher가 내부 메시지 또는 PostgreSQL NOTIFY 발행
- polling은 유실 대비 안전망으로 유지

### 12.3 서버 시작 실패 정책

- PRODUCTION active release가 없으면 서버 readiness 실패
- 참조 무결성/체크섬 오류면 readiness 실패
- 캐시 로딩 전 게임 요청을 받지 않는다.

---

## 13. 관리자 API 인터페이스

관리자 API는 별도 권한과 감사 기록을 사용한다.

| Method | URI | 역할 |
|---|---|---|
| POST | `/admin/content/releases` | base release로 draft 생성 |
| GET | `/admin/content/releases/{id}` | release 상세 |
| POST | `/admin/content/releases/{id}/validate` | 전체 정합성 검증 |
| POST | `/admin/content/releases/{id}/approve` | 승인 |
| POST | `/admin/content/releases/{id}/publish` | 즉시/예약 배포 |
| POST | `/admin/content/channels/{channel}/rollback` | 이전/지정 버전 롤백 |
| PUT | `/admin/content/releases/{id}/jobs/{key}` | draft 직업 밸런스 수정 |
| PUT | `/admin/content/releases/{id}/facilities/{key}/levels/{level}` | 시설 레벨 수정 |
| PUT | `/admin/content/releases/{id}/stages/{key}` | 스테이지 수정 |
| PUT | `/admin/content/releases/{id}/drop-tables/{key}` | 드롭 테이블 수정 |
| PUT | `/admin/content/releases/{id}/configs/{key}` | runtime config 수정 |
| POST | `/admin/players/{publicId}/rewards` | 원장 기반 관리자 보상 지급 |

필수 헤더:

```text
Authorization
X-Request-Id
X-Idempotency-Key
If-Match 또는 expectedVersion
```

관리자 변경 요청에는 `reason`을 필수로 받고 `audit.admin_action_log`에 before/after를 기록한다.

---

## 14. 인덱스와 성능

### 14.1 필수 인덱스

- 모든 FK 컬럼 인덱스
- player 소유 테이블: `(player_id, status)` 또는 주요 조회 조합
- `wallet_transaction(player_id, created_at DESC)`
- `item_transaction(player_id, created_at DESC)`
- `battle_record(player_id, created_at DESC)`
- `payment_order(player_id, created_at DESC)`
- `store_receipt(store, transaction_id)` UNIQUE
- `outbox_event(published_at, next_attempt_at)` WHERE `published_at IS NULL`
- `content_release(status, scheduled_at)`
- 기간형 운영 테이블 `(enabled, starts_at, ends_at)`
- `save_checkpoint(player_id, created_at DESC)`

### 14.2 파티셔닝

초기 1.0에서는 무조건 파티셔닝하지 않는다. 아래 테이블이 실제 크기 기준을 초과할 때 월 단위 range partition을 적용한다.

- wallet_transaction
- item_transaction
- battle_record
- payment_event
- admin_action_log
- security_event
- outbox_event

전환 기준은 운영 데이터 측정 후 결정하며, 최초 설계에서 created_at과 PK를 반드시 둔다.

### 14.3 페이지네이션

- 운영/원장 목록은 offset이 아니라 keyset pagination을 사용한다.
- 예: `(created_at, wallet_transaction_id) < (?, ?)`.

---

## 15. Flyway 마이그레이션 계획

```text
V001__create_roles_and_schemas.sql
V002__create_audit_base.sql
V003__create_master_release_tables.sql
V004__create_master_character_tables.sql
V005__create_master_item_tables.sql
V006__create_master_world_tables.sql
V007__create_master_facility_reward_tables.sql
V008__create_game_account_tables.sql
V009__create_game_mercenary_inventory_tables.sql
V010__create_game_town_progress_tables.sql
V011__create_game_wallet_reward_tables.sql
V012__create_ops_summon_shop_event_tables.sql
V013__create_game_summon_mission_mail_tables.sql
V014__create_billing_tables.sql
V015__create_indexes_and_constraints.sql
V016__seed_content_channel_and_config_definitions.sql
V017__seed_initial_content_release.sql
R__views_for_operations.sql
R__master_validation_functions.sql
```

규칙:

- versioned migration은 영구 환경 적용 후 수정하지 않는다.
- 변경은 새 migration으로 roll-forward 한다.
- repeatable migration은 view/function처럼 `CREATE OR REPLACE` 가능한 객체에만 사용한다.
- 운영 데이터 release는 Flyway migration이 아니라 관리자 배포 기능으로 관리한다.
- 초기 seed release만 Flyway가 생성한다.
- migration 파일명 검증을 활성화한다.

---

## 16. 초기 데이터와 bootstrap

초기 migration은 다음만 생성한다.

- DEV/STAGING/PRODUCTION content channel
- runtime config definition
- 기본 통화 key
- 시스템 관리자 역할 seed 또는 bootstrap 방법
- 최초 `DRAFT` content release

실제 5직업·5지역·2레이드·8시설 데이터는 별도 seed import가 가능해야 한다.

권장 import 인터페이스:

```bash
./gradlew bootRun --args='--spring.profiles.active=local'

./gradlew masterDataImport \
  --release-version=1.0.0-content.1 \
  --source=./content/initial

./gradlew masterDataValidate \
  --release-version=1.0.0-content.1
```

실제 Gradle task 이름은 저장소 구조에 맞출 수 있지만 동일 기능과 실행 명령을 문서에 남긴다.

---

## 17. 테스트 요구사항

### 17.1 필수 통합 테스트

1. 새 계정 생성 시 기본 profile/town/wallet이 원자 생성된다.
2. 같은 idempotency key의 재화 요청은 한 번만 적용된다.
3. 동시에 재화를 소비해도 잔액이 음수가 되지 않는다.
4. wallet balance와 ledger의 마지막 balance_after가 일치한다.
5. 같은 보상 request_id가 두 번 지급되지 않는다.
6. 모집 비용 차감, pity, 결과, 용병 생성이 한 transaction이다.
7. 모집 transaction 중 실패하면 비용과 pity가 모두 rollback된다.
8. roster slot은 1~16만 허용하고 같은 용병을 중복 배치할 수 없다.
9. 동일 스토어 transaction ID를 두 계정에서 사용할 수 없다.
10. PUBLISHED release의 수정 API가 거부된다.
11. validation ERROR가 있는 release는 publish할 수 없다.
12. channel expected version 불일치 시 publish가 거부된다.
13. publish 후 서버가 새 release snapshot을 읽는다.
14. 잘못된 새 snapshot이면 기존 캐시가 유지된다.
15. rollback 시 previous release가 다시 활성화되고 이력이 남는다.
16. 관리자 변경은 before/after/reason과 함께 감사 로그에 남는다.
17. outbox publisher 다중 인스턴스가 같은 이벤트를 동시에 처리하지 않는다.
18. save checkpoint에 save/game/content version과 checksum이 저장된다.

### 17.2 테스트 환경

- Testcontainers PostgreSQL 18.4
- 실제 Flyway migration 적용 후 테스트
- H2로 PostgreSQL 동작을 대체하지 않는다.
- 동시성 테스트는 최소 20개 병렬 요청으로 수행한다.

---

## 18. 운영 검증 쿼리/모니터링 항목

운영 대시보드 또는 health check에서 확인한다.

- 현재 PRODUCTION active release/version
- 마지막 콘텐츠 배포/롤백 시각
- content validation ERROR 수
- wallet 음수 잔액 수: 항상 0
- 처리 중으로 오래 남은 idempotency 수
- PROCESSING 상태로 오래 남은 reward/payment 수
- 미발행 outbox 수와 최고 지연 시간
- 영수증 중복 탐지 수
- migration 상태와 checksum 오류
- DB connection pool active/pending
- lock wait/deadlock 횟수
- 주요 원장 테이블 증가량

---

## 19. 보안 및 개인정보

- provider subject는 원문 대신 provider 정책에 맞는 암호화 또는 hash를 사용한다.
- refresh token은 hash만 저장한다.
- 영수증·purchase token을 애플리케이션 로그에 출력하지 않는다.
- 운영 조회에서 계정 식별정보를 최소화한다.
- 관리자 작업은 actor, reason, correlation/request ID를 필수 기록한다.
- SQL 로그에서 bind parameter의 민감정보 마스킹을 적용한다.
- 탈퇴 시 법적 보존 대상이 아닌 프로필 식별정보를 익명화한다.
- DB 백업 암호화와 복구 훈련은 운영 플랫폼 확정 단계에서 추가한다.

---

## 20. 완료 조건

Codex 작업은 다음 조건을 모두 만족해야 완료다.

- PostgreSQL 18.4 로컬 환경이 한 명령으로 실행된다.
- 모든 Flyway migration이 빈 DB에서 성공한다.
- migration 재실행 시 변경 없이 성공한다.
- 5개 스키마와 권한이 설계대로 생성된다.
- 핵심 Repository/Service 인터페이스가 구현된다.
- wallet/reward/summon/content publish 트랜잭션 테스트가 통과한다.
- DB 제약조건 테스트가 통과한다.
- 초기 content release import/validate/publish가 가능하다.
- 운영 active release를 변경하고 rollback할 수 있다.
- 실행·테스트 명령을 README 또는 별도 결과 문서에 남긴다.
- 구현 결과 ERD와 실제 테이블 목록이 본 문서와 대응한다.

---

## 21. Codex 최종 보고 형식

```text
1. 분석한 기존 구조
2. 생성/수정한 파일
3. 생성한 DB 스키마와 테이블
4. 구현한 Repository/Service
5. Flyway migration 적용 결과
6. 테스트 실행 명령과 결과
7. 설계서와 달라진 부분 및 사유
8. 미구현/OPS_LATER 항목
9. 다음 작업 권장 순서
```

---

## 22. 구현 우선순위

### P0: DB 기반과 핵심 무결성

- 역할/스키마/Flyway
- content release/channel/validation
- 계정/프로필/세이브 버전
- wallet/ledger/reward grant
- mercenary/item/equipment
- summon/pity
- idempotency/outbox/audit

### P1: 게임 진행 데이터

- town/facility
- region/stage/raid
- offline reward
- mission/achievement/mail
- master 데이터 전체 import 및 검증

### P2: 실제 운영 연동 준비

- 관리자 API와 간단한 운영 화면
- shop/payment persistence
- 실제 스토어 검증 provider port
- cache adapter/Redis 선택 구현
- outbox 외부 분석 전달 adapter

### OPS_LATER

- 실제 클라우드 PostgreSQL 상품/리전
- 백업 주기와 PITR 수치
- 로그인 사업자
- Google/Apple 실제 결제 검증
- 분석/크래시 도구
- 관리자 SSO
- 개인정보 보존 기간

OPS_LATER는 미설계가 아니라 교체 가능한 인터페이스와 설정만 선행하는 항목이다.

---

## 참고 기준

- PostgreSQL 18.4와 18 계열 최신 minor 유지
- PostgreSQL 18 `uuidv7()` 사용
- PostgreSQL row-level `FOR UPDATE` 잠금 사용
- Flyway versioned migration은 순서대로 한 번만 적용하고 적용 후 수정하지 않음
- Flyway repeatable migration은 view/function 등 재생성 가능한 객체에 한정
