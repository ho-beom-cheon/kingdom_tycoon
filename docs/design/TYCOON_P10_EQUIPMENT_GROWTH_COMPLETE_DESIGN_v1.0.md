# TYCOON P10 장비 성장 최종 설계서 v1.0

> 문서 상태: FINAL
> 구현 기준: P09 `1.0.0-content.7` / Save v1
> 산출 버전: Game `1.0.0-p10`, Content `1.0.0-content.8`
> IMPLEMENTATION_READY: YES
> UNRESOLVED: NONE

## 1. 목적과 완료 정의

P10은 P07 인벤토리와 P09 생산 장비를 입력으로 강화, 기본 제련, 분해를 제공한다. 사냥 재료가 생산을 거쳐 장비가 되고, 다시 장비 성장과 재료 회수로 이어지는 첫 완전한 성장 순환을 완성한다.

P10 완료 조건은 다음과 같다.

1. content.7 저장을 손실 없이 content.8로 마이그레이션한다.
2. 강화는 +0~+10, 시설 레벨 상한, 성공 확률, 실패 보정을 결정론적으로 처리한다.
3. 제련은 장비당 현재 옵션 1개와 미결 후보 1개만 허용하고 현재/후보 중 선택할 수 있다.
4. 분해는 Tier별 기본 강화석과 실제 투자 강화석의 70%를 원자적으로 반환한다.
5. 장비, 재료, 개인 골드, 경제 원장, operation journal, 성장 이벤트는 Save revision 1회에 함께 커밋한다.
6. 16:9~20:9 모바일 화면에서 확률, 비용, 실패 결과, 보정, 현재/후보 옵션, 분해 반환을 실행 전 확인할 수 있다.
7. Content/Save golden, EditMode, PlayMode, 렌더 캡처, CI와 Android 빌드 진입점을 제공한다.

## 2. 선행 계약과 범위 경계

### 2.1 보존하는 계약

- P07 장비 식별자, Tier, 품질, 장착 링크, 잠금, 획득 content/operation 식별자
- P07 유효 전투력 공식 `floor(floor(basePower*qualityBps/10000)*(10000+enhancementLevel*500)/10000)`
- P08 개인 골드와 store economy ledger의 hash chain 및 pruning
- P09 생산 큐, 재고 목표, 예약 재료, `PRODUCTION` 재고 출처, NPC 숙련 XP
- RFC 8785 request hash, operation replay, expected revision/CAS, UUIDv7

### 2.2 구현 범위

- 대장간 레벨 기반 강화·제련·분해 gate
- +0~+10 강화와 누적 실패 보정
- 단일 제련 옵션 후보 생성과 수락/유지
- 단일·일괄 분해 최대 20개
- content.8, Save migration/recovery, 성장 전용 UI와 증거

### 2.3 제외 범위

- 장비 파괴, 강화 단계 하락, 유료 재화 보호: 1.0 금지
- 다중 제련 옵션, 옵션 잠금, 제련 재료 보호: 후속 업데이트
- 자동 강화·자동 제련·자동 분해: 후속 업데이트
- 장비 거래소·경매장: 1.0 제외
- 생산 취소·예약 재료 환불: P09 계약 유지
- 장비 내구도: 1.0 비활성

## 3. Content `1.0.0-content.8`

content.7의 78개 표를 보존하고 아래 2개 표를 추가한다. 총 표 수는 80개다. `csvSchemaSetVersion=6`, `minimumGameVersion=1.0.0-p10`이다.

### 3.1 `equipment_growth_facility_rules.csv`

```csv
facility_level,max_enhancement_level,refine_enabled,dismantle_enabled,batch_dismantle_limit,status,enabled
1,0,FALSE,TRUE,10,TUNABLE,TRUE
2,5,FALSE,TRUE,10,TUNABLE,TRUE
3,8,TRUE,TRUE,20,TUNABLE,TRUE
4,10,TRUE,TRUE,20,TUNABLE,TRUE
```

Primary key는 `facility_level`이다. `facility_level`은 1..4, `max_enhancement_level`은 0..10, `batch_dismantle_limit`은 1..20이다.

### 3.2 `dismantle_rules.csv`

```csv
equipment_tier,stone_item_id,base_stone_quantity,quality_multiplier_enabled,enhancement_refund_bps,status,enabled
1,MAT_ENHANCE_1,1,TRUE,7000,TUNABLE,TRUE
2,MAT_ENHANCE_1,2,TRUE,7000,TUNABLE,TRUE
3,MAT_ENHANCE_2,2,TRUE,7000,TUNABLE,TRUE
4,MAT_ENHANCE_3,2,TRUE,7000,TUNABLE,TRUE
5,MAT_ENHANCE_4,2,TRUE,7000,TUNABLE,TRUE
```

Primary key는 `equipment_tier`다. `stone_item_id`는 `items.csv(item_id)` HARD FK다. `enhancement_refund_bps`는 0..10000이다.

### 3.3 기존 강화 규칙 채택

| 목표 단계 | 기본 성공 BPS | 강화석/수량 | 개인 골드 | 실패 보정 증가 BPS |
|---:|---:|---|---:|---:|
| +1 | 10000 | MAT_ENHANCE_1 / 1 | 100 | 0 |
| +2 | 10000 | MAT_ENHANCE_1 / 1 | 400 | 0 |
| +3 | 10000 | MAT_ENHANCE_1 / 1 | 900 | 0 |
| +4 | 10000 | MAT_ENHANCE_1 / 1 | 1600 | 0 |
| +5 | 10000 | MAT_ENHANCE_2 / 1 | 2500 | 0 |
| +6 | 8500 | MAT_ENHANCE_2 / 2 | 3600 | 500 |
| +7 | 7200 | MAT_ENHANCE_2 / 2 | 4900 | 600 |
| +8 | 6000 | MAT_ENHANCE_3 / 3 | 6400 | 700 |
| +9 | 4800 | MAT_ENHANCE_3 / 3 | 8100 | 800 |
| +10 | 3800 | MAT_ENHANCE_3 / 4 | 10000 | 1000 |

`successChanceBps=min(10000,baseChanceBps+enhancementPityBps)`다. +1~+5가 100%이므로 튜토리얼 첫 강화 확정 계약은 별도 상태 없이 충족한다. 성공하면 pity를 0으로 초기화하고 실패하면 해당 목표 단계 증가량을 더해 10000으로 clamp한다. 성공과 실패 모두 강화석과 개인 골드를 소비하며 파괴와 하락은 없다.

### 3.4 기존 제련 규칙 채택

사용자는 `refine_options.csv`의 활성 `refine_option_id`를 선택한다. 선택한 행의 특수 재료와 개인 골드를 소비한 뒤 `min_value..max_value`에서 1 BPS 단위 정수 값을 inclusive 추첨한다. 옵션 종류 자체는 무작위로 바꾸지 않는다. 이 방식은 옵션별 재료 FK를 실제 비용 계약으로 사용하며 무의미한 재료 선결제를 방지한다.

## 4. Save content.8 계약

### 4.1 `EquipmentInstance` 및 `StoreEquipment` 추가 필드

```json
{
  "enhancementPityBps": 0,
  "enhancementAttemptCount": 0,
  "enhancementMaterialInvested": [],
  "pendingRefineOption": null,
  "refineRollCount": 0
}
```

- `enhancementPityBps`: integer 0..10000
- `enhancementAttemptCount`: safe integer 0 이상, 성공/실패 모든 시도 누적
- `enhancementMaterialInvested`: `{itemId,quantity}` unique/sorted, 최대 4개, 성공/실패에 소비한 강화석 전체
- `pendingRefineOption`: null 또는 `RefineOption`
- `refineRollCount`: safe integer 0 이상
- `refineOption.value`와 `pendingRefineOption.value`는 정수 BPS다.

상점 재고 장비에도 같은 필드를 둔다. 판매·구매는 deep clone으로 값을 보존하지만 성장 명령은 인벤토리 장비에만 적용한다. P10 이후 생산 장비는 모든 필드를 0/null로 초기화한다.

### 4.2 `payload.equipmentGrowth`

```json
{
  "growthVersion": 1,
  "nextEventSequence": 1,
  "events": []
}
```

이벤트는 최대 100개 ring buffer다. 필드는 `sequence`, `eventType`, `equipmentInstanceId`, `actorMercenaryInstanceId`, `resultCode`, `levelBefore`, `levelAfter`, `optionId`, `createdAtUtc`다. nullable 필드는 명시적으로 null을 저장한다.

`eventType`은 다음만 허용한다.

- `EquipmentEnhancementAttempted`
- `EquipmentRefineRolled`
- `EquipmentRefineResolved`
- `EquipmentDismantled`

### 4.3 operation journal과 경제 원장

- `operationType`에 `EQUIPMENT_GROWTH_COMMAND`를 추가한다.
- 경제 원장 `transactionType`에 `ENHANCE_EQUIPMENT`, `REFINE_EQUIPMENT`를 추가한다.
- 골드 지출 원장 line은 `productKind=EQUIPMENT`, `productId=instanceId`, `quantity=1`, `unitPrice=lineTotal=cost`, `stockDelta=0`이다.
- 분해와 제련 후보 확정/폐기는 골드 이동이 없으므로 경제 원장을 추가하지 않고 operation journal/result payload와 성장 이벤트에 기록한다.
- ledger `stockVersionAfter`는 성장 거래에서 변경하지 않고 현재 값을 기록한다.

### 4.4 migration

content.7→content.8 migration은 다음 순서다.

1. 모든 inventory/store 장비에 성장 필드 기본값을 추가한다.
2. `payload.equipmentGrowth`를 추가한다.
3. `gameVersion=1.0.0-p10`, `contentVersion=1.0.0-content.8`로 변경한다.
4. payload/file hash를 RFC 8785로 재계산한다.

기존 enhancement/refine/장착/잠금/출처/상점 획득 필드는 변경하지 않는다. 이미 존재하는 알 수 없는 성장 필드는 migration 전 content.7 schema가 거부하므로 묵인하지 않는다.

## 5. 명령 계약

모든 명령은 `operationId`, `expectedRevision`, `requestHash`, `commandType`을 포함한다. request hash는 requestHash 필드를 제외한 전체 명령을 RFC 8785 canonicalize 후 SHA-256으로 계산한다.

### 5.1 `ENHANCE_EQUIPMENT`

추가 필드: `actorMercenaryInstanceId`, `equipmentInstanceId`.

처리 순서:

1. operation replay와 request hash를 확인한다.
2. Save revision, 대장간 ACTIVE, 시설 레벨 상한을 확인한다.
3. actor가 `IDLE_TOWN`이며 존재하는지 확인한다.
4. 장비가 inventory에 있고 unlocked/unequipped인지 확인한다.
5. 목표 단계 규칙, 강화석, 개인 골드를 사전 검증한다.
6. 결정론적 roll과 유효 확률을 계산한다.
7. 강화석과 골드를 차감하고 투자 집계를 증가시킨다.
8. 성공 시 단계 증가/pity 0, 실패 시 단계 유지/pity 증가를 적용한다.
9. 경제 원장, 성장 이벤트, operation journal을 추가하고 revision 1회로 저장한다.

### 5.2 `ROLL_REFINE_OPTION`

추가 필드: `actorMercenaryInstanceId`, `equipmentInstanceId`, `refineOptionId`.

대장간 레벨 3 이상이며 `pendingRefineOption=null`이어야 한다. 선택 옵션 행의 재료와 개인 골드를 사전 검증 후 소비하고 결정론적 후보를 저장한다. 현재 옵션은 변경하지 않는다.

### 5.3 `RESOLVE_REFINE_OPTION`

추가 필드: `actorMercenaryInstanceId`, `equipmentInstanceId`, `acceptCandidate`.

pending 후보가 반드시 있어야 한다. `acceptCandidate=true`이면 현재 옵션으로 이동하고 false이면 현재 옵션을 유지한다. 두 경우 모두 pending을 null로 지운다. 비용과 RNG는 없다.

### 5.4 `DISMANTLE_EQUIPMENT`

추가 필드: `actorMercenaryInstanceId`, UTF-8 ordinal 오름차순 unique `equipmentInstanceIds` 1..20, `confirmProtected`.

모든 장비를 먼저 검증한 뒤 하나라도 실패하면 아무 것도 변경하지 않는다. locked/equipped/pending refine 장비는 확인 여부와 무관하게 거부한다. inventory policy의 보호 품질, 보스 장비, first discovery에 해당하면 `confirmProtected=true`가 필요하다.

각 장비 반환량:

- 기본 강화석: `max(1,floor(baseQuantity*qualityBps/10000))`
- 투자 강화석: item별 `floor(investedQuantity*enhancementRefundBps/10000)`
- 반환 0인 item은 생성하지 않는다.
- batch는 장비별 floor 후 itemId별 합산한다.

장비를 삭제하고 반환 item stack을 병합한 뒤 inventory capacity를 검증한다. 개인/왕국 골드는 변하지 않는다.

## 6. 결정론과 replay

### 6.1 강화 roll

`seed="operationId:ENHANCE:equipmentInstanceId:targetLevel"`를 UTF-8 SHA-256하고 앞 4 byte를 big-endian unsigned로 읽어 `rollBps=value mod 10000`으로 계산한다. `rollBps < successChanceBps`이면 성공이다.

### 6.2 제련 값

`seed="operationId:REFINE:equipmentInstanceId:refineOptionId:refineRollCount"`를 같은 방식으로 계산한다. `minBps..maxBps` inclusive 범위에서 `min+(value mod (max-min+1))`을 사용한다.

동일 operationId와 동일 hash는 저장된 digest/result payload를 replay한다. 동일 operationId와 다른 hash는 `P10_OPERATION_REPLAY_MISMATCH`다. RNG는 재실행하지 않는다.

## 7. 오류 레지스트리

| 코드 | 의미 |
|---|---|
| `P10_CONTENT_MISSING` | content.8 또는 필수 표 누락 |
| `P10_CONTENT_INVALID` | 표 값·참조·행 수 위반 |
| `P10_SAVE_REVISION_CONFLICT` | expected revision 불일치 |
| `P10_OPERATION_REPLAY_MISMATCH` | operationId/hash 충돌 |
| `P10_BLACKSMITH_LOCKED` | 시설 미건설/비활성 |
| `P10_FACILITY_LEVEL_REQUIRED` | 강화/제련 레벨 부족 |
| `P10_ACTOR_NOT_FOUND` | actor 용병 없음 |
| `P10_ACTOR_NOT_IN_TOWN` | actor가 마을 대기 상태 아님 |
| `P10_EQUIPMENT_NOT_FOUND` | inventory 장비 없음 |
| `P10_EQUIPMENT_LOCKED` | 잠금 장비 |
| `P10_EQUIPMENT_EQUIPPED` | 장착 장비 |
| `P10_ENHANCEMENT_MAX` | +10 또는 시설 상한 |
| `P10_MATERIAL_INSUFFICIENT` | 재료 부족 |
| `P10_PERSONAL_GOLD_INSUFFICIENT` | 개인 골드 부족 |
| `P10_REFINE_NOT_AVAILABLE` | 시설 레벨/옵션 위반 |
| `P10_REFINE_PENDING` | 미결 후보가 이미 있음 |
| `P10_REFINE_CANDIDATE_MISSING` | 확정할 후보 없음 |
| `P10_DISMANTLE_SELECTION_INVALID` | batch 공집합/중복/상한 위반 |
| `P10_PROTECTED_CONFIRM_REQUIRED` | 보호 정책 확인 필요 |
| `P10_TRANSACTION_INVARIANT_FAILED` | 원자성/저장 불변식 위반 |

모든 오류는 Save, inventory, gold, ledger, journal, RNG trace를 변경하지 않는다.

## 8. 전투·가격 연동

- 강화는 P07 유효 전투력의 단계당 +5% 공식을 그대로 사용한다.
- 제련 ATTACK/DEFENSE/HP는 현재 flat stat에 후보 BPS를 적용하고 floor한다.
- CRIT은 BPS를 additive로 적용한다.
- FIRE/FROST/POISON/MATERIAL/RARE/BOSS/PART는 typed sidecar BPS로 보존한다. 현재 P06 일반 전투가 소비하지 않는 sidecar는 P11 raid/loot 정산에서 소비한다.
- P08 장비 가격은 기존 품질·강화 공식만 사용하며 제련은 직접 가격에 반영하지 않는다.
- 성장 완료 후 장비 점수와 자동 장착 평가는 다음 조회부터 즉시 새 값을 사용한다.

## 9. UI/UX 계약

### 9.1 진입과 구조

Bootstrap 하단 전역 진입 버튼 `P10_GROWTH_NAV_BUTTON`을 추가한다. 화면 stable id는 `P10_EQUIPMENT_GROWTH_SCREEN`이다.

첫 viewport는 다음 순서다.

1. 상단: 장비 성장, actor 개인 골드, 대장간 레벨/상태
2. 좌측: inventory 장비 목록과 Tier/품질/+단계/잠금·장착 배지
3. 중앙: 선택 장비 전투력과 강화 확률·보정·비용
4. 우측: 현재 제련 옵션, 후보 비교, 분해 예상 반환
5. 하단: 강화, 제련 후보 생성, 후보 적용/유지, 분해

### 9.2 상태

- Content: 선택 장비와 모든 실행 버튼 표시
- Empty: 장비 없음, 생산 화면 진입 유도
- Locked: 대장간 상태/레벨과 필요한 기능 표시
- Insufficient: 부족한 골드/재료를 비용 행에 표시하고 버튼 disabled
- Pending refine: 현재/후보 비교와 적용/유지 두 primary actions
- Protected: 잠금·장착은 차단, 정책 보호는 2단계 확인
- Success/Failure: 결과 코드, roll/effective chance, 단계·pity 변화 표시
- Error/Conflict: 저장 revision 충돌 후 snapshot 재조회

최소 터치 영역은 64×64 px, 본문 28 px 이상, 중요 수치 32 px 이상이다. 1920×1080 기준으로 구성하고 2400×1080 safe area에서 좌우 패널이 잘리지 않아야 한다. 색만으로 성공/실패를 구분하지 않고 아이콘/텍스트를 함께 쓴다.

## 10. 테스트와 증거

### 10.1 Content/Save

- 80개 표, manifest/schema/reference/hash 검증
- content.7→.8 inventory/store 장비 성장 필드 migration
- new game content.8와 before/after golden
- schema registry .7/.8 동시 선택 및 이전 registry 보존
- payload/file hash, recovery, unknown/duplicate 필드 실패

### 10.2 EditMode

- +1~+5 확정, +6 실패 보정, 보정 상한, +10 상한
- 결정론적 roll과 replay/hash mismatch
- 재료/골드 부족 rollback
- facility level 1/2/3/4 gate
- 제련 inclusive bound, pending, accept/keep
- 단일·batch 분해, 품질 multiplier, 70% 투자 환급
- locked/equipped/protected/pending 차단
- ledger/journal/event 원자성
- structured refine stat 적용

### 10.3 PlayMode/캡처

- 화면 생성, 진입 버튼, stable id
- Content/Empty/Locked/Insufficient/Pending/Protected/Success/Failure
- 1920×1080과 2400×1080 nonblank 캡처
- 모든 실행 버튼 64 px 이상, 텍스트 overflow/overlap 없음

### 10.4 CI/Android

- `scripts/ci/p10.sh`가 generator, source entrypoint, migration, 생성 에셋, 캡처를 검증한다.
- licensed runner에서 P10 EditMode/PlayMode, setup/verifier, capture를 실행한다.
- Android IL2CPP ARM64 개발 빌드 진입점을 제공하고 증거 artifact를 업로드한다.

## 11. P11 인계 경계

P11은 지역 진행, 승급비 또는 후속 로드맵의 다음 확정 범위를 구현하되 P10 장비 성장 필드와 operation/ledger 기록을 재해석하거나 삭제하지 않는다. P10 typed sidecar 옵션을 레이드·loot 계산에 사용하면 기존 BPS 값과 optionId를 그대로 입력으로 사용한다. 다중 옵션이나 옵션 잠금은 별도 Save migration과 UI 계약 없이 추가하지 않는다.

## 12. 완결성 감사

| 검토 항목 | 판정 |
|---|---|
| P07 장비/전투력 호환 | 확정 |
| P08 골드/ledger 호환 | 확정 |
| P09 생산/출처 호환 | 확정 |
| 강화 확률/실패/보정 | 확정 |
| 제련 비용/RNG/선택 | 확정 |
| 분해 반환/보호/batch | 확정 |
| Save migration/recovery | 확정 |
| 원자성/replay/RNG | 확정 |
| UI/접근성/증거 | 확정 |
| P11 인계 | 확정 |
| 미결정 | 없음 |

P10 구현을 막는 미결정 사항은 없다. 수치는 모두 Content CSV의 `TUNABLE` 값이며 코드 상수로 숨기지 않는다.
