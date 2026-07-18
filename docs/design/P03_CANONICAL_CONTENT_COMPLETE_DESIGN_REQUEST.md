# P03 canonical content 최종 통합 설계 요청서

> 이 문서 **하나만** ChatGPT에 전달한다. ChatGPT가 직전 대화의
> `TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md`를 참고하더라도, 답변은 이전 파일을
> 다시 열지 않아도 구현할 수 있는 완전한 단일 문서여야 한다.
> 답변 파일명은 `TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md`다.

## 0. 목적

`TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md`의 8개 gameplay data 묶음은 대부분
구현 가능한 수준으로 확정됐다. 그러나 해당 문서가 요구하는 60개 required canonical CSV 전체를
현재 저장소에서 생성하려고 대조한 결과, 8개 잔여 계약 충돌·누락과 전체 package를 독립적으로
재현할 schema/row bundle 부재가 발견됐다.

이번 답변은 단순 정정 부록이 아니다. v1.0의 검증된 결정을 유지하면서 manifest, 60개 schema,
60개 전체 row, migration, validator, golden, placeholder asset과 Phase 경계를 **한 파일로 통합한 최종
구현 계약**이어야 한다. 이후 Codex가 추가 설계서를 다시 요청하지 않고 P03 구현·테스트·player
build를 완료할 수 있어야 한다.

## 1. 이미 검증되어 유지할 내용

다음 항목은 Codex가 독립 계산과 legacy source 대조로 검증했다. 정정 부록에서 값이나 ID를 바꾸지
않는다.

- v1.0의 fenced CSV block 18개는 모든 row의 column 수가 header와 일치한다.
- 문서의 row count 18개는 실제 fenced CSV row 수와 일치한다.
- 모집 pool/entry/pity row와 기존 9개 grade weight가 일치한다.
- 튜토리얼 recipe 재료, 시설 건설비, 강화석, 승급 token ID가 legacy source와 일치한다.
- random equipment spec 5개와 quality weight 합이 일치한다.
- SplitMix64 mercenary golden의 raw/bounded, 선택 결과, growth factor 6개가 모두 재현된다.
- random equipment golden의 template/quality/affix gate raw와 bounded 값이 모두 재현된다.
- rate-up 0-row 정책, offline 6-row 정책, tutorial 10-step chain은 내부적으로 일치한다.

유지 대상에는 다음 v1.0 ID가 포함된다.

```text
GEN_MERC_STANDARD_V1
NAME_POOL_KO_V1
APPEARANCE_POOL_V1
PITY_GROUP_SPECIAL_STANDARD
TAVERN_TUTORIAL_FIRST
SPECIAL_STANDARD_TICKET
SPECIAL_STANDARD_FREE_PREMIUM
SPECIAL_STANDARD_PAID_PREMIUM
RANDOM_EQ_T1_ANY..RANDOM_EQ_T5_ANY
```

## 2. 답변 공통 규칙

- 각 항목은 `Decision`, `Reason`, `Compatibility`, `Exact output` 순서로 작성한다.
- exact CSV가 필요한 항목은 header와 전체 row가 있는 fenced `csv` block을 제공한다.
- exact JSON이 필요한 항목은 unknown field를 허용하지 않는 완전한 fenced `json` fixture를 제공한다.
- 생략 부호, 예시값, `TODO`, `TBD`, `추후 결정`을 쓰지 않는다.
- 모든 `TUNABLE` 값에도 초기값을 지정한다.
- nullable은 빈 CSV cell 또는 JSON `null`, bool은 CSV에서 `TRUE|FALSE`를 사용한다.
- v1.0의 18개 확정 CSV block은 값 변경 없이 최종 통합 문서에 다시 포함한다. 이전 문서를
  참조하라는 표현으로 생략하지 않는다.
- 아래 60개 파일은 모두 header와 전체 row를 최종 문서에 포함한다. legacy에서 기계적으로
  변환 가능하다는 이유로 row를 생략하지 않는다.
- hash, row count처럼 exact CSV bytes에서 기계적으로 계산되는 값은 설계자가 임의 숫자를 만들지
  않는다. 대신 계산 알고리즘과 byte contract를 고정하고 Codex generator가 산출·검증하도록
  `BUILD_DERIVED`라고 문서 본문에서만 표시한다. 실제 CSV/manifest cell에는 이 문자열을 쓰지 않는다.
- 마지막 `UNRESOLVED`는 `NONE`이어야 한다. 남는다면 P03이 계속 차단됨을 명시한다.

## 3. 정정 1 — manifest contract version과 strict JSON fixture

### 3.1 발견된 충돌

현재 P03 `content_manifest.schema.json`과 importer의 contract version 1은 root에 다음 5개 property만
정확히 허용한다.

```text
schemaId, contractVersion, contentVersion, generatedAtUtc, tables
```

현재 table descriptor는 다음 6개 property만 정확히 허용한다.

```text
file, sha256, rowCount, primaryKey, fields, foreignKeys
```

반면 v1.0은 root에 `csv_schema_set_version`, `packageKind`, `baseContentVersion`,
`minimumGameVersion`, `channel`을 추가하고 table descriptor에 `schema version`, `required`를
추가하도록 요구한다. 동시에 “envelope field/의미 변경 시 contractVersion 증가”라고 규정하면서
`contractVersion=1`을 유지한다.

strict parser에서는 이 차이를 구현자가 추측할 수 없다. snake_case 설명과 camelCase JSON 이름도
혼재한다.

### 3.2 반드시 확정할 내용

- 아직 공개 release가 없으므로 기존 pre-release contract 1을 파기하고 새 완전 계약을 다시
  `contractVersion=1`로 고정할지, 정식 `contractVersion=2`로 올릴지
- 그 결정에 맞는 exact `schemaId`와 JSON Schema `$id`
- root property의 exact camelCase 이름과 required/null 규칙
- table descriptor의 exact `schemaVersion`, `required` property 이름과 타입
- BASE일 때 `baseContentVersion`을 property가 있는 JSON null로 둘지, property 자체를 금지할지
- 60개 table이 들어간 완전한 `content_manifest.json` fixture의 구조
- table이 file name UTF-8 ordinal 순서가 아닐 때 reject할지 importer가 정렬해 받을지
- manifest 자체의 RFC 8785 byte/hash identity를 어디에 저장·비교할지

### 3.3 필수 출력

1. 완전한 manifest root/table/field/FK JSON Schema
2. table 한 개가 아니라 root와 최소 2개 table descriptor가 포함된 exact valid JSON fixture
3. 기존 P03 contract 1 → 최종 계약의 compatibility/reset 결정표
4. unknown/missing/order/version invalid fixture 표

## 4. 정정 2 — manifest field domain vocabulary

### 4.1 발견된 충돌

현재 P03 importer의 manifest field domain은 다음 9개다.

```text
STRING, STABLE_ID, BOOL, INT32, SAFE_INT, DECIMAL, UTC_INSTANT, STATUS, ENUM
```

기준 문서는 이 외에 `uint64str`, `locale`, `date`, `semver`, `hex64`, `ratio`, `int64`를 사용한다.
v1.0의 `growth_seed_max=18446744073709551615`는 `SAFE_INT` 최대값을 넘는다. 60개 descriptor를
생성하려면 semantic type을 기존 domain으로 내릴지 새 domain을 추가할지 exact 결정이 필요하다.

### 4.2 반드시 확정할 내용

- `uint64str`, `locale`, `date`, `semver`, `hex64`, `ratio`, `int64` 각각의 manifest domain
- generic domain 검사 뒤 적용할 semantic/table-specific validator
- `growth_seed_min/max`가 `SEED64`인지 `STRING`+semantic validation인지
- `max_balance`, `row_count`, 일반 quantity를 `SAFE_INT`로 제한할지 별도 signed 64-bit로 둘지
- canonical decimal의 `1.0`을 허용할지 `1`로 정규화할지
- status enum 전체 8개 값과 `enabled` 조합 규칙

### 4.3 필수 출력

semantic type → manifest domain → lexical regex/range → 적용 field의 완전한 매핑표를 제공한다.

## 5. 정정 3 — `currencies.csv` exact row와 localization

### 5.1 발견된 누락

v1.0은 `currencies.csv`를 required file로 선언하고 tutorial grant가 `CURRENCY/KINGDOM_GOLD`를
hard FK로 참조한다. 그러나 네 currency의 `max_balance`, `name_text_key`, localization row가 없다.
namespace 매핑만으로는 canonical row를 생성할 수 없다.

### 5.2 잠긴 namespace

| 의미 | request enum | local content ID | type/authority | server key |
|---|---|---|---|---|
| 왕국 골드 | `KINGDOM_GOLD` | `KINGDOM_GOLD` | `KINGDOM/LOCAL` | 없음 |
| 무료 premium | `FREE_PREMIUM` | `PREMIUM_FREE` | `PREMIUM_FREE/SERVER` | `FREE_GEM` |
| 유료 premium | `PAID_PREMIUM` | `PREMIUM_PAID` | `PREMIUM_PAID/SERVER` | `PAID_GEM` |
| 특별 모집권 | `TICKET` | `SPECIAL_RECRUIT_TICKET` | `TICKET/SERVER` | `SPECIAL_TICKET` |

### 5.3 필수 출력

다음 header의 4개 exact row를 제공한다.

```csv
currency_id,currency_type,authority,max_balance,name_text_key,status,enabled
```

그리고 네 `name_text_key`의 `ko-KR` localization exact row를 제공한다. `max_balance`가 Save
SafeInt, server DB bigint 또는 별도 cap 중 무엇을 기준으로 하는지 명시한다.

## 6. 정정 4 — trait category enum

### 6.1 발견된 충돌

legacy `traits.csv`에는 다음 category가 실제로 존재한다.

```text
STAT, COMBAT, COLLECTION, BOSS, RESIST, AI
```

v1.1 canonical schema는 다음 enum만 허용한다.

```text
STAT, COMBAT, ECONOMY, SURVIVAL, UTILITY
```

`COLLECTION`, `BOSS`, `RESIST`, `AI`의 변환표가 없고, v1.2 enum 보강에도 trait 정정은 없다.
v1.0 golden은 `TRAIT_LUCKY`를 사용하므로 row를 버리거나 disabled로 둘 수 없다.

### 6.2 필수 출력

- canonical trait category enum 최종 목록
- legacy 10개 trait 각각의 exact canonical category
- enum을 확장하는 경우 file schema version과 content contract 영향
- lossy mapping인 경우에도 gameplay effect가 유지되는 근거
- 전체 `traits.csv` exact row 또는 최소한 완전한 legacy category → canonical category 표

권장안은 legacy 의미를 잃지 않도록 enum을 확장해 6개 값을 1:1 보존하는 것이다.

## 7. 정정 5 — equipment source enum과 random drop filter

### 7.1 발견된 충돌

legacy `equipment_templates.csv.source`는 `CRAFT|BOSS_CRAFT`다. v1.1 canonical enum은
`CRAFT|DROP|BOSS|RAID`다. v1.0 random equipment 규칙은 한 곳에서 `source=CRAFT`만 포함한다고
하면서 다른 곳에서는 canonical 후보에서 `source=BOSS_CRAFT`를 제외한다고 표현한다.
`BOSS_CRAFT`는 canonical enum에 없으므로 그대로 방출할 수 없다.

### 7.2 필수 출력

- legacy `CRAFT`, `BOSS_CRAFT`의 exact canonical source 변환
- `boss_id` nullable/FK 행렬
- `RANDOM_EQ_T*_ANY`의 exact source allow/deny predicate를 canonical enum으로만 표현
- random equipment golden 후보 순서가 변하지 않는다는 확인

권장안은 `CRAFT→CRAFT`, `BOSS_CRAFT→BOSS`, random drop은 `source=CRAFT`만 허용하는 것이다.

## 8. 정정 6 — `autonomy_rules.csv` exact runtime 정책

### 8.1 발견된 누락

`autonomy_rules.csv`는 60개 required file에 포함되고 v1.1은 personality와 balance parameter에서
신규 생성하도록 요구한다. 그러나 exact row가 없고 0-row 허용 여부도 없다. 용병 자율행동은 1.0
핵심 범위이므로 구현자가 임의 state transition, priority, threshold를 만들 수 없다.

### 8.2 필수 출력

다음 header의 전체 exact row를 제공한다.

```csv
state,rule_no,priority,condition_type,condition_value,reason_code,next_state,status,enabled
```

만약 P03 BASE에서 header-only로 두고 P04의 새 contentVersion에서 채우는 것이 의도라면 다음을
명시한다.

- row_count=0을 허용하는 이유
- 어떤 validator가 nonempty를 요구하지 않는지
- P04 최초 contentVersion
- P03 player build가 빈 autonomy catalog를 실행하지 않는 보장

## 9. 정정 7 — `raid_difficulties.csv`와 reward group

### 9.1 발견된 누락

legacy raid 두 개에는 `NORMAL|HARD|CORRUPTED` 문자열만 있다. canonical
`raid_difficulties.csv`는 6개 row 각각에 `recommended_power`와 non-null `reward_group_id`를
요구하지만 해당 수치와 reward row가 없다. v1.0의 9개 reward group은 first-clear/part-break만
있고 difficulty 반복 보상 group은 없다.

### 9.2 필수 출력

다음 파일의 exact row를 제공한다.

```csv
raid_id,difficulty,recommended_power,reward_group_id,status,enabled
```

연결되는 신규 `reward_groups.csv`, `reward_entries.csv` row도 모두 제공한다. 기존 v1.0의 9개
reward group과 ID가 충돌하면 안 된다. 난이도별 배율로 계산한다면 base 값, integer rounding과
최종 6개 결과를 함께 고정한다.

## 10. 정정 8 — facility effect, runtime config, localization normalization

### 10.1 발견된 누락

60개 package를 만드는 나머지 legacy normalization 중 다음 값은 source를 단순 분할하는 것만으로
결정되지 않는다.

1. `facility_levels.effect_key`: legacy에는 자연어 `effect_summary`만 있고 executable key가 없다.
2. `runtime_config.value_type/min_value/max_value`: legacy에는 key/value/unit/description만 있다.
3. `name_text_key`: 기존 registry의 `name_ko`를 옮길 exact key naming 규칙이 없다.
4. 기존 `description`, `notes`, `effect_summary`를 무음 폐기하지 말라는 계약은 있지만 어느
   localization key로 옮길지 정해지지 않았다.

### 10.2 필수 출력

- 8시설 × 4레벨의 exact `effect_key`와 각 key의 executable 의미/값
- 14개 `balance_parameters.csv` row의 exact `runtime_config.csv` block
- registry `name_ko` → `name_text_key` deterministic naming 규칙
- description/notes/effect_summary → localization key deterministic naming 규칙
- key 충돌, 64자 StableId 한계와 기존 `localization_ko.csv` key 공존 규칙
- migration report에 남길 source field와 generated key

`effect_key`가 단순 localization key인지 runtime behavior key인지 반드시 구분한다. runtime behavior
key라면 consumer가 해석할 값까지 제공하고, localization 문구는 별도 key로 보존한다.

## 11. 정정 부록 완료 조건

- [ ] 최종 manifest contract version과 exact JSON property가 하나로 확정됐다.
- [ ] 60개 descriptor를 생성할 field domain vocabulary가 완전하다.
- [ ] 네 currency row와 localization이 존재한다.
- [ ] legacy trait category 6종이 모두 canonical에서 유효하다.
- [ ] `BOSS_CRAFT`가 canonical source enum으로 정확히 변환된다.
- [ ] autonomy rules가 exact row 또는 명시적 header-only 정책을 가진다.
- [ ] raid difficulty 6개와 반복 reward FK가 닫힌다.
- [ ] facility effect/runtime config/localization normalization이 결정적이다.
- [ ] v1.0의 18개 CSV block과 두 golden vector는 변경되지 않았다.
- [ ] 정정 후 required canonical CSV의 모든 enabled hard FK가 enabled target을 가진다.
- [ ] 남은 `UNRESOLVED`가 `NONE`이다.

## 12. Codex 재개 게이트

정정 부록을 받으면 Codex는 다음 순서로 재개한다.

1. 정정 부록 자체 일관성 및 legacy source 대조
2. v1.0 설계와 정정 부록을 저장소 기준 문서로 반영
3. deterministic one-way migrator와 60-file schema registry 구현
4. `StreamingAssets/Content` BASE package와 placeholder prefab 생성
5. manifest/importer/tagged-union/generation golden test
6. Unity EditMode/PlayMode/player build와 server regression
7. P03 report 갱신 및 완료 판정

## 13. 최종 통합 문서의 60개 required CSV

아래 파일을 정확히 한 번씩 포함한다. 각 파일에는 최종 header와 전체 canonical row가 있어야 한다.
0-row가 의도된 파일도 header-only fenced `csv` block과 0-row validator 근거를 제공한다.

```text
asset_register.csv
autonomy_rules.csv
condition_group_members.csv
condition_groups.csv
conditions.csv
content_aliases.csv
currencies.csv
enhancement_rules.csv
equipment_job_eligibility.csv
equipment_qualities.csv
equipment_quality_weights.csv
equipment_templates.csv
facilities.csv
facility_levels.csv
facility_upgrade_materials.csv
items.csv
job_skill_unlocks.csv
jobs.csv
kingdom_stages.csv
localizations.csv
loot_entries.csv
loot_tables.csv
mercenary_appearance_pool_entries.csv
mercenary_generation_profiles.csv
mercenary_grades.csv
mercenary_name_pool_entries.csv
mercenary_ranks.csv
monsters.csv
npc_professions.csv
npc_proficiency_levels.csv
offline_reward_rules.csv
personalities.csv
potions.csv
progression_flags.csv
promotion_grade_requirements.csv
raid_difficulties.csv
raid_part_effects.csv
raid_parts.csv
raids.csv
random_equipment_tier_specs.csv
recipe_materials.csv
recipe_outputs.csv
recipes.csv
recruitment_pity_groups.csv
recruitment_pity_rules.csv
recruitment_pool_entries.csv
recruitment_pools.csv
recruitment_rate_up_entries.csv
recruitment_rate_up_groups.csv
refine_options.csv
regions.csv
reward_entries.csv
reward_groups.csv
runtime_config.csv
skills.csv
status_effects.csv
trait_job_eligibility.csv
traits.csv
tutorial_grants.csv
tutorial_steps.csv
```

다음 표현은 최종 답변에서 금지한다.

```text
legacy와 동일
기존 값을 보존
writer가 알아서 변환
이전 문서 참조
나머지는 생략
필요 시 추가
예시 row
```

값을 보존한다면 보존된 결과 row를 실제 CSV block으로 다시 적는다. pipe-list와 JSON cell은 관계
row로 전개한 최종 결과만 적는다.

## 14. 60개 table schema registry

CSV block에 앞서 모든 파일의 schema registry를 제공한다. 파일마다 다음 항목이 빠짐없이 있어야
한다.

| 항목 | 요구사항 |
|---|---|
| file name | 13장의 exact file name |
| file schema version | 정수와 증가 조건 |
| required | P03 BASE에서는 모두 `TRUE` |
| primary key | 단일/복합 field 순서 |
| fields | exact header 순서 |
| domain | 최종 manifest domain enum |
| nullable | field별 true/false |
| enum values | exact 목록, 순서 비의존 여부 |
| lexical/range | min/max, decimal, Seed64, locale, UTC 규칙 |
| foreign keys | source fields, target file, target fields |
| tagged union | discriminator별 nullable/FK 행렬 |
| semantic validator | row/table/cross-table 규칙과 오류 코드 |

`status`, `enabled`를 모든 schema의 마지막 두 field로 명시한다. v1.1 압축표가
`name_text_key`를 생략했더라도 실제 최종 schema에서 빠뜨리지 않는다. v1.2 이후 정정으로 field가
교체됐으면 폐기된 old field를 함께 방출하지 않는다.

schema registry는 manifest descriptor를 생성하는 단일 원본이다. JSON manifest에 schema를 손으로
중복 작성하는 별도 원본을 만들지 않는다. 최종 문서는 registry → CSV writer → manifest descriptor
생성 흐름을 고정한다.

## 15. 전체 legacy column migration matrix

현재 `data/csv` 30개 파일의 모든 source column을 다음 중 하나로 분류해 표로 제공한다.

- `COPIED`: lexical normalization만 하고 같은 의미로 이동
- `RENAMED`: 새 field 이름과 target file을 명시
- `SPLIT`: 관계 row로 분리하고 row ordering 규칙 명시
- `DERIVED`: exact deterministic 식과 입력 field 명시
- `REPLACED`: 승인된 신규 canonical row로 교체
- `DROPPED`: runtime 의미가 없고 migration report에 보존할 근거 명시

각 matrix row는 다음 열을 가진다.

```text
source_file,source_column,target_file,target_column,action,conversion_rule,loss_policy,report_code
```

다음 source 값은 특히 무음 폐기하지 않는다.

- 모든 `name_ko`, `description`, `notes`, `effect_summary`, `priority_hint`
- `initial_skill_ids`, `applicable_jobs`, `allowed_jobs`, `ingredients_json`, `materials_json`
- `unlock_condition`, `unlock_type`, `unlock_value`, `break_reward_condition`
- `repeat_difficulties`, `first_clear_reward`, `condition`, `environment_tag`
- `source_region`, `primary_uses`, `rarity`, `ui_color`, `ui_token`

canonical schema에 target field가 없어서 보존할 수 없다면 schema correction 또는 명시적 migration
report 보존 중 하나를 최종 결정한다. 구현자가 새 field를 임의로 추가하게 하지 않는다.

## 16. 전체 localization bundle

`localizations.csv`는 기존 `localization_ko.csv` 19행뿐 아니라 모든 registry의 표시 문자열을
포함한다. 다음 원칙과 전체 결과 row를 제공한다.

- `(locale,text_key)` composite key
- locale은 현재 `ko-KR`
- `name_ko`는 exact `name_text_key`로 이동
- description/notes/effect summary는 별도 exact text key로 이동
- key는 StableId 길이 제한과 충돌 방지 규칙을 만족
- 같은 한국어 문자열이어도 의미 context가 다르면 무조건 합치지 않음
- 원문 CSV/row/column을 migration report에 기록
- 내부 asset metadata처럼 localization 대상이 아닌 문자열은 제외 근거 명시

최종 `localizations.csv` block의 row count를 실제 전체 row와 함께 제공한다. “규칙만 제공하고 writer가
생성”하는 답변은 허용하지 않는다.

## 17. 전체 FK·tagged-union closure

60개 CSV block을 기준으로 다음 closure 표를 제공한다.

1. 모든 enabled hard FK source row 수
2. target registry별 enabled target row 수
3. nullable FK의 discriminator별 null/non-null 조건
4. 0-row registry를 참조하는 enabled source가 0임을 증명
5. system sentinel별 허용 file/field/context
6. reward type별 target registry와 quantity/probability/weight 규칙
7. recruitment result type별 target registry와 grade/job selector 규칙
8. condition member/alias/raid part composite key 규칙
9. equipment template/job/quality/spec 후보가 모든 tier×job에서 nonempty임을 증명
10. tutorial grant, raid reward, loot reward 사이의 중복 지급 여부와 의도

`reward_entries`, `tutorial_grants`, `loot_entries`, `recipe_outputs`가 같은 discriminator를 쓰더라도
각 context에서 허용되는 subtype 차이를 명시한다.

## 18. deterministic writer와 package fingerprint

최종 문서는 writer가 OS/locale/Python/Unity 버전에 무관하게 같은 bytes를 내도록 다음을 고정한다.

- source CSV decoder와 BOM 허용/제거 규칙
- RFC 4180 parser와 JSON/pipe legacy cell을 migration 단계에서만 읽는 규칙
- 관계 row의 정렬 key와 숫자 정렬/문자열 정렬 구분
- UTF-8 BOM 없음, CRLF, final CRLF, CSV quote 조건
- decimal/boolean/null/UTC/date/Seed64 canonical lexical form
- JSON object property ordering과 RFC 8785 manifest bytes
- SHA-256 계산 대상 exact bytes
- row count 정의
- schema registry와 manifest descriptor drift 검사
- `SOURCE_DATE_EPOCH=1784332800` 적용 위치
- 같은 contentVersion에 다른 bytes가 생겼을 때 split-brain 실패

최종 문서의 CSV block은 Markdown line ending과 무관하다. writer가 위 규칙으로 canonical bytes를
생성한다. Codex가 생성 후 기록할 전체 package fingerprint는 `BUILD_DERIVED`이며, 설계 답변이
가짜 hash를 만들어 넣지 않는다.

## 19. generation·reward golden 전체 고정

v1.0에서 검증된 다음 golden은 값 변경 없이 최종 문서에 다시 포함한다.

- mercenary `operationSeed=123456789`
- growth factor BPS 6개
- random equipment `operationSeed=987654321`

추가로 다음을 제공한다.

- special pity 9→10, 79→80 경계 fixture
- 80번째 pull에서 두 rule 동시 발동 fixture
- tutorial fixed job이 `nextBounded(1)`을 소비하는 fixture
- multi-draw `drawIndex=0`과 `drawIndex=9` operation seed fixture
- quality affix gate 성공 fixture와 refine value inclusive-bound fixture
- crash retry snapshot verbatim fixture
- offline line identity 6 type fixture
- tutorial multi-line grant idempotency fixture

각 fixture는 입력, ordered candidates, 모든 consumed raw uint64, bounded 값, 최종 결과와 expected error
또는 digest를 포함한다. cryptographic digest가 필요한 fixture는 canonical input bytes를 함께 제공한다.

## 20. placeholder asset과 Addressables exact contract

다섯 내부 placeholder를 실제 Unity batch 생성할 수 있도록 다음을 확정한다.

- prefab 저장 경로 5개
- Addressables group 이름과 address 5개
- address가 `asset_id`와 byte-for-byte 같다는 규칙
- root/child hierarchy와 component 목록
- 기준 pixel size, pivot, pixels-per-unit, sorting layer/order
- 직업별 RGBA 색상과 무기/역할 표식
- animation 없음의 정확한 의미
- 외부 texture/font/material 미사용 보장
- editor generator idempotency와 stale asset 정리 규칙
- asset registry row와 prefab/address 존재 검증
- player build에서 missing/duplicate address를 fail closed하는 오류 코드

scene/prefab YAML을 텍스트로 만들지 않고 Unity Editor API로 생성한다는 원칙을 명시한다.

## 21. P03 구현 범위와 이후 Phase 경계

v1.0은 tutorial, offline, recruitment, random equipment의 실행 의미까지 상세히 서술한다. P03은
콘텐츠 파이프라인·Save 단계이므로 이번 구현에 포함할 runtime 범위를 표로 고정한다.

각 항목을 다음 중 하나로 분류한다.

- `P03_IMPLEMENT`: migrator, writer, importer, schema/semantic validator, immutable catalog, deterministic
  algorithm/golden, Save/content compatibility, placeholder asset/package
- `P03_FIXTURE_ONLY`: 이후 consumer가 사용할 DTO/algorithm contract와 fixture만 구현
- `LATER_PHASE`: gameplay orchestration은 구현하지 않고 정확한 target Phase를 명시

최소한 다음을 각각 분류한다.

- 실제 모집 비용 차감·pity transaction·용병 Instance 추가
- tutorial step action 실행과 skip grant 원자 적용
- offline 6 type simulation과 reward 지급
- random equipment reward journal/snapshot 적용
- facility build/craft/store transaction
- raid part/first-clear/difficulty reward 지급
- Android/player build 시 package·Addressables 검증

이 표로 P03에서 다음 Phase 기능을 과도하게 선행 구현하거나, 반대로 P03 필수 validator/golden을
누락하는 일을 막는다.

## 22. 최종 답변 구조

답변 `TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md`는 다음 순서로 작성한다.

1. 문서 권위와 supersedes 목록
2. 최종 결정 요약 및 `UNRESOLVED NONE`
3. P03/LATER Phase 범위 matrix
4. manifest contract version 결정
5. 완전한 manifest JSON Schema
6. field domain vocabulary
7. 60-table schema registry
8. 60개 전체 canonical CSV block
9. 전체 legacy column migration matrix
10. localization bundle 규칙과 전체 row
11. FK/tagged-union closure
12. deterministic writer/package contract
13. mercenary/equipment/pity/offline/tutorial golden fixtures
14. placeholder asset/Addressables contract
15. validator 오류 목록
16. implementation acceptance checklist
17. superseded 문구 목록
18. 남은 `UNRESOLVED: NONE`

이 문서는 v1.0, v1.0.1 정정 요청, v1.1/v1.2/v1.2.1/v1.2.2의 P03 구현 관련 결정을 한
곳에 병합한 최종 권위 문서다. 최종 답변에서 “이전 문서와 동일”이라고 생략하지 않는다.

## 23. 한 번에 끝내는 최종 수용 게이트

- [ ] 문서 한 파일만으로 clean checkout에서 60-file package를 생성할 수 있다.
- [ ] 60개 schema registry와 60개 full CSV block이 모두 존재한다.
- [ ] 모든 legacy source column의 처리 결과가 matrix에 있다.
- [ ] 모든 display/description 문자열의 localization row가 있다.
- [ ] manifest version/property/domain 충돌이 없다.
- [ ] 모든 enabled hard FK가 닫힌다.
- [ ] 모든 enum legacy 값의 exact canonical 변환이 있다.
- [ ] autonomy/facility/raid difficulty/runtime config 값이 완전하다.
- [ ] 18개 검증 완료 CSV와 두 기존 golden이 유지된다.
- [ ] 신규 경계/idempotency golden이 exact expected 값을 가진다.
- [ ] placeholder prefab을 Unity Editor API로 재현할 수 있다.
- [ ] P03과 later Phase 구현 경계가 명시됐다.
- [ ] build-derived hash 외에 placeholder나 생략값이 없다.
- [ ] `UNRESOLVED`, `TODO`, `TBD`가 없다.
- [ ] 이 답변 이후 추가 상세 설계 요청 없이 Codex가 구현·테스트·player build를 완료할 수 있다.
