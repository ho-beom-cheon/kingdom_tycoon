# TYCOON P04 왕국·시설 최종 통합 설계 v1.0.1 정정 부록

## 0. 문서 상태·적용 권위

**상태: CONFIRMED**

| 항목 | Exact value |
|---|---|
| 파일 | TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.1_CORRECTION_APPENDIX.md |
| 대상 원문 | TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md |
| 적용 방식 | v1.0과 함께 읽으며 동일 항목은 본 부록이 우선 |
| 변경 범위 | CR-01..CR-07만 정정; 나머지 v1.0 유지 |
| saveVersion | 1 유지; Save JSON shape 변경 없음 |
| contentVersion | 1.0.0-content.2 유지 |
| csvSchemaSetVersion | 3 유지 |
| 기준 commit | 5100086 |
각 절 머리의 상태가 그 절의 모든 규칙과 표 행에 적용된다. `CORRECTION`은 v1.0의 동일 문장을 폐기하며, 별도 언급이 없는 v1.0 golden/state/economy/NPC/UI/Save 값은 byte-semantic 그대로다.

## 1. CR-01 — runtime BOOLEAN lexical

**상태: CORRECTION / CONFIRMED**

P03의 BOOLEAN lexical `TRUE|FALSE`를 유지한다. `0|1`, 대소문자 변형, 공백은 `CSV_RUNTIME_BOOLEAN_LEXICAL_INVALID`로 import를 차단한다. BOOLEAN 행의 `min_value`와 `max_value`는 반드시 empty field이며 unit은 `BOOL`이다. v1.0의 `P04_TEST_CLOCK_MODE,BOOLEAN,0,...,0,1,...` 행과 그 runtime hash는 폐기한다.

### 1.1 `runtime_config.csv` 전체 18 rows

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
P04_TEST_CLOCK_MODE,BOOLEAN,FALSE,BOOL,,,TXT_P04_DEV_CLOCK_DESC,CONFIRMED,TRUE
P04_DRAWER_ANIMATION_SECONDS,DECIMAL,0.22,SECONDS,0,1,TXT_P04_DRAWER_DURATION_DESC,TUNABLE,TRUE
P04_AUTOSAVE_DEBOUNCE_SECONDS,DECIMAL,0.5,SECONDS,0,5,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,TUNABLE,TRUE
P04_TIMER_UI_TICK_SECONDS,DECIMAL,1,SECONDS,0.1,5,TXT_P04_TIMER_TICK_DESC,TUNABLE,TRUE
```
| 항목 | 값 |
|---|---|
| rowCount | 18 |
| canonical bytes | UTF-8 no BOM, RFC4180, CRLF, final CRLF |
| SHA-256 | 610576da67809118f8dd87f4526be69682c5657de0852baa3fd8b3efc28efd25 |
validator matrix는 BOOLEAN→`value in {TRUE,FALSE}` 및 min/max empty, INTEGER→정수 lexical과 min/max 정수, DECIMAL→finite decimal과 min/max decimal, STRING→그대로다. 기존 `RARE_EQUIPMENT_AUTO_PROTECT=TRUE`는 변환 없이 통과하며 runtime adapter가 `TRUE→true`, `FALSE→false`로만 변환한다.

## 2. CR-02 — schema set 3 manifest와 schema

**상태: CORRECTION / CONFIRMED**

v1.0의 manifest checksum 단독 설명을 아래 full JSON 두 개로 교체한다. property, field, enumValues, foreignKeys와 배열 순서는 모두 digest 입력이다. `content_manifest.json`은 UTF-8 논리 JSON을 RFC 8785로 canonicalize한 digest가 권위이고, pretty JSON의 공백/개행은 권위가 아니다.

### 2.1 최종 `content_manifest.json` 전체 JSON

```json
{
  "schemaId": "urn:tycoon:content-manifest:v2",
  "contractVersion": 2,
  "contentVersion": "1.0.0-content.2",
  "csvSchemaSetVersion": 3,
  "packageKind": "BASE",
  "baseContentVersion": null,
  "minimumGameVersion": "1.0.0-p04",
  "channel": "DEV",
  "generatedAtUtc": "2026-07-19T00:00:00.000Z",
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
      "sha256": "6767d07492a4ece8abf3d6d8b19e1b84056564a6632a619cd4118751001956f6",
      "rowCount": 612,
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
            "BOSS"
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
          "domain": "DECIMAL",
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

### 2.2 최종 `content_manifest.schema.json` 전체 JSON

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:tycoon:content-manifest-schema:v2",
  "type": "object",
  "additionalProperties": false,
  "required": [
    "schemaId",
    "contractVersion",
    "contentVersion",
    "csvSchemaSetVersion",
    "packageKind",
    "baseContentVersion",
    "minimumGameVersion",
    "channel",
    "generatedAtUtc",
    "tables"
  ],
  "properties": {
    "schemaId": {
      "const": "urn:tycoon:content-manifest:v2"
    },
    "contractVersion": {
      "const": 2
    },
    "contentVersion": {
      "type": "string",
      "pattern": "^[0-9]+\\.[0-9]+\\.[0-9]+-content\\.[1-9][0-9]*$"
    },
    "csvSchemaSetVersion": {
      "const": 3
    },
    "packageKind": {
      "enum": [
        "BASE",
        "PATCH",
        "TEMPLATE"
      ]
    },
    "baseContentVersion": {
      "type": [
        "string",
        "null"
      ]
    },
    "minimumGameVersion": {
      "type": "string",
      "pattern": "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?$"
    },
    "channel": {
      "enum": [
        "DEV",
        "STAGING",
        "PRODUCTION"
      ]
    },
    "generatedAtUtc": {
      "type": "string",
      "pattern": "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z$"
    },
    "tables": {
      "type": "array",
      "minItems": 1,
      "items": {
        "$ref": "#/$defs/table"
      }
    }
  },
  "allOf": [
    {
      "if": {
        "properties": {
          "packageKind": {
            "const": "BASE"
          }
        }
      },
      "then": {
        "properties": {
          "baseContentVersion": {
            "type": "null"
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "packageKind": {
            "enum": [
              "PATCH",
              "TEMPLATE"
            ]
          }
        }
      },
      "then": {
        "properties": {
          "baseContentVersion": {
            "type": "string"
          }
        }
      }
    }
  ],
  "$defs": {
    "field": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "name",
        "domain",
        "nullable",
        "enumValues"
      ],
      "properties": {
        "name": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9_]*$"
        },
        "domain": {
          "enum": [
            "STRING",
            "STABLE_ID",
            "BOOL",
            "INT32",
            "INT64",
            "SAFE_INT",
            "DECIMAL",
            "UTC_INSTANT",
            "DATE",
            "LOCALE",
            "SEMVER",
            "HEX64",
            "SEED64",
            "STATUS",
            "ENUM"
          ]
        },
        "nullable": {
          "type": "boolean"
        },
        "enumValues": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "uniqueItems": true
        }
      }
    },
    "foreignKey": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "sourceFields",
        "targetFile",
        "targetFields",
        "mode"
      ],
      "properties": {
        "sourceFields": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "string"
          }
        },
        "targetFile": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9_]*\\.csv$"
        },
        "targetFields": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "string"
          }
        },
        "mode": {
          "enum": [
            "HARD",
            "SOFT"
          ]
        }
      }
    },
    "table": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "file",
        "schemaVersion",
        "required",
        "sha256",
        "rowCount",
        "primaryKey",
        "fields",
        "foreignKeys"
      ],
      "properties": {
        "file": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9_]*\\.csv$"
        },
        "schemaVersion": {
          "type": "integer",
          "minimum": 1
        },
        "required": {
          "const": true
        },
        "sha256": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        },
        "rowCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "primaryKey": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "string"
          }
        },
        "fields": {
          "type": "array",
          "minItems": 1,
          "items": {
            "$ref": "#/$defs/field"
          }
        },
        "foreignKeys": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/foreignKey"
          }
        }
      }
    }
  }
}
```

| Golden | Expected |
|---|---|
| manifest RFC8785 byte length | 67264 |
| manifest RFC8785 SHA-256 | 73e493ca508b5af96435a2cc1ad72d6c33ffdeae2f594863d9d3ee0dc302fd07 |
| schema RFC8785 byte length | 2724 |
| schema RFC8785 SHA-256 | 451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41 |
| runtime_config.csv SHA-256 | 610576da67809118f8dd87f4526be69682c5657de0852baa3fd8b3efc28efd25 |
| localizations.csv SHA-256 | 6767d07492a4ece8abf3d6d8b19e1b84056564a6632a619cd4118751001956f6 |
위 manifest digest가 P04 package golden checksum이다. 동일 contentVersion에 다른 digest는 `CONTENT_RELEASE_SPLIT_BRAIN`, set 3을 set 2 schema로 검사하면 `CONTENT_MANIFEST_SCHEMA_SET_UNSUPPORTED`다.

### 2.3 신규 descriptor semantic validator

| File | Field/Rule | Exact validation | ErrorCode |
|---|---|---|---|
| facility_construction_rules.csv | PK | (facility_id,level) unique; 정확히 enabled 8×4=32 | CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID |
| facility_construction_rules.csv | facility_id+level | facility_levels composite HARD FK; enabled target | CSV_FACILITY_CONSTRUCTION_FK_INVALID |
| facility_construction_rules.csv | level | SAFE_INT lexical, 1..4 | CSV_FACILITY_CONSTRUCTION_LEVEL_INVALID |
| facility_construction_rules.csv | build_or_upgrade_duration_seconds | SAFE_INT 1..86400 | CSV_FACILITY_DURATION_INVALID |
| facility_construction_rules.csv | cancel_refund_ratio | DECIMAL exact numeric 0 for all P04 rows | CSV_FACILITY_CANCEL_RATIO_INVALID |
| facility_world_assets.csv | PK/address | asset_id unique; address nonempty and globally unique | CSV_FACILITY_WORLD_ASSET_DUPLICATE |
| facility_world_assets.csv | asset_id | asset_register.asset_id HARD FK | CSV_FACILITY_WORLD_ASSET_REGISTRY_INVALID |
| facility_world_assets.csv | facility_id | nullable; nonempty value facilities HARD FK | CSV_FACILITY_WORLD_ASSET_FACILITY_INVALID |
| facility_world_assets.csv | facility BASE coverage | 8 enabled facilityId each exactly 1 PREFAB/BASE | CSV_FACILITY_WORLD_ASSET_COVERAGE_INVALID |
| facility_world_assets.csv | role coverage | BACKGROUND,PLOT,LOCKED,CONSTRUCTION,STOPPED each exactly 1; facility_id empty | CSV_FACILITY_WORLD_ROLE_COVERAGE_INVALID |
| facility_world_assets.csv | dimensions | width_px,height_px SAFE_INT 1..4096 | CSV_FACILITY_WORLD_DIMENSION_INVALID |
| facility_world_assets.csv | pivot | finite DECIMAL 0..1 | CSV_FACILITY_WORLD_PIVOT_INVALID |
| facility_world_assets.csv | pixels_per_unit | SAFE_INT exactly 100 | CSV_FACILITY_WORLD_PPU_INVALID |
| both | status/enabled | CONFIRMED\|TUNABLE만 enabled TRUE; common policy | CSV_STATUS_ENABLED_INVALID |
descriptor의 fields 순서는 CSV header 순서와 byte-for-byte 같고 primaryKey/sourceFields/targetFields 순서는 비교 시 정렬하지 않는다. enumValues는 manifest에 기록된 순서만 허용하며 importer가 다시 정렬하지 않는다.

## 3. CR-03 — immutable package 경로와 active content

**상태: CORRECTION / CONFIRMED**

```text
client-unity/Assets/StreamingAssets/Content/
├── 1.0.0-content.1/
│   ├── content_manifest.json
│   ├── content_manifest.schema.json
│   └── <P03 CSV 60개>
└── 1.0.0-content.2/
    ├── content_manifest.json
    ├── content_manifest.schema.json
    └── <P04 CSV 62개>
```

현재 평면 P03 package의 60 CSV, manifest, schema는 `git mv`로 `.1/`에 이동한다. copy 후 중복을 남기거나 bytes를 재직렬화하지 않는다. 이동 전후 각 CSV/manifest hash와 rowCount를 검증한 뒤에만 평면 파일을 없앤다. root에는 두 version directory 외 파일을 허용하지 않으며 `.1`은 이후 수정·삭제하지 않는다. `.2`는 clean generator output으로 전부 생성한다.

| Type/API | Exact contract |
|---|---|
| KingdomTycoon.Application.Content.IActiveContentVersionProvider | `string ActiveContentVersion { get; }` |
| KingdomTycoon.Infrastructure.Content.CompileTimeActiveContentVersionProvider | P04 player constant `1.0.0-content.2`; constructor input 없음 |
| KingdomTycoon.Infrastructure.Content.ContentCatalogService | `Task<ContentCatalog> LoadActiveAsync(CancellationToken)`; provider 값과 동일한 한 package만 load |
| KingdomTycoon.Infrastructure.Content.IStreamingAssetReader | `Task<byte[]> ReadAllBytesAsync(string relativePath,CancellationToken)`; directory enumeration API 없음 |
| Path composition | POSIX relative `Content/{ActiveContentVersion}/{file}`; 모든 segment를 regex/manifest allow-list로 검증 |
| Version assertion | directory name = manifest.contentVersion = provider value; 하나라도 다르면 fail closed |
| Schema registry | compiled set2/set3 schema를 csvSchemaSetVersion으로 선택; sibling schema는 tooling golden과 동일성 검증 |
Android reader는 `Application.streamingAssetsPath + '/' + relativePath`를 `UnityWebRequest.Get`으로 읽고, Editor/standalone reader는 canonicalized local path의 `File.ReadAllBytesAsync`를 사용한다. importer는 manifest descriptor에서 파일명을 얻으므로 APK 내부 directory를 열거하지 않는다. `..`, backslash, URI scheme, percent-encoding, absolute path는 `CONTENT_ACTIVE_VERSION_INVALID`다. latest 탐색, fallback, `.1→.2` catalog merge, remote network fetch는 금지한다. `UNITY_EDITOR || DEVELOPMENT_BUILD`에서만 `DevelopmentActiveContentVersionProvider(string)`를 DI할 수 있고 허용 set은 `.1|.2`다. player command-line/environment override는 사용하지 않는다. non-development build에 development provider type reference나 override flag가 있으면 `CONTENT_RELEASE_OVERRIDE_FORBIDDEN`으로 build를 차단한다.

| Code | Trigger |
|---|---|
| CONTENT_ACTIVE_VERSION_INVALID | provider 값 regex/path 안전성 실패 |
| CONTENT_ACTIVE_PACKAGE_NOT_FOUND | 선택 directory/manifest/schema 없음 |
| CONTENT_ACTIVE_MANIFEST_VERSION_MISMATCH | directory/provider/manifest version 불일치 |
| CONTENT_ACTIVE_PACKAGE_VALIDATION_FAILED | schema/hash/row/FK 검증 실패 |
| CONTENT_RELEASE_OVERRIDE_FORBIDDEN | release build override/provider 포함 |
generator entrypoint는 `KingdomTycoon.Editor.P03ImmutableContentPackager.GenerateContent1`과 `KingdomTycoon.Editor.P04ContentPackageGenerator.GenerateContent2`; 검증은 각각 `VerifyContent1`, `VerifyContent2`, 통합은 `P04GeneratedAssetVerifier.VerifyAll` 순서다. 두 generator 모두 staging에 생성·검증한 뒤 target이 없을 때만 atomic directory rename으로 publish한다. target이 이미 있고 golden과 같으면 no-op, 하나라도 다르면 `CONTENT_RELEASE_SPLIT_BRAIN`으로 실패하며 같은 version directory를 replace하지 않는다.

`.gitattributes` exact rule은 다음과 같다. 더 구체적인 상충 rule을 뒤에 두지 않는다.

```gitattributes
client-unity/Assets/StreamingAssets/Content/**/*.csv text eol=crlf
client-unity/Assets/StreamingAssets/Content/**/*.json text eol=lf
```

Android APK에는 `.1`과 `.2`를 모두 포함한다. active는 항상 `.2`; `.1`은 Save migration 검증·진단과 rollback build 재현을 위해 v1 지원 기간 동안 보존한다. build guard는 version directory가 정확히 2개이고 flat CSV/manifest가 0개인지 검사한다.

## 4. CR-04 — single local profile 발견

**상태: CORRECTION / CONFIRMED**

`profile-index.json`은 만들지 않는다. P04는 single local profile이며 directory 자체와 strict recovered Save가 index다.

| TypeName | Namespace | Public API |
|---|---|---|
| IProfileLocator | KingdomTycoon.Application.Profiles | `Task<ProfileLocateResult> LocateAsync(CancellationToken cancellationToken)` |
| ProfileLocateResult | KingdomTycoon.Application.Profiles | `Kind: NONE\|ONE\|AMBIGUOUS`, `ProfileId?`, `ValidCandidates`, `InvalidEntries` |
| LocalProfileLocator | KingdomTycoon.Infrastructure.Save | constructor `(IAtomicSaveRepository, IFileSystem, ISavePathPolicy)`; implements locator |
| ISingleProfileCreator | KingdomTycoon.Application.Profiles | `Task<ProfileId> CreateAndCommitAsync(NewGameDraft,CancellationToken)` |
발견 algorithm은 다음 순서다.

1. `<persistentDataPath>/saves`를 직접 열고 자식 entry만 열거한다. root canonical path 밖은 접근하지 않는다.
2. symlink/reparse point를 먼저 검사해 follow하지 않고 invalid diagnostic `SAVE_PROFILE_LINK_FORBIDDEN`으로 기록한다.
3. directory name이 소문자 RFC 9562 UUIDv7 canonical `8-4-4-4-12`가 아니면 candidate에서 제외하고 `SAVE_PROFILE_DIRECTORY_NAME_INVALID`로 기록한다. file entry와 `.creating.*`도 candidate가 아니다.
4. canonical directory를 name UTF-8 ordinal로 정렬한다.
5. `save.json|save.tmp|save.bak1|save.bak2|save.bak3` 중 하나라도 있으면 기존 profile lock 안에서 recovery를 실행한다. tmp/bak만 있어도 동일하다. 유효 후보 승격은 기존 원자 복구 계약 그대로다. 다섯 파일이 모두 없으면 invalid `SAVE_PROFILE_NO_SAVE_CANDIDATE`.
6. recovered Save의 profileId가 directory name과 같고 strict schema/content/integrity/invariant를 통과할 때만 valid다. 실패 directory는 삭제·rename·repair하지 않고 error 목록에 보존한다.
7. valid=0이면 새 profile 생성, valid=1이면 그 ID, valid>=2이면 자동 선택 없이 `SAVE_PROFILE_AMBIGUOUS`다. invalid count는 0/1/N 선택 수에 포함하지 않는다. root enumeration 자체 실패는 `SAVE_PROFILE_DISCOVERY_FAILED`.

새 profile은 `saves/.creating.<profileId>.<writerId>/`에 Save를 revision=1까지 atomic commit하고 file/directory flush 및 strict reload를 마친 뒤, canonical target이 없음을 확인해 directory를 `<profileId>/`로 atomic rename하고 parent `saves/`를 flush한다. locator는 `.creating.*`를 valid candidate 수에서 무조건 제외하므로 rename 전에는 노출되지 않는다. creator preflight에서 staging이 1개이고 strict recovered Save/profileId가 유효하면 새 ID를 만들지 않고 그 staging의 rename/flush만 재시도한다. staging이 2개 이상이거나 하나라도 invalid이면 `P04_BOOTSTRAP_SAVE_FAILED`로 fail closed하고 새 staging을 추가하지 않는다. 자동 삭제는 없다. rename 성공 뒤에만 AppRoot session에 active profileId를 publish한다.

| Error | UI key | Retryable |
|---|---|---|
| SAVE_PROFILE_AMBIGUOUS | TXT_P04_ERROR_PROFILE_AMBIGUOUS | FALSE |
| SAVE_PROFILE_DISCOVERY_FAILED | TXT_P04_ERROR_PROFILE_DISCOVERY | TRUE |
| SAVE_PROFILE_LINK_FORBIDDEN | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE |
| SAVE_PROFILE_DIRECTORY_NAME_INVALID | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE |
| SAVE_PROFILE_NO_SAVE_CANDIDATE | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE |
| TestId | Class | Given | Then |
|---|---|---|---|
| P04-E-PROFILE-001 | LocalProfileLocatorTests | empty root | NONE; creator invoked once |
| P04-E-PROFILE-002 | LocalProfileLocatorTests | one valid save.json | ONE/exact ID |
| P04-E-PROFILE-003 | LocalProfileLocatorTests | two valid canonical dirs | AMBIGUOUS/no active ID |
| P04-E-PROFILE-004 | LocalProfileLocatorTests | one valid + malformed dir | ONE; malformed diagnostic retained |
| P04-E-PROFILE-005 | LocalProfileLocatorTests | tmp only valid | recovery promotes; ONE |
| P04-E-PROFILE-006 | LocalProfileLocatorTests | bak1 only valid | recovery promotes; ONE |
| P04-E-PROFILE-007 | LocalProfileLocatorTests | symlink/reparse to valid save | not followed; invalid diagnostic |
| P04-E-PROFILE-008 | SingleProfileCreatorTests | commit before rename crash | canonical dir invisible; staging retained |
| P04-E-PROFILE-009 | SingleProfileCreatorTests | rename+parent flush success | next locate ONE |
| P04-E-PROFILE-010 | LocalProfileLocatorTests | directory ID != payload profileId | invalid; no repair |
| P04-E-PROFILE-011 | LocalProfileLocatorTests | enumeration Unauthorized/IO | DISCOVERY_FAILED |
| P04-E-PROFILE-012 | SingleProfileCreatorTests | one valid .creating staging | same profileId rename resume; new UUID 0회 |
| P04-E-PROFILE-013 | SingleProfileCreatorTests | multiple/invalid staging | fail closed; new staging 0개 |
## 5. CR-05 — operationId/requestHash 책임

**상태: CORRECTION / CONFIRMED**

| TypeName | Namespace | Public API / responsibility |
|---|---|---|
| FacilityOperationRequestFactory | KingdomTycoon.Application.Facilities.Commands | ctor `(IUuidV7Provider,IFacilityRequestHasher)`; `CreateBuild(rev,facilityId)`, `CreateUpgrade(rev,facilityId,targetLevel)`, `CreateClaim(rev,facilityId,facilityJobOperationId)`, `CreateAssign(rev,facilityId,npcId)`, `CreateUnassign(rev,facilityId,npcId)` |
| IFacilityRequestHasher | KingdomTycoon.Application.Facilities.Commands | `string ComputeHash(IFacilityOperationHashInput input)`; RFC8785 UTF-8→SHA-256 lower hex |
| FacilityRequestHasher | KingdomTycoon.Infrastructure.Facilities | strict typed object serializer; unknown/null-added field 금지 |
| Facility command records | KingdomTycoon.Application.Facilities.Commands | immutable `operationId,requestHash,expectedRevision` + type fields |
| Start/Claim/Assign handlers | same command namespace | ID 생성 금지; hash input 재구성·constant-time 비교 후 guard |
| KingdomScreenPresenter | KingdomTycoon.Presentation.Kingdom | factory 결과 command instance를 pending slot에 보존하고 handler에 전달 |
| Immutable command DTO | Exact required fields | Handler API |
|---|---|---|
| StartFacilityBuildCommand | `Guid operationId,string requestHash,long expectedRevision,string facilityId,int targetLevel` | `StartFacilityBuildHandler.HandleAsync(command,ct)` |
| StartFacilityUpgradeCommand | `Guid operationId,string requestHash,long expectedRevision,string facilityId,int targetLevel` | `StartFacilityUpgradeHandler.HandleAsync(command,ct)` |
| ClaimFacilityJobCommand | `Guid operationId,string requestHash,long expectedRevision,string facilityId,Guid facilityJobOperationId` | `ClaimFacilityJobHandler.HandleAsync(command,ct)` |
| AssignManagementNpcCommand | `Guid operationId,string requestHash,long expectedRevision,string facilityId,Guid npcInstanceId` | `AssignManagementNpcHandler.HandleAsync(command,ct)` |
| UnassignManagementNpcCommand | `Guid operationId,string requestHash,long expectedRevision,string facilityId,Guid npcInstanceId` | `UnassignManagementNpcHandler.HandleAsync(command,ct)` |
다섯 DTO는 모두 `KingdomTycoon.Application.Facilities.Commands`의 `sealed record`이며 constructor 외 setter와 parameterless constructor를 공개하지 않는다. `targetLevel`은 Build에서 정확히 1이고 Upgrade에서 current+1이며 handler가 catalog/current Save와 다시 대조한다.

v1.0 handler type 표의 `clock,id` dependency는 `clock`만 남긴다. UUID provider는 factory에만 있다. factory는 새 operationId를 정확히 한 번 만든 뒤 hash를 계산한다. `requestHash`는 hash input object 자체에는 포함하지 않는다. property는 아래 shape 그대로이며 null placeholder나 추가 property를 넣지 않는다.

### 5.1 `START_FACILITY_BUILD` requestHash input

```json
{
  "commandType": "START_FACILITY_BUILD",
  "operationId": "019f77ac-2c00-7002-8000-000000000001",
  "expectedRevision": 12,
  "facilityId": "FAC_STORE",
  "targetLevel": 1
}
```

expected RFC8785 SHA-256: `1aeb5d362aa9e3cac14c7b41815706033eb89ac48dc0db738e834eebd7a57f88`

### 5.2 `START_FACILITY_UPGRADE` requestHash input

```json
{
  "commandType": "START_FACILITY_UPGRADE",
  "operationId": "019f77ac-2c00-7002-8000-000000000002",
  "expectedRevision": 12,
  "facilityId": "FAC_STORE",
  "targetLevel": 2
}
```

expected RFC8785 SHA-256: `33d1a481922f108cbf9f6741bdc1d6dae05fe674839a9c9af888dc31986f7196`

### 5.3 `CLAIM_FACILITY_JOB` requestHash input

```json
{
  "commandType": "CLAIM_FACILITY_JOB",
  "operationId": "019f77ac-2c00-7002-8000-000000000003",
  "expectedRevision": 13,
  "facilityId": "FAC_STORE",
  "facilityJobOperationId": "019f77ac-2c00-7002-8000-000000000001"
}
```

expected RFC8785 SHA-256: `5a0c35f7fe9a5ac6598a3ad395ff82c172d11d60a33f5a1c652972978fcdb693`

### 5.4 `ASSIGN_MANAGEMENT_NPC` requestHash input

```json
{
  "commandType": "ASSIGN_MANAGEMENT_NPC",
  "operationId": "019f77ac-2c00-7002-8000-000000000004",
  "expectedRevision": 14,
  "facilityId": "FAC_STORE",
  "npcInstanceId": "019f77ac-2c00-7001-8000-000000000001"
}
```

expected RFC8785 SHA-256: `c196c8327648bf8d72ecca3ed13e3b1429493e368b58cb405e281247cef98349`

### 5.5 `UNASSIGN_MANAGEMENT_NPC` requestHash input

```json
{
  "commandType": "UNASSIGN_MANAGEMENT_NPC",
  "operationId": "019f77ac-2c00-7002-8000-000000000005",
  "expectedRevision": 15,
  "facilityId": "FAC_STORE",
  "npcInstanceId": "019f77ac-2c00-7001-8000-000000000001"
}
```

expected RFC8785 SHA-256: `9eebf15000fbfe2f8c40f82a321270cc3362e5ec7959e9554c71d5069884a589`

`operationId`는 UI command attempt ID이고 `facilityJobOperationId`는 이미 Save의 FacilityJob을 식별하는 시작 operation ID다. Build/Upgrade 성공 시 둘은 같은 값이다. Claim은 새 operationId를 가지며 기존 job ID를 별도 참조한다.

Build/Upgrade/Claim은 `operationType=FACILITY_JOB`, facilityJobType=`BUILD|UPGRADE` journal을 사용한다. Assign/Unassign은 strict Save v1 journal discriminator에 대응 subtype이 없으므로 journal을 추가하지 않는다. 대신 desired-state command다: 동일 NPC가 이미 양방향 배치돼 있으면 Assign success no-op, 이미 해제됐으면 Unassign success no-op이다. 이를 위해 Save schema/saveVersion을 늘리지 않는다.

handler 결과를 받기 전 timeout, `SAVE_LOCK_BUSY`, transient `SAVE_WRITE_FAILED`의 Retry는 presenter pending slot의 동일 immutable command instance를 사용한다. success/permanent error에서 slot을 폐기한다. `FACILITY_SAVE_REVISION_CONFLICT`에서는 pending을 폐기하고 reload한 뒤 사용자의 새 tap이 새 operationId/hash를 만든다. scene close/app kill 뒤 UI command instance는 복원하지 않는다. 재실행 때 journaled command는 Save journal/FacilityJob로 resume 또는 replay하고, Assign/Unassign은 현재 desired state로 이미 적용됐는지 판정한다.

## 6. CR-06 — Kingdom world uGUI component 계약

**상태: CORRECTION / CONFIRMED**

P04 시설 월드는 uGUI로 고정한다. v1.0의 SpriteRenderer, Orthographic camera와 PixelPerfect camera가 P04 배치/render acceptance라는 문장은 폐기한다. Sprite pivot/PPU는 production sprite 교체 compatibility metadata로만 검증한다.

| assetId | address | P04 consumer | role |
|---|---|---|---|
| ASSET_FAC_TAVERN_PLACEHOLDER_V1 | P04/Kingdom/FAC_TAVERN | Sprite subasset + uGUI prefab BaseSprite | FAC_TAVERN |
| ASSET_FAC_LODGE_PLACEHOLDER_V1 | P04/Kingdom/FAC_LODGE | Sprite subasset + uGUI prefab BaseSprite | FAC_LODGE |
| ASSET_FAC_GUILD_PLACEHOLDER_V1 | P04/Kingdom/FAC_GUILD | Sprite subasset + uGUI prefab BaseSprite | FAC_GUILD |
| ASSET_FAC_STORE_PLACEHOLDER_V1 | P04/Kingdom/FAC_STORE | Sprite subasset + uGUI prefab BaseSprite | FAC_STORE |
| ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1 | P04/Kingdom/FAC_BLACKSMITH | Sprite subasset + uGUI prefab BaseSprite | FAC_BLACKSMITH |
| ASSET_FAC_ALCHEMY_PLACEHOLDER_V1 | P04/Kingdom/FAC_ALCHEMY | Sprite subasset + uGUI prefab BaseSprite | FAC_ALCHEMY |
| ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1 | P04/Kingdom/FAC_WAREHOUSE | Sprite subasset + uGUI prefab BaseSprite | FAC_WAREHOUSE |
| ASSET_FAC_INFIRMARY_PLACEHOLDER_V1 | P04/Kingdom/FAC_INFIRMARY | Sprite subasset + uGUI prefab BaseSprite | FAC_INFIRMARY |
| ASSET_KINGDOM_BACKGROUND_V1 | P04/Kingdom/Background | scene-level Image | BACKGROUND |
| ASSET_FACILITY_PLOT_V1 | P04/Kingdom/Plot | 8 scene-level Images | PLOT |
| ASSET_FACILITY_LOCKED_V1 | P04/Kingdom/Locked | StateOverlay Image | LOCKED |
| ASSET_FACILITY_CONSTRUCTION_V1 | P04/Kingdom/Construction | StateOverlay Image | BUILDING/UPGRADING |
| ASSET_FACILITY_STOPPED_V1 | P04/Kingdom/Stopped | StoppedIcon Image | STOPPED |
8개 prefab은 address별로 다음 동일 hierarchy를 가진다. `{order}`는 v1.0 anchor 표의 20..27이다.

| GameObject | Components | Exact settings | raycastTarget | Content |
|---|---|---|---|---|
| FacilityWorldView(root) | RectTransform | anchor/size from catalog; pivot 0.5,0.5 | — | — |
| FacilityWorldView(root) | Canvas | renderMode inherited; overrideSorting=true; sortingLayerName=KingdomWorld; sortingOrder={order} | — | — |
| FacilityWorldView(root) | GraphicRaycaster | ignoreReversedGraphics=true; blockingObjects=None | — | — |
| FacilityWorldView(root) | FacilityWorldView | facilityId serialized marker; no Save/Catalog reference | — | — |
| BaseSprite | RectTransform+Image | stretch full; preserveAspect=true; type=Simple; maskable=true | FALSE | base facility sprite |
| StateOverlay | RectTransform+Image | stretch full; preserveAspect=true; type=Simple; maskable=true | FALSE | locked/construction sprite |
| StoppedIcon | RectTransform+Image | anchor 1,1; pivot 1,1; size 64×64; offset -8,-8 | FALSE | stopped sprite |
| Label | RectTransform+TextMeshProUGUI | anchor below center; width root; height 52; alignment Center; wrapping=true | FALSE | localized facility name |
| HitTarget | RectTransform+Image | stretch full then min-size expansion; color #00000000 | TRUE | none |
| HitTarget | Button | transition=ColorTint with alpha unchanged; navigation=Explicit/None on touch | — | onClick→presenter SelectFacility |
background은 `WorldCanvas/KingdomScreen/SafeArea/KingdomBackground`의 scene-level stretch Image, raycastTarget=false, sorting order 0이다. plot은 `FacilityPlotLayer/Plot_<facilityId>`의 scene-level Image 8개, raycastTarget=false, order 10..17이며 prefab 공통 child가 아니다. facility nested Canvas만 order 20..27을 쓴다. Drawer/HUD canvas sorting은 기존 v1.0을 유지한다.

| FacilityState | BaseSprite | StateOverlay | StoppedIcon | Label/status | HitTarget |
|---|---|---|---|---|---|
| LOCKED | active; #0000008C | Locked sprite; active; #FFFFFFFF | inactive | label + lock reason | hit active |
| BUILDABLE | active; #FFFFFF59 | inactive | inactive | label + buildable badge | hit active |
| BUILDING | active; #B0B0B0FF | Construction; active; #FFFFFFFF | inactive | label + timer | hit active |
| ACTIVE | active; #FFFFFFFF | inactive | inactive | label + active badge | hit active |
| UPGRADING | active; #FFFFFFFF | Construction; active; #6EC6FFFF | inactive | label + timer | hit active |
| STOPPED | active; #A0A0A0FF | inactive | active; #FFFFFFFF | label + stop reason | hit active |
Image color는 sRGB RGBA hex다. StateOverlay sprite를 상태 변경 때 명시적으로 null/assigned하고 inactive object의 old sprite에 의존하지 않는다. `Image.SetNativeSize()`는 금지한다. 모든 visual Image/TMP의 raycastTarget=false이고 투명 HitTarget만 true다.

WorldCanvas는 `ScreenSpaceOverlay`, worldCamera=null이다. CanvasScaler는 `ScaleWithScreenSize`, referenceResolution=1920×1080, ScreenMatchMode=MatchWidthOrHeight, matchWidthOrHeight=0.5, referencePixelsPerUnit=100이다. P04 acceptance에서 orthographic size/camera crop/PixelPerfect는 검사하지 않는다. 생성 Sprite는 256×256(Stopped 64×64), PPU=100, facility pivot=(0.5,0.1), role pivot=(0.5,0.5)를 asset replacement validator가 검사한다.

Safe Area에서 `content = safeArea`를 Canvas reference pixels로 환산하고 `facilityBand = content`에서 top HUD 112와 bottom nav 112를 뺀다. `designWidth=min(facilityBand.width, facilityBand.height*16/9)`, `designX=facilityBand.x+(facilityBand.width-designWidth)/2`. 시설 rect는 `center=(designX+anchorX*designWidth, facilityBand.y+anchorY*facilityBand.height)`, `size=(width*designWidth,height*facilityBand.height)`다. HitTarget size는 각 축 `max(rootSize,64)`이고 root center를 유지한다. Safe Area clipping은 하지 않으며 16:9/18:9/20:9에서 design frame은 중앙 정렬된다.

## 7. CR-07 — invariant/bootstrap error registry와 localization

**상태: CORRECTION / CONFIRMED**

| Code | Trigger | UserMessageKey | Retryable | LoggingLevel | SaveMutationAllowed |
|---|---|---|---|---|---|
| CSV_RUNTIME_BOOLEAN_LEXICAL_INVALID | BOOLEAN value가 TRUE/FALSE가 아니거나 min/max nonempty | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_MANIFEST_SCHEMA_SET_UNSUPPORTED | manifest csvSchemaSetVersion에 대응 compiled schema 없음 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID | construction 8×4 coverage/PK 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_CONSTRUCTION_FK_INVALID | construction composite facility level FK 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_CONSTRUCTION_LEVEL_INVALID | construction level 1..4 위반 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_DURATION_INVALID | duration 1..86400 위반 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_CANCEL_RATIO_INVALID | P04 cancel_refund_ratio가 0이 아님 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_ASSET_DUPLICATE | asset_id/address duplicate | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_ASSET_REGISTRY_INVALID | asset_register HARD FK 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_ASSET_FACILITY_INVALID | facility_id HARD FK/tag 조건 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_ASSET_COVERAGE_INVALID | 8 facility PREFAB/BASE coverage 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_ROLE_COVERAGE_INVALID | 5 scene/state role coverage 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_DIMENSION_INVALID | asset dimensions 1..4096 위반 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_PIVOT_INVALID | pivot finite 0..1 위반 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CSV_FACILITY_WORLD_PPU_INVALID | pixels_per_unit !=100 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_ACTIVE_VERSION_INVALID | active version/path segment validation 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_ACTIVE_PACKAGE_NOT_FOUND | pinned package/manifest/schema 없음 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_ACTIVE_MANIFEST_VERSION_MISMATCH | provider/directory/manifest version 불일치 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_ACTIVE_PACKAGE_VALIDATION_FAILED | active package schema/hash/row/FK 실패 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| CONTENT_RELEASE_OVERRIDE_FORBIDDEN | release build에 development override 포함 | TXT_P04_UI_ERROR | FALSE | ERROR | FALSE |
| SAVE_FACILITY_SET_INVALID | 8종 누락/중복/추가 또는 level 범위 실패 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| SAVE_FACILITY_NPC_FORBIDDEN | SYSTEM assigned NPC 또는 working link | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| SAVE_FACILITY_STOPPED_INVALID | STOPPED mode/job/NPC 조건 불일치 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| SAVE_FACILITY_TERMINAL_JOURNAL_MISMATCH | CLAIMED↔COMMITTED 또는 CANCELLED↔FAILED_PERMANENT 불일치 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| SAVE_FACILITY_STATE_DERIVATION_MISMATCH | stored state와 deterministic projection 불일치 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| FACILITY_CLOCK_INVALID | started/finishes/claimed/cancelled timestamp ordering 위반 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| P04_BOOTSTRAP_SIGNATURE_MISMATCH | CanApply=false인데 Apply 호출 또는 forced golden signature 불일치 | TXT_P04_ERROR_SAVE_LOAD | FALSE | ERROR | FALSE |
| P04_BOOTSTRAP_UUID_FAILED | save/profile/NPC UUIDv7 provider 실패 | TXT_P04_ERROR_SAVE_CREATE | TRUE | ERROR | FALSE |
| P04_BOOTSTRAP_VALIDATION_FAILED | draft/after payload strict 또는 invariant 실패 | TXT_P04_ERROR_SAVE_CREATE | FALSE | ERROR | FALSE |
| P04_BOOTSTRAP_SAVE_FAILED | staging Save/flush/rename/parent flush 실패 | TXT_P04_ERROR_SAVE_CREATE | TRUE | ERROR | FALSE |
| SAVE_PROFILE_AMBIGUOUS | strict valid canonical profile directory 2개 이상 | TXT_P04_ERROR_PROFILE_AMBIGUOUS | FALSE | ERROR | FALSE |
| SAVE_PROFILE_DISCOVERY_FAILED | saves root 열거/권한/IO 실패 | TXT_P04_ERROR_PROFILE_DISCOVERY | TRUE | ERROR | FALSE |
| SAVE_PROFILE_LINK_FORBIDDEN | candidate가 symlink/reparse point | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE | WARN | FALSE |
| SAVE_PROFILE_DIRECTORY_NAME_INVALID | candidate 이름이 canonical UUIDv7 아님 | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE | WARN | FALSE |
| SAVE_PROFILE_NO_SAVE_CANDIDATE | canonical dir 안에 active/tmp/bak 후보 0개 | TXT_P04_ERROR_PROFILE_DISCOVERY | FALSE | WARN | FALSE |
`P04_BOOTSTRAP_SIGNATURE_MISMATCH`는 normal `CanApply=false`에서 사용자 오류로 발행하지 않는다. migration registry가 false 결과를 무시하고 Apply를 강제 호출했거나 golden test가 exact signature를 요구할 때만 발생한다. invariant/profile discovery 실패는 원본 Save나 directory를 수정하지 않는다.

### 7.1 신규 localization 4 rows

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P04_ERROR_PROFILE_AMBIGUOUS,Multiple local kingdoms were found and cannot be selected automatically.,P04_PROFILE,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_PROFILE_DISCOVERY,The local kingdom save location could not be inspected.,P04_PROFILE,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_PROFILE_AMBIGUOUS,여러 로컬 왕국이 발견되어 자동으로 선택할 수 없습니다.,P04_PROFILE,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_PROFILE_DISCOVERY,로컬 왕국 저장소를 확인하지 못했습니다.,P04_PROFILE,CONFIRMED,TRUE
```
| 항목 | 최종값 |
|---|---|
| 기존 v1.0 rows | 608 |
| 신규 rows | 4 |
| 최종 rowCount | 612 |
| localizations.csv SHA-256 | 6767d07492a4ece8abf3d6d8b19e1b84056564a6632a619cd4118751001956f6 |
| manifest JCS SHA-256 | 73e493ca508b5af96435a2cc1ad72d6c33ffdeae2f594863d9d3ee0dc302fd07 |
## 8. 변경 hash·golden 종합

**상태: CONFIRMED**

| Artifact | v1.0.1 row/byte count | SHA-256 | 판정 |
|---|---|---|---|
| runtime_config.csv | 18 rows | 610576da67809118f8dd87f4526be69682c5657de0852baa3fd8b3efc28efd25 | CHANGED |
| localizations.csv | 612 rows | 6767d07492a4ece8abf3d6d8b19e1b84056564a6632a619cd4118751001956f6 | CHANGED |
| facility_construction_rules.csv | 32 rows | a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5 | UNCHANGED |
| facility_world_assets.csv | 13 rows | d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f | UNCHANGED |
| asset_register.csv | 18 rows | f44f551c6929a08cbb781e71cbadc24f37dbcc721725843b3729498e4c6fd149 | UNCHANGED |
| content_manifest.json | 67264 RFC8785 bytes | 73e493ca508b5af96435a2cc1ad72d6c33ffdeae2f594863d9d3ee0dc302fd07 | CHANGED |
| content_manifest.schema.json | 2724 RFC8785 bytes | 451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41 | NEW SET-3 GOLDEN |
Save golden 3종, 시설 전이, duration/world asset/asset register bytes는 요청서 1장의 검증값을 그대로 유지한다. BOOLEAN/localization/manifest 외 CSV는 재생성하지 않는다.

## 9. v1.0.1 수용 checklist

**상태: CONFIRMED**

- [ ] BOOLEAN value=FALSE, min/max empty와 runtime CRLF hash 일치
- [ ] 62-table manifest full JSON/JCS byte length/hash 일치
- [ ] schema set 3 full JSON과 compiled schema golden 일치
- [ ] 신규 두 descriptor fields/enum/FK 배열 및 semantic validator 일치
- [ ] flat P03 package를 immutable .1로 git move하고 .2를 별도 생성
- [ ] release active content constant가 .2이며 fallback/latest/override 0개
- [ ] single profile locator의 0/1/N, tmp/bak recovery, link 방어 test 통과
- [ ] request factory가 5 command의 UUID/hash를 만들고 handler는 검증만 수행
- [ ] Build/Upgrade/Claim journal 및 Assign/Unassign desired-state retry 구현
- [ ] 8 prefab이 uGUI exact hierarchy이고 SpriteRenderer component 0개
- [ ] 6 state visual matrix, Safe Area/touch formula, 16:9~20:9 test 통과
- [ ] 누락 invariant/bootstrap/profile errors가 registry/localization과 일치
- [ ] EditMode/PlayMode/content/build guard 통과 후 Android APK 생성

## 10. UNRESOLVED

**상태: CONFIRMED**

`NONE`

이 부록과 v1.0을 합친 계약에는 P04 구현자가 선택해야 할 CR-01..CR-07 결정이 남아 있지 않다.
