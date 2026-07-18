# P04 왕국·시설 최종 통합 설계 요청서

> 이 문서는 ChatGPT에 그대로 전달해 단일 상세 설계서
> `TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md`를 받기 위한 요청서다.
> 답변은 설명·예시 일부가 아니라 Codex가 추가 설계 질문 없이 P04 구현, 테스트,
> Android 빌드까지 끝낼 수 있는 완전한 구현 계약이어야 한다.

## 0. 목적

현재 저장소는 P03에서 canonical content 60개 table, strict Save v1, 원자 저장·복구,
Unity AppRoot와 Kingdom 빈 씬까지 구현했다. P04의 목표는 다음이다.

- 왕국 월드에 핵심 시설 8종을 표시한다.
- 시설의 잠금, 건설 가능, 건설 중, 가동, 업그레이드 중, 정지 상태를 구현한다.
- 시설 선택 Drawer에서 현재 상태, 문제 원인, 비용, 다음 행동을 제공한다.
- 관리 시설은 올바른 직종의 NPC 배치 여부에 따라 가동 상태가 결정된다.
- 모든 변경을 strict Save v1에 결정적으로 저장하고 다시 로드한다.
- P05 이후 실제 모집·생산·상점·제작 기능은 구현하지 않는다.

기존 문서는 방향과 데이터 구조는 제공하지만, 상태 전이와 런타임 초기화에 필요한
결정값이 부족하다. 아래 항목을 임의로 추정하면 Save 호환성, 경제, 오프라인 정산,
튜토리얼, 후속 Phase의 작업 경계가 달라진다. 따라서 이 문서의 모든 질문에 한 번에
답해야 P04 애플리케이션 소스 구현을 시작한다.

## 1. 저장소에서 이미 검증된 기준선

아래 내용은 새 설계가 명시적으로 `CORRECTION`으로 선언하지 않는 한 유지한다.

### 1.1 Phase와 범위

- 선행 기준: `P03_CONTENT_PIPELINE_SAVE`, PR #15, commit `9862909`
- 현재 이슈: #16 `클라이언트: P04 왕국·시설 상태 흐름과 Drawer 구현`
- P04 목적: Kingdom scene, facility domain, UI, 시설 상태 흐름 검증
- 1.0 시설 수: 정확히 8개
- 왕국 단계: `KINGDOM_1`부터 `KINGDOM_5`
- 시설 레벨: 1부터 4
- P04 제외: P05 로스터, P07 인벤토리 루프, P08 상점 거래, P09 이후 실제 제작,
  P15 오프라인 정산·튜토리얼 실행, P16 서버 API

### 1.2 시설 ID와 운영 방식

| facilityId | 표시명 | operationMode | requiredProfessionId |
|---|---|---|---|
| `FAC_TAVERN` | 주점 | `SYSTEM` | `null` |
| `FAC_LODGE` | 용병 숙소 | `SYSTEM` | `null` |
| `FAC_GUILD` | 모험가 길드 | `SYSTEM` | `null` |
| `FAC_STORE` | 상점 | `MANAGED` | `NPC_MERCHANT` |
| `FAC_BLACKSMITH` | 대장간 | `MANAGED` | `NPC_BLACKSMITH` |
| `FAC_ALCHEMY` | 연금술 공방 | `MANAGED` | `NPC_ALCHEMIST` |
| `FAC_WAREHOUSE` | 창고 | `SYSTEM` | `null` |
| `FAC_INFIRMARY` | 치료소 | `MANAGED` | `NPC_HEALER` |

### 1.3 기존 Save v1 시설 계약

`payload.facilities`는 정확히 8개이며 각 객체는 다음 필드를 필수로 가진다.

```text
facilityId
level                 // integer 1..4, 현재 schema는 0을 허용하지 않음
state                 // LOCKED | BUILDABLE | BUILDING | ACTIVE | UPGRADING | STOPPED
assignedNpcInstanceId // UUID 또는 null
job                   // FacilityJob 또는 null
storage               // FacilityStorageEntry[]
```

`FacilityJob`의 기존 계약:

```text
operationId           // UUIDv7
jobType               // BUILD | UPGRADE | PRODUCTION | CRAFT | TREATMENT
status                // RUNNING | READY | CLAIMED | CANCELLED
recipeId              // stable ID 또는 null
targetLevel           // 1..4 또는 null
treatmentTargetInstanceId // UUID 또는 null
contentVersion
startedAtUtc
finishesAtUtc
claimedAtUtc          // UTC 또는 null
cancelledAtUtc        // UTC 또는 null
cycleCount            // safe integer >= 1
inputSnapshot         // AssetAmount[]
outputSnapshot        // RewardSnapshot[]
```

`payload.managementNpcs`의 각 객체는 `instanceId`, `professionId`, `proficiencyId`,
`proficiencyExp`, `assignedFacilityId`, `working`을 가진다.

`payload.operationJournal`은 `FACILITY_JOB`과 `facilityJobType`을 이미 지원한다.

### 1.4 기존 canonical data

- content manifest: contract v2, schema set v2
- 현재 contentVersion: `1.0.0-content.1`
- `facilities.csv`: 8 rows
- `facility_levels.csv`: 시설당 level 1..4, 총 32 rows
- `facility_upgrade_materials.csv`: level 2..4 비용 재료, 총 40 rows
- `kingdom_stages.csv`: 단계별 facility level cap 제공
- `npc_professions.csv`: 관리 직종 4개
- `npc_proficiency_levels.csv`: 견습·숙련·장인·명장 4단계
- 시설 level 데이터에는 왕국 골드 비용과 효과 표시 key가 있으나 작업 시간은 없다.
- P03 package는 exact CRLF bytes와 manifest SHA-256으로 고정돼 있다.

### 1.5 Unity 기준선

- Unity `6000.3.20f1`
- uGUI + TextMeshPro, 1920×1080, 16:9~20:9 Safe Area
- `Bootstrap`, `Kingdom`, `Region`, `Raid` 씬
- 영속 `AppRoot`와 `WorldCanvas`, `HudCanvas`, `ScreenCanvas`, `DrawerCanvas`,
  `ModalCanvas`, `ToastCanvas`, `TutorialCanvas`, `DebugCanvas`
- Presentation/Application/Domain/Infrastructure 계층
- View는 Save, Catalog, HTTP API를 직접 변경·호출하지 않는다.
- 씬·프리팹은 Unity Editor API로 생성하고 YAML을 손으로 만들지 않는다.
- 현재 Kingdom 씬은 제목만 표시하는 `SampleKingdomScreen`이다.
- 시설·왕국용 production asset은 아직 없다.

## 2. 답변 공통 규칙

1. 답변 파일명은 정확히 `TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md`로 한다.
2. 모든 규칙에 `CONFIRMED`, `TUNABLE`, `DEFERRED`, `OPS_LATER`, `REFERENCE_ONLY`,
   `UNRESOLVED` 중 하나를 표시한다.
3. P04 구현에 필요한 항목을 `UNRESOLVED`로 남기지 않는다.
4. 값이 조정 가능하면 `TUNABLE`로 표시하되 정확한 초기값과 저장 위치를 제공한다.
5. 기존 P03 계약을 변경할 때는 `CORRECTION` 또는 `MIGRATION`으로 표시하고 이유,
   호환 전략, golden fixture를 제공한다.
6. 시설별 규칙을 “등”으로 생략하지 말고 8개 모두 행으로 제공한다.
7. 상태·명령·오류·이벤트·UI 문구는 exact enum/ID/string을 제공한다.
8. 시간은 UTC `yyyy-MM-dd'T'HH:mm:ss.fff'Z'`, ID는 영문 대문자 스네이크 케이스,
   instance ID는 기존 UUID/UUIDv7 계약을 유지한다.
9. 모든 비용과 보상은 safe integer 범위를 유지하고 음수를 허용하지 않는다.
10. 실제 생산·모집·판매·제작 기능은 후속 Phase로 남긴다. P04에서는 시설 상태와
    해당 기능으로 진입할 수 없는 이유를 명확히 표현한다.
11. 새로운 CSV 또는 기존 CSV 변경이 필요하면 exact header, 전체 row, manifest 변경,
    contentVersion을 모두 제공한다.
12. 구현자가 선택하도록 `A 또는 B`를 남기지 말고 권장안 하나를 최종 확정한다.

## 3. 시설 상태 모델과 불변식

### 3.1 현재 누락

- `level` 최소값이 1인데 미건설 시설도 `LOCKED/BUILDABLE`일 수 있어 level 의미가 불명확하다.
- `state`와 `job.status`, NPC 배치 상태의 허용 조합이 없다.
- 앱 시작, 시간 경과, NPC 해제, 왕국 단계 변경 시 상태 재계산 우선순위가 없다.
- `ACTIVE`와 `STOPPED`의 정확한 기준이 없다.

### 3.2 반드시 확정할 내용

다음을 exact transition matrix로 제공한다.

- 모든 출발 상태 × command/event × guard × 도착 상태
- `LOCKED`, `BUILDABLE`, `BUILDING`, `ACTIVE`, `UPGRADING`, `STOPPED` 각각의 불변식
- 미건설 시설의 `level` 저장 의미
- `state`가 저장 권위인지 파생값인지
- `job.status=RUNNING/READY/CLAIMED/CANCELLED`와 facility state 조합
- SYSTEM 시설과 MANAGED 시설의 ACTIVE/STOPPED 차이
- 잘못된 조합을 로드했을 때 fail closed, repair, migration 중 하나의 exact 처리
- 동일 command 재시도와 중복 operationId 처리
- 완료 시간이 지난 RUNNING job의 앱 재시작 처리

필수 출력 표:

```text
CurrentFacilityState | Trigger | Preconditions | FacilityJobBefore |
Mutation | FacilityJobAfter | NextFacilityState | DomainEvent | ErrorCode
```

또한 다음 invariant 표를 제공한다.

```text
InvariantId | AppliesTo | ExactRule | ValidationTiming | ErrorCode
```

## 4. 신규 게임과 P03 Save의 P04 초기화

### 4.1 현재 누락

P03 테스트 fixture는 왕국 골드 0, 관리 NPC 0명, 8개 시설 모두 level 1 + `LOCKED`다.
런타임에는 새 프로필 생성기와 기본 Save 작성 흐름이 아직 없다. 이 상태로는 P04 화면에서
어떤 시설도 건설할 수 없다.

### 4.2 반드시 확정할 내용

- 신규 게임 `Save v1`의 P04 관련 exact payload
- 시작 `kingdomGold`
- 8개 시설 각각의 `level/state/job/assignedNpc/storage`
- 시작 관리 NPC instance 수와 직종, 숙련도, 배치 여부
- 시스템 시설 4종의 시작 건설 여부
- 관리 시설 4종의 잠금/건설 가능 여부
- P03 Save를 처음 P04로 로드할 때 content migration 또는 별도 bootstrap normalization
- 기존 P03 fixture를 변경하는지, 별도 P04 fixture를 추가하는지
- profileId/saveId/NPC instanceId 생성 시점과 UUID 계약
- 새 게임 저장이 실패했을 때 UI 상태와 재시도 규칙

필수 출력:

1. `P04_NEW_GAME_GOLDEN` 전체 JSON
2. `P03_TO_P04_GOLDEN_BEFORE` 전체 JSON
3. `P03_TO_P04_GOLDEN_AFTER` 전체 JSON
4. 변경 전후 canonical payload SHA-256 또는 해시 계산 절차와 기대값
5. 초기화 단계별 error code

## 5. 잠금·건설 가능·왕국 단계 규칙

### 5.1 현재 누락

모든 시설 level 1의 `required_kingdom_stage_id`는 `KINGDOM_1`이지만, 8개 시설이 시작부터
동시에 건설 가능한지, 시스템 시설이 기본 지급인지, 관리 시설과 NPC 후보가 언제 열리는지
확정돼 있지 않다.

### 5.2 반드시 확정할 내용

시설 8개 × 왕국 5단계의 exact availability matrix를 제공한다.

```text
facilityId | kingdomStageId | visible | unlocked | buildable |
maxLevel | requiredProgressionFlagOrCondition | lockedReasonCode
```

추가로 확정한다.

- 왕국 단계가 올라갈 때 잠긴 시설의 전이
- 단계가 내려가는 상황을 금지할지와 로드 시 처리
- level cap과 `facility_levels.required_kingdom_stage_id`가 충돌할 때 우선순위
- 잠긴 시설을 숨길지 silhouette로 표시할지
- 개방 직전 진행도 표시 기준
- 실제 왕국 단계 승급 command는 P04에 포함하는지, read-only projection만 제공하는지

## 6. 건설·업그레이드 시간과 완료·취소

### 6.1 현재 누락

기존 `facility_levels.csv`에는 골드와 재료만 있고 build/upgrade duration이 없다.
시간 값이 없으면 `BUILDING/UPGRADING` 상태와 Save의 `startedAtUtc/finishesAtUtc`를
결정할 수 없다.

### 6.2 반드시 확정할 내용

- 시설별 level 1 건설 시간과 level 2..4 업그레이드 시간
- Development/PlayMode 테스트용 시간 주입 규칙
- authoritative clock interface와 UTC 단조성 처리
- 앱이 종료된 동안의 완료 판정과 P15 오프라인 정산의 경계
- 완료 즉시 자동 claim인지, 사용자가 완료 버튼을 눌러야 하는지
- 취소 가능 여부, 취소 가능 시 골드·재료 환급률과 operation journal 처리
- 앱 강제 종료 시 input snapshot과 중복 차감 방지
- 시계가 뒤로 이동했을 때 오류 또는 보수적 처리

권장 데이터가 필요하면 다음 exact schema와 전체 32 rows를 제공한다.

```csv
facility_id,level,build_or_upgrade_duration_seconds,cancel_refund_ratio,status,enabled
```

다른 schema를 선택해도 되지만 exact header, 전체 rows, PK/FK/domain, validator error code를
반드시 제공한다.

## 7. 건설·업그레이드 비용과 원자성

### 7.1 현재 누락

골드와 재료 비용은 있지만 P04에서 어떤 wallet/inventory를 권위로 차감하고, 실패·재시도·
취소 시 어떻게 복구하는지 없다. P08 경제 시스템을 선행 구현하지 않으면서도 시설 비용은
원자적으로 처리해야 한다.

### 7.2 반드시 확정할 내용

- 왕국 골드와 inventory item stack 차감 순서
- 모든 guard를 통과한 뒤 한 번에 적용하는 transaction boundary
- 부족 자원별 exact error code
- `inputSnapshot`의 assetType/assetId/amount exact 표현
- operation journal의 `requestHash`, `resultDigest`, status 전이
- 예상 Save revision 충돌 처리
- 중복 operationId, 동일 requestHash, 다른 requestHash 조합의 결과
- `facilityUpgradeCount` 증가 시점과 BUILD 포함 여부
- 비용 0인 건설/업그레이드의 snapshot 표현
- P04에서는 서버 원장을 호출하지 않는다는 경계

필수 command 계약:

```text
StartFacilityBuild
StartFacilityUpgrade
ClaimFacilityJob
CancelFacilityJob // 지원하지 않으면 명시적으로 REJECTED
```

각 command마다 input DTO, output DTO, precondition, mutation, error code, idempotency를 표로
제공한다.

## 8. 관리 NPC 후보·고용·배치

### 8.1 현재 누락

문서에는 시설 권한과 NPC 고용 후보가 동시에 개방된다고 되어 있지만, candidate/hire data와
비용, P04에서의 생성 방식이 없다. Save는 보유 관리 NPC와 배치만 표현한다.

### 8.2 반드시 확정할 내용

- P04에서 NPC “고용”까지 구현하는지 또는 확정된 starter NPC만 사용하는지
- 4개 관리 직종별 starter/candidate 생성 수
- NPC instanceId 생성, 초기 proficiency, XP, working 값
- 같은 직종 NPC를 여러 명 보유할 수 있는지
- 한 NPC의 동시 다중 배치 금지
- 잘못된 직종 배치 금지
- 건설 중/업그레이드 중 시설에 배치 가능한지
- NPC 배치·해제 후 시설 상태 재계산
- 배치 해제 중 진행 중 job 처리
- tutorial 지급 NPC와 P15 경계

필수 command 계약:

```text
AssignManagementNpc
UnassignManagementNpc
```

실제 고용을 P04에서 제외한다면, 플레이 가능한 P04 fixture가 어떻게 정확히 4개 관리 시설의
ACTIVE/STOPPED 흐름을 모두 검증하는지 golden scenario를 제공한다.

## 9. 시설 기능 효과와 후속 Phase 경계

### 9.1 현재 누락

`effect_key/effect_text_key`는 표시용 문자열만 제공한다. 숙소 슬롯처럼 P04에서 즉시 반영해야
할 효과와 상점·제작처럼 후속 Phase에서 구현할 효과의 경계가 없다.

### 9.2 반드시 확정할 내용

32개 facility level effect 각각을 다음 표로 분류한다.

```text
facilityId | level | effectKey | P04Behavior |
OwningPhase | ProjectionField | DeferredReason
```

`P04Behavior` enum은 최소 다음 중 하나로 exact 정의한다.

```text
APPLY_NOW | DISPLAY_ONLY | LOCKED_UNTIL_PHASE
```

특히 아래를 확정한다.

- 숙소 level과 `activeMercenaryLimit/ownedMercenaryLimit`의 동기화
- 시설 level cap과 왕국 stage의 동기화
- 주점·길드·창고의 P04 동작 범위
- 관리 시설의 실제 생산/거래/치료가 아직 없을 때 ACTIVE 표시 의미
- Drawer에서 후속 기능 버튼을 숨김, 잠금, 비활성 중 어느 방식으로 표현하는지

## 10. P04 contentVersion과 canonical package

### 10.1 현재 누락

P03 package는 `1.0.0-content.1`로 서명돼 있다. P04에 duration, UI string, placeholder asset,
새 progression rule을 추가하면 기존 package를 in-place 수정할 수 없다.

### 10.2 반드시 확정할 내용

- P04 contentVersion exact 값
- packageKind `BASE` 또는 `PATCH`
- baseContentVersion
- 변경·추가되는 table 전체 목록
- 각 table exact header와 전체 rows
- manifest 전체 JSON 또는 deterministic 생성 규칙
- 이전 package 보존 정책
- Save `contentVersion` migration과 alias 필요 여부
- P04 content package golden SHA-256
- generator가 동일 bytes를 재생성하는 수용 기준

최소 검토 대상:

- construction/upgrade duration rule
- P04 UI localization ko-KR/en-US
- P04 placeholder facility/world asset register
- 시설 availability/unlock rule
- P04 test/development runtime config

## 11. 왕국 월드와 placeholder asset 계약

### 11.1 현재 누락

8개 시설의 asset, 위치, 크기, hit area, sorting order, 상태 variant가 없다. 무료 에셋 최종 선택은
미정이므로 P04가 내부 placeholder를 쓸 수 있는지와 production asset 교체 경계가 필요하다.

### 11.2 반드시 확정할 내용

- P04에서 내부 제작 placeholder 사용 승인 여부
- 8개 시설 각각의 assetId/addressable address
- 왕국 배경, 건설 부지, 잠금 silhouette, 건설 중 overlay, 정지 경고 icon assetId
- 1920×1080 기준 exact world/screen 좌표 또는 normalized anchor
- facility sprite pixels-per-unit, reference pixel size, pivot
- render sorting layer/order
- 최소 터치 영역 64×64 적용 방식
- 16:9, 18:9, 20:9에서 crop/letterbox/reflow 규칙
- camera orthographic size와 Pixel Perfect 기준
- asset 교체 시 코드·Save·content ID가 변하지 않는 규칙

필수 출력 표:

```text
assetId | address | type | dimensions | pivot | pixelsPerUnit |
facilityIdOrRole | stateVariant | source | license | replacementContract
```

```text
facilityId | anchorX | anchorY | width | height | sortingOrder | labelPlacement
```

에셋은 Editor API가 생성하는 내부 SVG/Texture/Sprite/Prefab 중 하나로 exact 방식을 확정하고,
씬·프리팹 YAML 직접 작성은 금지한다.

## 12. Kingdom HUD·Drawer·내비게이션

### 12.1 현재 누락

기존 문서는 상단 HUD와 우측 Drawer의 정보 범주만 제공한다. 실제 component hierarchy,
크기, 탭, 버튼, 상태별 표시가 없다.

### 12.2 반드시 확정할 내용

- Kingdom 화면 object/component hierarchy
- HUD의 왕국 골드, 프리미엄 재화, 왕국 단계, 알림 표시 형식
- 하단 내비게이션 6개 중 P04에서 보이는 항목과 locked 처리
- 시설 선택/재선택/바깥 영역 터치/Android back 동작
- Drawer open/close width, animation duration/easing, focus order
- 운영·생산·직원·업그레이드 탭 중 P04 활성 범위
- 시설 상태, 효과, 비용, 남은 시간, NPC, 중단 원인, 주요 행동의 exact 배치
- 모달을 사용하는 행동과 Drawer 내부에서 끝나는 행동
- 동시에 하나의 modal만 허용하는 규칙
- 버튼 disabled 사유 표시 방식
- 한글 긴 문자열과 큰 텍스트 모드

필수 wireframe은 1920×1080 기준 ASCII 또는 Mermaid가 아니라 좌표 표와 hierarchy를 함께
제공한다.

```text
ElementId | ParentId | AnchorMin | AnchorMax | OffsetMin | OffsetMax |
MinTouchSize | VisibleWhen | InteractableWhen | LocalizationKey
```

## 13. Loading/Content/Empty/Error/Locked/Offline 상태

P04 화면과 Drawer에 대해 6개 공통 상태를 모두 정의한다.

```text
UiState | EntryCondition | WorldVisibility | DrawerContent |
PrimaryAction | SecondaryAction | LocalizationKey | AnalyticsEvent
```

다음을 포함한다.

- content manifest load 전 Loading
- 8개 시설 정상 표시 Content
- 필수 projection이 0개인 비정상 Empty
- catalog/Save validation 실패 Error
- 선택 시설이 LOCKED인 Locked
- 네트워크와 무관한 로컬 P04에서 Offline의 정확한 의미
- Error에서 Save를 자동 수정하지 않는 규칙
- DebugCanvas 진단 정보와 release build 노출 금지

## 14. Localization exact bundle

현재 canonical localization에는 시설명·level 효과와 일부 공통 문구만 있다. P04에서 필요한
모든 UI text key를 ko-KR/en-US 전체 row로 제공한다.

최소 범주:

- 6개 facility state 이름
- 건설/업그레이드/완료/취소/배치/해제 action
- 골드·재료 부족
- 왕국 단계 부족, 최대 level, 진행 중 작업
- NPC 없음, 잘못된 직종, 이미 배치됨
- 생산/거래/제작/치료의 후속 Phase 잠금 안내
- 남은 시간·완료 시간 format
- 6개 UI 상태
- Drawer 탭과 닫기/뒤로가기
- Save 생성/로드/저장 실패 및 재시도

필수 exact CSV 형식:

```csv
locale,text_key,text_value,context,status,enabled
```

문자열 interpolation placeholder 문법과 ko/en plural 처리도 확정한다.

## 15. Application/Domain/Presentation 계약

다음 계층과 type을 exact namespace, 책임, 공개 API 수준으로 제공한다. 클래스명은 구현자가
임의로 바꾸지 않도록 하나로 확정한다.

### Domain

- Facility entity/value object
- FacilityState/FacilityJobStatus/FacilityStopReason
- invariant validator
- transition policy
- domain events

### Application

- Kingdom screen query
- Facility detail query
- build/upgrade/claim command
- NPC assign/unassign command
- unit-of-work 또는 Save mutation boundary
- clock/ID provider interface

### Infrastructure

- canonical catalog projection
- Save v1 mapper/repository adapter
- localization lookup
- content migration adapter

### Presentation

- Kingdom screen presenter
- facility world view
- facility Drawer view
- state renderer
- Android back/input adapter

필수 출력:

```text
TypeName | Namespace | Layer | Responsibility | Dependencies | PublicMembers
```

View가 Save/Catalog를 직접 호출하지 않고 Presenter가 JObject를 UI에 노출하지 않도록 DTO 필드도
exact 정의한다.

## 16. Domain event와 error code registry

모든 P04 event와 error를 빠짐없이 제공한다.

필수 event 후보:

```text
FacilityUnlocked
FacilityBuildStarted
FacilityBuildCompleted
FacilityUpgradeStarted
FacilityUpgradeCompleted
FacilityStopped
FacilityActivated
ManagementNpcAssigned
ManagementNpcUnassigned
```

필수 error 범주:

```text
FACILITY_NOT_FOUND
FACILITY_STATE_INVALID
FACILITY_LOCKED
FACILITY_LEVEL_MAX
FACILITY_STAGE_REQUIRED
FACILITY_GOLD_INSUFFICIENT
FACILITY_MATERIAL_INSUFFICIENT
FACILITY_JOB_ALREADY_RUNNING
FACILITY_JOB_NOT_READY
FACILITY_NPC_REQUIRED
FACILITY_NPC_PROFESSION_MISMATCH
FACILITY_NPC_ALREADY_ASSIGNED
FACILITY_OPERATION_DUPLICATE
FACILITY_SAVE_REVISION_CONFLICT
```

이 이름을 그대로 채택하거나 정정안을 하나로 확정하고, 다음 표를 제공한다.

```text
Code | Trigger | UserMessageKey | Retryable | LoggingLevel | SaveMutationAllowed
```

## 17. Save write·load·recovery 계약

다음을 exact sequence diagram 또는 번호 순서로 확정한다.

- AppRoot service 초기화
- content load/validation
- profile discovery 또는 new game creation
- Save load/recovery/migration
- facility projection 생성
- command 실행과 expectedRevision 검사
- domain mutation
- integrity 재계산
- atomic Save
- UI event 발행
- 실패 시 in-memory rollback 또는 reload

추가로 확정한다.

- autosave 시점과 debounce
- 앱 pause/focus lost/quit 처리
- 한 command당 Save 횟수
- concurrent tap 차단
- recovered Save를 P04 UI에 알리는 방식
- P04에서 saveVersion 1 유지 여부
- schema 변경 여부와 migration registry 변경 여부

## 18. 테스트와 golden fixture

추가 설계 질문이 생기지 않도록 exact 테스트 목록을 제공한다.

### EditMode 필수

- 6개 상태 전체 허용/거부 전이
- 8개 시설 SYSTEM/MANAGED 불변식
- 32개 level 비용·stage cap 조회
- 골드·재료 원자 차감과 부족 실패
- operationId 중복·revision 충돌
- 작업 시간 경계 `now <`, `==`, `>` finishesAtUtc
- NPC 직종 일치·불일치·중복 배치
- P03→P04 migration/normalization
- Save commit 후 load round trip
- 잘못된 state/job 조합 fail closed
- content package hash/golden

### PlayMode 필수

- Bootstrap → Kingdom → 8개 시설 표시
- 시설 선택 → Drawer open → 재선택 → close
- LOCKED/BUILDABLE/BUILDING/ACTIVE/UPGRADING/STOPPED 시각 상태
- 건설 시작 → 시간 진행 → 완료 → 재로드
- NPC 배치/해제 → STOPPED/ACTIVE
- Loading/Content/Empty/Error/Locked/Offline
- Android back, 단일 modal, 최소 터치 영역
- 16:9, 18:9, 20:9 Safe Area와 긴 한글

필수 출력:

```text
TestId | TestClass | Mode | Given | When | Then | Fixture | Timeout
```

모든 테스트에 deterministic clock과 deterministic ID provider 사용 규칙을 제공한다.

## 19. 성능·수명주기 수용 기준

다음을 수치로 확정한다.

- 8개 facility view의 초기 생성 횟수와 이후 재사용
- UI event subscription/unsubscription 시점
- polling 금지 범위와 남은 시간 표시 update 주기
- command 처리 중 GC allocation 권장 상한
- Drawer animation 중 frame target
- 10분 Kingdom 화면에서 메모리 지속 증가 허용 기준
- scene 이동 후 presenter/view/service reference 정리
- Android Development Build profiler capture 항목

## 20. P04 Editor asset·scene generation과 빌드 계약

다음을 exact entrypoint와 검증 순서로 확정한다.

- P04 placeholder asset generator class/method
- Kingdom scene configurator class/method
- generated asset verify class/method
- idempotent 재실행 수용 기준
- Addressables group/label/address 규칙
- Android builder class/method와 output APK 이름
- build 전 content/asset/scene guard
- scene YAML·prefab YAML 직접 편집 금지
- generated asset의 `.meta` 동시 커밋

권장 명령 형태를 실제 class 이름으로 완성한다.

```powershell
Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.<P04_SETUP_METHOD> `
  -logFile .\client-unity\Logs\p04-setup.log

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform EditMode `
  -testResults .\client-unity\Logs\p04-editmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform PlayMode `
  -testResults .\client-unity\Logs\p04-playmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.<P04_BUILD_METHOD> `
  -logFile .\client-unity\Logs\p04-android-build.log
```

## 21. 수동 검증과 화면 캡처

P04 완료 보고에 넣을 exact capture 목록을 제공한다.

최소:

- 1920×1080 Kingdom 전체 화면
- 20:9 Safe Area 화면
- LOCKED 시설 Drawer
- BUILDABLE 시설 비용/행동
- BUILDING 또는 UPGRADING 남은 시간
- ACTIVE SYSTEM 시설
- STOPPED MANAGED 시설의 NPC 없음 원인
- NPC 배치 후 ACTIVE
- Error 또는 Save recovery 알림

각 capture에 필요한 fixture ID, 해상도, 파일명, 성공 조건을 표로 제공한다.

## 22. 구현 파일 지도와 커밋 분리

예상 경로를 기준으로 최종 파일 지도를 제공한다.

```text
client-unity/Assets/KingdomTycoon/Runtime/Domain/Facilities/
client-unity/Assets/KingdomTycoon/Runtime/Application/Facilities/
client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Facilities/
client-unity/Assets/KingdomTycoon/Runtime/Presentation/Kingdom/
client-unity/Assets/KingdomTycoon/Editor/
client-unity/Assets/KingdomTycoon/Tests/EditMode/
client-unity/Assets/KingdomTycoon/Tests/PlayMode/
client-unity/Assets/StreamingAssets/Content/
docs/reports/P04_KINGDOM_FACILITIES_REPORT.md
```

필수 출력:

```text
Path | NewOrModified | Responsibility | GeneratedOrAuthored | OwningCommit
```

논리적 커밋은 최소 다음 목적을 섞지 않는다.

1. 최종 설계 채택과 canonical data
2. facility domain/application/save integration
3. Kingdom world/Drawer와 generated assets
4. EditMode/PlayMode test와 build guard
5. CI·문서·완료 보고

## 23. 최종 답변 구조

답변 문서는 다음 순서를 정확히 따른다.

1. 문서 상태·버전·권위
2. P04 범위와 Phase 경계
3. correction/migration 요약
4. 시설 상태 모델과 transition matrix
5. 신규 게임·P03→P04 golden
6. 잠금·왕국 단계 availability matrix
7. 건설·업그레이드 시간·취소 규칙
8. 비용·원자성·idempotency
9. 관리 NPC 생성·배치
10. 32개 facility effect Phase 분류
11. P04 content package exact 계약과 전체 변경 rows
12. world/asset/layout exact 계약
13. HUD/Drawer/navigation hierarchy와 좌표
14. 6개 UI 상태
15. ko-KR/en-US localization 전체 rows
16. Domain/Application/Infrastructure/Presentation type 계약
17. event/error registry
18. Save sequence와 recovery
19. 테스트·golden 전체 목록
20. 성능·수명주기
21. Editor generation·Addressables·Android build
22. 수동 검증·capture 목록
23. 구현 파일 지도·커밋 분리
24. P04 완료 판정 checklist
25. `UNRESOLVED` — 반드시 `NONE`

## 24. 한 번에 끝내는 최종 수용 게이트

아래 조건을 모두 충족해야 Codex가 구현을 재개한다.

- [ ] 8개 시설 × 6개 상태의 허용 전이와 불변식이 완전하다.
- [ ] level 1 미건설 표현과 Save state/job 조합이 모순 없이 확정됐다.
- [ ] 신규 게임과 P03 Save 전환의 전체 golden JSON이 있다.
- [ ] 시작 골드·시설·관리 NPC가 exact 값으로 확정됐다.
- [ ] 8×5 availability matrix가 있다.
- [ ] 32개 build/upgrade duration과 완료·취소 규칙이 있다.
- [ ] 골드·재료 차감, journal, revision, idempotency가 원자적으로 정의됐다.
- [ ] NPC 배치·해제와 직종 불변식이 확정됐다.
- [ ] 32개 facility effect의 P04/후속 Phase 경계가 확정됐다.
- [ ] P04 contentVersion과 변경 table 전체 rows·manifest 규칙이 있다.
- [ ] 내부 placeholder asset과 8개 시설 위치·상태 variant가 확정됐다.
- [ ] HUD/Drawer/navigation hierarchy와 좌표·상호작용이 있다.
- [ ] Loading/Content/Empty/Error/Locked/Offline가 모두 정의됐다.
- [ ] ko-KR/en-US P04 localization 전체 rows가 있다.
- [ ] 계층별 type/DTO/command/query/event/error 계약이 있다.
- [ ] Save write/load/recovery와 rollback 순서가 있다.
- [ ] EditMode/PlayMode exact 테스트와 deterministic fixture가 있다.
- [ ] Editor asset/scene generator와 Android build entrypoint가 있다.
- [ ] 수동 capture 목록과 P04 완료 checklist가 있다.
- [ ] P04 구현 필수 `UNRESOLVED`가 `NONE`이다.

이 수용 게이트를 만족한 단일 설계서를 받으면 Codex는 추가 설계 요청 없이 같은 이슈 #16과
브랜치에서 canonical data 갱신 → domain/application → Save integration → Kingdom UI/asset →
EditMode/PlayMode → Android build → 화면 캡처 → P04 완료 보고 → Draft PR 갱신까지 한 흐름으로
진행한다.
