# TYCOON P04 왕국·시설 최종 통합 설계 v1.0

## 1. 문서 상태·버전·권위

**상태: CONFIRMED**

| 항목 | 값 |
|---|---|
| 문서 | TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md |
| 기준 commit | 9862909 |
| 대상 | Issue #16 / P04_KINGDOM_FACILITIES |
| 상위 권위 | 저장소 CONFIRMED 설계 → P03 strict Save/content 계약 → 본 문서 CORRECTION/MIGRATION |
| 설계 시각 | 2026-07-19T00:00:00.000Z |
| 구현 승인 | 본 계약 수용 뒤 별도 구현 작업 |
| saveVersion | 1 유지 |
| P04 contentVersion | 1.0.0-content.2 |
`CONFIRMED`는 호환 계약, `TUNABLE`은 지정 위치의 값만 변경 가능, `DEFERRED`는 후속 Phase, `OPS_LATER`는 운영 기능, `REFERENCE_ONLY`는 설명 전용이다. 각 절 머리의 상태는 그 절의 모든 문장·표 행에 적용하며, 행이 별도 status를 가지면 행 값이 우선한다. P04 필수 결정에는 `UNRESOLVED`가 없다.

## 2. P04 범위와 Phase 경계

**상태: CONFIRMED**

| 기능 | 판정 | Owning Phase | P04 결과 |
|---|---|---|---|
| 8개 시설 월드/Drawer/상태 | CONFIRMED | P04 | 완전 구현 |
| 건설·업그레이드·claim·NPC 배치 | CONFIRMED | P04 | 로컬 Save 원자 처리 |
| 용병 로스터 | DEFERRED | P05 | 숙소 한도 projection만 |
| 인벤토리 UI/창고 루프 | DEFERRED | P07 | 건설 재료 stack 차감 adapter만 |
| 상점 거래 | DEFERRED | P08 | ACTIVE 표시와 잠금 안내만 |
| 생산/치료 | DEFERRED | P09 | job 생성 금지 |
| 제작/연금술 | DEFERRED | P10 | job 생성 금지 |
| 길드 진행 | DEFERRED | P11 | 효과 표시만 |
| 모집 | DEFERRED | P13 | 주점 기능 버튼 disabled |
| 오프라인 정산/튜토리얼 실행 | DEFERRED | P15 | 경과시간 READY 판정만 |
| 서버 API/원장 | DEFERRED | P16 | HTTP 호출 0회; premium은 cache read-only |
## 3. correction/migration 요약

**상태: CONFIRMED**

| ID | 종류 | 확정 변경 | 호환/이유 |
|---|---|---|---|
| P04-C01 | CORRECTION | 미건설 level=1은 level-1 blueprint/목표를 뜻한다. | level=0 없이 strict v1 유지 |
| P04-C02 | CORRECTION | state는 저장된 materialized state이며 아래 불변식의 권위값이다. | load 때 재계산으로 숨은 수정을 하지 않음 |
| P04-C03 | CORRECTION | RUNNING 만료는 READY로 원자 저장; claim은 수동. | P15 정산과 분리 |
| P04-C04 | CORRECTION | player CancelFacilityJob은 항상 REJECTED. | 환급/부분 rollback 경제를 P04에 도입하지 않음 |
| P04-C05 | CORRECTION | STOPPED는 BUILD/UPGRADE CLAIMED와 UPGRADE CANCELLED terminal job을 정확히 한 revision 허용. | v1.2.1 terminal matrix를 MANAGED NPC 상태와 양립시킴; JSON shape/saveVersion 불변 |
| P04-M01 | MIGRATION | 엄격한 P03 bootstrap signature만 새 게임 seed로 1회 정규화. | contentVersion .1→.2 및 revision +1 |
| P04-M02 | MIGRATION | 62-table BASE, schema set 3 추가. | P03 .1 package 불변 보존; alias 불필요 |
## 4. 시설 상태 모델과 transition matrix

**상태: CONFIRMED**

### 4.1 저장 의미와 우선순위

`level`은 완료된 건물에서는 현재 레벨, 미건설(`LOCKED|BUILDABLE|BUILDING`)에서는 level-1 blueprint 값 `1`이다. BUILD job은 `targetLevel=1`; UPGRADE는 `targetLevel=level+1`이다. 판정 순서는 schema/FK → job/state → level/stage → NPC 양방향 참조 → working/state이다. stage는 단조 증가이며 감소 입력은 거부한다.

| InvariantId | AppliesTo | ExactRule | ValidationTiming | ErrorCode |
|---|---|---|---|---|
| FAC-INV-001 | ALL | 8 facilityId가 각 1회; level 1..4 | load/pre-commit | SAVE_FACILITY_SET_INVALID |
| FAC-INV-002 | LOCKED | level=1; job=null; assigned=null | load/pre-commit | SAVE_FACILITY_STATE_JOB_MISMATCH |
| FAC-INV-003 | BUILDABLE | level=1; unlock true; job=null 또는 BUILD/CANCELLED terminal; assigned=null | load/pre-commit | SAVE_FACILITY_STATE_JOB_MISMATCH |
| FAC-INV-004 | BUILDING | level=1; BUILD targetLevel=1; status RUNNING\|READY; assigned=null | load/pre-commit | SAVE_FACILITY_STATE_JOB_MISMATCH |
| FAC-INV-005 | ACTIVE SYSTEM | job=null 또는 BUILD/UPGRADE CLAIMED terminal; assigned=null | load/pre-commit | SAVE_FACILITY_NPC_FORBIDDEN |
| FAC-INV-006 | ACTIVE MANAGED | job=null/terminal; matching NPC bidirectional; terminal이면 working=false, 아니면 true | load/pre-commit | SAVE_FACILITY_NPC_LINK_INVALID |
| FAC-INV-007 | STOPPED | MANAGED built; job=null 또는 BUILD/UPGRADE CLAIMED·UPGRADE CANCELLED terminal; no working NPC | load/pre-commit | SAVE_FACILITY_STOPPED_INVALID |
| FAC-INV-008 | UPGRADING | UPGRADE target=level+1; status RUNNING\|READY; assigned may remain; working=false | load/pre-commit | SAVE_FACILITY_STATE_JOB_MISMATCH |
| FAC-INV-009 | TERMINAL JOB | CLAIMED must reference journal COMMITTED; CANCELLED must reference FAILED_PERMANENT | recovery | SAVE_FACILITY_TERMINAL_JOURNAL_MISMATCH |
| FAC-INV-010 | TIME | started<=finishes; READY iff normalized now>=finishes | command/load normalization | FACILITY_CLOCK_INVALID |
| FAC-INV-011 | NPC | one NPC↔at most one facility; profession exact | load/pre-commit | SAVE_FACILITY_NPC_LINK_INVALID |
| FAC-INV-012 | STATE | stored state and projection recomputation must byte-match | load | SAVE_FACILITY_STATE_DERIVATION_MISMATCH |
| CurrentFacilityState | Trigger | Preconditions | FacilityJobBefore | Mutation | FacilityJobAfter | NextFacilityState | DomainEvent | ErrorCode |
|---|---|---|---|---|---|---|---|---|
| LOCKED | UnlockEligibilityChanged | stage>=required and enabled | null | state=BUILDABLE | null | BUILDABLE | FacilityUnlocked | — |
| LOCKED | StartBuild | always | null | none | null | LOCKED | — | FACILITY_LOCKED |
| BUILDABLE | StartFacilityBuild | expectedRevision; funds; operation new | null | journal PREPARED→COMMITTED; subtract snapshot | BUILD/RUNNING target=1 | BUILDING | FacilityBuildStarted | — |
| BUILDABLE | StartFacilityBuild retry | same opId + requestHash | null or same job | return stored result | unchanged | BUILDING | — | — |
| BUILDABLE | StartFacilityBuild retry | same opId + different hash | any | none | unchanged | BUILDABLE | — | FACILITY_OPERATION_HASH_MISMATCH |
| BUILDING | ClockObserved | now<finishes | BUILD/RUNNING | none | unchanged | BUILDING | — | — |
| BUILDING | ClockObserved/load | now>=finishes | BUILD/RUNNING | status=READY; one Save | BUILD/READY | BUILDING | FacilityJobReady | — |
| BUILDING | ClaimFacilityJob | status=READY; same opId | BUILD/READY | level=1; claimedAtUtc=now; journal COMMITTED | BUILD/CLAIMED | SYSTEM→ACTIVE; MANAGED→STOPPED | FacilityBuildCompleted (+FacilityStopped for MANAGED) | — |
| BUILDING | ClaimFacilityJob | RUNNING | BUILD/RUNNING | none | unchanged | BUILDING | — | FACILITY_JOB_NOT_READY |
| ACTIVE | StartFacilityUpgrade | level<cap; costs available | null | working=false; subtract; journal commit | UPGRADE/RUNNING target=level+1 | UPGRADING | FacilityUpgradeStarted | — |
| STOPPED | StartFacilityUpgrade | level<cap; costs available | null | subtract; journal commit | UPGRADE/RUNNING target=level+1 | UPGRADING | FacilityUpgradeStarted | — |
| UPGRADING | ClockObserved/load | now>=finishes | UPGRADE/RUNNING | status=READY | UPGRADE/READY | UPGRADING | FacilityJobReady | — |
| UPGRADING | ClaimFacilityJob | READY | UPGRADE/READY | level=target; upgradeCount+1; claimedAtUtc=now; journal COMMITTED | UPGRADE/CLAIMED | SYSTEM→ACTIVE; MANAGED matching assigned→ACTIVE else STOPPED | FacilityUpgradeCompleted (+Activated/Stopped) | — |
| ACTIVE | UnassignManagementNpc | MANAGED; same NPC | null | clear both links; working=false | null | STOPPED | ManagementNpcUnassigned,FacilityStopped | — |
| STOPPED | AssignManagementNpc | matching, unassigned NPC | null | set both links; working=true | null | ACTIVE | ManagementNpcAssigned,FacilityActivated | — |
| ACTIVE | AssignManagementNpc | same already linked | null | idempotent no-op | null | ACTIVE | — | — |
| STOPPED | UnassignManagementNpc | already null | null | idempotent no-op | null | STOPPED | — | — |
| UPGRADING | Assign/Unassign | any | RUNNING\|READY | none | unchanged | UPGRADING | — | FACILITY_JOB_ALREADY_RUNNING |
| ANY | CancelFacilityJob | player request | any | none | unchanged | same | — | FACILITY_JOB_CANCEL_UNSUPPORTED |
| ANY | KingdomStageDecrease | new stage lower | any | none | unchanged | same | — | KINGDOM_STAGE_REGRESSION_FORBIDDEN |
| ACTIVE\|STOPPED | NextSuccessfulSaveCleanup | CLAIMED + journal COMMITTED/resultDigest match; terminal observed for >=1 revision | CLAIMED | cleanup only; no reapply | null | same | — | — |
| BUILDABLE\|ACTIVE\|STOPPED | NextSuccessfulSaveCleanup | CANCELLED + journal FAILED_PERMANENT and no delta; terminal observed for >=1 revision | CANCELLED | cleanup only | null | same | — | — |
### 4.2 command/event 완전 교차 행렬

| Current state | UnlockEligibilityChanged | StartBuild | StartUpgrade | Claim | Cancel | AssignNpc | UnassignNpc | Clock/Load |
|---|---|---|---|---|---|---|---|---|
| LOCKED | eligible→BUILDABLE; 아니면 no-op | LOCKED | STATE_INVALID | JOB_NOT_FOUND | CANCEL_UNSUPPORTED | NOT_BUILT | NOT_BUILT | no-op |
| BUILDABLE | no-op | ALLOW→BUILDING | STATE_INVALID | JOB_NOT_FOUND | CANCEL_UNSUPPORTED | NOT_BUILT | NOT_BUILT | no-op |
| BUILDING | no-op | JOB_ALREADY_RUNNING | JOB_ALREADY_RUNNING | READY→CLAIMED/ACTIVE\|STOPPED; RUNNING→NOT_READY | CANCEL_UNSUPPORTED | JOB_ALREADY_RUNNING | JOB_ALREADY_RUNNING | RUNNING if now<finish; READY otherwise |
| ACTIVE | no-op | STATE_INVALID | ALLOW→UPGRADING or LEVEL_MAX/STAGE_REQUIRED | terminal same op→REPLAY; null→JOB_NOT_FOUND | CANCEL_UNSUPPORTED | MANAGED same link→no-op; other→NPC_ALREADY_ASSIGNED; SYSTEM→NPC_FORBIDDEN | MANAGED→STOPPED; SYSTEM→NPC_FORBIDDEN | terminal cleanup when next Save; otherwise no-op |
| UPGRADING | no-op | JOB_ALREADY_RUNNING | JOB_ALREADY_RUNNING | READY→CLAIMED/ACTIVE\|STOPPED; RUNNING→NOT_READY | CANCEL_UNSUPPORTED | JOB_ALREADY_RUNNING | JOB_ALREADY_RUNNING | RUNNING if now<finish; READY otherwise |
| STOPPED | no-op | STATE_INVALID | ALLOW→UPGRADING or LEVEL_MAX/STAGE_REQUIRED | terminal same op→REPLAY; null→JOB_NOT_FOUND | CANCEL_UNSUPPORTED | matching free NPC→ACTIVE | already null→no-op | terminal cleanup when next Save; otherwise no-op |
교차 행렬의 축약 error는 모두 `FACILITY_` prefix를 붙인 17장 exact code다. 표에 없는 stage decrease는 전 state에서 `KINGDOM_STAGE_REGRESSION_FORBIDDEN`, 같은 operationId/different hash는 전 state에서 `FACILITY_OPERATION_HASH_MISMATCH`, stale revision은 mutation 가능한 command 전부에서 `FACILITY_SAVE_REVISION_CONFLICT`가 우선한다.

`CLAIMED`가 참조하는 journal terminal status는 정확히 `COMMITTED`, `CANCELLED`가 참조하는 status는 정확히 `FAILED_PERMANENT`다. terminal job은 결과/rollback과 같은 commit에서 저장하고 정확히 한 committed revision 동안 보존한 뒤 다음 성공 Save에서만 null로 정리한다. `FacilityJob.cancelledAtUtc`는 CANCELLED에서만 non-null, `claimedAtUtc`는 CLAIMED에서만 non-null이다. P04-C05는 strict JSON 속성을 바꾸지 않고 semantic validator matrix만 확장하며, 출시 Save가 없고 P03 fixture에 terminal job이 없으므로 saveVersion을 올리지 않는다. invalid 조합은 golden과 일치하는 명시적 P03 bootstrap signature를 제외하고 repair하지 않고 fail closed한다.

## 5. 신규 게임·P03→P04 golden

**상태: CONFIRMED**

신규 게임은 profileId와 saveId를 `IUuidV7Provider`로 payload 구성 전에 각각 한 번 생성하고, starter NPC ID는 고정 직종 순서(MERCHANT, BLACKSMITH, ALCHEMIST, HEALER)로 생성한다. 첫 atomic commit 성공 시 revision=1이다. 실패하면 메모리 draft를 폐기하고 `SAVE_CREATE_FAILED` 화면에서 같은 ID를 재사용하지 않고 전체 생성 transaction을 새 ID로 재시도한다.

### P04_NEW_GAME_GOLDEN

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.2",
  "saveId": "019f77ac-2c00-7000-8000-000000000001",
  "profileId": "019f77ac-2c00-7000-8000-000000000002",
  "revision": 1,
  "createdAtUtc": "2026-07-19T00:00:00.000Z",
  "savedAtUtc": "2026-07-19T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719",
    "fileSha256": "6434fbccf5dd968a5c76782cb9fb5c938a737c7577caaa62358c97b565b0a22c"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 5000,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [],
    "managementNpcs": [
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000004",
        "professionId": "NPC_HEALER",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      }
    ],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-19T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-19T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-19T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

| vector | payload canonical bytes | payload SHA-256 | EnvelopeDigestInput SHA-256 |
|---|---|---|---|
| P04_NEW_GAME_GOLDEN | 3679 | 81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719 | 6434fbccf5dd968a5c76782cb9fb5c938a737c7577caaa62358c97b565b0a22c |
### P03_TO_P04_GOLDEN_BEFORE

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.1",
  "saveId": "019f77ac-2c00-7000-8000-000000000011",
  "profileId": "019f77ac-2c00-7000-8000-000000000012",
  "revision": 1,
  "createdAtUtc": "2026-07-19T00:00:00.000Z",
  "savedAtUtc": "2026-07-19T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "ea91321e2fa15caf0eef1ef7e30499a7f2ea116f44334a3e89e2b8195628953d",
    "fileSha256": "c1e7262a1d7612812cafc3f2f4cf90991b4f8dbd7f6fd3ac6a6a175e1427c053"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 0,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [],
    "managementNpcs": [],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "LOCKED",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-19T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-19T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-19T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

| vector | payload canonical bytes | payload SHA-256 | EnvelopeDigestInput SHA-256 |
|---|---|---|---|
| P03_TO_P04_GOLDEN_BEFORE | 2952 | ea91321e2fa15caf0eef1ef7e30499a7f2ea116f44334a3e89e2b8195628953d | c1e7262a1d7612812cafc3f2f4cf90991b4f8dbd7f6fd3ac6a6a175e1427c053 |
### P03_TO_P04_GOLDEN_AFTER

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.2",
  "saveId": "019f77ac-2c00-7000-8000-000000000011",
  "profileId": "019f77ac-2c00-7000-8000-000000000012",
  "revision": 2,
  "createdAtUtc": "2026-07-19T00:00:00.000Z",
  "savedAtUtc": "2026-07-19T00:00:01.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719",
    "fileSha256": "ebe58e5afd487c23980f1bf89e368ccf4010d1cd4f1b80c5394e1b8af638870c"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 5000,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [],
    "managementNpcs": [
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f77ac-2c00-7001-8000-000000000004",
        "professionId": "NPC_HEALER",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      }
    ],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-19T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-19T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-19T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

| vector | payload canonical bytes | payload SHA-256 | EnvelopeDigestInput SHA-256 |
|---|---|---|---|
| P03_TO_P04_GOLDEN_AFTER | 3679 | 81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719 | ebe58e5afd487c23980f1bf89e368ccf4010d1cd4f1b80c5394e1b8af638870c |
MIGRATION 적용 predicate는 `.1`, revision>=1, kingdomGold=0, managementNpcs=[], 8개 모두 level1/LOCKED/job=null/assigned=null/storage=[], operationJournal=[]인 경우 전부를 동시에 만족하는 것이다. 그때만 AFTER로 바꾸고 contentVersion을 `.2`, revision을 +1한다. 하나라도 다르면 bootstrap하지 않고 일반 content compatibility 검사만 한다. 기존 P03 fixture는 보존하고 세 golden을 P04 fixture로 추가한다. 단계 오류는 `P04_BOOTSTRAP_SIGNATURE_MISMATCH`, `P04_BOOTSTRAP_UUID_FAILED`, `P04_BOOTSTRAP_VALIDATION_FAILED`, `P04_BOOTSTRAP_SAVE_FAILED`; 원본 후보는 변경하지 않는다.

## 6. 잠금·왕국 단계 availability matrix

**상태: CONFIRMED**

| facilityId | kingdomStageId | visible | unlocked | buildable | maxLevel | requiredProgressionFlagOrCondition | lockedReasonCode |
|---|---|---|---|---|---|---|---|
| FAC_TAVERN | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_TAVERN | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_TAVERN | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_TAVERN | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_TAVERN | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_LODGE | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_LODGE | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_LODGE | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_LODGE | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_LODGE | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_GUILD | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_GUILD | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_GUILD | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_GUILD | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_GUILD | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_STORE | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_STORE | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_STORE | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_STORE | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_STORE | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_BLACKSMITH | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_BLACKSMITH | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_BLACKSMITH | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_BLACKSMITH | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_BLACKSMITH | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_ALCHEMY | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_ALCHEMY | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_ALCHEMY | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_ALCHEMY | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_ALCHEMY | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_WAREHOUSE | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_WAREHOUSE | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_WAREHOUSE | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_WAREHOUSE | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_WAREHOUSE | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_INFIRMARY | KINGDOM_1 | TRUE | TRUE | TRUE | 1 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_INFIRMARY | KINGDOM_2 | TRUE | TRUE | TRUE | 2 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_INFIRMARY | KINGDOM_3 | TRUE | TRUE | TRUE | 3 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_INFIRMARY | KINGDOM_4 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
| FAC_INFIRMARY | KINGDOM_5 | TRUE | TRUE | TRUE | 4 | KINGDOM_STAGE>=KINGDOM_1 | NONE |
이 표의 `buildable`은 *미건설이면* 건설 명령이 허용된다는 catalog 능력이다. 신규 게임의 SYSTEM 4종은 이미 건설돼 표시만 ACTIVE다. 모든 잠금 시설도 silhouette로 보이며, 다음 요구 stage와 현재 진행 `(currentOrdinal/requiredOrdinal)`을 표시한다. stage 상승은 `UnlockEligibilityChanged`를 같은 Save commit에 반영한다. stage 감소 command는 P04에 없고 load 시 감소 흔적은 fail closed한다. maxLevel은 `min(kingdom_stages cap, facility_levels.required stage를 만족하는 최고 level)`이며 더 엄격한 값이 우선한다. P04는 stage read-only projection만 제공한다.

## 7. 건설·업그레이드 시간·취소 규칙

**상태: CONFIRMED**

```csv
facility_id,level,build_or_upgrade_duration_seconds,cancel_refund_ratio,status,enabled
FAC_TAVERN,1,30,0,TUNABLE,TRUE
FAC_TAVERN,2,120,0,TUNABLE,TRUE
FAC_TAVERN,3,300,0,TUNABLE,TRUE
FAC_TAVERN,4,600,0,TUNABLE,TRUE
FAC_LODGE,1,20,0,TUNABLE,TRUE
FAC_LODGE,2,90,0,TUNABLE,TRUE
FAC_LODGE,3,240,0,TUNABLE,TRUE
FAC_LODGE,4,480,0,TUNABLE,TRUE
FAC_GUILD,1,45,0,TUNABLE,TRUE
FAC_GUILD,2,180,0,TUNABLE,TRUE
FAC_GUILD,3,420,0,TUNABLE,TRUE
FAC_GUILD,4,900,0,TUNABLE,TRUE
FAC_STORE,1,30,0,TUNABLE,TRUE
FAC_STORE,2,120,0,TUNABLE,TRUE
FAC_STORE,3,300,0,TUNABLE,TRUE
FAC_STORE,4,600,0,TUNABLE,TRUE
FAC_BLACKSMITH,1,45,0,TUNABLE,TRUE
FAC_BLACKSMITH,2,180,0,TUNABLE,TRUE
FAC_BLACKSMITH,3,420,0,TUNABLE,TRUE
FAC_BLACKSMITH,4,900,0,TUNABLE,TRUE
FAC_ALCHEMY,1,45,0,TUNABLE,TRUE
FAC_ALCHEMY,2,180,0,TUNABLE,TRUE
FAC_ALCHEMY,3,420,0,TUNABLE,TRUE
FAC_ALCHEMY,4,900,0,TUNABLE,TRUE
FAC_WAREHOUSE,1,30,0,TUNABLE,TRUE
FAC_WAREHOUSE,2,120,0,TUNABLE,TRUE
FAC_WAREHOUSE,3,300,0,TUNABLE,TRUE
FAC_WAREHOUSE,4,600,0,TUNABLE,TRUE
FAC_INFIRMARY,1,45,0,TUNABLE,TRUE
FAC_INFIRMARY,2,180,0,TUNABLE,TRUE
FAC_INFIRMARY,3,420,0,TUNABLE,TRUE
FAC_INFIRMARY,4,900,0,TUNABLE,TRUE
```
| Field | Domain | Rule | Validator |
|---|---|---|---|
| facility_id | STABLE_ID hard FK | facilities + composite FK facility_levels | CSV_FACILITY_CONSTRUCTION_FK_INVALID |
| level | integer 1..4 | PK with facility_id; exactly 32 combinations | CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID |
| build_or_upgrade_duration_seconds | integer 1..86400 | level1=BUILD, 2..4=UPGRADE | CSV_FACILITY_DURATION_INVALID |
| cancel_refund_ratio | decimal | exactly 0 in P04 | CSV_FACILITY_CANCEL_RATIO_INVALID |
| status/enabled | common | TUNABLE+TRUE | CSV_STATUS_ENABLED_INVALID |
권위 시계는 `ITrustedUtcClock.UtcNow`이며 command 시작 시 한 번 읽고 초 정수 duration을 더한다. 같은 process에서는 monotonic elapsed를 병행하고 `UtcNow < lastTrustedUtc-2s`면 `FACILITY_CLOCK_ROLLBACK_DETECTED`로 새 command를 거부하되 기존 finishes를 앞당기지 않는다. 앱 종료 중 경과는 생산 보상이 아닌 READY 판정만 수행하므로 P15 offline settlement가 아니다. claim은 수동이다. `CancelFacilityJob`은 전 상태에서 `FACILITY_JOB_CANCEL_UNSUPPORTED`; CSV ratio 0은 validator와 후속 호환을 위한 명시값이다. EditMode/PlayMode는 `DeterministicClock.Advance()`만 사용하고 production config를 바꾸지 않는다.

## 8. 비용·원자성·idempotency

**상태: CONFIRMED**

비용 조회는 targetLevel 행의 `build_or_upgrade_kingdom_gold`와 `facility_upgrade_materials`를 item_id UTF-8 ordinal로 정렬한다. guard 단계에서 expectedRevision→시설/state/stage/cap→catalog→금액 범위→gold→각 item 순으로 검사하고, 통과 후 단일 cloned payload에서 gold를 먼저 차감하고 item stack을 정렬 차감한다. 어느 실패도 원본 메모리/파일을 바꾸지 않는다. Build level1의 재료는 빈 배열이며 SYSTEM level1은 bootstrap 지급이므로 command가 없다. 0비용은 `inputSnapshot=[]`; gold>0은 `{assetType:KINGDOM_GOLD,assetId:SYSTEM_KINGDOM_GOLD,quantity:N,targetMercenaryInstanceId:null}`, 재료는 ITEM 행이다.

| Command | Input DTO | Output DTO | Preconditions | Mutation | Errors | Idempotency |
|---|---|---|---|---|---|---|
| StartFacilityBuild | operationId,requestHash,expectedRevision,facilityId | operationId,revision,facility,startedAtUtc,finishesAtUtc | BUILDABLE/level1/stage/funds | start job; cost; journal | locked/state/stage/gold/material/revision | same op+hash returns recorded output |
| StartFacilityUpgrade | operationId,requestHash,expectedRevision,facilityId | operationId,revision,facility,targetLevel,times | ACTIVE\|STOPPED; target<=cap; funds | working false; cost; job; journal | max/state/stage/gold/material/revision | same op+hash returns recorded output |
| ClaimFacilityJob | operationId,requestHash,expectedRevision,facilityId,facilityJobOperationId | operationId,revision,facility,resultDigest,replayed | BUILDING\|UPGRADING + READY | commit level/state; upgradeCount only for UPGRADE; persist CLAIMED one revision | not found/not ready/hash/revision | journal digest returned; never double increment |
| CancelFacilityJob | operationId,requestHash,expectedRevision,facilityId | error only | none | none | FACILITY_JOB_CANCEL_UNSUPPORTED | repeated rejection has no journal |
`requestHash=SHA256(JCS({command,operationId,expectedRevision,facilityId,targetLevelOrNull,facilityJobOperationIdOrNull}))`. 새 operation은 PREPARED를 cloned journal에 만들고 모든 delta와 함께 COMMITTED/resultDigest로 한 번 저장한다; APPLYING은 디스크에 독립 저장하지 않는다. FACILITY_JOB journal은 기존 strict 필드 전부와 `facilityJobType=BUILD|UPGRADE`, `serverReceiptId=null`, `errorCode=null`, `failureResolution=null`, `resolvedAtUtc=null`을 저장한다. resultDigest 입력은 `JCS({command,facilityId,facilityJobOperationId,targetLevel,nextState,inputSnapshot,facilityUpgradeCountAfter})`다. 같은 operationId+same hash+COMMITTED는 mutation 없이 `OperationReplayDto(operationId,status,resultDigest,replayed=true,revision=currentRevision)`를 반환한다. PREPARED는 같은 snapshot으로 resume하고 다른 hash는 `FACILITY_OPERATION_HASH_MISMATCH`다. stale expectedRevision은 `FACILITY_SAVE_REVISION_CONFLICT`. `facilityUpgradeCount`는 Kingdom의 기존 누적 counter 계약에 따라 성공 UPGRADE claim에만 +1하며 BUILD는 제외한다. P04에서 서버 원장/HTTP 호출은 금지다.

## 9. 관리 NPC 생성·배치

**상태: CONFIRMED**

| professionId | Starter count | instanceId golden | proficiency | XP | initial assignment | working |
|---|---|---|---|---|---|---|
| NPC_MERCHANT | 1 | 019f77ac-2c00-7001-8000-000000000001 | NPC_APPRENTICE | 0 | null | FALSE |
| NPC_BLACKSMITH | 1 | 019f77ac-2c00-7001-8000-000000000002 | NPC_APPRENTICE | 0 | null | FALSE |
| NPC_ALCHEMIST | 1 | 019f77ac-2c00-7001-8000-000000000003 | NPC_APPRENTICE | 0 | null | FALSE |
| NPC_HEALER | 1 | 019f77ac-2c00-7001-8000-000000000004 | NPC_APPRENTICE | 0 | null | FALSE |
P04는 고용/candidate를 구현하지 않는다. future Save는 동일 profession 여러 명을 허용하지만 instanceId는 Save 전체 unique다. bootstrap은 위 4명만 만든다. `AssignManagementNpc(operationId,requestHash,expectedRevision,facilityId,npcInstanceId)`와 `UnassignManagementNpc(...,facilityId,npcInstanceId)`는 build/upgrade 중 거부하고 직종·양방향 uniqueness를 원자 검사한다. assign은 STOPPED→ACTIVE, working=true; unassign은 ACTIVE→STOPPED, working=false. SYSTEM 대상은 `FACILITY_NPC_FORBIDDEN`. 진행 중 실제 생산 job은 P04에서 생성되지 않으므로 배치 해제와 보상 충돌이 없다. P15 tutorial은 별도 grant key로 NPC를 지급하며 P04 bootstrap ID를 tutorial 지급으로 기록하지 않는다.

Golden scenario `P04_NPC_FLOW`: 초기 4 MANAGED=BUILDABLE → FAC_STORE 건설/claim=STOPPED → Merchant assign=ACTIVE → unassign=STOPPED → 재assign=ACTIVE; 나머지 세 시설도 대응 직종으로 같은 순서를 수행한다. 모든 작업 비용을 실행하려면 fixture `P04_ALL_MANAGED_FLOW`는 kingdomGold=100000, level1 material empty를 사용한다.

## 10. 32개 facility effect Phase 분류

**상태: CONFIRMED**

| facilityId | level | effectKey | P04Behavior | OwningPhase | ProjectionField | DeferredReason |
|---|---|---|---|---|---|---|
| FAC_TAVERN | 1 | FAC_EFFECT_TAVERN_L1 | DISPLAY_ONLY | P13 | recruitmentPreview | Primary feature owned by P13 |
| FAC_TAVERN | 2 | FAC_EFFECT_TAVERN_L2 | DISPLAY_ONLY | P13 | recruitmentPreview | Primary feature owned by P13 |
| FAC_TAVERN | 3 | FAC_EFFECT_TAVERN_L3 | DISPLAY_ONLY | P13 | recruitmentPreview | Primary feature owned by P13 |
| FAC_TAVERN | 4 | FAC_EFFECT_TAVERN_L4 | DISPLAY_ONLY | P13 | recruitmentPreview | Primary feature owned by P13 |
| FAC_LODGE | 1 | FAC_EFFECT_LODGE_L1 | APPLY_NOW | P04 | activeMercenaryLimit/ownedMercenaryLimit | NONE |
| FAC_LODGE | 2 | FAC_EFFECT_LODGE_L2 | APPLY_NOW | P04 | activeMercenaryLimit/ownedMercenaryLimit | NONE |
| FAC_LODGE | 3 | FAC_EFFECT_LODGE_L3 | APPLY_NOW | P04 | activeMercenaryLimit/ownedMercenaryLimit | NONE |
| FAC_LODGE | 4 | FAC_EFFECT_LODGE_L4 | APPLY_NOW | P04 | activeMercenaryLimit/ownedMercenaryLimit | NONE |
| FAC_GUILD | 1 | FAC_EFFECT_GUILD_L1 | DISPLAY_ONLY | P11 | guildEffectText | Primary feature owned by P11 |
| FAC_GUILD | 2 | FAC_EFFECT_GUILD_L2 | DISPLAY_ONLY | P11 | guildEffectText | Primary feature owned by P11 |
| FAC_GUILD | 3 | FAC_EFFECT_GUILD_L3 | DISPLAY_ONLY | P11 | guildEffectText | Primary feature owned by P11 |
| FAC_GUILD | 4 | FAC_EFFECT_GUILD_L4 | DISPLAY_ONLY | P11 | guildEffectText | Primary feature owned by P11 |
| FAC_STORE | 1 | FAC_EFFECT_STORE_L1 | LOCKED_UNTIL_PHASE | P08 | storeEffectText | Primary feature owned by P08 |
| FAC_STORE | 2 | FAC_EFFECT_STORE_L2 | LOCKED_UNTIL_PHASE | P08 | storeEffectText | Primary feature owned by P08 |
| FAC_STORE | 3 | FAC_EFFECT_STORE_L3 | LOCKED_UNTIL_PHASE | P08 | storeEffectText | Primary feature owned by P08 |
| FAC_STORE | 4 | FAC_EFFECT_STORE_L4 | LOCKED_UNTIL_PHASE | P08 | storeEffectText | Primary feature owned by P08 |
| FAC_BLACKSMITH | 1 | FAC_EFFECT_BLACKSMITH_L1 | LOCKED_UNTIL_PHASE | P10 | blacksmithEffectText | Primary feature owned by P10 |
| FAC_BLACKSMITH | 2 | FAC_EFFECT_BLACKSMITH_L2 | LOCKED_UNTIL_PHASE | P10 | blacksmithEffectText | Primary feature owned by P10 |
| FAC_BLACKSMITH | 3 | FAC_EFFECT_BLACKSMITH_L3 | LOCKED_UNTIL_PHASE | P10 | blacksmithEffectText | Primary feature owned by P10 |
| FAC_BLACKSMITH | 4 | FAC_EFFECT_BLACKSMITH_L4 | LOCKED_UNTIL_PHASE | P10 | blacksmithEffectText | Primary feature owned by P10 |
| FAC_ALCHEMY | 1 | FAC_EFFECT_ALCHEMY_L1 | LOCKED_UNTIL_PHASE | P10 | alchemyEffectText | Primary feature owned by P10 |
| FAC_ALCHEMY | 2 | FAC_EFFECT_ALCHEMY_L2 | LOCKED_UNTIL_PHASE | P10 | alchemyEffectText | Primary feature owned by P10 |
| FAC_ALCHEMY | 3 | FAC_EFFECT_ALCHEMY_L3 | LOCKED_UNTIL_PHASE | P10 | alchemyEffectText | Primary feature owned by P10 |
| FAC_ALCHEMY | 4 | FAC_EFFECT_ALCHEMY_L4 | LOCKED_UNTIL_PHASE | P10 | alchemyEffectText | Primary feature owned by P10 |
| FAC_WAREHOUSE | 1 | FAC_EFFECT_WAREHOUSE_L1 | DISPLAY_ONLY | P07 | warehouseEffectText | Primary feature owned by P07 |
| FAC_WAREHOUSE | 2 | FAC_EFFECT_WAREHOUSE_L2 | DISPLAY_ONLY | P07 | warehouseEffectText | Primary feature owned by P07 |
| FAC_WAREHOUSE | 3 | FAC_EFFECT_WAREHOUSE_L3 | DISPLAY_ONLY | P07 | warehouseEffectText | Primary feature owned by P07 |
| FAC_WAREHOUSE | 4 | FAC_EFFECT_WAREHOUSE_L4 | DISPLAY_ONLY | P07 | warehouseEffectText | Primary feature owned by P07 |
| FAC_INFIRMARY | 1 | FAC_EFFECT_INFIRMARY_L1 | LOCKED_UNTIL_PHASE | P09 | infirmaryEffectText | Primary feature owned by P09 |
| FAC_INFIRMARY | 2 | FAC_EFFECT_INFIRMARY_L2 | LOCKED_UNTIL_PHASE | P09 | infirmaryEffectText | Primary feature owned by P09 |
| FAC_INFIRMARY | 3 | FAC_EFFECT_INFIRMARY_L3 | LOCKED_UNTIL_PHASE | P09 | infirmaryEffectText | Primary feature owned by P09 |
| FAC_INFIRMARY | 4 | FAC_EFFECT_INFIRMARY_L4 | LOCKED_UNTIL_PHASE | P09 | infirmaryEffectText | Primary feature owned by P09 |
Lodge projection은 level 1..4에서 `(active,owned)=(4,8),(8,12),(12,18),(16,24)`이며 load/claim 때 Kingdom snapshot과 일치해야 한다. stage cap은 6장의 min 규칙이다. SYSTEM ACTIVE는 기반 시설이 건설·정상 상태라는 뜻이지 모집/길드/창고 feature가 실행 가능하다는 뜻이 아니다. MANAGED ACTIVE도 올바른 NPC가 배치돼 future operation 준비가 됐다는 뜻이다. Overview/Staff/Upgrade 탭은 보이고 Operations 탭은 숨기지 않고 disabled+해당 `TXT_P04_LOCKED_PHASE_*`를 표시한다.

## 11. P04 content package exact 계약과 전체 변경 rows

**상태: CONFIRMED**

| Property | Exact value |
|---|---|
| contentVersion | 1.0.0-content.2 |
| packageKind | BASE |
| baseContentVersion | null |
| manifest contract/schema | 2 / urn:tycoon:content-manifest:v2 |
| csvSchemaSetVersion | 3 |
| minimumGameVersion | 1.0.0-p04 |
| channel | DEV |
| generatedAtUtc | 2026-07-19T00:00:00.000Z |
| table count | 62 |
| manifest JCS SHA-256 | 4be272f22ba2b178d59f27004177dd00384118303240534b0b05e0c2ff630632 |
| manifest JCS byte length | 67264 |
P03 계약에 따라 위 `manifest JCS SHA-256`가 곧 P04 content package golden checksum이며 catalog metadata, CI artifact metadata, 향후 DB content release checksum이 모두 이 값을 사용한다. ZIP/container byte hash는 포장 도구에 따라 달라질 수 있어 권위값이 아니다.

P03 `.1` 디렉터리와 bytes는 영구 보존한다. `.2`는 60개 P03 table을 BASE에 byte-semantic carry-forward하되 아래 3개를 재생성하고 2개를 추가한다. Save alias는 stable ID 변화가 없어 0행이다. `.1→.2` migration은 5장의 signature 대상만 bootstrap하고 그 외에는 contentVersion만 호환 검증 후 다음 정상 Save에 갱신한다.

| table | change | final rowCount | exact CRLF SHA-256 | header |
|---|---|---|---|---|
| asset_register.csv | MODIFY | 18 | f44f551c6929a08cbb781e71cbadc24f37dbcc721725843b3729498e4c6fd149 | asset_id,name,creator,source_url,version,acquired_date,price_krw,license,commercial_use,modification_allowed,credit_required,used_in,notes,status,enabled |
| facility_construction_rules.csv | ADD | 32 | a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5 | facility_id,level,build_or_upgrade_duration_seconds,cancel_refund_ratio,status,enabled |
| facility_world_assets.csv | ADD | 13 | d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f | asset_id,address,asset_type,width_px,height_px,pivot_x,pivot_y,pixels_per_unit,facility_id,state_variant,status,enabled |
| localizations.csv | MODIFY | 608 | 56347e8e9c1fc61c45b18f5e024a31df966574aefdfbb5cb27703bfbd3c946bf | locale,text_key,text_value,context,status,enabled |
| runtime_config.csv | MODIFY | 18 | 0cb85fbf0e5f4200f115c629d261d9b88bef4166f722886b516dac55b38b16db | config_key,value_type,value,unit,min_value,max_value,description_text_key,status,enabled |
### 11.1 `facility_construction_rules.csv` 전체 32 rows

```csv
facility_id,level,build_or_upgrade_duration_seconds,cancel_refund_ratio,status,enabled
FAC_TAVERN,1,30,0,TUNABLE,TRUE
FAC_TAVERN,2,120,0,TUNABLE,TRUE
FAC_TAVERN,3,300,0,TUNABLE,TRUE
FAC_TAVERN,4,600,0,TUNABLE,TRUE
FAC_LODGE,1,20,0,TUNABLE,TRUE
FAC_LODGE,2,90,0,TUNABLE,TRUE
FAC_LODGE,3,240,0,TUNABLE,TRUE
FAC_LODGE,4,480,0,TUNABLE,TRUE
FAC_GUILD,1,45,0,TUNABLE,TRUE
FAC_GUILD,2,180,0,TUNABLE,TRUE
FAC_GUILD,3,420,0,TUNABLE,TRUE
FAC_GUILD,4,900,0,TUNABLE,TRUE
FAC_STORE,1,30,0,TUNABLE,TRUE
FAC_STORE,2,120,0,TUNABLE,TRUE
FAC_STORE,3,300,0,TUNABLE,TRUE
FAC_STORE,4,600,0,TUNABLE,TRUE
FAC_BLACKSMITH,1,45,0,TUNABLE,TRUE
FAC_BLACKSMITH,2,180,0,TUNABLE,TRUE
FAC_BLACKSMITH,3,420,0,TUNABLE,TRUE
FAC_BLACKSMITH,4,900,0,TUNABLE,TRUE
FAC_ALCHEMY,1,45,0,TUNABLE,TRUE
FAC_ALCHEMY,2,180,0,TUNABLE,TRUE
FAC_ALCHEMY,3,420,0,TUNABLE,TRUE
FAC_ALCHEMY,4,900,0,TUNABLE,TRUE
FAC_WAREHOUSE,1,30,0,TUNABLE,TRUE
FAC_WAREHOUSE,2,120,0,TUNABLE,TRUE
FAC_WAREHOUSE,3,300,0,TUNABLE,TRUE
FAC_WAREHOUSE,4,600,0,TUNABLE,TRUE
FAC_INFIRMARY,1,45,0,TUNABLE,TRUE
FAC_INFIRMARY,2,180,0,TUNABLE,TRUE
FAC_INFIRMARY,3,420,0,TUNABLE,TRUE
FAC_INFIRMARY,4,900,0,TUNABLE,TRUE
```
### 11.2 `facility_world_assets.csv` 전체 rows

```csv
asset_id,address,asset_type,width_px,height_px,pivot_x,pivot_y,pixels_per_unit,facility_id,state_variant,status,enabled
ASSET_FAC_TAVERN_PLACEHOLDER_V1,P04/Kingdom/FAC_TAVERN,PREFAB,256,256,0.5,0.1,100,FAC_TAVERN,BASE,CONFIRMED,TRUE
ASSET_FAC_LODGE_PLACEHOLDER_V1,P04/Kingdom/FAC_LODGE,PREFAB,256,256,0.5,0.1,100,FAC_LODGE,BASE,CONFIRMED,TRUE
ASSET_FAC_GUILD_PLACEHOLDER_V1,P04/Kingdom/FAC_GUILD,PREFAB,256,256,0.5,0.1,100,FAC_GUILD,BASE,CONFIRMED,TRUE
ASSET_FAC_STORE_PLACEHOLDER_V1,P04/Kingdom/FAC_STORE,PREFAB,256,256,0.5,0.1,100,FAC_STORE,BASE,CONFIRMED,TRUE
ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1,P04/Kingdom/FAC_BLACKSMITH,PREFAB,256,256,0.5,0.1,100,FAC_BLACKSMITH,BASE,CONFIRMED,TRUE
ASSET_FAC_ALCHEMY_PLACEHOLDER_V1,P04/Kingdom/FAC_ALCHEMY,PREFAB,256,256,0.5,0.1,100,FAC_ALCHEMY,BASE,CONFIRMED,TRUE
ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1,P04/Kingdom/FAC_WAREHOUSE,PREFAB,256,256,0.5,0.1,100,FAC_WAREHOUSE,BASE,CONFIRMED,TRUE
ASSET_FAC_INFIRMARY_PLACEHOLDER_V1,P04/Kingdom/FAC_INFIRMARY,PREFAB,256,256,0.5,0.1,100,FAC_INFIRMARY,BASE,CONFIRMED,TRUE
ASSET_KINGDOM_BACKGROUND_V1,P04/Kingdom/Background,SPRITE,1920,1080,0.5,0.5,100,,BACKGROUND,CONFIRMED,TRUE
ASSET_FACILITY_PLOT_V1,P04/Kingdom/Plot,SPRITE,256,192,0.5,0.5,100,,PLOT,CONFIRMED,TRUE
ASSET_FACILITY_LOCKED_V1,P04/Kingdom/Locked,SPRITE,256,256,0.5,0.5,100,,LOCKED,CONFIRMED,TRUE
ASSET_FACILITY_CONSTRUCTION_V1,P04/Kingdom/Construction,SPRITE,256,256,0.5,0.5,100,,CONSTRUCTION,CONFIRMED,TRUE
ASSET_FACILITY_STOPPED_V1,P04/Kingdom/Stopped,SPRITE,64,64,0.5,0.5,100,,STOPPED,CONFIRMED,TRUE
```
### 11.3 `asset_register.csv` 전체 rows

```csv
asset_id,name,creator,source_url,version,acquired_date,price_krw,license,commercial_use,modification_allowed,credit_required,used_in,notes,status,enabled
ASSET_MERC_PLACEHOLDER_WARRIOR_V1,Internal Warrior Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_GUARDIAN_V1,Internal Guardian Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_ARCHER_V1,Internal Archer Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_MAGE_V1,Internal Mage Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_CLERIC_V1,Internal Cleric Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_FAC_TAVERN_PLACEHOLDER_V1,Asset Fac Tavern Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_TAVERN; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_LODGE_PLACEHOLDER_V1,Asset Fac Lodge Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_LODGE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_GUILD_PLACEHOLDER_V1,Asset Fac Guild Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_GUILD; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_STORE_PLACEHOLDER_V1,Asset Fac Store Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_STORE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1,Asset Fac Blacksmith Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_BLACKSMITH; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_ALCHEMY_PLACEHOLDER_V1,Asset Fac Alchemy Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_ALCHEMY; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1,Asset Fac Warehouse Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_WAREHOUSE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_INFIRMARY_PLACEHOLDER_V1,Asset Fac Infirmary Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_INFIRMARY; generated placeholder,CONFIRMED,TRUE
ASSET_KINGDOM_BACKGROUND_V1,Asset Kingdom Background V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Background; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_PLOT_V1,Asset Facility Plot V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Plot; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_LOCKED_V1,Asset Facility Locked V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Locked; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_CONSTRUCTION_V1,Asset Facility Construction V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Construction; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_STOPPED_V1,Asset Facility Stopped V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Stopped; generated placeholder,CONFIRMED,TRUE
```
### 11.4 `runtime_config.csv` 전체 rows

```csv
config_key,value_type,value,unit,min_value,max_value,description_text_key,status,enabled
OFFLINE_MAX_HOURS,INTEGER,8,HOURS,0,24,TXT_OFFLINE_MAX_HOURS_DESCRIPTION,TUNABLE,TRUE
OFFLINE_HUNT_EFFICIENCY,DECIMAL,0.75,RATIO,0,1,TXT_OFFLINE_HUNT_EFFICIENCY_DESCRIPTION,TUNABLE,TRUE
EQUIPMENT_UPGRADE_THRESHOLD,DECIMAL,0.05,RATIO,0,1,TXT_EQUIPMENT_UPGRADE_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
RARE_EQUIPMENT_AUTO_PROTECT,BOOLEAN,TRUE,BOOL,,,TXT_RARE_EQUIPMENT_AUTO_PROTECT_DESCRIPTION,TUNABLE,TRUE
DISMANTLE_ENHANCE_REFUND,DECIMAL,0.7,RATIO,0,1,TXT_DISMANTLE_ENHANCE_REFUND_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_LOW,DECIMAL,0.9,RATIO,0.5,2,TXT_STORE_PRICE_LOW_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_STANDARD,DECIMAL,1.0,RATIO,0.5,2,TXT_STORE_PRICE_STANDARD_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_HIGH,DECIMAL,1.15,RATIO,0.5,2,TXT_STORE_PRICE_HIGH_DESCRIPTION,TUNABLE,TRUE
INVENTORY_RETURN_THRESHOLD,DECIMAL,0.9,RATIO,0,1,TXT_INVENTORY_RETURN_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
DEFAULT_HP_RETURN_THRESHOLD,DECIMAL,0.3,RATIO,0,1,TXT_DEFAULT_HP_RETURN_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
ACTIVE_MERC_CAP_V1,INTEGER,16,COUNT,1,24,TXT_ACTIVE_MERC_CAP_V1_DESCRIPTION,TUNABLE,TRUE
ROSTER_CAP_V1,INTEGER,24,COUNT,1,24,TXT_ROSTER_CAP_V1_DESCRIPTION,TUNABLE,TRUE
RAID_PARTY_MIN,INTEGER,6,COUNT,1,8,TXT_RAID_PARTY_MIN_DESCRIPTION,TUNABLE,TRUE
RAID_PARTY_MAX,INTEGER,8,COUNT,1,16,TXT_RAID_PARTY_MAX_DESCRIPTION,TUNABLE,TRUE
P04_TEST_CLOCK_MODE,BOOLEAN,0,BOOL,0,1,TXT_P04_DEV_CLOCK_DESC,CONFIRMED,TRUE
P04_DRAWER_ANIMATION_SECONDS,DECIMAL,0.22,SECONDS,0,1,TXT_P04_DRAWER_DURATION_DESC,TUNABLE,TRUE
P04_AUTOSAVE_DEBOUNCE_SECONDS,DECIMAL,0.5,SECONDS,0,5,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,TUNABLE,TRUE
P04_TIMER_UI_TICK_SECONDS,DECIMAL,1,SECONDS,0.1,5,TXT_P04_TIMER_TICK_DESC,TUNABLE,TRUE
```
### 11.5 `localizations.csv` P04 추가 전체 rows

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P04_ACTION_ASSIGN,Assign NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BACK,Back,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BUILD,Build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CANCEL,Cancel,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLAIM,Complete,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLOSE,Close,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CONFIRM,Confirm,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_RETRY,Retry,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UNASSIGN,Unassign,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 autosave debounce,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_GOLD,Kingdom Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DEV_CLOCK_DESC,P04 test clock mode,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DRAWER_DURATION_DESC,Facility drawer animation duration,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_GOLD,Not enough Kingdom Gold.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_NOT_READY,The facility job is not ready.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_RUNNING,A facility job is already in progress.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MATERIAL,Not enough materials.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MAX_LEVEL,Maximum level reached.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_ASSIGNED,This NPC is already assigned.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_NONE,No management NPC is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_PROFESSION,This NPC has the wrong profession.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_REVISION,Another change was saved first. Reloading.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_CREATE,Could not save the new kingdom.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_LOAD,Could not load save data.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_WRITE,Could not save changes.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_STAGE,A higher Kingdom Stage is required.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_GOLD,Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_PREMIUM,Premium {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_RECOVERED,A backup save was recovered.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_STAGE,Kingdom Stage {stage},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_ALCHEMY,Alchemy unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_BLACKSMITH,Smithing unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_GUILD,Guild requests unlock in P11.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_INFIRMARY,Treatment unlocks in P09.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_STORE,Trading unlocks in P08.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_TAVERN,Recruitment unlocks in P13.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_WAREHOUSE,Warehouse operations unlock in P07.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_STAGE,Unlocks at Kingdom Stage {stage}.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_ASSIGN_TITLE,Assign Management NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_BUILD_TITLE,Build Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_UPGRADE_TITLE,Upgrade Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_CRAFT,Craft,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_KINGDOM,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MENU,Menu,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MERCENARIES,Mercenaries,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_RECRUIT,Recruit,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_REGION,Region,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_ACTIVE,Active,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDABLE,Ready to build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDING,Building,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_LOCKED,Locked,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_STOPPED,Stopped,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_UPGRADING,Upgrading,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STOP_NPC_REQUIRED,Assign the required management NPC to activate.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OPERATIONS,Operations,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OVERVIEW,Overview,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_STAFF,Staff,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIMER_TICK_DESC,Facility remaining-time display interval,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_FINISHES,Finishes at {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_REMAINING,Time remaining: {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_CONTENT,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_EMPTY,No facility data is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_ERROR,Kingdom cannot be displayed.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOADING,Loading kingdom…,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOCKED,This facility is locked.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_OFFLINE,Offline: local facility management remains available.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_ASSIGN,NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BACK,뒤로,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BUILD,건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CANCEL,취소,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLAIM,완료,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLOSE,닫기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CONFIRM,확인,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_RETRY,다시 시도,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UNASSIGN,배치 해제,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 자동 저장 debounce,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_GOLD,왕국 골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DEV_CLOCK_DESC,P04 테스트 시계 모드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DRAWER_DURATION_DESC,시설 Drawer 애니메이션 시간,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_GOLD,왕국 골드가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_NOT_READY,아직 완료되지 않았습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_RUNNING,이미 진행 중인 작업이 있습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MATERIAL,건설 재료가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MAX_LEVEL,최대 레벨입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_ASSIGNED,이미 다른 시설에 배치된 NPC입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_NONE,배치할 관리 NPC가 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_PROFESSION,이 시설에 맞는 직종이 아닙니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_CREATE,새 왕국을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_LOAD,저장 데이터를 불러오지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_WRITE,변경 사항을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_STAGE,왕국 단계가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_GOLD,골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_PREMIUM,프리미엄 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_RECOVERED,백업 저장을 복구했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_STAGE,왕국 단계 {stage},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_ALCHEMY,연금술 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_BLACKSMITH,제작 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_GUILD,의뢰 기능은 P11에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_INFIRMARY,치료 기능은 P09에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_STORE,거래 기능은 P08에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_TAVERN,모집 기능은 P13에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_WAREHOUSE,창고 기능은 P07에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_STAGE,왕국 단계 {stage}에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_ASSIGN_TITLE,관리 NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_BUILD_TITLE,시설 건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_UPGRADE_TITLE,시설 업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_CRAFT,제작,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_KINGDOM,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MENU,메뉴,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MERCENARIES,용병,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_RECRUIT,모집,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_REGION,지역,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_ACTIVE,가동 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDABLE,건설 가능,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDING,건설 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_LOCKED,잠김,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_STOPPED,가동 정지,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_UPGRADING,업그레이드 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STOP_NPC_REQUIRED,올바른 관리 NPC를 배치하면 가동됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OPERATIONS,운영,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OVERVIEW,개요,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_STAFF,직원,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIMER_TICK_DESC,시설 남은 시간 표시 주기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_FINISHES,완료 예정 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_REMAINING,남은 시간 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_CONTENT,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_EMPTY,시설 정보를 찾을 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_ERROR,왕국을 표시할 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOADING,왕국을 불러오는 중…,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOCKED,아직 잠긴 시설입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_OFFLINE,오프라인: 로컬 시설 관리는 사용할 수 있습니다.,P04_UI,CONFIRMED,TRUE
```
최종 localizations 파일은 P03 확정 470행을 동일 순서로 유지하고 위 행을 `(locale,text_key)` UTF-8 ordinal 정렬 merge한다. 다른 57개 파일은 P03 descriptor/bytes 그대로다. manifest는 P03 v2 descriptor를 복제하고 3개 hash/rowCount를 위 값으로 교체한 뒤 신규 2 descriptor를 추가하여 `file` UTF-8 ordinal 정렬하고 RFC8785로 직렬화한다. CSV는 UTF-8 no BOM, CRLF, RFC4180, 마지막 CRLF 포함이다. 재생성된 62개 hash와 manifest digest가 위 값과 같아야 하며 아니면 `CONTENT_P04_PACKAGE_GOLDEN_MISMATCH`.

## 12. world/asset/layout exact 계약

**상태: CONFIRMED**

| assetId | address | type | dimensions | pivot | pixelsPerUnit | facilityIdOrRole | stateVariant | source | license | replacementContract |
|---|---|---|---|---|---|---|---|---|---|---|
| ASSET_FAC_TAVERN_PLACEHOLDER_V1 | P04/Kingdom/FAC_TAVERN | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_TAVERN | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_LODGE_PLACEHOLDER_V1 | P04/Kingdom/FAC_LODGE | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_LODGE | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_GUILD_PLACEHOLDER_V1 | P04/Kingdom/FAC_GUILD | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_GUILD | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_STORE_PLACEHOLDER_V1 | P04/Kingdom/FAC_STORE | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_STORE | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1 | P04/Kingdom/FAC_BLACKSMITH | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_BLACKSMITH | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_ALCHEMY_PLACEHOLDER_V1 | P04/Kingdom/FAC_ALCHEMY | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_ALCHEMY | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1 | P04/Kingdom/FAC_WAREHOUSE | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_WAREHOUSE | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FAC_INFIRMARY_PLACEHOLDER_V1 | P04/Kingdom/FAC_INFIRMARY | PREFAB | 256×256 | (0.5,0.1) | 100 | FAC_INFIRMARY | BASE | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_KINGDOM_BACKGROUND_V1 | P04/Kingdom/Background | SPRITE | 1920×1080 | (0.5,0.5) | 100 | BACKGROUND | BACKGROUND | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FACILITY_PLOT_V1 | P04/Kingdom/Plot | SPRITE | 256×192 | (0.5,0.5) | 100 | PLOT | PLOT | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FACILITY_LOCKED_V1 | P04/Kingdom/Locked | SPRITE | 256×256 | (0.5,0.5) | 100 | LOCKED | LOCKED | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FACILITY_CONSTRUCTION_V1 | P04/Kingdom/Construction | SPRITE | 256×256 | (0.5,0.5) | 100 | CONSTRUCTION | CONSTRUCTION | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| ASSET_FACILITY_STOPPED_V1 | P04/Kingdom/Stopped | SPRITE | 64×64 | (0.5,0.5) | 100 | STOPPED | STOPPED | EDITOR_GENERATED | PROJECT_INTERNAL | address/assetId/facilityId stable; visual subasset replace only |
| facilityId | anchorX | anchorY | width | height | sortingOrder | labelPlacement |
|---|---|---|---|---|---|---|
| FAC_TAVERN | 0.18 | 0.72 | 0.18 | 0.26 | 20 | BELOW_CENTER |
| FAC_LODGE | 0.39 | 0.76 | 0.18 | 0.26 | 21 | BELOW_CENTER |
| FAC_GUILD | 0.62 | 0.72 | 0.18 | 0.26 | 22 | BELOW_CENTER |
| FAC_STORE | 0.82 | 0.7 | 0.18 | 0.26 | 23 | BELOW_CENTER |
| FAC_BLACKSMITH | 0.2 | 0.35 | 0.18 | 0.26 | 24 | BELOW_CENTER |
| FAC_ALCHEMY | 0.42 | 0.3 | 0.18 | 0.26 | 25 | BELOW_CENTER |
| FAC_WAREHOUSE | 0.64 | 0.35 | 0.18 | 0.26 | 26 | BELOW_CENTER |
| FAC_INFIRMARY | 0.82 | 0.3 | 0.18 | 0.26 | 27 | BELOW_CENTER |
Editor API는 256×256 RGBA `Texture2D .asset`의 Sprite subasset과 root `FacilityWorldView` prefab을 생성한다. prefab child는 BaseSprite/StateOverlay/StoppedIcon/Label/HitTarget이며 `PrefabUtility.SaveAsPrefabAsset`만 쓴다. 시설 pivot=(0.5,0.1), PPU=100, SortingLayer=`KingdomWorld`; background order=0, plot=10, facility=표 값, overlay=+100, label=+200. normalized anchor는 1920×1080 Safe Area의 좌하단 원점이다. visual width/height는 Safe Area 비율이고 hit rect는 `max(visual,64×64 px)`이다.

Orthographic camera size=5.4, Pixel Perfect reference=1920×1080, assetsPPU=100, cropFrameX=false, cropFrameY=false, stretchFill=false. 16:9는 기준, 18:9/20:9는 세로 safe height를 유지하며 좌우 world를 확장한다. anchor는 중앙 16:9 safe frame에 고정해 critical facility가 crop되지 않는다. production asset 교체는 동일 assetId/address/prefab marker/hit bounds를 유지하므로 code·Save·content ID가 바뀌지 않는다.

## 13. HUD/Drawer/navigation hierarchy와 좌표

**상태: CONFIRMED**

`WorldCanvas/KingdomScreen/SafeArea/FacilityLayer/{8 FacilityWorldView}`; `HudCanvas/KingdomHud`; `DrawerCanvas/FacilityDrawer`; `ModalCanvas/ConfirmationModal|NpcPickerModal`; Presenter가 DTO를 bind한다.

| ElementId | ParentId | AnchorMin | AnchorMax | OffsetMin | OffsetMax | MinTouchSize | VisibleWhen | InteractableWhen | LocalizationKey |
|---|---|---|---|---|---|---|---|---|---|
| KINGDOM_ROOT | WORLD_CANVAS | 0,0 | 1,1 | 0,0 | 0,0 | 0 | CONTENT\|LOCKED\|OFFLINE | FALSE |  |
| SAFE_AREA | KINGDOM_ROOT | 0,0 | 1,1 | 0,0 | 0,0 | 0 | ALWAYS | FALSE |  |
| HUD_BAR | SAFE_AREA | 0,1 | 1,1 | 0,-112 | 0,0 | 0 | CONTENT\|LOCKED\|OFFLINE | FALSE |  |
| HUD_GOLD | HUD_BAR | 0,0 | 0,1 | 24,16 | 344,-16 | 64×64 | CONTENT\|LOCKED\|OFFLINE | FALSE | TXT_P04_HUD_GOLD |
| HUD_PREMIUM | HUD_BAR | 0,0 | 0,1 | 360,16 | 700,-16 | 64×64 | CONTENT\|OFFLINE | FALSE | TXT_P04_HUD_PREMIUM |
| HUD_STAGE | HUD_BAR | 0.5,0 | 0.5,1 | -180,16 | 180,-16 | 64×64 | CONTENT\|LOCKED\|OFFLINE | FALSE | TXT_P04_HUD_STAGE |
| HUD_ALERT | HUD_BAR | 1,0 | 1,1 | -96,16 | -24,-16 | 64×64 | RECOVERED\|OFFLINE | TRUE | TXT_P04_HUD_RECOVERED |
| FACILITY_LAYER | SAFE_AREA | 0,0 | 1,1 | 0,112 | 0,-112 | 0 | CONTENT\|LOCKED\|OFFLINE | FALSE |  |
| BOTTOM_NAV | SAFE_AREA | 0,0 | 1,0 | 0,0 | 0,112 | 0 | CONTENT\|LOCKED\|OFFLINE | FALSE |  |
| NAV_KINGDOM | BOTTOM_NAV | 0,0 | 0.1667,1 | 0,0 | 0,0 | 64×64 | ALWAYS | TRUE | TXT_P04_NAV_KINGDOM |
| NAV_MERCENARIES | BOTTOM_NAV | 0.1667,0 | 0.3333,1 | 0,0 | 0,0 | 64×64 | ALWAYS | FALSE | TXT_P04_NAV_MERCENARIES |
| NAV_REGION | BOTTOM_NAV | 0.3333,0 | 0.5,1 | 0,0 | 0,0 | 64×64 | ALWAYS | FALSE | TXT_P04_NAV_REGION |
| NAV_CRAFT | BOTTOM_NAV | 0.5,0 | 0.6667,1 | 0,0 | 0,0 | 64×64 | ALWAYS | FALSE | TXT_P04_NAV_CRAFT |
| NAV_RECRUIT | BOTTOM_NAV | 0.6667,0 | 0.8333,1 | 0,0 | 0,0 | 64×64 | ALWAYS | FALSE | TXT_P04_NAV_RECRUIT |
| NAV_MENU | BOTTOM_NAV | 0.8333,0 | 1,1 | 0,0 | 0,0 | 64×64 | ALWAYS | TRUE | TXT_P04_NAV_MENU |
| DRAWER_SCRIM | DRAWER_CANVAS | 0,0 | 1,1 | 0,0 | -640,0 | 64×64 | DRAWER_OPEN | TRUE |  |
| FACILITY_DRAWER | DRAWER_CANVAS | 1,0 | 1,1 | -640,0 | 0,0 | 0 | DRAWER_OPEN | FALSE |  |
| DRAWER_CLOSE | FACILITY_DRAWER | 1,1 | 1,1 | -88,-88 | -16,-16 | 64×64 | ALWAYS | TRUE | TXT_P04_ACTION_CLOSE |
| DRAWER_HEADER | FACILITY_DRAWER | 0,1 | 1,1 | 24,-176 | -96,-24 | 0 | ALWAYS | FALSE |  |
| DRAWER_STATUS | FACILITY_DRAWER | 0,1 | 1,1 | 24,-240 | -24,-184 | 0 | ALWAYS | FALSE |  |
| TAB_OVERVIEW | FACILITY_DRAWER | 0,1 | 0.25,1 | 16,-320 | -4,-256 | 64×64 | ALWAYS | TRUE | TXT_P04_TAB_OVERVIEW |
| TAB_STAFF | FACILITY_DRAWER | 0.25,1 | 0.5,1 | 4,-320 | -4,-256 | 64×64 | MANAGED | TRUE | TXT_P04_TAB_STAFF |
| TAB_UPGRADE | FACILITY_DRAWER | 0.5,1 | 0.75,1 | 4,-320 | -4,-256 | 64×64 | BUILT | TRUE | TXT_P04_TAB_UPGRADE |
| TAB_OPERATIONS | FACILITY_DRAWER | 0.75,1 | 1,1 | 4,-320 | -16,-256 | 64×64 | ALWAYS | FALSE | TXT_P04_TAB_OPERATIONS |
| DRAWER_SCROLL | FACILITY_DRAWER | 0,0 | 1,1 | 24,120 | -24,-336 | 0 | ALWAYS | TRUE |  |
| PRIMARY_ACTION | FACILITY_DRAWER | 0,0 | 1,0 | 24,24 | -24,104 | 64×64 | HAS_PRIMARY_ACTION | guard && !COMMAND_BUSY |  |
| MODAL_ROOT | MODAL_CANVAS | 0,0 | 1,1 | 0,0 | 0,0 | 0 | MODAL_OPEN | FALSE |  |
Drawer width=640 px, open/close=0.22s, easing=`CUBIC_OUT`, 한 번에 1개다. facility 재선택은 열린 Drawer 내용을 0프레임 bind 교체하고 animation 재시작 없음. scrim/close는 닫기, Android back은 modal→drawer→scene navigation 순이다. Build/Upgrade는 confirmation modal, Assign은 NPC picker modal, Unassign은 confirmation modal이다. modal stack 깊이는 1; 열려 있으면 다른 action 거부. focus 순서는 close→tabs→scroll controls→primary action이며 open 시 header, close 시 선택 시설로 복귀한다. disabled button 아래 한 줄 reason을 보이고 눌러도 command를 보내지 않는다. 130% text scale에서 Drawer width 유지, scroll 활성, ellipsis 금지, 최소 본문 32px/버튼 30px다.

## 14. 6개 UI 상태

**상태: CONFIRMED**

| UiState | EntryCondition | WorldVisibility | DrawerContent | PrimaryAction | SecondaryAction | LocalizationKey | AnalyticsEvent |
|---|---|---|---|---|---|---|---|
| LOADING | manifest/catalog/Save flow 미완료 | skeleton만 | skeleton | NONE | NONE | TXT_P04_UI_LOADING | P04_KINGDOM_LOADING |
| CONTENT | projection facilities=8 and valid | 8 시설 | selected detail/없으면 closed | 상태별 action | close | TXT_P04_UI_CONTENT | P04_KINGDOM_CONTENT |
| EMPTY | valid load but projection count!=8 | 배경+empty panel | closed | RETRY | MENU | TXT_P04_UI_EMPTY | P04_KINGDOM_EMPTY |
| ERROR | catalog/Save/invariant validation 실패 | 배경 숨김; error panel | closed | RETRY LOAD | OPEN DIAGNOSTICS(dev only) | TXT_P04_UI_ERROR | P04_KINGDOM_ERROR |
| LOCKED | selected facility.state=LOCKED | silhouette 포함 8 시설 | 요구 stage/진행도 | CLOSE | NONE | TXT_P04_UI_LOCKED | P04_FACILITY_LOCKED_VIEW |
| OFFLINE | network reachability=false; local data valid | CONTENT와 동일+badge | CONTENT와 동일 | local command 허용 | RETRY NETWORK | TXT_P04_UI_OFFLINE | P04_KINGDOM_OFFLINE |
Offline은 P04 local Save의 장애가 아니며 server premium 값은 마지막 cache를 읽기만 한다. Error는 Save를 자동 수정하지 않는다. DebugCanvas에는 contentVersion/manifest digest/save revision/recovery source/error code/facility invariant만 표시하며 `DEVELOPMENT_BUILD || UNITY_EDITOR`가 아니면 GameObject와 symbols를 build에서 제거한다.

## 15. ko-KR/en-US localization 전체 rows

**상태: CONFIRMED**

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P04_ACTION_ASSIGN,Assign NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BACK,Back,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BUILD,Build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CANCEL,Cancel,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLAIM,Complete,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLOSE,Close,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CONFIRM,Confirm,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_RETRY,Retry,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UNASSIGN,Unassign,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 autosave debounce,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_GOLD,Kingdom Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DEV_CLOCK_DESC,P04 test clock mode,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DRAWER_DURATION_DESC,Facility drawer animation duration,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_GOLD,Not enough Kingdom Gold.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_NOT_READY,The facility job is not ready.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_RUNNING,A facility job is already in progress.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MATERIAL,Not enough materials.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MAX_LEVEL,Maximum level reached.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_ASSIGNED,This NPC is already assigned.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_NONE,No management NPC is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_PROFESSION,This NPC has the wrong profession.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_REVISION,Another change was saved first. Reloading.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_CREATE,Could not save the new kingdom.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_LOAD,Could not load save data.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_WRITE,Could not save changes.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_STAGE,A higher Kingdom Stage is required.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_GOLD,Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_PREMIUM,Premium {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_RECOVERED,A backup save was recovered.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_STAGE,Kingdom Stage {stage},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_ALCHEMY,Alchemy unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_BLACKSMITH,Smithing unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_GUILD,Guild requests unlock in P11.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_INFIRMARY,Treatment unlocks in P09.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_STORE,Trading unlocks in P08.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_TAVERN,Recruitment unlocks in P13.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_WAREHOUSE,Warehouse operations unlock in P07.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_STAGE,Unlocks at Kingdom Stage {stage}.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_ASSIGN_TITLE,Assign Management NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_BUILD_TITLE,Build Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_UPGRADE_TITLE,Upgrade Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_CRAFT,Craft,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_KINGDOM,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MENU,Menu,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MERCENARIES,Mercenaries,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_RECRUIT,Recruit,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_REGION,Region,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_ACTIVE,Active,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDABLE,Ready to build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDING,Building,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_LOCKED,Locked,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_STOPPED,Stopped,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_UPGRADING,Upgrading,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STOP_NPC_REQUIRED,Assign the required management NPC to activate.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OPERATIONS,Operations,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OVERVIEW,Overview,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_STAFF,Staff,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIMER_TICK_DESC,Facility remaining-time display interval,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_FINISHES,Finishes at {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_REMAINING,Time remaining: {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_CONTENT,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_EMPTY,No facility data is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_ERROR,Kingdom cannot be displayed.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOADING,Loading kingdom…,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOCKED,This facility is locked.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_OFFLINE,Offline: local facility management remains available.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_ASSIGN,NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BACK,뒤로,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BUILD,건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CANCEL,취소,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLAIM,완료,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLOSE,닫기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CONFIRM,확인,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_RETRY,다시 시도,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UNASSIGN,배치 해제,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 자동 저장 debounce,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_GOLD,왕국 골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DEV_CLOCK_DESC,P04 테스트 시계 모드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DRAWER_DURATION_DESC,시설 Drawer 애니메이션 시간,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_GOLD,왕국 골드가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_NOT_READY,아직 완료되지 않았습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_RUNNING,이미 진행 중인 작업이 있습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MATERIAL,건설 재료가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MAX_LEVEL,최대 레벨입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_ASSIGNED,이미 다른 시설에 배치된 NPC입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_NONE,배치할 관리 NPC가 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_PROFESSION,이 시설에 맞는 직종이 아닙니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_CREATE,새 왕국을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_LOAD,저장 데이터를 불러오지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_WRITE,변경 사항을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_STAGE,왕국 단계가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_GOLD,골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_PREMIUM,프리미엄 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_RECOVERED,백업 저장을 복구했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_STAGE,왕국 단계 {stage},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_ALCHEMY,연금술 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_BLACKSMITH,제작 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_GUILD,의뢰 기능은 P11에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_INFIRMARY,치료 기능은 P09에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_STORE,거래 기능은 P08에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_TAVERN,모집 기능은 P13에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_WAREHOUSE,창고 기능은 P07에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_STAGE,왕국 단계 {stage}에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_ASSIGN_TITLE,관리 NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_BUILD_TITLE,시설 건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_UPGRADE_TITLE,시설 업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_CRAFT,제작,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_KINGDOM,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MENU,메뉴,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MERCENARIES,용병,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_RECRUIT,모집,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_REGION,지역,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_ACTIVE,가동 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDABLE,건설 가능,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDING,건설 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_LOCKED,잠김,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_STOPPED,가동 정지,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_UPGRADING,업그레이드 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STOP_NPC_REQUIRED,올바른 관리 NPC를 배치하면 가동됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OPERATIONS,운영,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OVERVIEW,개요,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_STAFF,직원,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIMER_TICK_DESC,시설 남은 시간 표시 주기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_FINISHES,완료 예정 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_REMAINING,남은 시간 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_CONTENT,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_EMPTY,시설 정보를 찾을 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_ERROR,왕국을 표시할 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOADING,왕국을 불러오는 중…,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOCKED,아직 잠긴 시설입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_OFFLINE,오프라인: 로컬 시설 관리는 사용할 수 있습니다.,P04_UI,CONFIRMED,TRUE
```
placeholder 문법은 ICU가 아닌 named token `{amount}`, `{item}`, `{stage}`, `{time}`이며 허용 token set을 key별 validator가 검사한다. literal brace는 `{{`/`}}`. 숫자는 locale formatter, 시간 duration은 `H:mm:ss` 또는 `m:ss`, absolute time은 locale short time이다. P04 문자열은 count-dependent 문장을 쓰지 않으므로 ko/en plural category가 없고, future plural은 별도 key suffix `_ONE/_OTHER`를 추가해야 한다. unknown/missing token은 `LOCALIZATION_PLACEHOLDER_MISMATCH`다.

## 16. Domain/Application/Infrastructure/Presentation type 계약

**상태: CONFIRMED**

| TypeName | Namespace | Layer | Responsibility | Dependencies | PublicMembers |
|---|---|---|---|---|---|
| Facility | KingdomTycoon.Domain.Facilities | Domain | entity + invariant | FacilityId, FacilityJob | Level,State,AssignedNpcId,Job,Storage |
| FacilityState | KingdomTycoon.Domain.Facilities | Domain | six-value enum | none | LOCKED..STOPPED |
| FacilityJobStatus | KingdomTycoon.Domain.Facilities | Domain | job enum | none | RUNNING,READY,CLAIMED,CANCELLED |
| FacilityStopReason | KingdomTycoon.Domain.Facilities | Domain | NONE\|NPC_REQUIRED\|NPC_PROFESSION_MISMATCH\|PHASE_LOCKED | none | Value |
| FacilityInvariantValidator | KingdomTycoon.Domain.Facilities | Domain | FAC-INV validation | catalog projection | Validate(snapshot,now) |
| FacilityTransitionPolicy | KingdomTycoon.Domain.Facilities | Domain | pure transition guard | validator | Evaluate(trigger,context) |
| FacilityDomainEvent | KingdomTycoon.Domain.Facilities.Events | Domain | typed events | IDs/UTC | EventId,OccurredAtUtc,FacilityId,OperationId |
| GetKingdomScreenQuery | KingdomTycoon.Application.Facilities.Queries | Application | 8-view projection | catalog,save,clock | Execute():KingdomScreenDto |
| GetFacilityDetailQuery | KingdomTycoon.Application.Facilities.Queries | Application | drawer projection | catalog,save | Execute(facilityId):FacilityDetailDto |
| StartFacilityBuildHandler | KingdomTycoon.Application.Facilities.Commands | Application | build transaction | UoW,clock,id | Handle(Input):Result |
| StartFacilityUpgradeHandler | KingdomTycoon.Application.Facilities.Commands | Application | upgrade transaction | UoW,clock,id | Handle(Input):Result |
| ClaimFacilityJobHandler | KingdomTycoon.Application.Facilities.Commands | Application | claim transaction | UoW,clock | Handle(Input):Result |
| AssignManagementNpcHandler | KingdomTycoon.Application.Facilities.Commands | Application | assign transaction | UoW | Handle(Input):Result |
| UnassignManagementNpcHandler | KingdomTycoon.Application.Facilities.Commands | Application | unassign transaction | UoW | Handle(Input):Result |
| IFacilityUnitOfWork | KingdomTycoon.Application.Facilities | Application | clone/compare/commit | Save repository | Begin(expectedRevision),Commit() |
| ITrustedUtcClock | KingdomTycoon.Application.Abstractions | Application | UTC+monotonic | none | UtcNow,MonotonicTicks |
| IUuidV7Provider | KingdomTycoon.Application.Abstractions | Application | ordered IDs | none | NewUuidV7() |
| CanonicalFacilityCatalog | KingdomTycoon.Infrastructure.Facilities | Infrastructure | CSV→immutable DTO | ContentCatalog | GetFacility/GetLevel/GetDuration |
| SaveV1FacilityMapper | KingdomTycoon.Infrastructure.Facilities | Infrastructure | strict Save mapping | Save v1 types | Read/Write |
| AtomicFacilityUnitOfWork | KingdomTycoon.Infrastructure.Facilities | Infrastructure | revision+atomic persistence | AtomicSaveRepository | Begin/Commit/Rollback |
| P03ToP04ContentMigration | KingdomTycoon.Infrastructure.Content.Migrations | Infrastructure | signature normalization | catalog,uuid,clock | CanApply/Apply |
| P04LocalizationLookup | KingdomTycoon.Infrastructure.Localization | Infrastructure | typed key formatting | LocalizationCatalog | Format(key,args) |
| KingdomScreenPresenter | KingdomTycoon.Presentation.Kingdom | Presentation | lifecycle/query/event bind | queries,commands,views | Initialize/Refresh/Dispose |
| FacilityWorldView | KingdomTycoon.Presentation.Kingdom.Views | Presentation | facility visual/input | DTO only | Bind/SetSelected |
| FacilityDrawerView | KingdomTycoon.Presentation.Kingdom.Views | Presentation | detail/tabs/actions | DTO only | Open/Bind/Close |
| FacilityStateRenderer | KingdomTycoon.Presentation.Kingdom.Views | Presentation | variant mapping | assets | Render(state,reason) |
| AndroidBackInputAdapter | KingdomTycoon.Presentation.Input | Presentation | back priority | modal,drawer,navigation | HandleBack() |
`KingdomScreenDto` = contentVersion,saveRevision,stageId,kingdomGold,freePremiumCache,paidPremiumCache,recovered,offline,facilities[8]. `FacilityWorldDto` = facilityId,nameText,state,level,selected,stopReason,remainingSeconds,assetAddress,anchor. `FacilityDetailDto` = world fields + operationMode,requiredProfession,effectText,maxLevel,nextLevel,costGold,costItems,job,npcCandidates,tabs,primaryAction,disabledReason. `FacilityJobDto` = operationId,type,status,startedAtUtc,finishesAtUtc,remainingSeconds. `CostItemDto` = itemId,nameText,required,owned. JObject/Save/Catalog type은 public DTO에 금지한다. View는 presenter event만 호출한다.

## 17. event/error registry

**상태: CONFIRMED**

| EventId | Payload | Publish timing |
|---|---|---|
| FacilityUnlocked | facilityId,stageId | atomic Save success 후 main thread, 표 순서 |
| FacilityBuildStarted | facilityId,operationId,targetLevel,finishesAtUtc | atomic Save success 후 main thread, 표 순서 |
| FacilityJobReady | facilityId,operationId | atomic Save success 후 main thread, 표 순서 |
| FacilityBuildCompleted | facilityId,operationId,level | atomic Save success 후 main thread, 표 순서 |
| FacilityUpgradeStarted | facilityId,operationId,fromLevel,targetLevel,finishesAtUtc | atomic Save success 후 main thread, 표 순서 |
| FacilityUpgradeCompleted | facilityId,operationId,level | atomic Save success 후 main thread, 표 순서 |
| FacilityStopped | facilityId,reason | atomic Save success 후 main thread, 표 순서 |
| FacilityActivated | facilityId | atomic Save success 후 main thread, 표 순서 |
| ManagementNpcAssigned | facilityId,npcInstanceId | atomic Save success 후 main thread, 표 순서 |
| ManagementNpcUnassigned | facilityId,npcInstanceId | atomic Save success 후 main thread, 표 순서 |
| FacilitySaveRecovered | source,revision | atomic Save success 후 main thread, 표 순서 |
| Code | Trigger | UserMessageKey | Retryable | LoggingLevel | SaveMutationAllowed |
|---|---|---|---|---|---|
| FACILITY_NOT_FOUND | facilityId missing | TXT_P04_UI_ERROR | False | ERROR | False |
| FACILITY_NOT_BUILT | assign/unassign before build | TXT_P04_UI_LOCKED | False | INFO | False |
| FACILITY_STATE_INVALID | transition invalid | TXT_P04_UI_ERROR | False | WARN | False |
| FACILITY_LOCKED | state LOCKED | TXT_P04_UI_LOCKED | False | INFO | False |
| FACILITY_LEVEL_MAX | level==cap | TXT_P04_ERROR_MAX_LEVEL | False | INFO | False |
| FACILITY_STAGE_REQUIRED | stage guard | TXT_P04_ERROR_STAGE | False | INFO | False |
| FACILITY_GOLD_INSUFFICIENT | gold<cost | TXT_P04_ERROR_GOLD | True | INFO | False |
| FACILITY_MATERIAL_INSUFFICIENT | item<cost | TXT_P04_ERROR_MATERIAL | True | INFO | False |
| FACILITY_JOB_ALREADY_RUNNING | job exists | TXT_P04_ERROR_JOB_RUNNING | False | INFO | False |
| FACILITY_JOB_NOT_FOUND | claim without matching job | TXT_P04_UI_ERROR | False | INFO | False |
| FACILITY_JOB_NOT_READY | now<finish/status RUNNING | TXT_P04_ERROR_JOB_NOT_READY | True | INFO | False |
| FACILITY_JOB_CANCEL_UNSUPPORTED | cancel command | TXT_P04_ACTION_CANCEL | False | INFO | False |
| FACILITY_NPC_REQUIRED | managed without candidate | TXT_P04_ERROR_NPC_NONE | True | INFO | False |
| FACILITY_NPC_PROFESSION_MISMATCH | profession mismatch | TXT_P04_ERROR_NPC_PROFESSION | False | INFO | False |
| FACILITY_NPC_ALREADY_ASSIGNED | NPC linked elsewhere | TXT_P04_ERROR_NPC_ASSIGNED | False | INFO | False |
| FACILITY_NPC_FORBIDDEN | SYSTEM assignment | TXT_P04_ERROR_NPC_PROFESSION | False | WARN | False |
| FACILITY_OPERATION_DUPLICATE | duplicate apply attempt | TXT_P04_UI_ERROR | False | ERROR | False |
| FACILITY_OPERATION_HASH_MISMATCH | same ID different hash | TXT_P04_UI_ERROR | False | ERROR | False |
| FACILITY_SAVE_REVISION_CONFLICT | expected!=current | TXT_P04_ERROR_REVISION | True | WARN | False |
| FACILITY_CLOCK_ROLLBACK_DETECTED | UTC rollback >2s | TXT_P04_UI_ERROR | True | WARN | False |
| KINGDOM_STAGE_REGRESSION_FORBIDDEN | new stage lower than stored | TXT_P04_ERROR_STAGE | False | ERROR | False |
| SAVE_FACILITY_STATE_JOB_MISMATCH | load invariant | TXT_P04_ERROR_SAVE_LOAD | False | ERROR | False |
| SAVE_FACILITY_NPC_LINK_INVALID | bidirectional mismatch | TXT_P04_ERROR_SAVE_LOAD | False | ERROR | False |
| SAVE_CREATE_FAILED | new file write failed | TXT_P04_ERROR_SAVE_CREATE | True | ERROR | False |
| SAVE_WRITE_FAILED | atomic write failed | TXT_P04_ERROR_SAVE_WRITE | True | ERROR | False |
| CONTENT_P04_PACKAGE_GOLDEN_MISMATCH | manifest/hash mismatch | TXT_P04_UI_ERROR | False | ERROR | False |
## 18. Save sequence와 recovery

**상태: CONFIRMED**

1. `AppRoot`가 clock, UUID, content loader, atomic Save repository, localization을 만든다.
2. `.2` manifest JCS/hash, 62 descriptors/CSV/FK/Addressable registry를 검증한다.
3. profile slot을 찾고 없으면 NEW draft를 생성한다.
4. P03 원자 복구 순서로 active/tmp/bak1..3을 digest 검증·정렬해 하나를 선택한다.
5. strict Save v1을 parse하고 content compatibility 후 5장의 signature migration을 cloned payload에 적용한다.
6. READY normalization과 terminal checkpoint cleanup을 operation journal로 검증한다. 변경이 있으면 한 번만 atomic Save한다.
7. 12 invariant를 검증하고 immutable Kingdom projection을 만든다.
8. command tap은 facility별 semaphore와 화면 전체 commit gate로 중복 차단한다.
9. handler가 expectedRevision을 검사하고 cloned UoW에서 guard→journal→domain mutation→invariant를 수행한다.
10. payload JCS/hash, EnvelopeDigestInput/hash를 재계산하고 revision+1로 lock/atomic Save한다. 한 command당 정확히 1회다.
11. 성공 뒤 event를 발행하고 presenter를 새 revision DTO로 bind한다.
12. 실패하면 clone을 폐기하고 disk current revision을 reload; 파일 commit 뒤 event publish만 실패한 경우 재저장하지 않고 refresh한다.

autosave debounce=0.5s는 settings 같은 non-command dirty만 대상이다. 모든 P04 mutation command는 즉시 저장하므로 debounce에 합치지 않는다. pause/focus lost는 dirty가 있으면 최대 2s best-effort flush, quit은 동일하되 Android kill 성공을 가정하지 않는다. 같은 facility concurrent tap은 첫 요청만 수행하고 나머지는 UI disabled; 다른 facility도 단일 Save writer로 직렬화한다. 복구 후보를 썼으면 HUD toast `TXT_P04_HUD_RECOVERED`를 세션당 1회 표시한다. saveVersion=1, strict schema 변경 없음, migration registry에 content migration `P03_TO_P04_001`만 추가한다.

## 19. 테스트·golden 전체 목록

**상태: CONFIRMED**

| TestId | TestClass | Mode | Given | When | Then | Fixture | Timeout |
|---|---|---|---|---|---|---|---|
| P04-E-001 | FacilityTransitionPolicyTests | EditMode | LOCKED | all declared triggers | matrix result/error exact | STATE_LOCKED | 5s |
| P04-E-002 | FacilityTransitionPolicyTests | EditMode | BUILDABLE | all declared triggers | matrix result/error exact | STATE_BUILDABLE | 5s |
| P04-E-003 | FacilityTransitionPolicyTests | EditMode | BUILDING | all declared triggers | matrix result/error exact | STATE_BUILDING | 5s |
| P04-E-004 | FacilityTransitionPolicyTests | EditMode | ACTIVE | all declared triggers | matrix result/error exact | STATE_ACTIVE | 5s |
| P04-E-005 | FacilityTransitionPolicyTests | EditMode | UPGRADING | all declared triggers | matrix result/error exact | STATE_UPGRADING | 5s |
| P04-E-006 | FacilityTransitionPolicyTests | EditMode | STOPPED | all declared triggers | matrix result/error exact | STATE_STOPPED | 5s |
| P04-E-007 | FacilityInvariantTests | EditMode | 8 facility modes | validate all states | SYSTEM/MANAGED invariants exact | P04_INVARIANTS | 5s |
| P04-E-008 | FacilityCatalogTests | EditMode | 32 levels | lookup costs/caps | all exact rows | P04_CONTENT_2 | 5s |
| P04-E-009 | FacilityCostTests | EditMode | sufficient gold/items | start upgrade | sorted atomic deduction | P04_COST_OK | 5s |
| P04-E-010 | FacilityCostTests | EditMode | each resource insufficient | start | no mutation + exact error | P04_COST_FAIL | 5s |
| P04-E-011 | FacilityIdempotencyTests | EditMode | same op/hash | retry | same output/no double cost | P04_DUP_SAME | 5s |
| P04-E-012 | FacilityIdempotencyTests | EditMode | same op/different hash | retry | hash mismatch/no mutation | P04_DUP_SPLIT | 5s |
| P04-E-013 | FacilityRevisionTests | EditMode | stale revision | command | conflict/reload | P04_REVISION | 5s |
| P04-E-014 | FacilityClockTests | EditMode | now < finishes | observe | RUNNING if < else READY | P04_TIME_14 | 5s |
| P04-E-015 | FacilityClockTests | EditMode | now == finishes | observe | RUNNING if < else READY | P04_TIME_15 | 5s |
| P04-E-016 | FacilityClockTests | EditMode | now > finishes | observe | RUNNING if < else READY | P04_TIME_16 | 5s |
| P04-E-017 | ManagementNpcTests | EditMode | matching NPC | assign/unassign | STOPPED↔ACTIVE links | P04_NPC_FLOW | 5s |
| P04-E-018 | ManagementNpcTests | EditMode | wrong profession | assign | no mutation/error | P04_NPC_WRONG | 5s |
| P04-E-019 | ManagementNpcTests | EditMode | NPC already linked | assign | no mutation/error | P04_NPC_DUP | 5s |
| P04-E-020 | P03ToP04MigrationTests | EditMode | BEFORE | migrate | byte-semantic AFTER + hashes | P03_TO_P04 | 5s |
| P04-E-021 | P03ToP04MigrationTests | EditMode | near-miss signature | migrate | not applied | P03_TO_P04_NEAR_MISS | 5s |
| P04-E-022 | SaveRoundTripTests | EditMode | each successful command | commit/load | strict equality + digest | P04_ROUND_TRIP | 5s |
| P04-E-023 | SaveInvalidFacilityTests | EditMode | each invalid state/job/NPC | load | fail closed exact code | P04_INVALID_SAVE | 5s |
| P04-E-024 | P04ContentGoldenTests | EditMode | generated package | hash | 62 rows/descriptors/digest exact | P04_CONTENT_2 | 20s |
| P04-P-001 | KingdomBootstrapPlayModeTests | PlayMode | Bootstrap | open Kingdom | 8 world views exactly once | P04_NEW_GAME | 15s |
| P04-P-002 | FacilityDrawerPlayModeTests | PlayMode | content | select/reselect/scrim | bind/replace/close | P04_NEW_GAME | 15s |
| P04-P-003 | FacilityVisualStatePlayModeTests | PlayMode | 6 state fixtures | render | variant exact | P04_ALL_STATES | 15s |
| P04-P-004 | FacilityBuildPlayModeTests | PlayMode | BUILDABLE | build/advance/claim/reload | STOPPED/ACTIVE retained | P04_BUILD | 15s |
| P04-P-005 | ManagementNpcPlayModeTests | PlayMode | STOPPED store | assign/unassign | ACTIVE/STOPPED retained | P04_NPC_FLOW | 15s |
| P04-P-006 | KingdomCommonStatesPlayModeTests | PlayMode | six UI fixtures | enter | state table exact | P04_UI_STATES | 15s |
| P04-P-007 | AndroidBackPlayModeTests | PlayMode | modal+drawer | back×3 | modal then drawer then nav | P04_INPUT | 15s |
| P04-P-008 | SingleModalPlayModeTests | PlayMode | modal open | second action | no second modal | P04_INPUT | 15s |
| P04-P-009 | SafeAreaPlayModeTests | PlayMode | 16:9/18:9/20:9 | render | no critical crop; 64px hits | P04_LAYOUT | 15s |
| P04-P-010 | KoreanLargeTextPlayModeTests | PlayMode | ko 130% | open all drawers | no clipping; scroll | P04_LONG_KO | 15s |
모든 테스트는 `DeterministicClock(2026-07-19T00:00:00.000Z)`와 sequence UUIDv7 provider를 DI한다. wall clock, `Guid.NewGuid`, coroutine real-time wait, network는 금지한다. hash golden은 NEW/BEFORE/AFTER 및 manifest 값이다.

## 20. 성능·수명주기

**상태: CONFIRMED**

| Metric | Acceptance |
|---|---|
| Facility view creation | Kingdom 첫 enter에 정확히 8; 같은 scene 동안 추가 instantiate 0, pool/rebind |
| Subscriptions | Presenter.Initialize 1회 subscribe, OnDisable/Dispose에서 전부 unsubscribe; duplicate 0 |
| Polling | Save/catalog polling 0; RUNNING/READY timer label만 1.0s tick, 화면 비활성 시 0 |
| Command GC | handler+projection 합계 64 KiB 이하 권장, steady timer 0 B/frame |
| Drawer animation | 60 fps target, Android 기준 p95 frame<=16.67ms, 단일 Canvas rebuild |
| 10-minute memory | managed heap baseline 대비 지속 증가 <=1 MiB; view/event count 불변 |
| Scene exit | Presenter, CTS, timer, view listeners 해제; AppRoot catalog/Save service만 유지 |
| Profiler capture | CPU Timeline, GC.Alloc, UI.Render/UI.Batch, Rendering, Memory, Addressables, 300 frames |
## 21. Editor generation·Addressables·Android build

**상태: CONFIRMED**

| Purpose | Exact entrypoint |
|---|---|
| all setup | KingdomTycoon.Editor.P04KingdomFacilitiesSetup.Run |
| placeholder generator | KingdomTycoon.Editor.P04FacilityPlaceholderGenerator.Generate |
| scene configurator | KingdomTycoon.Editor.P04KingdomSceneConfigurator.Configure |
| verify | KingdomTycoon.Editor.P04GeneratedAssetVerifier.VerifyAll |
| Android build | KingdomTycoon.Editor.P04AndroidBuilder.Build |
managed root=`Assets/KingdomTycoon/ContentGenerated/P04Kingdom`; group=`Content-P04-Kingdom-v1`; label=`P04_KINGDOM`; address는 11장의 값이다. generator fingerprint는 asset row JCS SHA-256이며 동일하면 timestamp까지 no-op, 달라지면 marker가 있는 managed asset만 재생성한다. `.meta`는 생성 asset과 같은 commit에 포함한다. scene/prefab YAML 직접 작성은 금지다. Verify 순서: content manifest/hash→CSV/FK→asset registry/address uniqueness→prefab components/pivots/hit rect→Kingdom scene roots/camera/canvas→build scenes→strict Save fixture→Android settings. APK=`Builds/Android/KingdomTycoon-P04-Development.apk`.

```powershell
Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P04KingdomFacilitiesSetup.Run `
  -logFile .\client-unity\Logs\p04-setup.log

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform EditMode `
  -testResults .\client-unity\Logs\p04-editmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -runTests -testPlatform PlayMode `
  -testResults .\client-unity\Logs\p04-playmode-results.xml

Unity.exe -batchmode -quit -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P04AndroidBuilder.Build `
  -logFile .\client-unity\Logs\p04-android-build.log
```

## 22. 수동 검증·capture 목록

**상태: CONFIRMED**

| CaptureId | FixtureId | Resolution | Filename | SuccessCondition |
|---|---|---|---|---|
| P04_CAP_01 | P04_NEW_GAME | 1920×1080 | p04_01_kingdom_1920x1080.png | 8 시설/HUD/nav/safe frame |
| P04_CAP_02 | P04_NEW_GAME | 2400×1080 | p04_02_kingdom_20x9.png | critical crop 0; safe margins |
| P04_CAP_03 | P04_LOCKED_STAGE | 1920×1080 | p04_03_locked_drawer.png | silhouette/reason/progress |
| P04_CAP_04 | P04_BUILDABLE_STORE | 1920×1080 | p04_04_buildable_cost.png | gold/material/action exact |
| P04_CAP_05 | P04_BUILDING_STORE | 1920×1080 | p04_05_building_timer.png | remaining/finish/manual claim |
| P04_CAP_06 | P04_ACTIVE_TAVERN | 1920×1080 | p04_06_active_system.png | ACTIVE+P13 disabled reason |
| P04_CAP_07 | P04_STOPPED_STORE | 1920×1080 | p04_07_stopped_npc_required.png | NPC_REQUIRED and assign |
| P04_CAP_08 | P04_ACTIVE_STORE | 1920×1080 | p04_08_active_npc.png | Merchant linked/working |
| P04_CAP_09 | P04_RECOVERED_BAK1 | 1920×1080 | p04_09_save_recovered.png | recovery toast+revision debug(dev) |
## 23. 구현 파일 지도·커밋 분리

**상태: CONFIRMED**

| Path | NewOrModified | Responsibility | GeneratedOrAuthored | OwningCommit |
|---|---|---|---|---|
| docs/design/TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md | NEW | adopted contract | AUTHORED | P04-01 |
| client-unity/Assets/StreamingAssets/Content/1.0.0-content.2/ | NEW | 62-table immutable BASE | GENERATED | P04-01 |
| client-unity/Assets/KingdomTycoon/Runtime/Domain/Facilities/ | NEW | entity/invariant/transition/events | AUTHORED | P04-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Application/Facilities/ | NEW | queries/commands/UoW ports | AUTHORED | P04-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Facilities/ | NEW | catalog/Save mapper/UoW | AUTHORED | P04-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Content/Migrations/P03ToP04ContentMigration.cs | NEW | one-time signature migration | AUTHORED | P04-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Presentation/Kingdom/ | MODIFIED | world/HUD/Drawer presenter/views | AUTHORED | P04-03 |
| client-unity/Assets/KingdomTycoon/ContentGenerated/P04Kingdom/ | NEW | placeholder sprites/prefabs | GENERATED | P04-03 |
| client-unity/Assets/KingdomTycoon/Scenes/Kingdom.unity | MODIFIED | generated P04 hierarchy | GENERATED | P04-03 |
| client-unity/Assets/KingdomTycoon/Editor/P04KingdomFacilitiesSetup.cs | NEW | idempotent setup | AUTHORED | P04-03 |
| client-unity/Assets/KingdomTycoon/Editor/P04AndroidBuilder.cs | NEW | guarded APK build | AUTHORED | P04-04 |
| client-unity/Assets/KingdomTycoon/Tests/EditMode/Facilities/ | NEW | domain/content/Save tests | AUTHORED | P04-04 |
| client-unity/Assets/KingdomTycoon/Tests/PlayMode/Kingdom/ | NEW | UI/layout/input tests | AUTHORED | P04-04 |
| docs/reports/P04_KINGDOM_FACILITIES_REPORT.md | NEW | tests/build/captures/result | AUTHORED | P04-05 |
| .github/workflows/client-unity.yml | MODIFIED | P04 guards/artifacts | AUTHORED | P04-05 |
| Commit | Scope | Must not include |
|---|---|---|
| P04-01 | 설계 채택 + canonical .2 | runtime source/UI |
| P04-02 | domain/application/Save integration | scene/assets/tests |
| P04-03 | Kingdom world/Drawer/generated assets | domain/data changes |
| P04-04 | EditMode/PlayMode/build guard | CI/report |
| P04-05 | CI, captures, completion report, Draft PR update | feature behavior changes |
## 24. P04 완료 판정 checklist

**상태: CONFIRMED**

- [ ] 8 시설×6 상태 matrix/invariant 구현과 test 통과
- [ ] 미건설 level1 및 state/job strict validation
- [ ] NEW/BEFORE/AFTER JSON/digest golden 일치
- [ ] 8×5 availability/32 duration/32 effect 정확 일치
- [ ] 비용·journal·revision·idempotency 원자 처리
- [ ] starter NPC 4명 및 STOPPED↔ACTIVE
- [ ] content .2 62 descriptors/manifest digest 일치
- [ ] 8 placeholder/address/layout/build validation
- [ ] HUD/Drawer/6 UI states/ko-en bundle
- [ ] 계층 API에서 View direct Save/Catalog/HTTP 0회
- [ ] atomic Save/recovery/rollback test
- [ ] EditMode/PlayMode 전체 green
- [ ] Android Development APK 생성·설치·Bootstrap→Kingdom
- [ ] 9 capture와 profiler 기준 충족
- [ ] P04 report와 Draft PR 갱신; P04 범위 밖 기능 0개

체크는 구현 결과가 증명할 때만 `[x]`로 바꾼다. 설계 문서 생성 자체는 P04 완료 판정이 아니다.

## 25. UNRESOLVED

**상태: CONFIRMED**

`NONE`

P04 구현자가 선택해야 할 상태, 값, ID, row, UI 배치, Save/migration, test/build 결정은 남아 있지 않다. TUNABLE 값 변경은 새 contentVersion과 golden 재생성을 요구한다.
