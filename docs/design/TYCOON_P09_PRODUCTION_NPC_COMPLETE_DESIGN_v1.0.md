# TYCOON P09 생산·NPC 최종 설계서 v1.0

> 문서 상태: FINAL
> 구현 기준: P08 `1.0.0-content.6` / Save v1
> 산출 버전: Game `1.0.0-p09`, Content `1.0.0-content.7`
> IMPLEMENTATION_READY: YES
> UNRESOLVED: NONE

## 1. 목적과 완료 정의

P09는 사냥에서 획득한 재료가 왕국 생산 시설을 거쳐 상점 상품과 치료 서비스로 전환되는 첫 완전한 운영 루프다. 플레이어는 재고 목표와 생산 우선순위를 정하고, 배치된 관리 NPC는 결정론적 생산 틱에 따라 자동으로 작업한다.

P09 완료 조건은 다음과 같다.

1. P08 저장을 손실 없이 P09로 마이그레이션한다.
2. 대장간과 연금 공방은 재료 예약, 대기열, 완료, 상점 입고를 원자적으로 처리한다.
3. 진료소는 부상 상태 용병의 치료 대기열과 완료를 처리한다.
4. 상점의 P08 시스템 보급을 중단하고 P09 생산 소유 모드로 전환한다.
5. 시설 레벨, NPC 숙련도, 레시피 자격과 재료 효율을 서로 다른 책임으로 계산한다.
6. 재고 목표, 대기열, 숙련도, 생산 중단 사유를 한 화면에서 확인하고 조작할 수 있다.
7. Content/Save 골든, EditMode, PlayMode, 에셋 검증, CI 게이트와 Android 개발 빌드 진입점을 제공한다.

## 2. 설계 권위와 충돌 해결

우선순위는 이 문서, P08 최종 설계, `docs/10`, `12`, `13`, `14`, `15`, `26`, 나머지 마스터 문서 순이다.

| 충돌 | 최종 결정 |
|---|---|
| P08 문서의 `IStoreStockSink` 예고와 실제 내부 전용 재고 함수 | P09에서 저장 초안을 받는 어댑터를 추가한다. 생산 서비스가 Save commit을 소유해 생산 완료와 입고를 한 revision으로 확정한다. |
| P08 `SYSTEM_SUPPLY`와 P09 생산 | 마이그레이션 시 `supplyState.mode=PRODUCTION_OWNED`. 기존 시스템 보급 재고는 판매 가능하지만 새 보급 명령은 거부한다. |
| 기존 레시피 전 범위와 P09 범위 | 데이터는 보존하되 P09 자동 목표 기본값은 소형 회복 물약과 직업별 T1 무기 5종이다. 시설/숙련 자격을 만족하면 나머지 기존 레시피도 수동 생산할 수 있다. |
| 진료소 레시피 부재 | 치료는 재료 제작 레시피가 아닌 서비스 규칙으로 분리한다. `INJURED` 용병을 `IDLE_TOWN`으로 복귀시키며 P09에서는 영구 사망이나 상태 이상 치료를 도입하지 않는다. |
| 실시간/오프라인 생산 | P09 생산은 명시적인 정수 tick만 사용한다. UTC 기반 자동 완료와 오프라인 따라잡기는 P14 이전에 도입하지 않는다. |
| 유료 에셋 | P08~P14 정책에 따라 무료 또는 자체 제작 대체 에셋만 사용한다. P17 승인 전 구매하지 않는다. |

## 3. 범위

### 3.1 포함

- 대장간 장비 제작, 연금 공방 포션 제작
- 진료소 부상 치료
- 시설별 FIFO 대기열과 재고 목표 기반 자동 보충
- 관리 NPC 숙련 경험치와 4단계 승급
- 생산 중단 사유 및 복구 안내
- 상점 생산 입고와 기존 재고 판매
- P08→P09 Save/Content migration
- 모바일 가로 UI, 접근성, 검증 자동화

### 3.2 제외

- 강화, 제련, 분해, 잠금 정책: P10
- NPC 장비와 NPC 개별 스탯
- 멀티 슬롯 병렬 생산, 생산 취소 환불, 작업 순서 드래그
- 오프라인 생산과 벽시계 기반 완료
- 서버 권위 생산, 광고, IAP
- 영구 사망, 질병, 상태 이상 치료
- 유료 아트·오디오 구매

## 4. 도메인 규칙

### 4.1 시설 책임

시설 레벨은 다음만 소유한다.

- 대기열 한도: Lv1 4, Lv2 6, Lv3 8, Lv4 10
- 생산 속도 bps: 10000, 11250, 12750, 14500
- 상점 스택/장비 입고 한도는 P08 상점 레벨 규칙을 그대로 사용한다.
- 레시피의 `facility_level` 이하만 허용한다.

시설은 `ACTIVE`이고 맞는 직업의 `working=true` NPC가 양방향으로 배치돼야 작업할 수 있다.

### 4.2 NPC 책임

NPC 숙련도는 `npc_proficiency_levels.csv`를 따른다.

- 자격: 레시피 `npc_proficiency_id` 이상의 order
- 재료: 각 입력에 `ceil(baseQuantity * materialEfficiency)` 적용, 최솟값 1
- 속도: `ceil(baseTicks * 10000 / facilitySpeedBps / npcSpeedBps)`; 최솟값 1
- 경험치: 제작 완료 시 `baseCraftSeconds * quantity`, 치료 완료 시 `treatmentXp`
- 승급: 누적 XP가 다음 단계 `xp_required` 이상이면 가장 높은 충족 단계로 즉시 정규화
- 품질 보너스는 P09에서 품질 결정에 사용하되 강화·제련과 결합하지 않는다.

상인 경험치는 P09 생산품이 상점에 입고될 때 입고 수량만큼 증가한다. P08 거래 장부와 가격 계산은 변경하지 않는다.

### 4.3 생산 대기열

시설별 큐는 `queueNo` 오름차순 FIFO다. 하나의 시설은 한 tick에서 맨 앞 작업 하나만 전진한다.

작업 상태는 `QUEUED`, `RUNNING`, `COMPLETED`만 사용한다. 완료 작업은 결과와 경험치를 반영한 같은 commit에서 큐에서 제거되고 `lastCompletion`에 요약된다.

재료는 enqueue 성공 시 전량 차감해 `reservedMaterials`에 기록한다. 따라서 동일 재료를 두 작업이 이중 사용하지 않는다. 취소가 없으므로 예약 재료의 환불 규칙은 P09에 없다.

### 4.4 생산 tick

- tick은 양의 정수이며 명령당 1~3600이다.
- 서비스 전역의 `currentTick`은 단조 증가한다.
- tick을 여러 번 나눠 적용해도 합계가 같으면 결과가 같다.
- 한 명령에서 완료 가능한 작업 수는 최대 64다. 초과분은 다음 명령에서 계속된다.
- 벽시계와 프레임 시간은 결과에 영향을 주지 않는다.
- 작업 완료 장비 ID는 `operationId`, `jobId`, output index로 생성한 결정론적 UUIDv7 형식이다.

### 4.5 재고 목표 자동화

초기 목표는 P08 보급량과 같다.

| priority | kind | product | target |
|---:|---|---|---:|
| 10 | POTION | `POT_HEAL_SMALL` | 8 |
| 20 | EQUIPMENT | `EQ_T1_WARRIOR_WEAPON` | 1 |
| 30 | EQUIPMENT | `EQ_T1_GUARDIAN_WEAPON` | 1 |
| 40 | EQUIPMENT | `EQ_T1_ARCHER_WEAPON` | 1 |
| 50 | EQUIPMENT | `EQ_T1_MAGE_WEAPON` | 1 |
| 60 | EQUIPMENT | `EQ_T1_CLERIC_WEAPON` | 1 |

가용량은 상점 현재 수량과 해당 product의 큐 출력량 합이다. `available < target`이면 priority 오름차순으로 작업을 최대 하나 enqueue한다. 자동화 한 번의 상한은 시설당 1개, 전체 2개다.

소형 회복 물약은 비상 품목이다. 목표가 2 미만이면 설정 명령이 2로 보정한다. 목표 최댓값은 포션 99, 장비 10이다. 목표 0은 장비만 허용한다.

자동화 실패는 저장을 깨지 않고 해당 목표의 `lastStopReason`을 갱신한다.

### 4.6 치료

- `INJURED` 상태이고 마을에 있는 활성 용병만 대상이다.
- `FAC_INFIRMARY` ACTIVE와 배치된 `NPC_HEALER`가 필요하다.
- 기본 치료 tick은 30, 시설/NPC 속도 규칙을 동일 적용한다.
- 완료 시 autonomy state=`IDLE_TOWN`, reasonCode=`TREATMENT_COMPLETE`, 현재 지역/타깃은 null이다.
- 동일 용병의 중복 치료 요청은 거부한다.

### 4.7 중단 사유

| 코드 | 의미 | 복구 |
|---|---|---|
| `NONE` | 생산 가능 | 없음 |
| `P09_FACILITY_LOCKED` | 미건설/건설 중 | 시설 건설·완료 |
| `P09_FACILITY_INACTIVE` | 정지/업그레이드 중 | 시설 활성화 |
| `P09_NPC_REQUIRED` | NPC 미배치/비근무 | 직업 일치 NPC 배치 |
| `P09_RECIPE_LOCKED` | 시설 레벨 부족 | 시설 업그레이드 |
| `P09_PROFICIENCY_REQUIRED` | 숙련도 부족 | 하위 작업으로 XP 획득 |
| `P09_MATERIAL_INSUFFICIENT` | 재료 부족 | 사냥/재고 확보 |
| `P09_QUEUE_FULL` | 큐 한도 도달 | tick 진행 |
| `P09_TARGET_REACHED` | 목표 충족 | 판매 또는 목표 상향 |
| `P09_OUTPUT_CAPACITY` | 상점 수용량 부족 | 판매/상점 업그레이드 |
| `P09_TREATMENT_TARGET_INVALID` | 치료 대상 부적합 | 부상 용병 선택 |

## 5. 애플리케이션 계약

### 5.1 명령

모든 명령은 `operationId`, `expectedRevision`, `requestHash`를 가진다. 해시는 RFC 8785 canonical JSON의 SHA-256이다.

- `SET_STOCK_TARGET(productKind, productId, targetQuantity, priority, enabled)`
- `ENQUEUE_PRODUCTION(recipeId, quantity)`
- `ADVANCE_PRODUCTION_TICKS(ticks)`
- `RUN_PRODUCTION_AUTOMATION()`
- `ENQUEUE_TREATMENT(mercenaryInstanceId)`

동일 operationId와 동일 requestHash 재요청은 현재 결과를 replay한다. operationId가 같고 hash가 다르면 `P09_OPERATION_REPLAY_MISMATCH`다. revision 불일치는 `P09_SAVE_REVISION_CONFLICT`다.

### 5.2 원자성

한 명령은 다음 순서를 한 Save revision에서 처리한다.

1. 준비 상태와 replay 검증
2. Save snapshot clone
3. 자격/한도 검증
4. 재료 예약 또는 tick 진행
5. 결과 생성과 `IStoreStockSink` 입고 또는 치료 반영
6. NPC XP/숙련도 정규화
7. production event 및 operation journal 추가
8. Save repository CAS commit
9. 공유 게임 문서 동기화 후 UI 이벤트 발행

중간 실패 시 clone을 폐기하며 재료, 큐, 상점, XP 중 일부만 반영되는 상태는 금지한다.

### 5.3 이벤트

- `ProductionQueueChanged`
- `ProductionCompleted`
- `FacilityProductionStopped`
- `NpcProficiencyChanged`
- `TreatmentCompleted`

Save에는 최근 100개의 production event만 유지한다. UI 이벤트 발행 실패는 commit을 되돌리지 않는다.

## 6. Content `1.0.0-content.7`

P08의 75개 테이블을 바이트 그대로 복사하고 다음 3개를 추가한다.

### `production_facility_rules.csv`

```csv
facility_level,queue_capacity,speed_bps,status,enabled
1,4,10000,TUNABLE,TRUE
2,6,11250,TUNABLE,TRUE
3,8,12750,TUNABLE,TRUE
4,10,14500,TUNABLE,TRUE
```

### `production_stock_targets.csv`

```csv
target_id,priority,product_kind,product_id,recipe_id,target_quantity,min_target,max_target,emergency_floor,status,enabled
TARGET_SMALL_HEAL,10,POTION,POT_HEAL_SMALL,REC_POT_HEAL_SMALL,8,2,99,2,TUNABLE,TRUE
TARGET_WARRIOR,20,EQUIPMENT,EQ_T1_WARRIOR_WEAPON,REC_EQ_T1_WARRIOR_WEAPON,1,0,10,0,TUNABLE,TRUE
TARGET_GUARDIAN,30,EQUIPMENT,EQ_T1_GUARDIAN_WEAPON,REC_EQ_T1_GUARDIAN_WEAPON,1,0,10,0,TUNABLE,TRUE
TARGET_ARCHER,40,EQUIPMENT,EQ_T1_ARCHER_WEAPON,REC_EQ_T1_ARCHER_WEAPON,1,0,10,0,TUNABLE,TRUE
TARGET_MAGE,50,EQUIPMENT,EQ_T1_MAGE_WEAPON,REC_EQ_T1_MAGE_WEAPON,1,0,10,0,TUNABLE,TRUE
TARGET_CLERIC,60,EQUIPMENT,EQ_T1_CLERIC_WEAPON,REC_EQ_T1_CLERIC_WEAPON,1,0,10,0,TUNABLE,TRUE
```

### `treatment_rules.csv`

```csv
treatment_id,facility_id,required_profession_id,base_ticks,xp_reward,from_state,to_state,status,enabled
TREAT_INJURY_BASIC,FAC_INFIRMARY,NPC_HEALER,30,30,INJURED,IDLE_TOWN,TUNABLE,TRUE
```

manifest는 `contentVersion=1.0.0-content.7`, `csvSchemaSetVersion=5`, `minimumGameVersion=1.0.0-p09`, 정확히 78개 table, 파일별 rowCount/SHA-256을 가진다.

## 7. Save 계약과 Migration

### 7.1 payload.production

```json
{
  "productionVersion": 1,
  "currentTick": 0,
  "nextQueueNo": 1,
  "stockTargets": [],
  "facilityQueues": [
    {"facilityId":"FAC_BLACKSMITH","stoppedReason":"P09_FACILITY_LOCKED","jobs":[],"lastCompletion":null},
    {"facilityId":"FAC_ALCHEMY","stoppedReason":"P09_FACILITY_LOCKED","jobs":[],"lastCompletion":null},
    {"facilityId":"FAC_INFIRMARY","stoppedReason":"P09_FACILITY_LOCKED","jobs":[],"lastCompletion":null}
  ],
  "events": []
}
```

stock target는 content 기본값을 snapshot해 이후 밸런스 변경에도 사용자 설정을 보존한다. queue job은 다음 필드를 가진다.

- `jobId`, `queueNo`, `jobKind` (`CRAFT|TREATMENT`)
- craft: `recipeId`, treatment: `targetMercenaryInstanceId`
- `quantity`, `ticksTotal`, `ticksRemaining`, `status`
- `reservedMaterials[] {itemId, quantity}`
- `outputKind`, `outputId`, `outputQuantity`
- `enqueuedOperationId`, `startedAtTick`

### 7.2 P08→P09

마이그레이션은 다음을 단일 commit으로 수행한다.

1. game/content version을 P09/.7로 변경한다.
2. `payload.production` 기본 구조와 content 기본 목표를 추가한다.
3. `payload.economy.store.supplyState.mode=PRODUCTION_OWNED`로 변경한다.
4. 기존 `SYSTEM_SUPPLY`, `MERCENARY_SALE` 재고와 장부를 그대로 보존한다.
5. 기존 시설/NPC 상태, 인벤토리, 용병, operation journal을 보존한다.
6. 무결성 해시는 Save repository commit 단계에서 재계산한다.

Migration은 동일 입력에 동일 payload를 만들며, 이미 .7인 문서에는 적용하지 않는다.

### 7.3 복구

- 예약 재료와 큐는 항상 같은 Save revision에 존재하므로 재기동 시 추가 차감하지 않는다.
- `RUNNING` 작업은 남은 tick에서 재개한다.
- 결과 입고와 큐 제거는 같은 commit이므로 완료 이중 지급이 없다.
- 손상된 queueNo, 중복 jobId, 음수 tick, 존재하지 않는 target/recipe는 저장 검증 실패 후 기존 백업 복구 경로를 사용한다.

## 8. UI/UX 최종 설계

화면 ID는 `P09_PRODUCTION_SCREEN`, 진입 ID는 `P09_CRAFT_NAV_BUTTON`이다. Bootstrap 공통 HUD의 `제작` 버튼으로 열며, ScreenCanvas 위 전면 패널로 표시한다.

### 8.1 화면 구조

- 상단: `왕국 생산`, 현재 revision/tick, 닫기
- 좌측 34%: 시설 카드 3개(대장간/연금/진료소), 상태/레벨/NPC/숙련도/XP
- 중앙 36%: 선택 시설 큐, 진행 bar, 남은 tick, 중단 사유와 해결 문구
- 우측 30%: 재고 목표 목록, 현재/큐/목표, `-`, `+`, 자동 보충 토글
- 하단: `자동 보충 1회`, `생산 10 tick 진행`, 결과 toast

한 화면에서 상태를 설명하되 조작 버튼은 64px 이상, 주요 버튼은 72px 이상이다. 16:9~20:9 Safe Area를 지키고 텍스트 대비는 4.5:1 이상이다.

### 8.2 상태

- Loading: skeleton과 입력 잠금
- Content: 정상 카드/큐/목표
- Empty: 큐가 비었으나 생산 가능
- Locked: 시설 미건설
- Error: 코드와 재시도
- Offline: 로컬 생산은 가능하므로 네트워크 불필요 안내

중단 사유는 색만으로 전달하지 않고 아이콘, 제목, 해결 문구를 함께 표시한다. 애니메이션은 220ms 이내이며 Reduce Motion에서는 즉시 전환한다.

### 8.3 비주얼 기준

- 따뜻한 다크 판타지 픽셀풍: 숯색 배경, 황동 테두리, 양피지 텍스트, 녹색 정상, 호박색 경고
- 무료/자체 생성 단색 텍스처와 기존 TMP 폰트만 사용
- 기존 P08 상점과 동일한 정보 밀도 및 색 토큰을 유지
- 상용 게임의 완성도는 정보 계층·피드백·일관성의 기준일 뿐 화면/아트 복제 금지

## 9. 오류 및 경계

- quantity 1~99, target/priority 도메인 외 값은 거부 또는 명시된 emergency floor로 보정한다.
- recipe/product/facility/NPC ID는 ordinal 대소문자 구분 안정 ID다.
- 모든 정수 연산은 checked 또는 safe integer 상한을 사용한다.
- 큐는 facilityId, queueNo 순; 목표는 priority, targetId 순; 장비 입고는 instanceId 순으로 정렬한다.
- P08 `RefreshSystemStoreSupply`는 `PRODUCTION_OWNED`에서 `P08_SUPPLY_ALREADY_APPLIED`를 반환한다.
- 생산품 sourceType은 `PRODUCTION`이며 기존 소비 순서 `MERCENARY_SALE → PRODUCTION → SYSTEM_SUPPLY`를 유지한다.

## 10. 검증 계약

### 10.1 정적/Content

- generator generate/check byte equality
- manifest 78 table, FK/PK/domain/hash/row count 검증
- Save .6→.7 schema registry와 new-game/migration goldens
- 모든 Unity Asset의 `.meta`

### 10.2 EditMode

1. 시설/NPC/레시피 자격 정책
2. 재료 효율 ceil과 원자 예약
3. 큐 한도/FIFO/tick 분할 결정성
4. 포션과 장비 상점 입고
5. 재고 목표 자동화와 emergency floor
6. 숙련 XP/승급
7. 치료 완료
8. replay/hash/revision 충돌
9. P08 migration 재고/장부 보존
10. schema와 custom invariant 검증

### 10.3 PlayMode

- Bootstrap 제작 진입 버튼 유일성
- 화면 open/close와 Content/Locked/Error 상태
- 필수 stable ID와 64px touch target
- 자동 보충, tick 버튼이 presenter를 통해 service를 호출
- 1920x1080, 2400x1080 Safe Area

### 10.4 배포 증거

- `docs/reports/P09_IMPLEMENTATION_REPORT.md`
- `docs/reports/captures/P09/*.png`
- `docs/goldens/P09/*.json`
- `scripts/ci/p09.sh`
- `KingdomTycoon-P09-Development.apk` 빌드 진입점

## 11. P10 인계 경계

P10은 P09가 만든 생산 장비와 인벤토리를 입력으로 받아 강화, 제련, 분해를 추가한다. P10은 P09 큐, 목표, 예약 재료, `PRODUCTION` 재고 출처와 숙련 XP를 재해석하거나 삭제하지 않는다. 생산 취소/환불이 필요하면 별도 명령과 migration 계약을 먼저 설계한다.

## 12. 최종 완결성 감사

| 항목 | 결과 |
|---|---|
| 목적/범위/비범위 | 확정 |
| P08 경계 및 system supply 전환 | 확정 |
| Content/Save/Migration/복구 | 확정 |
| 원자성/결정성/idempotency | 확정 |
| 시설/NPC/레시피 책임 | 확정 |
| 자동화/중단 사유/치료 | 확정 |
| UI 상태/접근성/모바일 | 확정 |
| 테스트/CI/Android/증거 | 확정 |
| P10 인계 | 확정 |
| 미결정 사항 | 없음 |

결론: 본 문서는 P09 구현과 인수 테스트를 시작하기에 완결되었다.
