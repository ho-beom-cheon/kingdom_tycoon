# P03 canonical content data v1.0.1 정정 부록 요청서

> 이 문서를 `TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md`와 함께 ChatGPT에 전달한다.
> 답변은 `TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.1_CORRECTION_APPENDIX.md`
> 한 파일의 완성된 본문이어야 한다.

## 0. 목적

`TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md`의 8개 gameplay data 묶음은 대부분
구현 가능한 수준으로 확정됐다. 그러나 해당 문서가 요구하는 60개 required canonical CSV 전체를
현재 저장소에서 생성하려고 대조한 결과, 8개 잔여 계약 충돌·누락이 발견됐다.

이번 답변은 v1.0의 확정 row를 다시 설계하지 않고 아래 항목만 최소 정정한다. 답변이 완결되기
전에는 canonical migrator, `StreamingAssets/Content` BASE package와 P03 완료 판정을 재개할 수 없다.

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
- v1.0의 18개 확정 CSV block을 통째로 반복하지 않는다. 정정되는 row만 교체 대상으로 명시한다.
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
