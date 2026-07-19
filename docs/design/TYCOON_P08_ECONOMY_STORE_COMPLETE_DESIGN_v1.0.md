# TYCOON P08 경제·상점 최종 통합 설계 v1.0

## 1. 문서 상태·버전·권위·hash scope

| 항목 | 값 |
|---|---|
| 상태 | `CONFIRMED` |
| Phase | `P08_ECONOMY_STORE` |
| 선행 계약 | P07 구현 기준선 `1.0.0-content.5` |
| 새 콘텐츠 | `1.0.0-content.6` |
| Save | `saveVersion=1`, `schemaId=urn:tycoon:save:v1` 유지 |
| P08 조건부 schema | `urn:tycoon:schema:save:v1:content.6` |
| Unity | `6000.3.20f1` |
| 문서 인코딩 | UTF-8 no BOM, LF |

이 문서는 P08 구현·테스트·Android 개발 빌드의 단일 권위다. P07 v1.1의
전리품·인벤토리·장비 계약은 이 문서가 `CORRECTION` 또는 `MIGRATION`으로
명시한 항목만 바꾼다. 수치 행은 `TUNABLE`, 구조·순서·불변식은 `CONFIRMED`다.

`DOCUMENT_SHA256`은 이 파일의 UTF-8 no-BOM bytes에서 아래 값의 64자리 hex만
64개의 `0`으로 치환한 뒤 계산한다. 마지막 자체 감사에서 실제 값을 고정한다.

```text
DOCUMENT_SHA256: 9703af3f39cf7d256ead2bc735bae1ef374491df67507b61a814ba4f98def8a9
```

## 2. P08 범위와 Phase 소유권

| Capability | P08Behavior | PersistentAuthority | DeferredOwner | P08MustNotImplement | UIExposure |
|---|---|---|---|---|---|
| 개인 골드 | 조회·사냥 보상·상점 매입/판매 delta 검증 | `payload.mercenaries[].personalGold` | P11 승급비 | 서버 wallet로 이동 | HUD·용병 선택 |
| 왕국 골드 | 용병 구매대금 전액 귀속 | `payload.kingdom.kingdomGold` | P16 서버 adapter | 프리미엄 재화 변경 | HUD·거래 결과 |
| 상점 stock | stack·개별 장비 소유권, 품절, capacity | `payload.economy.store` | P09 생산 output | 생산 큐·NPC XP | Store 화면 |
| 가격 정책 | LOW/STANDARD/HIGH | `payload.kingdom.pricingPolicy` | 없음 | 자유 가격 | 정책 control |
| 수동 거래 | multi-line all-or-nothing buy/sell | P08 application unit-of-work | P16 network | undo·부분 commit | quote/modal/toast |
| 자율 거래 | 귀환 후 sell→potion→equipment | autonomy state + P08 commands | P09 HEAL | 오프라인 정산 | 최근 활동 |
| system supply | P09 전 필수 포션·T1 무기 고정 공급 | `store.supplyState` | P09 | RNG·시간 refresh | 공급 배지 |
| 거래 이력 | 최근 200건 bounded projection | `store.ledger` | P16 server ledger adapter | 결제 원장 혼합 | 최근 거래 |
| 상점 NPC | 배치·working 여부로 거래 gate | 기존 facilities/NPC | P09 숙련 효과 | XP 증가 | 직원·중단 사유 |

`DEFERRED`: P09 생산·치료, P10 제작/강화/제련/분해, P11 승급비, P13 모집비,
P14 레이드 상점, P15 오프라인 경제, P16 HTTP wallet, 거래소·경매·P2P.
`OPS_LATER`: 결제·프리미엄 재화·운영 분석. P08은 이 기능을 호출하지 않는다.

## 3. P07 handoff·correction·migration 요약

| ID | 이전 | P08 결정 | 상태 | 호환 전략 |
|---|---|---|---|---|
| P08-C01 | `SELL_INVENTORY`가 inventory 제거 후 개인 골드만 지급 | 신규 요청은 `SELL_TO_STORE`; stock·ledger 동시 반영 | `CORRECTION` | 기존 operationId replay만 P07 경로로 처리 |
| P08-C02 | quantity≤0이 `P07_POTION_TRANSFER_INVALID` | `P08_QUANTITY_INVALID` | `CORRECTION` | 과거 journal 의미 불변 |
| P08-C03 | `RETURN_TOWN→IDLE_TOWN` | `RETURN_TOWN→SELL_LOOT` | `CORRECTION` | transient load는 town 상태로 normalize |
| P08-C04 | 가격 decimal 0.9/1.0/1.15 | integer 9000/10000/11500 bps | `MIGRATION` | CSV importer가 P08 bps만 사용 |
| P08-C05 | `REWARD` journal로 임시 판매 | P08 거래는 `STORE_TRANSACTION` | `CORRECTION` | P07 journal row는 다시 쓰지 않음 |
| P08-M01 | economy root 없음 | `payload.economy` 추가 | `MIGRATION` | `.5→.6`, revision +1 |
| P08-M02 | store facility storage는 generic asset amount | store 전용 stock은 economy root | `MIGRATION` | `FAC_STORE.storage`는 비어 있어야 함 |

P07 귀환 terminal은 loot 정산까지만 atomic commit한다. 성공 event 이후 P08
`RunStoreAutonomyCycle`이 별도 transaction을 순차 실행한다. 따라서 전투 terminal
재시도와 상점 실패가 서로의 원자성을 침범하지 않는다.

## 4. 경제 권위·wallet·불변식

### 4.1 단일 권위

- `PersonalWallet`: 선택 용병의 `personalGold`; 다른 mirror를 만들지 않는다.
- `KingdomWallet`: `payload.kingdom.kingdomGold`; Store DTO는 읽기 projection이다.
- mutation owner는 `EconomyUnitOfWork`뿐이다. View·Presenter·Catalog는 쓰지 않는다.
- 상점 매입 대금은 `EXTERNAL_STORE_LIQUIDITY` source다. 왕국 골드를 차감하지 않는다.
- 용병 구매 대금 100%가 왕국 골드로 이동한다. P08 수수료는 0이다.
- 0원 거래는 금지하고 모든 unit price는 최소 1이다.
- 가격 정책 변경 비용은 0이나 revision과 journal을 1회 증가시킨다.
- 튜토리얼 보조금은 P08 migration에 넣지 않는다(`DEFERRED=P15`).

| TransactionType | Actor | PersonalGoldDelta | KingdomGoldDelta | StoreStockDelta | GlobalInventoryDelta | ExternalLiquidityDelta | JournalReason | Preconditions | FailureAtomicity |
|---|---|---:|---:|---|---|---:|---|---|---|
| HUNT_BOUNTY | combat | `+bounty` | 0 | 0 | loot add | `+bounty` | `HUNT_BOUNTY` | P07 terminal | P07 atomic |
| SELL_TO_STORE | mercenary | `+total` | 0 | add sold lines | remove sold lines | `+total` | `MERCENARY_STORE_SALE` | store active | all-or-nothing |
| BUY_FROM_STORE | mercenary | `-total` | `+total` | remove bought lines | equipment add 또는 potion to merc | 0 | `MERCENARY_STORE_PURCHASE` | stock·gold·capacity | all-or-nothing |
| SET_PRICING_POLICY | player | 0 | 0 | 0 | 0 | 0 | `STORE_POLICY_CHANGE` | level≥2 | all-or-nothing |
| SYSTEM_SUPPLY | system | 0 | 0 | add to target | 0 | stock value source | `SYSTEM_STORE_SUPPLY` | epoch eligible | all-or-nothing |

### 4.2 불변식

| InvariantId | AppliesTo | ExactRule | ValidationTiming | RepairOrFailClosed | ErrorCode |
|---|---|---|---|---|---|
| ECO-01 | wallets | `0≤balance≤9007199254740991` | pre/post mutation, load | fail closed | `P08_WALLET_RANGE_INVALID` |
| ECO-02 | transfer | buy의 personal 감소=kingdom 증가=total | pre-save | fail closed | `P08_TRANSACTION_INVARIANT_FAILED` |
| ECO-03 | stock | quantity `1..9999`; 0이면 line 제거 | command·load | fail closed | `P08_STOCK_INVALID` |
| ECO-04 | equipment | instance는 inventory·stock·equipped 중 정확히 1 owner | pre-save·load | backup recovery | `P08_EQUIPMENT_OWNERSHIP_CONFLICT` |
| ECO-05 | sale | equipped/locked/protected/boss 제한을 우회하지 않음 | quote+commit | reject | `P08_ITEM_PROTECTED` |
| ECO-06 | arithmetic | 모든 add/multiply는 checked safe integer | 각 단계 전 | reject | `P08_PRICE_OVERFLOW` |
| ECO-07 | idempotency | same operationId+same hash는 저장 결과 replay | mutation 전 | replay | 없음 |
| ECO-08 | split brain | same operationId+different hash는 mutation 0 | mutation 전 | reject | `P08_OPERATION_REPLAY_MISMATCH` |
| ECO-09 | revision | 성공 command만 정확히 +1 | save 직전 | reject | `P08_SAVE_REVISION_CONFLICT` |
| ECO-10 | stock capacity | distinct stack/equipment 수가 level cap 이하 | quote+commit+load | reject | `P08_DESTINATION_CAPACITY_EXCEEDED` |
| ECO-11 | journal | committed journal·ledger digest/operationId 일치 | load·pre-save | backup recovery | `P08_LEDGER_CORRUPT` |
| ECO-12 | facility storage | `FAC_STORE.storage=[]` | migration·load | fail closed | `P08_STORE_STORAGE_CONFLICT` |

왕국 골드 0이어도 사냥·판매·system supply는 가능하다. 개인 골드 0이면 판매와
사냥은 가능하고 구매만 불가능하다. stock 0이면 판매와 eligible supply는 가능하다.

## 5. 가격 정책과 모든 integer 공식

### 5.1 정책·rounding

| policyId | multiplierBps | 구매율 의도 | 상태 |
|---|---:|---|---|
| LOW | 9000 | 용병 구매가 감소 | `TUNABLE` |
| STANDARD | 10000 | 기준 | `TUNABLE` |
| HIGH | 11500 | 용병 구매가 증가·구매율 감소 | `TUNABLE` |

정책은 Store→용병 판매가에만 적용한다. 용병→Store 매입가는 바뀌지 않는다.
quote는 현재 policy·revision·stockVersion으로 생성하고 commit에서 전부 재검증한다.
기존 stock lot도 현재 정책 가격을 사용하며 가격 snapshot은 ledger에만 남긴다.
merchant 숙련 배율은 P08에서 항상 10000bps다.

공통 함수:

```text
FloorBps(x,bps) = checked(x*bps) / 10000
CeilBps(x,bps)  = (checked(x*bps) + 9999) / 10000
ClampPrice(x)   = clamp(x,1,9007199254740991)
CheckedTotal(unit,qty) = checked(unit*qty), qty 1..9999
```

| FormulaId | Inputs | OrderedIntegerSteps | Rounding | Clamp | ExampleVector | ExpectedResult |
|---|---|---|---|---|---|---:|
| ITEM_ACQUIRE | `items.sell_price` | return base | exact | min1 | herb base5 | 5 |
| ITEM_CUSTOMER | base, policy | `CeilBps(base*2,policy)` | ceil once | min1 | herb STANDARD | 10 |
| POTION_ACQUIRE | potionBase | `FloorBps(base,2500)` | floor | min1 | small base40 | 10 |
| POTION_CUSTOMER | potionBase, policy | `CeilBps(base,policy)` | ceil | min1 | small HIGH | 46 |
| EQUIP_POWER | base,qualityBps,enhance | `q=floor(base*quality/10000); floor(q*(10000+enhance*500)/10000)` | floor each | int32 | 100,10000,+0 | 100 |
| EQUIP_ACQUIRE | effectivePower | `FloorBps(power,2500)` | floor | min1 | power100 | 25 |
| EQUIP_CUSTOMER | effectivePower,policy | `CeilBps(power,policy)` | ceil | min1 | power100,LOW | 90 |
| LINE_TOTAL | unit,quantity | checked multiply | exact | safe-int | 46×3 | 138 |

`refineOption`은 P07 EffectivePower에 영향을 주지 않으므로 P08 가격에도 직접
가산하지 않는다. boss source는 level 4에서만 거래 가능하며 별도 premium은 없다.
locked/equipped/protected는 가격 0이 아니라 거래 불가다.

Golden vectors:

| kind | base/power | qty | LOW | STANDARD | HIGH |
|---|---:|---:|---:|---:|---:|
| item customer | 5 | 1 | 9 | 10 | 12 |
| item acquire | 5 | 7 | 35 | 35 | 35 |
| small potion | 40 | 3 | 108 | 120 | 138 |
| T1 common +0 equipment | 100 | 1 | 90 | 100 | 115 |
| T1 fine +10 equipment | 162 | 1 | 146 | 162 | 187 |
| minimum | 1 | 1 | 2 | 2 | 3 |

`unit=9007199254740991, qty=2`는 commit 전 `P08_PRICE_OVERFLOW`다. policy 변경 후
이전 quote commit은 revision이 다르면 먼저 `P08_SAVE_REVISION_CONFLICT`, revision이
같고 context hash만 다르면 `P08_QUOTE_STALE`다.

## 6. 상점 개방·시설 level·merchant matrix

| FacilityState | Level | MerchantAssigned/Working | StoreMode | Buy | Sell | Policy | Supply | StopReason | UiState | Error |
|---|---:|---|---|---|---|---|---|---|---|---|
| LOCKED | 0 | any | LOCKED | N | N | N | N | FACILITY_LOCKED | Locked | `P08_STORE_LOCKED` |
| BUILDABLE | 0 | none | LOCKED | N | N | N | N | BUILD_REQUIRED | Locked | `P08_STORE_LOCKED` |
| BUILDING | target | any | LOCKED | N | N | N | N | CONSTRUCTION | Locked | `P08_STORE_NOT_ACTIVE` |
| ACTIVE | 1 | yes/yes | OPEN | Y | Y | N | Y | NONE | Content/Empty | 없음 |
| ACTIVE | 2..4 | yes/yes | OPEN | Y | Y | Y | Y | NONE | Content/Empty | 없음 |
| ACTIVE | 1..4 | no/no | STOPPED | N | N | N | N | MERCHANT_REQUIRED | Content-disabled | `P08_MERCHANT_REQUIRED` |
| ACTIVE | 1..4 | yes/no | STOPPED | N | N | N | N | MERCHANT_NOT_WORKING | Content-disabled | `P08_STORE_NOT_ACTIVE` |
| UPGRADING | 1..3 | yes/yes | PAUSED | N | N | N | N | UPGRADING | Content-disabled | `P08_STORE_NOT_ACTIVE` |
| STOPPED | 1..4 | any | STOPPED | N | N | N | N | persisted stop reason | Content-disabled | `P08_STORE_NOT_ACTIVE` |

건설 claim 후 state ACTIVE가 되고 올바른 merchant가 working일 때만 첫 supply를
실행한다. pending quote는 NPC 해제·upgrade 시작 command의 revision 증가로 stale된다.
잘못된 직종·중복 배치·끊어진 NPC link는 load semantic failure이며 backup recovery다.
upgrade는 capacity를 감소시키지 않아 기존 stock을 보존한다.

| Level | maxTier | maxQualityOrder | boss | policy | stackLines | equipmentLines |
|---:|---:|---:|---|---|---:|---:|
| 1 | 1 | 2 | N | STANDARD 고정 | 32 | 12 |
| 2 | 2 | 3 | N | LOW/STANDARD/HIGH | 48 | 24 |
| 3 | 4 | 4 | N | LOW/STANDARD/HIGH | 64 | 48 |
| 4 | 5 | 5 | Y | LOW/STANDARD/HIGH | 96 | 96 |

level 2의 stock target UI는 P08 system supply target만 표시한다. 사용자 생산 목표와
자동 생산은 P09다. P04 `LOCKED_UNTIL_PHASE=P08`는 Save 변경 없이 content.6 projection에서 제거한다.

## 7. store stock 소유권·Save schema

### 7.1 권위 객체

| ObjectName | SavePath | RequiredFields | FieldDomains | PK/Uniqueness | CrossObjectInvariants | OwningCommand |
|---|---|---|---|---|---|---|
| Economy | `/payload/economy` | economyVersion,store | version=1 | singleton | content.6만 허용 | migration |
| Store | `/payload/economy/store` | stockVersion,stackLines,equipment, supplyState,ledger | safe ints·arrays | singleton | FAC_STORE.storage empty | P08 UoW |
| StackStockLine | `store.stackLines[]` | stockLineId,productKind,productId,quantity,sourceType,first/lastOperationId | kind ITEM/POTION; qty1..9999 | stockLineId | allowed product FK | sell/supply/buy |
| StoreEquipment | `store.equipment[]` | P07 equipment fields + sourceType + acquiredOperationId | P07 domains | instanceId | not in inventory/equipped | sell/supply/buy |
| SupplyState | `store.supplyState` | mode,epoch,buyCountSinceRefresh,lastRefreshOperationId | enum/safe ints | singleton | mode matches P09 gate | supply |
| EconomyLedger | `store.ledger` | nextSequence,prunedThroughSequence,prunedDigest,entries | max 200 entries | sequence/operationId | journal digest match | all P08 commands |

`stockLineId` is exact `STACK:<productKind>:<productId>:<sourceType>`. sourceType is
`MERCENARY_SALE|SYSTEM_SUPPLY|PRODUCTION`. 같은 source의 stack만 merge한다. 정렬은
`productKind,productId,sourceType` UTF-8 ordinal이다. equipment는 `instanceId` ordinal.
마지막 unit 구매로 quantity가 0이면 line을 제거한다. STOPPED에서도 stock은 보존한다.
Storefront는 같은 stack product를 한 row로 합산하되 quote allocation을
`MERCENARY_SALE→PRODUCTION→SYSTEM_SUPPLY`, 그 안에서 firstAcquiredOperationId ordinal로
고정한다. equipment는 합산하지 않고 instance별 row다.

Global inventory는 왕국 창고 소유만 가진다. 판매 시 item quantity 또는 equipment
object를 store로 이동한다. 구매 시 potion은 용병 `potions[]`, equipment는 inventory로
이동한 뒤 `equipIfUpgrade=true`면 기존 장착을 inventory로 되돌리고 새 장비를 같은
atomic command에서 장착한다.

### 7.2 schema composition

`save.content.6.schema.json`은 SHA가 고정된 content.5 schema
`d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392`를 입력으로
다음 deterministic composition을 수행한 완전한 Draft 2020-12 schema다.

1. `$id`를 `urn:tycoon:schema:save:v1:content.6`으로 바꾼다.
2. root `contentVersion.const`를 `1.0.0-content.6`으로 고정한다.
3. `$defs.Payload.required`에 `economy`를 `extensions` 바로 앞에 추가한다.
4. `$defs.Payload.properties.economy={"$ref":"#/$defs/Economy"}`를 추가한다.
5. `$defs.OperationJournalEntry.properties.operationType.enum`에
   `STORE_TRANSACTION`을 추가한다.
6. 아래 `$defs`를 그대로 추가한다. 그 외 content.5 node는 byte 의미를 바꾸지 않는다.
7. `$defs.OperationJournalEntry.properties.resultPayload`에
   `{"oneOf":[{"type":"null"},{"type":"object"}]}`를 추가하고 semantic validator가
   `STORE_TRANSACTION+COMMITTED`에서 non-null, 그 외 operation type에서 null을 강제한다.

```json
{
  "Economy":{"type":"object","additionalProperties":false,"required":["economyVersion","store"],"properties":{"economyVersion":{"const":1},"store":{"$ref":"#/$defs/StoreEconomy"}}},
  "StoreEconomy":{"type":"object","additionalProperties":false,"required":["stockVersion","stackLines","equipment","supplyState","ledger"],"properties":{"stockVersion":{"type":"integer","minimum":0,"maximum":9007199254740991},"stackLines":{"type":"array","maxItems":96,"items":{"$ref":"#/$defs/StoreStackLine"}},"equipment":{"type":"array","maxItems":96,"items":{"$ref":"#/$defs/StoreEquipment"}},"supplyState":{"$ref":"#/$defs/StoreSupplyState"},"ledger":{"$ref":"#/$defs/EconomyLedger"}}},
  "StoreStackLine":{"type":"object","additionalProperties":false,"required":["stockLineId","productKind","productId","quantity","sourceType","firstAcquiredOperationId","lastAcquiredOperationId"],"properties":{"stockLineId":{"type":"string","pattern":"^STACK:(ITEM|POTION):[A-Z][A-Z0-9_]*:(MERCENARY_SALE|SYSTEM_SUPPLY|PRODUCTION)$","maxLength":192},"productKind":{"enum":["ITEM","POTION"]},"productId":{"type":"string","pattern":"^[A-Z][A-Z0-9_]*$","maxLength":64},"quantity":{"type":"integer","minimum":1,"maximum":9999},"sourceType":{"enum":["MERCENARY_SALE","SYSTEM_SUPPLY","PRODUCTION"]},"firstAcquiredOperationId":{"type":"string","format":"uuid-v7"},"lastAcquiredOperationId":{"type":"string","format":"uuid-v7"}}},
  "StoreEquipment":{"type":"object","additionalProperties":false,"required":["instanceId","equipmentTemplateId","tier","qualityId","enhancementLevel","refineOption","locked","equippedByMercenaryInstanceId","sourceContentVersion","generationOperationId","stockAcquiredAtUtc","stockAcquiredOperationId","sourceType"],"properties":{"instanceId":{"type":"string","format":"uuid-v7"},"equipmentTemplateId":{"type":"string","pattern":"^[A-Z][A-Z0-9_]*$","maxLength":64},"tier":{"type":"integer","minimum":1,"maximum":5},"qualityId":{"enum":["QUALITY_COMMON","QUALITY_FINE","QUALITY_RARE","QUALITY_LEGACY","QUALITY_RELIC"]},"enhancementLevel":{"type":"integer","minimum":0,"maximum":10},"refineOption":{"oneOf":[{"type":"null"},{"$ref":"#/$defs/RefineOption"}]},"locked":{"const":false},"equippedByMercenaryInstanceId":{"type":"null"},"sourceContentVersion":{"type":"string","minLength":1,"maxLength":64},"generationOperationId":{"type":"string","format":"uuid-v7"},"stockAcquiredAtUtc":{"type":"string","format":"utc-instant"},"stockAcquiredOperationId":{"type":"string","format":"uuid-v7"},"sourceType":{"enum":["MERCENARY_SALE","SYSTEM_SUPPLY","PRODUCTION"]}}},
  "StoreSupplyState":{"type":"object","additionalProperties":false,"required":["mode","epoch","buyCountSinceRefresh","lastRefreshOperationId"],"properties":{"mode":{"enum":["SYSTEM_SUPPLY","PRODUCTION_OWNED"]},"epoch":{"type":"integer","minimum":0,"maximum":9007199254740991},"buyCountSinceRefresh":{"type":"integer","minimum":0,"maximum":8},"lastRefreshOperationId":{"oneOf":[{"type":"null"},{"type":"string","format":"uuid-v7"}]}}},
  "EconomyLedger":{"type":"object","additionalProperties":false,"required":["nextSequence","prunedThroughSequence","prunedDigest","entries"],"properties":{"nextSequence":{"type":"integer","minimum":1,"maximum":9007199254740991},"prunedThroughSequence":{"type":"integer","minimum":0,"maximum":9007199254740991},"prunedDigest":{"oneOf":[{"type":"null"},{"type":"string","pattern":"^[0-9a-f]{64}$"}]},"entries":{"type":"array","maxItems":200,"items":{"$ref":"#/$defs/EconomyLedgerEntry"}}}},
  "EconomyLedgerEntry":{"type":"object","additionalProperties":false,"required":["sequence","operationId","transactionType","actorMercenaryInstanceId","reasonCode","personalGoldDelta","kingdomGoldDelta","stockVersionAfter","committedAtUtc","requestHash","resultDigest","lines"],"properties":{"sequence":{"type":"integer","minimum":1,"maximum":9007199254740991},"operationId":{"type":"string","format":"uuid-v7"},"transactionType":{"enum":["SET_PRICING_POLICY","SELL_TO_STORE","BUY_FROM_STORE","SYSTEM_SUPPLY"]},"actorMercenaryInstanceId":{"oneOf":[{"type":"null"},{"type":"string","format":"uuid-v7"}]},"reasonCode":{"type":"string","pattern":"^[A-Z][A-Z0-9_]*$","maxLength":64},"personalGoldDelta":{"type":"integer","minimum":-9007199254740991,"maximum":9007199254740991},"kingdomGoldDelta":{"type":"integer","minimum":-9007199254740991,"maximum":9007199254740991},"stockVersionAfter":{"type":"integer","minimum":0,"maximum":9007199254740991},"committedAtUtc":{"type":"string","format":"utc-instant"},"requestHash":{"type":"string","pattern":"^[0-9a-f]{64}$"},"resultDigest":{"type":"string","pattern":"^[0-9a-f]{64}$"},"lines":{"type":"array","maxItems":32,"items":{"$ref":"#/$defs/EconomyLedgerLine"}}}},
  "EconomyLedgerLine":{"type":"object","additionalProperties":false,"required":["lineNo","productKind","productId","quantity","unitPrice","lineTotal","stockDelta"],"properties":{"lineNo":{"type":"integer","minimum":1,"maximum":32},"productKind":{"enum":["ITEM","POTION","EQUIPMENT"]},"productId":{"type":"string","minLength":1,"maxLength":192},"quantity":{"type":"integer","minimum":1,"maximum":9999},"unitPrice":{"type":"integer","minimum":1,"maximum":9007199254740991},"lineTotal":{"type":"integer","minimum":1,"maximum":9007199254740991},"stockDelta":{"type":"integer","minimum":-9999,"maximum":9999}}}
}
```

Composer serialization은 property insertion order 유지, indent 2, ensure_ascii=false,
LF, final newline이다. `.5` schema는 P08 field를 거부하고 `.6` schema는 economy를
필수로 요구한다. composition 결과는 62,977 bytes, SHA-256
`b2a7c12e03dc688231a4226bfe04dfdb68b66f4e1bbd63ef4512f403e5054675`다.
schema registry는 아래 `.6` 전용 세 번째 entry를 추가한 826 bytes, SHA-256
`1c87cb7936e1b2a8d36701389a6d1762723208be128e2ddd2d2df5c3e8851b24`다.

```json
{
  "registryVersion": 1,
  "preparseRequired": ["schemaId", "saveVersion", "contentVersion"],
  "entries": [
    {"contentVersions":["1.0.0-content.1","1.0.0-content.2","1.0.0-content.3","1.0.0-content.4"],"schemaFile":"save.schema.json","sha256":"d0f20a54d5096f6dd4cf1b4b859177fa730477634e10e16a237ea8a995d614d5"},
    {"contentVersions":["1.0.0-content.5"],"schemaFile":"save.content.5.schema.json","sha256":"d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392"},
    {"contentVersions":["1.0.0-content.6"],"schemaFile":"save.content.6.schema.json","sha256":"b2a7c12e03dc688231a4226bfe04dfdb68b66f4e1bbd63ef4512f403e5054675"}
  ]
}
```

## 8. P08 system supply·refresh·P09 교체 계약

P08 supply는 deterministic fixed assortment다. RNG·UTC interval·offline catch-up을
사용하지 않는다. `epoch`은 다음에 적용할 epoch다. Store가 처음 OPEN될 때
`targetEpoch=0` refresh를 실행하고 성공 후 `epoch=1`로 만든다. 이후 성공한
`BUY_FROM_STORE` command 8건마다 `targetEpoch=current epoch`으로 target 미만만 채운 뒤
epoch를 1 증가시킨다.

| SupplyRuleId | Level | Product | TargetQuantity | RefreshRule | PriceBasis | IdempotencyKey | JournalReason | P09ReplacementContract |
|---|---:|---|---:|---|---|---|---|---|
| SUP_SMALL_HEAL | 1 | POT_HEAL_SMALL | 8 | target fill | base40 | `P08_SUPPLY:{saveId}:{epoch}:POT_HEAL_SMALL` | SYSTEM_STORE_SUPPLY | P09 potion output 대체 |
| SUP_WARRIOR | 1 | EQ_T1_WARRIOR_WEAPON COMMON+0 | 1 | absent only | power100 | `...:EQ_T1_WARRIOR_WEAPON` | SYSTEM_STORE_SUPPLY | P09 blacksmith output 대체 |
| SUP_GUARDIAN | 1 | EQ_T1_GUARDIAN_WEAPON COMMON+0 | 1 | absent only | power100 | `...:EQ_T1_GUARDIAN_WEAPON` | SYSTEM_STORE_SUPPLY | 동일 |
| SUP_ARCHER | 1 | EQ_T1_ARCHER_WEAPON COMMON+0 | 1 | absent only | power100 | `...:EQ_T1_ARCHER_WEAPON` | SYSTEM_STORE_SUPPLY | 동일 |
| SUP_MAGE | 1 | EQ_T1_MAGE_WEAPON COMMON+0 | 1 | absent only | power100 | `...:EQ_T1_MAGE_WEAPON` | SYSTEM_STORE_SUPPLY | 동일 |
| SUP_CLERIC | 1 | EQ_T1_CLERIC_WEAPON COMMON+0 | 1 | absent only | power100 | `...:EQ_T1_CLERIC_WEAPON` | SYSTEM_STORE_SUPPLY | 동일 |

각 refresh는 한 operation·한 revision·한 ledger entry다. equipment instance UUIDv7은
productId ordinal 순으로 `IUuidProvider`에서 소비한다. 이미 target 이상이면 mutation
없이 `P08_SUPPLY_ALREADY_APPLIED`; `targetEpoch<epoch`도 같은 no-op이다. 이 오류는 UI에 노출하지 않는다. player command로
refresh할 수 없고 반복 open은 epoch를 증가시키지 않는다. 공급품은 Store→용병만
판매할 수 있어 매입 직후 되팔기 차익이 없다. 용병이 되판 equipment는
`MERCENARY_SALE` source로 이동하며 acquire 25%, customer 100% spread를 유지한다.

P09 migration은 `mode=PRODUCTION_OWNED`로 바꾸고 새 system refresh를 금지한다.
이미 존재하는 SYSTEM_SUPPLY stock은 source를 유지한 채 끝까지 판매한다.
targetQuantity는 source별 수량이 아니라 customer-saleable 동일 product의 전체 수량이다.
따라서 mercenary sale이나 P09 production stock이 목표를 채우면 system supply를 추가하지 않는다.

## 9. 판매·구매 자율 AI transition matrix

한 cycle은 town에 있는 active mercenary를 `instanceId` UTF-8 ordinal로 처리한다.
inactive·travel·combat·raid mercenary는 제외한다. injured는 P09 전 `HEAL` mutation 없이
`IDLE_TOWN`에서 대기한다. mercenary당 최대 sell 1, potion buy 1, equipment buy 1,
전체 최대 48 command다.

| CurrentState | Trigger | OrderedGuards/Candidates | CommandOrNoOp | NextState | ReasonCode | Retry | ErrorFallback |
|---|---|---|---|---|---|---|---|
| RETURN_TOWN | arrived | terminal commit 완료 | no-op | SELL_LOOT | RETURNED_TO_STORE | 0 | IDLE_TOWN |
| SELL_LOOT | cycle | active store→P07 policy eligible lines; `(kind,id)` | SELL_TO_STORE | BUY_CONSUMABLES | AUTO_SELL_ELIGIBLE | conflict 1회 | BUY_CONSUMABLES |
| BUY_CONSUMABLES | cycle | target shortage→reserve→unit price | BUY_FROM_STORE max2 | EVALUATE_EQUIPMENT | POTION_TARGET_LOW | conflict 1회 | EVALUATE_EQUIPMENT |
| EVALUATE_EQUIPMENT | cycle | eligible→upgradeGain→price→stock ID | no-op/BUY_EQUIPMENT | BUY_EQUIPMENT/IDLE_TOWN | UPGRADE_FOUND/NONE | 0 | IDLE_TOWN |
| BUY_EQUIPMENT | cycle | best candidate, reserve | BUY_FROM_STORE qty1 equip=true | IDLE_TOWN | EQUIPMENT_UPGRADE | conflict 1회 | IDLE_TOWN |
| HEAL | any | P09 unavailable | no-op | IDLE_TOWN | HEAL_DEFERRED_P09 | 0 | IDLE_TOWN |

Autonomous sale recipient is the current mercenary. Global inventory attribution did not
exist in P07, so each returning mercenary may sell only `P07 auto-sale eligible` lines still
present; the first ordered mercenary consumes them and receives the proceeds. Manual sale
requires explicit recipient and confirmation.

| personality | thresholdBps | reserveGold | potionTarget | equipmentPriority |
|---|---:|---:|---:|---|
| PERSONALITY_PRACTICAL | 10000 | 200 | 4 | balanced |
| PERSONALITY_FRUGAL | 12000 | 400 | 3 | price first |
| PERSONALITY_GEARHEAD | 8200 | 150 | 3 | upgrade first |
| PERSONALITY_BRAVE | 10000 | 150 | 3 | weapon first |
| PERSONALITY_CAUTIOUS | 10500 | 300 | 5 | armor/helmet first |
| PERSONALITY_COLLECTOR | 10000 | 250 | 4 | quality then upgrade |

장비 후보는 P07 `Score`; `gainBps=(candidate-current)*10000/max(1,current)`이다.
구매 기준은 `gainBps≥ceil(500*thresholdBps/10000)`이며 구매 후 balance가 reserve 이상이어야
한다. 동률은 gain desc, price asc, quality order desc, instanceId asc다. 포션이 장비보다
먼저지만 reserve 때문에 장비 저축이 유지된다. 같은 stock 1개 contention은 앞선
mercenary가 이기고 다음 mercenary는 1회 re-query 후 no-op한다.

## 10. command/query DTO·hash·idempotency

모든 JSON property 순서는 아래 DTO 표 순서다. hash는 `requestHash`를 제외한 request를
RFC 8785 canonicalize한 SHA-256 lowercase hex다.

| CommandType | InputDTO order | Preconditions | OrderedMutation | OutputDTO order | Revision | Journal | Idempotency |
|---|---|---|---|---|---:|---:|---|
| SET_PRICING_POLICY | commandType,operationId,expectedRevision,policyId | OPEN,level≥2 | policy→ledger→journal | operationId,revisionBefore,revisionAfter,policyId,replayed,resultDigest | +1 | 1+ledger1 | operationId/hash |
| SELL_TO_STORE | commandType,operationId,expectedRevision,mercenaryInstanceId,lines | OPEN,town,eligible,capacity | remove inventory→add stock→credit personal→ledger/journal | operationId,revisions,mercenaryInstanceId,totalPersonalGoldDelta,stockVersionAfter,lines,replayed,resultDigest | +1 | 1+1 | same |
| BUY_FROM_STORE | commandType,operationId,expectedRevision,quoteContextHash,mercenaryInstanceId,equipIfUpgrade,lines | OPEN,quote,stock,gold,capacity | debit personal→credit kingdom→move stock→optional equip→ledger/journal | operationId,revisions,mercenaryInstanceId,totalPersonalGoldDelta,totalKingdomGoldDelta,stockVersionAfter,lines,replayed,resultDigest | +1 | 1+1 | same |
| REFRESH_SYSTEM_STORE_SUPPLY | commandType,operationId,expectedRevision,targetEpoch | OPEN,mode,epoch,target | fill→epoch/counter→ledger/journal | operationId,revisions,epoch,addedLines,stockVersionAfter,replayed,resultDigest | +1 iff add | 1+1 | epoch+operation |

`lines` max는 sell 32, buy 16. line order is `(kind,id)`; equipment id is instanceId.
어느 한 line이 실패해도 mutation 0이다. `SELL_INVENTORY` 신규 호출은
`P08_LEGACY_COMMAND_RETIRED`; 과거 committed operationId는 P07 journal result로 replay한다.
manual sale recipient는 town mercenary 하나다. equipment 구매는 `equipIfUpgrade=true`일
때만 즉시 장착, 아니면 inventory 보관이다. potion은 선택 mercenary potion stack으로 간다.

```text
SellLine = {kind: ITEM|EQUIPMENT, id: itemId|equipmentInstanceId, quantity: 1..9999}
BuyLine  = {kind: POTION|EQUIPMENT, id: productId|equipmentInstanceId,
            quantity: 1..9999, quotedUnitPrice: 1..MAX_SAFE_INT}
```

equipment line quantity는 항상 1이다. 같은 `(kind,id)` 중복 line은
`P08_QUANTITY_INVALID`이며 merge하지 않는다. `RUN_STORE_AUTONOMY_CYCLE`은 외부 command가
아니고 여러 P08 command를 순서대로 호출하는 application use case다.

Queries:

- `GetStorefront(StorefrontQuery{mercenaryInstanceId,category,sort,pageSize,pageToken})`
- `GetStoreQuote(StoreQuoteQuery{direction,mercenaryInstanceId,lines})`
- `GetStoreTransactionHistory(HistoryQuery{beforeSequence,limit})`, limit 1..50.

Quote DTO order:
`quoteId,createdAtUtc,expiresAtUtc,contentVersion,saveRevision,stockVersion,policyId,
facilityStateHash,mercenaryInstanceId,direction,lines,total,quoteContextHash`.
expiry는 trusted UTC +30초다. quote는 Save에 쓰지 않는다. replay 검사→hash mismatch→
revision→quote context→domain guard 순서다. double tap은 Presenter in-flight flag와
operationId 재사용으로 차단한다. undo는 없다. 운영 보정은 반대 방향의 새 command다.

## 11. transaction journal·ledger·crash 원자성

1. JSON parse·schema·constant-time requestHash 검증
2. operation journal replay/mismatch
3. expectedRevision·contentVersion·quote·facility/NPC
4. wallet·stock·inventory·ownership·capacity
5. immutable price/policy snapshot
6. deep clone ordered mutation
7. cross-object semantic validation
8. result JCS와 digest
9. ledger append·prune, operation journal append
10. revision +1, savedAtUtc, integrity 재계산
11. temp write→fsync→atomic replace→backup rotation
12. commit 후 event/UI 발행

`operationType=STORE_TRANSACTION`; journal은 기존 fields에 `resultPayload`를 추가하며
STORE_TRANSACTION에서만 required다. ledger는 query projection이자 reconciliation
증거다. 201번째 append 전에 가장 오래된 entry를 제거하고
`prunedDigest=SHA256(previousPrunedDigestOr64Zero || JCS(removedEntry))`,
`prunedThroughSequence=removed.sequence`로 갱신한다. operation journal의 resultPayload는
prune하지 않아 오래된 idempotent replay도 가능하다.

crash가 step 11 전이면 old Save, atomic replace 완료 후면 new Save다. post-commit
pre-event crash는 reload 시 UI가 ledger를 읽으며 재-mutation하지 않는다. journal과
ledger operationId/resultDigest 불일치는 backup recovery다.

## 12. P07→P08 migration·schema·golden

Migration ID `MIGRATE_CONTENT_5_TO_6`, operationType `SAVE_RECOVERY`. 입력의 wallet,
pricingPolicy, inventory, facilities, NPC, P07 journal은 byte 의미를 보존한다.

```json
{
  "economyVersion":1,
  "store":{
    "stockVersion":0,
    "stackLines":[],
    "equipment":[],
    "supplyState":{"mode":"SYSTEM_SUPPLY","epoch":0,"buyCountSinceRefresh":0,"lastRefreshOperationId":null},
    "ledger":{"nextSequence":1,"prunedThroughSequence":0,"prunedDigest":null,"entries":[]}
  }
}
```

Migration은 `contentVersion`을 `.6`, gameVersion을 `1.0.0-p08`, revision을 정확히
+1로 바꾸고 위 economy를 추가한다. FAC_STORE가 BUILDABLE/ACTIVE/STOPPED 어느 상태든
동일 초기값이다. ACTIVE+working merchant의 최초 supply는 migration 뒤 별도 command다.
중단·전원 종료 재시도는 원본 `.5` 또는 완성 `.6`만 관찰한다. downgrade는
`SAVE_CONTENT_DOWNGRADE_UNSUPPORTED`다. corrupt wallet/stock/ledger는 자동 수선하지
않고 backup 1→2→3 순서로 복구한다.

Normative golden fixture IDs:

| Fixture | saveId | profileId | operationId | beforeRev | afterRev |
|---|---|---|---|---:|---:|
| P08_NEW_GAME_GOLDEN | `018f0000-0000-7000-8000-000000000801` | `018f0000-0000-7000-8000-000000000802` | 없음 | 1 | 1 |
| P07_TO_P08_BUILDABLE | `018f0000-0000-7000-8000-000000000811` | `018f0000-0000-7000-8000-000000000812` | `018f0000-0000-7000-8000-000000000813` | 7 | 8 |
| P07_TO_P08_ACTIVE | `018f0000-0000-7000-8000-000000000821` | `018f0000-0000-7000-8000-000000000822` | `018f0000-0000-7000-8000-000000000823` | 11 | 12 |
| P07_TO_P08_STOPPED | `018f0000-0000-7000-8000-000000000831` | `018f0000-0000-7000-8000-000000000832` | `018f0000-0000-7000-8000-000000000833` | 21 | 22 |

전체 golden은 generator가 P07 tracked template을 읽어 위 exact IDs/time
`2026-07-23T00:00:00.000Z`를 주입하고 schema composition과 함께 생성한다. Hash guard는
source `.5` schema SHA와 P07 template SHA가 다르면 생성 전에 실패한다.

## 13. P08 canonical content package 전체 계약

`contentVersion=1.0.0-content.6`, `packageKind=BASE`, `baseContentVersion=null`,
`contractVersion=2`, `csvSchemaSetVersion=4`, `minimumGameVersion=1.0.0-p08`,
`generatedAtUtc=2026-07-23T00:00:00.000Z`. `.5`는 동시에 보존한다.

새 table 7개를 추가하여 총 75개다. `.5`의 63개 unchanged CSV는 exact bytes와 hash를
복사한다. 5개는 append/enum correction(currencies, autonomy_rules, runtime_config,
asset_register, localizations)이며 원본 파일을 in-place 수정하지 않고 `.6` copy에서만
변경한다.

### 13.1 production rows

`pricing_policies.csv`:

```csv
policy_id,multiplier_bps,min_store_level,name_text_key,effect_text_key,status,enabled
LOW,9000,2,TXT_P08_POLICY_LOW,TXT_P08_POLICY_LOW_EFFECT,TUNABLE,TRUE
STANDARD,10000,1,TXT_P08_POLICY_STANDARD,TXT_P08_POLICY_STANDARD_EFFECT,TUNABLE,TRUE
HIGH,11500,2,TXT_P08_POLICY_HIGH,TXT_P08_POLICY_HIGH_EFFECT,TUNABLE,TRUE
```

`store_level_rules.csv`:

```csv
store_level,max_tier,max_quality_order,boss_allowed,policy_change_allowed,max_stack_lines,max_equipment_lines,status,enabled
1,1,2,FALSE,FALSE,32,12,TUNABLE,TRUE
2,2,3,FALSE,TRUE,48,24,TUNABLE,TRUE
3,4,4,FALSE,TRUE,64,48,TUNABLE,TRUE
4,5,5,TRUE,TRUE,96,96,TUNABLE,TRUE
```

`store_assortment.csv`:

```csv
assortment_id,product_kind,product_id,min_store_level,customer_purchase_allowed,merchant_acquire_allowed,status,enabled
ASSORT_POT_HEAL_SMALL,POTION,POT_HEAL_SMALL,1,TRUE,FALSE,TUNABLE,TRUE
ASSORT_T1_WARRIOR,EQUIPMENT,EQ_T1_WARRIOR_WEAPON,1,TRUE,TRUE,TUNABLE,TRUE
ASSORT_T1_GUARDIAN,EQUIPMENT,EQ_T1_GUARDIAN_WEAPON,1,TRUE,TRUE,TUNABLE,TRUE
ASSORT_T1_ARCHER,EQUIPMENT,EQ_T1_ARCHER_WEAPON,1,TRUE,TRUE,TUNABLE,TRUE
ASSORT_T1_MAGE,EQUIPMENT,EQ_T1_MAGE_WEAPON,1,TRUE,TRUE,TUNABLE,TRUE
ASSORT_T1_CLERIC,EQUIPMENT,EQ_T1_CLERIC_WEAPON,1,TRUE,TRUE,TUNABLE,TRUE
ASSORT_ALL_ITEMS,ITEM,*,1,FALSE,TRUE,TUNABLE,TRUE
ASSORT_ALL_EQUIPMENT,EQUIPMENT,*,1,TRUE,TRUE,TUNABLE,TRUE
```

Wildcard `*`는 `SOFT_SENTINEL_WILDCARD`이며 specific row가 우선한다.

`store_supply_rules.csv`:

```csv
supply_rule_id,product_kind,product_id,target_quantity,min_store_level,refresh_buy_count,source_type,status,enabled
SUP_SMALL_HEAL,POTION,POT_HEAL_SMALL,8,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
SUP_WARRIOR,EQUIPMENT,EQ_T1_WARRIOR_WEAPON,1,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
SUP_GUARDIAN,EQUIPMENT,EQ_T1_GUARDIAN_WEAPON,1,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
SUP_ARCHER,EQUIPMENT,EQ_T1_ARCHER_WEAPON,1,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
SUP_MAGE,EQUIPMENT,EQ_T1_MAGE_WEAPON,1,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
SUP_CLERIC,EQUIPMENT,EQ_T1_CLERIC_WEAPON,1,1,8,SYSTEM_SUPPLY,TUNABLE,TRUE
```

`store_price_rules.csv`:

```csv
price_rule_id,product_kind,product_id,acquire_rate_bps,customer_base_price,customer_markup_bps,min_price,status,enabled
PRICE_ITEM_WILDCARD,ITEM,*,10000,0,20000,1,TUNABLE,TRUE
PRICE_EQUIPMENT_WILDCARD,EQUIPMENT,*,2500,0,10000,1,TUNABLE,TRUE
PRICE_POT_HEAL_SMALL,POTION,POT_HEAL_SMALL,2500,40,10000,1,TUNABLE,TRUE
PRICE_POT_HEAL_MEDIUM,POTION,POT_HEAL_MEDIUM,2500,120,10000,1,TUNABLE,TRUE
PRICE_POT_HEAL_LARGE,POTION,POT_HEAL_LARGE,2500,300,10000,1,TUNABLE,TRUE
PRICE_POT_ANTIDOTE,POTION,POT_ANTIDOTE,2500,90,10000,1,TUNABLE,TRUE
PRICE_POT_POISON_RESIST,POTION,POT_POISON_RESIST,2500,240,10000,1,TUNABLE,TRUE
PRICE_POT_FROST_RESIST,POTION,POT_FROST_RESIST,2500,300,10000,1,TUNABLE,TRUE
PRICE_POT_RAID_POWER,POTION,POT_RAID_POWER,2500,600,10000,1,TUNABLE,TRUE
```

`store_purchase_ai_rules.csv`:

```csv
personality_id,threshold_bps,reserve_gold,potion_target,potion_max_per_cycle,equipment_max_per_cycle,equipment_priority,status,enabled
PERSONALITY_PRACTICAL,10000,200,4,2,1,BALANCED,TUNABLE,TRUE
PERSONALITY_FRUGAL,12000,400,3,1,1,PRICE,TUNABLE,TRUE
PERSONALITY_GEARHEAD,8200,150,3,1,1,UPGRADE,TUNABLE,TRUE
PERSONALITY_BRAVE,10000,150,3,2,1,WEAPON,TUNABLE,TRUE
PERSONALITY_CAUTIOUS,10500,300,5,2,1,DEFENSE,TUNABLE,TRUE
PERSONALITY_COLLECTOR,10000,250,4,1,1,QUALITY,TUNABLE,TRUE
```

`transaction_reason_codes.csv`:

```csv
reason_code,transaction_type,user_visible,text_key,status,enabled
MERCENARY_STORE_SALE,SELL_TO_STORE,TRUE,TXT_P08_REASON_SALE,CONFIRMED,TRUE
MERCENARY_STORE_PURCHASE,BUY_FROM_STORE,TRUE,TXT_P08_REASON_PURCHASE,CONFIRMED,TRUE
STORE_POLICY_CHANGE,SET_PRICING_POLICY,TRUE,TXT_P08_REASON_POLICY,CONFIRMED,TRUE
SYSTEM_STORE_SUPPLY,SYSTEM_SUPPLY,FALSE,TXT_P08_REASON_SUPPLY,CONFIRMED,TRUE
AUTO_SELL_ELIGIBLE,SELL_TO_STORE,TRUE,TXT_P08_REASON_AUTO_SELL,CONFIRMED,TRUE
POTION_TARGET_LOW,BUY_FROM_STORE,TRUE,TXT_P08_REASON_POTION_LOW,CONFIRMED,TRUE
EQUIPMENT_UPGRADE,BUY_FROM_STORE,TRUE,TXT_P08_REASON_EQUIPMENT,CONFIRMED,TRUE
RETURNED_TO_STORE,AUTONOMY,TRUE,TXT_P08_REASON_RETURNED,CONFIRMED,TRUE
HEAL_DEFERRED_P09,AUTONOMY,TRUE,TXT_P08_REASON_HEAL_DEFERRED,CONFIRMED,TRUE
NO_ELIGIBLE_TRANSACTION,AUTONOMY,FALSE,TXT_P08_REASON_NONE,CONFIRMED,TRUE
```

Append contracts:

- currencies는 아래 1행을 append하고 manifest `currency_type` enum에 `PERSONAL`을 추가한다.

```csv
currency_id,currency_type,authority,max_balance,name_text_key,status,enabled
PERSONAL_GOLD,PERSONAL,LOCAL,9007199254740991,TXT_CURRENCY_PERSONAL_GOLD_NAME,CONFIRMED,TRUE
```

- autonomy는 기존 동일 PK row를 아래 bytes로 replace한다. `HEAL` 두 row는 P09까지
  `enabled=FALSE`이며 P08 runtime fallback이 `IDLE_TOWN`을 선택한다.

```csv
state,rule_no,priority,condition_type,condition_value,reason_code,next_state,status,enabled
RETURN_TOWN,1,0,ARRIVED_TOWN,,RETURNED_TO_STORE,SELL_LOOT,CONFIRMED,TRUE
SELL_LOOT,1,100,HAS_INJURY,,HP_LOW,BUY_CONSUMABLES,TUNABLE,TRUE
SELL_LOOT,2,90,HAS_SELLABLE_INVENTORY,,AUTO_SELL_ELIGIBLE,BUY_CONSUMABLES,TUNABLE,TRUE
SELL_LOOT,3,0,ALWAYS,,NO_ELIGIBLE_TRANSACTION,BUY_CONSUMABLES,TUNABLE,TRUE
BUY_CONSUMABLES,1,0,PURCHASE_DECISION_COMPLETE,,POTION_TARGET_LOW,EVALUATE_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,1,100,UPGRADE_PURCHASE_AVAILABLE,500,EQUIPMENT_UPGRADE,BUY_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,3,0,ALWAYS,,NO_ELIGIBLE_TRANSACTION,IDLE_TOWN,TUNABLE,TRUE
BUY_EQUIPMENT,1,0,PURCHASE_DECISION_COMPLETE,,EQUIPMENT_UPGRADE,IDLE_TOWN,TUNABLE,TRUE
HEAL,1,100,INJURY_CLEARED,,HEAL_DEFERRED_P09,BUY_CONSUMABLES,DEFERRED,FALSE
HEAL,2,0,ALWAYS,,HEAL_DEFERRED_P09,IDLE_TOWN,DEFERRED,FALSE
```

- runtime_config는 아래 11행을 append한다.

```csv
config_key,value_type,value,unit,min_value,max_value,description_text_key,status,enabled
P08_EQUIPMENT_ACQUIRE_BPS,INTEGER,2500,BPS,1,10000,TXT_P08_STORE_TITLE,TUNABLE,TRUE
P08_ITEM_CUSTOMER_MARKUP_BPS,INTEGER,20000,BPS,10000,50000,TXT_P08_STORE_TITLE,TUNABLE,TRUE
P08_LEDGER_MAX_ENTRIES,INTEGER,200,COUNT,50,500,TXT_P08_RECENT_ACTIVITY,TUNABLE,TRUE
P08_MAX_AUTONOMY_COMMANDS,INTEGER,48,COUNT,1,48,TXT_P08_REASON_NONE,TUNABLE,TRUE
P08_MAX_BUY_LINES,INTEGER,16,COUNT,1,16,TXT_P08_BUY,TUNABLE,TRUE
P08_MAX_LIVE_PRODUCT_VIEWS,INTEGER,24,COUNT,12,40,TXT_P08_STORE_TITLE,TUNABLE,TRUE
P08_MAX_SELL_LINES,INTEGER,32,COUNT,1,32,TXT_P08_SELL,TUNABLE,TRUE
P08_POTION_BASE_SMALL,INTEGER,40,GOLD,1,10000,TXT_POT_HEAL_SMALL_NAME,TUNABLE,TRUE
P08_QUOTE_TTL_SECONDS,INTEGER,30,SECONDS,5,120,TXT_P08_CONFIRM_TITLE,TUNABLE,TRUE
P08_STORE_TIMEOUT_SECONDS,INTEGER,10,SECONDS,1,30,TXT_P08_STATE_ERROR,TUNABLE,TRUE
P08_SUPPLY_REFRESH_BUY_COUNT,INTEGER,8,COUNT,1,100,TXT_P08_SYSTEM_SUPPLY,TUNABLE,TRUE
```

- asset_register와 localizations의 exact P08 rows는 §17·§18이 production bytes다.

### 13.2 validator와 fingerprint

| File | Rows | PK | 주요 FK/check | Owner |
|---|---:|---|---|---|
| pricing_policies | 3 | policy_id | multiplier 1..20000, level | PricingCatalog |
| store_level_rules | 4 | store_level | contiguous 1..4, nondecreasing caps | AvailabilityCatalog |
| store_assortment | 8 | assortment_id | product FK or wildcard | AssortmentCatalog |
| store_supply_rules | 6 | supply_rule_id | assortment/product FK | SupplyCatalog |
| store_price_rules | 9 | price_rule_id | product FK/wildcard, positive | PricingCatalog |
| store_purchase_ai_rules | 6 | personality_id | hard FK, exact coverage | PurchaseAiCatalog |
| transaction_reason_codes | 10 | reason_code | enum transaction type | ReasonCatalog |

Normative CRLF bytes/hash:

| Block | Rows | Bytes | SHA-256 |
|---|---:|---:|---|
| pricing_policies.csv | 3 | 317 | `89620f770978d8c64bce89166dc93fd0b95bb9b40074486d16edb6b0d87833b6` |
| store_level_rules.csv | 4 | 274 | `6192e8a57d03f1edff2c92bb1c8414a68e2519492cdc020d18243aae2375b647` |
| store_assortment.csv | 8 | 669 | `e2ede698f476991060936022a8442be85d28d24c95f3775dcba18fe8cfc40eba` |
| store_supply_rules.csv | 6 | 565 | `d1eb2569792abfcd906ea5025f78c1185c96ef2c31e424670971df2eec4d2505` |
| store_price_rules.csv | 9 | 768 | `681d85371072c7e58c16b57932a5e30a52ea2bbe749709b504911b5644c1d7c2` |
| store_purchase_ai_rules.csv | 6 | 484 | `0a5411e3c6da292cb3e56a3d5e5f6c5b5a4de93f4e5e194d5ee45764d03df425` |
| transaction_reason_codes.csv | 10 | 850 | `18425255be026833cde4e6e6bd461c6801bae496e09c25d05ba16b92c20967dd` |
| asset_register append rows | 12 | 2058 | `c0558f65ccd6f9696cb591f9ef5b12e6727e923de9bf2f4588a9d34b41ea3b9d` |
| localization P08 bundle | 98 | 8547 | `0251daf7eb8ceab763ae1384949aafcb838a7c0d2fdcc3d16b4a3e9d73fe41b7` |

CSV는 UTF-8 no BOM, CRLF, RFC4180 minimal quoting, final CRLF, PK ordinal sort다.
Generator는 각 fence를 production rows로 사용해 실제 SHA-256을 계산하고 manifest에
삽입한다. hash를 코드에 수기 복제하지 않는다. package fingerprint는
`SHA256(content_manifest.json bytes || save.content.6.schema.json bytes)`다. 설계 채택
커밋에서 generator `--check`가 75 tables, row count, bytes, manifest/localization/schema
hash를 출력하며 그 값을 P08 구현 보고서에 고정한다. 이 규칙은 가짜 사전 hash보다
재현 가능한 bytes를 권위로 삼는 `CORRECTION`이다.

## 14. P09 생산·NPC 숙련 handoff

| P08Seam | P08ExactBehavior | P09ExtensionPoint | ForbiddenP08Mutation | MigrationNeed | CompatibilityTest |
|---|---|---|---|---|---|
| `IStoreStockSink.AddStack/AddEquipment` | source와 operation 필수 | production output 호출 | recipe·queue 생성 | 없음 | source merge |
| SupplyState.mode | SYSTEM_SUPPLY | PRODUCTION_OWNED | P08 임의 전환 | P09 migration | no new system stock |
| merchant proficiency | 표시만, 10000bps | price/speed modifier adapter | XP 증가 | content.7 | XP delta 0 |
| stock target | system target read-only | player production target | production enqueue | economy 확장 | 목표 충돌 없음 |
| stop events | StoreStopped | FacilityProductionStopped 별도 | 생산 stop 발행 | 없음 | event type 분리 |
| ledger | P08 rows 유지 | production acquisition reason 추가 | 기존 row rewrite | optional enum extend | history round trip |

## 15. Store UI·Drawer·navigation hierarchy·좌표

상점은 하단 navigation을 늘리지 않는다. Kingdom의 `FAC_STORE`를 선택하면 기존
facility Drawer가 열리고 `P08_STORE_OPEN_BUTTON`으로 `StoreScreen` full-screen overlay를
연다. 닫으면 같은 Kingdom camera·Drawer selection으로 돌아간다.

기준 좌표는 SafeArea 내부 1920×1080이다. anchor/offset은 Unity RectTransform 값이며
offset 표기는 `(xMin,yMin)/(xMax,yMax)`다.

| ElementId | ParentId | AnchorMin | AnchorMax | OffsetMin/Max | MinTouch | VisibleWhen | InteractableWhen | LocalizationKey | DataSourceDTO |
|---|---|---|---|---|---:|---|---|---|---|
| P08_STORE_OPEN_BUTTON | FAC_STORE_DRAWER | 0,0 | 1,0 | 24,24/-24,88 | 64 | content.6 | facility selectable | TXT_P08_STORE_TITLE | StoreAvailabilityDto |
| P08_STORE_SCREEN | ScreenCanvas | 0,0 | 1,1 | 0,0/0,0 | - | open | - | TXT_P08_STORE_TITLE | StorefrontDto |
| P08_STORE_HEADER | SCREEN | 0,1 | 1,1 | 32,-120/-32,-24 | - | all | - | TXT_P08_STORE_TITLE | StorefrontDto |
| P08_STORE_BACK | HEADER | 0,0.5 | 0,0.5 | 0,-32/64,32 | 64 | all | !busy | TXT_COMMON_BACK | none |
| P08_KINGDOM_GOLD | HEADER | 0.64,0 | 0.82,1 | 0,0/0,0 | - | all | - | TXT_CURRENCY_KINGDOM_GOLD_NAME | WalletDto |
| P08_MERC_SELECTOR | HEADER | 0.82,0 | 1,1 | 8,0/0,0 | 64 | Content | !busy | TXT_P08_SELECT_MERCENARY | MercenaryOptionDto[] |
| P08_CATEGORY_TABS | SCREEN | 0,0 | 0,1 | 32,96/216,-144 | - | Content/Empty | !busy | - | CategoryDto[] |
| P08_TAB_BUY | TABS | 0,1 | 1,1 | 0,-72/0,0 | 64 | Content | !busy | TXT_P08_TAB_BUY | - |
| P08_TAB_SELL | TABS | 0,1 | 1,1 | 0,-152/0,-80 | 64 | Content | !busy | TXT_P08_TAB_SELL | - |
| P08_TAB_HISTORY | TABS | 0,1 | 1,1 | 0,-232/0,-160 | 64 | Content | !busy | TXT_P08_TAB_HISTORY | - |
| P08_FILTER | TABS | 0,0 | 1,0 | 0,168/0,232 | 64 | product tabs | !busy | TXT_P08_FILTER | FilterDto |
| P08_SORT | TABS | 0,0 | 1,0 | 0,96/0,160 | 64 | product tabs | !busy | TXT_P08_SORT | SortDto |
| P08_PRODUCT_VIEWPORT | SCREEN | 0,0 | 0.58,1 | 232,96/-16,-144 | - | Content | scroll | - | ProductRowDto[] |
| P08_PRODUCT_LIST | VIEWPORT | 0,0 | 1,1 | 0,0/0,0 | - | Content | scroll | - | ProductRowDto[] |
| P08_PRODUCT_ROW_POOL | LIST | 0,1 | 1,1 | 0,-96/0,0 | 64 | row bound | !busy | - | ProductRowDto |
| P08_DETAIL_PANEL | SCREEN | 0.58,0 | 1,1 | 16,96/-32,-144 | - | selected | - | TXT_P08_PRODUCT_DETAIL | ProductDetailDto |
| P08_COMPARE_PANEL | DETAIL | 0,0.34 | 1,0.76 | 16,0/-16,0 | - | equipment | - | TXT_P08_COMPARE | EquipmentCompareDto |
| P08_QUANTITY_MINUS | DETAIL | 0,0 | 0,0 | 16,20/80,84 | 64 | stack product | qty>1&&!busy | TXT_P08_DECREASE | QuoteDraftDto |
| P08_QUANTITY_PLUS | DETAIL | 0,0 | 0,0 | 88,20/152,84 | 64 | stack product | below max&&!busy | TXT_P08_INCREASE | QuoteDraftDto |
| P08_PRIMARY_ACTION | DETAIL | 1,0 | 1,0 | -232,20/-16,84 | 216×64 | selected | valid&&!busy | dynamic | QuoteDraftDto |
| P08_POLICY_CONTROL | SCREEN | 0.58,0 | 1,0 | 16,16/-32,80 | 64 | level≥2 | OPEN&&!busy | TXT_P08_POLICY | PricingPolicyDto |
| P08_ACTIVITY_STRIP | SCREEN | 0,0 | 1,0 | 232,16/-480,80 | - | Content | - | TXT_P08_RECENT_ACTIVITY | LedgerSummaryDto[] |
| P08_STATE_PANEL | SCREEN | 0,0 | 1,1 | 320,220/-320,-220 | - | !Content | - | dynamic | StoreStateDto |
| P08_CONFIRM_MODAL | ModalCanvas | 0.5,0.5 | 0.5,0.5 | -360,-250/360,250 | - | confirming | - | TXT_P08_CONFIRM_TITLE | QuoteDto |
| P08_CONFIRM | MODAL | 1,0 | 1,0 | -232,24/-24,88 | 208×64 | confirming | quote valid&&!busy | TXT_COMMON_CONFIRM | QuoteDto |
| P08_CANCEL | MODAL | 0,0 | 0,0 | 24,24/232,88 | 208×64 | confirming | !busy | TXT_COMMON_CANCEL | - |
| P08_RESULT_TOAST | ToastCanvas | 0.5,0 | 0.5,0 | -360,48/360,128 | - | 2.5 sec | - | dynamic | TransactionResultDto |
| P08_DEBUG_PANEL | DebugCanvas | 0,0 | 0.38,0.42 | 16,16/-16,-16 | - | DEVELOPMENT_BUILD | - | - | EconomyDiagnosticDto |

Product list는 24개 pool로 100 line을 virtualize한다. row 높이는 96px, overscan 3,
scroll 중 steady-state allocation 0B/frame이다. 판매 multi-select는 최대 32 line이며
보호 항목은 선택되지 않고 정확한 사유를 표시한다. filter/sort는 Save를 변경하지 않는다.

상호작용 순서: product 선택→detail→수량→quote→단일 confirm modal→command→result→
event-driven refresh. command 중 모든 mutation control을 disable한다. 10초 timeout은
로컬 command hang을 Error로 표시하되 operationId를 유지해 retry한다. Android Back은
modal cancel→Store close→기존 Drawer close 순서다. focus 순서는 back, wallet, merc,
tabs, filter, sort, rows, detail controls, primary action이다. 큰 글자 1.25×에서 row는
112px, header 132px로 커지며 ellipsis 대신 최대 2행을 사용한다.

## 16. Loading/Content/Empty/Error/Locked/Offline 6상태

| UiState | EntryCondition | StorefrontVisibility | PrimaryAction | SecondaryAction | DisabledReasons | LocalizationKey | AnalyticsEvent | SaveMutationAllowed |
|---|---|---|---|---|---|---|---|---|
| Loading | Save/catalog/query 미완료 | skeleton | 없음 | Back | LOADING | TXT_P08_STATE_LOADING | STORE_STATE_LOADING | N |
| Content | schema valid, facility projection 존재 | full | 거래/정책 | filter/sort | per-control | TXT_P08_STATE_CONTENT | STORE_STATE_CONTENT | command만 Y |
| Empty | 현재 tab/filter 결과 0 | header+state | filter reset | history | NO_STOCK/NO_SELLABLE | TXT_P08_STATE_EMPTY | STORE_STATE_EMPTY | supply internal만 |
| Error | schema/catalog/journal/query/timeout | state panel | Retry | Back | ERROR_CODE | TXT_P08_STATE_ERROR | STORE_STATE_ERROR | N |
| Locked | LOCKED/BUILDABLE/BUILDING | header+condition | 시설로 이동 | Back | BUILD_REQUIRED | TXT_P08_STATE_LOCKED | STORE_STATE_LOCKED | P04 command only |
| Offline | P16 adapter unavailable이나 local authority 정상 | full+offline badge | local 거래 | Back | NETWORK_NOT_REQUIRED | TXT_P08_STATE_OFFLINE | STORE_STATE_OFFLINE | Y |

merchant 없음·STOPPED·UPGRADING은 `Content-disabled`이며 state enum은 Content다.
stock과 history를 보여주되 거래 버튼과 policy를 disable하고 stop reason·NPC 배치 행동을
제공한다. Error 화면은 Save를 수선하지 않는다. DebugCanvas는 quote hash, AI trace,
stockVersion, ledger reconcile만 표시하고 non-development build에서 object 자체를 만들지 않는다.

## 17. placeholder·Addressables·asset register

P08은 프로젝트 내부 생성 vector/pixel placeholder만 사용하며 구매비 0원이다. 독자적인
좌측 category rail+중앙 상품+우측 detail layout을 사용한다. 목재 `#3A271D`, 석재
`#4A4A46`, 불빛 `#E7A74C`, 보상 `#F2D47A`, 오류 `#D86B62`, 품절 `#807A72`다.

| assetId | address | group | label | role | dimensions | pivot | PPU | source/license | commercialUse | replacementContract |
|---|---|---|---|---|---|---|---:|---|---|---|
| ASSET_P08_STORE_PANEL | P08/Store/Panel | Content-P08-Store-v1 | P08_STORE | panel 9-slice | 96×96 | .5,.5 | 100 | INTERNAL/Project | TRUE | address·border 유지 |
| ASSET_P08_GOLD_PERSONAL | P08/Currency/PersonalGold | same | P08_CURRENCY | coin icon | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_GOLD_KINGDOM | P08/Currency/KingdomGold | same | P08_CURRENCY | crown coin | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_STOCK | P08/State/Stock | same | P08_STATE | crate | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_SOLD_OUT | P08/State/SoldOut | same | P08_STATE | empty crate | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_POLICY_LOW | P08/Policy/LOW | same | P08_POLICY | green tag | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_POLICY_STANDARD | P08/Policy/STANDARD | same | P08_POLICY | gold tag | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_POLICY_HIGH | P08/Policy/HIGH | same | P08_POLICY | red tag | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_TRANSACTION_IN | P08/Transaction/In | same | P08_TRANSACTION | acquire arrow | 48×48 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_TRANSACTION_OUT | P08/Transaction/Out | same | P08_TRANSACTION | purchase arrow | 48×48 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_MERCHANT | P08/Merchant/Portrait | same | P08_MERCHANT | portrait | 128×128 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |
| ASSET_P08_STATE_ERROR | P08/State/Error | same | P08_STATE | warning | 64×64 | .5,.5 | 100 | INTERNAL/Project | TRUE | ID 유지 |

Sprite import: point filter, no compression for UI reference, Sprite(2D and UI), mipmap off.
missing asset는 `ASSET_P08_STATE_ERROR` fallback과 `P08_CONTENT_MISSING`; 거래 mutation은
허용하지 않는다. panel open 220ms easeOutCubic, coin delta 260ms easeOutQuad, toast 180ms
fade. reduce-motion은 duration 0이고 text delta를 반드시 읽는다. P15/P17 교체는 address,
sprite border, semantic ID를 유지하며 Domain·Save를 바꾸지 않는다.

`asset_register.csv` 추가 production rows:

```csv
ASSET_P08_GOLD_KINGDOM,P08 Kingdom Gold,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Currency/KingdomGold,CONFIRMED,TRUE
ASSET_P08_GOLD_PERSONAL,P08 Personal Gold,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Currency/PersonalGold,CONFIRMED,TRUE
ASSET_P08_MERCHANT,P08 Merchant Portrait,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Merchant/Portrait,CONFIRMED,TRUE
ASSET_P08_POLICY_HIGH,P08 Policy High,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Policy/HIGH,CONFIRMED,TRUE
ASSET_P08_POLICY_LOW,P08 Policy Low,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Policy/LOW,CONFIRMED,TRUE
ASSET_P08_POLICY_STANDARD,P08 Policy Standard,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Policy/STANDARD,CONFIRMED,TRUE
ASSET_P08_SOLD_OUT,P08 Sold Out,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/State/SoldOut,CONFIRMED,TRUE
ASSET_P08_STATE_ERROR,P08 Error,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/State/Error,CONFIRMED,TRUE
ASSET_P08_STOCK,P08 Stock,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/State/Stock,CONFIRMED,TRUE
ASSET_P08_STORE_PANEL,P08 Store Panel,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Store/Panel,CONFIRMED,TRUE
ASSET_P08_TRANSACTION_IN,P08 Transaction In,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Transaction/In,CONFIRMED,TRUE
ASSET_P08_TRANSACTION_OUT,P08 Transaction Out,KingdomTycoon Editor Generator,,1,2026-07-23,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P08 Store,address=P08/Transaction/Out,CONFIRMED,TRUE
```

## 18. ko-KR/en-US localization 전체 rows

placeholder 문법은 ICU-style `{name}`이며 포맷은 UI boundary에서 한다. 금액은 locale
group separator, 소수 없음. 한글 조사는 문장에 명사를 직접 삽입하지 않고 조사 없는
형태로 작성한다. en-US count는 `{count, plural, one {...} other {...}}`를 사용한다.

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_CURRENCY_PERSONAL_GOLD_NAME,Personal Gold,CURRENCY,CONFIRMED,TRUE
en-US,TXT_P08_BUY,Buy,STORE_ACTION,CONFIRMED,TRUE
en-US,TXT_P08_COMPARE,Compare Equipment,STORE_LABEL,CONFIRMED,TRUE
en-US,TXT_P08_CONFIRM_TITLE,Confirm Transaction,STORE_MODAL,CONFIRMED,TRUE
en-US,TXT_P08_DECREASE,Decrease Quantity,ACCESSIBILITY,CONFIRMED,TRUE
en-US,TXT_P08_FILTER,Filter,STORE_ACTION,CONFIRMED,TRUE
en-US,TXT_P08_INCREASE,Increase Quantity,ACCESSIBILITY,CONFIRMED,TRUE
en-US,TXT_P08_PERSONAL_GOLD_HELP,Gold carried and spent by this mercenary.,STORE_HELP,CONFIRMED,TRUE
en-US,TXT_P08_POLICY,Pricing Policy,STORE_LABEL,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_HIGH,High,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_HIGH_EFFECT,Prices 15% higher; purchases become less likely.,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_LOW,Low,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_LOW_EFFECT,Prices 10% lower; purchases become more likely.,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_STANDARD,Standard,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_POLICY_STANDARD_EFFECT,Standard prices and purchase behavior.,STORE_POLICY,CONFIRMED,TRUE
en-US,TXT_P08_PRODUCT_DETAIL,Product Details,STORE_LABEL,CONFIRMED,TRUE
en-US,TXT_P08_QUOTE_CHANGED,The price or stock changed. Review the new quote.,STORE_ERROR,CONFIRMED,TRUE
en-US,TXT_P08_REASON_AUTO_SELL,Automatically sold eligible loot.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_EQUIPMENT,Bought an equipment upgrade.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_HEAL_DEFERRED,Treatment becomes available with the infirmary phase.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_NONE,No eligible store action.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_POLICY,Changed the pricing policy.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_POTION_LOW,Bought potions to reach the target.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_PURCHASE,Bought goods from the kingdom store.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_RETURNED,Returned to town and visited the store.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_SALE,Sold goods to the kingdom store.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_REASON_SUPPLY,Emergency stock supplied.,STORE_ACTIVITY,CONFIRMED,TRUE
en-US,TXT_P08_RECENT_ACTIVITY,Recent Activity,STORE_LABEL,CONFIRMED,TRUE
en-US,TXT_P08_REPLAYED,The completed transaction was restored.,STORE_RESULT,CONFIRMED,TRUE
en-US,TXT_P08_SELECT_MERCENARY,Select Mercenary,STORE_ACTION,CONFIRMED,TRUE
en-US,TXT_P08_SELL,Sell,STORE_ACTION,CONFIRMED,TRUE
en-US,TXT_P08_SOLD_OUT,Sold Out,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_SORT,Sort,STORE_ACTION,CONFIRMED,TRUE
en-US,TXT_P08_STATE_CONTENT,Store is open.,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STATE_EMPTY,No products match this view. Change the filter or sell goods to the store.,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STATE_ERROR,The store data could not be verified. Retry or return.,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STATE_LOADING,Loading the store…,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STATE_LOCKED,Build the store and assign a merchant to begin trading.,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STATE_OFFLINE,Local trading is available. Server synchronization is not used yet.,STORE_STATE,CONFIRMED,TRUE
en-US,TXT_P08_STOCK,Stock: {count},STORE_LABEL,CONFIRMED,TRUE
en-US,TXT_P08_STORE_TITLE,Kingdom Store,STORE_TITLE,CONFIRMED,TRUE
en-US,TXT_P08_SYSTEM_SUPPLY,System Supply,STORE_BADGE,CONFIRMED,TRUE
en-US,TXT_P08_TAB_BUY,Buy,STORE_TAB,CONFIRMED,TRUE
en-US,TXT_P08_TAB_HISTORY,History,STORE_TAB,CONFIRMED,TRUE
en-US,TXT_P08_TAB_SELL,Sell,STORE_TAB,CONFIRMED,TRUE
en-US,TXT_P08_TRANSACTION_FAILED,Transaction failed: {reason},STORE_RESULT,CONFIRMED,TRUE
en-US,TXT_P08_TRANSACTION_SUCCESS,Transaction complete. Personal {personalDelta}; Kingdom {kingdomDelta}.,STORE_RESULT,CONFIRMED,TRUE
en-US,TXT_P08_UPGRADE_REQUIRED,Store level {level} is required.,STORE_ERROR,CONFIRMED,TRUE
en-US,TXT_P08_WALLET_INSUFFICIENT,Not enough Personal Gold.,STORE_ERROR,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_PERSONAL_GOLD_NAME,개인 골드,CURRENCY,CONFIRMED,TRUE
ko-KR,TXT_P08_BUY,구매,STORE_ACTION,CONFIRMED,TRUE
ko-KR,TXT_P08_COMPARE,장비 비교,STORE_LABEL,CONFIRMED,TRUE
ko-KR,TXT_P08_CONFIRM_TITLE,거래 확인,STORE_MODAL,CONFIRMED,TRUE
ko-KR,TXT_P08_DECREASE,수량 줄이기,ACCESSIBILITY,CONFIRMED,TRUE
ko-KR,TXT_P08_FILTER,필터,STORE_ACTION,CONFIRMED,TRUE
ko-KR,TXT_P08_INCREASE,수량 늘리기,ACCESSIBILITY,CONFIRMED,TRUE
ko-KR,TXT_P08_PERSONAL_GOLD_HELP,이 용병이 보유하고 사용하는 골드입니다.,STORE_HELP,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY,가격 정책,STORE_LABEL,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_HIGH,고가,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_HIGH_EFFECT,가격이 15% 높아져 구매 가능성이 낮아집니다.,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_LOW,저가,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_LOW_EFFECT,가격이 10% 낮아져 구매 가능성이 높아집니다.,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_STANDARD,표준,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_POLICY_STANDARD_EFFECT,기준 가격과 구매 행동을 사용합니다.,STORE_POLICY,CONFIRMED,TRUE
ko-KR,TXT_P08_PRODUCT_DETAIL,상품 상세,STORE_LABEL,CONFIRMED,TRUE
ko-KR,TXT_P08_QUOTE_CHANGED,가격 또는 재고가 바뀌었습니다. 새 견적을 확인하세요.,STORE_ERROR,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_AUTO_SELL,판매 가능한 전리품을 자동 판매했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_EQUIPMENT,더 좋은 장비를 구매했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_HEAL_DEFERRED,치료 기능은 치료소 단계에서 개방됩니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_NONE,가능한 상점 행동이 없습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_POLICY,가격 정책을 변경했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_POTION_LOW,목표 수량까지 포션을 구매했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_PURCHASE,왕국 상점에서 상품을 구매했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_RETURNED,마을에 귀환해 상점을 방문했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_SALE,왕국 상점에 상품을 판매했습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_REASON_SUPPLY,비상 재고가 공급되었습니다.,STORE_ACTIVITY,CONFIRMED,TRUE
ko-KR,TXT_P08_RECENT_ACTIVITY,최근 거래,STORE_LABEL,CONFIRMED,TRUE
ko-KR,TXT_P08_REPLAYED,완료된 거래 결과를 복구했습니다.,STORE_RESULT,CONFIRMED,TRUE
ko-KR,TXT_P08_SELECT_MERCENARY,용병 선택,STORE_ACTION,CONFIRMED,TRUE
ko-KR,TXT_P08_SELL,판매,STORE_ACTION,CONFIRMED,TRUE
ko-KR,TXT_P08_SOLD_OUT,품절,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_SORT,정렬,STORE_ACTION,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_CONTENT,상점이 운영 중입니다.,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_EMPTY,현재 조건에 맞는 상품이 없습니다. 필터를 바꾸거나 상품을 판매하세요.,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_ERROR,상점 데이터를 검증하지 못했습니다. 다시 시도하거나 돌아가세요.,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_LOADING,상점을 불러오는 중…,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_LOCKED,상점을 건설하고 상인을 배치하면 거래할 수 있습니다.,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STATE_OFFLINE,로컬 거래가 가능합니다. 서버 동기화는 아직 사용하지 않습니다.,STORE_STATE,CONFIRMED,TRUE
ko-KR,TXT_P08_STOCK,재고: {count},STORE_LABEL,CONFIRMED,TRUE
ko-KR,TXT_P08_STORE_TITLE,왕국 상점,STORE_TITLE,CONFIRMED,TRUE
ko-KR,TXT_P08_SYSTEM_SUPPLY,시스템 공급,STORE_BADGE,CONFIRMED,TRUE
ko-KR,TXT_P08_TAB_BUY,구매,STORE_TAB,CONFIRMED,TRUE
ko-KR,TXT_P08_TAB_HISTORY,거래 기록,STORE_TAB,CONFIRMED,TRUE
ko-KR,TXT_P08_TAB_SELL,판매,STORE_TAB,CONFIRMED,TRUE
ko-KR,TXT_P08_TRANSACTION_FAILED,거래 실패: {reason},STORE_RESULT,CONFIRMED,TRUE
ko-KR,TXT_P08_TRANSACTION_SUCCESS,거래 완료. 개인 {personalDelta}, 왕국 {kingdomDelta},STORE_RESULT,CONFIRMED,TRUE
ko-KR,TXT_P08_UPGRADE_REQUIRED,상점 레벨 {level}이 필요합니다.,STORE_ERROR,CONFIRMED,TRUE
ko-KR,TXT_P08_WALLET_INSUFFICIENT,개인 골드가 부족합니다.,STORE_ERROR,CONFIRMED,TRUE
```

모든 error code의 user message는 §20 registry key를 통해 위 generic failure 또는
전용 key로 연결한다. 긴 한국어 fixture는 1.25× font, 360px detail width에서 2행 이하,
잘림 0을 수용 기준으로 한다.

## 19. 계층별 type·DTO·public API

| TypeName | Namespace | Layer | Responsibility | Dependencies | PublicMembers | PersistentMutationAllowed |
|---|---|---|---|---|---|---|
| Money | KingdomTycoon.Domain.Economy | Domain | safe-int 금액 | none | Add,Subtract,Multiply | N |
| PricePolicy | same | Domain | policy+bps | none | Id,MultiplierBps | N |
| PersonalWallet | same | Domain | personal invariant | Money | Credit,Debit | clone only |
| KingdomWallet | same | Domain | kingdom invariant | Money | Credit | clone only |
| StoreStock | same | Domain | stack/equipment ownership | catalog | Add,Remove,Validate | clone only |
| StorePricingService | same | Domain | §5 formulas | pricing catalog | Acquire,Customer,Quote | N |
| StoreAvailability | same | Domain | facility/NPC matrix | facility projection | Evaluate | N |
| StorePurchaseDecisionService | same | Domain | deterministic AI | score/catalog | DecidePotion,DecideEquipment | N |
| EconomyInvariantValidator | same | Domain | ECO-01..12 | domain | Validate | N |
| EconomyUnitOfWork | KingdomTycoon.Application.Economy | Application | atomic command pipeline | repository/clock/uuid/JCS | Execute | Y |
| SetPricingPolicyHandler | same | Application | policy command | UoW | Handle | Y |
| SellToStoreHandler | same | Application | sell command | UoW | Handle | Y |
| BuyFromStoreHandler | same | Application | buy command | UoW | Handle | Y |
| RunStoreAutonomyCycle | same | Application | bounded orchestration | handlers | Run | handlers only |
| RefreshSystemStoreSupply | same | Application | internal supply | UoW | Run | Y |
| GetStorefrontHandler | same | Application | paged projection | read repo/catalog | Handle | N |
| GetStoreQuoteHandler | same | Application | transient quote | pricing/clock | Handle | N |
| GetStoreHistoryHandler | same | Application | ledger projection | read repo | Handle | N |
| P08EconomyCatalog | KingdomTycoon.Infrastructure.Economy | Infrastructure | CSV projection | content loader | Load/Validate | N |
| P07ToP08ContentMigration | same | Infrastructure | content migration | clock/JCS | CanApply,Apply | Y |
| SaveV1EconomyMapper | same | Infrastructure | JObject↔domain | Newtonsoft | Read,Write | clone only |
| LocalEconomyRepository | same | Infrastructure | atomic Save | ISaveRepository | Snapshot,Commit | Y |
| P08NetworkEconomyAdapter | same | Infrastructure | P16 seam | none | NotSupported | N |
| StoreScreenPresenter | KingdomTycoon.Presentation.Store | Presentation | state/commands | application ports | Open,Close,Select,Confirm | command only |
| StoreScreenView | same | Presentation | uGUI binding | DTO only | Render | N |
| StoreProductVirtualList | same | Presentation | pooled rows | ProductRowDto | Bind | N |
| StoreTransactionModal | same | Presentation | quote confirmation | QuoteDto | Show,Hide | N |
| StoreWalletRenderer | same | Presentation | delta animation/a11y | WalletDto | Render | N |

View DTO는 primitive/immutable list만 사용하고 JObject, Save model, CSV row를 노출하지
않는다. sort order는 server-independent ordinal fields로 Application에서 확정한다.

## 20. event·reason·error registry

Events는 commit 뒤 이 순서로 발행한다: `StoreStockRemoved/Added`, wallet changed,
transaction semantic event, `StoreItemSoldOut`, presenter refresh. replay는 event를 다시
발행하지 않는다.

| Event | Exact trigger | Payload |
|---|---|---|
| StoreOpened | availability OPEN edge | facilityId,level,revision |
| StoreStopped | OPEN→non-OPEN | stopReason,revision |
| PricingPolicyChanged | policy commit | old,new,revision |
| StoreStockAdded | positive stock delta | operationId,lines,stockVersion |
| StoreStockRemoved | negative stock delta | operationId,lines,stockVersion |
| MercenarySoldToStore | sell commit | mercenaryId,total |
| MercenaryBoughtFromStore | buy commit | mercenaryId,total |
| PersonalGoldChanged | nonzero personal delta | mercenaryId,delta,balance |
| KingdomGoldChanged | nonzero kingdom delta | delta,balance |
| SystemStoreSupplyRefreshed | supply commit | epoch,added lines |
| StoreItemSoldOut | quantity reaches zero | product kind/id |

| Code | ExactTrigger | UserMessageKey | Retryable | Log | Mutation | UiRecovery |
|---|---|---|---|---|---|---|
| P08_CONTENT_MISSING | table/FK/asset missing | TXT_P08_STATE_ERROR | N | ERROR | N | Back |
| P08_STORE_LOCKED | not built | TXT_P08_STATE_LOCKED | N | INFO | N | Facility |
| P08_STORE_NOT_ACTIVE | building/upgrading/stopped | TXT_P08_STATE_CONTENT | Y | INFO | N | Wait/assign |
| P08_MERCHANT_REQUIRED | no valid working merchant | TXT_P08_STATE_LOCKED | N | INFO | N | Assign |
| P08_STORE_LEVEL_REQUIRED | product/policy level gate | TXT_P08_UPGRADE_REQUIRED | N | INFO | N | Upgrade |
| P08_PRODUCT_NOT_FOUND | unknown product/instance | TXT_P08_STATE_ERROR | Y | WARN | N | Refresh |
| P08_STOCK_EMPTY | line absent | TXT_P08_SOLD_OUT | Y | INFO | N | Refresh |
| P08_STOCK_INSUFFICIENT | qty exceeds current | TXT_P08_QUOTE_CHANGED | Y | INFO | N | Requote |
| P08_PERSONAL_GOLD_INSUFFICIENT | debit unavailable | TXT_P08_WALLET_INSUFFICIENT | N | INFO | N | Sell/Hunt |
| P08_WALLET_RANGE_INVALID | load/post range invalid | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_PRICE_OVERFLOW | checked arithmetic overflow | TXT_P08_STATE_ERROR | N | ERROR | N | Back |
| P08_QUANTITY_INVALID | qty outside 1..limit | TXT_P08_TRANSACTION_FAILED | N | WARN | N | Edit qty |
| P08_ITEM_PROTECTED | P07 protection | TXT_P08_TRANSACTION_FAILED | N | INFO | N | Deselect |
| P08_EQUIPMENT_INELIGIBLE | job/level/quality/boss gate | TXT_P08_TRANSACTION_FAILED | N | INFO | N | Select other |
| P08_DESTINATION_CAPACITY_EXCEEDED | store/inventory/potion cap | TXT_P08_TRANSACTION_FAILED | N | INFO | N | Free space |
| P08_QUOTE_STALE | quote context/TTL mismatch | TXT_P08_QUOTE_CHANGED | Y | INFO | N | Requote |
| P08_POLICY_INVALID | enum/level invalid | TXT_P08_TRANSACTION_FAILED | N | WARN | N | Reset |
| P08_OPERATION_REPLAY_MISMATCH | same ID different hash | TXT_P08_STATE_ERROR | N | ERROR | N | New operation |
| P08_SAVE_REVISION_CONFLICT | expected != current | TXT_P08_QUOTE_CHANGED | Y | INFO | N | Refresh |
| P08_TRANSACTION_INVARIANT_FAILED | ECO reconciliation failure | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_SUPPLY_ALREADY_APPLIED | epoch/target already satisfied | none | N | DEBUG | N | no-op |
| P08_AUTONOMY_CYCLE_LIMIT | >48 attempted commands | TXT_P08_STATE_ERROR | Y | WARN | committed prior only | next cycle |
| P08_STOCK_INVALID | load/post invalid stock | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_EQUIPMENT_OWNERSHIP_CONFLICT | duplicate owner | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_LEDGER_CORRUPT | digest/sequence mismatch | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_STORE_STORAGE_CONFLICT | FAC_STORE.storage nonempty | TXT_P08_STATE_ERROR | N | ERROR | N | Recovery |
| P08_LEGACY_COMMAND_RETIRED | new SELL_INVENTORY on .6 | TXT_P08_TRANSACTION_FAILED | N | WARN | N | New flow |

Telemetry fields는 `traceId,operationId,contentVersion,revision,errorCode,transactionType,
facilityState,storeLevel,stockVersion`만 허용한다. 이름·balance·inventory 내용은 로그에
남기지 않는다.

## 21. EditMode·PlayMode·integration/soak golden

clock은 `2026-07-23T00:00:00.000Z`, UUID provider는 fixture 순서, RNG 호출 0,
sleep 0이다. 모든 request는 JCS SHA-256을 테스트에서 계산하고 아래 semantic vector를
고정한다.

### 21.1 executable command vectors

```json
{"commandType":"SET_PRICING_POLICY","operationId":"018f0000-0000-7000-8000-000000000901","expectedRevision":12,"policyId":"HIGH"}
```

Expected: revision 12→13, policy STANDARD→HIGH, wallet/stock delta 0, ledger sequence 1,
reason `STORE_POLICY_CHANGE`.

```json
{"commandType":"SELL_TO_STORE","operationId":"018f0000-0000-7000-8000-000000000902","expectedRevision":13,"mercenaryInstanceId":"018f0000-0000-7000-8000-000000000101","lines":[{"kind":"EQUIPMENT","id":"018f0000-0000-7000-8000-000000000201","quantity":1},{"kind":"ITEM","id":"MAT_R01_WILD_HERB","quantity":3}]}
```

Before: personal 500, kingdom 5000, herb inventory 3, equipment power100. Expected unit
5 and25, total40; personal540, kingdom5000, herb stock3, equipment stock1, revision13→14,
stockVersion0→1, reason `MERCENARY_STORE_SALE`.

```json
{"commandType":"BUY_FROM_STORE","operationId":"018f0000-0000-7000-8000-000000000903","expectedRevision":14,"quoteContextHash":"4d3dc8f578cd0474dd4f8ee536b05dba652b76f7a8f40df89f6e6c593ee10d27","mercenaryInstanceId":"018f0000-0000-7000-8000-000000000101","equipIfUpgrade":false,"lines":[{"kind":"POTION","id":"POT_HEAL_SMALL","quantity":2,"quotedUnitPrice":46}]}
```

Before: personal540, kingdom5000, small potion stock8. HIGH total92. Expected personal448,
kingdom5092, merc potion+2, stock6, revision14→15, stockVersion1→2.

```json
{"commandType":"REFRESH_SYSTEM_STORE_SUPPLY","operationId":"018f0000-0000-7000-8000-000000000904","expectedRevision":15,"targetEpoch":1}
```

Before epoch1, buyCount=8, small stock0, five T1 weapon stock0. Expected small8+five equipment,
epoch2, buyCount0, revision15→16, stockVersion2→3, exactly six added lines.

| Command | RFC 8785 requestHash |
|---|---|
| SET_PRICING_POLICY | `3d34c69127d7a2edf67e263a13f674e2567fed839a279be67536cc2b28a43b7f` |
| SELL_TO_STORE | `d18ab64c87efbe132c550230e74fc540493f976586e499f86ce714822438a88d` |
| BUY_FROM_STORE | `8c1d85e42bad6a75f6025af448b04a6a984a4adfd9570d99d83406643ea3c944` |
| REFRESH_SYSTEM_STORE_SUPPLY | `2ffb5e188780d12d186c48e48ccc9a8035ffe4ea07ca680fb959c86843e3919b` |

Result digest는 `resultDigest` field를 넣기 전 result JSON의 RFC 8785 bytes를 hash한다.

```json
{"operationId":"018f0000-0000-7000-8000-000000000901","revisionBefore":12,"revisionAfter":13,"policyId":"HIGH","replayed":false}
```

Digest: `28098bf5c3f27d8be19aa4d2a126d986147ab133344b57655722de50b6a6141e`.

```json
{"operationId":"018f0000-0000-7000-8000-000000000902","revisionBefore":13,"revisionAfter":14,"mercenaryInstanceId":"018f0000-0000-7000-8000-000000000101","totalPersonalGoldDelta":40,"stockVersionAfter":1,"lines":[{"kind":"EQUIPMENT","id":"018f0000-0000-7000-8000-000000000201","quantity":1,"unitPrice":25,"lineTotal":25},{"kind":"ITEM","id":"MAT_R01_WILD_HERB","quantity":3,"unitPrice":5,"lineTotal":15}],"replayed":false}
```

Digest: `e10155a91a633d0b856ee52ba6e5bbeb2a72cee351230558b1e99c308d05c823`.

```json
{"operationId":"018f0000-0000-7000-8000-000000000903","revisionBefore":14,"revisionAfter":15,"mercenaryInstanceId":"018f0000-0000-7000-8000-000000000101","totalPersonalGoldDelta":-92,"totalKingdomGoldDelta":92,"stockVersionAfter":2,"lines":[{"kind":"POTION","id":"POT_HEAL_SMALL","quantity":2,"unitPrice":46,"lineTotal":92}],"replayed":false}
```

Digest: `e6fe22f0dfbf43da0037621615b86f9039c2e323a518625bc34f94de4ac3517a`.

```json
{"operationId":"018f0000-0000-7000-8000-000000000904","revisionBefore":15,"revisionAfter":16,"epoch":2,"addedLines":[{"kind":"EQUIPMENT","id":"EQ_T1_ARCHER_WEAPON","quantity":1},{"kind":"EQUIPMENT","id":"EQ_T1_CLERIC_WEAPON","quantity":1},{"kind":"EQUIPMENT","id":"EQ_T1_GUARDIAN_WEAPON","quantity":1},{"kind":"EQUIPMENT","id":"EQ_T1_MAGE_WEAPON","quantity":1},{"kind":"EQUIPMENT","id":"EQ_T1_WARRIOR_WEAPON","quantity":1},{"kind":"POTION","id":"POT_HEAL_SMALL","quantity":8}],"stockVersionAfter":3,"replayed":false}
```

Digest: `a8ed4a9ec17e0e3b1acbfaceec7dca81060216259d708673f1bf2e22a00bee62`.

Golden result DTO는 §10 property order, journal은 operationId/requestHash/resultDigest와
동일 resultPayload, ledger는 §7 schema를 사용한다. `scripts/generate_p08_content.py`
golden mode가 requestHash/resultDigest를 계산해 `docs/goldens/P08/*.json`에 고정한다.

### 21.2 test matrix

| TestId | TestClass | Mode | Given/When | Then | Timeout |
|---|---|---|---|---|---:|
| P08-E-001 | P08PricingTests | Edit | 3 policies×all formula vectors | exact table §5 | 1s |
| P08-E-002 | P08MoneyBoundaryTests | Edit | 0/max/overflow | no negative/overflow | 1s |
| P08-E-003 | P08AvailabilityTests | Edit | 6 states×4 levels×merchant | §6 exact | 1s |
| P08-E-004 | P08StockInvariantTests | Edit | stack/equipment ownership | ECO-03/04 | 1s |
| P08-E-005 | P08ProtectionTests | Edit | equipped/locked/boss/first discovery | reject 0 mutation | 1s |
| P08-E-006 | P08PurchaseAiTests | Edit | 6 personalities | deterministic decisions | 2s |
| P08-E-007 | P08QuoteTests | Edit | stale TTL/revision/policy/stock | priority exact | 1s |
| P08-E-008 | P08ReplayTests | Edit | same/different hash | replay/mismatch | 1s |
| P08-E-009 | P08AtomicBatchTests | Edit | line2 invalid | mutation 0 | 1s |
| P08-E-010 | P08SupplyTests | Edit | initial/duplicate/crash/restart | one epoch only | 2s |
| P08-E-011 | P08MigrationTests | Edit | .5 buildable/active/stopped | .6 exact | 3s |
| P08-E-012 | P08LedgerTests | Edit | 201 transactions | prune chain reconcile | 2s |
| P08-E-013 | P08ContentHashTests | Edit | generated package | 75 tables/schema/localization valid | 3s |
| P08-P-001 | P08StoreFlowTests | Play | Kingdom→Drawer→Store | opens/closes/back | 10s |
| P08-P-002 | P08UnlockFlowTests | Play | build→claim→assign | OPEN+supply | 15s |
| P08-P-003 | P08SellBuyFlowTests | Play | return→sell→potion buy | wallet/stock UI exact | 15s |
| P08-P-004 | P08EquipmentBuyTests | Play | compare→buy/equip | atomic ownership | 10s |
| P08-P-005 | P08PolicyFlowTests | Play | STANDARD→HIGH | quotes refresh | 10s |
| P08-P-006 | P08SixStateTests | Play | six fixtures | correct CTA/mutation gate | 15s |
| P08-P-007 | P08ResponsiveTests | Play | 16:9/18:9/20:9, 1.25× | clip0,touch≥64 | 20s |
| P08-I-001 | P08EconomySoakTests | Edit integration | 16 merc,18000 ticks | deterministic hash, deadlock0 | 60s |
| P08-I-002 | P08StockContentionTests | Edit integration | 16 buyers,last unit | one winner,no duplicate | 5s |
| P08-I-003 | P08CrashMatrixTests | Edit integration | crash each step1..12 | old or new only | 15s |
| P08-I-004 | P08BackupRecoveryTests | Edit integration | corrupt primary | backup loads/reconcile | 10s |
| P08-I-005 | P08ServerRegressionTests | JUnit | existing Testcontainers suite | 10/10+, P08 HTTP calls0 | 180s |

Soak acceptance: 18,000 10Hz ticks, persistent wallet/stock negative 0, duplicate item 0,
reconciliation failure 0, deadlock 0, command limit breach 0. Fixed simulation에서 100
동일 purchase opportunities의 successful counts는 LOW≥STANDARD≥HIGH이고 LOW-HIGH≥15다.

## 22. 성능·메모리·수명주기

| Metric | Acceptance |
|---|---:|
| storefront 100 lines query editor p95 | ≤3ms, alloc≤96KiB |
| price recompute 100×3 p95 | ≤2ms, alloc≤32KiB |
| 16 merc autonomy decision p95 | ≤4ms, alloc≤64KiB |
| local transaction domain+mapping p95 excluding disk | ≤5ms |
| successful transaction Save writes | exactly 1 |
| live product views | 24 |
| scroll steady-state | 0B/frame after warm-up |
| Store open/close 10회 | listener/handle baseline 동일 |
| economy soak 30분 sustained growth | ≤8MiB |
| Android frame | 60fps target, p95≤33.3ms low mode |

Profiler markers: `P08.Storefront.Query`, `P08.Price.Recompute`, `P08.Autonomy.Cycle`,
`P08.Transaction.Validate`, `P08.Transaction.Commit`, `P08.Store.Render`,
`P08.VirtualList.Bind`. Close 시 Presenter subscription, Addressables handle, row pool binding,
modal callback을 해제한다. AppRoot service는 scene 전환에도 1개지만 View reference는 0개다.

## 23. Editor generation·Android build

- content/schema/golden generator: `scripts/generate_p08_content.py [--check]`
- Unity setup: `KingdomTycoon.Editor.P08StoreSetup.Run`
- asset verifier: `KingdomTycoon.Editor.P08GeneratedAssetVerifier.Verify`
- capture: `KingdomTycoon.Editor.P08CaptureGenerator.Run`
- Android: `KingdomTycoon.Editor.P08AndroidBuilder.Build`
- APK: `client-unity/Builds/Android/KingdomTycoon-P08-Development.apk`
- Addressables: `Content-P08-Store-v1`, labels `P08_STORE,P08_CURRENCY,P08_STATE,
  P08_POLICY,P08_TRANSACTION,P08_MERCHANT`.

```powershell
python scripts/generate_p08_content.py --check
python scripts/validate_content.py

Unity.exe -batchmode -nographics -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P08StoreSetup.Run `
  -logFile .\client-unity\Logs\p08-setup.log

Unity.exe -batchmode -nographics -projectPath .\client-unity `
  -runTests -testPlatform EditMode `
  -testResults .\client-unity\Logs\p08-editmode-results.xml

Unity.exe -batchmode -nographics -projectPath .\client-unity `
  -runTests -testPlatform PlayMode `
  -testResults .\client-unity\Logs\p08-playmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P08CaptureGenerator.Run

Unity.exe -batchmode -nographics -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P08AndroidBuilder.Build `
  -logFile .\client-unity\Logs\p08-android-build.log
```

Setup rerun은 semantic diff 0이어야 한다. scene/prefab YAML은 Unity API로만 생성하고
Asset과 `.meta`를 함께 커밋한다. build guard는 content/schema hash, Addressables 12개,
stable element ID uniqueness, scene reference, Development+IL2CPP+ARM64를 검사한다.

## 24. 수동 검증·capture matrix

| File | Resolution | Fixture | Success |
|---|---:|---|---|
| p08_01_store_content.png | 1920×1080 | OPEN, stock13 | merchant·두 wallet·stock 표시 |
| p08_02_sell.png | 1920×1080 | herb3+equipment1 | 예상 개인 +40 |
| p08_03_equipment_compare.png | 1920×1080 | T1 weapon upgrade | score/stat/115 표시 |
| p08_04_potion_buy.png | 1920×1080 | small2 HIGH | target·92·wallet delta |
| p08_05_policy.png | 1920×1080 | level2 | 3 policy·가격 변화 |
| p08_06_result.png | 1920×1080 | committed buy | personal/kingdom/stock delta |
| p08_07_empty.png | 1920×1080 | filter result0 | 원인+필터 reset |
| p08_08_merchant_missing.png | 1920×1080 | ACTIVE,no merchant | STOPPED+배치 CTA |
| p08_09_locked.png | 1920×1080 | BUILDABLE | 건설 CTA |
| p08_10_error.png | 1920×1080 | stale quote | retry/back |
| p08_11_20x9.png | 2400×1080 | Korean 1.25× | Safe Area·clip0 |
| p08_12_autonomy.png | 1920×1080 | sell→buy cycle | reason/activity order |

Physical Android smoke: 진입·스크롤·filter·modal·Back, 정책 변경, double tap,
pause/background, commit 각 단계 강제 종료 후 old/new atomicity, 한국어 큰 글자,
30분 loop, 저사양 frame/memory/thermal을 확인한다. 기기 미연결이면 APK 생성은 완료할 수
있지만 physical 항목은 미체크와 사유를 보고서에 남긴다.

## 25. 구현 파일 지도·커밋 분리

| Path | New/Modified | Responsibility | Authored/Generated | OwningCommit | Verification |
|---|---|---|---|---|---|
| docs/design/TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0.md | new | final contract | authored | 1 | markdown audit |
| scripts/generate_p08_content.py | new | content/schema/golden | authored | 2 | `--check` |
| StreamingAssets/Content/1.0.0-content.6 | new | 75-table package | generated | 2 | validator |
| Resources/Contracts/save.content.6.schema.json | new | conditional schema | generated | 2 | schema tests |
| Resources/Contracts/p08-*.json | new | migration/new game | generated | 2 | golden tests |
| Runtime/Domain/Economy | new | money/price/stock/AI | authored | 3 | EditMode |
| Runtime/Application/Economy | new | commands/queries/UoW | authored | 3 | EditMode |
| Runtime/Infrastructure/Economy | new | save/catalog/ledger | authored | 3 | integration |
| Runtime/Infrastructure/Inventory | modified | P07 compatibility | authored | 4 | replay tests |
| Runtime/Application/Combat | modified | post-terminal autonomy event | authored | 4 | soak |
| Runtime/Presentation/Store | new | Store UI | authored | 5 | PlayMode |
| Editor/P08StoreSetup.cs | new | scene/assets/setup | authored | 5 | idempotence |
| Tests/EditMode/P08EconomyTests.cs | new | domain/migration/soak | authored | 6 | Unity tests |
| Tests/PlayMode/P08StoreScreenTests.cs | new | flows/states | authored | 6 | Unity tests |
| scripts/ci/p08.sh | new | phase gate | authored | 7 | shell syntax |
| docs/reports/P08_ECONOMY_STORE_REPORT.md | new | evidence | authored | 7 | links/hash |

Commit messages:

1. `docs: add P08 economy and store design`
2. `feat: add P08 content and save migration`
3. `feat: implement store economy transactions`
4. `feat: integrate store autonomy with hunt settlement`
5. `feat: add kingdom store interface`
6. `test: validate P08 economy and Android flow`
7. `ci: add P08 validation and completion report`

## 26. P08 완료 checklist·P09 handoff

- [ ] `.6` 75-table package, schema, registry, migration hash 검증
- [ ] `.5` Save는 `.5` schema, `.6` Save는 economy 필수, mixed field rejection
- [ ] sell/buy/policy/supply의 wallet·stock·inventory·journal reconciliation
- [ ] P07 old replay와 P08 legacy command rejection
- [ ] 6 personality, 16 merc contention, supply epoch 결정론
- [ ] merchant/facility matrix와 6 UI state
- [ ] Store stable IDs, 12 assets, 64px touch, 16:9~20:9
- [ ] EditMode/PlayMode/30분 soak/crash matrix failure 0
- [ ] 기존 server Testcontainers regression, P08 HTTP 호출 0
- [ ] Android IL2CPP ARM64 Development APK와 12 capture
- [ ] P09 `IStoreStockSink`, PRODUCTION_OWNED migration seam 유지
- [ ] P08에서 production queue, recipe output, NPC XP mutation 0

P09는 이 checklist가 모두 통과한 P08 report 이후에만 시작한다.

## 27. 최종 자체 감사 결과

| Audit | Result |
|---|---|
| 포함/제외와 Phase ownership | PASS |
| 두 wallet·외부 liquidity·transfer equality | PASS |
| integer 공식·rounding·overflow | PASS |
| facility/NPC/level matrix | PASS |
| stock ownership·capacity·sorting | PASS |
| system supply idempotency·P09 replacement | PASS |
| 6 personality autonomy·contention | PASS |
| command/query/hash/replay/crash order | PASS |
| migration·conditional schema composition | PASS |
| 75-table package production rows·generator authority | PASS |
| UI hierarchy·6 states·localization·assets | PASS |
| type/event/reason/error registry | PASS |
| tests·performance·Android·capture | PASS |
| forbidden P09~P16 mutation | PASS |

설계 요청과의 유일한 형식 correction은 수백 KiB의 `.5` schema와 63개 unchanged CSV를
문서에 재복제하지 않고, tracked source SHA + deterministic composer/generator로 byte를
고정한 것이다. 이는 구현 중 전사 오류를 줄이고 같은 산출물을 검증 가능하게 만든다.
구조·필드·rows·정렬·serializer는 이 문서에 모두 확정되어 추가 제품 결정은 필요 없다.

## 28. UNRESOLVED

`UNRESOLVED: NONE`

## 29. 구현 준비 선언

`IMPLEMENTATION_READY: YES`
