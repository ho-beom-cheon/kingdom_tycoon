# P08 경제·상점 최종 통합 설계 요청서

> 이 문서는 ChatGPT에 그대로 전달해 단일 상세 설계서
> `TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0.md`를 받기 위한 요청서다.
> 답변은 방향 제안이나 예시가 아니라 Codex가 추가 설계 질문 없이 P08 구현,
> 자동 테스트, Android 빌드와 수동 검증까지 끝낼 수 있는 완전한 실행 계약이어야
> 한다. 후속 correction appendix를 전제로 일부를 생략하지 않는다.

## 0. 요청 목적

현재 저장소는 P07까지 다음 플레이 흐름을 구현했다.

```text
왕국 시설 관리
→ 용병 편성
→ R01 자율 사냥과 전투
→ 전리품 정산
→ 인벤토리·포션·장비 관리
→ 개인 골드만 지급하는 임시 자동/수동 판매
```

P08의 목표는 이 흐름을 실제 경제·상점 루프로 확장하는 것이다.

```text
용병 귀환
→ 재료·장비를 상점에 판매
→ 용병 개인 골드 증가와 상점 재고 증가
→ 용병이 포션·장비 구매
→ 개인 골드 감소와 왕국 골드 증가
→ 플레이어가 가격 정책과 재고 상태 관리
→ 다음 사냥 준비
```

현재 Phase 문서는 `Wallets`, `transactions`, `store AI`만 명시한다. 가격 권위,
상점 재고 소유권, P09 생산 전 상품 공급, 개인·왕국 골드 이동, 자율 구매 순서,
Save migration, 실패 원자성과 UI 계약은 없다. 이를 추측하면 경제가 멈추거나
재화·아이템이 복제될 수 있으므로 아래 모든 항목을 한 번에 확정해야 한다.

## 1. 저장소에서 검증된 P07 기준선

새 설계가 명시적으로 `CORRECTION` 또는 `MIGRATION`으로 선언하지 않는 한 아래
계약을 유지한다.

### 1.1 Phase·버전·검증 상태

| 항목 | 현재 exact 값 |
|---|---|
| 선행 Phase | `P07_LOOT_INVENTORY_EQUIPMENT` |
| P07 GitHub Issue / Draft PR | `#22` / `#23` |
| P08 설계 요청 Issue | `#26` |
| Unity | `6000.3.20f1` |
| contentVersion | `1.0.0-content.5` |
| canonical table 수 | 68 |
| saveVersion / schemaId | `1` / `urn:tycoon:save:v1` |
| P07 Save schema | 조건부 `save.content.5.schema.json` |
| P07 EditMode | 72 passed / 0 failed |
| P07 PlayMode | 18 passed / 0 failed / 1 environment skip |
| P07 Android | IL2CPP ARM64 Development APK 생성 완료 |

P07 완료 보고서가 남긴 P08 차단점은 다음과 같다.

- 가격 권위와 판매 가격을 확정해야 한다.
- 상점 재고와 갱신 규칙을 확정해야 한다.
- 구매·판매 명령과 journal 원자성을 확정해야 한다.
- Save field, migration, 실패 코드와 executable golden이 필요하다.
- 상점 UI, Android 수용 기준과 캡처 목록이 필요하다.

### 1.2 현재 Save root와 경제 관련 필드

Save root는 다음 필드를 유지한다.

```text
schemaId, saveVersion, gameVersion, contentVersion, saveId, profileId,
revision, createdAtUtc, savedAtUtc, integrity, payload
```

`payload`는 다음 필드를 유지한다.

```text
profile, kingdom, mercenaries, managementNpcs, facilities, inventory,
regions, recruitmentMockState, tutorial, offline, operationJournal,
settings, extensions
```

현재 P07 신규 게임의 경제 관련 값:

```text
contentVersion = 1.0.0-content.5
revision = 1
payload.kingdom.kingdomGold = 5000
payload.kingdom.pricingPolicy = STANDARD
payload.mercenaries[*].personalGold = 500
payload.inventory = itemStacks + equipment + warehousePotions
```

`payload.kingdom.pricingPolicy`는 이미 아래 enum을 허용하지만 P07에서는 저장과
표시 외 실제 가격 계산에 사용하지 않는다.

```text
LOW | STANDARD | HIGH
```

각 용병은 `personalGold`, `personalityId`, `potions`, `equipmentSlots`, `autonomy`를
가진다. 개인 골드와 왕국 골드는 safe integer `0..9007199254740991` 범위다.

### 1.3 현재 상점 시설과 관리 NPC

상점은 P04부터 다음 계약을 가진다.

| 항목 | exact 값 |
|---|---|
| facilityId | `FAC_STORE` |
| operationMode | `MANAGED` |
| requiredProfessionId | `NPC_MERCHANT` |
| facility level | 1..4 |
| P07 신규 게임 state | `BUILDABLE` |
| P07 신규 게임 assigned NPC | `null` |
| P07 신규 게임 storage | `[]` |
| starter merchant | `NPC_MERCHANT`, `NPC_APPRENTICE`, 미배치 |

상점 level effect 문구의 기존 의도:

| Level | 기존 의미 |
|---:|---|
| 1 | 기본 매입·판매 |
| 2 | 가격 정책·재고 목표 |
| 3 | 희귀 장비 취급 |
| 4 | 보스 장비 진열 |

P04는 상점 기능을 `LOCKED_UNTIL_PHASE=P08`로 표시했다. P08은 기존 시설
건설·업그레이드·NPC 배치 상태와 충돌하지 않고 실제 기능을 개방해야 한다.

### 1.4 현재 인벤토리·판매 계약

P07의 권위 인벤토리는 전역 `payload.inventory`다.

```text
payload.inventory.itemStacks[]
payload.inventory.equipment[]
payload.inventory.warehousePotions[]
payload.mercenaries[].potions[]
payload.mercenaries[].equipmentSlots
payload.kingdom.inventoryPolicies
```

P07 `SELL_INVENTORY`는 다음 입력을 받는다.

```text
operationId
expectedRevision
requestHash
mercenaryInstanceId
lines[{kind,id,quantity}]
```

현재 runtime constructor는 `kind/id/quantity` 한 line만 받고 request canonical JSON에서
길이 1의 `lines` 배열로 감싼다. P08이 multi-line 거래를 도입한다면 old one-line hash와
replay를 보존하면서 새 DTO/version을 구분해야 한다. 또한 현재 quantity `<=0` 판매는
판매 전용 오류가 아니라 `P07_POTION_TRANSFER_INVALID`를 반환하므로 P08 error registry에서
명시적 `CORRECTION`으로 처리해야 한다.

현재 임시 동작:

- `ITEM`: `items.sell_price × quantity`를 판매 용병의 개인 골드에 지급한다.
- `EQUIPMENT`: `max(1, EffectivePower × 2500 / 10000)`을 개인 골드에 지급한다.
- 판매한 재료·장비는 인벤토리에서 제거되며 상점 재고에는 들어가지 않는다.
- 왕국 골드는 변하지 않는다.
- 상점 건설·NPC·재고·가격 정책을 검사하지 않는다.
- equipped, locked, boss, 보호 품질, 최초 발견, unknown line은 판매할 수 없다.
- 한 line이라도 잘못되면 전체 명령이 실패한다.
- auto-sale도 동일한 보호 규칙을 사용하고 개인 골드만 지급한다.

P08은 위 명령을 호환 확장, migration 또는 명시적 supersede 중 하나로 정확히
결정해야 한다. P07 journal replay와 이미 커밋된 Save의 의미를 깨뜨리면 안 된다.

### 1.5 현재 hunt terminal과 journal 계약

P07은 encounter 중 persistent write를 하지 않는다. 귀환 완료 시
`CommitHuntSettlement`를 정확히 한 번 호출해 전리품, 포션, 장비, 자동 판매,
자동 장착, 개인 골드, 기록, autonomy를 하나의 atomic Save에 반영한다.

공통 명령 규칙:

1. `operationId`로 기존 journal을 찾는다.
2. 동일 `requestHash`의 완료 명령은 저장 결과를 replay한다.
3. 동일 ID와 다른 hash는 split-brain 오류다.
4. `expectedRevision`을 검사한다.
5. clone mutation과 semantic validation을 완료한다.
6. RFC 8785 result digest와 journal을 작성한다.
7. revision을 정확히 1 증가시키고 atomic Save한다.
8. commit 이후에만 domain event와 UI 결과를 발행한다.

현재 `operationJournal.operationType` enum:

```text
OFFLINE_SETTLEMENT | FACILITY_JOB | PROMOTION | RECRUITMENT |
REWARD | SAVE_RECOVERY
```

P08 거래를 `REWARD`로 계속 기록할지 `STORE_TRANSACTION` 같은 새 type을 추가할지,
별도 bounded economy ledger가 필요한지 최종 설계가 확정해야 한다.

### 1.6 현재 콘텐츠 데이터의 가격 관련 상태

| Table | 현재 가격 관련 필드/상태 |
|---|---|
| `items.csv` | `sell_price` 존재, 53개 material 가격 기준 |
| `equipment_templates.csv` | `base_power`, tier, quality 계산 가능, 명시 가격 없음 |
| `potions.csv` | 효과와 tier만 존재, 매입·판매 가격 없음 |
| `personalities.csv` | `buy_threshold_multiplier`, 행동 성향 존재 |
| `facility_levels.csv` | 상점 level과 효과 문구 존재 |
| `npc_professions.csv` | 상점 주인 work unit=`TRANSACTION` |
| `runtime_config.csv` | P08 경제 안전값 없음 |

기존 상위 설계의 가격 정책 초기값:

```text
LOW      = 90%
STANDARD = 100%
HIGH     = 115%
```

자유 가격 입력은 1.0 범위가 아니다.

### 1.7 확정된 경제 원칙

- 용병 개인 골드 획득: 현상금, 재료·장비 판매, 후속 임무·레이드 보상.
- 용병 개인 골드 소비: 장비, 포션, 치료, 승급비.
- 왕국 골드 획득: 용병의 상품 구매, 시장 거래 수수료, 왕국 목표·업적.
- 왕국 골드 소비: 시설, NPC, 생산 보조, 숙소, 승급 지원.
- 상점이 용병에게 지급하는 매입 골드는 외부 유동성이다.
- 매입 대금은 왕국 Treasury에서 차감하지 않는다.
- 왕국 골드가 0이어도 일반 사냥과 기본 판매 루프는 멈추면 안 된다.
- 용병은 포션을 산 뒤에도 장비를 위해 저축할 수 있어야 한다.
- 고가 정책은 판매량 감소가 체감돼야 한다.
- 경제 수치는 CSV 또는 content data의 `TUNABLE` 값이어야 한다.

### 1.8 Unity·디자인·에셋 기준

- uGUI + TextMeshPro, 1920×1080, 16:9~20:9 Safe Area.
- Presentation/Application/Domain/Infrastructure 계층을 유지한다.
- View는 Save, Catalog, HTTP API를 직접 호출하지 않는다.
- UI는 Loading/Content/Empty/Error/Locked/Offline 6개 상태를 가진다.
- P08~P14는 상업 이용이 확인된 무료·기보유 또는 프로젝트 내부 생성 에셋만
  사용한다.
- `이블헌터 타이쿤`은 왕국 운영과 자율 용병 경제가 읽히는 상용 완성도의
  참고 기준일 뿐이다. 타사 그래픽·UI·명칭·연출을 복제하지 않는다.
- 외부 에셋은 `asset_register.csv` 등록 전 프로젝트에 넣지 않는다.
- 에셋은 ID·Addressables 카탈로그로 교체 가능해야 한다.

## 2. 답변 공통 규칙

1. 답변 파일명은 정확히
   `TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0.md`로 한다.
2. 답변 자체만으로 구현 가능한 standalone 문서로 작성한다. “P07과 동일”,
   “기존 문서 참고”만으로 구현 필수 필드·row·공식을 생략하지 않는다.
3. 모든 규칙에 `CONFIRMED`, `TUNABLE`, `CORRECTION`, `MIGRATION`, `DEFERRED`,
   `OPS_LATER`, `REFERENCE_ONLY`, `UNRESOLVED` 중 하나를 표시한다.
4. P08 구현에 필요한 항목을 `UNRESOLVED`로 남기지 않는다.
5. 값이 조정 가능하면 `TUNABLE`로 표시하되 정확한 초기값, 단위, 범위와 저장
   위치를 제공한다.
6. 기존 P07 계약을 바꿀 때는 correction/migration 표에 이전 값, 새 값, 이유,
   호환 전략, golden fixture를 기록한다.
7. `1.0.0-content.5` 파일을 in-place 수정하지 않는다. 새 immutable package와
   정확한 `contentVersion`을 제공한다.
8. 새 CSV 또는 기존 CSV 변경은 exact header, 전체 production row, PK/FK/domain,
   validator error, 정렬 순서와 UTF-8/CRLF bytes SHA-256을 제공한다.
9. 새 Save 조건부 schema는 완전한 JSON Schema Draft 2020-12 본문, registry 변경,
   full golden JSON과 실제 계산한 SHA-256을 제공한다.
10. request/result/journal golden은 RFC 8785 canonical bytes와 실제 SHA-256을
    제공한다. `TODO_HASH`, 예시 hash, placeholder hash는 금지한다.
11. 금액·수량·revision은 safe integer를 사용한다. 소수 계산은 런타임 float가
    아니라 integer/basis point 공식, rounding 순서, 최소·최대 clamp를 확정한다.
12. 시간은 UTC `yyyy-MM-dd'T'HH:mm:ss.fff'Z'`, instance/operation ID는 기존
    UUIDv7 계약을 유지한다.
13. enum, ID, command, event, error, localization key와 UI stable element ID를
    정확히 제공한다. “등”으로 생략하지 않는다.
14. 구현자에게 `A 또는 B`, “상황에 따라”, “적절히”를 남기지 않고 권장안 하나를
    최종 확정한다.
15. P09 생산·NPC 숙련, P10 제작, P13 모집, P15 오프라인 정산, P16 HTTP 서버를
    앞당겨 구현하지 않는다.
16. 프리미엄 재화는 로컬 권위로 변경하지 않는다. P08은 개인·왕국 골드만
    persistent mutation한다.
17. 유료 에셋 구매나 도입을 설계 범위에 넣지 않는다.
18. 최종 문서 끝에 `UNRESOLVED: NONE`, `IMPLEMENTATION_READY: YES`와 문서
    hash scope·실제 SHA-256을 제공한다.
19. 답변 전에 상호 참조, row 수, hash, enum, error registry, golden을 자체
    감사하고 correction appendix가 필요 없는 한 파일로 통합한다.

## 3. P08 범위와 Phase 소유권

다음 기능의 소유권을 하나의 표로 확정한다.

```text
Capability | P08Behavior | PersistentAuthority | DeferredOwner |
P08MustNotImplement | UIExposure
```

최소 포함 기능:

- 용병 개인 골드 wallet 조회·증감 불변식
- 왕국 골드 wallet 조회·증감 불변식
- 상점 매입과 판매
- 상점 재고와 품절
- LOW/STANDARD/HIGH 가격 정책
- 용병 성격·예산·필요도 기반 자율 구매
- 포션과 직업 적합 장비 구매
- P07 자동/수동 판매의 상점 거래 확장
- 거래 이력 또는 bounded projection
- 상점 시설/NPC 상태에 따른 기능 정지와 복구
- 경제 deadlock 방지용 P08 system supply

반드시 후속 Phase로 남길 기능:

- P09 생산 큐, 실제 제작 output, stock target 자동 생산, NPC 숙련 XP
- P09 치료소 치료 transaction
- P10 장비·포션 제작, 강화, 제련, 분해
- P11 승급비 전체 루프
- P13 주점·특별 모집 비용 차감
- P14 레이드 상점·보상 확장
- P15 오프라인 경제 정산
- P16 서버 wallet API·네트워크 동기화
- 자유 입력 가격, 거래소, 경매장, 플레이어 간 거래
- PvP·길드·광고·결제·프리미엄 재화 mutation

## 4. 경제 권위·wallet·불변식

### 4.1 반드시 확정할 항목

- 개인 골드와 왕국 골드의 단일 persistent authority 경로
- query projection과 mutation owner
- 각 거래 유형별 payer, payee, 외부 source/sink, stock mutation
- 상점 매입 대금이 왕국 골드에서 차감되지 않는 exact 처리
- 용병 구매 대금 중 왕국 골드에 귀속되는 금액과 수수료
- 가격 정책 변경 비용 유무
- 0원 거래 허용 여부와 최소 가격
- safe integer overflow 전 사전 검사와 exact error
- 음수·중간 상태·부분 반영 금지
- 실패·replay 시 wallet delta 0 또는 저장 결과 반환
- hunt bounty, P07 sale, P08 store 거래의 reason code 구분
- 왕국 골드 0, 용병 골드 0, stock 0에서 가능한 행동
- 튜토리얼 보조금은 P08 migration에 넣는지 P15로 남기는지

필수 표:

```text
TransactionType | Actor | PersonalGoldDelta | KingdomGoldDelta |
StoreStockDelta | GlobalInventoryDelta | ExternalLiquidityDelta |
JournalReason | Preconditions | FailureAtomicity
```

필수 invariant 표:

```text
InvariantId | AppliesTo | ExactRule | ValidationTiming |
RepairOrFailClosed | ErrorCode
```

## 5. 가격 권위와 integer 공식

### 5.1 가격 정책

기존 의도인 90%/100%/115%를 채택하거나 명시적 correction으로 바꾸되 다음을
완전히 확정한다.

- policy ID와 multiplier bps
- 어떤 거래 방향에 policy가 적용되는지
- 상점 매입가와 용병 구매가의 spread
- 정책 변경이 기존 stock lot의 가격을 바꾸는지
- quote 생성 시점과 transaction commit 시점의 가격 재검증
- 시설 level, merchant 존재, 상품 tier/quality가 가격에 미치는 영향
- P09 NPC proficiency 효과를 P08에서 0으로 고정하는 방식

### 5.2 상품 종류별 기준 가격

다음 각각의 exact integer 공식을 제공한다.

- material/item store buy price와 store sell price
- potion buy price와 필요한 base price data
- equipment instance buy/sell price
- tier, quality, enhancement, refine, boss source, locked 상태의 반영 여부
- quantity multiplication과 checked overflow
- bps multiply/divide 순서와 floor/ceil/round-half 규칙
- 계산 결과 0일 때 최소 가격
- UI 표시 quote와 실제 commit price 일치 규칙

필수 공식 출력 형식:

```text
FormulaId | Inputs | OrderedIntegerSteps | Rounding | Clamp |
ExampleVector | ExpectedResult
```

최소 golden vector:

- item 1개/다수 LOW·STANDARD·HIGH 매입과 판매
- T1 COMMON +0 장비와 품질·강화 경계 장비
- 소형 포션 1개/stack
- 가격 1, maximum-safe-near-boundary, overflow rejection
- 정책 변경 직전 quote와 변경 후 commit conflict

## 6. 상점 개방·시설 level·NPC 상태

다음 matrix를 exact 제공한다.

```text
FacilityState | StoreLevel | MerchantAssigned | MerchantWorking |
StoreMode | BuyAllowed | SellAllowed | PolicyAllowed |
SystemSupplyAllowed | StopReason | UiState | ErrorCode
```

반드시 결정한다.

- `FAC_STORE`가 BUILDABLE인 P07 신규 게임에서 P08 기능의 최초 진입 흐름
- 건설·claim·merchant 배치 후 거래 개방 순서
- `LOCKED`, `BUILDABLE`, `BUILDING`, `ACTIVE`, `UPGRADING`, `STOPPED` 각각의 동작
- 업그레이드 중 거래 가능 여부
- NPC 해제 직전 pending quote와 진행 중 transaction 처리
- 잘못된 직종/중복 배치/누락 merchant 로드 처리
- 상점 level 1..4별 assortment, tier, quality, 정책 기능, stock capacity
- level 2의 가격 정책·재고 목표 중 P08과 P09 소유 범위
- level 3 희귀 장비와 level 4 보스 장비 취급의 exact unlock
- P04 Drawer의 `LOCKED_UNTIL_PHASE=P08` 표시를 제거하는 migration/projection

## 7. 상점 재고 소유권·Save 모델

### 7.1 반드시 해결할 충돌

현재 전역 inventory는 왕국 창고 권위이고 `FAC_STORE.storage`는 P04부터 존재하지만
asset amount만 표현한다. 장비는 개별 instance다. P08은 판매한 재료·장비·포션과
판매 상품의 소유권을 어디에 저장할지 하나로 확정해야 한다.

다음을 제공한다.

- store stock의 단일 Save path
- global inventory, facility storage, store stock의 책임 분리
- material/item stack schema
- potion stack schema
- equipment instance와 store ownership/link schema
- stock line stable ID 또는 compound key
- source type, acquired operation, quoted/base price snapshot 필요 여부
- stock capacity, distinct line/quantity 제한, exactly-full/overflow 처리
- canonical sort와 serialization order
- sold-out line 제거 규칙
- locked/protected/equipped 장비가 stock으로 들어갈 수 없는 불변식
- store가 STOPPED일 때 stock 보존
- facility upgrade 중 capacity 감소 가능 여부와 overflow 처리
- corrupt stock와 broken equipment link의 fail-closed/recovery

필수 schema 출력:

```text
ObjectName | SavePath | RequiredFields | FieldDomains | PK/Uniqueness |
CrossObjectInvariants | OwningCommand
```

## 8. P09 전 P08 system supply와 stock refresh

P08에는 구매할 상품이 필요하지만 실제 생산은 P09 소유다. 임의 무한 재고나 P09
선행 구현 없이 플레이 가능한 공급 계약을 확정한다.

반드시 결정한다.

- P08에서 허용하는 `SYSTEM_SUPPLY`의 정확한 상품 목록과 수량
- 필수 하급 포션, T1 직업 장비, 재료 중 무엇을 공급하는지
- 신규 게임/migration/상점 최초 개장/품절 시 공급 트리거
- 시간 기반이면 authoritative UTC, refresh interval, offline 경계와 clock rollback
- transaction-count 기반이면 exact counter와 threshold
- RNG가 있으면 seed bytes, draw order, rejection rule과 executable trace
- deterministic fixed assortment인지 rotating assortment인지
- refresh가 revision/journal을 증가시키는지
- 앱 재시작·중복 event·crash에서 중복 stock 방지
- 무료 supply인지 상점의 외부 조달인지와 경제 source/sink 표시
- player가 반복 refresh로 차익을 만들 수 없는 방지 규칙
- P09 진입 시 system supply를 어떻게 disable/migrate하는지

필수 출력:

```text
SupplyRuleId | Trigger | FacilityLevel | Product | TargetQuantity |
RefreshRule | PriceBasis | IdempotencyKey | JournalReason |
P09ReplacementContract
```

## 9. 용병의 판매·구매 자율 AI

P06 상태 머신에는 다음 상태가 있으나 실제 상점 mutation은 아직 없다.

```text
SELL_LOOT | BUY_CONSUMABLES | EVALUATE_EQUIPMENT | BUY_EQUIPMENT |
HEAL | IDLE_TOWN
```

`HEAL`은 P09 치료소 소유로 유지하면서 P08이 나머지 상태를 어떻게 개방하는지
exact transition matrix를 제공한다.

```text
CurrentState | Trigger | OrderedGuards | CandidateOrdering |
CommandOrNoOp | NextState | ReasonCode | RetryRule | ErrorFallback
```

반드시 결정한다.

- active/inactive/injured/returned mercenary별 상점 AI 실행 여부
- 귀환 정산과 판매·구매의 transaction 분리 또는 단일 terminal 확장 여부
- 최대 16명 동시 귀환 시 deterministic 처리 순서
- 판매 대상 선정과 P07 inventory policy 관계
- 개인 소유권이 없는 global inventory에서 판매 용병을 결정하는 규칙
- 포션 target quantity, reserve gold, 장비 저축 reserve
- potion 구매와 equipment 구매의 우선순위
- 현재 장비 score, upgrade threshold, price penalty, job eligibility 공식
- personality별 `buy_threshold_multiplier` 적용 공식과 decimal 제거/bps migration
- PRACTICAL, FRUGAL, GEARHEAD 등 모든 personality의 구매 차이
- stock 1개를 여러 용병이 원할 때 승자와 conflict/retry
- 한 cycle 최대 거래 수, cooldown, 무한 구매·판매 loop 방지
- 같은 상품을 매입 직후 재판매해 차익을 내지 못하는 조건
- 상점 정지·품절·개인 골드 부족 시 다음 autonomy 상태
- AI 결정 trace와 Development Build 진단 표시

## 10. 수동·자동 command와 query 계약

최소 다음 command/query를 채택하거나 정확한 대체 이름으로 확정한다.

```text
SetPricingPolicy
SellToStore
BuyFromStore
RunStoreAutonomyCycle        // 내부 use case면 외부 command 여부 명시
RefreshSystemStoreSupply    // 내부 event면 외부 command 여부 명시
GetStorefront
GetStoreQuote
GetStoreTransactionHistory
```

각 command에 다음 표를 제공한다.

```text
CommandType | InputDTO | CanonicalHashFields | Preconditions |
OrderedMutation | OutputDTO | RevisionDelta | JournalEntries |
Idempotency | ErrorCodes
```

추가 필수 결정:

- multi-line buy/sell 허용 여부와 all-or-nothing 원칙
- `SELL_INVENTORY`의 유지, alias, migration 또는 rejection 정책
- P07 old request replay와 P08 new request hash 구분
- manual transaction에서 판매 개인 골드 수령 용병 선택 규칙
- equipment 구매 즉시 장착 또는 인벤토리 보관
- potion 구매 destination과 distinct-slot capacity
- quote ID/expiry/revision/policy/stock snapshot 필드
- stale quote와 stale revision 중 우선 error
- 동일 operationId+same hash와 different hash 결과
- UI double tap, concurrent command, app pause 중 transaction 처리
- undo 금지 여부와 잘못된 거래의 보정 transaction 정책

## 11. transaction journal·ledger·crash 원자성

P08 거래마다 다음 sequence를 번호로 확정한다.

1. request parse와 requestHash constant-time 검증
2. operation replay/mismatch 확인
3. expectedRevision, contentVersion, quote, facility/NPC 검사
4. wallet, stock, inventory, ownership, capacity guard
5. immutable input/price/policy snapshot 생성
6. clone에서 ordered mutation
7. cross-object semantic validation
8. result canonicalization과 digest
9. journal/ledger 작성
10. revision+1과 integrity 재계산
11. atomic Save
12. post-commit event/UI 발행

반드시 결정한다.

- 거래 operationType enum
- operation journal과 별도 economy ledger의 필요 여부
- ledger가 있다면 append-only field schema와 bounded/pruning 정책
- idempotent replay에 필요한 full result 저장 위치
- sale/buy/system supply/pricing change reason code
- 한 거래의 journal row 수와 line ordering
- wallet balance after, stock after, line price snapshot 기록 여부
- pre-commit crash, atomic replace crash, post-commit pre-event crash의 복구
- batch 중간 line 실패 시 mutation 0
- journal/ledger 무결성 오류의 fail-closed와 UI recovery
- P16 서버 wallet ledger와 이름·DTO가 충돌하지 않는 adapter 경계

필수 executable golden:

- `SET_PRICING_POLICY`
- single/multi-line `SELL_TO_STORE`
- material/potion/equipment `BUY_FROM_STORE`
- autonomous sell→buy cycle
- system supply refresh
- replay, hash mismatch, revision conflict, stale quote

각 golden은 full request JSON, RFC 8785 bytes, requestHash, before/after full relevant
snapshot, full result JSON, resultDigest와 journal/ledger row를 포함한다.

## 12. P07→P08 Save migration·schema·recovery

다음을 확정한다.

- P08 exact `contentVersion`
- saveVersion 1 유지 여부
- `save.content.6.schema.json` 같은 conditional schema ID/파일명
- `.5` schema 보존과 `.5` Save의 P08 field 거부
- P08 schema required/optional field와 `additionalProperties=false`
- P07 신규/운영 Save를 P08로 올리는 migration registry entry
- kingdomGold, personalGold, pricingPolicy 보존
- store state/stock/ledger/supply counter 초기값
- P07 operationJournal와 committed sale 의미 보존
- auto-sale pending 상태가 없는 이유 또는 normalization
- merchant/facility state가 다른 Save별 migration matrix
- migration operationId, requestHash, journal type과 revision 증가 규칙
- migration 중 실패·전원 종료·재시도 안전성
- contentVersion downgrade 거부
- corrupt stock/wallet/ledger의 schema failure와 3-backup recovery

필수 산출물:

1. `P08_NEW_GAME_GOLDEN` 전체 JSON
2. `P07_TO_P08_GOLDEN_BEFORE` 전체 JSON
3. `P07_TO_P08_GOLDEN_AFTER` 전체 JSON
4. 상점 미건설/ACTIVE/STOPPED 세 migration fixture
5. full P08 JSON Schema Draft 2020-12
6. schema registry 전체 변경 JSON
7. JCS payload/envelope bytes와 실제 SHA-256
8. migration error registry와 recovery UI 결과

## 13. P08 canonical content package

새 immutable package의 전체 계약을 제공한다.

- exact contentVersion, packageKind, baseContentVersion
- 모든 table 파일명, row 수, header, PK, hard/soft FK, status/enabled 규칙
- unchanged table도 P08 package에 포함되는 exact bytes/hash
- manifest 전체 JSON, schema set 전체 JSON, localization 전체 bundle
- raw design manifest와 executable manifest가 다르면 correction rule과 두 hash
- generator 정렬, CSV quoting, CRLF, UTF-8 no BOM 규칙
- 이전 `.5` package 동시 보존과 active version 전환

최소 검토·추가 대상:

```text
currency_definitions
pricing_policies
store_level_rules
store_assortment
store_supply_rules
store_price_rules
store_purchase_ai_rules
economy_runtime_config
transaction_reason_codes
autonomy_rules P08 rows
facility effect P08 activation
asset_register P08 UI assets
localizations ko-KR/en-US
```

다른 table 구성을 선택할 수 있지만 의미가 빠져서는 안 되며, 최종 문서에는 선택한
이름 하나와 전체 production row를 제공한다.

필수 검증 산출물:

```text
File | RowCount | Header | PrimaryKey | Sha256 | ChangeFromContent5 |
ValidatorChecks | OwningRuntimeType
```

package 전체 byte 수, manifest SHA-256, localization SHA-256, Save schema SHA-256와
package fingerprint를 실제 값으로 제공한다.

## 14. P09 생산·NPC 숙련 handoff 계약

P08의 임시 system supply가 P09 구현을 오염시키지 않도록 다음을 확정한다.

- P08에서 merchant proficiency는 표시만 하고 transaction 효과 0인지
- P08이 management NPC XP를 증가시키지 않는다는 경계
- P09 생산 output이 들어갈 exact stock API/Save path
- P09 stock target이 P08 stock과 겹칠 때 source별 merge/priority
- P09 활성화 후 P08 system supply disable/축소 시점
- 기존 system-supplied stock의 보존·판매·식별
- P09 migration이 P08 transaction history를 유지하는 방식
- `FacilityProductionStopped`와 P08 store stop reason의 구분
- P08 API가 P09 구현 시 breaking change 없이 확장되는 interface

필수 출력:

```text
P08Seam | P08ExactBehavior | P09ExtensionPoint | ForbiddenP08Mutation |
MigrationNeed | CompatibilityTest
```

## 15. Store UI·시설 Drawer·내비게이션

P08 상점은 하단 6개 내비게이션에 별도 상점 탭을 임의로 추가하지 않는다. 상점
시설 Drawer에서 전체 Store 화면으로 진입할지 Drawer 안에서 끝낼지 하나를 확정하고
exact hierarchy와 좌표를 제공한다.

반드시 포함할 화면/기능:

- 왕국 HUD의 왕국 골드 실시간 갱신
- 상점 시설 상태와 merchant 표시
- 상품 목록과 category/filter/sort
- item/potion/equipment stock 수량과 품절
- 개인 골드를 가진 구매 용병 선택
- 장비 현재/후보 score·stat·가격 비교
- potion 보유량·target 표시
- 판매 대상, 보호/판매 불가 이유, 예상 개인 골드
- LOW/STANDARD/HIGH 정책과 예상 효과
- 거래 확인 modal과 완료 toast
- 최근 거래 또는 자동 거래 activity projection
- 왕국 골드·개인 골드 변화 애니메이션/접근성 대체 표시

필수 hierarchy/좌표 표:

```text
ElementId | ParentId | AnchorMin | AnchorMax | OffsetMin | OffsetMax |
MinTouchSize | VisibleWhen | InteractableWhen | LocalizationKey |
DataSourceDTO
```

필수 상호작용:

- 상점 시설 선택, open/close, Android back
- tab/filter/sort와 스크롤 virtualization
- 상품 선택→상세→quote→confirm→commit→result
- 판매 multi-select 여부와 보호 항목 처리
- 가격 정책 변경 confirm과 stale state 처리
- modal 동시 1개, focus order, 큰 글자 모드
- 16:9/18:9/20:9 Safe Area와 한글 긴 문자열
- command 중 double tap 차단, loading timeout, retry

## 16. Loading/Content/Empty/Error/Locked/Offline 상태

Store screen과 상점 Drawer에 대해 6개 상태를 모두 exact 정의한다.

```text
UiState | EntryCondition | StorefrontVisibility | PrimaryAction |
SecondaryAction | DisabledReasons | LocalizationKey | AnalyticsEvent |
SaveMutationAllowed
```

필수 사례:

- content/Save load 전 Loading
- 정상 stock과 거래 가능한 Content
- 허용 상품 0개 또는 stock 0의 Empty
- schema/catalog/journal 오류의 Error
- 시설 미건설·상점 level 부족의 Locked
- P16 이전 로컬 모드에서 Offline의 정확한 의미
- merchant 없음/STOPPED는 Empty, Locked, Content-disabled 중 하나로 확정
- Error 화면이 Save를 임의 수정하지 않는 규칙
- DebugCanvas의 quote/AI/ledger 진단과 Release Build 노출 금지

## 17. 상용 디자인·placeholder·Addressables 계약

P08에서는 유료 에셋을 사용하지 않는다. 프로젝트 내부 생성 placeholder 또는 이미
등록된 상업 이용 가능 에셋만 사용한다.

다음을 확정한다.

- Store screen, category, currency, stock, policy, transaction state asset ID
- 각 asset의 Addressables group/label/address
- sprite reference size, PPU, filter, pivot와 color role
- 무료/내부 asset source와 `asset_register.csv` 전체 추가 row
- missing asset fail-closed/fallback
- P15/P17 production asset 교체 시 ID·code·Save 불변 계약
- 외부 상용 게임의 화면·아이콘·배치를 복제하지 않는 독자적 layout
- 따뜻한 다크 판타지, 목재·석재·불빛, 고대비 보상 색상의 적용 위치
- 모션 duration/easing, reduce-motion 대체

필수 출력:

```text
assetId | address | group | label | role | dimensions | pivot | PPU |
source | license | commercialUse | replacementContract
```

## 18. ko-KR/en-US localization 전체 bundle

P08에서 필요한 모든 UI·오류·상태 문구를 exact CSV 전체 row로 제공한다.

최소 범주:

- 상점·매입·판매·구매·재고·품절
- 개인 골드·왕국 골드·외부 유동성 설명
- LOW/STANDARD/HIGH 이름과 효과
- 시설/NPC/level/정책 잠금 이유
- 상품 category, filter, sort, 장비 비교
- 판매 보호·구매 제한·용병 선택
- 가격 변경·quote stale·revision conflict
- 거래 성공·실패·replay
- 6개 UI 상태와 retry/back/confirm/cancel
- system supply와 P09 생산 잠금 설명
- 자동 거래 reason/trace 사용자 문구

exact 형식:

```csv
locale,text_key,text_value,context,status,enabled
```

interpolation placeholder 문법, 금액 formatting, 한글 조사 처리, en-US plural과
긴 문자열 수용 기준을 확정한다.

## 19. Domain·Application·Infrastructure·Presentation 계약

클래스명과 namespace를 구현자가 임의로 결정하지 않도록 exact type map을 제공한다.

### Domain

- PersonalWallet / KingdomWallet value and invariant
- Money/Price/PricePolicy value object
- StoreStock / StoreStockLine / StoreEquipment ownership
- StoreAvailability / StopReason
- StorePricingService
- StorePurchaseDecision / personality policy
- StoreTransaction and line result
- domain events와 invariant validator

### Application

- storefront/quote/history query
- pricing policy command
- buy/sell command
- autonomous store cycle
- system supply refresh use case
- unit-of-work, clock, UUID, request canonicalizer interface

### Infrastructure

- P08 content catalog projection
- Save v1 mapper/schema registry/migration
- atomic repository adapter
- transaction journal/ledger adapter
- P16 future network adapter seam

### Presentation

- StoreScreenPresenter/View
- facility Drawer entry adapter
- product list virtualization
- quote/confirm/result modal
- pricing policy control
- wallet animation/accessibility renderer
- Android back/input adapter

필수 표:

```text
TypeName | Namespace | Layer | Responsibility | Dependencies |
PublicMembers | PersistentMutationAllowed
```

DTO는 exact field, type, nullability, sort order와 localization boundary를 제공한다.
View에 `JObject`, Save model 또는 Catalog row를 직접 노출하지 않는다.

## 20. Domain event·reason·error registry

최소 검토 event:

```text
StoreOpened
StoreStopped
PricingPolicyChanged
StoreStockAdded
StoreStockRemoved
MercenarySoldToStore
MercenaryBoughtFromStore
PersonalGoldChanged
KingdomGoldChanged
SystemStoreSupplyRefreshed
StoreItemSoldOut
```

최소 검토 error:

```text
P08_CONTENT_MISSING
P08_STORE_LOCKED
P08_STORE_NOT_ACTIVE
P08_MERCHANT_REQUIRED
P08_STORE_LEVEL_REQUIRED
P08_PRODUCT_NOT_FOUND
P08_STOCK_EMPTY
P08_STOCK_INSUFFICIENT
P08_PERSONAL_GOLD_INSUFFICIENT
P08_PRICE_OVERFLOW
P08_QUANTITY_INVALID
P08_ITEM_PROTECTED
P08_EQUIPMENT_INELIGIBLE
P08_DESTINATION_CAPACITY_EXCEEDED
P08_QUOTE_STALE
P08_POLICY_INVALID
P08_OPERATION_REPLAY_MISMATCH
P08_SAVE_REVISION_CONFLICT
P08_TRANSACTION_INVARIANT_FAILED
P08_SUPPLY_ALREADY_APPLIED
P08_AUTONOMY_CYCLE_LIMIT
```

이 이름을 채택하거나 정정안을 하나로 확정하고 모든 runtime 발생 code가 registry에
정확히 한 번 존재하도록 한다.

필수 표:

```text
Code | ExactTrigger | UserMessageKey | Retryable | LoggingLevel |
SaveMutationAllowed | UiRecovery | TelemetryFields
```

reason code도 transaction/event/autonomy별 exact registry와 localization 여부를
제공한다.

## 21. 테스트·executable golden·경제 soak

모든 테스트에 deterministic clock, UUID, RNG, content package와 zero sleep 규칙을
제공한다.

### 21.1 EditMode 필수

- LOW/STANDARD/HIGH 가격 공식과 boundary vector
- item/potion/equipment 매입·판매 공식
- 모든 personality purchase score와 reserve gold
- wallet 0/min/max/overflow와 음수 방지
- store level 1..4 assortment/capacity
- 시설/NPC 상태 matrix
- stock stack/individual equipment ownership invariant
- protected/equipped/unknown sale rejection
- potion/equipment purchase destination capacity
- quote stale, expectedRevision, policy conflict
- operation same hash replay/different hash mismatch
- multi-line 중간 실패의 mutation 0
- system supply 최초/중복/crash/restart
- P07→P08 migration과 schema `.5`/`.6` 선택
- journal/ledger append-only와 balance reconciliation
- P09 생산/NPC 숙련 호출 0회
- full content/manifest/localization/schema hash

### 21.2 PlayMode 필수

- Kingdom→상점 시설 Drawer→Store 진입
- 미건설→건설→merchant 배치→거래 가능
- 귀환 용병 판매→개인 골드 증가→stock 증가
- 포션 구매→개인 골드 감소→왕국 골드 증가
- 장비 비교·구매와 즉시 장착/보관 확정 흐름
- 가격 정책 변경과 표시 가격 갱신
- 품절→system supply 또는 명시된 recovery
- merchant 해제→STOPPED와 disabled reason
- 자동 용병 SELL→BUY_CONSUMABLES→BUY_EQUIPMENT→IDLE
- Loading/Content/Empty/Error/Locked/Offline
- Android back, modal 1개, double tap, 64×64 touch
- 16:9/18:9/20:9, 큰 글자, 한글 긴 문자열

### 21.3 통합·soak 필수

- 사냥→귀환→loot→판매→구매→재출발 30분 결정론 loop
- 최대 16명과 마지막 stock 1개 contention
- transaction마다 wallet·stock·inventory·ledger reconcile
- persistent wallet/stock 음수 0회
- 재화·아이템 복제 0회
- 경제 deadlock 0회 또는 정확한 safety recovery
- 정책별 구매율 차이를 보여주는 fixed simulation golden
- crash injection 각 mutation step과 재로드 결과
- Save round trip과 3-backup recovery
- server Testcontainers 기존 10개 regression 유지, P08 HTTP 호출 0회

필수 test 표:

```text
TestId | TestClass | Mode | Given | When | Then | Fixture |
ExpectedHashOrVector | Timeout
```

## 22. 성능·메모리·수명주기 수용 기준

다음을 수치로 확정한다.

- storefront 100 stock line query p95와 allocation
- live product view 최대 수와 virtualization pool
- price recompute 100 lines × 3 policies p95
- 16 mercenary autonomy cycle budget
- transaction command p95와 Save write 횟수
- scroll steady-state allocation
- wallet animation과 Drawer frame target
- 30분 economy loop memory sustained growth
- Store 화면 10회 open/close listener·Addressables handle baseline
- scene 전환 후 presenter/view/service reference 정리
- Android Development profiler marker 목록

P07의 100-row p95 3ms/96KiB, 30분 memory growth 8MiB, Android p95 33.3ms
기준을 유지하거나 변경 이유와 새 exact 수치를 제공한다.

## 23. Editor generation·Addressables·Android build 계약

다음을 exact class/method/path로 제공한다.

- P08 content generator와 `--check` entry
- Save schema/golden generator와 hash guard
- Store placeholder asset generator
- Store screen/facility Drawer scene configurator
- generated asset verifier와 idempotent rerun 기준
- Addressables group/label/address 규칙
- EditMode/PlayMode test filter
- capture generator
- Android builder와 APK 파일명
- build 전 content/schema/asset/scene guard
- `.meta` 동시 커밋과 YAML 직접 작성 금지

권장 명령을 최종 class 이름으로 완성한다.

```powershell
python scripts/generate_p08_content.py --check

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.<P08_SETUP_METHOD> `
  -logFile .\client-unity\Logs\p08-setup.log

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform EditMode `
  -testResults .\client-unity\Logs\p08-editmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform PlayMode `
  -testResults .\client-unity\Logs\p08-playmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.<P08_BUILD_METHOD> `
  -logFile .\client-unity\Logs\p08-android-build.log
```

## 24. 수동 검증·화면 캡처

최소 다음 캡처를 exact fixture, 해상도, 파일명과 성공 조건으로 제공한다.

| Capture intent | 최소 요구 |
|---|---|
| 상점 Content | stock, merchant, 왕국 골드, 선택 용병 개인 골드 |
| 판매 | 다중 상품 또는 확정된 single flow, 예상 개인 골드 |
| 장비 구매 비교 | 현재/후보 score·stat·가격 |
| 포션 구매 | 보유량, target, 구매 후 wallet |
| 가격 정책 | LOW/STANDARD/HIGH와 가격 변화 |
| 거래 결과 | personal/kingdom gold와 stock delta |
| 품절 Empty | 원인과 다음 행동 |
| merchant 없음 | STOPPED reason과 배치 행동 |
| Locked | 상점 미건설/level 부족 |
| Error | stale quote/retry 또는 Save recovery |
| 20:9 | Safe Area와 긴 한글 |
| 자동 거래 | 용병 행동 이유/activity projection |

필수 physical Android smoke:

- Store 진입·스크롤·filter·modal·back
- 가격 정책 변경과 거래 double tap 방지
- pause/background/강제 종료 후 거래 원자성
- 한국어 글자 잘림과 큰 글자
- 30분 사냥→판매→구매 loop
- 저사양 기준 frame/memory/발열 관찰

## 25. 구현 파일 지도·커밋 분리

최종 설계는 예상 경로와 new/modified/generated를 완전하게 제공한다.

```text
docs/design/TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0.md
scripts/generate_p08_content.py
client-unity/Assets/StreamingAssets/Content/<P08_VERSION>/
client-unity/Assets/KingdomTycoon/Resources/Contracts/
client-unity/Assets/KingdomTycoon/Runtime/Domain/Economy/
client-unity/Assets/KingdomTycoon/Runtime/Application/Economy/
client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Economy/
client-unity/Assets/KingdomTycoon/Runtime/Presentation/Store/
client-unity/Assets/KingdomTycoon/Editor/
client-unity/Assets/KingdomTycoon/Tests/EditMode/
client-unity/Assets/KingdomTycoon/Tests/PlayMode/
docs/reports/P08_ECONOMY_STORE_REPORT.md
```

필수 표:

```text
Path | NewOrModified | Responsibility | GeneratedOrAuthored |
OwningCommit | VerificationCommand
```

최소 논리 커밋 분리:

1. 최종 설계 채택
2. content `.6`와 Save schema/migration
3. wallet·price·stock domain/application
4. P07 settlement와 store autonomy 통합
5. Store UI와 내부 placeholder assets
6. EditMode/PlayMode/soak/Android 검증
7. hook·CI·완료 보고

각 커밋의 Conventional Commit 영문 메시지까지 제공한다.

## 26. 최종 답변 문서 구조

최종 설계서는 다음 순서를 정확히 따른다.

1. 문서 상태·버전·권위·hash scope
2. P08 범위와 Phase 소유권
3. P07 handoff·correction·migration 요약
4. 경제 권위·wallet·불변식
5. 가격 정책과 모든 integer 공식
6. 상점 개방·시설 level·merchant matrix
7. store stock 소유권·Save schema
8. P08 system supply·refresh·P09 교체 계약
9. 판매·구매 자율 AI transition matrix
10. command/query DTO·hash·idempotency
11. transaction journal/ledger·crash 원자성
12. P07→P08 migration·full schema·golden
13. P08 content package 전체 bytes/rows/hash
14. P09 생산·NPC 숙련 handoff
15. Store UI·Drawer·navigation hierarchy·좌표
16. 6개 UI 상태
17. placeholder·Addressables·asset register
18. ko-KR/en-US localization 전체 rows
19. 계층별 type/DTO/public API
20. event/reason/error registry
21. EditMode·PlayMode·integration/soak golden
22. 성능·메모리·수명주기
23. Editor generation·Android build
24. 수동 검증·capture matrix
25. 구현 파일 지도·커밋 분리
26. P08 완료 checklist·P09 handoff
27. 최종 자체 감사 결과
28. `UNRESOLVED: NONE`
29. `IMPLEMENTATION_READY: YES`

## 27. 한 번에 끝내는 최종 수용 게이트

아래 항목이 하나라도 빠지면 구현을 시작하지 않고 설계 수정이 필요하다. 최종 답변을
내기 전에 작성자가 스스로 전부 확인해 한 문서에 통합한다.

- [ ] P08 포함·제외와 P09~P16 소유권이 모순 없이 확정됐다.
- [ ] 개인·왕국 골드의 모든 거래 delta와 외부 source/sink가 정의됐다.
- [ ] wallet·stock·inventory·journal의 불변식과 음수/overflow 방지가 완전하다.
- [ ] LOW/STANDARD/HIGH, item/potion/equipment 가격 공식과 rounding이 exact하다.
- [ ] 상점 시설 6상태 × level 4 × merchant 상태 matrix가 있다.
- [ ] store stock의 단일 Save path와 equipment ownership이 확정됐다.
- [ ] P09 전 system supply가 결정적이고 반복 악용·중복을 막는다.
- [ ] P09 production 전환 시 system supply replacement 계약이 있다.
- [ ] 모든 personality의 판매·구매 AI와 deterministic 순서가 있다.
- [ ] SELL/BUY/POLICY/SUPPLY command·query DTO와 hash가 exact하다.
- [ ] P07 `SELL_INVENTORY`와 terminal auto-sale의 호환·migration이 확정됐다.
- [ ] 거래별 journal/ledger row와 crash/replay 결과가 확정됐다.
- [ ] full request/result/before/after/journal executable golden과 실제 hash가 있다.
- [ ] P08 contentVersion, 전체 package rows/bytes/manifest/hash가 있다.
- [ ] full P08 Save schema, registry, new game와 migration JSON/hash가 있다.
- [ ] `.5` Save 호환과 `.6` mixed-field rejection이 검증된다.
- [ ] Store UI hierarchy·좌표·stable ID·상호작용이 완전하다.
- [ ] Loading/Content/Empty/Error/Locked/Offline가 모두 정의됐다.
- [ ] ko-KR/en-US localization 전체 row가 있다.
- [ ] 무료/내부 placeholder와 Addressables replacement 계약이 있다.
- [ ] Domain/Application/Infrastructure/Presentation type map이 있다.
- [ ] 모든 runtime event/reason/error가 registry에 정확히 한 번 있다.
- [ ] EditMode·PlayMode·30분 economy soak와 contention/crash test가 exact하다.
- [ ] 성능 수치, Android build entry, physical smoke와 capture matrix가 있다.
- [ ] 구현 파일 지도와 논리 커밋 분리가 있다.
- [ ] 모든 row 수·FK·enum·hash·golden을 자체 감사했다.
- [ ] 구현 필수 `UNRESOLVED`가 `NONE`이다.
- [ ] `IMPLEMENTATION_READY: YES`다.

이 수용 게이트를 충족한 단일 설계서를 전달받으면 Codex는 별도 P08 구현 이슈와
브랜치에서 설계 채택 → content `.6` → Save migration → 경제 domain/application →
P07 settlement·autonomy 통합 → Store UI/asset → EditMode/PlayMode/soak → Android build →
화면 캡처 → 완료 보고 → Draft PR까지 한 흐름으로 진행한다.
