# TYCOON P05 용병 로스터 최종 통합 설계 v1.0

## 1. 문서 상태·버전·권위

**상태: CONFIRMED**

| 항목 | Exact value |
|---|---|
| 문서 | TYCOON_P05_MERCENARY_ROSTER_COMPLETE_DESIGN_v1.0.md |
| Phase | P05_MERCENARY_ROSTER |
| 선행 계약 | P03 canonical/Save + P04 v1.0/v1.0.1/v1.0.2 |
| Unity | 6000.3.20f1 |
| saveVersion | 1 유지 |
| contentVersion | 1.0.0-content.3 |
| csvSchemaSetVersion | 3 유지 |
| manifest contract | 2 / urn:tycoon:content-manifest:v2 |
| 구현 필수 UNRESOLVED | NONE |
각 절의 상태는 표와 문장 전체에 적용한다. 동일 항목은 저장소 CONFIRMED → P04 v1.0.2 → 본 문서 순으로 해석하며, P05가 명시적으로 CORRECTION/MIGRATION한 항목만 선행 계약을 바꾼다.

## 2. P05 범위와 Phase 경계

**상태: CONFIRMED**

| 기능 | P05 판정 | Owning Phase | Exact P05 behavior |
|---|---|---|---|
| 용병 strict domain/Save mapper | IMPLEMENT | P05 | 전체 Save field validation/read projection |
| starter 용병 4명 | IMPLEMENT | P05 | P04→P05 1회 content migration |
| 목록·검색·필터·정렬·상세 4탭 | IMPLEMENT | P05 | session-only query state |
| 활동/비활동 전환 | IMPLEMENT | P05 | expectedRevision+atomic Save |
| 16 active/24 owned cap | IMPLEMENT | P05 | Lodge projection·load/command invariant |
| 전투/자율 AI 실행 | DEFERRED | P06 | 현재 상태/이유 표시만 |
| 장비 획득·교체 | DEFERRED | P07 | 4 slots read-only |
| 포션/인벤토리 조작 | DEFERRED | P07 | 수량 read-only |
| 상점/생산/제작 | DEFERRED | P08/P09/P10 | 호출 0회 |
| 레벨업·승급 심사 | DEFERRED | P11 | level/exp/promotion read-only |
| 지역 배치 | DEFERRED | P12 | currentRegion 표시만 |
| 모집/해고/이름 변경 | DEFERRED | P13 | 생성 UI command 없음 |
| 레이드 파티 | DEFERRED | P14 | raid records read-only |
| 서버 API | DEFERRED | P16 | HTTP 호출 0회 |
## 3. correction·migration 요약

**상태: MIGRATION / CONFIRMED**

| ID | 결정 | 호환 |
|---|---|---|
| P05-M01 | P04 `.2` Save를 `.3`으로 올리고 mercenaries=[]이면 starter 4명 추가 | P04에는 용병 생성 경로가 없으므로 사용자 결과 덮어쓰기 없음 |
| P05-M02 | `.2` Save에 mercenary가 1명 이상이면 배열을 그대로 보존하고 starter 추가 안 함 | 개발 fixture/향후 import 무손실 |
| P05-C01 | P04 active content `.2` constant를 P05 `.3`으로 교체 | `.1/.2/.3` immutable package 모두 APK 보존 |
| P05-C02 | P04 nav의 Mercenaries locked를 visible+interactable로 전환 | Save shape 변화 없음 |
## 4. Mercenary domain·불변식

**상태: CONFIRMED**

| InvariantId | AppliesTo | ExactRule | Timing | ErrorCode |
|---|---|---|---|---|
| MERC-INV-001 | identity | instanceId RFC9562 UUIDv7; Save unique | load/create/precommit | SAVE_MERCENARY_ID_INVALID |
| MERC-INV-002 | generation | profile/name/appearance/growth seeds nonnull; Seed64 anchored uint64 | load/create | SAVE_MERCENARY_GENERATION_INVALID |
| MERC-INV-003 | identity content | job/grade/rank/personality/traits enabled hard FK | load/create | SAVE_MERCENARY_CONTENT_REF_INVALID |
| MERC-INV-004 | grade | C/B/A/S/SS; immutable after creation | mutation | MERCENARY_GRADE_IMMUTABLE |
| MERC-INV-005 | rank/level | level 1..rank.max_level; exp nonnegative | load/precommit | SAVE_MERCENARY_LEVEL_INVALID |
| MERC-INV-006 | traits | unique; job eligible; count<=grade.initial_trait_count+rank.trait_slot_bonus | load/create | SAVE_MERCENARY_TRAITS_INVALID |
| MERC-INV-007 | wallet | personalGold/contribution safe nonnegative | load/precommit | SAVE_MERCENARY_COUNTER_INVALID |
| MERC-INV-008 | slots | active true count<=kingdom.activeMercenaryLimit; total<=ownedMercenaryLimit | load/precommit | SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED |
| MERC-INV-009 | inactive | active=false이면 autonomy가 IDLE_TOWN\|PROMOTION_READY\|INJURED 중 하나; region/target null | load/precommit | SAVE_MERCENARY_INACTIVE_STATE_INVALID |
| MERC-INV-010 | autonomy | 17 state discriminator와 region/target/time/reason 10 enum 일치 | load/precommit | SAVE_MERCENARY_AUTONOMY_INVALID |
| MERC-INV-011 | potions | potionId unique; quantity>0; 0행 삭제 | load/precommit | SAVE_MERCENARY_POTION_INVALID |
| MERC-INV-012 | equipment | 정확히 WEAPON/ARMOR/HELMET/ACCESSORY; inventory와 양방향/직업 eligibility | load/precommit | SAVE_MERCENARY_EQUIPMENT_LINK_INVALID |
| MERC-INV-013 | promotion | NONE\|READY\|IN_REVIEW\|COMPLETED_PENDING_APPLY tagged union | load/precommit | SAVE_MERCENARY_PROMOTION_INVALID |
| MERC-INV-014 | records | 4 counters safe nonnegative; unknown counter 금지 | load/precommit | SAVE_MERCENARY_RECORD_INVALID |
| MERC-INV-015 | displayName | trim 후 Unicode scalar 1..20; control 금지; snapshot | load/create | SAVE_MERCENARY_NAME_INVALID |
| MERC-INV-016 | Lodge | Kingdom limits=(4,8),(8,12),(12,18),(16,24) for Lodge L1..4 | load/facility claim | SAVE_MERCENARY_LODGE_LIMIT_MISMATCH |
Save 배열의 canonical order는 instanceId UTF-8 ordinal이다. UI 정렬은 Save order를 변경하지 않는다. targetInstanceId만 soft runtime ref이며 미해석 시 P03 계약대로 IDLE_TOWN 정규화 후 새 revision; 다른 실패는 자동 repair 없이 candidate fail closed다.

## 5. starter 생성·P04→P05 golden

**상태: MIGRATION / CONFIRMED**

| 순서 | instanceId | name | job | grade | rank | personality | traits | personalGold | active |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 019f7cd2-8800-7002-8000-000000000001 | 레온 | JOB_WARRIOR | GRADE_B | RANK_APPRENTICE | PERSONALITY_PRACTICAL | TRAIT_STRONG | 500 | TRUE |
| 2 | 019f7cd2-8800-7002-8000-000000000002 | 미라 | JOB_GUARDIAN | GRADE_C | RANK_APPRENTICE | PERSONALITY_CAUTIOUS | TRAIT_STURDY | 500 | TRUE |
| 3 | 019f7cd2-8800-7002-8000-000000000003 | 아린 | JOB_ARCHER | GRADE_B | RANK_APPRENTICE | PERSONALITY_BRAVE | TRAIT_KEEN_EYE | 500 | TRUE |
| 4 | 019f7cd2-8800-7002-8000-000000000004 | 세라 | JOB_CLERIC | GRADE_C | RANK_APPRENTICE | PERSONALITY_FRUGAL | TRAIT_ARCANE | 500 | TRUE |
starter는 generation reroll이 아니라 확정 snapshot이다. generationProfile=`GEN_MERC_STANDARD_V1`, level=1, exp=0, contribution=0, 모든 장비 null, potions empty, records 0, promotion NONE, autonomy IDLE_TOWN/NONE이다. migration transaction 시작 시 UUID provider를 직업 표 순서로 4회 호출하고 완성 snapshot을 검증한 뒤 한 atomic Save로 contentVersion/revision과 함께 commit한다. golden ID는 deterministic provider 결과다.

P05 신규 profile의 UUIDv7 호출 순서는 `saveId → profileId → NPC_MERCHANT → NPC_BLACKSMITH → NPC_ALCHEMIST → NPC_HEALER → starter WARRIOR → GUARDIAN → ARCHER → CLERIC`이다. P04 기존 profile migration은 앞의 save/profile/NPC ID를 보존하고 starter 4회만 호출한다. UUID 호출 하나라도 실패하면 draft 전체를 폐기한다.

### 5.1 적용 조건과 재시도

`.2` strict Save이면 migration registry가 정확히 한 번 적용된다. mercenaries=[]이면 4명 추가, nonempty이면 보존한다. `.3`에는 재적용하지 않는다. active/owned limit을 초과하면 원본 보존 후 `P05_MIGRATION_SLOT_LIMIT_INVALID`. Save 실패는 clone/새 UUID를 폐기하고 원본 `.2`를 유지한다. 재시도는 새 UUID sequence를 사용한다. atomic commit 후 crash는 `.3`으로 복구되어 중복 생성하지 않는다. operationJournal은 content migration subtype이 없으므로 추가하지 않는다.

### 5.2 `P05_NEW_GAME_GOLDEN` 전체 JSON

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.3",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 1,
  "createdAtUtc": "2026-07-20T00:00:00.000Z",
  "savedAtUtc": "2026-07-20T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b",
    "fileSha256": "a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a"
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
    "mercenaries": [
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000001",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1001",
        "appearanceSeed": "2001",
        "growthSeed": "3001",
        "displayName": "레온",
        "jobId": "JOB_WARRIOR",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_PRACTICAL",
        "traitIds": [
          "TRAIT_STRONG"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000002",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1002",
        "appearanceSeed": "2002",
        "growthSeed": "3002",
        "displayName": "미라",
        "jobId": "JOB_GUARDIAN",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_CAUTIOUS",
        "traitIds": [
          "TRAIT_STURDY"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000003",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1009",
        "appearanceSeed": "2003",
        "growthSeed": "3003",
        "displayName": "아린",
        "jobId": "JOB_ARCHER",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_BRAVE",
        "traitIds": [
          "TRAIT_KEEN_EYE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000004",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1004",
        "appearanceSeed": "2004",
        "growthSeed": "3004",
        "displayName": "세라",
        "jobId": "JOB_CLERIC",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_FRUGAL",
        "traitIds": [
          "TRAIT_ARCANE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      }
    ],
    "managementNpcs": [
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000004",
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
          "firstUnlockedAtUtc": "2026-07-20T00:00:00.000Z",
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
      "accrualCursorUtc": "2026-07-20T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-20T00:00:00.000Z",
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

### 5.3 `P04_TO_P05_GOLDEN_AFTER` 전체 JSON

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.3",
  "saveId": "019f77ac-2c00-7000-8000-000000000001",
  "profileId": "019f77ac-2c00-7000-8000-000000000002",
  "revision": 2,
  "createdAtUtc": "2026-07-19T00:00:00.000Z",
  "savedAtUtc": "2026-07-20T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "441d02adf6d58189ea0b15c8f5ea3919211c5aa0a0c1ca31f706fe0a254c887e",
    "fileSha256": "ef10df65febf0f049ebb330cc3adfb2c2f87985d90e3f015c079f204e85cbec1"
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
    "mercenaries": [
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000001",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1001",
        "appearanceSeed": "2001",
        "growthSeed": "3001",
        "displayName": "레온",
        "jobId": "JOB_WARRIOR",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_PRACTICAL",
        "traitIds": [
          "TRAIT_STRONG"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000002",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1002",
        "appearanceSeed": "2002",
        "growthSeed": "3002",
        "displayName": "미라",
        "jobId": "JOB_GUARDIAN",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_CAUTIOUS",
        "traitIds": [
          "TRAIT_STURDY"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000003",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1009",
        "appearanceSeed": "2003",
        "growthSeed": "3003",
        "displayName": "아린",
        "jobId": "JOB_ARCHER",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_BRAVE",
        "traitIds": [
          "TRAIT_KEEN_EYE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000004",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1004",
        "appearanceSeed": "2004",
        "growthSeed": "3004",
        "displayName": "세라",
        "jobId": "JOB_CLERIC",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_FRUGAL",
        "traitIds": [
          "TRAIT_ARCANE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      }
    ],
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

| Vector | revision | payload JCS bytes | payload SHA-256 | EnvelopeDigestInput SHA-256 |
|---|---|---|---|---|
| P05_NEW_GAME_GOLDEN | 1 | 7195 | 46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b | a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a |
| P04_TO_P05_GOLDEN_AFTER | 2 | 7195 | 441d02adf6d58189ea0b15c8f5ea3919211c5aa0a0c1ca31f706fe0a254c887e | ef10df65febf0f049ebb330cc3adfb2c2f87985d90e3f015c079f204e85cbec1 |
P04 before vector는 v1.0의 `P04_NEW_GAME_GOLDEN` 그대로이며 payload SHA-256=`81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719`, contentVersion `.2`, revision 1이다. 신규 profile은 2026-07-20 IDs/timestamps와 revision 1, 기존 profile migration은 원래 save/profile/NPC IDs와 createdAtUtc를 보존하고 revision 2다.

## 6. 활동·보유 슬롯과 상태 전이

**상태: CONFIRMED**

| Lodge level | activeMercenaryLimit | ownedMercenaryLimit | P05 source |
|---|---|---|---|
| 1 | 4 | 8 | FAC_EFFECT_LODGE_L1 |
| 2 | 8 | 12 | FAC_EFFECT_LODGE_L2 |
| 3 | 12 | 18 | FAC_EFFECT_LODGE_L3 |
| 4 | 16 | 24 | FAC_EFFECT_LODGE_L4 |
| Current | Command | Guard | Mutation | Event | Error |
|---|---|---|---|---|---|
| active=true | SetActive(false) | IDLE_TOWN\|PROMOTION_READY 또는 INJURED이면서 region/target null; promotion not IN_REVIEW | active=false; autonomy 유지 | MercenaryDeactivated | — |
| active=true | SetActive(false) | travel/combat/loot/return/heal/buy/raid/promotion process 또는 region/target nonnull | no mutation | — | MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN |
| active=false | SetActive(true) | IDLE_TOWN; activeCount<limit | active=true | MercenaryActivated | — |
| active=false | SetActive(true) | INJURED\|PROMOTION_READY 또는 promotion review | no mutation | — | MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN |
| active=false | SetActive(true) | activeCount==limit | no mutation | — | MERCENARY_ACTIVE_LIMIT_REACHED |
| any | SetActive(current) | same desired value | idempotent no-op | — | — |
P05에서 활동은 왕국 활동 슬롯 예약 의미이며 지역 assignment가 아니다. 비활동 전환은 전투를 강제 중단시키지 않으므로 town-safe state에서만 허용한다. Lodge downgrade는 v1에서 없고 limit 감소 API도 없다. 16/24 acceptance fixture는 Lodge L4, 24 owned, first 16 active다.

## 7. 생성·수정 가능 필드 계약

**상태: CONFIRMED**

| Field | P05 ownership | Mutation path |
|---|---|---|
| instanceId/seeds/displayName/job/grade/personality/traits | IMMUTABLE | starter migration 또는 future recruitment snapshot only |
| rank/level/exp/contribution/promotion | READ_ONLY | P11 |
| personalGold | READ_ONLY | P06/P08/P11 economy |
| active | MUTABLE | SetMercenaryActive only |
| autonomy | READ_ONLY | P06 |
| potions/equipmentSlots | READ_ONLY | P07 |
| records | READ_ONLY | P06/P14 |
Application internal `CreateMercenaryFromSnapshot`는 migration/test/future P13 adapter만 호출할 수 있고 public UI route가 없다. owned count, all invariants, duplicate instanceId를 검사하며 cap이면 `MERCENARY_OWNED_LIMIT_REACHED`. rename/retire/delete/duplicate/reroll command는 P05에 존재하지 않는다.

## 8. 목록 query·검색·필터·정렬

**상태: CONFIRMED**

| Dimension | Values | Combination |
|---|---|---|
| search | trimmed Unicode case-insensitive displayName substring; 0..20 scalars | AND |
| job | ALL or 5 job IDs | AND; selected values within dimension OR |
| grade | ALL or C/B/A/S/SS | AND/OR |
| rank | ALL or 6 rank IDs | AND/OR |
| autonomy state | ALL or 17 states | AND/OR |
| active | ALL\|ACTIVE\|INACTIVE | AND |
| promotion ready | ANY\|READY_ONLY | promotion.status=READY or autonomy=PROMOTION_READY |
| injury | ANY\|INJURED_ONLY | autonomy=INJURED |
Normalization은 Unicode NFC 후 locale `ko-KR` invariant case fold이며 Save displayName은 수정하지 않는다. filter/sort/search/scroll/selected ID는 session-only이고 strict Settings에 쓰지 않는다. app/scene 재진입 기본값으로 초기화한다.

| SortId | Exact comparator |
|---|---|
| DEFAULT | active desc → job order asc → grade order desc → rank order desc → level desc → displayName Unicode scalar ordinal → instanceId ordinal |
| NAME_ASC\|DESC | normalized displayName ordinal → instanceId; direction applies name only |
| LEVEL_DESC\|ASC | level → rank order → instanceId; requested direction on level |
| GRADE_DESC\|ASC | grade order → rank order → level → instanceId |
| RANK_DESC\|ASC | rank order → level → grade order → instanceId |
Query input=`MercenaryRosterQueryDto(search,jobIds,gradeIds,rankIds,stateIds,activeFilter,promotionFilter,injuryFilter,sortId)`. Output은 totalOwned,totalActive,limits,filteredCount,ordered `MercenaryCardDto[]`다. Empty roster와 filter no-results는 다른 UI state다.

## 9. command·원자성·idempotency

**상태: CONFIRMED**

`SetMercenaryActiveCommand` required fields는 `operationId UUIDv7`, `requestHash Hex64`, `expectedRevision safe int`, `mercenaryInstanceId UUID`, `desiredActive bool`이다. `MercenaryOperationRequestFactory.CreateSetActive(revision,id,desired)`가 ID와 hash를 만들고 handler는 새 ID를 만들지 않는다.

requestHash input exact shape:

```json
{
  "commandType": "SET_MERCENARY_ACTIVE",
  "operationId": "019f7cd2-8800-7002-8000-000000000001",
  "expectedRevision": 2,
  "mercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000001",
  "desiredActive": false
}
```

expected RFC8785 SHA-256=`8b19c3a58c3803be9fa801b09a281cb975e76b2fa900e48df1c6314475d9a37c`.

handler 순서는 hash constant-time 검증→current Save/mercenary lookup→`current.active==desiredActive` replay 판정→expectedRevision→state guard→slot guard→clone mutation→16 invariants→payload/envelope digest→revision+1 atomic Save→event다. replay는 `expectedRevision<=currentRevision`일 때 성공 `replayed=true`이고 Save/event가 0회이며, expectedRevision이 미래면 revision conflict다. desired와 다를 때는 expectedRevision이 정확히 current여야 한다. Save v1 journal에는 roster subtype이 없으므로 journal을 추가하지 않는다. timeout/lock/write transient retry는 동일 immutable command instance, revision conflict 후 reload+새 tap은 새 operationId다. 한 성공 mutation당 Save 1회, 실패 0회다.

## 10. P05 canonical content package

**상태: CONFIRMED**

| Property | Exact value |
|---|---|
| contentVersion | 1.0.0-content.3 |
| packageKind | BASE |
| baseContentVersion | null |
| contractVersion | 2 |
| csvSchemaSetVersion | 3 |
| minimumGameVersion | 1.0.0-p05 |
| generatedAtUtc | 2026-07-20T00:00:00.000Z |
| table count | 62 |
| changed table | localizations.csv only |
| localizations rowCount | 806 |
| localizations CRLF SHA-256 | 60feb5d4c780aaa52fc7808d206d690950883432e2608f95034050c88f5f1e18 |
| manifest RFC8785 bytes | 67270 |
| manifest SHA-256 | 352a947980b09b0dc5d5699a9367bd6e35cfb1296241784d4c52bc80df0a98e1 |
`.1/.2` directories는 immutable 유지하고 `.3`를 추가한다. active compile-time constant=`1.0.0-content.3`; APK에는 `.1/.2/.3` 모두 포함한다. P04 v1.0.1의 exact loader/override/path rules를 유지하고 build guard의 expected directory set만 3개로 교체한다. schema JSON은 set3 2724 bytes/hash `451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41` 그대로다.

### 10.1 P05 localization 추가 전체 rows

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P05_ACTION_ACTIVATE,Set Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLEAR_SEARCH,Clear Search,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLOSE,Close,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_DEACTIVATE,Set Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RESET_FILTERS,Reset Filters,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RETRY,Retry,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTIVE_COUNT,Active {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_CONSUMABLES,Buy Consumables,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_EQUIPMENT,Buy Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_COMBAT,Combat,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_CONTINUE_DECISION,Continue Decision,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,Evaluate Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_FIND_TARGET,Find Target,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_HEAL,Heal,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_IDLE_TOWN,Idle Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_INJURED,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_LOOT,Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PREPARE,Prepare,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_PROCESS,Promotion Process,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_READY,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RAID_READY,Raid Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RETURN_TOWN,Return Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_SELL_LOOT,Sell Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,Travel To Region,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_COMBAT_STATS,Combat stats unlock in P06.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_EQUIPMENT,Equipment changes unlock in P07.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_PROMOTION,Promotion review unlocks in P11.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_RECRUITMENT,Recruitment unlocks in P13.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_ACTIVE_LIMIT,All active mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_LOAD,Could not load mercenary save data.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_NOT_FOUND,Mercenary not found.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_OWNED_LIMIT,All owned mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_REVISION,Another change was saved first. Reloading.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_SAVE,Could not save mercenary changes.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_STATE_FORBIDDEN,Activity cannot be changed in the current state.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACCESSORY,Accessory,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACTION_REASON,Action Reason,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ARMOR,Armor,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CONTRIBUTION,Contribution {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CURRENT_ACTION,Current Action,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_EXP,EXP {exp},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_GRADE,Base Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HELMET,Helmet,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HUNT_COUNT,Hunts {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ITEMS_COLLECTED,Items Collected {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_KILL_COUNT,Kills {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_LEVEL,Level {level}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONALITY,Personality,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONAL_GOLD,Personal Gold {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_POTIONS,Potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RAID_CLEAR,Raid Clears {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RANK,Growth Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_TRAITS,Traits,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_WEAPON,Weapon,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ACTIVE,Activity,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ALL,All,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_INJURY,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_NO_RESULTS,No mercenaries match these filters.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_PROMOTION,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_STATUS,Status,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_HP_LOW,Low HP,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_INVENTORY_FULL,Inventory full,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_LOOT_COMPLETE,Loot complete,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_NONE,None,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PLAYER_RECALL,Player recall,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POLICY,Kingdom policy,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POTION_LOW,Low potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PROMOTION_AVAILABLE,Promotion available,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_SURVIVAL_LOW,Low survival chance,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_TARGET_FOUND,Target found,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ROSTER_COUNT,Owned {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SEARCH_PLACEHOLDER,Search mercenary name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SLOT_SOURCE,Mercenary Lodge Level {level},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_ASC,Ascending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DEFAULT,Default Order,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DESC,Descending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_LEVEL,Level,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_NAME,Name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_ACTIVE,Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_INACTIVE,Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_EQUIPMENT,Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_GROWTH,Growth,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_OVERVIEW,Overview,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_RECORDS,Records,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TITLE,Mercenaries,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_CONTENT,Mercenary Roster,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_EMPTY,No mercenaries are owned.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_ERROR,Mercenary data cannot be displayed.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOADING,Loading mercenaries…,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOCKED,The mercenary roster is still locked.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_OFFLINE,Offline: local roster management remains available.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_ACTIVATE,활동 배치,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLEAR_SEARCH,검색 지우기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLOSE,닫기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_DEACTIVATE,활동 해제,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RESET_FILTERS,필터 초기화,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RETRY,다시 시도,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTIVE_COUNT,활동 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_CONSUMABLES,소모품 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_EQUIPMENT,장비 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_COMBAT,전투,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_CONTINUE_DECISION,계속 여부 판단,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,장비 평가,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_FIND_TARGET,대상 탐색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_HEAL,치료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_IDLE_TOWN,마을 대기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_INJURED,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_LOOT,전리품 회수,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PREPARE,준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_PROCESS,승급 심사,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_READY,승급 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RAID_READY,레이드 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RETURN_TOWN,마을 귀환,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_SELL_LOOT,전리품 판매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,지역 이동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_COMBAT_STATS,전투 능력치는 P06에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_EQUIPMENT,장비 변경은 P07에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_PROMOTION,승급 심사는 P11에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_RECRUITMENT,새 용병 모집은 P13에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_ACTIVE_LIMIT,활동 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_LOAD,용병 저장 데이터를 불러오지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_NOT_FOUND,용병을 찾을 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_OWNED_LIMIT,보유 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_SAVE,용병 변경 사항을 저장하지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_STATE_FORBIDDEN,현재 상태에서는 활동 여부를 바꿀 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACCESSORY,장신구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACTION_REASON,행동 이유,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ARMOR,갑옷,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CONTRIBUTION,기여도 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CURRENT_ACTION,현재 행동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_EXP,경험치 {exp},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_GRADE,기본 등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HELMET,투구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HUNT_COUNT,사냥 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ITEMS_COLLECTED,수집 아이템 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_KILL_COUNT,처치 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_LEVEL,레벨 {level}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONALITY,성격,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONAL_GOLD,개인 골드 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_POTIONS,보유 포션,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RAID_CLEAR,레이드 클리어 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RANK,성장 랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_TRAITS,특성,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_WEAPON,무기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ACTIVE,활동 여부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ALL,전체,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_INJURY,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_NO_RESULTS,필터와 일치하는 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_PROMOTION,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_STATUS,상태,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_HP_LOW,체력 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_INVENTORY_FULL,인벤토리 가득 참,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_LOOT_COMPLETE,전리품 회수 완료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_NONE,없음,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PLAYER_RECALL,플레이어 귀환 명령,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POLICY,왕국 정책,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POTION_LOW,포션 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PROMOTION_AVAILABLE,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_SURVIVAL_LOW,생존 가능성 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_TARGET_FOUND,대상 발견,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ROSTER_COUNT,보유 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SEARCH_PLACEHOLDER,용병 이름 검색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SLOT_SOURCE,용병 숙소 레벨 {level},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_ASC,오름차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DEFAULT,기본 순서,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DESC,내림차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_LEVEL,레벨,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_NAME,이름,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_ACTIVE,활동 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_INACTIVE,대기 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_EQUIPMENT,장비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_GROWTH,성장,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_OVERVIEW,개요,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_RECORDS,기록,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TITLE,용병,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_CONTENT,용병 명부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_EMPTY,보유한 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_ERROR,용병 정보를 표시할 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOADING,용병 정보를 불러오는 중…,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOCKED,용병 명부가 아직 잠겨 있습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_OFFLINE,오프라인: 로컬 용병 관리는 사용할 수 있습니다.,P05_ROSTER,CONFIRMED,TRUE
```
### 10.2 final content_manifest.json

```json
{
  "schemaId": "urn:tycoon:content-manifest:v2",
  "contractVersion": 2,
  "contentVersion": "1.0.0-content.3",
  "csvSchemaSetVersion": 3,
  "packageKind": "BASE",
  "baseContentVersion": null,
  "minimumGameVersion": "1.0.0-p05",
  "channel": "DEV",
  "generatedAtUtc": "2026-07-20T00:00:00.000Z",
  "tables": [
    {
      "file": "asset_register.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f44f551c6929a08cbb781e71cbadc24f37dbcc721725843b3729498e4c6fd149",
      "rowCount": 18,
      "primaryKey": [
        "asset_id"
      ],
      "fields": [
        {
          "name": "asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "creator",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "source_url",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "version",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "acquired_date",
          "domain": "DATE",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "price_krw",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "license",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "commercial_use",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "modification_allowed",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "credit_required",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "used_in",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "notes",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "autonomy_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a2b63279625d436e18011da606471724a00b4700bed6b3489e57bfbc68c4272f",
      "rowCount": 37,
      "primaryKey": [
        "state",
        "rule_no"
      ],
      "fields": [
        {
          "name": "state",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "IDLE_TOWN",
            "PREPARE",
            "TRAVEL_TO_REGION",
            "FIND_TARGET",
            "COMBAT",
            "LOOT",
            "CONTINUE_DECISION",
            "RETURN_TOWN",
            "SELL_LOOT",
            "HEAL",
            "BUY_CONSUMABLES",
            "EVALUATE_EQUIPMENT",
            "BUY_EQUIPMENT",
            "PROMOTION_READY",
            "PROMOTION_PROCESS",
            "INJURED",
            "RAID_READY"
          ]
        },
        {
          "name": "rule_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "priority",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "reason_code",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "POLICY",
            "HP_LOW",
            "POTION_LOW",
            "INVENTORY_FULL",
            "SURVIVAL_LOW",
            "PLAYER_RECALL",
            "TARGET_FOUND",
            "LOOT_COMPLETE",
            "PROMOTION_AVAILABLE"
          ]
        },
        {
          "name": "next_state",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "IDLE_TOWN",
            "PREPARE",
            "TRAVEL_TO_REGION",
            "FIND_TARGET",
            "COMBAT",
            "LOOT",
            "CONTINUE_DECISION",
            "RETURN_TOWN",
            "SELL_LOOT",
            "HEAL",
            "BUY_CONSUMABLES",
            "EVALUATE_EQUIPMENT",
            "BUY_EQUIPMENT",
            "PROMOTION_READY",
            "PROMOTION_PROCESS",
            "INJURED",
            "RAID_READY"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "condition_group_members.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d07c0ab53279eaf11806cd8e90b3a6415ef704f6e197628205dc77e3df74f2ba",
      "rowCount": 38,
      "primaryKey": [
        "condition_group_id",
        "member_no"
      ],
      "fields": [
        {
          "name": "condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "member_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "member_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "CONDITION",
            "GROUP"
          ]
        },
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "child_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "negate",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "condition_id"
          ],
          "targetFile": "conditions.csv",
          "targetFields": [
            "condition_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "child_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "condition_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "e3b888ec7dc13f63b388a5bcbebcfe0f4f953d97136843f19d5138526ab88dff",
      "rowCount": 21,
      "primaryKey": [
        "condition_group_id"
      ],
      "fields": [
        {
          "name": "condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "logic",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALL",
            "ANY"
          ]
        },
        {
          "name": "description_key",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "conditions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a94dac2c8f5dd2c0da3427d5c1b0e978a48532cda8e7729ead442e92cc6a0fbf",
      "rowCount": 34,
      "primaryKey": [
        "condition_id"
      ],
      "fields": [
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALWAYS_TRUE",
            "FACILITY_UPGRADE_COUNT",
            "FACILITY_LEVEL",
            "REGION_UNLOCKED",
            "REGION_PROGRESS_PERCENT",
            "MONSTER_KILL_COUNT",
            "MERCENARY_COUNT_AT_RANK_OR_HIGHER",
            "RAID_CLEAR_COUNT",
            "KINGDOM_STAGE_REACHED",
            "RAID_PART_BROKEN",
            "RAID_PART_EXPOSED",
            "TUTORIAL_STEP_COMPLETED"
          ]
        },
        {
          "name": "subject_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SYSTEM",
            "KINGDOM",
            "FACILITY",
            "REGION",
            "MONSTER",
            "MERCENARY_ROSTER",
            "RAID",
            "KINGDOM_STAGE",
            "RAID_PART",
            "TUTORIAL_STEP"
          ]
        },
        {
          "name": "subject_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "subject_sub_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "operator",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "EQ",
            "NE",
            "GTE",
            "LTE",
            "GT",
            "LT"
          ]
        },
        {
          "name": "value_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BOOLEAN",
            "INTEGER",
            "DECIMAL",
            "STABLE_ID"
          ]
        },
        {
          "name": "expected_value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "content_aliases.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a398fb76d2c1b0698e90bb0e5369d330b013a75f3d568c96a8c4a81e9b24986a",
      "rowCount": 0,
      "primaryKey": [
        "entity_type",
        "from_id",
        "from_content_version"
      ],
      "fields": [
        {
          "name": "entity_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "JOB",
            "SKILL",
            "TRAIT",
            "PERSONALITY",
            "MERCENARY_GRADE",
            "MERCENARY_RANK",
            "MERCENARY_GENERATION_PROFILE",
            "ITEM",
            "POTION",
            "EQUIPMENT_TEMPLATE",
            "EQUIPMENT_QUALITY",
            "REFINE_OPTION",
            "FACILITY",
            "NPC_PROFESSION",
            "NPC_PROFICIENCY",
            "KINGDOM_STAGE",
            "REGION",
            "MONSTER",
            "RAID",
            "RAID_PART",
            "RECIPE",
            "REWARD_GROUP",
            "TUTORIAL_STEP",
            "STATUS_EFFECT",
            "RECRUITMENT_POOL",
            "PITY_GROUP",
            "PITY_RULE",
            "RATE_UP_GROUP",
            "CONDITION_GROUP",
            "PROGRESSION_FLAG",
            "RUNTIME_CONFIG"
          ]
        },
        {
          "name": "from_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "from_content_version",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "alias_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "RENAME",
            "MERGE",
            "TOMBSTONE"
          ]
        },
        {
          "name": "to_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "currencies.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a2214c4c527aa654dab12449f3e528e9e1b44ea1048b8f9851b13d8cdd53120b",
      "rowCount": 4,
      "primaryKey": [
        "currency_id"
      ],
      "fields": [
        {
          "name": "currency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "currency_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "KINGDOM",
            "PREMIUM_FREE",
            "PREMIUM_PAID",
            "TICKET"
          ]
        },
        {
          "name": "authority",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "LOCAL",
            "SERVER"
          ]
        },
        {
          "name": "max_balance",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "enhancement_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "2c9edde3c79aa4588e749f6718f812638b45b7b6f674eddcb937011bc72866ac",
      "rowCount": 10,
      "primaryKey": [
        "target_level"
      ],
      "fields": [
        {
          "name": "target_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "success_chance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stone_item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stone_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personal_gold_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "fail_pity_increment",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "destroy_on_fail",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "downrank_on_fail",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "stone_item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_job_eligibility.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "7c915d0a9c956cbcc2c2390913af9720e9c825387357cbeacfdcf82e135525d8",
      "rowCount": 110,
      "primaryKey": [
        "equipment_template_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "equipment_template_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "equipment_template_id"
          ],
          "targetFile": "equipment_templates.csv",
          "targetFields": [
            "equipment_template_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_qualities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "150f6181c6130397d21ac56722b61656caec92ff01a95100336a7fe6795360f1",
      "rowCount": 5,
      "primaryKey": [
        "quality_id"
      ],
      "fields": [
        {
          "name": "quality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stat_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "innate_affix_chance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ui_token",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_quality_weights.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "c2bfbc11d56bc2318f56cb0c5d6cf876fa1c080778e599525dfa3bb699d0db5c",
      "rowCount": 20,
      "primaryKey": [
        "quality_profile_id",
        "quality_id"
      ],
      "fields": [
        {
          "name": "quality_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "quality_id"
          ],
          "targetFile": "equipment_qualities.csv",
          "targetFields": [
            "quality_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_templates.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5a844437f39c496406db16f6604f6e16de8dec31c5c6230eae40848ec4372227",
      "rowCount": 80,
      "primaryKey": [
        "equipment_template_id"
      ],
      "fields": [
        {
          "name": "equipment_template_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "WEAPON",
            "ARMOR",
            "HELMET",
            "ACCESSORY"
          ]
        },
        {
          "name": "profile",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "base_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "source",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "CRAFT",
            "DROP",
            "BOSS",
            "RAID"
          ]
        },
        {
          "name": "boss_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "boss_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facilities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5290ae6f1db2b1a9341b6b017d1cf18114116d44ac2b6645a3cf6b9dc2a731cc",
      "rowCount": 8,
      "primaryKey": [
        "facility_id"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "operation_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SYSTEM",
            "MANAGED"
          ]
        },
        {
          "name": "required_profession_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "required_profession_id"
          ],
          "targetFile": "npc_professions.csv",
          "targetFields": [
            "profession_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_construction_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5",
      "rowCount": 32,
      "primaryKey": [
        "facility_id",
        "level"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "build_or_upgrade_duration_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cancel_refund_ratio",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "facility_id",
            "level"
          ],
          "targetFile": "facility_levels.csv",
          "targetFields": [
            "facility_id",
            "level"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_levels.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "9f1dc6409d5ebdf6426cb29536f86bc23dc15c2776bee83ed4cef347c7e8b9c0",
      "rowCount": 32,
      "primaryKey": [
        "facility_id",
        "level"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "required_kingdom_stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "build_or_upgrade_kingdom_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "effect_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "required_kingdom_stage_id"
          ],
          "targetFile": "kingdom_stages.csv",
          "targetFields": [
            "stage_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_upgrade_materials.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "510dcd986ef754906b9c77da8ece2b0c35a51ef7d088c7492dd8c6dee5a4f9f4",
      "rowCount": 40,
      "primaryKey": [
        "facility_id",
        "level",
        "item_id"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "facility_id",
            "level"
          ],
          "targetFile": "facility_levels.csv",
          "targetFields": [
            "facility_id",
            "level"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_world_assets.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f",
      "rowCount": 13,
      "primaryKey": [
        "asset_id"
      ],
      "fields": [
        {
          "name": "asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "address",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "asset_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "PREFAB",
            "SPRITE"
          ]
        },
        {
          "name": "width_px",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "height_px",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pivot_x",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pivot_y",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pixels_per_unit",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "state_variant",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BASE",
            "BACKGROUND",
            "PLOT",
            "LOCKED",
            "CONSTRUCTION",
            "STOPPED"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "asset_id"
          ],
          "targetFile": "asset_register.csv",
          "targetFields": [
            "asset_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "items.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a18b362f3c118304e8a9363b7758015056d1572b496b563b697b2db67061aa2f",
      "rowCount": 53,
      "primaryKey": [
        "item_id"
      ],
      "fields": [
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALCHEMY",
            "BOSS",
            "CRAFT",
            "ENHANCE",
            "MAGIC",
            "MONSTER",
            "ORE",
            "PROMOTION",
            "REFINE",
            "RELIC"
          ]
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "source_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "REGION",
            "RAID",
            "DISMANTLE",
            "ELITE_AND_RAID",
            "PROMOTION_CONTENT"
          ]
        },
        {
          "name": "source_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "sell_price",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "rarity",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stack_limit",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "job_skill_unlocks.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "028a3ff48a9d3f13ab6204c5a2404130dc9a6621a5bd22303519f30715a676a7",
      "rowCount": 15,
      "primaryKey": [
        "job_id",
        "skill_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "skill_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "skill_id"
          ],
          "targetFile": "skills.csv",
          "targetFields": [
            "skill_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "jobs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "72ad4296c33ddf1d6fda43dcd51f9fbdcccdbdc7f4e57f931cbcbc829d5275f8",
      "rowCount": 5,
      "primaryKey": [
        "job_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "role",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BRUISER",
            "TANK",
            "RANGED_DPS",
            "AOE_DPS",
            "HEALER"
          ]
        },
        {
          "name": "armor_profile",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HEAVY",
            "LIGHT",
            "CLOTH"
          ]
        },
        {
          "name": "weapon_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "primary_stat",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "secondary_stat",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ai_priority",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "notes_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "notes_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "kingdom_stages.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "87eaf7cd0dec0ec615aeabb3d7b8c7ef815a4948989bc1b7c62188a5ace5cdfe",
      "rowCount": 5,
      "primaryKey": [
        "stage_id"
      ],
      "fields": [
        {
          "name": "stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "active_slots",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "roster_slots",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_level_cap",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "final_raid_unlocked",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "localizations.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "60feb5d4c780aaa52fc7808d206d690950883432e2608f95034050c88f5f1e18",
      "rowCount": 806,
      "primaryKey": [
        "locale",
        "text_key"
      ],
      "fields": [
        {
          "name": "locale",
          "domain": "LOCALE",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "text_value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "context",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "loot_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "6437c651ce648d298f3ee59a8074ded1b4bb3a6023cd611832d759da409451fb",
      "rowCount": 88,
      "primaryKey": [
        "loot_table_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "CURRENCY",
            "PERSONAL_GOLD",
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER",
            "PLAYER_EXP",
            "KINGDOM_EXP"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "probability",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "min_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "loot_table_id"
          ],
          "targetFile": "loot_tables.csv",
          "targetFields": [
            "loot_table_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "condition_id"
          ],
          "targetFile": "conditions.csv",
          "targetFields": [
            "condition_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "loot_tables.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "2359b43dfd323fbf263062fee98910bee9d2f3fcbfe29b7ae3ffcd922c49a9cd",
      "rowCount": 27,
      "primaryKey": [
        "loot_table_id"
      ],
      "fields": [
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "draw_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "INDEPENDENT",
            "ONE_WEIGHTED",
            "N_WEIGHTED"
          ]
        },
        {
          "name": "draw_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "mercenary_appearance_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "74a27c611876c1e969563cc3b4fbb4a4537aa67a0982ba63afa6e8691992ebb7",
      "rowCount": 5,
      "primaryKey": [
        "appearance_pool_id",
        "job_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "appearance_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "appearance_asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "appearance_asset_id"
          ],
          "targetFile": "asset_register.csv",
          "targetFields": [
            "asset_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_generation_profiles.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "7702394c9a2c24a3e8bdabd83573f485966e3f0ebbb45912e3f7866b51f55533",
      "rowCount": 1,
      "primaryKey": [
        "generation_profile_id"
      ],
      "fields": [
        {
          "name": "generation_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "algorithm_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GENERATED_MERCENARY_V1"
          ]
        },
        {
          "name": "initial_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "appearance_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personality_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "WEIGHTED_ALL",
            "FIXED"
          ]
        },
        {
          "name": "fixed_personality_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "growth_seed_min",
          "domain": "SEED64",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "growth_seed_max",
          "domain": "SEED64",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "initial_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "fixed_personality_id"
          ],
          "targetFile": "personalities.csv",
          "targetFields": [
            "personality_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_grades.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f4aaff18498325c99836d945be898632e1f7903282e82cff8e21fadb682f6079",
      "rowCount": 5,
      "primaryKey": [
        "grade_id"
      ],
      "fields": [
        {
          "name": "grade_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GRADE_C",
            "GRADE_B",
            "GRADE_A",
            "GRADE_S",
            "GRADE_SS"
          ]
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "base_stat_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "growth_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "initial_trait_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "promotion_cost_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tavern_eligible",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "special_pool_eligible",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ui_color",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_name_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "b3077cf682d4e5aeb0c0ed9d8ce1326640d4a1a0e2c2deb4088ca4f4f18724d9",
      "rowCount": 20,
      "primaryKey": [
        "name_pool_id",
        "locale",
        "entry_no"
      ],
      "fields": [
        {
          "name": "name_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "locale",
          "domain": "LOCALE",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "display_name",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "mercenary_ranks.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "3fbae56a6acab8fa6c2cc44a7e0cafac5b080704470956140e81aea0aeebbd4d",
      "rowCount": 6,
      "primaryKey": [
        "rank_id"
      ],
      "fields": [
        {
          "name": "rank_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "RANK_APPRENTICE",
            "RANK_REGULAR",
            "RANK_SKILLED",
            "RANK_ELITE",
            "RANK_HERO",
            "RANK_LEGEND"
          ]
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_region_tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "skill_slot_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "trait_slot_bonus",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_access",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "promotion_to",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "promotion_token_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "base_personal_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "contribution_required",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "additional_condition",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "promotion_to"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "promotion_token_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "monsters.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "92ae3f14fa06328bdedf3682c332aa9e390fe6f8816349d7f9c3aa3012fa0cb8",
      "rowCount": 27,
      "primaryKey": [
        "monster_id"
      ],
      "fields": [
        {
          "name": "monster_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "region_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NORMAL",
            "ELITE",
            "BOSS",
            "RAID"
          ]
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "hp",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "attack",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "defense",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "xp",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "bounty_personal_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "behavior_tag",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "spawn_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "region_id"
          ],
          "targetFile": "regions.csv",
          "targetFields": [
            "region_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "loot_table_id"
          ],
          "targetFile": "loot_tables.csv",
          "targetFields": [
            "loot_table_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "npc_professions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bef0f486b935b740e32d5660915226c9e08515bcb8716d5b187c131f4c870722",
      "rowCount": 4,
      "primaryKey": [
        "profession_id"
      ],
      "fields": [
        {
          "name": "profession_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "work_unit",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "primary_effect",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "npc_proficiency_levels.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "125ac69e78cd8ea50d04142e49b3aa178ef2a486d72e191bd0a1bae469a4757a",
      "rowCount": 4,
      "primaryKey": [
        "proficiency_id"
      ],
      "fields": [
        {
          "name": "proficiency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "xp_required",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "speed_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_bonus",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "offline_reward_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "36fc0e26c34f03d61c081c1fc1f981503e98997166a26705df157993cb6a54e6",
      "rowCount": 6,
      "primaryKey": [
        "rule_id"
      ],
      "fields": [
        {
          "name": "rule_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "settlement_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HUNT",
            "FACILITY",
            "NPC_PROFICIENCY",
            "POTION_CONSUMPTION",
            "INJURY_RECOVERY",
            "PROMOTION_REVIEW"
          ]
        },
        {
          "name": "max_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "personalities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "10114e5c65713cdcf7783c1b8d93f61a0f75f244586f16adcfd0d9c02bf41d4e",
      "rowCount": 6,
      "primaryKey": [
        "personality_id"
      ],
      "fields": [
        {
          "name": "personality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "buy_threshold_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "risk_tolerance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "return_hp_threshold",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "preferred_behavior",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "potions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "22e9dd956e3149e2976dfc6de11635785d9046df5bf3ddd10a50b975b231377b",
      "rowCount": 7,
      "primaryKey": [
        "potion_id"
      ],
      "fields": [
        {
          "name": "potion_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HEAL_FLAT",
            "CURE_POISON",
            "POISON_RESIST",
            "FROST_RESIST",
            "BOSS_DAMAGE"
          ]
        },
        {
          "name": "effect_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "duration_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "auto_use_condition",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "progression_flags.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "06bfcd7f830d060d34a5a984392b7bf78fd9d4ea2093670ca00572b5c842fcc0",
      "rowCount": 3,
      "primaryKey": [
        "progression_flag_id"
      ],
      "fields": [
        {
          "name": "progression_flag_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "scope",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "PROFILE",
            "KINGDOM"
          ]
        },
        {
          "name": "repeatable",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "promotion_grade_requirements.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "4669a10f62cc2b2ab43509a3f8f68bf6757e774281e39b36b2ce390a03a84e9c",
      "rowCount": 6,
      "primaryKey": [
        "grade_id",
        "from_rank_id",
        "to_rank_id",
        "item_id"
      ],
      "fields": [
        {
          "name": "grade_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "from_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "to_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "from_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "to_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raid_difficulties.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "cc6fc17186facc92c8a047ae2929bfee7fe0b1459bdb7fe9611d08cd6489506e",
      "rowCount": 6,
      "primaryKey": [
        "raid_id",
        "difficulty"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "difficulty",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NORMAL",
            "HARD",
            "CORRUPTED"
          ]
        },
        {
          "name": "recommended_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raid_part_effects.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "05e69d150777edbf6abf0c09d034ba2b251d5f7e1c6537120e452b31a8ef8ee4",
      "rowCount": 6,
      "primaryKey": [
        "effect_id"
      ],
      "fields": [
        {
          "name": "effect_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ABILITY_SCALE",
            "STAT_MODIFIER",
            "STATE_TRANSITION"
          ]
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "raid_parts.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "009baa859fc58ab25650c89ffbc4a9ba36f95e97bfb30ad793679ea13e382942",
      "rowCount": 7,
      "primaryKey": [
        "raid_id",
        "part_id"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "part_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_hp_ratio",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "break_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "break_reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "behavior_change",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "priority_hint_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "priority_hint_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "break_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "break_reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "behavior_change"
          ],
          "targetFile": "raid_part_effects.csv",
          "targetFields": [
            "effect_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raids.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "82b1fc2fa049daeb6942ff536ec52507b2efea02389dd38ceb20ca008adb180a",
      "rowCount": 2,
      "primaryKey": [
        "raid_id"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "boss_monster_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "party_min",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "party_max",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "first_clear_reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "time_limit_sec",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "boss_monster_id"
          ],
          "targetFile": "monsters.csv",
          "targetFields": [
            "monster_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "min_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "first_clear_reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "random_equipment_tier_specs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "58307716e9f2f1a217fc46075e4578fc36e5e9586fd62525badee6c6819a1c86",
      "rowCount": 5,
      "primaryKey": [
        "spec_id"
      ],
      "fields": [
        {
          "name": "spec_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ANY",
            "FIXED"
          ]
        },
        {
          "name": "fixed_slot",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "job_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ELIGIBLE_ANY",
            "KILLER_JOB",
            "FIXED"
          ]
        },
        {
          "name": "fixed_job_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "quality_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "fixed_job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipe_materials.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "1a27d1d79a6040698eb2950f0f199a22e03db74d189c830f8f0a8cf13c18f9a3",
      "rowCount": 183,
      "primaryKey": [
        "recipe_id",
        "material_no"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "recipe_id"
          ],
          "targetFile": "recipes.csv",
          "targetFields": [
            "recipe_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipe_outputs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f07d94b866f4be4bcc035261c5a912f583ad544989b7ffe2078f212fea0b4a6b",
      "rowCount": 87,
      "primaryKey": [
        "recipe_id",
        "output_no"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "output_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "EQUIPMENT_TEMPLATE"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "recipe_id"
          ],
          "targetFile": "recipes.csv",
          "targetFields": [
            "recipe_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipes.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "9a99e41c90612f5ae32d5218e40b39ed4a037fb9c3c209ec7521b557c8a8481c",
      "rowCount": 87,
      "primaryKey": [
        "recipe_id"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "npc_proficiency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "craft_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_roll",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "npc_proficiency_id"
          ],
          "targetFile": "npc_proficiency_levels.csv",
          "targetFields": [
            "proficiency_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pity_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "ad5410752817ce425b02dc6aee4a167b78d5d6db51f32422a01c0615c917ed73",
      "rowCount": 1,
      "primaryKey": [
        "pity_group_id"
      ],
      "fields": [
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SPECIAL",
            "SPECIAL_RATEUP"
          ]
        },
        {
          "name": "carry_over",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "recruitment_pity_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "40662eb78418ef2d32cc6bbea3b3d7d6e6416018fd3b7fa464b38e044957f849",
      "rowCount": 2,
      "primaryKey": [
        "pity_group_id",
        "pity_rule_id"
      ],
      "fields": [
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pity_rule_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "trigger_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "guaranteed_grade_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "reset_on_grade_or_higher_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "guarantee_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GRADE_AT_LEAST",
            "FEATURED_JOB"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pity_group_id"
          ],
          "targetFile": "recruitment_pity_groups.csv",
          "targetFields": [
            "pity_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "guaranteed_grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "reset_on_grade_or_higher_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "37a9fc6166b861bffe1e7077a51a0da6a5698176c1d05538c26b3751ad19f1c3",
      "rowCount": 17,
      "primaryKey": [
        "pool_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "result_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GENERATED_MERCENARY",
            "ITEM",
            "EQUIPMENT_TEMPLATE"
          ]
        },
        {
          "name": "result_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "grade_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "job_selection_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "ALL",
            "FIXED",
            "RATE_UP_GROUP"
          ]
        },
        {
          "name": "job_selection_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pool_id"
          ],
          "targetFile": "recruitment_pools.csv",
          "targetFields": [
            "pool_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pools.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "35545b565d7092ff0337d9968fc3b4697b66e98a6d66d93c614c2879ab0368fc",
      "rowCount": 6,
      "primaryKey": [
        "pool_id"
      ],
      "fields": [
        {
          "name": "pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pool_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "TAVERN",
            "SPECIAL",
            "SPECIAL_RATEUP"
          ]
        },
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "cost_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "KINGDOM_GOLD",
            "TICKET",
            "FREE_PREMIUM",
            "PAID_PREMIUM"
          ]
        },
        {
          "name": "cost_amount",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "starts_at_utc",
          "domain": "UTC_INSTANT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "ends_at_utc",
          "domain": "UTC_INSTANT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pity_group_id"
          ],
          "targetFile": "recruitment_pity_groups.csv",
          "targetFields": [
            "pity_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "rate_up_group_id"
          ],
          "targetFile": "recruitment_rate_up_groups.csv",
          "targetFields": [
            "rate_up_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_rate_up_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bb922b63227eeaaa922b3e98a6ca457c65588890ba745b0f0219c7294a96613d",
      "rowCount": 0,
      "primaryKey": [
        "rate_up_group_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "rate_up_group_id"
          ],
          "targetFile": "recruitment_rate_up_groups.csv",
          "targetFields": [
            "rate_up_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_rate_up_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "13b199e7d6348e05a15413af28c90c063db68aea0489c88c5af9fa8880e08762",
      "rowCount": 0,
      "primaryKey": [
        "rate_up_group_id"
      ],
      "fields": [
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "featured_share",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "failure_guarantee_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "NEXT_S_OR_SS_FEATURED"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "refine_options.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "b86ee167473b89b98078429b4fe832650526852b830fc218b9ac377c80ae9866",
      "rowCount": 11,
      "primaryKey": [
        "refine_option_id"
      ],
      "fields": [
        {
          "name": "refine_option_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "option_group",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stat_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personal_gold_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "material_item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "regions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "80d6c173d7e2379e8b5f914d2d1b6f4551d07dbd03e032e4d15010028824d336",
      "rowCount": 5,
      "primaryKey": [
        "region_id"
      ],
      "fields": [
        {
          "name": "region_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "kingdom_stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "recommended_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_active",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "environment_tag",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_gate_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "offline_efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "min_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "kingdom_stage_id"
          ],
          "targetFile": "kingdom_stages.csv",
          "targetFields": [
            "stage_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_gate_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "reward_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "8eadd36ca99cb817a95b2f526cbf6c4601253d769c21a086d818829e564d3863",
      "rowCount": 16,
      "primaryKey": [
        "reward_group_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "CURRENCY",
            "PERSONAL_GOLD",
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER",
            "PLAYER_EXP",
            "KINGDOM_EXP",
            "REGION_UNLOCK",
            "PROGRESSION_FLAG"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "probability",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "reward_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d26f5793228068d6be1bcaae3944761cc1348a03d16b40e8f502820c01d9d7da",
      "rowCount": 15,
      "primaryKey": [
        "reward_group_id"
      ],
      "fields": [
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "distribution_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALL",
            "ONE_PROBABILITY",
            "ONE_WEIGHTED",
            "N_WEIGHTED"
          ]
        },
        {
          "name": "draw_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "runtime_config.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "610576da67809118f8dd87f4526be69682c5657de0852baa3fd8b3efc28efd25",
      "rowCount": 18,
      "primaryKey": [
        "config_key"
      ],
      "fields": [
        {
          "name": "config_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "value_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "INTEGER",
            "DECIMAL",
            "BOOLEAN",
            "STRING"
          ]
        },
        {
          "name": "value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unit",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "max_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "skills.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "4bae7415a237c7ed40a2d8453139eafc8c651f3cd302236173aba70b1b084ef7",
      "rowCount": 15,
      "primaryKey": [
        "skill_id"
      ],
      "fields": [
        {
          "name": "skill_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "kind",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ACTIVE",
            "PASSIVE"
          ]
        },
        {
          "name": "unlock_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "target",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cooldown_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "power_coeff",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "resource_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "status_effects.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "43514690856fce638242b754f6fe1dc2cd98318dc11c5ba71d157f1da4e0b118",
      "rowCount": 6,
      "primaryKey": [
        "status_effect_id"
      ],
      "fields": [
        {
          "name": "status_effect_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BUFF",
            "DEBUFF",
            "CONTROL",
            "INJURY"
          ]
        },
        {
          "name": "stack_rule",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "duration_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "boss_resistance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "trait_job_eligibility.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f2964966cbd9258b85a9ed541ba22ae0d7d51e0146206a4c85575e6b6d5e59e4",
      "rowCount": 44,
      "primaryKey": [
        "trait_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "trait_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "trait_id"
          ],
          "targetFile": "traits.csv",
          "targetFields": [
            "trait_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "traits.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bf211a89b515e166c5bddab3294688ae9fca28ba33877ab7a8d3a8a219af9f86",
      "rowCount": 10,
      "primaryKey": [
        "trait_id"
      ],
      "fields": [
        {
          "name": "trait_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "STAT",
            "COMBAT",
            "COLLECTION",
            "BOSS",
            "RESIST",
            "AI"
          ]
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "tutorial_grants.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5782d9c3da2fdb7d8693c7bdf7f871d74a47d5d53357b6191000909826c4a450",
      "rowCount": 9,
      "primaryKey": [
        "grant_id",
        "line_no"
      ],
      "fields": [
        {
          "name": "grant_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tutorial_step_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "line_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "CURRENCY",
            "PROGRESSION_FLAG"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "tutorial_step_id"
          ],
          "targetFile": "tutorial_steps.csv",
          "targetFields": [
            "tutorial_step_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "tutorial_steps.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "67ad17e3832c69d303e02db1803e36e401f46b2dedff4f34420cc49e531d3e8b",
      "rowCount": 10,
      "primaryKey": [
        "tutorial_step_id"
      ],
      "fields": [
        {
          "name": "tutorial_step_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "action_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "target_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "prerequisite_step_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "skippable",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "prerequisite_step_id"
          ],
          "targetFile": "tutorial_steps.csv",
          "targetFields": [
            "tutorial_step_id"
          ],
          "mode": "HARD"
        }
      ]
    }
  ]
}
```

generator는 `.2` 62-table bytes를 carry-forward하고 localizations에 위 rows를 `(locale,text_key)` UTF-8 ordinal merge한다. CSV UTF-8 no BOM/RFC4180/CRLF/final CRLF. existing `.3`가 golden과 같으면 no-op, 다르면 `CONTENT_RELEASE_SPLIT_BRAIN`; in-place replace 금지다.

## 11. portrait asset·Addressables

**상태: CONFIRMED**

| jobId | assetId/address | P05 use |
|---|---|---|
| JOB_WARRIOR | ASSET_MERC_PLACEHOLDER_WARRIOR_V1 | card/detail portrait |
| JOB_GUARDIAN | ASSET_MERC_PLACEHOLDER_GUARDIAN_V1 | card/detail portrait |
| JOB_ARCHER | ASSET_MERC_PLACEHOLDER_ARCHER_V1 | card/detail portrait |
| JOB_MAGE | ASSET_MERC_PLACEHOLDER_MAGE_V1 | card/detail portrait |
| JOB_CLERIC | ASSET_MERC_PLACEHOLDER_CLERIC_V1 | card/detail portrait |
P03 Addressables group `Content-Mercenary-Placeholders-v1`와 existing asset IDs를 재사용해 asset_register/CSV bytes를 바꾸지 않는다. `appearanceSeed`는 appearance pool 후보를 ordered weighted resolve하며 현재 job별 후보 1개다. card는 prefab의 role Sprite를 uGUI Image에 복제하지 않고 Addressables prefab에서 `MercenaryPlaceholderMarker`가 제공하는 Sprite reference를 adapter가 읽는다. missing은 `P05_PORTRAIT_ASSET_MISSING`; production 교체는 address/assetId/job marker 유지다.

## 12. Roster 화면·Drawer hierarchy와 좌표

**상태: CONFIRMED**

P05는 새 scene을 만들지 않고 영속 `ScreenCanvas/MercenaryRosterScreen`을 사용한다. Kingdom 하단 `NAV_MERCENARIES`가 화면을 열며 Kingdom WorldCanvas는 비활성, AppRoot/HUD/Screen/Drawer/Modal/Toast는 유지한다.

| ElementId | ParentId | AnchorMin | AnchorMax | OffsetMin | OffsetMax | MinTouch | VisibleWhen | InteractableWhen | LocalizationKey |
|---|---|---|---|---|---|---|---|---|---|
| ROSTER_ROOT | SCREEN_CANVAS | 0,0 | 1,1 | 0,112 | 0,-112 | CONTENT\|EMPTY\|OFFLINE | FALSE |  |
| ROSTER_HEADER | ROSTER_ROOT | 0,1 | 1,1 | 24,-88 | -24,-16 | 64×64 | ALWAYS | FALSE | TXT_P05_TITLE |
| COUNT_OWNED | ROSTER_HEADER | 0,0 | 0.25,1 | 0,0 | 0,0 | 0 | ALWAYS | FALSE | TXT_P05_ROSTER_COUNT |
| COUNT_ACTIVE | ROSTER_HEADER | 0.25,0 | 0.5,1 | 0,0 | 0,0 | 0 | ALWAYS | FALSE | TXT_P05_ACTIVE_COUNT |
| SEARCH | ROSTER_ROOT | 0,1 | 0.35,1 | 24,-176 | -12,-104 | 64×64 | CONTENT | TRUE | TXT_P05_SEARCH_PLACEHOLDER |
| FILTER_BUTTON | ROSTER_ROOT | 0.35,1 | 0.5,1 | 12,-176 | -12,-104 | 64×64 | CONTENT | TRUE | TXT_P05_FILTER_ALL |
| SORT_BUTTON | ROSTER_ROOT | 0.5,1 | 0.65,1 | 12,-176 | -12,-104 | 64×64 | CONTENT | TRUE | TXT_P05_SORT_DEFAULT |
| FILTER_CHIPS | ROSTER_ROOT | 0.65,1 | 1,1 | 12,-176 | -24,-104 | 64×64 | HAS_FILTERS | TRUE |  |
| ROSTER_SCROLL | ROSTER_ROOT | 0,0 | 1,1 | 24,24 | -24,-192 | 64×64 | CONTENT | TRUE |  |
| NO_RESULTS | ROSTER_SCROLL | 0,0 | 1,1 | 0,0 | 0,0 | 64×64 | FILTERED_COUNT_ZERO | FALSE | TXT_P05_FILTER_NO_RESULTS |
| DETAIL_DRAWER | DRAWER_CANVAS | 1,0 | 1,1 | -720,0 | 0,0 | 0 | SELECTION | FALSE |  |
| DETAIL_CLOSE | DETAIL_DRAWER | 1,1 | 1,1 | -88,-88 | -16,-16 | 64×64 | SELECTION | TRUE | TXT_P05_ACTION_CLOSE |
| DETAIL_PORTRAIT | DETAIL_DRAWER | 0,1 | 0,1 | 24,-240 | 216,-48 | 64×64 | SELECTION | FALSE |  |
| DETAIL_IDENTITY | DETAIL_DRAWER | 0,1 | 1,1 | 232,-240 | -96,-48 | 0 | SELECTION | FALSE |  |
| TAB_OVERVIEW | DETAIL_DRAWER | 0,1 | 0.25,1 | 16,-320 | -4,-256 | 64×64 | SELECTION | TRUE | TXT_P05_TAB_OVERVIEW |
| TAB_EQUIPMENT | DETAIL_DRAWER | 0.25,1 | 0.5,1 | 4,-320 | -4,-256 | 64×64 | SELECTION | TRUE | TXT_P05_TAB_EQUIPMENT |
| TAB_GROWTH | DETAIL_DRAWER | 0.5,1 | 0.75,1 | 4,-320 | -4,-256 | 64×64 | SELECTION | TRUE | TXT_P05_TAB_GROWTH |
| TAB_RECORDS | DETAIL_DRAWER | 0.75,1 | 1,1 | 4,-320 | -16,-256 | 64×64 | SELECTION | TRUE | TXT_P05_TAB_RECORDS |
| DETAIL_SCROLL | DETAIL_DRAWER | 0,0 | 1,1 | 24,120 | -24,-336 | 0 | SELECTION | TRUE |  |
| ACTIVE_TOGGLE | DETAIL_DRAWER | 0,0 | 1,0 | 24,24 | -24,104 | 64×64 | SELECTION | guard && !BUSY |  |
CanvasScaler/Safe Area는 P04 uGUI 계약을 유지한다. Drawer width=720, animation=0.22s CUBIC_OUT. 선택/reselection/scrim/Android back은 P04와 동일하고 modal stack=1이다. Filter/Sort는 단일 modal, active toggle은 confirmation 없이 Drawer에서 즉시 수행하되 busy 동안 disabled다. 130% text scale에서 card height는 content-driven max 280, Drawer scroll을 사용하고 ellipsis 금지다.

virtualized grid는 drawer closed 4 columns, open 3 columns; gap 24, viewport padding 0. card width=`floor((viewportWidth-gap*(columns-1))/columns)`, height=220(default)/280(large text). pool은 visible rows+1 buffer, 최대 16 card GameObject다. 24 owned여도 instantiate<=16; selection은 instanceId로 유지하고 filtered-out이면 Drawer를 닫는다.

## 13. card·detail projection

**상태: CONFIRMED**

| Surface | Exact fields | Action |
|---|---|---|
| Card | portrait,name,job,grade color,rank,level,active badge,autonomy state,promotion/injury badge | select only |
| Overview | identity,level/max,raw exp,personality,traits,personalGold,contribution,current action+reason,region | active toggle |
| Equipment | 4 slot names/quality or Empty; potion stacks | read-only; defer P07 text |
| Growth | rank/max level,grade,raw exp,promotion status/target,contribution | read-only; defer P11 text |
| Records | huntCount,killCount,raidClearCount,itemsCollected | read-only |
combatPower/base stats/next-level exp는 authoritative curve가 canonical content에 없으므로 P05 DTO에 넣지 않는다. 0이나 추정값을 표시하지 않고 `TXT_P05_DEFER_COMBAT_STATS`를 표시한다. 항상 autonomy state와 reason을 text+icon으로 함께 노출한다.

## 14. 6개 UI 상태

**상태: CONFIRMED**

| UiState | EntryCondition | Content | Primary | Secondary | Key | Analytics |
|---|---|---|---|---|---|---|
| LOADING | catalog/Save/query pending | skeleton cards | NONE | NONE | TXT_P05_UI_LOADING | P05_ROSTER_LOADING |
| CONTENT | valid roster count>0 | grid+optional Drawer | card select/active toggle | back | TXT_P05_UI_CONTENT | P05_ROSTER_CONTENT |
| EMPTY | valid roster count=0 after non-starter dev Save | empty panel + P13 안내 | NONE | back | TXT_P05_UI_EMPTY | P05_ROSTER_EMPTY |
| ERROR | catalog/Save/invariant failure | error panel; no cards | RETRY LOAD | back | TXT_P05_UI_ERROR | P05_ROSTER_ERROR |
| LOCKED | roster nav progression lock in non-P05 fixture | locked panel | back | NONE | TXT_P05_UI_LOCKED | P05_ROSTER_LOCKED |
| OFFLINE | network unavailable, local valid | CONTENT+offline badge | local active toggle allowed | retry network | TXT_P05_UI_OFFLINE | P05_ROSTER_OFFLINE |
filter 결과 0은 CONTENT의 `NO_RESULTS` substate이며 EMPTY가 아니다. Error에서 Save 자동 수정 금지. Offline은 server-independent이며 HTTP 호출 0회. DebugCanvas는 revision/contentVersion/counts/invariant error만 development build에 표시한다.

## 15. localization exact bundle

**상태: CONFIRMED**

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P05_ACTION_ACTIVATE,Set Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLEAR_SEARCH,Clear Search,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLOSE,Close,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_DEACTIVATE,Set Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RESET_FILTERS,Reset Filters,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RETRY,Retry,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTIVE_COUNT,Active {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_CONSUMABLES,Buy Consumables,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_EQUIPMENT,Buy Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_COMBAT,Combat,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_CONTINUE_DECISION,Continue Decision,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,Evaluate Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_FIND_TARGET,Find Target,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_HEAL,Heal,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_IDLE_TOWN,Idle Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_INJURED,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_LOOT,Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PREPARE,Prepare,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_PROCESS,Promotion Process,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_READY,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RAID_READY,Raid Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RETURN_TOWN,Return Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_SELL_LOOT,Sell Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,Travel To Region,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_COMBAT_STATS,Combat stats unlock in P06.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_EQUIPMENT,Equipment changes unlock in P07.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_PROMOTION,Promotion review unlocks in P11.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_RECRUITMENT,Recruitment unlocks in P13.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_ACTIVE_LIMIT,All active mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_LOAD,Could not load mercenary save data.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_NOT_FOUND,Mercenary not found.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_OWNED_LIMIT,All owned mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_REVISION,Another change was saved first. Reloading.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_SAVE,Could not save mercenary changes.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_STATE_FORBIDDEN,Activity cannot be changed in the current state.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACCESSORY,Accessory,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACTION_REASON,Action Reason,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ARMOR,Armor,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CONTRIBUTION,Contribution {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CURRENT_ACTION,Current Action,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_EXP,EXP {exp},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_GRADE,Base Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HELMET,Helmet,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HUNT_COUNT,Hunts {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ITEMS_COLLECTED,Items Collected {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_KILL_COUNT,Kills {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_LEVEL,Level {level}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONALITY,Personality,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONAL_GOLD,Personal Gold {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_POTIONS,Potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RAID_CLEAR,Raid Clears {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RANK,Growth Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_TRAITS,Traits,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_WEAPON,Weapon,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ACTIVE,Activity,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ALL,All,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_INJURY,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_NO_RESULTS,No mercenaries match these filters.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_PROMOTION,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_STATUS,Status,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_HP_LOW,Low HP,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_INVENTORY_FULL,Inventory full,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_LOOT_COMPLETE,Loot complete,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_NONE,None,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PLAYER_RECALL,Player recall,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POLICY,Kingdom policy,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POTION_LOW,Low potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PROMOTION_AVAILABLE,Promotion available,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_SURVIVAL_LOW,Low survival chance,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_TARGET_FOUND,Target found,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ROSTER_COUNT,Owned {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SEARCH_PLACEHOLDER,Search mercenary name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SLOT_SOURCE,Mercenary Lodge Level {level},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_ASC,Ascending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DEFAULT,Default Order,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DESC,Descending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_LEVEL,Level,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_NAME,Name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_ACTIVE,Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_INACTIVE,Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_EQUIPMENT,Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_GROWTH,Growth,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_OVERVIEW,Overview,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_RECORDS,Records,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TITLE,Mercenaries,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_CONTENT,Mercenary Roster,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_EMPTY,No mercenaries are owned.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_ERROR,Mercenary data cannot be displayed.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOADING,Loading mercenaries…,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOCKED,The mercenary roster is still locked.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_OFFLINE,Offline: local roster management remains available.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_ACTIVATE,활동 배치,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLEAR_SEARCH,검색 지우기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLOSE,닫기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_DEACTIVATE,활동 해제,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RESET_FILTERS,필터 초기화,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RETRY,다시 시도,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTIVE_COUNT,활동 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_CONSUMABLES,소모품 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_EQUIPMENT,장비 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_COMBAT,전투,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_CONTINUE_DECISION,계속 여부 판단,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,장비 평가,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_FIND_TARGET,대상 탐색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_HEAL,치료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_IDLE_TOWN,마을 대기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_INJURED,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_LOOT,전리품 회수,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PREPARE,준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_PROCESS,승급 심사,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_READY,승급 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RAID_READY,레이드 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RETURN_TOWN,마을 귀환,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_SELL_LOOT,전리품 판매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,지역 이동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_COMBAT_STATS,전투 능력치는 P06에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_EQUIPMENT,장비 변경은 P07에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_PROMOTION,승급 심사는 P11에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_RECRUITMENT,새 용병 모집은 P13에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_ACTIVE_LIMIT,활동 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_LOAD,용병 저장 데이터를 불러오지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_NOT_FOUND,용병을 찾을 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_OWNED_LIMIT,보유 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_SAVE,용병 변경 사항을 저장하지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_STATE_FORBIDDEN,현재 상태에서는 활동 여부를 바꿀 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACCESSORY,장신구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACTION_REASON,행동 이유,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ARMOR,갑옷,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CONTRIBUTION,기여도 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CURRENT_ACTION,현재 행동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_EXP,경험치 {exp},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_GRADE,기본 등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HELMET,투구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HUNT_COUNT,사냥 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ITEMS_COLLECTED,수집 아이템 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_KILL_COUNT,처치 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_LEVEL,레벨 {level}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONALITY,성격,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONAL_GOLD,개인 골드 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_POTIONS,보유 포션,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RAID_CLEAR,레이드 클리어 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RANK,성장 랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_TRAITS,특성,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_WEAPON,무기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ACTIVE,활동 여부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ALL,전체,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_INJURY,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_NO_RESULTS,필터와 일치하는 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_PROMOTION,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_STATUS,상태,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_HP_LOW,체력 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_INVENTORY_FULL,인벤토리 가득 참,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_LOOT_COMPLETE,전리품 회수 완료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_NONE,없음,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PLAYER_RECALL,플레이어 귀환 명령,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POLICY,왕국 정책,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POTION_LOW,포션 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PROMOTION_AVAILABLE,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_SURVIVAL_LOW,생존 가능성 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_TARGET_FOUND,대상 발견,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ROSTER_COUNT,보유 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SEARCH_PLACEHOLDER,용병 이름 검색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SLOT_SOURCE,용병 숙소 레벨 {level},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_ASC,오름차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DEFAULT,기본 순서,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DESC,내림차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_LEVEL,레벨,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_NAME,이름,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_ACTIVE,활동 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_INACTIVE,대기 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_EQUIPMENT,장비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_GROWTH,성장,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_OVERVIEW,개요,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_RECORDS,기록,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TITLE,용병,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_CONTENT,용병 명부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_EMPTY,보유한 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_ERROR,용병 정보를 표시할 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOADING,용병 정보를 불러오는 중…,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOCKED,용병 명부가 아직 잠겨 있습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_OFFLINE,오프라인: 로컬 용병 관리는 사용할 수 있습니다.,P05_ROSTER,CONFIRMED,TRUE
```
placeholder는 P04 named token 문법을 유지한다. 허용 token: current,max,level,exp,amount. ko/en plural 문장을 사용하지 않는다. key별 token set exact mismatch는 `LOCALIZATION_PLACEHOLDER_MISMATCH`. existing job/grade/rank/personality/trait/potion/equipment 이름 key는 P03 rows를 재사용한다.

## 16. Domain/Application/Infrastructure/Presentation type 계약

**상태: CONFIRMED**

| TypeName | Namespace | Layer | Responsibility | Dependencies | PublicMembers |
|---|---|---|---|---|---|
| Mercenary | KingdomTycoon.Domain.Mercenaries | Domain | strict entity/value accessors | none | Validate/SetActive |
| MercenaryInvariantValidator | KingdomTycoon.Domain.Mercenaries | Domain | 16 invariants | catalog | Validate(snapshot) |
| MercenaryActivityPolicy | KingdomTycoon.Domain.Mercenaries | Domain | town-safe transition | none | Evaluate(entity,desired,limits) |
| MercenaryRosterFilter | KingdomTycoon.Domain.Mercenaries | Domain | pure AND/OR filters | none | Matches |
| MercenaryRosterComparer | KingdomTycoon.Domain.Mercenaries | Domain | stable comparators | catalog orders | Compare |
| GetMercenaryRosterQuery | KingdomTycoon.Application.Mercenaries.Queries | Application | cards/count projection | repo,catalog | Execute(queryDto) |
| GetMercenaryDetailQuery | KingdomTycoon.Application.Mercenaries.Queries | Application | 4-tab DTO | repo,catalog | Execute(instanceId) |
| SetMercenaryActiveHandler | KingdomTycoon.Application.Mercenaries.Commands | Application | atomic desired state | UoW,hasher | HandleAsync |
| MercenaryOperationRequestFactory | KingdomTycoon.Application.Mercenaries.Commands | Application | UUID/hash command | id,hasher | CreateSetActive |
| CreateMercenaryFromSnapshot | KingdomTycoon.Application.Mercenaries | Application | internal create/cap guard | validator | ExecuteInternal |
| IMercenaryUnitOfWork | KingdomTycoon.Application.Mercenaries | Application | clone/revision/commit | none | Begin/Commit/Rollback |
| SaveV1MercenaryMapper | KingdomTycoon.Infrastructure.Mercenaries | Infrastructure | strict Save mapping | Save types | Read/Write |
| CanonicalMercenaryCatalog | KingdomTycoon.Infrastructure.Mercenaries | Infrastructure | FK/order/appearance DTO | ContentCatalog | lookup APIs |
| P04ToP05ContentMigration | KingdomTycoon.Infrastructure.Content.Migrations | Infrastructure | starter one-time migration | uuid,clock,validator | CanApply/Apply |
| MercenaryRosterPresenter | KingdomTycoon.Presentation.Mercenaries | Presentation | query/filter/selection/lifecycle | queries,commands,views | Initialize/Refresh/Dispose |
| MercenaryRosterView | KingdomTycoon.Presentation.Mercenaries.Views | Presentation | screen state/grid | DTO only | BindState/BindCards |
| MercenaryCardView | KingdomTycoon.Presentation.Mercenaries.Views | Presentation | pooled card | DTO only | Bind/Reset |
| MercenaryDetailDrawerView | KingdomTycoon.Presentation.Mercenaries.Views | Presentation | tabs/action | DTO only | Open/Bind/Close |
| VirtualizedMercenaryGrid | KingdomTycoon.Presentation.Mercenaries.Views | Presentation | pool/visible range | ScrollRect | SetItems/ScrollTo |
public DTO는 primitive/enum/immutable arrays만 사용하고 JObject/Save/Catalog/Unity object를 노출하지 않는다. View는 Save/Catalog/HTTP를 호출하지 않는다. portrait Sprite는 Presentation asset adapter 내부에서만 resolve한다.

## 17. event·error registry

**상태: CONFIRMED**

| Event | Payload | Publish |
|---|---|---|
| MercenaryActivated | instanceId,activeCount,limit,revision | Save success 후 |
| MercenaryDeactivated | instanceId,activeCount,limit,revision | Save success 후 |
| MercenaryRosterMigrated | addedCount,contentVersion,revision | migration Save success 후 |
| MercenarySelectionChanged | oldId,newId | presentation only |
| MercenaryFilterChanged | queryDigest,filteredCount | presentation analytics only |
| Code | Trigger | UserMessageKey | Retryable | LoggingLevel | SaveMutationAllowed |
|---|---|---|---|---|---|
| MERCENARY_NOT_FOUND | ID lookup 실패 | TXT_P05_ERROR_NOT_FOUND | FALSE | WARN | FALSE |
| MERCENARY_ACTIVE_LIMIT_REACHED | activate count==limit | TXT_P05_ERROR_ACTIVE_LIMIT | TRUE | INFO | FALSE |
| MERCENARY_OWNED_LIMIT_REACHED | internal create total==limit | TXT_P05_ERROR_OWNED_LIMIT | TRUE | INFO | FALSE |
| MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN | non-town-safe/injured/review transition | TXT_P05_ERROR_STATE_FORBIDDEN | TRUE | INFO | FALSE |
| MERCENARY_SAVE_REVISION_CONFLICT | expected!=current | TXT_P05_ERROR_REVISION | TRUE | WARN | FALSE |
| MERCENARY_OPERATION_HASH_MISMATCH | requestHash mismatch | TXT_P05_UI_ERROR | FALSE | ERROR | FALSE |
| MERCENARY_SAVE_WRITE_FAILED | atomic Save 실패 | TXT_P05_ERROR_SAVE | TRUE | ERROR | FALSE |
| P05_MIGRATION_SLOT_LIMIT_INVALID | migration would exceed cap | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| P05_MIGRATION_UUID_FAILED | starter UUID generation 실패 | TXT_P05_ERROR_LOAD | TRUE | ERROR | FALSE |
| P05_MIGRATION_VALIDATION_FAILED | starter/after strict validation 실패 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| P05_PORTRAIT_ASSET_MISSING | job appearance address/prefab marker 없음 | TXT_P05_UI_ERROR | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_ID_INVALID | instanceId RFC9562 UUIDv7; Save unique | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_GENERATION_INVALID | profile/name/appearance/growth seeds nonnull; Seed64 anchored uint64 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_CONTENT_REF_INVALID | job/grade/rank/personality/traits enabled hard FK | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| MERCENARY_GRADE_IMMUTABLE | C/B/A/S/SS; immutable after creation | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_LEVEL_INVALID | level 1..rank.max_level; exp nonnegative | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_TRAITS_INVALID | unique; job eligible; count<=grade.initial_trait_count+rank.trait_slot_bonus | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_COUNTER_INVALID | personalGold/contribution safe nonnegative | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED | active true count<=kingdom.activeMercenaryLimit; total<=ownedMercenaryLimit | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_INACTIVE_STATE_INVALID | active=false이면 autonomy가 IDLE_TOWN\|PROMOTION_READY\|INJURED 중 하나; region/target null | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_AUTONOMY_INVALID | 17 state discriminator와 region/target/time/reason 10 enum 일치 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_POTION_INVALID | potionId unique; quantity>0; 0행 삭제 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_EQUIPMENT_LINK_INVALID | 정확히 WEAPON/ARMOR/HELMET/ACCESSORY; inventory와 양방향/직업 eligibility | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_PROMOTION_INVALID | NONE\|READY\|IN_REVIEW\|COMPLETED_PENDING_APPLY tagged union | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_RECORD_INVALID | 4 counters safe nonnegative; unknown counter 금지 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_NAME_INVALID | trim 후 Unicode scalar 1..20; control 금지; snapshot | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
| SAVE_MERCENARY_LODGE_LIMIT_MISMATCH | Kingdom limits=(4,8),(8,12),(12,18),(16,24) for Lodge L1..4 | TXT_P05_ERROR_LOAD | FALSE | ERROR | FALSE |
## 18. Save load·write·recovery sequence

**상태: CONFIRMED**

1. AppRoot가 active `.3` content와 set3 schema/62 table/hash/FK를 검증한다.
2. P04 single profile locator로 0/1/N을 결정하고 strict recovery한다.
3. `.1` Save이면 기존 `P03_TO_P04_001`로 `.2`를 먼저 commit한 뒤 reload하고, 이어 `P04_TO_P05_001`을 별도 revision으로 적용한다; 두 migration을 한 revision에 합치지 않는다.
4. `.2`이면 cloned Save에 P04→P05 migration을 적용하고 16 invariants를 검증해 1회 atomic commit한다.
5. `.3` Save를 strict map하고 Lodge limits/mercenary links를 검증한다.
6. immutable roster/detail projection을 만들고 Screen을 Content/Empty/Error로 bind한다.
7. SetActive tap은 화면 command gate로 1개만 허용하고 expectedRevision/hash를 검증한다.
8. cloned payload mutate→validate→digest→revision+1 atomic Save 한 번.
9. 성공 event 후 query refresh; 실패 clone 폐기 후 disk current reload.

active toggle은 즉시 Save하며 autosave debounce에 합치지 않는다. pause/focus lost에는 pending command가 있으면 최대 2초 완료를 기다리되 새 mutation을 만들지 않는다. recovered Save toast는 P04 계약을 재사용한다. schema/saveVersion/migration registry shape 변화 없음; content migration ID `P04_TO_P05_001`만 등록한다.

## 19. 테스트·golden

**상태: CONFIRMED**

| TestId | TestClass | Mode | Given | When | Then | Fixture | Timeout |
|---|---|---|---|---|---|---|---|
| P05-E-001 | MercenaryInvariantTests | EditMode | MERC-INV-001 | valid/invalid boundary | exact pass/error | INV_1 | 5s |
| P05-E-002 | MercenaryInvariantTests | EditMode | MERC-INV-002 | valid/invalid boundary | exact pass/error | INV_2 | 5s |
| P05-E-003 | MercenaryInvariantTests | EditMode | MERC-INV-003 | valid/invalid boundary | exact pass/error | INV_3 | 5s |
| P05-E-004 | MercenaryInvariantTests | EditMode | MERC-INV-004 | valid/invalid boundary | exact pass/error | INV_4 | 5s |
| P05-E-005 | MercenaryInvariantTests | EditMode | MERC-INV-005 | valid/invalid boundary | exact pass/error | INV_5 | 5s |
| P05-E-006 | MercenaryInvariantTests | EditMode | MERC-INV-006 | valid/invalid boundary | exact pass/error | INV_6 | 5s |
| P05-E-007 | MercenaryInvariantTests | EditMode | MERC-INV-007 | valid/invalid boundary | exact pass/error | INV_7 | 5s |
| P05-E-008 | MercenaryInvariantTests | EditMode | MERC-INV-008 | valid/invalid boundary | exact pass/error | INV_8 | 5s |
| P05-E-009 | MercenaryInvariantTests | EditMode | MERC-INV-009 | valid/invalid boundary | exact pass/error | INV_9 | 5s |
| P05-E-010 | MercenaryInvariantTests | EditMode | MERC-INV-010 | valid/invalid boundary | exact pass/error | INV_10 | 5s |
| P05-E-011 | MercenaryInvariantTests | EditMode | MERC-INV-011 | valid/invalid boundary | exact pass/error | INV_11 | 5s |
| P05-E-012 | MercenaryInvariantTests | EditMode | MERC-INV-012 | valid/invalid boundary | exact pass/error | INV_12 | 5s |
| P05-E-013 | MercenaryInvariantTests | EditMode | MERC-INV-013 | valid/invalid boundary | exact pass/error | INV_13 | 5s |
| P05-E-014 | MercenaryInvariantTests | EditMode | MERC-INV-014 | valid/invalid boundary | exact pass/error | INV_14 | 5s |
| P05-E-015 | MercenaryInvariantTests | EditMode | MERC-INV-015 | valid/invalid boundary | exact pass/error | INV_15 | 5s |
| P05-E-016 | MercenaryInvariantTests | EditMode | MERC-INV-016 | valid/invalid boundary | exact pass/error | INV_16 | 5s |
| P05-E-017 | P04ToP05MigrationTests | EditMode | P04 empty roster | migrate | 4 starters/exact digest | P05_GOLDEN | 5s |
| P05-E-018 | P04ToP05MigrationTests | EditMode | P04 nonempty roster | migrate | preserve/no starters | P05_NONEMPTY | 5s |
| P05-E-019 | P04ToP05MigrationTests | EditMode | already .3 | migrate | no-op | P05_GOLDEN | 5s |
| P05-E-020 | MercenaryActivityTests | EditMode | 3 active+idle inactive | activate | 4 active/save | P05_ACTIVITY | 5s |
| P05-E-021 | MercenaryActivityTests | EditMode | 4 active | activate | limit/no mutation | P05_ACTIVITY_FULL | 5s |
| P05-E-022 | MercenaryActivityTests | EditMode | each forbidden state | deactivate/activate | state error | P05_STATES | 5s |
| P05-E-023 | MercenaryIdempotencyTests | EditMode | same desired state | retry | no second save | P05_RETRY | 5s |
| P05-E-024 | MercenaryRevisionTests | EditMode | stale revision | toggle | conflict/reload | P05_REVISION | 5s |
| P05-E-025 | MercenaryFilterTests | EditMode | 24 diverse | all filters | AND/OR exact IDs | P05_24 | 5s |
| P05-E-026 | MercenarySortTests | EditMode | ties | all sorts | stable order | P05_24 | 5s |
| P05-E-027 | MercenarySaveRoundTripTests | EditMode | toggle success | reload | byte semantic equality | P05_ACTIVITY | 5s |
| P05-E-028 | P05ContentGoldenTests | EditMode | generated .3 | verify | 62 hash/manifest exact | CONTENT_3 | 20s |
| P05-P-001 | MercenaryRosterBootstrapTests | PlayMode | P05 golden | open nav | 4 cards/count 4/4 and 4/8 | P05-P-001 | 15s |
| P05-P-002 | MercenaryDrawerTests | PlayMode | 4 cards | select/reselect/back | drawer exact | P05-P-002 | 15s |
| P05-P-003 | MercenaryFilterUiTests | PlayMode | 24 roster | filter/search/sort/reset | ordered cards/count | P05-P-003 | 15s |
| P05-P-004 | MercenaryActivityUiTests | PlayMode | 3 active | activate/reload | badge/count persisted | P05-P-004 | 15s |
| P05-P-005 | MercenaryLimitUiTests | PlayMode | 16 active/24 owned | activate 17th | disabled reason | P05-P-005 | 15s |
| P05-P-006 | MercenaryTabsTests | PlayMode | one complete DTO | open 4 tabs | all fields/deferred text | P05-P-006 | 15s |
| P05-P-007 | MercenaryCommonStateTests | PlayMode | 6 fixtures | enter | state table exact | P05-P-007 | 15s |
| P05-P-008 | MercenaryVirtualGridTests | PlayMode | 100 dev DTOs | scroll end/back | pool<=16/no wrong bind | P05-P-008 | 15s |
| P05-P-009 | MercenarySafeAreaTests | PlayMode | 16:9/18:9/20:9 | render | no clip/64 touch | P05-P-009 | 15s |
| P05-P-010 | MercenaryKoreanLargeTextTests | PlayMode | ko 130% | all states/tabs | no ellipsis/scroll | P05-P-010 | 15s |
모든 test는 deterministic clock=`2026-07-20T00:00:00.000Z`, sequence UUIDv7, in-memory filesystem을 DI한다. real time wait/network/Guid.NewGuid 금지. content test는 62 CSV hashes, localization row count/hash, manifest JCS bytes/hash를 비교한다.

## 20. 성능·수명주기

**상태: CONFIRMED**

| Metric | Acceptance |
|---|---|
| Card instantiate | max 16; scroll/re-filter 추가 instantiate 0 |
| 24 roster query | filter+sort p95 <=2ms editor reference; allocation <=64KiB |
| 100 dev DTO scroll | 60fps target; p95 frame<=16.67ms; binding allocation 0B/frame |
| Events | Initialize subscribe once; OnDisable/Dispose all unsubscribe |
| Polling | Save/catalog 0; autonomy display P05 static, timer polling 0 |
| Portrait handles | 5 job Addressable handles shared; screen dispose release once |
| 10-minute memory | managed heap sustained growth <=1MiB; pool/listener counts stable |
| Scene transition | selection/filter/pool cleared; AppRoot catalog/Save retained |
## 21. Editor generation·Android build

**상태: CONFIRMED**

| Purpose | Entrypoint |
|---|---|
| P05 setup | KingdomTycoon.Editor.P05MercenaryRosterSetup.Run |
| UI prefab generator | KingdomTycoon.Editor.P05MercenaryUiGenerator.Generate |
| content .3 | KingdomTycoon.Editor.P05ContentPackageGenerator.GenerateContent3 |
| verify | KingdomTycoon.Editor.P05GeneratedAssetVerifier.VerifyAll |
| Android build | KingdomTycoon.Editor.P05AndroidBuilder.Build |
generated UI root=`Assets/KingdomTycoon/ContentGenerated/P05Mercenary`; prefabs=RosterScreen,Card,DetailDrawer,FilterModal,SortModal. YAML 직접 편집 금지, Editor API와 `.meta` 동시 commit. generator fingerprint는 hierarchy+layout+localization key JCS hash; same no-op, drift면 managed marker asset만 staging publish. Verify 순서 content .1/.2/.3→Save golden→portrait addresses→prefab components/touch/layout→AppRoot nav→tests→build scenes. APK=`Builds/Android/KingdomTycoon-P05-Development.apk`.

## 22. 수동 검증·capture

**상태: CONFIRMED**

| CaptureId | Fixture | Resolution | Filename | Success |
|---|---|---|---|---|
| P05_CAP_01 | P05_GOLDEN | 1920×1080 | p05_01_roster_4.png | 4 cards, active4/4, owned4/8 |
| P05_CAP_02 | P05_24 | 1920×1080 | p05_02_roster_24.png | virtual grid/count16/16 24/24 |
| P05_CAP_03 | P05_DETAIL_WARRIOR | 1920×1080 | p05_03_detail_overview.png | identity/action+reason |
| P05_CAP_04 | P05_DETAIL_WARRIOR | 1920×1080 | p05_04_detail_equipment.png | 4 empty slots/P07 lock |
| P05_CAP_05 | P05_FILTER | 1920×1080 | p05_05_filters.png | chips/query/count/order |
| P05_CAP_06 | P05_ACTIVITY_FULL | 1920×1080 | p05_06_active_limit.png | disabled exact reason |
| P05_CAP_07 | P05_GOLDEN | 2400×1080 | p05_07_20x9.png | safe area/no clip |
| P05_CAP_08 | P05_RECOVERED | 1920×1080 | p05_08_recovery.png | recovery toast+roster intact |
## 23. 구현 파일 지도·커밋 분리

**상태: CONFIRMED**

| Path | Change | Responsibility | Commit |
|---|---|---|---|
| docs/design/TYCOON_P05_MERCENARY_ROSTER_COMPLETE_DESIGN_v1.0.md | NEW | adopted contract | P05-01 |
| client-unity/Assets/StreamingAssets/Content/1.0.0-content.3/ | NEW | 62-table BASE | P05-01 |
| client-unity/Assets/KingdomTycoon/Runtime/Domain/Mercenaries/ | NEW | entity/invariants/filter/sort/activity | P05-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Application/Mercenaries/ | NEW | queries/command/UoW | P05-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Mercenaries/ | NEW | catalog/Save/assets | P05-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Content/Migrations/P04ToP05ContentMigration.cs | NEW | starter migration | P05-02 |
| client-unity/Assets/KingdomTycoon/Runtime/Presentation/Mercenaries/ | NEW | screen/card/drawer/presenter | P05-03 |
| client-unity/Assets/KingdomTycoon/ContentGenerated/P05Mercenary/ | NEW | generated uGUI prefabs | P05-03 |
| client-unity/Assets/KingdomTycoon/Editor/P05*.cs | NEW | setup/verify/build | P05-04 |
| client-unity/Assets/KingdomTycoon/Tests/EditMode/Mercenaries/ | NEW | domain/Save/content tests | P05-04 |
| client-unity/Assets/KingdomTycoon/Tests/PlayMode/Mercenaries/ | NEW | UI/layout/lifecycle tests | P05-04 |
| docs/reports/P05_MERCENARY_ROSTER_REPORT.md | NEW | evidence/captures | P05-05 |
| Commit | Scope | Must not include |
|---|---|---|
| P05-01 | design+content .3 | runtime/UI |
| P05-02 | domain/application/Save migration | UI/assets |
| P05-03 | roster UI/generated prefabs | domain changes |
| P05-04 | tests/verify/Android build | report/CI |
| P05-05 | CI/captures/report/Draft PR | feature changes |
## 24. P05 완료 checklist

**상태: CONFIRMED**

- [ ] P04→P05 starter migration/golden hashes 일치
- [ ] 16 Mercenary invariants와 invalid fixture fail closed
- [ ] Lodge 4/8→16/24 sync 및 max fixture
- [ ] 활동 toggle atomic Save/revision/idempotency
- [ ] 5 job/5 grade/6 rank/17 state filters and stable sort
- [ ] 4-tab detail 모든 strict Save field projection
- [ ] 전투/장비/승급/모집 mutation 0개
- [ ] content .3 62 table/localization/manifest golden
- [ ] portrait 5 address valid, generated uGUI no manual YAML
- [ ] 6 UI states/20:9/130%/64px touch
- [ ] virtual pool<=16 and lifecycle/perf acceptance
- [ ] EditMode/PlayMode green
- [ ] Android P05 APK/captures/report/Draft PR

## 25. UNRESOLVED

**상태: CONFIRMED**

`NONE`

본 문서는 추가 설계 질문 없이 P05 canonical package→domain/application/Save→roster UI→tests→Android APK를 구현하기 위한 최종 계약이다.
