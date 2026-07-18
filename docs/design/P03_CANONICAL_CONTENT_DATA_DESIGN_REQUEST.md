# P03 canonical gameplay data 상세 설계 요청서

> 이 문서를 ChatGPT에 그대로 전달한다. 답변은 설명 대화가 아니라
> `TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md` 한 파일의 완성된 본문이어야 한다.

## 0. ChatGPT 수행 지시

Kingdom Tycoon 1.0의 P03 콘텐츠 파이프라인·Save 구현을 끝낼 수 있도록, 아래 8개
`UNRESOLVED` 데이터 묶음의 canonical row를 값까지 확정하라.

- 이미 승인된 Save/CSV schema를 다시 설계하지 않는다.
- 명세에 없는 P04 이후 게임 기능을 추가하지 않는다.
- 모든 `TUNABLE` 수치에도 구현 가능한 초기값을 지정한다.
- 예시, 생략 부호, `TBD`, `TODO`, "추후 결정"을 최종 CSV row에 사용하지 않는다.
- 현재 schema로 확정 요구를 표현할 수 없다면 열을 조용히 추가하지 말고, 먼저
  `Schema correction required`에 충돌, 최소 정정안, 기존 데이터 영향과 권장안을 적는다.
- 근거가 부족한 항목은 임의 확정하지 말고 `UNRESOLVED`에 결정 질문, 선택지, 권장안을
  적는다. 단, `UNRESOLVED`가 하나라도 남으면 P03 구현은 계속 차단된다는 점을 명시한다.
- 최종 목표는 `UNRESOLVED 0건`이며, Codex가 답변 문서의 CSV block을 그대로 구현 입력으로
  옮길 수 있어야 한다.

## 1. 현재 구현 상태와 차단점

Issue #14의 안전한 범위는 이미 구현되고 검증됐다.

- strict Save v1 schema, JCS/SHA-256, 원자 저장, 3단 backup, 복구, split-brain 차단
- 연속 Save Migration registry와 future-version 차단
- RFC 4180 canonical CSV importer
- manifest SHA-256/row count/header 검증
- field domain, PK/FK, `status`/`enabled`, tagged-union 검증
- immutable runtime catalog와 lazy package load
- Unity EditMode `15 passed`, PlayMode `1 passed`
- PostgreSQL Testcontainers 포함 repository CI 통과

현재 `data/csv`는 legacy migration input일 뿐 runtime source가 아니다. 아래 8개 묶음의 실제
row 값이 없으므로 `StreamingAssets/Content` canonical package, 단방향 migrator와 최종 player
build를 만들지 않은 상태다. 이 설계가 확정되기 전에는 P04로 이동할 수 없다.

## 2. 권위 문서와 우선순위

충돌 시 아래에서 위에 있는 문서보다 아래에 있는 최신 정정 문서를 우선한다.

1. 저장소 `AGENTS.md`, `docs/00_MASTER_INDEX.md`
2. 저장소 `docs/06_MERCENARY_SYSTEM.md`
3. 저장소 `docs/09_RECRUITMENT_GACHA.md`
4. 저장소 `docs/12_ITEMS_MATERIALS_EQUIPMENT.md`
5. 저장소 `docs/17_TUTORIAL_ONBOARDING.md`
6. 저장소 `docs/18_SAVE_OFFLINE_PROGRESS.md`
7. 저장소 `docs/design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md`
8. `TYCOON_SAVE_CSV_CONTRACT_DESIGN_v1.1.md`
9. `TYCOON_SAVE_CSV_CONTRACT_DESIGN_v1.2_APPENDIX.md`
10. `TYCOON_SAVE_CSV_CONTRACT_DESIGN_v1.2.1_CORRECTION_APPENDIX.md`
11. `TYCOON_SAVE_CSV_CONTRACT_DESIGN_v1.2.2_CORRECTION_APPENDIX.md`

기준 구현 보고서는 `docs/reports/P03_CONTENT_SAVE_REPORT.md`다. 최신 정정 부록이 이전 문구를
교체한 경우 이전 문구를 병합하거나 되살리지 않는다.

## 3. 반환 문서 형식

반환 파일은 다음 순서를 따른다.

1. 결정 요약
2. Schema correction required 또는 `NONE`
3. 8개 묶음별 결정 근거
4. 파일별 완전한 fenced `csv` block
5. legacy ID → canonical ID 변환표
6. tagged-union 및 FK 검증표
7. deterministic generation 규칙
8. content release/manifest 결정
9. Codex 구현 수용 기준 자체 점검표
10. 남은 `UNRESOLVED` 또는 `NONE`

CSV block 규칙:

- 각 파일은 header와 전체 row를 포함한다.
- row가 0개인 파일도 header를 명시하고, 왜 0개가 유효한지 근거를 적는다.
- Markdown 표만 제공하지 말고 반드시 기계적으로 복사 가능한 fenced `csv` block을 제공한다.
- 기존 canonical schema의 열 순서를 사용한다.
- 모든 canonical CSV는 `status`, `enabled`를 마지막 두 필수 열로 가진다.
- nullable 값은 빈 셀로 표현한다. `NONE`, `NULL`, `N/A`를 null sentinel로 쓰지 않는다.
- bool은 대문자 `TRUE` 또는 `FALSE`만 사용한다.
- ID는 영문 대문자 snake case `StableId`를 사용한다.
- 한 셀에 JSON이나 pipe-list를 넣지 않는다. 다대다는 관계 CSV row로 분리한다.
- `status`는 `CONFIRMED|TUNABLE|DEFERRED|OPS_LATER|REFERENCE_ONLY|REJECTED|UNRESOLVED|TEMPLATE`
  중 하나다.
- `DEFERRED|OPS_LATER|REFERENCE_ONLY|REJECTED|UNRESOLVED|TEMPLATE` row는
  `enabled=FALSE`여야 한다.
- enabled row가 disabled hard-FK target을 참조하면 안 된다.
- 순서를 갖는 `entry_no`, `line_no`, `order`는 enabled row 기준 1부터 연속이어야 한다.
- UTC 값은 millisecond 단위 `yyyy-MM-ddTHH:mm:ss.fffZ`다.
- 같은 의미의 요청 enum, 로컬 content ID, 서버 key를 문자열이 비슷하다는 이유로 혼용하지 않는다.

## 4. 이미 잠긴 결정

### 4.1 모집 범위

- `TAVERN`: C/B/A만, 왕국 골드, P03 local/Mock, 1회 모집만 허용
- `SPECIAL`, `SPECIAL_RATEUP`: A/S/SS만, 서버 권위, 1회 또는 10회
- 생성형 용병 결과만 1.0에서 활성화한다.
- `GENERATED_MERCENARY`는 모집 result discriminator이며 일반 `reward_entries`에는 금지한다.
- 10회 비용은 별도 할인 열이 생기기 전까지 `cost_amount × 10`이다.
- 특별 모집은 free/paid premium을 섞거나 부족분을 다른 bucket에서 보충하지 않는다.

### 4.2 재화 namespace

| 의미 | 요청 enum 및 `cost_type` | 로컬 content currency ID | type/authority | 서버 key |
|---|---|---|---|---|
| 왕국 골드 | `KINGDOM_GOLD` | `KINGDOM_GOLD` | `KINGDOM/LOCAL` | 없음 |
| 무료 premium | `FREE_PREMIUM` | `PREMIUM_FREE` | `PREMIUM_FREE/SERVER` | `FREE_GEM` |
| 유료 premium | `PAID_PREMIUM` | `PREMIUM_PAID` | `PREMIUM_PAID/SERVER` | `PAID_GEM` |
| 특별 모집권 | `TICKET` | `SPECIAL_RECRUIT_TICKET` | `TICKET/SERVER` | `SPECIAL_TICKET` |

`SPECIAL_TICKET`은 서버 key로만 유지한다. 로컬 ID나 request enum으로 사용하지 않는다.

### 4.3 현재 legacy 모집 값

아래 확률 weight는 초기 `TUNABLE` 값으로 보존해야 한다.

| legacy pool | pool type | grade/weight |
|---|---|---|
| `TAVERN_L1` | `TAVERN` | C=75, B=23, A=2 |
| `TAVERN_L4` | `TAVERN` | C=30, B=48, A=22 |
| `SPECIAL_STANDARD` | `SPECIAL` | A=82, S=16, SS=2 |

현재 legacy pity row는 다음과 같다.

| pity ID | category | trigger | guaranteed | reset | carry over |
|---|---|---:|---|---|---:|
| `PITY_S_PLUS` | `SPECIAL` | 10 | `GRADE_S` | `GRADE_S` | 1 |
| `PITY_SS` | `SPECIAL` | 80 | `GRADE_SS` | `GRADE_SS` | 1 |
| `PITY_RATEUP_JOB` | `SPECIAL_RATEUP` | 2 | 빈 값 | 빈 값 | 0 |

### 4.4 현재 legacy random equipment 값

`loot_entries.csv`의 `RANDOM_EQUIPMENT_TIER` reward ID는 숫자 `1..5`다. canonical에서는 숫자
ID가 금지되며 tier별 spec FK로 치환해야 한다. 기존 계약의 예시는
`RANDOM_EQ_T1_ANY` 형식이지만, 최종 exact ID와 quality profile은 이번 답변에서 확정한다.

### 4.5 튜토리얼과 오프라인 기준

튜토리얼의 확정 순서는 다음 10단계다.

1. 폐허 전초기지 확인
2. 주점에서 첫 C/B 용병 모집
3. 외곽 초원 출입 허가
4. 자동 사냥 관찰
5. 귀환·판매
6. 대장간 개방 및 기본 무기 제작
7. 용병이 장비 구매·착용
8. 연금술 공방 개방, 하급 포션 생산
9. 수습 최대 레벨·정식 승급 체험
10. 어둠숲 개방

실패 방지는 첫 용병의 성장 가능한 직업 조합, 하급 포션 재료, 첫 강화 확정, 첫 승급 재료,
첫 부상 즉시 치료를 포함한다. `OFFLINE_MAX_HOURS=8`, `OFFLINE_HUNT_EFFICIENCY=0.75`는 기존
초기 `TUNABLE` 값이다.

## 5. 요청 1 — recruitment pool header

다음 schema의 전체 row를 확정한다.

```text
recruitment_pools.csv
PK: pool_id
fields: pool_id,pool_type,pity_group_id,rate_up_group_id,cost_type,cost_amount,starts_at_utc,ends_at_utc,status,enabled
```

반드시 결정할 내용:

- `TAVERN_L1`, `TAVERN_L4`의 1회 `cost_amount`
- 각 pool의 exact pity/rate-up group FK와 nullable 기간
- standard pool의 상시 기간 표현
- `SPECIAL`이 세 결제수단을 지원할 때 한 row에 cost type 하나만 허용하는 계약 처리
- 결제수단별 pool ID를 분리한다면 exact ID, 기존 `SPECIAL_STANDARD` alias, 각각의 `cost_amount`
- 1.0에 활성 `SPECIAL_RATEUP` banner가 있는지 여부
- rate-up이 없다면 관련 row를 아예 만들지 않는지, disabled template로 두는지와 근거

같은 논리 pool을 결제수단별 여러 pool ID로 나눌 경우 각 ID에 연결될 entry/pity/rate-up row까지
완결해야 한다. `SPECIAL` 한 row에 여러 cost type을 pipe-list로 넣어서는 안 된다.

## 6. 요청 2 — recruitment pool entry

다음 schema의 전체 row를 확정한다.

```text
recruitment_pool_entries.csv
PK: pool_id,entry_no
fields: pool_id,entry_no,result_type,result_id,grade_id,job_selection_type,job_selection_id,weight,status,enabled
```

반드시 결정할 내용:

- 모든 활성 row의 `result_type=GENERATED_MERCENARY`
- `result_id`로 사용할 exact generation profile FK
- 각 grade row의 `job_selection_type`과 nullable `job_selection_id`
- 위 9개 legacy grade weight의 보존
- 결제수단별 pool 분리 시 entry 복제 정책과 exact row
- `TAVERN` 첫 모집 C/B 보장처럼 일반 weight와 다른 튜토리얼 제약의 표현 위치

`GENERATED_MERCENARY` row는 `grade_id`가 필수다. `ALL`이면 `job_selection_id`는 빈 셀,
`FIXED`이면 jobs FK, `RATE_UP_GROUP`이면 rate-up group FK다.

## 7. 요청 3 — pity와 rate-up

아래 네 파일의 전체 row를 확정한다.

```text
recruitment_pity_groups.csv
PK: pity_group_id
fields: pity_group_id,category,carry_over,status,enabled

recruitment_pity_rules.csv
PK: pity_group_id,pity_rule_id
fields: pity_group_id,pity_rule_id,trigger_count,guaranteed_grade_id,reset_on_grade_or_higher_id,guarantee_type,status,enabled

recruitment_rate_up_groups.csv
PK: rate_up_group_id
fields: rate_up_group_id,featured_share,failure_guarantee_mode,status,enabled

recruitment_rate_up_entries.csv
PK: rate_up_group_id,job_id
fields: rate_up_group_id,job_id,weight,status,enabled
```

반드시 결정할 내용:

- `SPECIAL`과 `SPECIAL_RATEUP`의 exact pity group ID와 이월 경계
- `PITY_S_PLUS`, `PITY_SS`의 canonical rule ID, group, reset 의미
- 두 rule이 같은 pull에서 동시에 충족될 때 적용 우선순위와 counter reset 결과
- `PITY_RATEUP_JOB trigger_count=2`의 정확한 의미
- `featured_share`, 대상 직업 목록/weight와 실패 보장 상태 전이
- 활성 rate-up banner가 없다면 rate-up row의 존재/비활성 정책
- Save key `(player,pity_group,pity_rule)` 및 featured guarantee FK와의 일치

rate-up 대상 직업을 정할 근거가 없다면 임의 직업을 고르지 말고 권장안이 포함된 명시적 결정
항목으로 다룬다.

## 8. 요청 4 — 생성형 용병

아래 파일의 전체 row와 `GENERATED_MERCENARY_V1` 알고리즘을 확정한다.

```text
mercenary_generation_profiles.csv
PK: generation_profile_id
fields: generation_profile_id,algorithm_id,initial_rank_id,name_pool_id,appearance_pool_id,personality_policy,fixed_personality_id,growth_seed_min,growth_seed_max,status,enabled

mercenary_name_pool_entries.csv
PK: name_pool_id,locale,entry_no
fields: name_pool_id,locale,entry_no,display_name,weight,status,enabled

mercenary_appearance_pool_entries.csv
PK: appearance_pool_id,job_id,entry_no
fields: appearance_pool_id,job_id,entry_no,appearance_asset_id,weight,status,enabled
```

반드시 결정할 내용:

- TAVERN/SPECIAL/SPECIAL_RATEUP이 공유하거나 분리할 generation profile ID
- initial rank, name pool, appearance pool, personality policy, seed 범위
- `ko-KR` 출시 이름 pool의 실제 전체 이름과 weight
- 5개 직업별 실제 appearance entry ID, asset registry ID와 weight
- 실제 에셋이 아직 없을 때 P03 build에서 사용할 production-valid registry/placeholder 정책
- operation seed에서 grade, job, name, appearance, growth, personality, trait를 뽑는 고정 순서
- 각 draw의 RNG algorithm/version, 범위 경계, rejection/weighted selection 규칙
- 같은 seed와 contentVersion이 같은 결과를 내도록 하는 stat/growth seed 적용식
- 이름·외형 중복 허용 여부와 별도 Instance UUIDv7 생성 규칙
- personality와 trait 선택의 모집 pool/grade/job 제약 및 균등/가중 정책

schema 밖의 stat rule table이 꼭 필요하다면 `Schema correction required`에서 최소 변경으로
제안한다. 알고리즘을 자연어 "랜덤 선택"으로 끝내지 말고 pseudocode와 seed 소비 순서를 제공한다.

## 9. 요청 5 — random equipment tier spec

다음 파일의 전체 row를 확정한다.

```text
random_equipment_tier_specs.csv
PK: spec_id
fields: spec_id,tier,slot_policy,fixed_slot,job_policy,fixed_job_id,quality_profile_id,status,enabled

equipment_quality_weights.csv
PK: quality_profile_id,quality_id
fields: quality_profile_id,quality_id,weight,status,enabled
```

반드시 결정할 내용:

- legacy reward ID `1..5` 각각의 exact canonical spec ID
- tier 1..5별 slot policy와 4개 slot 허용 방식
- 직업 eligibility, killer job context가 없을 때의 처리
- tier별 quality profile ID와 5개 quality의 정수 weight
- 0 weight row를 보존할지 생략할지
- eligible template가 0개일 때 fail-closed 오류
- template 선택 → quality 선택 → equipment Instance 생성의 deterministic 순서
- 생성 snapshot의 template, quality, source contentVersion, operationId 기록

숫자 `1..5`를 canonical `reward_id`로 남기지 않는다. 일반 drop spec에 boss 전용 template가 포함되는지
여부도 명시한다.

## 10. 요청 6 — tutorial row와 grant

아래 파일에 필요한 전체 row를 확정한다.

```text
tutorial_steps.csv
PK: tutorial_step_id
fields: tutorial_step_id,order,action_type,target_id,prerequisite_step_id,skippable,status,enabled

tutorial_grants.csv
PK: grant_id,line_no
fields: grant_id,tutorial_step_id,line_no,reward_type,reward_id,quantity,status,enabled

reward_groups.csv
PK: reward_group_id
fields: reward_group_id,distribution_mode,draw_count,status,enabled

reward_entries.csv
PK: reward_group_id,entry_no
fields: reward_group_id,entry_no,reward_type,reward_id,quantity,probability,weight,status,enabled
```

반드시 결정할 내용:

- 10개 step의 exact ID, action type, target ID, 선행 step, skip 가능 여부
- 하급 포션 재료, 첫 강화, 첫 승급, 첫 부상 치료의 exact reward ID와 quantity
- `grant_id`를 기반으로 한 profile당 1회 idempotency key 구성
- skip 시 이미 지급한 grant, 아직 지급하지 않은 grant, progression flag 처리
- 첫 C/B 용병과 성장 가능한 직업 조합을 보장하는 방식
- 첫 강화 확정이 reward인지 일회성 progression rule인지와 데이터 표현 위치
- 튜토리얼 grant와 일반 reward group을 공유하는지 여부

일반 `reward_entries`에는 `GENERATED_MERCENARY`를 넣지 않는다. 첫 용병 보장이 필요하면 모집
pool/entry 또는 명시적인 tutorial recruitment policy로 표현하고 schema 충돌 여부를 보고한다.

현재 데이터의 후보 ID에는 `MAT_R01_WILD_HERB`, `MAT_R01_SLIME_GEL`, `MAT_ENHANCE_1`,
`MAT_PROMO_BRONZE_EMBLEM`, `POT_HEAL_SMALL` 등이 있지만, 이름만 보고 임의 선택하지 말고 정확한
게임 흐름과 recipe/승급 비용을 대조해 결정한다.

## 11. 요청 7 — offline rule

다음 schema의 6개 settlement type 전체 row를 확정한다.

```text
offline_reward_rules.csv
PK: rule_id
fields: rule_id,settlement_type,max_seconds,efficiency,reward_group_id,status,enabled
```

필수 settlement type:

- `HUNT`
- `FACILITY`
- `NPC_PROFICIENCY`
- `POTION_CONSUMPTION`
- `INJURY_RECOVERY`
- `PROMOTION_REVIEW`

반드시 결정할 내용:

- 각 type의 exact `rule_id`, `max_seconds`, `efficiency`
- `max_seconds=28800`과 hunt `efficiency=0.75`의 적용 범위
- reward group이 필요한 type의 exact FK와 전체 reward row
- reward가 snapshot/작업 결과에서 계산되어 group이 불필요한 type의 명시적 빈 FK 근거
- 부분 정산, rounding, 상한 적용 순서
- potion 부족, 시설 storage cap, 승급 완료, 부상 회복 완료 시 잔여 시간 처리
- raid, 특별 모집, 최초 발견, 최고 품질이 offline에서 실행되지 않는다는 보장
- 동일 `settlementId` 재시도 시 중복 지급을 막는 deterministic line identity

## 12. 요청 8 — content release와 runtime manifest

P03 runtime package는 `content_manifest.json`을 먼저 읽고 그 안의 table별 file name, SHA-256,
row count, primary key, field domain, FK를 검증한다. 다음 초기 release 값을 확정한다.

- exact `contentVersion` 문자열과 이후 증가 규칙
- `csv_schema_set_version=1`, 개별 file schema version의 초기값/증가 규칙
- package kind (`BASE|PATCH|TEMPLATE`)와 P03 초기 package 값
- minimum game/client version
- channel (`DEV|STAGING|PRODUCTION`)과 P03 build의 값
- `generatedAtUtc`를 재현 가능한 build metadata로 다루는 정책과 승인 fixture 값
- canonical package에 포함할 전체 CSV file 목록과 required/optional 여부
- manifest table 정렬 및 SHA-256 계산 순서
- legacy source를 runtime package에 포함하지 않는 규칙
- DB `master.content_release.release_version`과 client `contentVersion`의 정확한 매핑
- Save가 알 수 없는 future contentVersion을 만났을 때의 호환/차단 기준

현재 manifest JSON envelope의 고정값은 다음과 같다.

```text
schemaId=urn:tycoon:content-manifest:v1
contractVersion=1
```

이 JSON envelope를 승인 없이 다른 포맷으로 교체하지 않는다. canonical table로
`content_manifest.csv` 또는 `content_file_manifest.csv`도 유지해야 한다고 판단하면 JSON envelope와의
역할 분리, 단일 원본, 중복 검증 방식을 명시한다.

## 13. 필수 검증표

반환 문서에는 적어도 다음 검증의 기대 결과와 오류 코드를 표로 제공한다.

- 모든 모집 pool의 enabled entry weight 합이 0보다 큼
- TAVERN에 S/SS가 없고 SPECIAL 계열에 C/B가 없음
- pool type과 cost type/payment count 행렬 일치
- pity/rate-up FK 및 Save counter key 일치
- `GENERATED_MERCENARY` result tagged-union 유효/무효 사례
- 같은 seed/contentVersion의 생성형 용병 결과 재현
- random equipment spec의 template/quality 후보가 1개 이상
- tutorial step DAG 무순환, order 연속, grant idempotency
- offline 6 type 완전성, 최대 8시간 상한, 중복 정산 방지
- 모든 enabled row의 enabled hard-FK target 보장
- canonical cell의 pipe-list/embedded JSON 금지
- manifest hash/row count/header drift 실패

오류 코드가 기존 P03 validator 계약에 있으면 그대로 사용하고, 신규 코드가 꼭 필요하면 이름, 실패
조건, severity, 대상 path를 명시한다.

## 14. Codex 구현 수용 기준

반환 설계는 다음 작업을 추가 질문 없이 수행할 만큼 완전해야 한다.

1. legacy `data/csv` snapshot을 읽는 deterministic 단방향 migrator 작성
2. old/new 동시 runtime read 없이 `StreamingAssets/Content` canonical package 생성
3. manifest와 모든 CSV의 strict validation 성공
4. recruitment, generation, pity/rate-up, random equipment, tutorial, offline fixture 작성
5. 동일 seed/contentVersion golden 결과 고정
6. Save hard FK와 contentVersion compatibility test 통과
7. Unity EditMode/PlayMode 회귀 테스트 통과
8. Android 또는 현재 Phase에서 승인된 player build 성공
9. `docs/reports/P03_CONTENT_SAVE_REPORT.md`의 8개 `UNRESOLVED`를 모두 해소
10. P03 완료 판정 후에만 P04 진입

## 15. 반환 전 자체 점검

- [ ] 8개 요청 모두 exact row가 있다.
- [ ] 모든 CSV block에 header와 전체 row가 있다.
- [ ] 생략 부호, 예시값, TODO, TBD가 없다.
- [ ] 모든 `TUNABLE` 값에도 초기 수치가 있다.
- [ ] `status`와 `enabled`가 모든 row에 있다.
- [ ] nullable은 빈 셀이고 bool은 `TRUE|FALSE`다.
- [ ] 요청 enum, 로컬 currency ID, 서버 key가 분리됐다.
- [ ] 일반 reward에 `GENERATED_MERCENARY`가 없다.
- [ ] 숫자 random equipment reward ID가 canonical spec ID로 치환됐다.
- [ ] generation algorithm의 seed 소비 순서가 재현 가능하다.
- [ ] legacy → canonical 변환표가 완전하다.
- [ ] schema 충돌이 있으면 최소 정정안이 먼저 제시됐다.
- [ ] 남은 `UNRESOLVED`가 `NONE`이거나, P03 차단 사유가 명확하다.
