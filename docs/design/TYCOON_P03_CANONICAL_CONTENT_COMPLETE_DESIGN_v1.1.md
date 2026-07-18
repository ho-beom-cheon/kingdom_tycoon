# Kingdom Tycoon P03 Canonical Content 최종 통합 설계서 v1.1

> 상태: FINAL IMPLEMENTATION CONTRACT / UNRESOLVED NONE
> 기준일: 2026-07-18 / SOURCE_DATE_EPOCH=1784332800
> 출력: `TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md`
>
> 저장소 적용 정정(2026-07-19): exact CSV와 tagged-union 의미에 맞춰
> `monsters.type` enum에 `RAID`를 추가하고 `runtime_config.value` domain을
> `STRING`으로 정정했다. CSV row와 table SHA-256은 변경되지 않는다.

## 1. 문서 권위와 supersedes

### Decision

이 문서는 P03 canonical content 구현에 한해 `TYCOON_P03_CANONICAL_CONTENT_DATA_DESIGN_v1.0.md`, v1.0.1 정정 요청, Save/CSV v1.1·v1.2·v1.2.1·v1.2.2의 관련 문구를 병합·대체하는 단일 권위다. 저장소의 최신 CONFIRMED 게임 설계가 이 문서보다 우선한다.

### Reason

60-file package를 clean checkout에서 재현하려면 schema, row, manifest, migration, localization, golden과 Phase 경계가 한 곳에 있어야 한다.

### Compatibility

출시 Save와 공개 content release가 없으므로 content data migration은 one-way authoring migration이며 player Save migration은 발생하지 않는다. v1.0의 검증된 18개 block row와 두 RNG golden 값은 유지된다.

### Exact output

이 문서의 60개 CSV block과 manifest v2 schema가 유일한 구현 입력이다. `UNRESOLVED`는 `NONE`이다.

## 2. 최종 결정 요약

### Decision

| 항목 | 최종값 |
|---|---|
| manifest | contractVersion=2, schemaId=urn:tycoon:content-manifest:v2 |
| content | contentVersion=1.0.0-content.1, csvSchemaSetVersion=2, BASE/DEV |
| files | 60 required, 각 file schemaVersion=1 |
| trait | STAT/COMBAT/COLLECTION/BOSS/RESIST/AI 1:1 |
| equipment source | CRAFT→CRAFT, BOSS_CRAFT→BOSS; BOSS boss_id는 raids FK |
| region unlock | 5 condition groups; R02=TUT_09 completed로 legacy cycle 해소 |
| currency | 4 rows, SafeInt max 9007199254740991 |
| rate-up | 두 required file header-only; enabled reference 0 |
| autonomy | 17 state, 37 exact rules |
| raid difficulty | 6 rows, repeat reward group 6개 |
| unresolved | NONE |

### Reason

pre-release contract 1을 억지로 확장하지 않고 v2로 명시해 strict parser의 추측을 제거한다. 데이터 row의 ID와 검증 완료 수치는 유지한다.

### Compatibility

contract 1 importer는 v2 package를 거부해야 하며 P03에서 importer/schema를 함께 교체한다. file schemaVersion은 첫 공개 baseline이므로 1이며, trait/source/header 정정의 비호환성은 contractVersion=2와 csvSchemaSetVersion=2가 표현한다.

### Exact output

아래 모든 block이 exact output이다. hash와 rowCount는 해당 block을 canonical writer로 직렬화해 생성한 값이다.

## 3. P03/LATER Phase 범위 matrix

### Decision

| 항목 | 분류 | target Phase | P03 산출물 |
|---|---|---|---|
| one-way migrator, writer, importer, schema/semantic validator | P03_IMPLEMENT | P03 | 60-file package와 오류 코드 |
| immutable catalog·Save/contentVersion compatibility | P03_IMPLEMENT | P03 | future/split-brain 차단 |
| SplitMix64·pity·equipment·offline/tutorial idempotency algorithm | P03_FIXTURE_ONLY | P03 | DTO/순수 함수/golden만 |
| 실제 모집 비용 차감·pity transaction·용병 Instance 추가 | LATER_PHASE | P13_RECRUITMENT | P03 fixture를 소비 |
| tutorial step action·skip grant 원자 적용 | LATER_PHASE | P15_OFFLINE_TUTORIAL | P03 DAG/grant validator까지만 |
| offline 6 type simulation·reward 지급 | LATER_PHASE | P15_OFFLINE_TUTORIAL | P03 line identity fixture까지만 |
| random equipment reward journal/snapshot inventory 적용 | LATER_PHASE | P07_LOOT_INVENTORY_EQUIPMENT | P03 snapshot pure fixture까지만 |
| facility build/craft/store transaction | LATER_PHASE | P04/P08/P09/P10 | catalog DTO만 P03 |
| raid part/first-clear/difficulty reward 지급 | LATER_PHASE | P14_RAIDS | FK·reward fixture만 P03 |
| placeholder prefab·Addressables·package 검증 | P03_IMPLEMENT | P03 | Editor API 생성 및 build fail-closed |
| Android/승인 player build | P03_IMPLEMENT | P03 | package/address 존재 검증 포함 |

### Reason

P03은 콘텐츠·Save 기반을 완결하되 gameplay orchestration을 선행 구현하지 않는다.

### Compatibility

후속 Phase는 이 문서의 immutable DTO, ID, golden을 소비하며 의미 변경 시 새 contentVersion을 만든다.

### Exact output

표의 분류가 구현 scope gate다.

## 4. Manifest contract version 결정

### Decision

`contractVersion=2`, `schemaId=urn:tycoon:content-manifest:v2`, JSON Schema `$id=urn:tycoon:content-manifest-schema:v2`다. BASE도 `baseContentVersion` property를 반드시 가지며 값은 JSON null이다. tables는 file UTF-8 ordinal ascending이어야 하고 importer는 정렬해 수용하지 않고 거부한다.

### Reason

contract 1의 5/6 property strict object와 새 release metadata를 동시에 만족할 수 없다.

### Compatibility

| 입력 | 처리 |
|---|---|
| pre-release contract 1 fixture | 폐기; 자동 upgrade 금지 |
| contract 2 + csvSchemaSetVersion 2 | 지원 |
| contractVersion>2 | CONTENT_MANIFEST_CONTRACT_VERSION_UNSUPPORTED |
| BASE baseContentVersion non-null | CONTENT_MANIFEST_BASE_VERSION_INVALID |
| tables 비정렬 | CONTENT_MANIFEST_TABLE_ORDER_INVALID |
| 같은 contentVersion, 다른 manifest digest | CONTENT_RELEASE_SPLIT_BRAIN |

### Exact output

manifest full fixture는 6장의 schema registry에서 생성한 60 descriptor를 포함한다. manifest JCS SHA-256은 local immutable catalog metadata와 DB `master.content_release.checksum`에 저장한다. 자기 자신 안에는 넣지 않는다. CI artifact metadata도 같은 값을 기록한다. Save.contentVersion과 exact package가 설치됐을 때만 load하며, 더 오래된 Save는 alias chain이 모두 닫힐 때만 단방향 변환한다. 설치 최고 version보다 미래인 Save는 `SAVE_CONTENT_FUTURE_UNSUPPORTED`, hard FK 하나라도 해석되지 않으면 `SAVE_CONTENT_HARD_FK_UNRESOLVED`로 원본을 보존하고 쓰기를 차단한다.

## 5. 완전한 manifest JSON Schema

### Decision

unknown property는 root와 모든 nested object에서 금지한다.

### Reason

strict parser와 generator가 동일 계약을 사용해야 한다.

### Compatibility

camelCase만 허용하며 snake_case JSON property는 unknown-field 오류다.

### Exact output

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
      "const": 2
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

### 5.1 60-table valid fixture

아래 sha256/rowCount는 이 문서의 full CSV row를 canonical byte 계약으로 직렬화해 계산한 값이다.

```json
{
  "schemaId": "urn:tycoon:content-manifest:v2",
  "contractVersion": 2,
  "contentVersion": "1.0.0-content.1",
  "csvSchemaSetVersion": 2,
  "packageKind": "BASE",
  "baseContentVersion": null,
  "minimumGameVersion": "1.0.0",
  "channel": "DEV",
  "generatedAtUtc": "2026-07-18T00:00:00.000Z",
  "tables": [
    {
      "file": "asset_register.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "1f7d1823ef6df68d21228f4158d3ebc7b695e077a74560b587c775f4d2972e08",
      "rowCount": 5,
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
      "sha256": "ab5b6840ba88f9ffb2444d7f5f4761c09cba558cd116409e01b184da73c16a5b",
      "rowCount": 470,
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
      "sha256": "dcf81d9e689d0799ebdf5a3ef8ada3cf70aac398743cd0eba91714c2a032da98",
      "rowCount": 14,
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

### 5.2 invalid fixture contract

| 변형 | 오류 |
|---|---|
| unknown root/table/field/FK property | CONTENT_MANIFEST_UNKNOWN_FIELD |
| required property 누락 | CONTENT_MANIFEST_REQUIRED_FIELD_MISSING |
| tables file 순서 교환 | CONTENT_MANIFEST_TABLE_ORDER_INVALID |
| contractVersion=1 또는 3 | CONTENT_MANIFEST_CONTRACT_VERSION_UNSUPPORTED |
| BASE에 baseContentVersion 문자열 | CONTENT_MANIFEST_BASE_VERSION_INVALID |
| sha256 uppercase/길이 오류 | CONTENT_MANIFEST_HASH_FORMAT_INVALID |

## 6. Field domain vocabulary

### Decision

| domain | lexical | range/normalization | 적용 |
|---|---|---|---|
| STRING | UTF-8 scalar string | non-null 1..4096 UTF-8 bytes; CR/LF only text_value; NUL 금지 | 표시 문자열·다형 value |
| STABLE_ID | ^[A-Z][A-Z0-9_]{0,63}$ | 1..64 bytes ASCII | ID·key·code |
| BOOL | TRUE\|FALSE | uppercase only | CSV bool |
| INT32 | 0\|-?[1-9][0-9]* | -2147483648..2147483647 | 순번·tier·level |
| INT64 | 0\|-?[1-9][0-9]* | -9223372036854775808..9223372036854775807 | DB 전용 signed 64-bit |
| SAFE_INT | 0\|-?[1-9][0-9]* | -9007199254740991..9007199254740991 | Save 왕복 수량·재화·전투 정수 |
| DECIMAL | -?(0\|[1-9][0-9]*)(\.[0-9]+)? | finite plain decimal; exponent/leading zero 금지; source의 1.0 유지 | 비율·계수·초 |
| UTC_INSTANT | ^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$ | 실제 UTC 날짜 | 기간 |
| DATE | ^\d{4}-\d{2}-\d{2}$ | 실제 Gregorian 날짜 | 취득일 |
| LOCALE | ^[a-z]{2}-[A-Z]{2}$ | 현재 ko-KR | localization/name pool |
| SEMVER | ^(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?$ | SemVer 2 core/pre-release | minimumGameVersion |
| HEX64 | ^[0-9a-f]{64}$ | lowercase | SHA-256 |
| SEED64 | ^(0\|[1-9][0-9]{0,19})$ | decimal string 0..18446744073709551615 | growth_seed_min/max |
| STATUS | CONFIRMED\|TUNABLE\|DEFERRED\|OPS_LATER\|REFERENCE_ONLY\|REJECTED\|UNRESOLVED\|TEMPLATE | 후반 6 status는 enabled=FALSE | 모든 row |
| ENUM | descriptor.enumValues 중 정확히 하나 | case-sensitive; ordinal 의미 없음 | 폐쇄 enum |

`uint64str→SEED64`, `locale→LOCALE`, `date→DATE`, `semver→SEMVER`, `hex64→HEX64`, `ratio→DECIMAL+0..1 semantic`, `int64→INT64`로 고정한다. Save와 왕복하는 max_balance/quantity는 SAFE_INT다. CSV decimal `1.0`은 허용·보존하며 `1`로 강제 축약하지 않는다.

### Reason

`growth_seed_max`는 SAFE_INT가 아니며 문자열 Seed64로만 무손실 표현된다.

### Compatibility

manifest contract 1의 9-domain parser는 v2에서 교체한다. semantic validator가 domain 검사 뒤 ratio/range/tagged-union을 검사한다.

### Exact output

모든 status/enable 조합은 공통 규칙을 따른다. `DEFERRED|OPS_LATER|REFERENCE_ONLY|REJECTED|UNRESOLVED|TEMPLATE`이면 enabled는 FALSE다.

## 7. 60-table schema registry

### Decision

아래 registry가 manifest descriptor의 단일 원본이다. `!`는 non-null, `?`는 nullable이다. 모든 file은 schemaVersion=1, required=TRUE이며 status/enabled가 마지막 두 field다.

### Reason

JSON manifest schema를 손으로 별도 유지하면 drift가 생긴다.

### Compatibility

header/domain/FK 변경은 해당 file schemaVersion과 csvSchemaSetVersion을 올리고 새 contentVersion을 만든다. data row 값만 바꾸면 file schemaVersion은 유지한다.

### Exact output

| file | ver | required | PK | fields/domain/null | enum values | lexical/range | FK | tagged union | semantic validator |
|---|---|---|---|---|---|---|---|---|---|
| asset_register.csv | 1 | TRUE | asset_id | asset_id:STABLE_ID!; name:STRING!; creator:STRING?; source_url:STRING?; version:STRING?; acquired_date:DATE?; price_krw:SAFE_INT!; license:STRING?; commercial_use:BOOL?; modification_allowed:BOOL?; credit_required:BOOL?; used_in:STRING?; notes:STRING?; status:STATUS!; enabled:BOOL! | — | name=1..4096 UTF-8 bytes | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| autonomy_rules.csv | 1 | TRUE | state+rule_no | state:ENUM!; rule_no:INT32!; priority:INT32!; condition_type:STABLE_ID!; condition_value:DECIMAL?; reason_code:ENUM!; next_state:ENUM!; status:STATUS!; enabled:BOOL! | state=IDLE_TOWN\|PREPARE\|TRAVEL_TO_REGION\|FIND_TARGET\|COMBAT\|LOOT\|CONTINUE_DECISION\|RETURN_TOWN\|SELL_LOOT\|HEAL\|BUY_CONSUMABLES\|EVALUATE_EQUIPMENT\|BUY_EQUIPMENT\|PROMOTION_READY\|PROMOTION_PROCESS\|INJURED\|RAID_READY; reason_code=NONE\|POLICY\|HP_LOW\|POTION_LOW\|INVENTORY_FULL\|SURVIVAL_LOW\|PLAYER_RECALL\|TARGET_FOUND\|LOOT_COMPLETE\|PROMOTION_AVAILABLE; next_state=IDLE_TOWN\|PREPARE\|TRAVEL_TO_REGION\|FIND_TARGET\|COMBAT\|LOOT\|CONTINUE_DECISION\|RETURN_TOWN\|SELL_LOOT\|HEAL\|BUY_CONSUMABLES\|EVALUATE_EQUIPMENT\|BUY_EQUIPMENT\|PROMOTION_READY\|PROMOTION_PROCESS\|INJURED\|RAID_READY | rule_no>=1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_AUTONOMY_RULE_INVALID |
| condition_group_members.csv | 1 | TRUE | condition_group_id+member_no | condition_group_id:STABLE_ID!; member_no:INT32!; member_type:ENUM!; condition_id:STABLE_ID?; child_group_id:STABLE_ID?; negate:BOOL!; status:STATUS!; enabled:BOOL! | member_type=CONDITION\|GROUP | member_no>=1 | condition_group_id→condition_groups.csv(condition_group_id)/HARD; condition_id→conditions.csv(condition_id)/HARD; child_group_id→condition_groups.csv(condition_group_id)/HARD | member_type CONDITION/GROUP null matrix | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_CONDITION_GROUP_INVALID |
| condition_groups.csv | 1 | TRUE | condition_group_id | condition_group_id:STABLE_ID!; logic:ENUM!; description_key:STABLE_ID?; status:STATUS!; enabled:BOOL! | logic=ALL\|ANY | §6 domain range | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_CONDITION_GROUP_INVALID |
| conditions.csv | 1 | TRUE | condition_id | condition_id:STABLE_ID!; condition_type:ENUM!; subject_type:ENUM!; subject_id:STABLE_ID?; subject_sub_id:STABLE_ID?; operator:ENUM!; value_type:ENUM!; expected_value:STRING!; status:STATUS!; enabled:BOOL! | condition_type=ALWAYS_TRUE\|FACILITY_UPGRADE_COUNT\|FACILITY_LEVEL\|REGION_UNLOCKED\|REGION_PROGRESS_PERCENT\|MONSTER_KILL_COUNT\|MERCENARY_COUNT_AT_RANK_OR_HIGHER\|RAID_CLEAR_COUNT\|KINGDOM_STAGE_REACHED\|RAID_PART_BROKEN\|RAID_PART_EXPOSED\|TUTORIAL_STEP_COMPLETED; subject_type=SYSTEM\|KINGDOM\|FACILITY\|REGION\|MONSTER\|MERCENARY_ROSTER\|RAID\|KINGDOM_STAGE\|RAID_PART\|TUTORIAL_STEP; operator=EQ\|NE\|GTE\|LTE\|GT\|LT; value_type=BOOLEAN\|INTEGER\|DECIMAL\|STABLE_ID | expected_value=1..4096 UTF-8 bytes | — | condition_type×subject/value matrix | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_CONDITION_UNION_INVALID |
| content_aliases.csv | 1 | TRUE | entity_type+from_id+from_content_version | entity_type:ENUM!; from_id:STABLE_ID!; from_content_version:STRING!; alias_type:ENUM!; to_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | entity_type=JOB\|SKILL\|TRAIT\|PERSONALITY\|MERCENARY_GRADE\|MERCENARY_RANK\|MERCENARY_GENERATION_PROFILE\|ITEM\|POTION\|EQUIPMENT_TEMPLATE\|EQUIPMENT_QUALITY\|REFINE_OPTION\|FACILITY\|NPC_PROFESSION\|NPC_PROFICIENCY\|KINGDOM_STAGE\|REGION\|MONSTER\|RAID\|RAID_PART\|RECIPE\|REWARD_GROUP\|TUTORIAL_STEP\|STATUS_EFFECT\|RECRUITMENT_POOL\|PITY_GROUP\|PITY_RULE\|RATE_UP_GROUP\|CONDITION_GROUP\|PROGRESSION_FLAG\|RUNTIME_CONFIG; alias_type=RENAME\|MERGE\|TOMBSTONE | from_content_version=1..4096 UTF-8 bytes | — | alias_type×to_id; entity registry dynamic FK | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_CONTENT_ALIAS_INVALID |
| currencies.csv | 1 | TRUE | currency_id | currency_id:STABLE_ID!; currency_type:ENUM!; authority:ENUM!; max_balance:SAFE_INT!; name_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | currency_type=KINGDOM\|PREMIUM_FREE\|PREMIUM_PAID\|TICKET; authority=LOCAL\|SERVER | max_balance>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| enhancement_rules.csv | 1 | TRUE | target_level | target_level:INT32!; success_chance:DECIMAL!; stone_item_id:STABLE_ID!; stone_quantity:SAFE_INT!; personal_gold_cost:SAFE_INT!; fail_pity_increment:DECIMAL!; destroy_on_fail:BOOL!; downrank_on_fail:BOOL!; status:STATUS!; enabled:BOOL! | — | target_level=1..10; success_chance=0..1; stone_quantity>=0; quantity/weight context는 >=1; personal_gold_cost>=0; quantity/weight context는 >=1 | stone_item_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| equipment_job_eligibility.csv | 1 | TRUE | equipment_template_id+job_id | equipment_template_id:STABLE_ID!; job_id:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | §6 domain range | equipment_template_id→equipment_templates.csv(equipment_template_id)/HARD; job_id→jobs.csv(job_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| equipment_qualities.csv | 1 | TRUE | quality_id | quality_id:STABLE_ID!; name_text_key:STABLE_ID!; order:INT32!; stat_multiplier:DECIMAL!; innate_affix_chance:DECIMAL!; ui_token:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | order>=1; innate_affix_chance=0..1 | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| equipment_quality_weights.csv | 1 | TRUE | quality_profile_id+quality_id | quality_profile_id:STABLE_ID!; quality_id:STABLE_ID!; weight:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | weight>=0; quantity/weight context는 >=1 | quality_id→equipment_qualities.csv(quality_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_RANDOM_EQUIPMENT_SPEC_INVALID |
| equipment_templates.csv | 1 | TRUE | equipment_template_id | equipment_template_id:STABLE_ID!; name_text_key:STABLE_ID!; tier:INT32!; slot:ENUM!; profile:STABLE_ID!; base_power:SAFE_INT!; source:ENUM!; boss_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | slot=WEAPON\|ARMOR\|HELMET\|ACCESSORY; source=CRAFT\|DROP\|BOSS\|RAID | tier=1..5 | name_text_key→localizations.csv(text_key)/HARD; boss_id→raids.csv(raid_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_EQUIPMENT_SOURCE_INVALID |
| facilities.csv | 1 | TRUE | facility_id | facility_id:STABLE_ID!; name_text_key:STABLE_ID!; operation_mode:ENUM!; required_profession_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | operation_mode=SYSTEM\|MANAGED | §6 domain range | name_text_key→localizations.csv(text_key)/HARD; required_profession_id→npc_professions.csv(profession_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| facility_levels.csv | 1 | TRUE | facility_id+level | facility_id:STABLE_ID!; level:INT32!; required_kingdom_stage_id:STABLE_ID!; build_or_upgrade_kingdom_gold:SAFE_INT!; effect_key:STABLE_ID!; effect_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | level=1..4; build_or_upgrade_kingdom_gold>=0; quantity/weight context는 >=1 | effect_text_key→localizations.csv(text_key)/HARD; facility_id→facilities.csv(facility_id)/HARD; required_kingdom_stage_id→kingdom_stages.csv(stage_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| facility_upgrade_materials.csv | 1 | TRUE | facility_id+level+item_id | facility_id:STABLE_ID!; level:INT32!; item_id:STABLE_ID!; quantity:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | level=1..4; quantity>=0; quantity/weight context는 >=1 | facility_id+level→facility_levels.csv(facility_id+level)/HARD; item_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| items.csv | 1 | TRUE | item_id | item_id:STABLE_ID!; name_text_key:STABLE_ID!; category:ENUM!; tier:INT32!; source_type:ENUM!; source_id:STABLE_ID?; sell_price:SAFE_INT!; rarity:STABLE_ID!; stack_limit:SAFE_INT!; status:STATUS!; enabled:BOOL! | category=ALCHEMY\|BOSS\|CRAFT\|ENHANCE\|MAGIC\|MONSTER\|ORE\|PROMOTION\|REFINE\|RELIC; source_type=REGION\|RAID\|DISMANTLE\|ELITE_AND_RAID\|PROMOTION_CONTENT | tier=1..5; sell_price>=0; quantity/weight context는 >=1; stack_limit>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD | source_type×source_id dynamic FK | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_ITEM_SOURCE_INVALID |
| job_skill_unlocks.csv | 1 | TRUE | job_id+skill_id | job_id:STABLE_ID!; skill_id:STABLE_ID!; slot_no:INT32!; unlock_rank_id:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | slot_no>=1 | job_id→jobs.csv(job_id)/HARD; skill_id→skills.csv(skill_id)/HARD; unlock_rank_id→mercenary_ranks.csv(rank_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| jobs.csv | 1 | TRUE | job_id | job_id:STABLE_ID!; name_text_key:STABLE_ID!; role:ENUM!; armor_profile:ENUM!; weapon_type:STABLE_ID!; primary_stat:STABLE_ID!; secondary_stat:STABLE_ID!; ai_priority:STABLE_ID!; notes_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | role=BRUISER\|TANK\|RANGED_DPS\|AOE_DPS\|HEALER; armor_profile=HEAVY\|LIGHT\|CLOTH | §6 domain range | name_text_key→localizations.csv(text_key)/HARD; notes_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| kingdom_stages.csv | 1 | TRUE | stage_id | stage_id:STABLE_ID!; name_text_key:STABLE_ID!; order:INT32!; active_slots:INT32!; roster_slots:INT32!; facility_level_cap:INT32!; unlock_condition_group_id:STABLE_ID!; final_raid_unlocked:BOOL!; status:STATUS!; enabled:BOOL! | — | order>=1 | name_text_key→localizations.csv(text_key)/HARD; unlock_condition_group_id→condition_groups.csv(condition_group_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| localizations.csv | 1 | TRUE | locale+text_key | locale:LOCALE!; text_key:STABLE_ID!; text_value:STRING!; context:STRING?; status:STATUS!; enabled:BOOL! | — | text_value=1..4096 UTF-8 bytes | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_LOCALIZATION_KEY_COLLISION |
| loot_entries.csv | 1 | TRUE | loot_table_id+entry_no | loot_table_id:STABLE_ID!; entry_no:INT32!; reward_type:ENUM!; reward_id:STABLE_ID!; probability:DECIMAL?; weight:SAFE_INT?; min_quantity:SAFE_INT!; max_quantity:SAFE_INT!; condition_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | reward_type=ITEM\|POTION\|CURRENCY\|PERSONAL_GOLD\|EQUIPMENT_TEMPLATE\|RANDOM_EQUIPMENT_TIER\|PLAYER_EXP\|KINGDOM_EXP | entry_no>=1; probability=0..1; weight>=0; quantity/weight context는 >=1; min_quantity>=0; quantity/weight context는 >=1; max_quantity>=0; quantity/weight context는 >=1 | loot_table_id→loot_tables.csv(loot_table_id)/HARD; condition_id→conditions.csv(condition_id)/HARD | reward_type×reward_id/probability/weight | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_REWARD_DISCRIMINATOR_MISMATCH |
| loot_tables.csv | 1 | TRUE | loot_table_id | loot_table_id:STABLE_ID!; draw_mode:ENUM!; draw_count:INT32!; status:STATUS!; enabled:BOOL! | draw_mode=INDEPENDENT\|ONE_WEIGHTED\|N_WEIGHTED | draw_count>=1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| mercenary_appearance_pool_entries.csv | 1 | TRUE | appearance_pool_id+job_id+entry_no | appearance_pool_id:STABLE_ID!; job_id:STABLE_ID!; entry_no:INT32!; appearance_asset_id:STABLE_ID!; weight:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | entry_no>=1; weight>=0; quantity/weight context는 >=1 | job_id→jobs.csv(job_id)/HARD; appearance_asset_id→asset_register.csv(asset_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| mercenary_generation_profiles.csv | 1 | TRUE | generation_profile_id | generation_profile_id:STABLE_ID!; algorithm_id:ENUM!; initial_rank_id:STABLE_ID!; name_pool_id:STABLE_ID!; appearance_pool_id:STABLE_ID!; personality_policy:ENUM!; fixed_personality_id:STABLE_ID?; growth_seed_min:SEED64!; growth_seed_max:SEED64!; status:STATUS!; enabled:BOOL! | algorithm_id=GENERATED_MERCENARY_V1; personality_policy=WEIGHTED_ALL\|FIXED | growth_seed_min=0..18446744073709551615; growth_seed_max=0..18446744073709551615 | initial_rank_id→mercenary_ranks.csv(rank_id)/HARD; fixed_personality_id→personalities.csv(personality_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| mercenary_grades.csv | 1 | TRUE | grade_id | grade_id:ENUM!; name_text_key:STABLE_ID!; order:INT32!; base_stat_multiplier:DECIMAL!; growth_multiplier:DECIMAL!; initial_trait_count:INT32!; promotion_cost_multiplier:DECIMAL!; tavern_eligible:BOOL!; special_pool_eligible:BOOL!; ui_color:STRING!; status:STATUS!; enabled:BOOL! | grade_id=GRADE_C\|GRADE_B\|GRADE_A\|GRADE_S\|GRADE_SS | order>=1; ui_color=1..4096 UTF-8 bytes | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| mercenary_name_pool_entries.csv | 1 | TRUE | name_pool_id+locale+entry_no | name_pool_id:STABLE_ID!; locale:LOCALE!; entry_no:INT32!; display_name:STRING!; weight:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | entry_no>=1; display_name=1..4096 UTF-8 bytes; weight>=0; quantity/weight context는 >=1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| mercenary_ranks.csv | 1 | TRUE | rank_id | rank_id:ENUM!; name_text_key:STABLE_ID!; order:INT32!; max_level:INT32!; min_region_tier:INT32!; skill_slot_count:INT32!; trait_slot_bonus:INT32!; raid_access:BOOL!; promotion_to:STRING?; promotion_token_id:STABLE_ID?; base_personal_gold:SAFE_INT!; contribution_required:SAFE_INT!; additional_condition:STABLE_ID!; status:STATUS!; enabled:BOOL! | rank_id=RANK_APPRENTICE\|RANK_REGULAR\|RANK_SKILLED\|RANK_ELITE\|RANK_HERO\|RANK_LEGEND | order>=1; base_personal_gold>=0; quantity/weight context는 >=1; contribution_required>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; promotion_to→mercenary_ranks.csv(rank_id)/HARD; promotion_token_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| monsters.csv | 1 | TRUE | monster_id | monster_id:STABLE_ID!; name_text_key:STABLE_ID!; region_id:STABLE_ID!; type:ENUM!; level:INT32!; hp:SAFE_INT!; attack:SAFE_INT!; defense:SAFE_INT!; xp:SAFE_INT!; bounty_personal_gold:SAFE_INT!; loot_table_id:STABLE_ID!; behavior_tag:STABLE_ID!; spawn_weight:SAFE_INT!; raid_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | type=NORMAL\|ELITE\|BOSS\|RAID | level=1..4; hp>=0; quantity/weight context는 >=1; attack>=0; quantity/weight context는 >=1; defense>=0; quantity/weight context는 >=1; xp>=0; quantity/weight context는 >=1; bounty_personal_gold>=0; quantity/weight context는 >=1; spawn_weight>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; region_id→regions.csv(region_id)/HARD; loot_table_id→loot_tables.csv(loot_table_id)/HARD; raid_id→raids.csv(raid_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| npc_professions.csv | 1 | TRUE | profession_id | profession_id:STABLE_ID!; name_text_key:STABLE_ID!; facility_id:STABLE_ID!; work_unit:STRING!; primary_effect:STRING!; status:STATUS!; enabled:BOOL! | — | work_unit=1..4096 UTF-8 bytes; primary_effect=1..4096 UTF-8 bytes | name_text_key→localizations.csv(text_key)/HARD; facility_id→facilities.csv(facility_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| npc_proficiency_levels.csv | 1 | TRUE | proficiency_id | proficiency_id:STABLE_ID!; name_text_key:STABLE_ID!; order:INT32!; xp_required:SAFE_INT!; speed_multiplier:DECIMAL!; material_efficiency:DECIMAL!; quality_bonus:DECIMAL!; status:STATUS!; enabled:BOOL! | — | order>=1; xp_required>=0; quantity/weight context는 >=1; quality_bonus=0..1 | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| offline_reward_rules.csv | 1 | TRUE | rule_id | rule_id:STABLE_ID!; settlement_type:ENUM!; max_seconds:SAFE_INT!; efficiency:DECIMAL!; reward_group_id:STABLE_ID?; status:STATUS!; enabled:BOOL! | settlement_type=HUNT\|FACILITY\|NPC_PROFICIENCY\|POTION_CONSUMPTION\|INJURY_RECOVERY\|PROMOTION_REVIEW | max_seconds>=0; quantity/weight context는 >=1; efficiency=0..1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_OFFLINE_RULE_SET_INVALID |
| personalities.csv | 1 | TRUE | personality_id | personality_id:STABLE_ID!; name_text_key:STABLE_ID!; buy_threshold_multiplier:DECIMAL!; risk_tolerance:DECIMAL!; return_hp_threshold:DECIMAL!; preferred_behavior:STABLE_ID!; description_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | return_hp_threshold=0..1 | name_text_key→localizations.csv(text_key)/HARD; description_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| potions.csv | 1 | TRUE | potion_id | potion_id:STABLE_ID!; name_text_key:STABLE_ID!; effect_type:ENUM!; effect_value:DECIMAL!; duration_sec:DECIMAL!; auto_use_condition:STRING!; tier:INT32!; status:STATUS!; enabled:BOOL! | effect_type=HEAL_FLAT\|CURE_POISON\|POISON_RESIST\|FROST_RESIST\|BOSS_DAMAGE | auto_use_condition=1..4096 UTF-8 bytes; tier=1..5 | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| progression_flags.csv | 1 | TRUE | progression_flag_id | progression_flag_id:STABLE_ID!; scope:ENUM!; repeatable:BOOL!; status:STATUS!; enabled:BOOL! | scope=PROFILE\|KINGDOM | §6 domain range | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| promotion_grade_requirements.csv | 1 | TRUE | grade_id+from_rank_id+to_rank_id+item_id | grade_id:STABLE_ID!; from_rank_id:STABLE_ID!; to_rank_id:STABLE_ID!; item_id:STABLE_ID!; item_quantity:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | item_quantity>=0; quantity/weight context는 >=1 | grade_id→mercenary_grades.csv(grade_id)/HARD; from_rank_id→mercenary_ranks.csv(rank_id)/HARD; to_rank_id→mercenary_ranks.csv(rank_id)/HARD; item_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| raid_difficulties.csv | 1 | TRUE | raid_id+difficulty | raid_id:STABLE_ID!; difficulty:ENUM!; recommended_power:SAFE_INT!; reward_group_id:STABLE_ID!; status:STATUS!; enabled:BOOL! | difficulty=NORMAL\|HARD\|CORRUPTED | recommended_power>=0; quantity/weight context는 >=1 | raid_id→raids.csv(raid_id)/HARD; reward_group_id→reward_groups.csv(reward_group_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_RAID_DIFFICULTY_INVALID |
| raid_part_effects.csv | 1 | TRUE | effect_id | effect_id:STABLE_ID!; effect_type:ENUM!; effect_key:STABLE_ID!; value:DECIMAL?; status:STATUS!; enabled:BOOL! | effect_type=ABILITY_SCALE\|STAT_MODIFIER\|STATE_TRANSITION | §6 domain range | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| raid_parts.csv | 1 | TRUE | raid_id+part_id | raid_id:STABLE_ID!; part_id:STABLE_ID!; name_text_key:STABLE_ID!; max_hp_ratio:DECIMAL!; break_condition_group_id:STABLE_ID!; break_reward_group_id:STABLE_ID!; behavior_change:STRING!; priority_hint_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | max_hp_ratio=0..1; behavior_change=1..4096 UTF-8 bytes | name_text_key→localizations.csv(text_key)/HARD; priority_hint_text_key→localizations.csv(text_key)/HARD; raid_id→raids.csv(raid_id)/HARD; break_condition_group_id→condition_groups.csv(condition_group_id)/HARD; break_reward_group_id→reward_groups.csv(reward_group_id)/HARD; behavior_change→raid_part_effects.csv(effect_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| raids.csv | 1 | TRUE | raid_id | raid_id:STABLE_ID!; name_text_key:STABLE_ID!; boss_monster_id:STABLE_ID!; min_rank_id:STABLE_ID!; party_min:INT32!; party_max:INT32!; unlock_condition_group_id:STABLE_ID!; first_clear_reward_group_id:STABLE_ID!; time_limit_sec:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | time_limit_sec>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; boss_monster_id→monsters.csv(monster_id)/HARD; min_rank_id→mercenary_ranks.csv(rank_id)/HARD; unlock_condition_group_id→condition_groups.csv(condition_group_id)/HARD; first_clear_reward_group_id→reward_groups.csv(reward_group_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| random_equipment_tier_specs.csv | 1 | TRUE | spec_id | spec_id:STABLE_ID!; tier:INT32!; slot_policy:ENUM!; fixed_slot:STRING?; job_policy:ENUM!; fixed_job_id:STABLE_ID?; quality_profile_id:STABLE_ID!; status:STATUS!; enabled:BOOL! | slot_policy=ANY\|FIXED; job_policy=ELIGIBLE_ANY\|KILLER_JOB\|FIXED | tier=1..5 | fixed_job_id→jobs.csv(job_id)/HARD | slot_policy/job_policy null matrix | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_RANDOM_EQUIPMENT_SPEC_INVALID |
| recipe_materials.csv | 1 | TRUE | recipe_id+material_no | recipe_id:STABLE_ID!; material_no:INT32!; item_id:STABLE_ID!; quantity:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | material_no>=1; quantity>=0; quantity/weight context는 >=1 | recipe_id→recipes.csv(recipe_id)/HARD; item_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| recipe_outputs.csv | 1 | TRUE | recipe_id+output_no | recipe_id:STABLE_ID!; output_no:INT32!; reward_type:ENUM!; reward_id:STABLE_ID!; quantity:SAFE_INT!; status:STATUS!; enabled:BOOL! | reward_type=ITEM\|POTION\|EQUIPMENT_TEMPLATE | output_no>=1; quantity>=0; quantity/weight context는 >=1 | recipe_id→recipes.csv(recipe_id)/HARD | reward_type×reward_id | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_REWARD_DISCRIMINATOR_MISMATCH |
| recipes.csv | 1 | TRUE | recipe_id | recipe_id:STABLE_ID!; name_text_key:STABLE_ID!; facility_id:STABLE_ID!; facility_level:INT32!; npc_proficiency_id:STABLE_ID!; craft_seconds:SAFE_INT!; quality_roll:BOOL!; status:STATUS!; enabled:BOOL! | — | facility_level>=1; craft_seconds>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; facility_id→facilities.csv(facility_id)/HARD; npc_proficiency_id→npc_proficiency_levels.csv(proficiency_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| recruitment_pity_groups.csv | 1 | TRUE | pity_group_id | pity_group_id:STABLE_ID!; category:ENUM!; carry_over:BOOL!; status:STATUS!; enabled:BOOL! | category=SPECIAL\|SPECIAL_RATEUP | §6 domain range | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_PITY_CONFIGURATION_INVALID |
| recruitment_pity_rules.csv | 1 | TRUE | pity_group_id+pity_rule_id | pity_group_id:STABLE_ID!; pity_rule_id:STABLE_ID!; trigger_count:INT32!; guaranteed_grade_id:STABLE_ID?; reset_on_grade_or_higher_id:STABLE_ID?; guarantee_type:ENUM!; status:STATUS!; enabled:BOOL! | guarantee_type=GRADE_AT_LEAST\|FEATURED_JOB | trigger_count>=1 | pity_group_id→recruitment_pity_groups.csv(pity_group_id)/HARD; guaranteed_grade_id→mercenary_grades.csv(grade_id)/HARD; reset_on_grade_or_higher_id→mercenary_grades.csv(grade_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_PITY_CONFIGURATION_INVALID |
| recruitment_pool_entries.csv | 1 | TRUE | pool_id+entry_no | pool_id:STABLE_ID!; entry_no:INT32!; result_type:ENUM!; result_id:STABLE_ID!; grade_id:STABLE_ID?; job_selection_type:ENUM!; job_selection_id:STABLE_ID?; weight:SAFE_INT!; status:STATUS!; enabled:BOOL! | result_type=GENERATED_MERCENARY\|ITEM\|EQUIPMENT_TEMPLATE; job_selection_type=NONE\|ALL\|FIXED\|RATE_UP_GROUP | entry_no>=1; weight>=0; quantity/weight context는 >=1 | pool_id→recruitment_pools.csv(pool_id)/HARD; grade_id→mercenary_grades.csv(grade_id)/HARD | result_type/grade/job selector matrix | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_RECRUITMENT_RESULT_MISMATCH |
| recruitment_pools.csv | 1 | TRUE | pool_id | pool_id:STABLE_ID!; pool_type:ENUM!; pity_group_id:STABLE_ID?; rate_up_group_id:STABLE_ID?; cost_type:ENUM!; cost_amount:SAFE_INT!; starts_at_utc:UTC_INSTANT?; ends_at_utc:UTC_INSTANT?; status:STATUS!; enabled:BOOL! | pool_type=TAVERN\|SPECIAL\|SPECIAL_RATEUP; cost_type=KINGDOM_GOLD\|TICKET\|FREE_PREMIUM\|PAID_PREMIUM | cost_amount>=0; quantity/weight context는 >=1 | pity_group_id→recruitment_pity_groups.csv(pity_group_id)/HARD; rate_up_group_id→recruitment_rate_up_groups.csv(rate_up_group_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| recruitment_rate_up_entries.csv | 1 | TRUE | rate_up_group_id+job_id | rate_up_group_id:STABLE_ID!; job_id:STABLE_ID!; weight:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | weight>=0; quantity/weight context는 >=1 | rate_up_group_id→recruitment_rate_up_groups.csv(rate_up_group_id)/HARD; job_id→jobs.csv(job_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| recruitment_rate_up_groups.csv | 1 | TRUE | rate_up_group_id | rate_up_group_id:STABLE_ID!; featured_share:DECIMAL!; failure_guarantee_mode:ENUM!; status:STATUS!; enabled:BOOL! | failure_guarantee_mode=NONE\|NEXT_S_OR_SS_FEATURED | featured_share=0..1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| refine_options.csv | 1 | TRUE | refine_option_id | refine_option_id:STABLE_ID!; name_text_key:STABLE_ID!; option_group:STABLE_ID!; stat_key:STABLE_ID!; min_value:DECIMAL!; max_value:DECIMAL!; material_item_id:STABLE_ID!; material_quantity:SAFE_INT!; personal_gold_cost:SAFE_INT!; status:STATUS!; enabled:BOOL! | — | material_quantity>=0; quantity/weight context는 >=1; personal_gold_cost>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; material_item_id→items.csv(item_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| regions.csv | 1 | TRUE | region_id | region_id:STABLE_ID!; name_text_key:STABLE_ID!; order:INT32!; tier:INT32!; min_rank_id:STABLE_ID!; unlock_condition_group_id:STABLE_ID!; kingdom_stage_id:STABLE_ID!; recommended_power:SAFE_INT!; max_active:INT32!; environment_tag:STABLE_ID!; raid_gate_id:STABLE_ID?; offline_efficiency:DECIMAL!; status:STATUS!; enabled:BOOL! | — | order>=1; tier=1..5; recommended_power>=0; quantity/weight context는 >=1; offline_efficiency=0..1 | name_text_key→localizations.csv(text_key)/HARD; min_rank_id→mercenary_ranks.csv(rank_id)/HARD; unlock_condition_group_id→condition_groups.csv(condition_group_id)/HARD; kingdom_stage_id→kingdom_stages.csv(stage_id)/HARD; raid_gate_id→raids.csv(raid_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| reward_entries.csv | 1 | TRUE | reward_group_id+entry_no | reward_group_id:STABLE_ID!; entry_no:INT32!; reward_type:ENUM!; reward_id:STABLE_ID!; quantity:SAFE_INT!; probability:DECIMAL?; weight:SAFE_INT?; status:STATUS!; enabled:BOOL! | reward_type=ITEM\|POTION\|CURRENCY\|PERSONAL_GOLD\|EQUIPMENT_TEMPLATE\|RANDOM_EQUIPMENT_TIER\|PLAYER_EXP\|KINGDOM_EXP\|REGION_UNLOCK\|PROGRESSION_FLAG | entry_no>=1; quantity>=0; quantity/weight context는 >=1; probability=0..1; weight>=0; quantity/weight context는 >=1 | reward_group_id→reward_groups.csv(reward_group_id)/HARD | reward_type×reward_id; distribution mode | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_REWARD_DISCRIMINATOR_MISMATCH |
| reward_groups.csv | 1 | TRUE | reward_group_id | reward_group_id:STABLE_ID!; distribution_mode:ENUM!; draw_count:INT32!; status:STATUS!; enabled:BOOL! | distribution_mode=ALL\|ONE_PROBABILITY\|ONE_WEIGHTED\|N_WEIGHTED | draw_count>=1 | — | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| runtime_config.csv | 1 | TRUE | config_key | config_key:STABLE_ID!; value_type:ENUM!; value:STRING!; unit:STABLE_ID!; min_value:DECIMAL?; max_value:DECIMAL?; description_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | value_type=INTEGER\|DECIMAL\|BOOLEAN\|STRING | value는 value_type tagged-union lexical | description_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| skills.csv | 1 | TRUE | skill_id | skill_id:STABLE_ID!; job_id:STABLE_ID!; name_text_key:STABLE_ID!; kind:ENUM!; unlock_rank_id:STABLE_ID!; target:STABLE_ID!; cooldown_sec:DECIMAL!; power_coeff:DECIMAL!; resource_cost:SAFE_INT!; condition:STABLE_ID!; description_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | kind=ACTIVE\|PASSIVE | resource_cost>=0; quantity/weight context는 >=1 | name_text_key→localizations.csv(text_key)/HARD; description_text_key→localizations.csv(text_key)/HARD; job_id→jobs.csv(job_id)/HARD; unlock_rank_id→mercenary_ranks.csv(rank_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| status_effects.csv | 1 | TRUE | status_effect_id | status_effect_id:STABLE_ID!; name_text_key:STABLE_ID!; category:ENUM!; stack_rule:STABLE_ID!; duration_sec:DECIMAL!; effect_key:STABLE_ID!; boss_resistance:DECIMAL!; status:STATUS!; enabled:BOOL! | category=BUFF\|DEBUFF\|CONTROL\|INJURY | boss_resistance=0..1 | name_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| trait_job_eligibility.csv | 1 | TRUE | trait_id+job_id | trait_id:STABLE_ID!; job_id:STABLE_ID!; status:STATUS!; enabled:BOOL! | — | §6 domain range | trait_id→traits.csv(trait_id)/HARD; job_id→jobs.csv(job_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; 공통 validator만 |
| traits.csv | 1 | TRUE | trait_id | trait_id:STABLE_ID!; name_text_key:STABLE_ID!; category:ENUM!; effect_key:STABLE_ID!; effect_value:DECIMAL!; description_text_key:STABLE_ID!; status:STATUS!; enabled:BOOL! | category=STAT\|COMBAT\|COLLECTION\|BOSS\|RESIST\|AI | §6 domain range | name_text_key→localizations.csv(text_key)/HARD; description_text_key→localizations.csv(text_key)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_TRAIT_CATEGORY_INVALID |
| tutorial_grants.csv | 1 | TRUE | grant_id+line_no | grant_id:STABLE_ID!; tutorial_step_id:STABLE_ID!; line_no:INT32!; reward_type:ENUM!; reward_id:STABLE_ID!; quantity:SAFE_INT!; status:STATUS!; enabled:BOOL! | reward_type=ITEM\|CURRENCY\|PROGRESSION_FLAG | line_no>=1; quantity>=0; quantity/weight context는 >=1 | tutorial_step_id→tutorial_steps.csv(tutorial_step_id)/HARD | reward_type×reward_id | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_REWARD_DISCRIMINATOR_MISMATCH |
| tutorial_steps.csv | 1 | TRUE | tutorial_step_id | tutorial_step_id:STABLE_ID!; order:INT32!; action_type:STABLE_ID!; target_id:STABLE_ID?; prerequisite_step_id:STABLE_ID?; skippable:BOOL!; status:STATUS!; enabled:BOOL! | — | order>=1 | prerequisite_step_id→tutorial_steps.csv(tutorial_step_id)/HARD | — | CSV_DOMAIN_INVALID; CSV_STATUS_ENABLED_INVALID; CSV_PRIMARY_KEY_DUPLICATE; CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET; CSV_TUTORIAL_DAG_INVALID |

### 7.1 시설 executable effect registry

`effect_key`는 localization key가 아니라 consumer dispatch key다. `effect_text_key`만 표시 문구다.

| effect_key | consumer | exact parameters |
|---|---|---|
| FAC_EFFECT_TAVERN_L1 | TAVERN | candidateCount=3; poolId=TAVERN_L1; lockSlots=0 |
| FAC_EFFECT_TAVERN_L2 | TAVERN | candidateCount=4; poolId=TAVERN_L1; lockSlots=0 |
| FAC_EFFECT_TAVERN_L3 | TAVERN | candidateCount=5; poolId=TAVERN_L1; lockSlots=0 |
| FAC_EFFECT_TAVERN_L4 | TAVERN | candidateCount=5; poolId=TAVERN_L4; lockSlots=2 |
| FAC_EFFECT_LODGE_L1 | ROSTER | activeSlots=4; rosterSlots=8 |
| FAC_EFFECT_LODGE_L2 | ROSTER | activeSlots=8; rosterSlots=12 |
| FAC_EFFECT_LODGE_L3 | ROSTER | activeSlots=12; rosterSlots=18 |
| FAC_EFFECT_LODGE_L4 | ROSTER | activeSlots=16; rosterSlots=24 |
| FAC_EFFECT_GUILD_L1 | PROMOTION | maxApprovalRank=RANK_REGULAR |
| FAC_EFFECT_GUILD_L2 | PROMOTION | maxApprovalRank=RANK_SKILLED; regionPolicyEnabled=TRUE |
| FAC_EFFECT_GUILD_L3 | PROMOTION | maxApprovalRank=RANK_ELITE; raidPreparationEnabled=TRUE |
| FAC_EFFECT_GUILD_L4 | PROMOTION | maxApprovalRank=RANK_LEGEND |
| FAC_EFFECT_STORE_L1 | STORE | buySellEnabled=TRUE; pricePolicy=LOW,STANDARD,HIGH |
| FAC_EFFECT_STORE_L2 | STORE | inventoryTargetPolicyEnabled=TRUE |
| FAC_EFFECT_STORE_L3 | STORE | minimumHandledQuality=QUALITY_RARE |
| FAC_EFFECT_STORE_L4 | STORE | bossEquipmentDisplayEnabled=TRUE |
| FAC_EFFECT_BLACKSMITH_L1 | CRAFT | maxCraftTier=1; dismantleEnabled=TRUE; maxEnhancementLevel=0 |
| FAC_EFFECT_BLACKSMITH_L2 | CRAFT | maxCraftTier=2; maxEnhancementLevel=5 |
| FAC_EFFECT_BLACKSMITH_L3 | CRAFT | maxCraftTier=4; maxEnhancementLevel=8; refineEnabled=TRUE |
| FAC_EFFECT_BLACKSMITH_L4 | CRAFT | maxCraftTier=5; maxEnhancementLevel=10; bossCraftEnabled=TRUE |
| FAC_EFFECT_ALCHEMY_L1 | ALCHEMY | maxPotionTier=1 |
| FAC_EFFECT_ALCHEMY_L2 | ALCHEMY | maxPotionTier=2; curePoisonEnabled=TRUE |
| FAC_EFFECT_ALCHEMY_L3 | ALCHEMY | maxPotionTier=4; resistancePotionEnabled=TRUE |
| FAC_EFFECT_ALCHEMY_L4 | ALCHEMY | maxPotionTier=5; raidPotionEnabled=TRUE |
| FAC_EFFECT_WAREHOUSE_L1 | STORAGE | capacity=200 |
| FAC_EFFECT_WAREHOUSE_L2 | STORAGE | capacity=500; reservedInventoryEnabled=TRUE |
| FAC_EFFECT_WAREHOUSE_L3 | STORAGE | capacity=1200 |
| FAC_EFFECT_WAREHOUSE_L4 | STORAGE | capacity=3000; filterEnabled=TRUE |
| FAC_EFFECT_INFIRMARY_L1 | TREATMENT | treatmentTimeMultiplier=1.0; statusEffectTreatment=FALSE |
| FAC_EFFECT_INFIRMARY_L2 | TREATMENT | treatmentTimeMultiplier=0.85; statusEffectTreatment=FALSE |
| FAC_EFFECT_INFIRMARY_L3 | TREATMENT | treatmentTimeMultiplier=0.85; statusEffectTreatment=TRUE |
| FAC_EFFECT_INFIRMARY_L4 | TREATMENT | treatmentTimeMultiplier=0.65; statusEffectTreatment=TRUE |

TAVERN L2/L3의 source 문구에는 확률 수치가 없으므로 임의 weight를 만들지 않고 TAVERN_L1 pool을 사용하며 후보 수만 증가한다. 원문 문구는 effect_text_key에 보존한다. raid difficulty recommended_power는 Hydra base 1800, Dragon base 3000에 NORMAL=1.0, HARD=1.5, CORRUPTED=2.25를 곱하고 floor한 1800/2700/4050 및 3000/4500/6750이다.

## 8. 60개 전체 canonical CSV

### Decision

각 파일은 정확히 한 번 나오며 header 아래가 전체 row다.

### Reason

implementation input을 외부 문서나 legacy runtime read에 의존시키지 않는다.

### Compatibility

0-row `content_aliases`, `recruitment_rate_up_groups`, `recruitment_rate_up_entries`도 required header file이다. 출시 Save/활성 rate-up 참조가 0이므로 유효하며 validator는 이 세 파일에 nonempty를 요구하지 않는다.

### Exact output

### 8.1 `asset_register.csv` (5 rows)

```csv
asset_id,name,creator,source_url,version,acquired_date,price_krw,license,commercial_use,modification_allowed,credit_required,used_in,notes,status,enabled
ASSET_MERC_PLACEHOLDER_WARRIOR_V1,Internal Warrior Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_GUARDIAN_V1,Internal Guardian Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_ARCHER_V1,Internal Archer Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_MAGE_V1,Internal Mage Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_CLERIC_V1,Internal Cleric Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
```

### 8.2 `autonomy_rules.csv` (37 rows)

```csv
state,rule_no,priority,condition_type,condition_value,reason_code,next_state,status,enabled
IDLE_TOWN,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
IDLE_TOWN,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
IDLE_TOWN,3,80,HAS_ACTIVE_ASSIGNMENT,,POLICY,PREPARE,TUNABLE,TRUE
IDLE_TOWN,4,0,ALWAYS,,NONE,IDLE_TOWN,TUNABLE,TRUE
PREPARE,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
PREPARE,2,90,POTION_COUNT_LTE,0,POTION_LOW,BUY_CONSUMABLES,TUNABLE,TRUE
PREPARE,3,0,ALWAYS,,POLICY,TRAVEL_TO_REGION,TUNABLE,TRUE
TRAVEL_TO_REGION,1,0,ARRIVED_REGION,,POLICY,FIND_TARGET,TUNABLE,TRUE
FIND_TARGET,1,100,TARGET_AVAILABLE,,TARGET_FOUND,COMBAT,TUNABLE,TRUE
FIND_TARGET,2,0,NO_TARGET_AVAILABLE,,POLICY,RETURN_TOWN,TUNABLE,TRUE
COMBAT,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
COMBAT,2,90,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,TUNABLE,TRUE
COMBAT,3,80,TARGET_DEFEATED,,LOOT_COMPLETE,LOOT,TUNABLE,TRUE
COMBAT,4,0,ALWAYS,,POLICY,COMBAT,TUNABLE,TRUE
LOOT,1,100,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,TUNABLE,TRUE
LOOT,2,0,ALWAYS,,LOOT_COMPLETE,CONTINUE_DECISION,TUNABLE,TRUE
CONTINUE_DECISION,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,2,90,POTION_COUNT_LTE,0,POTION_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,3,0,ALWAYS,,POLICY,FIND_TARGET,TUNABLE,TRUE
RETURN_TOWN,1,0,ARRIVED_TOWN,,PLAYER_RECALL,SELL_LOOT,TUNABLE,TRUE
SELL_LOOT,1,100,HAS_INJURY,,HP_LOW,HEAL,TUNABLE,TRUE
SELL_LOOT,2,0,ALWAYS,,LOOT_COMPLETE,EVALUATE_EQUIPMENT,TUNABLE,TRUE
HEAL,1,100,INJURY_CLEARED,,POLICY,BUY_CONSUMABLES,TUNABLE,TRUE
HEAL,2,0,ALWAYS,,POLICY,HEAL,TUNABLE,TRUE
BUY_CONSUMABLES,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,EVALUATE_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,1,100,UPGRADE_PURCHASE_AVAILABLE,0.05,POLICY,BUY_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
EVALUATE_EQUIPMENT,3,0,ALWAYS,,POLICY,PREPARE,TUNABLE,TRUE
BUY_EQUIPMENT,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,PREPARE,TUNABLE,TRUE
PROMOTION_READY,1,100,PROMOTION_REVIEW_STARTED,,PROMOTION_AVAILABLE,PROMOTION_PROCESS,TUNABLE,TRUE
PROMOTION_READY,2,0,ALWAYS,,PROMOTION_AVAILABLE,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,1,100,PROMOTION_REVIEW_COMPLETE,,POLICY,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,2,0,ALWAYS,,POLICY,PROMOTION_PROCESS,TUNABLE,TRUE
INJURED,1,100,IS_IN_TOWN,,HP_LOW,HEAL,TUNABLE,TRUE
INJURED,2,0,ALWAYS,,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
RAID_READY,1,100,PLAYER_RECALL_ACTIVE,,PLAYER_RECALL,IDLE_TOWN,TUNABLE,TRUE
RAID_READY,2,0,ALWAYS,,POLICY,RAID_READY,TUNABLE,TRUE
```

### 8.3 `condition_group_members.csv` (38 rows)

```csv
condition_group_id,member_no,member_type,condition_id,child_group_id,negate,status,enabled
COND_GRP_KINGDOM_1_UNLOCK,1,CONDITION,COND_ALWAYS_TRUE,,FALSE,CONFIRMED,TRUE
COND_GRP_KINGDOM_2_UNLOCK,1,CONDITION,COND_FACILITY_UPGRADE_COUNT_3,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_2_UNLOCK,2,CONDITION,COND_REGION_R02_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_3_UNLOCK,1,CONDITION,COND_MON_R02_ELITE_OGRE_KILL_1,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_3_UNLOCK,2,CONDITION,COND_REGULAR_OR_HIGHER_6,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_4_UNLOCK,1,CONDITION,COND_RAID_HYDRA_CLEAR_1,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_4_UNLOCK,2,CONDITION,COND_ELITE_OR_HIGHER_4,,FALSE,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,1,CONDITION,COND_REGION_R01_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,2,CONDITION,COND_REGION_R02_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,3,CONDITION,COND_REGION_R03_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,4,CONDITION,COND_REGION_R04_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,5,CONDITION,COND_REGION_R05_UNLOCKED,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,1,CONDITION,COND_FAC_TAVERN_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,2,CONDITION,COND_FAC_LODGE_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,3,CONDITION,COND_FAC_GUILD_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,4,CONDITION,COND_FAC_STORE_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,5,CONDITION,COND_FAC_BLACKSMITH_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,6,CONDITION,COND_FAC_ALCHEMY_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,7,CONDITION,COND_FAC_WAREHOUSE_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,8,CONDITION,COND_FAC_INFIRMARY_LEVEL_4,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_5_UNLOCK,1,GROUP,,COND_GRP_ALL_REGIONS_UNLOCKED,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_5_UNLOCK,2,CONDITION,COND_HERO_OR_HIGHER_4,,FALSE,TUNABLE,TRUE
COND_GRP_KINGDOM_5_UNLOCK,3,GROUP,,COND_GRP_CORE_FACILITIES_L4,FALSE,TUNABLE,TRUE
COND_GRP_REGION_R01_UNLOCK,1,CONDITION,COND_ALWAYS_TRUE,,FALSE,CONFIRMED,TRUE
COND_GRP_REGION_R02_UNLOCK,1,CONDITION,COND_TUT_09_COMPLETED,,FALSE,CONFIRMED,TRUE
COND_GRP_REGION_R03_UNLOCK,1,CONDITION,COND_MON_R02_ELITE_OGRE_KILL_1,,FALSE,CONFIRMED,TRUE
COND_GRP_REGION_R04_UNLOCK,1,CONDITION,COND_FAC_ALCHEMY_LEVEL_2,,FALSE,CONFIRMED,TRUE
COND_GRP_REGION_R04_UNLOCK,2,CONDITION,COND_MON_R03_ELITE_GUARDIAN_KILL_1,,FALSE,CONFIRMED,TRUE
COND_GRP_REGION_R05_UNLOCK,1,CONDITION,COND_RAID_HYDRA_CLEAR_1,,FALSE,CONFIRMED,TRUE
COND_GRP_RAID_HYDRA_UNLOCK,1,CONDITION,COND_REGION_R04_PROGRESS_100,,FALSE,TUNABLE,TRUE
COND_GRP_RAID_DRAGON_UNLOCK,1,CONDITION,COND_KINGDOM_5_REACHED,,FALSE,TUNABLE,TRUE
COND_GRP_HYDRA_HEAD_BREAK,1,CONDITION,COND_HYDRA_HEAD_BREAK,,FALSE,TUNABLE,TRUE
COND_GRP_HYDRA_BODY_BREAK,1,CONDITION,COND_HYDRA_BODY_BREAK,,FALSE,TUNABLE,TRUE
COND_GRP_HYDRA_HEART_EXPOSED,1,CONDITION,COND_HYDRA_HEART_EXPOSED,,FALSE,TUNABLE,TRUE
COND_GRP_DRAGON_HORN_BREAK,1,CONDITION,COND_DRAGON_HORN_BREAK,,FALSE,TUNABLE,TRUE
COND_GRP_DRAGON_WING_BREAK,1,CONDITION,COND_DRAGON_WING_BREAK,,FALSE,TUNABLE,TRUE
COND_GRP_DRAGON_BODY_BREAK,1,CONDITION,COND_DRAGON_BODY_BREAK,,FALSE,TUNABLE,TRUE
COND_GRP_DRAGON_HEART_EXPOSED,1,CONDITION,COND_DRAGON_HEART_EXPOSED,,FALSE,TUNABLE,TRUE
```

### 8.4 `condition_groups.csv` (21 rows)

```csv
condition_group_id,logic,description_key,status,enabled
COND_GRP_KINGDOM_1_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_KINGDOM_2_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_KINGDOM_3_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_KINGDOM_4_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_ALL_REGIONS_UNLOCKED,ALL,,TUNABLE,TRUE
COND_GRP_CORE_FACILITIES_L4,ALL,,TUNABLE,TRUE
COND_GRP_KINGDOM_5_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_REGION_R01_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_REGION_R02_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_REGION_R03_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_REGION_R04_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_REGION_R05_UNLOCK,ALL,,CONFIRMED,TRUE
COND_GRP_RAID_HYDRA_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_RAID_DRAGON_UNLOCK,ALL,,TUNABLE,TRUE
COND_GRP_HYDRA_HEAD_BREAK,ALL,,TUNABLE,TRUE
COND_GRP_HYDRA_BODY_BREAK,ALL,,TUNABLE,TRUE
COND_GRP_HYDRA_HEART_EXPOSED,ALL,,TUNABLE,TRUE
COND_GRP_DRAGON_HORN_BREAK,ALL,,TUNABLE,TRUE
COND_GRP_DRAGON_WING_BREAK,ALL,,TUNABLE,TRUE
COND_GRP_DRAGON_BODY_BREAK,ALL,,TUNABLE,TRUE
COND_GRP_DRAGON_HEART_EXPOSED,ALL,,TUNABLE,TRUE
```

### 8.5 `conditions.csv` (34 rows)

```csv
condition_id,condition_type,subject_type,subject_id,subject_sub_id,operator,value_type,expected_value,status,enabled
COND_ALWAYS_TRUE,ALWAYS_TRUE,SYSTEM,,,EQ,BOOLEAN,TRUE,CONFIRMED,TRUE
COND_FACILITY_UPGRADE_COUNT_3,FACILITY_UPGRADE_COUNT,KINGDOM,,,GTE,INTEGER,3,TUNABLE,TRUE
COND_REGION_R02_UNLOCKED,REGION_UNLOCKED,REGION,REGION_R02,,EQ,BOOLEAN,TRUE,CONFIRMED,TRUE
COND_MON_R02_ELITE_OGRE_KILL_1,MONSTER_KILL_COUNT,MONSTER,MON_R02_ELITE_OGRE,,GTE,INTEGER,1,TUNABLE,TRUE
COND_REGULAR_OR_HIGHER_6,MERCENARY_COUNT_AT_RANK_OR_HIGHER,MERCENARY_ROSTER,RANK_REGULAR,,GTE,INTEGER,6,TUNABLE,TRUE
COND_RAID_HYDRA_CLEAR_1,RAID_CLEAR_COUNT,RAID,RAID_HYDRA,,GTE,INTEGER,1,TUNABLE,TRUE
COND_ELITE_OR_HIGHER_4,MERCENARY_COUNT_AT_RANK_OR_HIGHER,MERCENARY_ROSTER,RANK_ELITE,,GTE,INTEGER,4,TUNABLE,TRUE
COND_REGION_R01_UNLOCKED,REGION_UNLOCKED,REGION,REGION_R01,,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_REGION_R03_UNLOCKED,REGION_UNLOCKED,REGION,REGION_R03,,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_REGION_R04_UNLOCKED,REGION_UNLOCKED,REGION,REGION_R04,,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_REGION_R05_UNLOCKED,REGION_UNLOCKED,REGION,REGION_R05,,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_FAC_TAVERN_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_TAVERN,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_LODGE_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_LODGE,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_GUILD_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_GUILD,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_STORE_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_STORE,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_BLACKSMITH_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_BLACKSMITH,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_ALCHEMY_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_ALCHEMY,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_WAREHOUSE_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_WAREHOUSE,,GTE,INTEGER,4,TUNABLE,TRUE
COND_FAC_INFIRMARY_LEVEL_4,FACILITY_LEVEL,FACILITY,FAC_INFIRMARY,,GTE,INTEGER,4,TUNABLE,TRUE
COND_HERO_OR_HIGHER_4,MERCENARY_COUNT_AT_RANK_OR_HIGHER,MERCENARY_ROSTER,RANK_HERO,,GTE,INTEGER,4,TUNABLE,TRUE
COND_TUT_09_COMPLETED,TUTORIAL_STEP_COMPLETED,TUTORIAL_STEP,TUT_09_PROMOTE_REGULAR,,EQ,BOOLEAN,TRUE,CONFIRMED,TRUE
COND_FAC_ALCHEMY_LEVEL_2,FACILITY_LEVEL,FACILITY,FAC_ALCHEMY,,GTE,INTEGER,2,CONFIRMED,TRUE
COND_MON_R03_ELITE_GUARDIAN_KILL_1,MONSTER_KILL_COUNT,MONSTER,MON_R03_ELITE_GUARDIAN,,GTE,INTEGER,1,CONFIRMED,TRUE
COND_REGION_R04_PROGRESS_100,REGION_PROGRESS_PERCENT,REGION,REGION_R04,,GTE,INTEGER,100,TUNABLE,TRUE
COND_KINGDOM_5_REACHED,KINGDOM_STAGE_REACHED,KINGDOM_STAGE,KINGDOM_5,,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_HYDRA_HEAD_BREAK,RAID_PART_BROKEN,RAID_PART,RAID_HYDRA,HYDRA_HEAD,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_HYDRA_BODY_BREAK,RAID_PART_BROKEN,RAID_PART,RAID_HYDRA,HYDRA_BODY,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_HYDRA_HEART_EXPOSED,RAID_PART_EXPOSED,RAID_PART,RAID_HYDRA,HYDRA_HEART,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_DRAGON_HORN_BREAK,RAID_PART_BROKEN,RAID_PART,RAID_DRAGON,DRAGON_HORN,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_DRAGON_WING_BREAK,RAID_PART_BROKEN,RAID_PART,RAID_DRAGON,DRAGON_WING,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_DRAGON_BODY_BREAK,RAID_PART_BROKEN,RAID_PART,RAID_DRAGON,DRAGON_BODY,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_DRAGON_HEART_EXPOSED,RAID_PART_EXPOSED,RAID_PART,RAID_DRAGON,DRAGON_HEART,EQ,BOOLEAN,TRUE,TUNABLE,TRUE
COND_RAID_HYDRA_CLEAR,RAID_CLEAR_COUNT,RAID,RAID_HYDRA,,GTE,INTEGER,1,TUNABLE,TRUE
COND_RAID_DRAGON_CLEAR,RAID_CLEAR_COUNT,RAID,RAID_DRAGON,,GTE,INTEGER,1,TUNABLE,TRUE
```

### 8.6 `content_aliases.csv` (0 rows)

```csv
entity_type,from_id,from_content_version,alias_type,to_id,status,enabled
```

### 8.7 `currencies.csv` (4 rows)

```csv
currency_id,currency_type,authority,max_balance,name_text_key,status,enabled
KINGDOM_GOLD,KINGDOM,LOCAL,9007199254740991,TXT_CURRENCY_KINGDOM_GOLD_NAME,CONFIRMED,TRUE
PREMIUM_FREE,PREMIUM_FREE,SERVER,9007199254740991,TXT_CURRENCY_PREMIUM_FREE_NAME,CONFIRMED,TRUE
PREMIUM_PAID,PREMIUM_PAID,SERVER,9007199254740991,TXT_CURRENCY_PREMIUM_PAID_NAME,CONFIRMED,TRUE
SPECIAL_RECRUIT_TICKET,TICKET,SERVER,9007199254740991,TXT_CURRENCY_SPECIAL_RECRUIT_TICKET_NAME,CONFIRMED,TRUE
```

### 8.8 `enhancement_rules.csv` (10 rows)

```csv
target_level,success_chance,stone_item_id,stone_quantity,personal_gold_cost,fail_pity_increment,destroy_on_fail,downrank_on_fail,status,enabled
1,1.0,MAT_ENHANCE_1,1,100,0,FALSE,FALSE,TUNABLE,TRUE
2,1.0,MAT_ENHANCE_1,1,400,0,FALSE,FALSE,TUNABLE,TRUE
3,1.0,MAT_ENHANCE_1,1,900,0,FALSE,FALSE,TUNABLE,TRUE
4,1.0,MAT_ENHANCE_1,1,1600,0,FALSE,FALSE,TUNABLE,TRUE
5,1.0,MAT_ENHANCE_2,1,2500,0,FALSE,FALSE,TUNABLE,TRUE
6,0.85,MAT_ENHANCE_2,2,3600,0.05,FALSE,FALSE,TUNABLE,TRUE
7,0.72,MAT_ENHANCE_2,2,4900,0.06,FALSE,FALSE,TUNABLE,TRUE
8,0.6,MAT_ENHANCE_3,3,6400,0.07,FALSE,FALSE,TUNABLE,TRUE
9,0.48,MAT_ENHANCE_3,3,8100,0.08,FALSE,FALSE,TUNABLE,TRUE
10,0.38,MAT_ENHANCE_3,4,10000,0.1,FALSE,FALSE,TUNABLE,TRUE
```

### 8.9 `equipment_job_eligibility.csv` (110 rows)

```csv
equipment_template_id,job_id,status,enabled
EQ_T1_WARRIOR_WEAPON,JOB_WARRIOR,TUNABLE,TRUE
EQ_T1_GUARDIAN_WEAPON,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T1_ARCHER_WEAPON,JOB_ARCHER,TUNABLE,TRUE
EQ_T1_MAGE_WEAPON,JOB_MAGE,TUNABLE,TRUE
EQ_T1_CLERIC_WEAPON,JOB_CLERIC,TUNABLE,TRUE
EQ_T1_HEAVY_ARMOR,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T1_HEAVY_ARMOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_T1_HEAVY_HELMET,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T1_HEAVY_HELMET,JOB_WARRIOR,TUNABLE,TRUE
EQ_T1_LIGHT_ARMOR,JOB_ARCHER,TUNABLE,TRUE
EQ_T1_LIGHT_HELMET,JOB_ARCHER,TUNABLE,TRUE
EQ_T1_CLOTH_ARMOR,JOB_CLERIC,TUNABLE,TRUE
EQ_T1_CLOTH_ARMOR,JOB_MAGE,TUNABLE,TRUE
EQ_T1_CLOTH_HELMET,JOB_CLERIC,TUNABLE,TRUE
EQ_T1_CLOTH_HELMET,JOB_MAGE,TUNABLE,TRUE
EQ_T1_POWER_ACCESSORY,JOB_ARCHER,TUNABLE,TRUE
EQ_T1_POWER_ACCESSORY,JOB_WARRIOR,TUNABLE,TRUE
EQ_T1_GUARD_ACCESSORY,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T1_WISDOM_ACCESSORY,JOB_CLERIC,TUNABLE,TRUE
EQ_T1_WISDOM_ACCESSORY,JOB_MAGE,TUNABLE,TRUE
EQ_T2_WARRIOR_WEAPON,JOB_WARRIOR,TUNABLE,TRUE
EQ_T2_GUARDIAN_WEAPON,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T2_ARCHER_WEAPON,JOB_ARCHER,TUNABLE,TRUE
EQ_T2_MAGE_WEAPON,JOB_MAGE,TUNABLE,TRUE
EQ_T2_CLERIC_WEAPON,JOB_CLERIC,TUNABLE,TRUE
EQ_T2_HEAVY_ARMOR,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T2_HEAVY_ARMOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_T2_HEAVY_HELMET,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T2_HEAVY_HELMET,JOB_WARRIOR,TUNABLE,TRUE
EQ_T2_LIGHT_ARMOR,JOB_ARCHER,TUNABLE,TRUE
EQ_T2_LIGHT_HELMET,JOB_ARCHER,TUNABLE,TRUE
EQ_T2_CLOTH_ARMOR,JOB_CLERIC,TUNABLE,TRUE
EQ_T2_CLOTH_ARMOR,JOB_MAGE,TUNABLE,TRUE
EQ_T2_CLOTH_HELMET,JOB_CLERIC,TUNABLE,TRUE
EQ_T2_CLOTH_HELMET,JOB_MAGE,TUNABLE,TRUE
EQ_T2_POWER_ACCESSORY,JOB_ARCHER,TUNABLE,TRUE
EQ_T2_POWER_ACCESSORY,JOB_WARRIOR,TUNABLE,TRUE
EQ_T2_GUARD_ACCESSORY,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T2_WISDOM_ACCESSORY,JOB_CLERIC,TUNABLE,TRUE
EQ_T2_WISDOM_ACCESSORY,JOB_MAGE,TUNABLE,TRUE
EQ_T3_WARRIOR_WEAPON,JOB_WARRIOR,TUNABLE,TRUE
EQ_T3_GUARDIAN_WEAPON,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T3_ARCHER_WEAPON,JOB_ARCHER,TUNABLE,TRUE
EQ_T3_MAGE_WEAPON,JOB_MAGE,TUNABLE,TRUE
EQ_T3_CLERIC_WEAPON,JOB_CLERIC,TUNABLE,TRUE
EQ_T3_HEAVY_ARMOR,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T3_HEAVY_ARMOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_T3_HEAVY_HELMET,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T3_HEAVY_HELMET,JOB_WARRIOR,TUNABLE,TRUE
EQ_T3_LIGHT_ARMOR,JOB_ARCHER,TUNABLE,TRUE
EQ_T3_LIGHT_HELMET,JOB_ARCHER,TUNABLE,TRUE
EQ_T3_CLOTH_ARMOR,JOB_CLERIC,TUNABLE,TRUE
EQ_T3_CLOTH_ARMOR,JOB_MAGE,TUNABLE,TRUE
EQ_T3_CLOTH_HELMET,JOB_CLERIC,TUNABLE,TRUE
EQ_T3_CLOTH_HELMET,JOB_MAGE,TUNABLE,TRUE
EQ_T3_POWER_ACCESSORY,JOB_ARCHER,TUNABLE,TRUE
EQ_T3_POWER_ACCESSORY,JOB_WARRIOR,TUNABLE,TRUE
EQ_T3_GUARD_ACCESSORY,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T3_WISDOM_ACCESSORY,JOB_CLERIC,TUNABLE,TRUE
EQ_T3_WISDOM_ACCESSORY,JOB_MAGE,TUNABLE,TRUE
EQ_T4_WARRIOR_WEAPON,JOB_WARRIOR,TUNABLE,TRUE
EQ_T4_GUARDIAN_WEAPON,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T4_ARCHER_WEAPON,JOB_ARCHER,TUNABLE,TRUE
EQ_T4_MAGE_WEAPON,JOB_MAGE,TUNABLE,TRUE
EQ_T4_CLERIC_WEAPON,JOB_CLERIC,TUNABLE,TRUE
EQ_T4_HEAVY_ARMOR,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T4_HEAVY_ARMOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_T4_HEAVY_HELMET,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T4_HEAVY_HELMET,JOB_WARRIOR,TUNABLE,TRUE
EQ_T4_LIGHT_ARMOR,JOB_ARCHER,TUNABLE,TRUE
EQ_T4_LIGHT_HELMET,JOB_ARCHER,TUNABLE,TRUE
EQ_T4_CLOTH_ARMOR,JOB_CLERIC,TUNABLE,TRUE
EQ_T4_CLOTH_ARMOR,JOB_MAGE,TUNABLE,TRUE
EQ_T4_CLOTH_HELMET,JOB_CLERIC,TUNABLE,TRUE
EQ_T4_CLOTH_HELMET,JOB_MAGE,TUNABLE,TRUE
EQ_T4_POWER_ACCESSORY,JOB_ARCHER,TUNABLE,TRUE
EQ_T4_POWER_ACCESSORY,JOB_WARRIOR,TUNABLE,TRUE
EQ_T4_GUARD_ACCESSORY,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T4_WISDOM_ACCESSORY,JOB_CLERIC,TUNABLE,TRUE
EQ_T4_WISDOM_ACCESSORY,JOB_MAGE,TUNABLE,TRUE
EQ_T5_WARRIOR_WEAPON,JOB_WARRIOR,TUNABLE,TRUE
EQ_T5_GUARDIAN_WEAPON,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T5_ARCHER_WEAPON,JOB_ARCHER,TUNABLE,TRUE
EQ_T5_MAGE_WEAPON,JOB_MAGE,TUNABLE,TRUE
EQ_T5_CLERIC_WEAPON,JOB_CLERIC,TUNABLE,TRUE
EQ_T5_HEAVY_ARMOR,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T5_HEAVY_ARMOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_T5_HEAVY_HELMET,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T5_HEAVY_HELMET,JOB_WARRIOR,TUNABLE,TRUE
EQ_T5_LIGHT_ARMOR,JOB_ARCHER,TUNABLE,TRUE
EQ_T5_LIGHT_HELMET,JOB_ARCHER,TUNABLE,TRUE
EQ_T5_CLOTH_ARMOR,JOB_CLERIC,TUNABLE,TRUE
EQ_T5_CLOTH_ARMOR,JOB_MAGE,TUNABLE,TRUE
EQ_T5_CLOTH_HELMET,JOB_CLERIC,TUNABLE,TRUE
EQ_T5_CLOTH_HELMET,JOB_MAGE,TUNABLE,TRUE
EQ_T5_POWER_ACCESSORY,JOB_ARCHER,TUNABLE,TRUE
EQ_T5_POWER_ACCESSORY,JOB_WARRIOR,TUNABLE,TRUE
EQ_T5_GUARD_ACCESSORY,JOB_GUARDIAN,TUNABLE,TRUE
EQ_T5_WISDOM_ACCESSORY,JOB_CLERIC,TUNABLE,TRUE
EQ_T5_WISDOM_ACCESSORY,JOB_MAGE,TUNABLE,TRUE
EQ_BOSS_HYDRA_WARRIOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_BOSS_HYDRA_GUARDIAN,JOB_GUARDIAN,TUNABLE,TRUE
EQ_BOSS_HYDRA_ARCHER,JOB_ARCHER,TUNABLE,TRUE
EQ_BOSS_HYDRA_MAGE,JOB_MAGE,TUNABLE,TRUE
EQ_BOSS_HYDRA_CLERIC,JOB_CLERIC,TUNABLE,TRUE
EQ_BOSS_DRAGON_WARRIOR,JOB_WARRIOR,TUNABLE,TRUE
EQ_BOSS_DRAGON_GUARDIAN,JOB_GUARDIAN,TUNABLE,TRUE
EQ_BOSS_DRAGON_ARCHER,JOB_ARCHER,TUNABLE,TRUE
EQ_BOSS_DRAGON_MAGE,JOB_MAGE,TUNABLE,TRUE
EQ_BOSS_DRAGON_CLERIC,JOB_CLERIC,TUNABLE,TRUE
```

### 8.10 `equipment_qualities.csv` (5 rows)

```csv
quality_id,name_text_key,order,stat_multiplier,innate_affix_chance,ui_token,status,enabled
QUALITY_COMMON,TXT_QUALITY_COMMON_NAME,1,1.0,0.0,QUALITY_COMMON,CONFIRMED,TRUE
QUALITY_FINE,TXT_QUALITY_FINE_NAME,2,1.08,0.1,QUALITY_FINE,CONFIRMED,TRUE
QUALITY_RARE,TXT_QUALITY_RARE_NAME,3,1.18,0.25,QUALITY_RARE,CONFIRMED,TRUE
QUALITY_LEGACY,TXT_QUALITY_LEGACY_NAME,4,1.32,0.5,QUALITY_LEGACY,CONFIRMED,TRUE
QUALITY_RELIC,TXT_QUALITY_RELIC_NAME,5,1.5,1.0,QUALITY_RELIC,CONFIRMED,TRUE
```

### 8.11 `equipment_quality_weights.csv` (20 rows)

```csv
quality_profile_id,quality_id,weight,status,enabled
QUALITY_DROP_T1_V1,QUALITY_COMMON,80,TUNABLE,TRUE
QUALITY_DROP_T1_V1,QUALITY_FINE,18,TUNABLE,TRUE
QUALITY_DROP_T1_V1,QUALITY_RARE,2,TUNABLE,TRUE
QUALITY_DROP_T2_V1,QUALITY_COMMON,65,TUNABLE,TRUE
QUALITY_DROP_T2_V1,QUALITY_FINE,28,TUNABLE,TRUE
QUALITY_DROP_T2_V1,QUALITY_RARE,7,TUNABLE,TRUE
QUALITY_DROP_T3_V1,QUALITY_COMMON,45,TUNABLE,TRUE
QUALITY_DROP_T3_V1,QUALITY_FINE,38,TUNABLE,TRUE
QUALITY_DROP_T3_V1,QUALITY_RARE,15,TUNABLE,TRUE
QUALITY_DROP_T3_V1,QUALITY_LEGACY,2,TUNABLE,TRUE
QUALITY_DROP_T4_V1,QUALITY_COMMON,25,TUNABLE,TRUE
QUALITY_DROP_T4_V1,QUALITY_FINE,40,TUNABLE,TRUE
QUALITY_DROP_T4_V1,QUALITY_RARE,27,TUNABLE,TRUE
QUALITY_DROP_T4_V1,QUALITY_LEGACY,7,TUNABLE,TRUE
QUALITY_DROP_T4_V1,QUALITY_RELIC,1,TUNABLE,TRUE
QUALITY_DROP_T5_V1,QUALITY_COMMON,10,TUNABLE,TRUE
QUALITY_DROP_T5_V1,QUALITY_FINE,30,TUNABLE,TRUE
QUALITY_DROP_T5_V1,QUALITY_RARE,38,TUNABLE,TRUE
QUALITY_DROP_T5_V1,QUALITY_LEGACY,18,TUNABLE,TRUE
QUALITY_DROP_T5_V1,QUALITY_RELIC,4,TUNABLE,TRUE
```

### 8.12 `equipment_templates.csv` (80 rows)

```csv
equipment_template_id,name_text_key,tier,slot,profile,base_power,source,boss_id,status,enabled
EQ_T1_WARRIOR_WEAPON,TXT_EQ_T1_WARRIOR_WEAPON_NAME,1,WEAPON,SWORD,100,CRAFT,,TUNABLE,TRUE
EQ_T1_GUARDIAN_WEAPON,TXT_EQ_T1_GUARDIAN_WEAPON_NAME,1,WEAPON,HAMMER_SHIELD,100,CRAFT,,TUNABLE,TRUE
EQ_T1_ARCHER_WEAPON,TXT_EQ_T1_ARCHER_WEAPON_NAME,1,WEAPON,BOW,100,CRAFT,,TUNABLE,TRUE
EQ_T1_MAGE_WEAPON,TXT_EQ_T1_MAGE_WEAPON_NAME,1,WEAPON,STAFF,100,CRAFT,,TUNABLE,TRUE
EQ_T1_CLERIC_WEAPON,TXT_EQ_T1_CLERIC_WEAPON_NAME,1,WEAPON,MACE,100,CRAFT,,TUNABLE,TRUE
EQ_T1_HEAVY_ARMOR,TXT_EQ_T1_HEAVY_ARMOR_NAME,1,ARMOR,HEAVY,90,CRAFT,,TUNABLE,TRUE
EQ_T1_HEAVY_HELMET,TXT_EQ_T1_HEAVY_HELMET_NAME,1,HELMET,HEAVY,55,CRAFT,,TUNABLE,TRUE
EQ_T1_LIGHT_ARMOR,TXT_EQ_T1_LIGHT_ARMOR_NAME,1,ARMOR,LIGHT,90,CRAFT,,TUNABLE,TRUE
EQ_T1_LIGHT_HELMET,TXT_EQ_T1_LIGHT_HELMET_NAME,1,HELMET,LIGHT,55,CRAFT,,TUNABLE,TRUE
EQ_T1_CLOTH_ARMOR,TXT_EQ_T1_CLOTH_ARMOR_NAME,1,ARMOR,CLOTH,90,CRAFT,,TUNABLE,TRUE
EQ_T1_CLOTH_HELMET,TXT_EQ_T1_CLOTH_HELMET_NAME,1,HELMET,CLOTH,55,CRAFT,,TUNABLE,TRUE
EQ_T1_POWER_ACCESSORY,TXT_EQ_T1_POWER_ACCESSORY_NAME,1,ACCESSORY,POWER,45,CRAFT,,TUNABLE,TRUE
EQ_T1_GUARD_ACCESSORY,TXT_EQ_T1_GUARD_ACCESSORY_NAME,1,ACCESSORY,GUARD,45,CRAFT,,TUNABLE,TRUE
EQ_T1_WISDOM_ACCESSORY,TXT_EQ_T1_WISDOM_ACCESSORY_NAME,1,ACCESSORY,WISDOM,45,CRAFT,,TUNABLE,TRUE
EQ_T2_WARRIOR_WEAPON,TXT_EQ_T2_WARRIOR_WEAPON_NAME,2,WEAPON,SWORD,180,CRAFT,,TUNABLE,TRUE
EQ_T2_GUARDIAN_WEAPON,TXT_EQ_T2_GUARDIAN_WEAPON_NAME,2,WEAPON,HAMMER_SHIELD,180,CRAFT,,TUNABLE,TRUE
EQ_T2_ARCHER_WEAPON,TXT_EQ_T2_ARCHER_WEAPON_NAME,2,WEAPON,BOW,180,CRAFT,,TUNABLE,TRUE
EQ_T2_MAGE_WEAPON,TXT_EQ_T2_MAGE_WEAPON_NAME,2,WEAPON,STAFF,180,CRAFT,,TUNABLE,TRUE
EQ_T2_CLERIC_WEAPON,TXT_EQ_T2_CLERIC_WEAPON_NAME,2,WEAPON,MACE,180,CRAFT,,TUNABLE,TRUE
EQ_T2_HEAVY_ARMOR,TXT_EQ_T2_HEAVY_ARMOR_NAME,2,ARMOR,HEAVY,162,CRAFT,,TUNABLE,TRUE
EQ_T2_HEAVY_HELMET,TXT_EQ_T2_HEAVY_HELMET_NAME,2,HELMET,HEAVY,99,CRAFT,,TUNABLE,TRUE
EQ_T2_LIGHT_ARMOR,TXT_EQ_T2_LIGHT_ARMOR_NAME,2,ARMOR,LIGHT,162,CRAFT,,TUNABLE,TRUE
EQ_T2_LIGHT_HELMET,TXT_EQ_T2_LIGHT_HELMET_NAME,2,HELMET,LIGHT,99,CRAFT,,TUNABLE,TRUE
EQ_T2_CLOTH_ARMOR,TXT_EQ_T2_CLOTH_ARMOR_NAME,2,ARMOR,CLOTH,162,CRAFT,,TUNABLE,TRUE
EQ_T2_CLOTH_HELMET,TXT_EQ_T2_CLOTH_HELMET_NAME,2,HELMET,CLOTH,99,CRAFT,,TUNABLE,TRUE
EQ_T2_POWER_ACCESSORY,TXT_EQ_T2_POWER_ACCESSORY_NAME,2,ACCESSORY,POWER,81,CRAFT,,TUNABLE,TRUE
EQ_T2_GUARD_ACCESSORY,TXT_EQ_T2_GUARD_ACCESSORY_NAME,2,ACCESSORY,GUARD,81,CRAFT,,TUNABLE,TRUE
EQ_T2_WISDOM_ACCESSORY,TXT_EQ_T2_WISDOM_ACCESSORY_NAME,2,ACCESSORY,WISDOM,81,CRAFT,,TUNABLE,TRUE
EQ_T3_WARRIOR_WEAPON,TXT_EQ_T3_WARRIOR_WEAPON_NAME,3,WEAPON,SWORD,320,CRAFT,,TUNABLE,TRUE
EQ_T3_GUARDIAN_WEAPON,TXT_EQ_T3_GUARDIAN_WEAPON_NAME,3,WEAPON,HAMMER_SHIELD,320,CRAFT,,TUNABLE,TRUE
EQ_T3_ARCHER_WEAPON,TXT_EQ_T3_ARCHER_WEAPON_NAME,3,WEAPON,BOW,320,CRAFT,,TUNABLE,TRUE
EQ_T3_MAGE_WEAPON,TXT_EQ_T3_MAGE_WEAPON_NAME,3,WEAPON,STAFF,320,CRAFT,,TUNABLE,TRUE
EQ_T3_CLERIC_WEAPON,TXT_EQ_T3_CLERIC_WEAPON_NAME,3,WEAPON,MACE,320,CRAFT,,TUNABLE,TRUE
EQ_T3_HEAVY_ARMOR,TXT_EQ_T3_HEAVY_ARMOR_NAME,3,ARMOR,HEAVY,288,CRAFT,,TUNABLE,TRUE
EQ_T3_HEAVY_HELMET,TXT_EQ_T3_HEAVY_HELMET_NAME,3,HELMET,HEAVY,176,CRAFT,,TUNABLE,TRUE
EQ_T3_LIGHT_ARMOR,TXT_EQ_T3_LIGHT_ARMOR_NAME,3,ARMOR,LIGHT,288,CRAFT,,TUNABLE,TRUE
EQ_T3_LIGHT_HELMET,TXT_EQ_T3_LIGHT_HELMET_NAME,3,HELMET,LIGHT,176,CRAFT,,TUNABLE,TRUE
EQ_T3_CLOTH_ARMOR,TXT_EQ_T3_CLOTH_ARMOR_NAME,3,ARMOR,CLOTH,288,CRAFT,,TUNABLE,TRUE
EQ_T3_CLOTH_HELMET,TXT_EQ_T3_CLOTH_HELMET_NAME,3,HELMET,CLOTH,176,CRAFT,,TUNABLE,TRUE
EQ_T3_POWER_ACCESSORY,TXT_EQ_T3_POWER_ACCESSORY_NAME,3,ACCESSORY,POWER,144,CRAFT,,TUNABLE,TRUE
EQ_T3_GUARD_ACCESSORY,TXT_EQ_T3_GUARD_ACCESSORY_NAME,3,ACCESSORY,GUARD,144,CRAFT,,TUNABLE,TRUE
EQ_T3_WISDOM_ACCESSORY,TXT_EQ_T3_WISDOM_ACCESSORY_NAME,3,ACCESSORY,WISDOM,144,CRAFT,,TUNABLE,TRUE
EQ_T4_WARRIOR_WEAPON,TXT_EQ_T4_WARRIOR_WEAPON_NAME,4,WEAPON,SWORD,560,CRAFT,,TUNABLE,TRUE
EQ_T4_GUARDIAN_WEAPON,TXT_EQ_T4_GUARDIAN_WEAPON_NAME,4,WEAPON,HAMMER_SHIELD,560,CRAFT,,TUNABLE,TRUE
EQ_T4_ARCHER_WEAPON,TXT_EQ_T4_ARCHER_WEAPON_NAME,4,WEAPON,BOW,560,CRAFT,,TUNABLE,TRUE
EQ_T4_MAGE_WEAPON,TXT_EQ_T4_MAGE_WEAPON_NAME,4,WEAPON,STAFF,560,CRAFT,,TUNABLE,TRUE
EQ_T4_CLERIC_WEAPON,TXT_EQ_T4_CLERIC_WEAPON_NAME,4,WEAPON,MACE,560,CRAFT,,TUNABLE,TRUE
EQ_T4_HEAVY_ARMOR,TXT_EQ_T4_HEAVY_ARMOR_NAME,4,ARMOR,HEAVY,504,CRAFT,,TUNABLE,TRUE
EQ_T4_HEAVY_HELMET,TXT_EQ_T4_HEAVY_HELMET_NAME,4,HELMET,HEAVY,308,CRAFT,,TUNABLE,TRUE
EQ_T4_LIGHT_ARMOR,TXT_EQ_T4_LIGHT_ARMOR_NAME,4,ARMOR,LIGHT,504,CRAFT,,TUNABLE,TRUE
EQ_T4_LIGHT_HELMET,TXT_EQ_T4_LIGHT_HELMET_NAME,4,HELMET,LIGHT,308,CRAFT,,TUNABLE,TRUE
EQ_T4_CLOTH_ARMOR,TXT_EQ_T4_CLOTH_ARMOR_NAME,4,ARMOR,CLOTH,504,CRAFT,,TUNABLE,TRUE
EQ_T4_CLOTH_HELMET,TXT_EQ_T4_CLOTH_HELMET_NAME,4,HELMET,CLOTH,308,CRAFT,,TUNABLE,TRUE
EQ_T4_POWER_ACCESSORY,TXT_EQ_T4_POWER_ACCESSORY_NAME,4,ACCESSORY,POWER,252,CRAFT,,TUNABLE,TRUE
EQ_T4_GUARD_ACCESSORY,TXT_EQ_T4_GUARD_ACCESSORY_NAME,4,ACCESSORY,GUARD,252,CRAFT,,TUNABLE,TRUE
EQ_T4_WISDOM_ACCESSORY,TXT_EQ_T4_WISDOM_ACCESSORY_NAME,4,ACCESSORY,WISDOM,252,CRAFT,,TUNABLE,TRUE
EQ_T5_WARRIOR_WEAPON,TXT_EQ_T5_WARRIOR_WEAPON_NAME,5,WEAPON,SWORD,900,CRAFT,,TUNABLE,TRUE
EQ_T5_GUARDIAN_WEAPON,TXT_EQ_T5_GUARDIAN_WEAPON_NAME,5,WEAPON,HAMMER_SHIELD,900,CRAFT,,TUNABLE,TRUE
EQ_T5_ARCHER_WEAPON,TXT_EQ_T5_ARCHER_WEAPON_NAME,5,WEAPON,BOW,900,CRAFT,,TUNABLE,TRUE
EQ_T5_MAGE_WEAPON,TXT_EQ_T5_MAGE_WEAPON_NAME,5,WEAPON,STAFF,900,CRAFT,,TUNABLE,TRUE
EQ_T5_CLERIC_WEAPON,TXT_EQ_T5_CLERIC_WEAPON_NAME,5,WEAPON,MACE,900,CRAFT,,TUNABLE,TRUE
EQ_T5_HEAVY_ARMOR,TXT_EQ_T5_HEAVY_ARMOR_NAME,5,ARMOR,HEAVY,810,CRAFT,,TUNABLE,TRUE
EQ_T5_HEAVY_HELMET,TXT_EQ_T5_HEAVY_HELMET_NAME,5,HELMET,HEAVY,495,CRAFT,,TUNABLE,TRUE
EQ_T5_LIGHT_ARMOR,TXT_EQ_T5_LIGHT_ARMOR_NAME,5,ARMOR,LIGHT,810,CRAFT,,TUNABLE,TRUE
EQ_T5_LIGHT_HELMET,TXT_EQ_T5_LIGHT_HELMET_NAME,5,HELMET,LIGHT,495,CRAFT,,TUNABLE,TRUE
EQ_T5_CLOTH_ARMOR,TXT_EQ_T5_CLOTH_ARMOR_NAME,5,ARMOR,CLOTH,810,CRAFT,,TUNABLE,TRUE
EQ_T5_CLOTH_HELMET,TXT_EQ_T5_CLOTH_HELMET_NAME,5,HELMET,CLOTH,495,CRAFT,,TUNABLE,TRUE
EQ_T5_POWER_ACCESSORY,TXT_EQ_T5_POWER_ACCESSORY_NAME,5,ACCESSORY,POWER,405,CRAFT,,TUNABLE,TRUE
EQ_T5_GUARD_ACCESSORY,TXT_EQ_T5_GUARD_ACCESSORY_NAME,5,ACCESSORY,GUARD,405,CRAFT,,TUNABLE,TRUE
EQ_T5_WISDOM_ACCESSORY,TXT_EQ_T5_WISDOM_ACCESSORY_NAME,5,ACCESSORY,WISDOM,405,CRAFT,,TUNABLE,TRUE
EQ_BOSS_HYDRA_WARRIOR,TXT_EQ_BOSS_HYDRA_WARRIOR_NAME,4,WEAPON,SWORD,820,BOSS,RAID_HYDRA,TUNABLE,TRUE
EQ_BOSS_HYDRA_GUARDIAN,TXT_EQ_BOSS_HYDRA_GUARDIAN_NAME,4,ARMOR,HEAVY,780,BOSS,RAID_HYDRA,TUNABLE,TRUE
EQ_BOSS_HYDRA_ARCHER,TXT_EQ_BOSS_HYDRA_ARCHER_NAME,4,WEAPON,BOW,810,BOSS,RAID_HYDRA,TUNABLE,TRUE
EQ_BOSS_HYDRA_MAGE,TXT_EQ_BOSS_HYDRA_MAGE_NAME,4,WEAPON,STAFF,805,BOSS,RAID_HYDRA,TUNABLE,TRUE
EQ_BOSS_HYDRA_CLERIC,TXT_EQ_BOSS_HYDRA_CLERIC_NAME,4,ACCESSORY,WISDOM,760,BOSS,RAID_HYDRA,TUNABLE,TRUE
EQ_BOSS_DRAGON_WARRIOR,TXT_EQ_BOSS_DRAGON_WARRIOR_NAME,5,WEAPON,SWORD,1450,BOSS,RAID_DRAGON,TUNABLE,TRUE
EQ_BOSS_DRAGON_GUARDIAN,TXT_EQ_BOSS_DRAGON_GUARDIAN_NAME,5,ARMOR,HEAVY,1400,BOSS,RAID_DRAGON,TUNABLE,TRUE
EQ_BOSS_DRAGON_ARCHER,TXT_EQ_BOSS_DRAGON_ARCHER_NAME,5,WEAPON,BOW,1420,BOSS,RAID_DRAGON,TUNABLE,TRUE
EQ_BOSS_DRAGON_MAGE,TXT_EQ_BOSS_DRAGON_MAGE_NAME,5,WEAPON,STAFF,1440,BOSS,RAID_DRAGON,TUNABLE,TRUE
EQ_BOSS_DRAGON_CLERIC,TXT_EQ_BOSS_DRAGON_CLERIC_NAME,5,ACCESSORY,WISDOM,1380,BOSS,RAID_DRAGON,TUNABLE,TRUE
```

### 8.13 `facilities.csv` (8 rows)

```csv
facility_id,name_text_key,operation_mode,required_profession_id,status,enabled
FAC_TAVERN,TXT_FAC_TAVERN_NAME,SYSTEM,,TUNABLE,TRUE
FAC_LODGE,TXT_FAC_LODGE_NAME,SYSTEM,,TUNABLE,TRUE
FAC_GUILD,TXT_FAC_GUILD_NAME,SYSTEM,,TUNABLE,TRUE
FAC_STORE,TXT_FAC_STORE_NAME,MANAGED,NPC_MERCHANT,TUNABLE,TRUE
FAC_BLACKSMITH,TXT_FAC_BLACKSMITH_NAME,MANAGED,NPC_BLACKSMITH,TUNABLE,TRUE
FAC_ALCHEMY,TXT_FAC_ALCHEMY_NAME,MANAGED,NPC_ALCHEMIST,TUNABLE,TRUE
FAC_WAREHOUSE,TXT_FAC_WAREHOUSE_NAME,SYSTEM,,TUNABLE,TRUE
FAC_INFIRMARY,TXT_FAC_INFIRMARY_NAME,MANAGED,NPC_HEALER,TUNABLE,TRUE
```

### 8.14 `facility_levels.csv` (32 rows)

```csv
facility_id,level,required_kingdom_stage_id,build_or_upgrade_kingdom_gold,effect_key,effect_text_key,status,enabled
FAC_TAVERN,1,KINGDOM_1,0,FAC_EFFECT_TAVERN_L1,TXT_FAC_TAVERN_L1_EFFECT,TUNABLE,TRUE
FAC_TAVERN,2,KINGDOM_2,2500,FAC_EFFECT_TAVERN_L2,TXT_FAC_TAVERN_L2_EFFECT,TUNABLE,TRUE
FAC_TAVERN,3,KINGDOM_3,12000,FAC_EFFECT_TAVERN_L3,TXT_FAC_TAVERN_L3_EFFECT,TUNABLE,TRUE
FAC_TAVERN,4,KINGDOM_4,45000,FAC_EFFECT_TAVERN_L4,TXT_FAC_TAVERN_L4_EFFECT,TUNABLE,TRUE
FAC_LODGE,1,KINGDOM_1,0,FAC_EFFECT_LODGE_L1,TXT_FAC_LODGE_L1_EFFECT,TUNABLE,TRUE
FAC_LODGE,2,KINGDOM_2,3000,FAC_EFFECT_LODGE_L2,TXT_FAC_LODGE_L2_EFFECT,TUNABLE,TRUE
FAC_LODGE,3,KINGDOM_3,15000,FAC_EFFECT_LODGE_L3,TXT_FAC_LODGE_L3_EFFECT,TUNABLE,TRUE
FAC_LODGE,4,KINGDOM_4,60000,FAC_EFFECT_LODGE_L4,TXT_FAC_LODGE_L4_EFFECT,TUNABLE,TRUE
FAC_GUILD,1,KINGDOM_1,0,FAC_EFFECT_GUILD_L1,TXT_FAC_GUILD_L1_EFFECT,TUNABLE,TRUE
FAC_GUILD,2,KINGDOM_2,4000,FAC_EFFECT_GUILD_L2,TXT_FAC_GUILD_L2_EFFECT,TUNABLE,TRUE
FAC_GUILD,3,KINGDOM_3,18000,FAC_EFFECT_GUILD_L3,TXT_FAC_GUILD_L3_EFFECT,TUNABLE,TRUE
FAC_GUILD,4,KINGDOM_4,70000,FAC_EFFECT_GUILD_L4,TXT_FAC_GUILD_L4_EFFECT,TUNABLE,TRUE
FAC_STORE,1,KINGDOM_1,500,FAC_EFFECT_STORE_L1,TXT_FAC_STORE_L1_EFFECT,TUNABLE,TRUE
FAC_STORE,2,KINGDOM_2,3500,FAC_EFFECT_STORE_L2,TXT_FAC_STORE_L2_EFFECT,TUNABLE,TRUE
FAC_STORE,3,KINGDOM_3,16000,FAC_EFFECT_STORE_L3,TXT_FAC_STORE_L3_EFFECT,TUNABLE,TRUE
FAC_STORE,4,KINGDOM_4,65000,FAC_EFFECT_STORE_L4,TXT_FAC_STORE_L4_EFFECT,TUNABLE,TRUE
FAC_BLACKSMITH,1,KINGDOM_1,800,FAC_EFFECT_BLACKSMITH_L1,TXT_FAC_BLACKSMITH_L1_EFFECT,TUNABLE,TRUE
FAC_BLACKSMITH,2,KINGDOM_2,5000,FAC_EFFECT_BLACKSMITH_L2,TXT_FAC_BLACKSMITH_L2_EFFECT,TUNABLE,TRUE
FAC_BLACKSMITH,3,KINGDOM_3,24000,FAC_EFFECT_BLACKSMITH_L3,TXT_FAC_BLACKSMITH_L3_EFFECT,TUNABLE,TRUE
FAC_BLACKSMITH,4,KINGDOM_4,85000,FAC_EFFECT_BLACKSMITH_L4,TXT_FAC_BLACKSMITH_L4_EFFECT,TUNABLE,TRUE
FAC_ALCHEMY,1,KINGDOM_1,1000,FAC_EFFECT_ALCHEMY_L1,TXT_FAC_ALCHEMY_L1_EFFECT,TUNABLE,TRUE
FAC_ALCHEMY,2,KINGDOM_2,6000,FAC_EFFECT_ALCHEMY_L2,TXT_FAC_ALCHEMY_L2_EFFECT,TUNABLE,TRUE
FAC_ALCHEMY,3,KINGDOM_3,28000,FAC_EFFECT_ALCHEMY_L3,TXT_FAC_ALCHEMY_L3_EFFECT,TUNABLE,TRUE
FAC_ALCHEMY,4,KINGDOM_4,90000,FAC_EFFECT_ALCHEMY_L4,TXT_FAC_ALCHEMY_L4_EFFECT,TUNABLE,TRUE
FAC_WAREHOUSE,1,KINGDOM_1,700,FAC_EFFECT_WAREHOUSE_L1,TXT_FAC_WAREHOUSE_L1_EFFECT,TUNABLE,TRUE
FAC_WAREHOUSE,2,KINGDOM_2,4000,FAC_EFFECT_WAREHOUSE_L2,TXT_FAC_WAREHOUSE_L2_EFFECT,TUNABLE,TRUE
FAC_WAREHOUSE,3,KINGDOM_3,19000,FAC_EFFECT_WAREHOUSE_L3,TXT_FAC_WAREHOUSE_L3_EFFECT,TUNABLE,TRUE
FAC_WAREHOUSE,4,KINGDOM_4,75000,FAC_EFFECT_WAREHOUSE_L4,TXT_FAC_WAREHOUSE_L4_EFFECT,TUNABLE,TRUE
FAC_INFIRMARY,1,KINGDOM_1,1000,FAC_EFFECT_INFIRMARY_L1,TXT_FAC_INFIRMARY_L1_EFFECT,TUNABLE,TRUE
FAC_INFIRMARY,2,KINGDOM_2,5500,FAC_EFFECT_INFIRMARY_L2,TXT_FAC_INFIRMARY_L2_EFFECT,TUNABLE,TRUE
FAC_INFIRMARY,3,KINGDOM_3,26000,FAC_EFFECT_INFIRMARY_L3,TXT_FAC_INFIRMARY_L3_EFFECT,TUNABLE,TRUE
FAC_INFIRMARY,4,KINGDOM_4,82000,FAC_EFFECT_INFIRMARY_L4,TXT_FAC_INFIRMARY_L4_EFFECT,TUNABLE,TRUE
```

### 8.15 `facility_upgrade_materials.csv` (40 rows)

```csv
facility_id,level,item_id,quantity,status,enabled
FAC_TAVERN,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_TAVERN,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_TAVERN,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_TAVERN,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_TAVERN,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_LODGE,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_LODGE,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_LODGE,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_LODGE,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_LODGE,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_GUILD,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_GUILD,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_GUILD,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_GUILD,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_GUILD,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_STORE,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_STORE,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_STORE,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_STORE,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_STORE,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_BLACKSMITH,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_BLACKSMITH,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_BLACKSMITH,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_BLACKSMITH,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_BLACKSMITH,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_ALCHEMY,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_ALCHEMY,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_ALCHEMY,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_ALCHEMY,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_ALCHEMY,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_WAREHOUSE,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_WAREHOUSE,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_WAREHOUSE,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_WAREHOUSE,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_WAREHOUSE,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
FAC_INFIRMARY,2,MAT_R02_DARKWOOD,10,TUNABLE,TRUE
FAC_INFIRMARY,3,MAT_R03_COAL,8,TUNABLE,TRUE
FAC_INFIRMARY,3,MAT_R03_IRON_ORE,15,TUNABLE,TRUE
FAC_INFIRMARY,4,MAT_R04_BOG_CRYSTAL,12,TUNABLE,TRUE
FAC_INFIRMARY,4,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
```

### 8.16 `items.csv` (53 rows)

```csv
item_id,name_text_key,category,tier,source_type,source_id,sell_price,rarity,stack_limit,status,enabled
MAT_R01_SOFTWOOD,TXT_MAT_R01_SOFTWOOD_NAME,CRAFT,1,REGION,REGION_R01,6,COMMON,9999,TUNABLE,TRUE
MAT_R01_WILD_HERB,TXT_MAT_R01_WILD_HERB_NAME,ALCHEMY,1,REGION,REGION_R01,5,COMMON,9999,TUNABLE,TRUE
MAT_R01_SLIME_GEL,TXT_MAT_R01_SLIME_GEL_NAME,ALCHEMY,1,REGION,REGION_R01,8,COMMON,9999,TUNABLE,TRUE
MAT_R01_WOLF_FANG,TXT_MAT_R01_WOLF_FANG_NAME,MONSTER,1,REGION,REGION_R01,12,COMMON,9999,TUNABLE,TRUE
MAT_R01_BOAR_HIDE,TXT_MAT_R01_BOAR_HIDE_NAME,CRAFT,1,REGION,REGION_R01,10,COMMON,9999,TUNABLE,TRUE
MAT_R01_MEADOW_CRYSTAL,TXT_MAT_R01_MEADOW_CRYSTAL_NAME,MAGIC,1,REGION,REGION_R01,18,COMMON,9999,TUNABLE,TRUE
MAT_R02_DARKWOOD,TXT_MAT_R02_DARKWOOD_NAME,CRAFT,2,REGION,REGION_R02,18,COMMON,9999,TUNABLE,TRUE
MAT_R02_MOONLEAF,TXT_MAT_R02_MOONLEAF_NAME,ALCHEMY,2,REGION,REGION_R02,16,COMMON,9999,TUNABLE,TRUE
MAT_R02_SPIDER_SILK,TXT_MAT_R02_SPIDER_SILK_NAME,CRAFT,2,REGION,REGION_R02,22,COMMON,9999,TUNABLE,TRUE
MAT_R02_VENOM_SAC,TXT_MAT_R02_VENOM_SAC_NAME,MONSTER,2,REGION,REGION_R02,28,COMMON,9999,TUNABLE,TRUE
MAT_R02_FOREST_CORE,TXT_MAT_R02_FOREST_CORE_NAME,MAGIC,2,REGION,REGION_R02,35,COMMON,9999,TUNABLE,TRUE
MAT_R02_OGRE_BONE,TXT_MAT_R02_OGRE_BONE_NAME,MONSTER,2,REGION,REGION_R02,40,COMMON,9999,TUNABLE,TRUE
MAT_R03_IRON_ORE,TXT_MAT_R03_IRON_ORE_NAME,ORE,3,REGION,REGION_R03,32,COMMON,9999,TUNABLE,TRUE
MAT_R03_SILVER_ORE,TXT_MAT_R03_SILVER_ORE_NAME,ORE,3,REGION,REGION_R03,48,COMMON,9999,TUNABLE,TRUE
MAT_R03_COAL,TXT_MAT_R03_COAL_NAME,CRAFT,3,REGION,REGION_R03,24,COMMON,9999,TUNABLE,TRUE
MAT_R03_BAT_WING,TXT_MAT_R03_BAT_WING_NAME,MONSTER,3,REGION,REGION_R03,35,COMMON,9999,TUNABLE,TRUE
MAT_R03_GOLEM_FRAGMENT,TXT_MAT_R03_GOLEM_FRAGMENT_NAME,MONSTER,3,REGION,REGION_R03,55,COMMON,9999,TUNABLE,TRUE
MAT_R03_ANCIENT_GEAR,TXT_MAT_R03_ANCIENT_GEAR_NAME,RELIC,3,REGION,REGION_R03,70,RARE,9999,TUNABLE,TRUE
MAT_R04_SWAMP_REED,TXT_MAT_R04_SWAMP_REED_NAME,ALCHEMY,4,REGION,REGION_R04,55,COMMON,9999,TUNABLE,TRUE
MAT_R04_TOXIC_GLAND,TXT_MAT_R04_TOXIC_GLAND_NAME,MONSTER,4,REGION,REGION_R04,72,RARE,9999,TUNABLE,TRUE
MAT_R04_MIASMA_MOSS,TXT_MAT_R04_MIASMA_MOSS_NAME,ALCHEMY,4,REGION,REGION_R04,64,COMMON,9999,TUNABLE,TRUE
MAT_R04_WITCHWATER,TXT_MAT_R04_WITCHWATER_NAME,MAGIC,4,REGION,REGION_R04,88,RARE,9999,TUNABLE,TRUE
MAT_R04_BOG_CRYSTAL,TXT_MAT_R04_BOG_CRYSTAL_NAME,MAGIC,4,REGION,REGION_R04,95,RARE,9999,TUNABLE,TRUE
MAT_R04_TROLL_HIDE,TXT_MAT_R04_TROLL_HIDE_NAME,CRAFT,4,REGION,REGION_R04,84,RARE,9999,TUNABLE,TRUE
MAT_R05_FROST_ORE,TXT_MAT_R05_FROST_ORE_NAME,ORE,5,REGION,REGION_R05,110,RARE,9999,TUNABLE,TRUE
MAT_R05_ICE_BLOOM,TXT_MAT_R05_ICE_BLOOM_NAME,ALCHEMY,5,REGION,REGION_R05,105,RARE,9999,TUNABLE,TRUE
MAT_R05_WRAITH_DUST,TXT_MAT_R05_WRAITH_DUST_NAME,MAGIC,5,REGION,REGION_R05,125,RARE,9999,TUNABLE,TRUE
MAT_R05_YETI_FUR,TXT_MAT_R05_YETI_FUR_NAME,CRAFT,5,REGION,REGION_R05,120,RARE,9999,TUNABLE,TRUE
MAT_R05_ANCIENT_RUNE,TXT_MAT_R05_ANCIENT_RUNE_NAME,RELIC,5,REGION,REGION_R05,155,RARE,9999,TUNABLE,TRUE
MAT_R05_FROZEN_CORE,TXT_MAT_R05_FROZEN_CORE_NAME,MAGIC,5,REGION,REGION_R05,170,RARE,9999,TUNABLE,TRUE
MAT_BOSS_HYDRA_SCALE,TXT_MAT_BOSS_HYDRA_SCALE_NAME,BOSS,4,RAID,RAID_HYDRA,220,EPIC,9999,TUNABLE,TRUE
MAT_BOSS_HYDRA_HEART,TXT_MAT_BOSS_HYDRA_HEART_NAME,BOSS,4,RAID,RAID_HYDRA,360,EPIC,9999,TUNABLE,TRUE
MAT_BOSS_HYDRA_VENOM,TXT_MAT_BOSS_HYDRA_VENOM_NAME,BOSS,4,RAID,RAID_HYDRA,280,EPIC,9999,TUNABLE,TRUE
MAT_BOSS_HYDRA_FANG,TXT_MAT_BOSS_HYDRA_FANG_NAME,BOSS,4,RAID,RAID_HYDRA,260,EPIC,9999,TUNABLE,TRUE
MAT_BOSS_DRAGON_HORN,TXT_MAT_BOSS_DRAGON_HORN_NAME,BOSS,5,RAID,RAID_DRAGON,600,RELIC,9999,TUNABLE,TRUE
MAT_BOSS_DRAGON_SCALE,TXT_MAT_BOSS_DRAGON_SCALE_NAME,BOSS,5,RAID,RAID_DRAGON,580,RELIC,9999,TUNABLE,TRUE
MAT_BOSS_DRAGON_HEART,TXT_MAT_BOSS_DRAGON_HEART_NAME,BOSS,5,RAID,RAID_DRAGON,900,RELIC,9999,TUNABLE,TRUE
MAT_BOSS_ASH_CORE,TXT_MAT_BOSS_ASH_CORE_NAME,BOSS,5,RAID,RAID_DRAGON,650,RELIC,9999,TUNABLE,TRUE
MAT_ENHANCE_1,TXT_MAT_ENHANCE_1_NAME,ENHANCE,1,DISMANTLE,,15,COMMON,9999,TUNABLE,TRUE
MAT_ENHANCE_2,TXT_MAT_ENHANCE_2_NAME,ENHANCE,2,DISMANTLE,,45,COMMON,9999,TUNABLE,TRUE
MAT_ENHANCE_3,TXT_MAT_ENHANCE_3_NAME,ENHANCE,3,DISMANTLE,,120,RARE,9999,TUNABLE,TRUE
MAT_ENHANCE_4,TXT_MAT_ENHANCE_4_NAME,ENHANCE,4,DISMANTLE,,300,RARE,9999,TUNABLE,TRUE
MAT_REFINE_STABILIZER,TXT_MAT_REFINE_STABILIZER_NAME,REFINE,4,ELITE_AND_RAID,,180,EPIC,9999,TUNABLE,TRUE
MAT_PROMO_BRONZE_EMBLEM,TXT_MAT_PROMO_BRONZE_EMBLEM_NAME,PROMOTION,5,PROMOTION_CONTENT,,50,COMMON,9999,TUNABLE,TRUE
MAT_PROMO_SILVER_BADGE,TXT_MAT_PROMO_SILVER_BADGE_NAME,PROMOTION,5,PROMOTION_CONTENT,,140,RARE,9999,TUNABLE,TRUE
MAT_PROMO_ELITE_SIGIL,TXT_MAT_PROMO_ELITE_SIGIL_NAME,PROMOTION,5,PROMOTION_CONTENT,,320,EPIC,9999,TUNABLE,TRUE
MAT_PROMO_HERO_CREST,TXT_MAT_PROMO_HERO_CREST_NAME,PROMOTION,5,PROMOTION_CONTENT,,700,EPIC,9999,TUNABLE,TRUE
MAT_PROMO_LEGEND_CREST,TXT_MAT_PROMO_LEGEND_CREST_NAME,PROMOTION,5,PROMOTION_CONTENT,,1500,RELIC,9999,TUNABLE,TRUE
MAT_PROMO_RARE_CORE,TXT_MAT_PROMO_RARE_CORE_NAME,PROMOTION,5,PROMOTION_CONTENT,,600,EPIC,9999,TUNABLE,TRUE
MAT_PROMO_AWAKENING_STONE,TXT_MAT_PROMO_AWAKENING_STONE_NAME,PROMOTION,5,PROMOTION_CONTENT,,900,EPIC,9999,TUNABLE,TRUE
MAT_PROMO_STAR_SIGIL,TXT_MAT_PROMO_STAR_SIGIL_NAME,PROMOTION,5,PROMOTION_CONTENT,,1800,RELIC,9999,TUNABLE,TRUE
MAT_PROMO_ANCIENT_AWAKENING,TXT_MAT_PROMO_ANCIENT_AWAKENING_NAME,PROMOTION,5,PROMOTION_CONTENT,,2200,RELIC,9999,TUNABLE,TRUE
MAT_PROMO_FATE_CHALICE,TXT_MAT_PROMO_FATE_CHALICE_NAME,PROMOTION,5,PROMOTION_CONTENT,,4000,RELIC,9999,TUNABLE,TRUE
```

### 8.17 `job_skill_unlocks.csv` (15 rows)

```csv
job_id,skill_id,slot_no,unlock_rank_id,status,enabled
JOB_WARRIOR,SK_WAR_HEAVY_SLASH,1,RANK_APPRENTICE,CONFIRMED,TRUE
JOB_WARRIOR,SK_WAR_BATTLE_RUSH,2,RANK_REGULAR,CONFIRMED,TRUE
JOB_WARRIOR,SK_WAR_TENACITY,3,RANK_SKILLED,CONFIRMED,TRUE
JOB_GUARDIAN,SK_GUA_TAUNT,1,RANK_APPRENTICE,CONFIRMED,TRUE
JOB_GUARDIAN,SK_GUA_FORTRESS,2,RANK_REGULAR,CONFIRMED,TRUE
JOB_GUARDIAN,SK_GUA_GUARD_MARK,3,RANK_SKILLED,CONFIRMED,TRUE
JOB_ARCHER,SK_ARC_QUICK_SHOT,1,RANK_APPRENTICE,CONFIRMED,TRUE
JOB_ARCHER,SK_ARC_PIERCE,2,RANK_REGULAR,CONFIRMED,TRUE
JOB_ARCHER,SK_ARC_HUNTER_EYE,3,RANK_SKILLED,CONFIRMED,TRUE
JOB_MAGE,SK_MAG_FIRE_BURST,1,RANK_APPRENTICE,CONFIRMED,TRUE
JOB_MAGE,SK_MAG_FROST_NOVA,2,RANK_REGULAR,CONFIRMED,TRUE
JOB_MAGE,SK_MAG_MANA_FLOW,3,RANK_SKILLED,CONFIRMED,TRUE
JOB_CLERIC,SK_CLE_HEAL,1,RANK_APPRENTICE,CONFIRMED,TRUE
JOB_CLERIC,SK_CLE_BARRIER,2,RANK_REGULAR,CONFIRMED,TRUE
JOB_CLERIC,SK_CLE_BLESSING,3,RANK_SKILLED,CONFIRMED,TRUE
```

### 8.18 `jobs.csv` (5 rows)

```csv
job_id,name_text_key,role,armor_profile,weapon_type,primary_stat,secondary_stat,ai_priority,notes_text_key,status,enabled
JOB_WARRIOR,TXT_JOB_WARRIOR_NAME,BRUISER,HEAVY,SWORD,STR,VIT,LOW_HP_ENEMY,TXT_JOB_WARRIOR_NOTES,CONFIRMED,TRUE
JOB_GUARDIAN,TXT_JOB_GUARDIAN_NAME,TANK,HEAVY,HAMMER_SHIELD,VIT,STR,PROTECT_ALLY,TXT_JOB_GUARDIAN_NOTES,CONFIRMED,TRUE
JOB_ARCHER,TXT_JOB_ARCHER_NAME,RANGED_DPS,LIGHT,BOW,DEX,LUK,WEAKPOINT,TXT_JOB_ARCHER_NOTES,CONFIRMED,TRUE
JOB_MAGE,TXT_JOB_MAGE_NAME,AOE_DPS,CLOTH,STAFF,INT,WIS,CLUSTER,TXT_JOB_MAGE_NOTES,CONFIRMED,TRUE
JOB_CLERIC,TXT_JOB_CLERIC_NAME,HEALER,CLOTH,MACE,WIS,VIT,LOW_HP_ALLY,TXT_JOB_CLERIC_NOTES,CONFIRMED,TRUE
```

### 8.19 `kingdom_stages.csv` (5 rows)

```csv
stage_id,name_text_key,order,active_slots,roster_slots,facility_level_cap,unlock_condition_group_id,final_raid_unlocked,status,enabled
KINGDOM_1,TXT_KINGDOM_1_NAME,1,4,8,1,COND_GRP_KINGDOM_1_UNLOCK,FALSE,CONFIRMED,TRUE
KINGDOM_2,TXT_KINGDOM_2_NAME,2,8,12,2,COND_GRP_KINGDOM_2_UNLOCK,FALSE,TUNABLE,TRUE
KINGDOM_3,TXT_KINGDOM_3_NAME,3,12,18,3,COND_GRP_KINGDOM_3_UNLOCK,FALSE,TUNABLE,TRUE
KINGDOM_4,TXT_KINGDOM_4_NAME,4,16,24,4,COND_GRP_KINGDOM_4_UNLOCK,FALSE,TUNABLE,TRUE
KINGDOM_5,TXT_KINGDOM_5_NAME,5,16,24,4,COND_GRP_KINGDOM_5_UNLOCK,TRUE,TUNABLE,TRUE
```

### 8.20 `localizations.csv` (470 rows)

```csv
locale,text_key,text_value,context,status,enabled
ko-KR,NARRATIVE_FINAL_CLEAR,왕국은 다시 세워졌지만 세계의 위협은 아직 끝나지 않았다.,CORE,CONFIRMED,TRUE
ko-KR,NARRATIVE_OPENING_1,몬스터의 침공으로 왕국은 폐허가 되었다.,CORE,CONFIRMED,TRUE
ko-KR,NARRATIVE_OPENING_2,마지막 거점을 복구하고 용병과 장인을 모아야 한다.,CORE,CONFIRMED,TRUE
ko-KR,TXT_ACTIVE_MERC_CAP_V1_DESCRIPTION,1.0 활동 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_BOSS_DRAGON_NAME,잿빛 고룡,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_BOSS_HYDRA_NAME,역병 히드라,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_CURRENCY_KINGDOM_GOLD_NAME,왕국 골드,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_PREMIUM_FREE_NAME,무료 프리미엄,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_PREMIUM_PAID_NAME,유료 프리미엄,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_SPECIAL_RECRUIT_TICKET_NAME,특별 모집권,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_DEFAULT_HP_RETURN_THRESHOLD_DESCRIPTION,기본 귀환 체력,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_DISMANTLE_ENHANCE_REFUND_DESCRIPTION,강화 재료 환급,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_EQUIPMENT_UPGRADE_THRESHOLD_DESCRIPTION,자동 교체 최소 개선,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_ARCHER_NAME,고룡 날개활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_CLERIC_NAME,재의 성휘,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_GUARDIAN_NAME,잿빛 용린갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_MAGE_NAME,고룡심장 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_WARRIOR_NAME,잿빛 고룡검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_ARCHER_NAME,독사의 눈 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_CLERIC_NAME,정화의 성배,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_GUARDIAN_NAME,히드라 비늘 수호갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_MAGE_NAME,역병 가지 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_WARRIOR_NAME,히드라 송곳니 대검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_ARCHER_WEAPON_NAME,개척자의 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLERIC_WEAPON_NAME,개척자의 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLOTH_ARMOR_NAME,개척자의 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLOTH_HELMET_NAME,개척자의 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_GUARDIAN_WEAPON_NAME,개척자의 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_GUARD_ACCESSORY_NAME,개척자의 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_HEAVY_ARMOR_NAME,개척자의 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_HEAVY_HELMET_NAME,개척자의 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_LIGHT_ARMOR_NAME,개척자의 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_LIGHT_HELMET_NAME,개척자의 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_MAGE_WEAPON_NAME,개척자의 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_POWER_ACCESSORY_NAME,개척자의 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_WARRIOR_WEAPON_NAME,개척자의 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_WISDOM_ACCESSORY_NAME,개척자의 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_ARCHER_WEAPON_NAME,어둠숲 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLERIC_WEAPON_NAME,어둠숲 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLOTH_ARMOR_NAME,어둠숲 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLOTH_HELMET_NAME,어둠숲 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_GUARDIAN_WEAPON_NAME,어둠숲 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_GUARD_ACCESSORY_NAME,어둠숲 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_HEAVY_ARMOR_NAME,어둠숲 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_HEAVY_HELMET_NAME,어둠숲 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_LIGHT_ARMOR_NAME,어둠숲 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_LIGHT_HELMET_NAME,어둠숲 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_MAGE_WEAPON_NAME,어둠숲 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_POWER_ACCESSORY_NAME,어둠숲 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_WARRIOR_WEAPON_NAME,어둠숲 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_WISDOM_ACCESSORY_NAME,어둠숲 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_ARCHER_WEAPON_NAME,철맥 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLERIC_WEAPON_NAME,철맥 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLOTH_ARMOR_NAME,철맥 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLOTH_HELMET_NAME,철맥 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_GUARDIAN_WEAPON_NAME,철맥 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_GUARD_ACCESSORY_NAME,철맥 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_HEAVY_ARMOR_NAME,철맥 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_HEAVY_HELMET_NAME,철맥 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_LIGHT_ARMOR_NAME,철맥 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_LIGHT_HELMET_NAME,철맥 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_MAGE_WEAPON_NAME,철맥 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_POWER_ACCESSORY_NAME,철맥 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_WARRIOR_WEAPON_NAME,철맥 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_WISDOM_ACCESSORY_NAME,철맥 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_ARCHER_WEAPON_NAME,독안개 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLERIC_WEAPON_NAME,독안개 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLOTH_ARMOR_NAME,독안개 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLOTH_HELMET_NAME,독안개 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_GUARDIAN_WEAPON_NAME,독안개 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_GUARD_ACCESSORY_NAME,독안개 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_HEAVY_ARMOR_NAME,독안개 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_HEAVY_HELMET_NAME,독안개 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_LIGHT_ARMOR_NAME,독안개 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_LIGHT_HELMET_NAME,독안개 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_MAGE_WEAPON_NAME,독안개 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_POWER_ACCESSORY_NAME,독안개 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_WARRIOR_WEAPON_NAME,독안개 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_WISDOM_ACCESSORY_NAME,독안개 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_ARCHER_WEAPON_NAME,빙결 유적 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLERIC_WEAPON_NAME,빙결 유적 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLOTH_ARMOR_NAME,빙결 유적 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLOTH_HELMET_NAME,빙결 유적 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_GUARDIAN_WEAPON_NAME,빙결 유적 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_GUARD_ACCESSORY_NAME,빙결 유적 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_HEAVY_ARMOR_NAME,빙결 유적 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_HEAVY_HELMET_NAME,빙결 유적 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_LIGHT_ARMOR_NAME,빙결 유적 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_LIGHT_HELMET_NAME,빙결 유적 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_MAGE_WEAPON_NAME,빙결 유적 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_POWER_ACCESSORY_NAME,빙결 유적 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_WARRIOR_WEAPON_NAME,빙결 유적 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_WISDOM_ACCESSORY_NAME,빙결 유적 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L1_EFFECT,하급 포션,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L2_EFFECT,중급·해독제,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L3_EFFECT,상급·저항약,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L4_EFFECT,레이드 포션,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_NAME,연금술 공방,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L1_EFFECT,T1 제작·분해,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L2_EFFECT,T2·강화 +5,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L3_EFFECT,T3/T4·강화 +8·제련,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L4_EFFECT,T5·보스 장비·강화 +10,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_NAME,대장간,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L1_EFFECT,정식 승급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L2_EFFECT,숙련 승급·지역 정책,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L3_EFFECT,정예 승급·레이드 준비,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L4_EFFECT,영웅·전설 승급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_NAME,모험가 길드,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L1_EFFECT,기본 치료,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L2_EFFECT,치료 시간 -15%,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L3_EFFECT,상태 이상 치료,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L4_EFFECT,치료 시간 -35%,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_NAME,치료소,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L1_EFFECT,활동 4/보유 8,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L2_EFFECT,활동 8/보유 12,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L3_EFFECT,활동 12/보유 18,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L4_EFFECT,활동 16/보유 24,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_NAME,용병 숙소,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L1_EFFECT,기본 매입·판매,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L2_EFFECT,가격 정책·재고 목표,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L3_EFFECT,희귀 장비 취급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L4_EFFECT,보스 장비 진열,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_NAME,상점,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L1_EFFECT,"후보 3명, C/B/A",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L2_EFFECT,"후보 4명, B/A 확률 증가",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L3_EFFECT,"후보 5명, A 확률 증가",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L4_EFFECT,"후보 잠금 2칸, 최고 A 확률",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_NAME,주점,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L1_EFFECT,용량 200,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L2_EFFECT,용량 500·예약 재고,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L3_EFFECT,용량 1200,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L4_EFFECT,용량 3000·필터,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_NAME,창고,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_GRADE_A_NAME,A,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_B_NAME,B,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_C_NAME,C,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_SS_NAME,SS,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_S_NAME,S,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_INVENTORY_RETURN_THRESHOLD_DESCRIPTION,귀환 임계치,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_JOB_ARCHER_NAME,궁수,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_ARCHER_NOTES,원거리 단일 딜러,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_CLERIC_NAME,성직자,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_CLERIC_NOTES,회복·보조,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_GUARDIAN_NAME,수호자,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_GUARDIAN_NOTES,도발·피해 흡수,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_MAGE_NAME,마법사,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_MAGE_NOTES,범위·속성 공격,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_WARRIOR_NAME,전사,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_WARRIOR_NOTES,근접 균형 딜러,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_KINGDOM_1_NAME,폐허 전초기지,KINGDOM_STAGE_NAME,CONFIRMED,TRUE
ko-KR,TXT_KINGDOM_2_NAME,정착 마을,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_3_NAME,요새 도시,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_4_NAME,성채 수도,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_5_NAME,복구된 왕국,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_ASH_CORE_NAME,잿불 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_HEART_NAME,고룡의 심장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_HORN_NAME,고룡의 뿔,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_SCALE_NAME,잿빛 용비늘,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_FANG_NAME,히드라 송곳니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_HEART_NAME,히드라 심장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_SCALE_NAME,히드라 비늘,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_VENOM_NAME,원초 독액,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_1_NAME,하급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_2_NAME,중급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_3_NAME,상급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_4_NAME,최상급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_ANCIENT_AWAKENING_NAME,고대 각성석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_AWAKENING_STONE_NAME,각성석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_BRONZE_EMBLEM_NAME,청동 승급 문장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_ELITE_SIGIL_NAME,정예의 인장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_FATE_CHALICE_NAME,운명의 성배,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_HERO_CREST_NAME,영웅의 증표,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_LEGEND_CREST_NAME,전설의 증표,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_RARE_CORE_NAME,희귀 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_SILVER_BADGE_NAME,은빛 승급패,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_STAR_SIGIL_NAME,별의 인장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_BOAR_HIDE_NAME,멧돼지 가죽,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_MEADOW_CRYSTAL_NAME,초원 결정,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_SLIME_GEL_NAME,슬라임 젤,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_SOFTWOOD_NAME,부드러운 목재,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_WILD_HERB_NAME,들풀 약초,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_WOLF_FANG_NAME,늑대 송곳니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_DARKWOOD_NAME,어둠목,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_FOREST_CORE_NAME,숲의 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_MOONLEAF_NAME,달빛잎,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_OGRE_BONE_NAME,오우거 뼈,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_SPIDER_SILK_NAME,거미 비단,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_VENOM_SAC_NAME,독주머니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_ANCIENT_GEAR_NAME,고대 톱니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_BAT_WING_NAME,동굴 박쥐 날개,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_COAL_NAME,고열 석탄,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_GOLEM_FRAGMENT_NAME,골렘 파편,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_IRON_ORE_NAME,철광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_SILVER_ORE_NAME,은광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_BOG_CRYSTAL_NAME,늪 결정,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_MIASMA_MOSS_NAME,독안개 이끼,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_SWAMP_REED_NAME,늪지 갈대,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_TOXIC_GLAND_NAME,맹독선,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_TROLL_HIDE_NAME,늪 트롤 가죽,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_WITCHWATER_NAME,마녀수,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_ANCIENT_RUNE_NAME,고대 룬,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_FROST_ORE_NAME,서리 광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_FROZEN_CORE_NAME,빙결 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_ICE_BLOOM_NAME,얼음꽃,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_WRAITH_DUST_NAME,망령 가루,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_YETI_FUR_NAME,설인 모피,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_REFINE_STABILIZER_NAME,제련 안정제,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_BOAR_NAME,들멧돼지,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_ELITE_DIRE_WOLF_NAME,광포한 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_GOBLIN_NAME,떠돌이 고블린,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_SLIME_NAME,초원 슬라임,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_WOLF_NAME,회색 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_ELITE_OGRE_NAME,숲 오우거 족장,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_LIZARD_NAME,숲도마뱀,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_SHAMAN_NAME,고블린 주술사,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_SPIDER_NAME,그늘 거미,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_TREANT_NAME,어린 트렌트,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_BAT_NAME,철광 박쥐,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_CRAWLER_NAME,동굴 포식자,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_ELITE_GUARDIAN_NAME,광산 수호자,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_GOLEM_NAME,철 골렘,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_MINER_NAME,망령 광부,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_ELITE_HYDRA_SPAWN_NAME,히드라 유생,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_FROG_NAME,맹독 개구리,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_PLAGUE_BEAST_NAME,역병 마수,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_TROLL_NAME,늪 트롤,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_WITCH_NAME,늪지 마녀,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_ELITE_FROST_GIANT_NAME,서리 거인,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_ICE_WOLF_NAME,빙설 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_RUNE_SENTINEL_NAME,룬 파수병,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_WRAITH_NAME,서리 망령,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_YETI_NAME,설인,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_ALCHEMIST_NAME,연금술사,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_APPRENTICE_NAME,견습,NPC_PROFICIENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_ARTISAN_NAME,장인,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_BLACKSMITH_NAME,대장장이,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_HEALER_NAME,치료사,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_MASTER_NAME,명장,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_MERCHANT_NAME,상점 주인,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_SKILLED_NAME,숙련,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_OFFLINE_HUNT_EFFICIENCY_DESCRIPTION,온라인 대비 기본 효율,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_OFFLINE_MAX_HOURS_DESCRIPTION,오프라인 정산 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_BRAVE_DESCRIPTION,위험한 지역과 강한 적을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_BRAVE_NAME,용감함,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_CAUTIOUS_DESCRIPTION,생존 가능성이 높은 지역과 이른 귀환을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_CAUTIOUS_NAME,신중함,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_COLLECTOR_DESCRIPTION,희귀 재료가 있는 지역을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_COLLECTOR_NAME,수집벽,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_FRUGAL_DESCRIPTION,큰 개선이 있을 때만 장비를 구매한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_FRUGAL_NAME,절약가,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_GEARHEAD_DESCRIPTION,작은 개선에도 장비 구매를 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_GEARHEAD_NAME,장비광,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_PRACTICAL_DESCRIPTION,가격과 성능을 균형 있게 판단한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_PRACTICAL_NAME,실용주의,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_ANTIDOTE_NAME,해독제,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_FROST_RESIST_NAME,빙결 저항약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_LARGE_NAME,상급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_MEDIUM_NAME,중급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_SMALL_NAME,하급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_POISON_RESIST_NAME,독 저항약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_RAID_POWER_NAME,레이드 전투약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_QUALITY_COMMON_NAME,일반,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_FINE_NAME,정교,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_LEGACY_NAME,유산,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_RARE_NAME,희귀,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_RELIC_NAME,유물,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_BODY_NAME,몸통,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_BODY_PRIORITY,방어구 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HEART_NAME,심장,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HEART_PRIORITY,최상위 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HORN_NAME,뿔,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HORN_PRIORITY,무기 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_WING_NAME,날개,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_WING_PRIORITY,공중 패턴 차단,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_NAME,잿빛 고룡,RAID_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_BODY_NAME,몸통,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_BODY_PRIORITY,비늘 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEAD_NAME,머리,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEAD_PRIORITY,독 공격 약화,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEART_NAME,심장,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEART_PRIORITY,희귀 핵심 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_NAME,역병 히드라,RAID_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_PARTY_MAX_DESCRIPTION,레이드 최대,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_RAID_PARTY_MIN_DESCRIPTION,레이드 최소,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_RANK_APPRENTICE_NAME,수습 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_ELITE_NAME,정예 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_HERO_NAME,영웅,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_LEGEND_NAME,전설,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_REGULAR_NAME,정식 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_SKILLED_NAME,숙련 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RARE_EQUIPMENT_AUTO_PROTECT_DESCRIPTION,희귀 이상 자동 보호,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_ARCHER_NAME,고룡 날개활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_CLERIC_NAME,재의 성휘,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_GUARDIAN_NAME,잿빛 용린갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_MAGE_NAME,고룡심장 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_WARRIOR_NAME,잿빛 고룡검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_ARCHER_NAME,독사의 눈 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_CLERIC_NAME,정화의 성배,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_GUARDIAN_NAME,히드라 비늘 수호갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_MAGE_NAME,역병 가지 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_WARRIOR_NAME,히드라 송곳니 대검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_ARCHER_WEAPON_NAME,개척자의 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLERIC_WEAPON_NAME,개척자의 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLOTH_ARMOR_NAME,개척자의 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLOTH_HELMET_NAME,개척자의 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_GUARDIAN_WEAPON_NAME,개척자의 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_GUARD_ACCESSORY_NAME,개척자의 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_HEAVY_ARMOR_NAME,개척자의 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_HEAVY_HELMET_NAME,개척자의 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_LIGHT_ARMOR_NAME,개척자의 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_LIGHT_HELMET_NAME,개척자의 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_MAGE_WEAPON_NAME,개척자의 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_POWER_ACCESSORY_NAME,개척자의 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_WARRIOR_WEAPON_NAME,개척자의 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_WISDOM_ACCESSORY_NAME,개척자의 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_ARCHER_WEAPON_NAME,어둠숲 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLERIC_WEAPON_NAME,어둠숲 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLOTH_ARMOR_NAME,어둠숲 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLOTH_HELMET_NAME,어둠숲 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_GUARDIAN_WEAPON_NAME,어둠숲 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_GUARD_ACCESSORY_NAME,어둠숲 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_HEAVY_ARMOR_NAME,어둠숲 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_HEAVY_HELMET_NAME,어둠숲 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_LIGHT_ARMOR_NAME,어둠숲 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_LIGHT_HELMET_NAME,어둠숲 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_MAGE_WEAPON_NAME,어둠숲 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_POWER_ACCESSORY_NAME,어둠숲 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_WARRIOR_WEAPON_NAME,어둠숲 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_WISDOM_ACCESSORY_NAME,어둠숲 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_ARCHER_WEAPON_NAME,철맥 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLERIC_WEAPON_NAME,철맥 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLOTH_ARMOR_NAME,철맥 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLOTH_HELMET_NAME,철맥 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_GUARDIAN_WEAPON_NAME,철맥 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_GUARD_ACCESSORY_NAME,철맥 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_HEAVY_ARMOR_NAME,철맥 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_HEAVY_HELMET_NAME,철맥 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_LIGHT_ARMOR_NAME,철맥 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_LIGHT_HELMET_NAME,철맥 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_MAGE_WEAPON_NAME,철맥 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_POWER_ACCESSORY_NAME,철맥 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_WARRIOR_WEAPON_NAME,철맥 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_WISDOM_ACCESSORY_NAME,철맥 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_ARCHER_WEAPON_NAME,독안개 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLERIC_WEAPON_NAME,독안개 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLOTH_ARMOR_NAME,독안개 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLOTH_HELMET_NAME,독안개 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_GUARDIAN_WEAPON_NAME,독안개 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_GUARD_ACCESSORY_NAME,독안개 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_HEAVY_ARMOR_NAME,독안개 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_HEAVY_HELMET_NAME,독안개 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_LIGHT_ARMOR_NAME,독안개 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_LIGHT_HELMET_NAME,독안개 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_MAGE_WEAPON_NAME,독안개 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_POWER_ACCESSORY_NAME,독안개 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_WARRIOR_WEAPON_NAME,독안개 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_WISDOM_ACCESSORY_NAME,독안개 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_ARCHER_WEAPON_NAME,빙결 유적 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLERIC_WEAPON_NAME,빙결 유적 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLOTH_ARMOR_NAME,빙결 유적 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLOTH_HELMET_NAME,빙결 유적 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_GUARDIAN_WEAPON_NAME,빙결 유적 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_GUARD_ACCESSORY_NAME,빙결 유적 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_HEAVY_ARMOR_NAME,빙결 유적 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_HEAVY_HELMET_NAME,빙결 유적 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_LIGHT_ARMOR_NAME,빙결 유적 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_LIGHT_HELMET_NAME,빙결 유적 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_MAGE_WEAPON_NAME,빙결 유적 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_POWER_ACCESSORY_NAME,빙결 유적 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_WARRIOR_WEAPON_NAME,빙결 유적 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_WISDOM_ACCESSORY_NAME,빙결 유적 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_ANTIDOTE_NAME,해독제,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_FROST_RESIST_NAME,빙결 저항약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_LARGE_NAME,상급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_MEDIUM_NAME,중급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_SMALL_NAME,하급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_POISON_RESIST_NAME,독 저항약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_RAID_POWER_NAME,레이드 전투약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_ATK_POWER_NAME,공격력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_BOSS_NAME,보스 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_CRIT_NAME,치명타 확률,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_DEF_NAME,방어력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_FIRE_NAME,화염 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_FROST_NAME,빙결 저항,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_HP_NAME,최대 체력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_MATERIAL_NAME,재료 추가 획득,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_PART_NAME,부위 파괴 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_POISON_NAME,독 피해·저항,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_RARE_FIND_NAME,희귀 재료 발견,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REGION_R01_NAME,왕국 외곽 초원,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R02_NAME,어둠숲,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R03_NAME,버려진 광산,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R04_NAME,독안개 늪지,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R05_NAME,얼어붙은 유적,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_ROSTER_CAP_V1_DESCRIPTION,1.0 보유 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_SK_ARC_HUNTER_EYE_DESCRIPTION,정예·보스 약점 피해 증가,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_HUNTER_EYE_NAME,사냥꾼의 눈,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_PIERCE_DESCRIPTION,직선상의 적 관통,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_PIERCE_NAME,관통 화살,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_QUICK_SHOT_DESCRIPTION,빠른 원거리 공격,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_QUICK_SHOT_NAME,속사,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BARRIER_DESCRIPTION,피해 흡수 보호막,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BARRIER_NAME,보호막,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BLESSING_DESCRIPTION,공격·방어 보조 버프,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BLESSING_NAME,축복,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_HEAL_DESCRIPTION,가장 체력이 낮은 아군 회복,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_HEAL_NAME,치유,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_FORTRESS_DESCRIPTION,일정 시간 방어력 증가,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_FORTRESS_NAME,요새 태세,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_GUARD_MARK_DESCRIPTION,낮은 체력 아군 피해 일부 대신 받음,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_GUARD_MARK_NAME,수호 표식,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_TAUNT_DESCRIPTION,주변 적의 위협도를 집중,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_TAUNT_NAME,방패 도발,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FIRE_BURST_DESCRIPTION,범위 화염 피해,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FIRE_BURST_NAME,화염 폭발,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FROST_NOVA_DESCRIPTION,범위 피해와 이동 둔화,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FROST_NOVA_NAME,서리 파동,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_MANA_FLOW_DESCRIPTION,스킬 재사용 대기시간 소폭 감소,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_MANA_FLOW_NAME,마력 순환,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_BATTLE_RUSH_DESCRIPTION,대상에게 돌진하고 짧게 경직,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_BATTLE_RUSH_NAME,전투 돌진,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_HEAVY_SLASH_DESCRIPTION,대상에게 강한 물리 피해,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_HEAVY_SLASH_NAME,강타,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_TENACITY_DESCRIPTION,체력이 낮을수록 피해 감소,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_TENACITY_NAME,강인함,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_STATUS_BARRIER_NAME,보호막,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_BLESSING_NAME,축복,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_BURN_NAME,화상,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_POISON_NAME,중독,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_SLOW_NAME,둔화,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_TAUNT_NAME,도발,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_HIGH_DESCRIPTION,고가 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_LOW_DESCRIPTION,저가 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_STANDARD_DESCRIPTION,표준 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_ARCANE_DESCRIPTION,마법·신성 효과가 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_ARCANE_NAME,마력 친화,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_BOSS_HUNTER_DESCRIPTION,보스 피해가 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_BOSS_HUNTER_NAME,거수 사냥꾼,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_FROSTBORN_DESCRIPTION,빙결 피해와 둔화가 감소한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_FROSTBORN_NAME,설원의 자식,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_KEEN_EYE_DESCRIPTION,치명타 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_KEEN_EYE_NAME,예리한 눈,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_LUCKY_DESCRIPTION,희귀 재료 발견 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_LUCKY_NAME,행운아,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_POISON_RESIST_DESCRIPTION,독 피해와 지속시간이 감소한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_POISON_RESIST_NAME,독 내성,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SCAVENGER_DESCRIPTION,재료 추가 획득 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SCAVENGER_NAME,수집가,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STRONG_DESCRIPTION,힘이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STRONG_NAME,완력,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STURDY_DESCRIPTION,최대 체력이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STURDY_NAME,튼튼함,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SURVIVOR_DESCRIPTION,더 안전한 시점에 귀환한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SURVIVOR_NAME,생존 본능,TRAIT_NAME,TUNABLE,TRUE
ko-KR,UI_FACILITY_STOP_MATERIAL,재료 부족으로 생산이 중단되었습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_FACILITY_STOP_NPC,담당 NPC가 없어 시설이 멈췄습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_CRAFT,제작,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_KINGDOM,왕국,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_MENU,메뉴,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_MERCENARY,용병,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_RECRUIT,모집,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_REGION,지역,CORE,CONFIRMED,TRUE
ko-KR,UI_PROMOTION_READY,승급 가능,CORE,CONFIRMED,TRUE
ko-KR,UI_RAID_ROLE_WARNING,권장 역할이 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_RECRUIT_ROSTER_FULL,용병 보유 슬롯이 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_REGION_LOCK_RANK,용병 성장 랭크가 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_EMPTY,표시할 내용이 없습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_ERROR,처리 중 문제가 발생했습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_LOADING,불러오는 중입니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_OFFLINE,오프라인 상태입니다.,CORE,CONFIRMED,TRUE
```

### 8.21 `loot_entries.csv` (88 rows)

```csv
loot_table_id,entry_no,reward_type,reward_id,probability,weight,min_quantity,max_quantity,condition_id,status,enabled
LOOT_R01_1,1,ITEM,MAT_R01_SOFTWOOD,0.75,,1,2,,TUNABLE,TRUE
LOOT_R01_1,2,ITEM,MAT_R01_SLIME_GEL,0.22,,1,1,,TUNABLE,TRUE
LOOT_R01_1,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T1_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R01_2,1,ITEM,MAT_R01_WILD_HERB,0.75,,1,2,,TUNABLE,TRUE
LOOT_R01_2,2,ITEM,MAT_R01_WOLF_FANG,0.22,,1,1,,TUNABLE,TRUE
LOOT_R01_2,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T1_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R01_3,1,ITEM,MAT_R01_SLIME_GEL,0.75,,1,2,,TUNABLE,TRUE
LOOT_R01_3,2,ITEM,MAT_R01_BOAR_HIDE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R01_3,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T1_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R01_4,1,ITEM,MAT_R01_WOLF_FANG,0.75,,1,2,,TUNABLE,TRUE
LOOT_R01_4,2,ITEM,MAT_R01_MEADOW_CRYSTAL,0.22,,1,1,,TUNABLE,TRUE
LOOT_R01_4,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T1_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R01_ELITE,1,ITEM,MAT_R01_BOAR_HIDE,1.0,,2,4,,TUNABLE,TRUE
LOOT_R01_ELITE,2,ITEM,MAT_R01_MEADOW_CRYSTAL,0.65,,1,2,,TUNABLE,TRUE
LOOT_R01_ELITE,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T1_ANY,0.18,,1,1,,TUNABLE,TRUE
LOOT_R01_ELITE,4,ITEM,MAT_PROMO_BRONZE_EMBLEM,0.2,,1,1,,TUNABLE,TRUE
LOOT_R02_1,1,ITEM,MAT_R02_DARKWOOD,0.75,,1,2,,TUNABLE,TRUE
LOOT_R02_1,2,ITEM,MAT_R02_SPIDER_SILK,0.22,,1,1,,TUNABLE,TRUE
LOOT_R02_1,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T2_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R02_2,1,ITEM,MAT_R02_MOONLEAF,0.75,,1,2,,TUNABLE,TRUE
LOOT_R02_2,2,ITEM,MAT_R02_VENOM_SAC,0.22,,1,1,,TUNABLE,TRUE
LOOT_R02_2,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T2_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R02_3,1,ITEM,MAT_R02_SPIDER_SILK,0.75,,1,2,,TUNABLE,TRUE
LOOT_R02_3,2,ITEM,MAT_R02_FOREST_CORE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R02_3,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T2_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R02_4,1,ITEM,MAT_R02_VENOM_SAC,0.75,,1,2,,TUNABLE,TRUE
LOOT_R02_4,2,ITEM,MAT_R02_OGRE_BONE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R02_4,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T2_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R02_ELITE,1,ITEM,MAT_R02_FOREST_CORE,1.0,,2,4,,TUNABLE,TRUE
LOOT_R02_ELITE,2,ITEM,MAT_R02_OGRE_BONE,0.65,,1,2,,TUNABLE,TRUE
LOOT_R02_ELITE,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T2_ANY,0.18,,1,1,,TUNABLE,TRUE
LOOT_R02_ELITE,4,ITEM,MAT_PROMO_SILVER_BADGE,0.2,,1,1,,TUNABLE,TRUE
LOOT_R03_1,1,ITEM,MAT_R03_IRON_ORE,0.75,,1,2,,TUNABLE,TRUE
LOOT_R03_1,2,ITEM,MAT_R03_COAL,0.22,,1,1,,TUNABLE,TRUE
LOOT_R03_1,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T3_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R03_2,1,ITEM,MAT_R03_SILVER_ORE,0.75,,1,2,,TUNABLE,TRUE
LOOT_R03_2,2,ITEM,MAT_R03_BAT_WING,0.22,,1,1,,TUNABLE,TRUE
LOOT_R03_2,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T3_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R03_3,1,ITEM,MAT_R03_COAL,0.75,,1,2,,TUNABLE,TRUE
LOOT_R03_3,2,ITEM,MAT_R03_GOLEM_FRAGMENT,0.22,,1,1,,TUNABLE,TRUE
LOOT_R03_3,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T3_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R03_4,1,ITEM,MAT_R03_BAT_WING,0.75,,1,2,,TUNABLE,TRUE
LOOT_R03_4,2,ITEM,MAT_R03_ANCIENT_GEAR,0.22,,1,1,,TUNABLE,TRUE
LOOT_R03_4,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T3_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R03_ELITE,1,ITEM,MAT_R03_GOLEM_FRAGMENT,1.0,,2,4,,TUNABLE,TRUE
LOOT_R03_ELITE,2,ITEM,MAT_R03_ANCIENT_GEAR,0.65,,1,2,,TUNABLE,TRUE
LOOT_R03_ELITE,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T3_ANY,0.18,,1,1,,TUNABLE,TRUE
LOOT_R03_ELITE,4,ITEM,MAT_PROMO_ELITE_SIGIL,0.2,,1,1,,TUNABLE,TRUE
LOOT_R04_1,1,ITEM,MAT_R04_SWAMP_REED,0.75,,1,2,,TUNABLE,TRUE
LOOT_R04_1,2,ITEM,MAT_R04_MIASMA_MOSS,0.22,,1,1,,TUNABLE,TRUE
LOOT_R04_1,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T4_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R04_2,1,ITEM,MAT_R04_TOXIC_GLAND,0.75,,1,2,,TUNABLE,TRUE
LOOT_R04_2,2,ITEM,MAT_R04_WITCHWATER,0.22,,1,1,,TUNABLE,TRUE
LOOT_R04_2,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T4_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R04_3,1,ITEM,MAT_R04_MIASMA_MOSS,0.75,,1,2,,TUNABLE,TRUE
LOOT_R04_3,2,ITEM,MAT_R04_BOG_CRYSTAL,0.22,,1,1,,TUNABLE,TRUE
LOOT_R04_3,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T4_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R04_4,1,ITEM,MAT_R04_WITCHWATER,0.75,,1,2,,TUNABLE,TRUE
LOOT_R04_4,2,ITEM,MAT_R04_TROLL_HIDE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R04_4,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T4_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R04_ELITE,1,ITEM,MAT_R04_BOG_CRYSTAL,1.0,,2,4,,TUNABLE,TRUE
LOOT_R04_ELITE,2,ITEM,MAT_R04_TROLL_HIDE,0.65,,1,2,,TUNABLE,TRUE
LOOT_R04_ELITE,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T4_ANY,0.18,,1,1,,TUNABLE,TRUE
LOOT_R05_1,1,ITEM,MAT_R05_FROST_ORE,0.75,,1,2,,TUNABLE,TRUE
LOOT_R05_1,2,ITEM,MAT_R05_WRAITH_DUST,0.22,,1,1,,TUNABLE,TRUE
LOOT_R05_1,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T5_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R05_2,1,ITEM,MAT_R05_ICE_BLOOM,0.75,,1,2,,TUNABLE,TRUE
LOOT_R05_2,2,ITEM,MAT_R05_YETI_FUR,0.22,,1,1,,TUNABLE,TRUE
LOOT_R05_2,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T5_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R05_3,1,ITEM,MAT_R05_WRAITH_DUST,0.75,,1,2,,TUNABLE,TRUE
LOOT_R05_3,2,ITEM,MAT_R05_ANCIENT_RUNE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R05_3,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T5_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R05_4,1,ITEM,MAT_R05_YETI_FUR,0.75,,1,2,,TUNABLE,TRUE
LOOT_R05_4,2,ITEM,MAT_R05_FROZEN_CORE,0.22,,1,1,,TUNABLE,TRUE
LOOT_R05_4,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T5_ANY,0.025,,1,1,,TUNABLE,TRUE
LOOT_R05_ELITE,1,ITEM,MAT_R05_ANCIENT_RUNE,1.0,,2,4,,TUNABLE,TRUE
LOOT_R05_ELITE,2,ITEM,MAT_R05_FROZEN_CORE,0.65,,1,2,,TUNABLE,TRUE
LOOT_R05_ELITE,3,RANDOM_EQUIPMENT_TIER,RANDOM_EQ_T5_ANY,0.18,,1,1,,TUNABLE,TRUE
LOOT_RAID_HYDRA,1,ITEM,MAT_BOSS_HYDRA_SCALE,1,,2,4,COND_HYDRA_BODY_BREAK,TUNABLE,TRUE
LOOT_RAID_HYDRA,2,ITEM,MAT_BOSS_HYDRA_HEART,0.55,,1,1,COND_HYDRA_HEART_EXPOSED,TUNABLE,TRUE
LOOT_RAID_HYDRA,3,ITEM,MAT_BOSS_HYDRA_VENOM,0.9,,1,3,COND_HYDRA_HEAD_BREAK,TUNABLE,TRUE
LOOT_RAID_HYDRA,4,ITEM,MAT_BOSS_HYDRA_FANG,0.65,,1,2,COND_HYDRA_HEAD_BREAK,TUNABLE,TRUE
LOOT_RAID_HYDRA,5,ITEM,MAT_PROMO_HERO_CREST,0.5,,1,1,COND_RAID_HYDRA_CLEAR,TUNABLE,TRUE
LOOT_RAID_DRAGON,1,ITEM,MAT_BOSS_DRAGON_HORN,0.75,,1,2,COND_DRAGON_HORN_BREAK,TUNABLE,TRUE
LOOT_RAID_DRAGON,2,ITEM,MAT_BOSS_DRAGON_SCALE,1,,2,5,COND_DRAGON_BODY_BREAK,TUNABLE,TRUE
LOOT_RAID_DRAGON,3,ITEM,MAT_BOSS_DRAGON_HEART,0.45,,1,1,COND_DRAGON_HEART_EXPOSED,TUNABLE,TRUE
LOOT_RAID_DRAGON,4,ITEM,MAT_BOSS_ASH_CORE,0.8,,1,3,COND_DRAGON_WING_BREAK,TUNABLE,TRUE
LOOT_RAID_DRAGON,5,ITEM,MAT_PROMO_LEGEND_CREST,0.65,,1,1,COND_RAID_DRAGON_CLEAR,TUNABLE,TRUE
```

### 8.22 `loot_tables.csv` (27 rows)

```csv
loot_table_id,draw_mode,draw_count,status,enabled
LOOT_R01_1,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R01_2,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R01_3,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R01_4,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R01_ELITE,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R02_1,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R02_2,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R02_3,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R02_4,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R02_ELITE,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R03_1,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R03_2,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R03_3,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R03_4,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R03_ELITE,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R04_1,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R04_2,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R04_3,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R04_4,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R04_ELITE,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R05_1,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R05_2,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R05_3,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R05_4,INDEPENDENT,1,TUNABLE,TRUE
LOOT_R05_ELITE,INDEPENDENT,1,TUNABLE,TRUE
LOOT_RAID_HYDRA,INDEPENDENT,1,TUNABLE,TRUE
LOOT_RAID_DRAGON,INDEPENDENT,1,TUNABLE,TRUE
```

### 8.23 `mercenary_appearance_pool_entries.csv` (5 rows)

```csv
appearance_pool_id,job_id,entry_no,appearance_asset_id,weight,status,enabled
APPEARANCE_POOL_V1,JOB_WARRIOR,1,ASSET_MERC_PLACEHOLDER_WARRIOR_V1,100,CONFIRMED,TRUE
APPEARANCE_POOL_V1,JOB_GUARDIAN,1,ASSET_MERC_PLACEHOLDER_GUARDIAN_V1,100,CONFIRMED,TRUE
APPEARANCE_POOL_V1,JOB_ARCHER,1,ASSET_MERC_PLACEHOLDER_ARCHER_V1,100,CONFIRMED,TRUE
APPEARANCE_POOL_V1,JOB_MAGE,1,ASSET_MERC_PLACEHOLDER_MAGE_V1,100,CONFIRMED,TRUE
APPEARANCE_POOL_V1,JOB_CLERIC,1,ASSET_MERC_PLACEHOLDER_CLERIC_V1,100,CONFIRMED,TRUE
```

### 8.24 `mercenary_generation_profiles.csv` (1 rows)

```csv
generation_profile_id,algorithm_id,initial_rank_id,name_pool_id,appearance_pool_id,personality_policy,fixed_personality_id,growth_seed_min,growth_seed_max,status,enabled
GEN_MERC_STANDARD_V1,GENERATED_MERCENARY_V1,RANK_APPRENTICE,NAME_POOL_KO_V1,APPEARANCE_POOL_V1,WEIGHTED_ALL,,0,18446744073709551615,CONFIRMED,TRUE
```

### 8.25 `mercenary_grades.csv` (5 rows)

```csv
grade_id,name_text_key,order,base_stat_multiplier,growth_multiplier,initial_trait_count,promotion_cost_multiplier,tavern_eligible,special_pool_eligible,ui_color,status,enabled
GRADE_C,TXT_GRADE_C_NAME,1,1.0,1.0,1,1.0,TRUE,FALSE,#8A8A8A,CONFIRMED,TRUE
GRADE_B,TXT_GRADE_B_NAME,2,1.04,1.02,1,1.1,TRUE,FALSE,#5F9B62,CONFIRMED,TRUE
GRADE_A,TXT_GRADE_A_NAME,3,1.08,1.04,2,1.25,TRUE,TRUE,#4E78B8,CONFIRMED,TRUE
GRADE_S,TXT_GRADE_S_NAME,4,1.13,1.07,2,1.5,FALSE,TRUE,#8464B8,CONFIRMED,TRUE
GRADE_SS,TXT_GRADE_SS_NAME,5,1.18,1.1,3,1.8,FALSE,TRUE,#D3A349,CONFIRMED,TRUE
```

### 8.26 `mercenary_name_pool_entries.csv` (20 rows)

```csv
name_pool_id,locale,entry_no,display_name,weight,status,enabled
NAME_POOL_KO_V1,ko-KR,1,레온,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,2,미라,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,3,카인,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,4,세라,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,5,로웬,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,6,이안,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,7,루나,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,8,테오,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,9,아린,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,10,다온,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,11,하린,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,12,벨라,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,13,로안,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,14,유나,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,15,시온,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,16,라엘,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,17,에린,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,18,노아,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,19,리안,100,TUNABLE,TRUE
NAME_POOL_KO_V1,ko-KR,20,소라,100,TUNABLE,TRUE
```

### 8.27 `mercenary_ranks.csv` (6 rows)

```csv
rank_id,name_text_key,order,max_level,min_region_tier,skill_slot_count,trait_slot_bonus,raid_access,promotion_to,promotion_token_id,base_personal_gold,contribution_required,additional_condition,status,enabled
RANK_APPRENTICE,TXT_RANK_APPRENTICE_NAME,1,20,1,1,0,FALSE,RANK_REGULAR,MAT_PROMO_BRONZE_EMBLEM,500,100,HUNT_RETURN_3,CONFIRMED,TRUE
RANK_REGULAR,TXT_RANK_REGULAR_NAME,2,30,2,2,0,FALSE,RANK_SKILLED,MAT_PROMO_SILVER_BADGE,2000,500,REGION_2_BATTLES_20,CONFIRMED,TRUE
RANK_SKILLED,TXT_RANK_SKILLED_NAME,3,40,3,3,1,FALSE,RANK_ELITE,MAT_PROMO_ELITE_SIGIL,8000,1500,ELITE_BATTLES_10,CONFIRMED,TRUE
RANK_ELITE,TXT_RANK_ELITE_NAME,4,50,4,3,1,TRUE,RANK_HERO,MAT_PROMO_HERO_CREST,25000,4000,BOSS_CONTRIBUTION_3,CONFIRMED,TRUE
RANK_HERO,TXT_RANK_HERO_NAME,5,60,5,4,2,TRUE,RANK_LEGEND,MAT_PROMO_LEGEND_CREST,80000,10000,RAID_CLEAR_3,CONFIRMED,TRUE
RANK_LEGEND,TXT_RANK_LEGEND_NAME,6,70,6,4,2,TRUE,,,0,0,END_RANK,CONFIRMED,TRUE
```

### 8.28 `monsters.csv` (27 rows)

```csv
monster_id,name_text_key,region_id,type,level,hp,attack,defense,xp,bounty_personal_gold,loot_table_id,behavior_tag,spawn_weight,raid_id,status,enabled
MON_R01_SLIME,TXT_MON_R01_SLIME_NAME,REGION_R01,NORMAL,11,131,12,3,18,7,LOOT_R01_1,MELEE,24,,TUNABLE,TRUE
MON_R01_WOLF,TXT_MON_R01_WOLF_NAME,REGION_R01,NORMAL,12,144,13,4,18,7,LOOT_R01_2,MELEE,24,,TUNABLE,TRUE
MON_R01_BOAR,TXT_MON_R01_BOAR_NAME,REGION_R01,NORMAL,13,156,14,4,18,7,LOOT_R01_3,MELEE,24,,TUNABLE,TRUE
MON_R01_GOBLIN,TXT_MON_R01_GOBLIN_NAME,REGION_R01,NORMAL,14,168,15,5,18,7,LOOT_R01_4,MELEE,24,,TUNABLE,TRUE
MON_R01_ELITE_DIRE_WOLF,TXT_MON_R01_ELITE_DIRE_WOLF_NAME,REGION_R01,ELITE,18,450,28,10,80,35,LOOT_R01_ELITE,ELITE,4,,TUNABLE,TRUE
MON_R02_SPIDER,TXT_MON_R02_SPIDER_NAME,REGION_R02,NORMAL,21,290,26,8,39,15,LOOT_R02_1,MELEE,24,,TUNABLE,TRUE
MON_R02_TREANT,TXT_MON_R02_TREANT_NAME,REGION_R02,NORMAL,22,316,29,9,39,15,LOOT_R02_2,MELEE,24,,TUNABLE,TRUE
MON_R02_SHAMAN,TXT_MON_R02_SHAMAN_NAME,REGION_R02,NORMAL,23,343,32,10,39,15,LOOT_R02_3,RANGED,24,,TUNABLE,TRUE
MON_R02_LIZARD,TXT_MON_R02_LIZARD_NAME,REGION_R02,NORMAL,24,369,34,11,39,15,LOOT_R02_4,MELEE,24,,TUNABLE,TRUE
MON_R02_ELITE_OGRE,TXT_MON_R02_ELITE_OGRE_NAME,REGION_R02,ELITE,28,990,61,22,176,77,LOOT_R02_ELITE,ELITE,4,,TUNABLE,TRUE
MON_R03_BAT,TXT_MON_R03_BAT_NAME,REGION_R03,NORMAL,31,567,52,16,77,30,LOOT_R03_1,MELEE,24,,TUNABLE,TRUE
MON_R03_CRAWLER,TXT_MON_R03_CRAWLER_NAME,REGION_R03,NORMAL,32,619,57,18,77,30,LOOT_R03_2,MELEE,24,,TUNABLE,TRUE
MON_R03_GOLEM,TXT_MON_R03_GOLEM_NAME,REGION_R03,NORMAL,33,670,62,20,77,30,LOOT_R03_3,MELEE,24,,TUNABLE,TRUE
MON_R03_MINER,TXT_MON_R03_MINER_NAME,REGION_R03,NORMAL,34,722,67,21,77,30,LOOT_R03_4,MELEE,24,,TUNABLE,TRUE
MON_R03_ELITE_GUARDIAN,TXT_MON_R03_ELITE_GUARDIAN_NAME,REGION_R03,ELITE,38,1935,120,43,344,150,LOOT_R03_ELITE,ELITE,4,,TUNABLE,TRUE
MON_R04_FROG,TXT_MON_R04_FROG_NAME,REGION_R04,NORMAL,41,1003,92,29,136,53,LOOT_R04_1,MELEE,24,,TUNABLE,TRUE
MON_R04_WITCH,TXT_MON_R04_WITCH_NAME,REGION_R04,NORMAL,42,1094,101,32,136,53,LOOT_R04_2,RANGED,24,,TUNABLE,TRUE
MON_R04_PLAGUE_BEAST,TXT_MON_R04_PLAGUE_BEAST_NAME,REGION_R04,NORMAL,43,1185,110,35,136,53,LOOT_R04_3,MELEE,24,,TUNABLE,TRUE
MON_R04_TROLL,TXT_MON_R04_TROLL_NAME,REGION_R04,NORMAL,44,1276,120,38,136,53,LOOT_R04_4,MELEE,24,,TUNABLE,TRUE
MON_R04_ELITE_HYDRA_SPAWN,TXT_MON_R04_ELITE_HYDRA_SPAWN_NAME,REGION_R04,ELITE,48,3420,212,76,608,266,LOOT_R04_ELITE,ELITE,4,,TUNABLE,TRUE
MON_R05_ICE_WOLF,TXT_MON_R05_ICE_WOLF_NAME,REGION_R05,NORMAL,51,1649,152,48,225,87,LOOT_R05_1,MELEE,24,,TUNABLE,TRUE
MON_R05_WRAITH,TXT_MON_R05_WRAITH_NAME,REGION_R05,NORMAL,52,1800,167,53,225,87,LOOT_R05_2,MELEE,24,,TUNABLE,TRUE
MON_R05_YETI,TXT_MON_R05_YETI_NAME,REGION_R05,NORMAL,53,1950,182,58,225,87,LOOT_R05_3,MELEE,24,,TUNABLE,TRUE
MON_R05_RUNE_SENTINEL,TXT_MON_R05_RUNE_SENTINEL_NAME,REGION_R05,NORMAL,54,2100,197,63,225,87,LOOT_R05_4,MELEE,24,,TUNABLE,TRUE
MON_R05_ELITE_FROST_GIANT,TXT_MON_R05_ELITE_FROST_GIANT_NAME,REGION_R05,ELITE,58,5625,350,125,1000,437,LOOT_R05_ELITE,ELITE,4,,TUNABLE,TRUE
BOSS_HYDRA,TXT_BOSS_HYDRA_NAME,REGION_R04,RAID,58,65000,950,320,8000,4000,LOOT_RAID_HYDRA,POISON_MULTIHEAD,0,RAID_HYDRA,TUNABLE,TRUE
BOSS_DRAGON,TXT_BOSS_DRAGON_NAME,REGION_R05,RAID,75,180000,1850,650,25000,12000,LOOT_RAID_DRAGON,FIRE_FLYING_PARTS,0,RAID_DRAGON,TUNABLE,TRUE
```

### 8.29 `npc_professions.csv` (4 rows)

```csv
profession_id,name_text_key,facility_id,work_unit,primary_effect,status,enabled
NPC_MERCHANT,TXT_NPC_MERCHANT_NAME,FAC_STORE,TRANSACTION,판매 속도·고가 상품 취급,CONFIRMED,TRUE
NPC_BLACKSMITH,TXT_NPC_BLACKSMITH_NAME,FAC_BLACKSMITH,CRAFT_SECOND,장비 품질·재료 효율,CONFIRMED,TRUE
NPC_ALCHEMIST,TXT_NPC_ALCHEMIST_NAME,FAC_ALCHEMY,CRAFT_SECOND,포션 품질·재료 효율,CONFIRMED,TRUE
NPC_HEALER,TXT_NPC_HEALER_NAME,FAC_INFIRMARY,HEALING,치료 속도·상태 이상,CONFIRMED,TRUE
```

### 8.30 `npc_proficiency_levels.csv` (4 rows)

```csv
proficiency_id,name_text_key,order,xp_required,speed_multiplier,material_efficiency,quality_bonus,status,enabled
NPC_APPRENTICE,TXT_NPC_APPRENTICE_NAME,1,0,1.0,1.0,0.0,CONFIRMED,TRUE
NPC_SKILLED,TXT_NPC_SKILLED_NAME,2,1000,1.12,0.96,0.05,TUNABLE,TRUE
NPC_ARTISAN,TXT_NPC_ARTISAN_NAME,3,5000,1.28,0.9,0.12,TUNABLE,TRUE
NPC_MASTER,TXT_NPC_MASTER_NAME,4,20000,1.5,0.82,0.22,TUNABLE,TRUE
```

### 8.31 `offline_reward_rules.csv` (6 rows)

```csv
rule_id,settlement_type,max_seconds,efficiency,reward_group_id,status,enabled
OFFLINE_HUNT_V1,HUNT,28800,0.75,,TUNABLE,TRUE
OFFLINE_FACILITY_V1,FACILITY,28800,1.0,,TUNABLE,TRUE
OFFLINE_NPC_PROFICIENCY_V1,NPC_PROFICIENCY,28800,1.0,,TUNABLE,TRUE
OFFLINE_POTION_CONSUMPTION_V1,POTION_CONSUMPTION,28800,1.0,,TUNABLE,TRUE
OFFLINE_INJURY_RECOVERY_V1,INJURY_RECOVERY,28800,1.0,,TUNABLE,TRUE
OFFLINE_PROMOTION_REVIEW_V1,PROMOTION_REVIEW,28800,1.0,,TUNABLE,TRUE
```

### 8.32 `personalities.csv` (6 rows)

```csv
personality_id,name_text_key,buy_threshold_multiplier,risk_tolerance,return_hp_threshold,preferred_behavior,description_text_key,status,enabled
PERSONALITY_PRACTICAL,TXT_PERSONALITY_PRACTICAL_NAME,1.0,1.0,0.3,BALANCED,TXT_PERSONALITY_PRACTICAL_DESCRIPTION,TUNABLE,TRUE
PERSONALITY_FRUGAL,TXT_PERSONALITY_FRUGAL_NAME,1.2,0.9,0.35,SAVE_GOLD,TXT_PERSONALITY_FRUGAL_DESCRIPTION,TUNABLE,TRUE
PERSONALITY_GEARHEAD,TXT_PERSONALITY_GEARHEAD_NAME,0.82,1.0,0.3,BUY_EQUIPMENT,TXT_PERSONALITY_GEARHEAD_DESCRIPTION,TUNABLE,TRUE
PERSONALITY_BRAVE,TXT_PERSONALITY_BRAVE_NAME,1.0,1.18,0.22,HIGH_RISK_REGION,TXT_PERSONALITY_BRAVE_DESCRIPTION,TUNABLE,TRUE
PERSONALITY_CAUTIOUS,TXT_PERSONALITY_CAUTIOUS_NAME,1.05,0.82,0.42,SAFE_REGION,TXT_PERSONALITY_CAUTIOUS_DESCRIPTION,TUNABLE,TRUE
PERSONALITY_COLLECTOR,TXT_PERSONALITY_COLLECTOR_NAME,1.0,0.95,0.32,RARE_MATERIAL_REGION,TXT_PERSONALITY_COLLECTOR_DESCRIPTION,TUNABLE,TRUE
```

### 8.33 `potions.csv` (7 rows)

```csv
potion_id,name_text_key,effect_type,effect_value,duration_sec,auto_use_condition,tier,status,enabled
POT_HEAL_SMALL,TXT_POT_HEAL_SMALL_NAME,HEAL_FLAT,120,0,HP_LT_45,1,TUNABLE,TRUE
POT_HEAL_MEDIUM,TXT_POT_HEAL_MEDIUM_NAME,HEAL_FLAT,360,0,HP_LT_45,2,TUNABLE,TRUE
POT_HEAL_LARGE,TXT_POT_HEAL_LARGE_NAME,HEAL_FLAT,900,0,HP_LT_45,4,TUNABLE,TRUE
POT_ANTIDOTE,TXT_POT_ANTIDOTE_NAME,CURE_POISON,1,0,POISON_STACK_GE_2,2,TUNABLE,TRUE
POT_POISON_RESIST,TXT_POT_POISON_RESIST_NAME,POISON_RESIST,0.3,180,REGION_SWAMP,4,TUNABLE,TRUE
POT_FROST_RESIST,TXT_POT_FROST_RESIST_NAME,FROST_RESIST,0.3,180,REGION_FROST,5,TUNABLE,TRUE
POT_RAID_POWER,TXT_POT_RAID_POWER_NAME,BOSS_DAMAGE,0.12,300,RAID,5,TUNABLE,TRUE
```

### 8.34 `progression_flags.csv` (3 rows)

```csv
progression_flag_id,scope,repeatable,status,enabled
FLAG_V1_ENDING,PROFILE,FALSE,CONFIRMED,TRUE
FLAG_RAID_DRAGON_VARIANTS,PROFILE,FALSE,CONFIRMED,TRUE
FLAG_TUTORIAL_FIRST_INJURY_INSTANT_TREATMENT,PROFILE,FALSE,CONFIRMED,TRUE
```

### 8.35 `promotion_grade_requirements.csv` (6 rows)

```csv
grade_id,from_rank_id,to_rank_id,item_id,item_quantity,status,enabled
GRADE_A,RANK_ELITE,RANK_HERO,MAT_PROMO_RARE_CORE,1,TUNABLE,TRUE
GRADE_A,RANK_HERO,RANK_LEGEND,MAT_PROMO_RARE_CORE,3,TUNABLE,TRUE
GRADE_S,RANK_ELITE,RANK_HERO,MAT_PROMO_AWAKENING_STONE,1,TUNABLE,TRUE
GRADE_S,RANK_HERO,RANK_LEGEND,MAT_PROMO_STAR_SIGIL,1,TUNABLE,TRUE
GRADE_SS,RANK_ELITE,RANK_HERO,MAT_PROMO_ANCIENT_AWAKENING,1,TUNABLE,TRUE
GRADE_SS,RANK_HERO,RANK_LEGEND,MAT_PROMO_FATE_CHALICE,1,TUNABLE,TRUE
```

### 8.36 `raid_difficulties.csv` (6 rows)

```csv
raid_id,difficulty,recommended_power,reward_group_id,status,enabled
RAID_HYDRA,NORMAL,1800,REWARD_RAID_HYDRA_NORMAL_REPEAT,TUNABLE,TRUE
RAID_HYDRA,HARD,2700,REWARD_RAID_HYDRA_HARD_REPEAT,TUNABLE,TRUE
RAID_HYDRA,CORRUPTED,4050,REWARD_RAID_HYDRA_CORRUPTED_REPEAT,TUNABLE,TRUE
RAID_DRAGON,NORMAL,3000,REWARD_RAID_DRAGON_NORMAL_REPEAT,TUNABLE,TRUE
RAID_DRAGON,HARD,4500,REWARD_RAID_DRAGON_HARD_REPEAT,TUNABLE,TRUE
RAID_DRAGON,CORRUPTED,6750,REWARD_RAID_DRAGON_CORRUPTED_REPEAT,TUNABLE,TRUE
```

### 8.37 `raid_part_effects.csv` (6 rows)

```csv
effect_id,effect_type,effect_key,value,status,enabled
POISON_BREATH_REDUCED,ABILITY_SCALE,POISON_BREATH_REDUCED,,TUNABLE,TRUE
DEFENSE_DOWN,STAT_MODIFIER,DEFENSE_DOWN,,TUNABLE,TRUE
ENRAGE,STATE_TRANSITION,ENRAGE,,TUNABLE,TRUE
CHARGE_REDUCED,ABILITY_SCALE,CHARGE_REDUCED,,TUNABLE,TRUE
FLIGHT_DISABLED,STATE_TRANSITION,FLIGHT_DISABLED,,TUNABLE,TRUE
FINAL_ENRAGE,STATE_TRANSITION,FINAL_ENRAGE,,TUNABLE,TRUE
```

### 8.38 `raid_parts.csv` (7 rows)

```csv
raid_id,part_id,name_text_key,max_hp_ratio,break_condition_group_id,break_reward_group_id,behavior_change,priority_hint_text_key,status,enabled
RAID_HYDRA,HYDRA_HEAD,TXT_RAID_HYDRA_HYDRA_HEAD_NAME,0.22,COND_GRP_HYDRA_HEAD_BREAK,REWARD_HYDRA_HEAD_BREAK,POISON_BREATH_REDUCED,TXT_RAID_HYDRA_HYDRA_HEAD_PRIORITY,TUNABLE,TRUE
RAID_HYDRA,HYDRA_BODY,TXT_RAID_HYDRA_HYDRA_BODY_NAME,0.38,COND_GRP_HYDRA_BODY_BREAK,REWARD_HYDRA_BODY_BREAK,DEFENSE_DOWN,TXT_RAID_HYDRA_HYDRA_BODY_PRIORITY,TUNABLE,TRUE
RAID_HYDRA,HYDRA_HEART,TXT_RAID_HYDRA_HYDRA_HEART_NAME,0.16,COND_GRP_HYDRA_HEART_EXPOSED,REWARD_HYDRA_HEART_EXPOSED,ENRAGE,TXT_RAID_HYDRA_HYDRA_HEART_PRIORITY,TUNABLE,TRUE
RAID_DRAGON,DRAGON_HORN,TXT_RAID_DRAGON_DRAGON_HORN_NAME,0.16,COND_GRP_DRAGON_HORN_BREAK,REWARD_DRAGON_HORN_BREAK,CHARGE_REDUCED,TXT_RAID_DRAGON_DRAGON_HORN_PRIORITY,TUNABLE,TRUE
RAID_DRAGON,DRAGON_WING,TXT_RAID_DRAGON_DRAGON_WING_NAME,0.24,COND_GRP_DRAGON_WING_BREAK,REWARD_DRAGON_WING_BREAK,FLIGHT_DISABLED,TXT_RAID_DRAGON_DRAGON_WING_PRIORITY,TUNABLE,TRUE
RAID_DRAGON,DRAGON_BODY,TXT_RAID_DRAGON_DRAGON_BODY_NAME,0.4,COND_GRP_DRAGON_BODY_BREAK,REWARD_DRAGON_BODY_BREAK,DEFENSE_DOWN,TXT_RAID_DRAGON_DRAGON_BODY_PRIORITY,TUNABLE,TRUE
RAID_DRAGON,DRAGON_HEART,TXT_RAID_DRAGON_DRAGON_HEART_NAME,0.14,COND_GRP_DRAGON_HEART_EXPOSED,REWARD_DRAGON_HEART_EXPOSED,FINAL_ENRAGE,TXT_RAID_DRAGON_DRAGON_HEART_PRIORITY,TUNABLE,TRUE
```

### 8.39 `raids.csv` (2 rows)

```csv
raid_id,name_text_key,boss_monster_id,min_rank_id,party_min,party_max,unlock_condition_group_id,first_clear_reward_group_id,time_limit_sec,status,enabled
RAID_HYDRA,TXT_RAID_HYDRA_NAME,BOSS_HYDRA,RANK_ELITE,6,8,COND_GRP_RAID_HYDRA_UNLOCK,REWARD_RAID_HYDRA_FIRST_CLEAR,300,TUNABLE,TRUE
RAID_DRAGON,TXT_RAID_DRAGON_NAME,BOSS_DRAGON,RANK_HERO,6,8,COND_GRP_RAID_DRAGON_UNLOCK,REWARD_RAID_DRAGON_FIRST_CLEAR,360,TUNABLE,TRUE
```

### 8.40 `random_equipment_tier_specs.csv` (5 rows)

```csv
spec_id,tier,slot_policy,fixed_slot,job_policy,fixed_job_id,quality_profile_id,status,enabled
RANDOM_EQ_T1_ANY,1,ANY,,KILLER_JOB,,QUALITY_DROP_T1_V1,CONFIRMED,TRUE
RANDOM_EQ_T2_ANY,2,ANY,,KILLER_JOB,,QUALITY_DROP_T2_V1,CONFIRMED,TRUE
RANDOM_EQ_T3_ANY,3,ANY,,KILLER_JOB,,QUALITY_DROP_T3_V1,CONFIRMED,TRUE
RANDOM_EQ_T4_ANY,4,ANY,,KILLER_JOB,,QUALITY_DROP_T4_V1,CONFIRMED,TRUE
RANDOM_EQ_T5_ANY,5,ANY,,KILLER_JOB,,QUALITY_DROP_T5_V1,CONFIRMED,TRUE
```

### 8.41 `recipe_materials.csv` (183 rows)

```csv
recipe_id,material_no,item_id,quantity,status,enabled
REC_EQ_T1_WARRIOR_WEAPON,1,MAT_R01_SOFTWOOD,4,TUNABLE,TRUE
REC_EQ_T1_WARRIOR_WEAPON,2,MAT_R01_WOLF_FANG,2,TUNABLE,TRUE
REC_EQ_T1_GUARDIAN_WEAPON,1,MAT_R01_SOFTWOOD,4,TUNABLE,TRUE
REC_EQ_T1_GUARDIAN_WEAPON,2,MAT_R01_WOLF_FANG,2,TUNABLE,TRUE
REC_EQ_T1_ARCHER_WEAPON,1,MAT_R01_SOFTWOOD,4,TUNABLE,TRUE
REC_EQ_T1_ARCHER_WEAPON,2,MAT_R01_WOLF_FANG,2,TUNABLE,TRUE
REC_EQ_T1_MAGE_WEAPON,1,MAT_R01_MEADOW_CRYSTAL,3,TUNABLE,TRUE
REC_EQ_T1_MAGE_WEAPON,2,MAT_R01_SOFTWOOD,3,TUNABLE,TRUE
REC_EQ_T1_CLERIC_WEAPON,1,MAT_R01_MEADOW_CRYSTAL,3,TUNABLE,TRUE
REC_EQ_T1_CLERIC_WEAPON,2,MAT_R01_SOFTWOOD,3,TUNABLE,TRUE
REC_EQ_T1_HEAVY_ARMOR,1,MAT_R01_BOAR_HIDE,5,TUNABLE,TRUE
REC_EQ_T1_HEAVY_ARMOR,2,MAT_R01_SOFTWOOD,3,TUNABLE,TRUE
REC_EQ_T1_HEAVY_HELMET,1,MAT_R01_BOAR_HIDE,3,TUNABLE,TRUE
REC_EQ_T1_HEAVY_HELMET,2,MAT_R01_WOLF_FANG,1,TUNABLE,TRUE
REC_EQ_T1_LIGHT_ARMOR,1,MAT_R01_BOAR_HIDE,5,TUNABLE,TRUE
REC_EQ_T1_LIGHT_ARMOR,2,MAT_R01_SOFTWOOD,3,TUNABLE,TRUE
REC_EQ_T1_LIGHT_HELMET,1,MAT_R01_BOAR_HIDE,3,TUNABLE,TRUE
REC_EQ_T1_LIGHT_HELMET,2,MAT_R01_WOLF_FANG,1,TUNABLE,TRUE
REC_EQ_T1_CLOTH_ARMOR,1,MAT_R01_MEADOW_CRYSTAL,5,TUNABLE,TRUE
REC_EQ_T1_CLOTH_ARMOR,2,MAT_R01_SOFTWOOD,3,TUNABLE,TRUE
REC_EQ_T1_CLOTH_HELMET,1,MAT_R01_MEADOW_CRYSTAL,3,TUNABLE,TRUE
REC_EQ_T1_CLOTH_HELMET,2,MAT_R01_WOLF_FANG,1,TUNABLE,TRUE
REC_EQ_T1_POWER_ACCESSORY,1,MAT_R01_SOFTWOOD,2,TUNABLE,TRUE
REC_EQ_T1_POWER_ACCESSORY,2,MAT_R01_WOLF_FANG,3,TUNABLE,TRUE
REC_EQ_T1_GUARD_ACCESSORY,1,MAT_R01_BOAR_HIDE,3,TUNABLE,TRUE
REC_EQ_T1_GUARD_ACCESSORY,2,MAT_R01_SOFTWOOD,2,TUNABLE,TRUE
REC_EQ_T1_WISDOM_ACCESSORY,1,MAT_R01_MEADOW_CRYSTAL,3,TUNABLE,TRUE
REC_EQ_T1_WISDOM_ACCESSORY,2,MAT_R01_SOFTWOOD,2,TUNABLE,TRUE
REC_EQ_T2_WARRIOR_WEAPON,1,MAT_R02_DARKWOOD,5,TUNABLE,TRUE
REC_EQ_T2_WARRIOR_WEAPON,2,MAT_R02_FOREST_CORE,3,TUNABLE,TRUE
REC_EQ_T2_GUARDIAN_WEAPON,1,MAT_R02_DARKWOOD,5,TUNABLE,TRUE
REC_EQ_T2_GUARDIAN_WEAPON,2,MAT_R02_FOREST_CORE,3,TUNABLE,TRUE
REC_EQ_T2_ARCHER_WEAPON,1,MAT_R02_DARKWOOD,5,TUNABLE,TRUE
REC_EQ_T2_ARCHER_WEAPON,2,MAT_R02_FOREST_CORE,3,TUNABLE,TRUE
REC_EQ_T2_MAGE_WEAPON,1,MAT_R02_DARKWOOD,4,TUNABLE,TRUE
REC_EQ_T2_MAGE_WEAPON,2,MAT_R02_OGRE_BONE,4,TUNABLE,TRUE
REC_EQ_T2_CLERIC_WEAPON,1,MAT_R02_DARKWOOD,4,TUNABLE,TRUE
REC_EQ_T2_CLERIC_WEAPON,2,MAT_R02_OGRE_BONE,4,TUNABLE,TRUE
REC_EQ_T2_HEAVY_ARMOR,1,MAT_R02_DARKWOOD,4,TUNABLE,TRUE
REC_EQ_T2_HEAVY_ARMOR,2,MAT_R02_SPIDER_SILK,6,TUNABLE,TRUE
REC_EQ_T2_HEAVY_HELMET,1,MAT_R02_FOREST_CORE,2,TUNABLE,TRUE
REC_EQ_T2_HEAVY_HELMET,2,MAT_R02_SPIDER_SILK,4,TUNABLE,TRUE
REC_EQ_T2_LIGHT_ARMOR,1,MAT_R02_DARKWOOD,4,TUNABLE,TRUE
REC_EQ_T2_LIGHT_ARMOR,2,MAT_R02_SPIDER_SILK,6,TUNABLE,TRUE
REC_EQ_T2_LIGHT_HELMET,1,MAT_R02_FOREST_CORE,2,TUNABLE,TRUE
REC_EQ_T2_LIGHT_HELMET,2,MAT_R02_SPIDER_SILK,4,TUNABLE,TRUE
REC_EQ_T2_CLOTH_ARMOR,1,MAT_R02_DARKWOOD,4,TUNABLE,TRUE
REC_EQ_T2_CLOTH_ARMOR,2,MAT_R02_OGRE_BONE,6,TUNABLE,TRUE
REC_EQ_T2_CLOTH_HELMET,1,MAT_R02_FOREST_CORE,2,TUNABLE,TRUE
REC_EQ_T2_CLOTH_HELMET,2,MAT_R02_OGRE_BONE,4,TUNABLE,TRUE
REC_EQ_T2_POWER_ACCESSORY,1,MAT_R02_DARKWOOD,3,TUNABLE,TRUE
REC_EQ_T2_POWER_ACCESSORY,2,MAT_R02_FOREST_CORE,4,TUNABLE,TRUE
REC_EQ_T2_GUARD_ACCESSORY,1,MAT_R02_DARKWOOD,3,TUNABLE,TRUE
REC_EQ_T2_GUARD_ACCESSORY,2,MAT_R02_SPIDER_SILK,4,TUNABLE,TRUE
REC_EQ_T2_WISDOM_ACCESSORY,1,MAT_R02_DARKWOOD,3,TUNABLE,TRUE
REC_EQ_T2_WISDOM_ACCESSORY,2,MAT_R02_OGRE_BONE,4,TUNABLE,TRUE
REC_EQ_T3_WARRIOR_WEAPON,1,MAT_R03_IRON_ORE,6,TUNABLE,TRUE
REC_EQ_T3_WARRIOR_WEAPON,2,MAT_R03_SILVER_ORE,3,TUNABLE,TRUE
REC_EQ_T3_GUARDIAN_WEAPON,1,MAT_R03_IRON_ORE,6,TUNABLE,TRUE
REC_EQ_T3_GUARDIAN_WEAPON,2,MAT_R03_SILVER_ORE,3,TUNABLE,TRUE
REC_EQ_T3_ARCHER_WEAPON,1,MAT_R03_IRON_ORE,6,TUNABLE,TRUE
REC_EQ_T3_ARCHER_WEAPON,2,MAT_R03_SILVER_ORE,3,TUNABLE,TRUE
REC_EQ_T3_MAGE_WEAPON,1,MAT_R03_ANCIENT_GEAR,5,TUNABLE,TRUE
REC_EQ_T3_MAGE_WEAPON,2,MAT_R03_IRON_ORE,5,TUNABLE,TRUE
REC_EQ_T3_CLERIC_WEAPON,1,MAT_R03_ANCIENT_GEAR,5,TUNABLE,TRUE
REC_EQ_T3_CLERIC_WEAPON,2,MAT_R03_IRON_ORE,5,TUNABLE,TRUE
REC_EQ_T3_HEAVY_ARMOR,1,MAT_R03_GOLEM_FRAGMENT,7,TUNABLE,TRUE
REC_EQ_T3_HEAVY_ARMOR,2,MAT_R03_IRON_ORE,5,TUNABLE,TRUE
REC_EQ_T3_HEAVY_HELMET,1,MAT_R03_GOLEM_FRAGMENT,5,TUNABLE,TRUE
REC_EQ_T3_HEAVY_HELMET,2,MAT_R03_SILVER_ORE,2,TUNABLE,TRUE
REC_EQ_T3_LIGHT_ARMOR,1,MAT_R03_GOLEM_FRAGMENT,7,TUNABLE,TRUE
REC_EQ_T3_LIGHT_ARMOR,2,MAT_R03_IRON_ORE,5,TUNABLE,TRUE
REC_EQ_T3_LIGHT_HELMET,1,MAT_R03_GOLEM_FRAGMENT,5,TUNABLE,TRUE
REC_EQ_T3_LIGHT_HELMET,2,MAT_R03_SILVER_ORE,2,TUNABLE,TRUE
REC_EQ_T3_CLOTH_ARMOR,1,MAT_R03_ANCIENT_GEAR,7,TUNABLE,TRUE
REC_EQ_T3_CLOTH_ARMOR,2,MAT_R03_IRON_ORE,5,TUNABLE,TRUE
REC_EQ_T3_CLOTH_HELMET,1,MAT_R03_ANCIENT_GEAR,5,TUNABLE,TRUE
REC_EQ_T3_CLOTH_HELMET,2,MAT_R03_SILVER_ORE,2,TUNABLE,TRUE
REC_EQ_T3_POWER_ACCESSORY,1,MAT_R03_IRON_ORE,4,TUNABLE,TRUE
REC_EQ_T3_POWER_ACCESSORY,2,MAT_R03_SILVER_ORE,5,TUNABLE,TRUE
REC_EQ_T3_GUARD_ACCESSORY,1,MAT_R03_GOLEM_FRAGMENT,5,TUNABLE,TRUE
REC_EQ_T3_GUARD_ACCESSORY,2,MAT_R03_IRON_ORE,4,TUNABLE,TRUE
REC_EQ_T3_WISDOM_ACCESSORY,1,MAT_R03_ANCIENT_GEAR,5,TUNABLE,TRUE
REC_EQ_T3_WISDOM_ACCESSORY,2,MAT_R03_IRON_ORE,4,TUNABLE,TRUE
REC_EQ_T4_WARRIOR_WEAPON,1,MAT_R04_BOG_CRYSTAL,7,TUNABLE,TRUE
REC_EQ_T4_WARRIOR_WEAPON,2,MAT_R04_TOXIC_GLAND,4,TUNABLE,TRUE
REC_EQ_T4_GUARDIAN_WEAPON,1,MAT_R04_BOG_CRYSTAL,7,TUNABLE,TRUE
REC_EQ_T4_GUARDIAN_WEAPON,2,MAT_R04_TOXIC_GLAND,4,TUNABLE,TRUE
REC_EQ_T4_ARCHER_WEAPON,1,MAT_R04_BOG_CRYSTAL,7,TUNABLE,TRUE
REC_EQ_T4_ARCHER_WEAPON,2,MAT_R04_TOXIC_GLAND,4,TUNABLE,TRUE
REC_EQ_T4_MAGE_WEAPON,1,MAT_R04_BOG_CRYSTAL,6,TUNABLE,TRUE
REC_EQ_T4_MAGE_WEAPON,2,MAT_R04_WITCHWATER,6,TUNABLE,TRUE
REC_EQ_T4_CLERIC_WEAPON,1,MAT_R04_BOG_CRYSTAL,6,TUNABLE,TRUE
REC_EQ_T4_CLERIC_WEAPON,2,MAT_R04_WITCHWATER,6,TUNABLE,TRUE
REC_EQ_T4_HEAVY_ARMOR,1,MAT_R04_BOG_CRYSTAL,6,TUNABLE,TRUE
REC_EQ_T4_HEAVY_ARMOR,2,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
REC_EQ_T4_HEAVY_HELMET,1,MAT_R04_TOXIC_GLAND,3,TUNABLE,TRUE
REC_EQ_T4_HEAVY_HELMET,2,MAT_R04_TROLL_HIDE,6,TUNABLE,TRUE
REC_EQ_T4_LIGHT_ARMOR,1,MAT_R04_BOG_CRYSTAL,6,TUNABLE,TRUE
REC_EQ_T4_LIGHT_ARMOR,2,MAT_R04_TROLL_HIDE,8,TUNABLE,TRUE
REC_EQ_T4_LIGHT_HELMET,1,MAT_R04_TOXIC_GLAND,3,TUNABLE,TRUE
REC_EQ_T4_LIGHT_HELMET,2,MAT_R04_TROLL_HIDE,6,TUNABLE,TRUE
REC_EQ_T4_CLOTH_ARMOR,1,MAT_R04_BOG_CRYSTAL,6,TUNABLE,TRUE
REC_EQ_T4_CLOTH_ARMOR,2,MAT_R04_WITCHWATER,8,TUNABLE,TRUE
REC_EQ_T4_CLOTH_HELMET,1,MAT_R04_TOXIC_GLAND,3,TUNABLE,TRUE
REC_EQ_T4_CLOTH_HELMET,2,MAT_R04_WITCHWATER,6,TUNABLE,TRUE
REC_EQ_T4_POWER_ACCESSORY,1,MAT_R04_BOG_CRYSTAL,5,TUNABLE,TRUE
REC_EQ_T4_POWER_ACCESSORY,2,MAT_R04_TOXIC_GLAND,6,TUNABLE,TRUE
REC_EQ_T4_GUARD_ACCESSORY,1,MAT_R04_BOG_CRYSTAL,5,TUNABLE,TRUE
REC_EQ_T4_GUARD_ACCESSORY,2,MAT_R04_TROLL_HIDE,6,TUNABLE,TRUE
REC_EQ_T4_WISDOM_ACCESSORY,1,MAT_R04_BOG_CRYSTAL,5,TUNABLE,TRUE
REC_EQ_T4_WISDOM_ACCESSORY,2,MAT_R04_WITCHWATER,6,TUNABLE,TRUE
REC_EQ_T5_WARRIOR_WEAPON,1,MAT_R05_FROST_ORE,8,TUNABLE,TRUE
REC_EQ_T5_WARRIOR_WEAPON,2,MAT_R05_FROZEN_CORE,4,TUNABLE,TRUE
REC_EQ_T5_GUARDIAN_WEAPON,1,MAT_R05_FROST_ORE,8,TUNABLE,TRUE
REC_EQ_T5_GUARDIAN_WEAPON,2,MAT_R05_FROZEN_CORE,4,TUNABLE,TRUE
REC_EQ_T5_ARCHER_WEAPON,1,MAT_R05_FROST_ORE,8,TUNABLE,TRUE
REC_EQ_T5_ARCHER_WEAPON,2,MAT_R05_FROZEN_CORE,4,TUNABLE,TRUE
REC_EQ_T5_MAGE_WEAPON,1,MAT_R05_ANCIENT_RUNE,7,TUNABLE,TRUE
REC_EQ_T5_MAGE_WEAPON,2,MAT_R05_FROST_ORE,7,TUNABLE,TRUE
REC_EQ_T5_CLERIC_WEAPON,1,MAT_R05_ANCIENT_RUNE,7,TUNABLE,TRUE
REC_EQ_T5_CLERIC_WEAPON,2,MAT_R05_FROST_ORE,7,TUNABLE,TRUE
REC_EQ_T5_HEAVY_ARMOR,1,MAT_R05_FROST_ORE,7,TUNABLE,TRUE
REC_EQ_T5_HEAVY_ARMOR,2,MAT_R05_YETI_FUR,9,TUNABLE,TRUE
REC_EQ_T5_HEAVY_HELMET,1,MAT_R05_FROZEN_CORE,3,TUNABLE,TRUE
REC_EQ_T5_HEAVY_HELMET,2,MAT_R05_YETI_FUR,7,TUNABLE,TRUE
REC_EQ_T5_LIGHT_ARMOR,1,MAT_R05_FROST_ORE,7,TUNABLE,TRUE
REC_EQ_T5_LIGHT_ARMOR,2,MAT_R05_YETI_FUR,9,TUNABLE,TRUE
REC_EQ_T5_LIGHT_HELMET,1,MAT_R05_FROZEN_CORE,3,TUNABLE,TRUE
REC_EQ_T5_LIGHT_HELMET,2,MAT_R05_YETI_FUR,7,TUNABLE,TRUE
REC_EQ_T5_CLOTH_ARMOR,1,MAT_R05_ANCIENT_RUNE,9,TUNABLE,TRUE
REC_EQ_T5_CLOTH_ARMOR,2,MAT_R05_FROST_ORE,7,TUNABLE,TRUE
REC_EQ_T5_CLOTH_HELMET,1,MAT_R05_ANCIENT_RUNE,7,TUNABLE,TRUE
REC_EQ_T5_CLOTH_HELMET,2,MAT_R05_FROZEN_CORE,3,TUNABLE,TRUE
REC_EQ_T5_POWER_ACCESSORY,1,MAT_R05_FROST_ORE,6,TUNABLE,TRUE
REC_EQ_T5_POWER_ACCESSORY,2,MAT_R05_FROZEN_CORE,7,TUNABLE,TRUE
REC_EQ_T5_GUARD_ACCESSORY,1,MAT_R05_FROST_ORE,6,TUNABLE,TRUE
REC_EQ_T5_GUARD_ACCESSORY,2,MAT_R05_YETI_FUR,7,TUNABLE,TRUE
REC_EQ_T5_WISDOM_ACCESSORY,1,MAT_R05_ANCIENT_RUNE,7,TUNABLE,TRUE
REC_EQ_T5_WISDOM_ACCESSORY,2,MAT_R05_FROST_ORE,6,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_WARRIOR,1,MAT_BOSS_HYDRA_FANG,3,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_WARRIOR,2,MAT_BOSS_HYDRA_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_WARRIOR,3,MAT_R04_BOG_CRYSTAL,5,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_GUARDIAN,1,MAT_BOSS_HYDRA_SCALE,5,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_GUARDIAN,2,MAT_R04_TROLL_HIDE,5,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_ARCHER,1,MAT_BOSS_HYDRA_FANG,2,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_ARCHER,2,MAT_BOSS_HYDRA_VENOM,3,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_ARCHER,3,MAT_R04_BOG_CRYSTAL,4,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_MAGE,1,MAT_BOSS_HYDRA_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_MAGE,2,MAT_BOSS_HYDRA_VENOM,3,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_MAGE,3,MAT_R04_WITCHWATER,5,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_CLERIC,1,MAT_BOSS_HYDRA_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_CLERIC,2,MAT_R04_WITCHWATER,6,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_WARRIOR,1,MAT_BOSS_ASH_CORE,3,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_WARRIOR,2,MAT_BOSS_DRAGON_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_WARRIOR,3,MAT_BOSS_DRAGON_HORN,3,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_GUARDIAN,1,MAT_BOSS_ASH_CORE,3,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_GUARDIAN,2,MAT_BOSS_DRAGON_SCALE,6,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_ARCHER,1,MAT_BOSS_ASH_CORE,4,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_ARCHER,2,MAT_BOSS_DRAGON_HORN,2,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_ARCHER,3,MAT_R05_ANCIENT_RUNE,5,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_MAGE,1,MAT_BOSS_ASH_CORE,4,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_MAGE,2,MAT_BOSS_DRAGON_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_MAGE,3,MAT_R05_FROZEN_CORE,5,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_CLERIC,1,MAT_BOSS_DRAGON_HEART,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_CLERIC,2,MAT_BOSS_DRAGON_SCALE,3,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_CLERIC,3,MAT_R05_ANCIENT_RUNE,5,TUNABLE,TRUE
REC_POT_HEAL_SMALL,1,MAT_R01_SLIME_GEL,1,TUNABLE,TRUE
REC_POT_HEAL_SMALL,2,MAT_R01_WILD_HERB,2,TUNABLE,TRUE
REC_POT_HEAL_MEDIUM,1,MAT_R01_SLIME_GEL,1,TUNABLE,TRUE
REC_POT_HEAL_MEDIUM,2,MAT_R02_MOONLEAF,2,TUNABLE,TRUE
REC_POT_HEAL_MEDIUM,3,MAT_R02_SPIDER_SILK,1,TUNABLE,TRUE
REC_POT_HEAL_LARGE,1,MAT_R03_BAT_WING,1,TUNABLE,TRUE
REC_POT_HEAL_LARGE,2,MAT_R04_MIASMA_MOSS,2,TUNABLE,TRUE
REC_POT_HEAL_LARGE,3,MAT_R04_WITCHWATER,1,TUNABLE,TRUE
REC_POT_ANTIDOTE,1,MAT_R02_MOONLEAF,1,TUNABLE,TRUE
REC_POT_ANTIDOTE,2,MAT_R02_VENOM_SAC,1,TUNABLE,TRUE
REC_POT_POISON_RESIST,1,MAT_R04_MIASMA_MOSS,2,TUNABLE,TRUE
REC_POT_POISON_RESIST,2,MAT_R04_TOXIC_GLAND,1,TUNABLE,TRUE
REC_POT_FROST_RESIST,1,MAT_R05_ICE_BLOOM,2,TUNABLE,TRUE
REC_POT_FROST_RESIST,2,MAT_R05_WRAITH_DUST,1,TUNABLE,TRUE
REC_POT_RAID_POWER,1,MAT_BOSS_ASH_CORE,1,TUNABLE,TRUE
REC_POT_RAID_POWER,2,MAT_R05_ANCIENT_RUNE,2,TUNABLE,TRUE
```

### 8.42 `recipe_outputs.csv` (87 rows)

```csv
recipe_id,output_no,reward_type,reward_id,quantity,status,enabled
REC_EQ_T1_WARRIOR_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T1_WARRIOR_WEAPON,1,TUNABLE,TRUE
REC_EQ_T1_GUARDIAN_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T1_GUARDIAN_WEAPON,1,TUNABLE,TRUE
REC_EQ_T1_ARCHER_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T1_ARCHER_WEAPON,1,TUNABLE,TRUE
REC_EQ_T1_MAGE_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T1_MAGE_WEAPON,1,TUNABLE,TRUE
REC_EQ_T1_CLERIC_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T1_CLERIC_WEAPON,1,TUNABLE,TRUE
REC_EQ_T1_HEAVY_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T1_HEAVY_ARMOR,1,TUNABLE,TRUE
REC_EQ_T1_HEAVY_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T1_HEAVY_HELMET,1,TUNABLE,TRUE
REC_EQ_T1_LIGHT_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T1_LIGHT_ARMOR,1,TUNABLE,TRUE
REC_EQ_T1_LIGHT_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T1_LIGHT_HELMET,1,TUNABLE,TRUE
REC_EQ_T1_CLOTH_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T1_CLOTH_ARMOR,1,TUNABLE,TRUE
REC_EQ_T1_CLOTH_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T1_CLOTH_HELMET,1,TUNABLE,TRUE
REC_EQ_T1_POWER_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T1_POWER_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T1_GUARD_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T1_GUARD_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T1_WISDOM_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T1_WISDOM_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T2_WARRIOR_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T2_WARRIOR_WEAPON,1,TUNABLE,TRUE
REC_EQ_T2_GUARDIAN_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T2_GUARDIAN_WEAPON,1,TUNABLE,TRUE
REC_EQ_T2_ARCHER_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T2_ARCHER_WEAPON,1,TUNABLE,TRUE
REC_EQ_T2_MAGE_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T2_MAGE_WEAPON,1,TUNABLE,TRUE
REC_EQ_T2_CLERIC_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T2_CLERIC_WEAPON,1,TUNABLE,TRUE
REC_EQ_T2_HEAVY_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T2_HEAVY_ARMOR,1,TUNABLE,TRUE
REC_EQ_T2_HEAVY_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T2_HEAVY_HELMET,1,TUNABLE,TRUE
REC_EQ_T2_LIGHT_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T2_LIGHT_ARMOR,1,TUNABLE,TRUE
REC_EQ_T2_LIGHT_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T2_LIGHT_HELMET,1,TUNABLE,TRUE
REC_EQ_T2_CLOTH_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T2_CLOTH_ARMOR,1,TUNABLE,TRUE
REC_EQ_T2_CLOTH_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T2_CLOTH_HELMET,1,TUNABLE,TRUE
REC_EQ_T2_POWER_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T2_POWER_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T2_GUARD_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T2_GUARD_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T2_WISDOM_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T2_WISDOM_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T3_WARRIOR_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T3_WARRIOR_WEAPON,1,TUNABLE,TRUE
REC_EQ_T3_GUARDIAN_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T3_GUARDIAN_WEAPON,1,TUNABLE,TRUE
REC_EQ_T3_ARCHER_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T3_ARCHER_WEAPON,1,TUNABLE,TRUE
REC_EQ_T3_MAGE_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T3_MAGE_WEAPON,1,TUNABLE,TRUE
REC_EQ_T3_CLERIC_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T3_CLERIC_WEAPON,1,TUNABLE,TRUE
REC_EQ_T3_HEAVY_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T3_HEAVY_ARMOR,1,TUNABLE,TRUE
REC_EQ_T3_HEAVY_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T3_HEAVY_HELMET,1,TUNABLE,TRUE
REC_EQ_T3_LIGHT_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T3_LIGHT_ARMOR,1,TUNABLE,TRUE
REC_EQ_T3_LIGHT_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T3_LIGHT_HELMET,1,TUNABLE,TRUE
REC_EQ_T3_CLOTH_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T3_CLOTH_ARMOR,1,TUNABLE,TRUE
REC_EQ_T3_CLOTH_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T3_CLOTH_HELMET,1,TUNABLE,TRUE
REC_EQ_T3_POWER_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T3_POWER_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T3_GUARD_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T3_GUARD_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T3_WISDOM_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T3_WISDOM_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T4_WARRIOR_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T4_WARRIOR_WEAPON,1,TUNABLE,TRUE
REC_EQ_T4_GUARDIAN_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T4_GUARDIAN_WEAPON,1,TUNABLE,TRUE
REC_EQ_T4_ARCHER_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T4_ARCHER_WEAPON,1,TUNABLE,TRUE
REC_EQ_T4_MAGE_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T4_MAGE_WEAPON,1,TUNABLE,TRUE
REC_EQ_T4_CLERIC_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T4_CLERIC_WEAPON,1,TUNABLE,TRUE
REC_EQ_T4_HEAVY_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T4_HEAVY_ARMOR,1,TUNABLE,TRUE
REC_EQ_T4_HEAVY_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T4_HEAVY_HELMET,1,TUNABLE,TRUE
REC_EQ_T4_LIGHT_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T4_LIGHT_ARMOR,1,TUNABLE,TRUE
REC_EQ_T4_LIGHT_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T4_LIGHT_HELMET,1,TUNABLE,TRUE
REC_EQ_T4_CLOTH_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T4_CLOTH_ARMOR,1,TUNABLE,TRUE
REC_EQ_T4_CLOTH_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T4_CLOTH_HELMET,1,TUNABLE,TRUE
REC_EQ_T4_POWER_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T4_POWER_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T4_GUARD_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T4_GUARD_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T4_WISDOM_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T4_WISDOM_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T5_WARRIOR_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T5_WARRIOR_WEAPON,1,TUNABLE,TRUE
REC_EQ_T5_GUARDIAN_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T5_GUARDIAN_WEAPON,1,TUNABLE,TRUE
REC_EQ_T5_ARCHER_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T5_ARCHER_WEAPON,1,TUNABLE,TRUE
REC_EQ_T5_MAGE_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T5_MAGE_WEAPON,1,TUNABLE,TRUE
REC_EQ_T5_CLERIC_WEAPON,1,EQUIPMENT_TEMPLATE,EQ_T5_CLERIC_WEAPON,1,TUNABLE,TRUE
REC_EQ_T5_HEAVY_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T5_HEAVY_ARMOR,1,TUNABLE,TRUE
REC_EQ_T5_HEAVY_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T5_HEAVY_HELMET,1,TUNABLE,TRUE
REC_EQ_T5_LIGHT_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T5_LIGHT_ARMOR,1,TUNABLE,TRUE
REC_EQ_T5_LIGHT_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T5_LIGHT_HELMET,1,TUNABLE,TRUE
REC_EQ_T5_CLOTH_ARMOR,1,EQUIPMENT_TEMPLATE,EQ_T5_CLOTH_ARMOR,1,TUNABLE,TRUE
REC_EQ_T5_CLOTH_HELMET,1,EQUIPMENT_TEMPLATE,EQ_T5_CLOTH_HELMET,1,TUNABLE,TRUE
REC_EQ_T5_POWER_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T5_POWER_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T5_GUARD_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T5_GUARD_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_T5_WISDOM_ACCESSORY,1,EQUIPMENT_TEMPLATE,EQ_T5_WISDOM_ACCESSORY,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_WARRIOR,1,EQUIPMENT_TEMPLATE,EQ_BOSS_HYDRA_WARRIOR,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_GUARDIAN,1,EQUIPMENT_TEMPLATE,EQ_BOSS_HYDRA_GUARDIAN,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_ARCHER,1,EQUIPMENT_TEMPLATE,EQ_BOSS_HYDRA_ARCHER,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_MAGE,1,EQUIPMENT_TEMPLATE,EQ_BOSS_HYDRA_MAGE,1,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_CLERIC,1,EQUIPMENT_TEMPLATE,EQ_BOSS_HYDRA_CLERIC,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_WARRIOR,1,EQUIPMENT_TEMPLATE,EQ_BOSS_DRAGON_WARRIOR,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_GUARDIAN,1,EQUIPMENT_TEMPLATE,EQ_BOSS_DRAGON_GUARDIAN,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_ARCHER,1,EQUIPMENT_TEMPLATE,EQ_BOSS_DRAGON_ARCHER,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_MAGE,1,EQUIPMENT_TEMPLATE,EQ_BOSS_DRAGON_MAGE,1,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_CLERIC,1,EQUIPMENT_TEMPLATE,EQ_BOSS_DRAGON_CLERIC,1,TUNABLE,TRUE
REC_POT_HEAL_SMALL,1,POTION,POT_HEAL_SMALL,1,TUNABLE,TRUE
REC_POT_HEAL_MEDIUM,1,POTION,POT_HEAL_MEDIUM,1,TUNABLE,TRUE
REC_POT_HEAL_LARGE,1,POTION,POT_HEAL_LARGE,1,TUNABLE,TRUE
REC_POT_ANTIDOTE,1,POTION,POT_ANTIDOTE,1,TUNABLE,TRUE
REC_POT_POISON_RESIST,1,POTION,POT_POISON_RESIST,1,TUNABLE,TRUE
REC_POT_FROST_RESIST,1,POTION,POT_FROST_RESIST,1,TUNABLE,TRUE
REC_POT_RAID_POWER,1,POTION,POT_RAID_POWER,1,TUNABLE,TRUE
```

### 8.43 `recipes.csv` (87 rows)

```csv
recipe_id,name_text_key,facility_id,facility_level,npc_proficiency_id,craft_seconds,quality_roll,status,enabled
REC_EQ_T1_WARRIOR_WEAPON,TXT_REC_EQ_T1_WARRIOR_WEAPON_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,20,TRUE,TUNABLE,TRUE
REC_EQ_T1_GUARDIAN_WEAPON,TXT_REC_EQ_T1_GUARDIAN_WEAPON_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,20,TRUE,TUNABLE,TRUE
REC_EQ_T1_ARCHER_WEAPON,TXT_REC_EQ_T1_ARCHER_WEAPON_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,20,TRUE,TUNABLE,TRUE
REC_EQ_T1_MAGE_WEAPON,TXT_REC_EQ_T1_MAGE_WEAPON_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,20,TRUE,TUNABLE,TRUE
REC_EQ_T1_CLERIC_WEAPON,TXT_REC_EQ_T1_CLERIC_WEAPON_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,20,TRUE,TUNABLE,TRUE
REC_EQ_T1_HEAVY_ARMOR,TXT_REC_EQ_T1_HEAVY_ARMOR_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,18,TRUE,TUNABLE,TRUE
REC_EQ_T1_HEAVY_HELMET,TXT_REC_EQ_T1_HEAVY_HELMET_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,14,TRUE,TUNABLE,TRUE
REC_EQ_T1_LIGHT_ARMOR,TXT_REC_EQ_T1_LIGHT_ARMOR_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,18,TRUE,TUNABLE,TRUE
REC_EQ_T1_LIGHT_HELMET,TXT_REC_EQ_T1_LIGHT_HELMET_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,14,TRUE,TUNABLE,TRUE
REC_EQ_T1_CLOTH_ARMOR,TXT_REC_EQ_T1_CLOTH_ARMOR_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,18,TRUE,TUNABLE,TRUE
REC_EQ_T1_CLOTH_HELMET,TXT_REC_EQ_T1_CLOTH_HELMET_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,14,TRUE,TUNABLE,TRUE
REC_EQ_T1_POWER_ACCESSORY,TXT_REC_EQ_T1_POWER_ACCESSORY_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,16,TRUE,TUNABLE,TRUE
REC_EQ_T1_GUARD_ACCESSORY,TXT_REC_EQ_T1_GUARD_ACCESSORY_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,16,TRUE,TUNABLE,TRUE
REC_EQ_T1_WISDOM_ACCESSORY,TXT_REC_EQ_T1_WISDOM_ACCESSORY_NAME,FAC_BLACKSMITH,1,NPC_APPRENTICE,16,TRUE,TUNABLE,TRUE
REC_EQ_T2_WARRIOR_WEAPON,TXT_REC_EQ_T2_WARRIOR_WEAPON_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,40,TRUE,TUNABLE,TRUE
REC_EQ_T2_GUARDIAN_WEAPON,TXT_REC_EQ_T2_GUARDIAN_WEAPON_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,40,TRUE,TUNABLE,TRUE
REC_EQ_T2_ARCHER_WEAPON,TXT_REC_EQ_T2_ARCHER_WEAPON_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,40,TRUE,TUNABLE,TRUE
REC_EQ_T2_MAGE_WEAPON,TXT_REC_EQ_T2_MAGE_WEAPON_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,40,TRUE,TUNABLE,TRUE
REC_EQ_T2_CLERIC_WEAPON,TXT_REC_EQ_T2_CLERIC_WEAPON_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,40,TRUE,TUNABLE,TRUE
REC_EQ_T2_HEAVY_ARMOR,TXT_REC_EQ_T2_HEAVY_ARMOR_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,36,TRUE,TUNABLE,TRUE
REC_EQ_T2_HEAVY_HELMET,TXT_REC_EQ_T2_HEAVY_HELMET_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,28,TRUE,TUNABLE,TRUE
REC_EQ_T2_LIGHT_ARMOR,TXT_REC_EQ_T2_LIGHT_ARMOR_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,36,TRUE,TUNABLE,TRUE
REC_EQ_T2_LIGHT_HELMET,TXT_REC_EQ_T2_LIGHT_HELMET_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,28,TRUE,TUNABLE,TRUE
REC_EQ_T2_CLOTH_ARMOR,TXT_REC_EQ_T2_CLOTH_ARMOR_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,36,TRUE,TUNABLE,TRUE
REC_EQ_T2_CLOTH_HELMET,TXT_REC_EQ_T2_CLOTH_HELMET_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,28,TRUE,TUNABLE,TRUE
REC_EQ_T2_POWER_ACCESSORY,TXT_REC_EQ_T2_POWER_ACCESSORY_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,32,TRUE,TUNABLE,TRUE
REC_EQ_T2_GUARD_ACCESSORY,TXT_REC_EQ_T2_GUARD_ACCESSORY_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,32,TRUE,TUNABLE,TRUE
REC_EQ_T2_WISDOM_ACCESSORY,TXT_REC_EQ_T2_WISDOM_ACCESSORY_NAME,FAC_BLACKSMITH,2,NPC_APPRENTICE,32,TRUE,TUNABLE,TRUE
REC_EQ_T3_WARRIOR_WEAPON,TXT_REC_EQ_T3_WARRIOR_WEAPON_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,60,TRUE,TUNABLE,TRUE
REC_EQ_T3_GUARDIAN_WEAPON,TXT_REC_EQ_T3_GUARDIAN_WEAPON_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,60,TRUE,TUNABLE,TRUE
REC_EQ_T3_ARCHER_WEAPON,TXT_REC_EQ_T3_ARCHER_WEAPON_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,60,TRUE,TUNABLE,TRUE
REC_EQ_T3_MAGE_WEAPON,TXT_REC_EQ_T3_MAGE_WEAPON_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,60,TRUE,TUNABLE,TRUE
REC_EQ_T3_CLERIC_WEAPON,TXT_REC_EQ_T3_CLERIC_WEAPON_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,60,TRUE,TUNABLE,TRUE
REC_EQ_T3_HEAVY_ARMOR,TXT_REC_EQ_T3_HEAVY_ARMOR_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,54,TRUE,TUNABLE,TRUE
REC_EQ_T3_HEAVY_HELMET,TXT_REC_EQ_T3_HEAVY_HELMET_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,42,TRUE,TUNABLE,TRUE
REC_EQ_T3_LIGHT_ARMOR,TXT_REC_EQ_T3_LIGHT_ARMOR_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,54,TRUE,TUNABLE,TRUE
REC_EQ_T3_LIGHT_HELMET,TXT_REC_EQ_T3_LIGHT_HELMET_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,42,TRUE,TUNABLE,TRUE
REC_EQ_T3_CLOTH_ARMOR,TXT_REC_EQ_T3_CLOTH_ARMOR_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,54,TRUE,TUNABLE,TRUE
REC_EQ_T3_CLOTH_HELMET,TXT_REC_EQ_T3_CLOTH_HELMET_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,42,TRUE,TUNABLE,TRUE
REC_EQ_T3_POWER_ACCESSORY,TXT_REC_EQ_T3_POWER_ACCESSORY_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,48,TRUE,TUNABLE,TRUE
REC_EQ_T3_GUARD_ACCESSORY,TXT_REC_EQ_T3_GUARD_ACCESSORY_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,48,TRUE,TUNABLE,TRUE
REC_EQ_T3_WISDOM_ACCESSORY,TXT_REC_EQ_T3_WISDOM_ACCESSORY_NAME,FAC_BLACKSMITH,3,NPC_SKILLED,48,TRUE,TUNABLE,TRUE
REC_EQ_T4_WARRIOR_WEAPON,TXT_REC_EQ_T4_WARRIOR_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,80,TRUE,TUNABLE,TRUE
REC_EQ_T4_GUARDIAN_WEAPON,TXT_REC_EQ_T4_GUARDIAN_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,80,TRUE,TUNABLE,TRUE
REC_EQ_T4_ARCHER_WEAPON,TXT_REC_EQ_T4_ARCHER_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,80,TRUE,TUNABLE,TRUE
REC_EQ_T4_MAGE_WEAPON,TXT_REC_EQ_T4_MAGE_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,80,TRUE,TUNABLE,TRUE
REC_EQ_T4_CLERIC_WEAPON,TXT_REC_EQ_T4_CLERIC_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,80,TRUE,TUNABLE,TRUE
REC_EQ_T4_HEAVY_ARMOR,TXT_REC_EQ_T4_HEAVY_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,72,TRUE,TUNABLE,TRUE
REC_EQ_T4_HEAVY_HELMET,TXT_REC_EQ_T4_HEAVY_HELMET_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,56,TRUE,TUNABLE,TRUE
REC_EQ_T4_LIGHT_ARMOR,TXT_REC_EQ_T4_LIGHT_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,72,TRUE,TUNABLE,TRUE
REC_EQ_T4_LIGHT_HELMET,TXT_REC_EQ_T4_LIGHT_HELMET_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,56,TRUE,TUNABLE,TRUE
REC_EQ_T4_CLOTH_ARMOR,TXT_REC_EQ_T4_CLOTH_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,72,TRUE,TUNABLE,TRUE
REC_EQ_T4_CLOTH_HELMET,TXT_REC_EQ_T4_CLOTH_HELMET_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,56,TRUE,TUNABLE,TRUE
REC_EQ_T4_POWER_ACCESSORY,TXT_REC_EQ_T4_POWER_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,64,TRUE,TUNABLE,TRUE
REC_EQ_T4_GUARD_ACCESSORY,TXT_REC_EQ_T4_GUARD_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,64,TRUE,TUNABLE,TRUE
REC_EQ_T4_WISDOM_ACCESSORY,TXT_REC_EQ_T4_WISDOM_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_ARTISAN,64,TRUE,TUNABLE,TRUE
REC_EQ_T5_WARRIOR_WEAPON,TXT_REC_EQ_T5_WARRIOR_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_MASTER,100,TRUE,TUNABLE,TRUE
REC_EQ_T5_GUARDIAN_WEAPON,TXT_REC_EQ_T5_GUARDIAN_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_MASTER,100,TRUE,TUNABLE,TRUE
REC_EQ_T5_ARCHER_WEAPON,TXT_REC_EQ_T5_ARCHER_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_MASTER,100,TRUE,TUNABLE,TRUE
REC_EQ_T5_MAGE_WEAPON,TXT_REC_EQ_T5_MAGE_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_MASTER,100,TRUE,TUNABLE,TRUE
REC_EQ_T5_CLERIC_WEAPON,TXT_REC_EQ_T5_CLERIC_WEAPON_NAME,FAC_BLACKSMITH,4,NPC_MASTER,100,TRUE,TUNABLE,TRUE
REC_EQ_T5_HEAVY_ARMOR,TXT_REC_EQ_T5_HEAVY_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_MASTER,90,TRUE,TUNABLE,TRUE
REC_EQ_T5_HEAVY_HELMET,TXT_REC_EQ_T5_HEAVY_HELMET_NAME,FAC_BLACKSMITH,4,NPC_MASTER,70,TRUE,TUNABLE,TRUE
REC_EQ_T5_LIGHT_ARMOR,TXT_REC_EQ_T5_LIGHT_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_MASTER,90,TRUE,TUNABLE,TRUE
REC_EQ_T5_LIGHT_HELMET,TXT_REC_EQ_T5_LIGHT_HELMET_NAME,FAC_BLACKSMITH,4,NPC_MASTER,70,TRUE,TUNABLE,TRUE
REC_EQ_T5_CLOTH_ARMOR,TXT_REC_EQ_T5_CLOTH_ARMOR_NAME,FAC_BLACKSMITH,4,NPC_MASTER,90,TRUE,TUNABLE,TRUE
REC_EQ_T5_CLOTH_HELMET,TXT_REC_EQ_T5_CLOTH_HELMET_NAME,FAC_BLACKSMITH,4,NPC_MASTER,70,TRUE,TUNABLE,TRUE
REC_EQ_T5_POWER_ACCESSORY,TXT_REC_EQ_T5_POWER_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_MASTER,80,TRUE,TUNABLE,TRUE
REC_EQ_T5_GUARD_ACCESSORY,TXT_REC_EQ_T5_GUARD_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_MASTER,80,TRUE,TUNABLE,TRUE
REC_EQ_T5_WISDOM_ACCESSORY,TXT_REC_EQ_T5_WISDOM_ACCESSORY_NAME,FAC_BLACKSMITH,4,NPC_MASTER,80,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_WARRIOR,TXT_REC_EQ_BOSS_HYDRA_WARRIOR_NAME,FAC_BLACKSMITH,4,NPC_MASTER,240,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_GUARDIAN,TXT_REC_EQ_BOSS_HYDRA_GUARDIAN_NAME,FAC_BLACKSMITH,4,NPC_MASTER,240,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_ARCHER,TXT_REC_EQ_BOSS_HYDRA_ARCHER_NAME,FAC_BLACKSMITH,4,NPC_MASTER,240,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_MAGE,TXT_REC_EQ_BOSS_HYDRA_MAGE_NAME,FAC_BLACKSMITH,4,NPC_MASTER,240,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_HYDRA_CLERIC,TXT_REC_EQ_BOSS_HYDRA_CLERIC_NAME,FAC_BLACKSMITH,4,NPC_MASTER,240,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_WARRIOR,TXT_REC_EQ_BOSS_DRAGON_WARRIOR_NAME,FAC_BLACKSMITH,4,NPC_MASTER,420,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_GUARDIAN,TXT_REC_EQ_BOSS_DRAGON_GUARDIAN_NAME,FAC_BLACKSMITH,4,NPC_MASTER,420,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_ARCHER,TXT_REC_EQ_BOSS_DRAGON_ARCHER_NAME,FAC_BLACKSMITH,4,NPC_MASTER,420,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_MAGE,TXT_REC_EQ_BOSS_DRAGON_MAGE_NAME,FAC_BLACKSMITH,4,NPC_MASTER,420,TRUE,TUNABLE,TRUE
REC_EQ_BOSS_DRAGON_CLERIC,TXT_REC_EQ_BOSS_DRAGON_CLERIC_NAME,FAC_BLACKSMITH,4,NPC_MASTER,420,TRUE,TUNABLE,TRUE
REC_POT_HEAL_SMALL,TXT_REC_POT_HEAL_SMALL_NAME,FAC_ALCHEMY,1,NPC_APPRENTICE,12,FALSE,TUNABLE,TRUE
REC_POT_HEAL_MEDIUM,TXT_REC_POT_HEAL_MEDIUM_NAME,FAC_ALCHEMY,2,NPC_SKILLED,24,FALSE,TUNABLE,TRUE
REC_POT_HEAL_LARGE,TXT_REC_POT_HEAL_LARGE_NAME,FAC_ALCHEMY,3,NPC_ARTISAN,45,FALSE,TUNABLE,TRUE
REC_POT_ANTIDOTE,TXT_REC_POT_ANTIDOTE_NAME,FAC_ALCHEMY,2,NPC_SKILLED,20,FALSE,TUNABLE,TRUE
REC_POT_POISON_RESIST,TXT_REC_POT_POISON_RESIST_NAME,FAC_ALCHEMY,3,NPC_ARTISAN,50,FALSE,TUNABLE,TRUE
REC_POT_FROST_RESIST,TXT_REC_POT_FROST_RESIST_NAME,FAC_ALCHEMY,4,NPC_MASTER,70,FALSE,TUNABLE,TRUE
REC_POT_RAID_POWER,TXT_REC_POT_RAID_POWER_NAME,FAC_ALCHEMY,4,NPC_MASTER,120,FALSE,TUNABLE,TRUE
```

### 8.44 `recruitment_pity_groups.csv` (1 rows)

```csv
pity_group_id,category,carry_over,status,enabled
PITY_GROUP_SPECIAL_STANDARD,SPECIAL,TRUE,TUNABLE,TRUE
```

### 8.45 `recruitment_pity_rules.csv` (2 rows)

```csv
pity_group_id,pity_rule_id,trigger_count,guaranteed_grade_id,reset_on_grade_or_higher_id,guarantee_type,status,enabled
PITY_GROUP_SPECIAL_STANDARD,PITY_S_PLUS,10,GRADE_S,GRADE_S,GRADE_AT_LEAST,TUNABLE,TRUE
PITY_GROUP_SPECIAL_STANDARD,PITY_SS,80,GRADE_SS,GRADE_SS,GRADE_AT_LEAST,TUNABLE,TRUE
```

### 8.46 `recruitment_pool_entries.csv` (17 rows)

```csv
pool_id,entry_no,result_type,result_id,grade_id,job_selection_type,job_selection_id,weight,status,enabled
TAVERN_TUTORIAL_FIRST,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_C,FIXED,JOB_WARRIOR,75,CONFIRMED,TRUE
TAVERN_TUTORIAL_FIRST,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_B,FIXED,JOB_WARRIOR,23,CONFIRMED,TRUE
TAVERN_L1,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_C,ALL,,75,TUNABLE,TRUE
TAVERN_L1,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_B,ALL,,23,TUNABLE,TRUE
TAVERN_L1,3,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_A,ALL,,2,TUNABLE,TRUE
TAVERN_L4,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_C,ALL,,30,TUNABLE,TRUE
TAVERN_L4,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_B,ALL,,48,TUNABLE,TRUE
TAVERN_L4,3,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_A,ALL,,22,TUNABLE,TRUE
SPECIAL_STANDARD_TICKET,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_A,ALL,,82,TUNABLE,TRUE
SPECIAL_STANDARD_TICKET,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_S,ALL,,16,TUNABLE,TRUE
SPECIAL_STANDARD_TICKET,3,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_SS,ALL,,2,TUNABLE,TRUE
SPECIAL_STANDARD_FREE_PREMIUM,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_A,ALL,,82,TUNABLE,TRUE
SPECIAL_STANDARD_FREE_PREMIUM,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_S,ALL,,16,TUNABLE,TRUE
SPECIAL_STANDARD_FREE_PREMIUM,3,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_SS,ALL,,2,TUNABLE,TRUE
SPECIAL_STANDARD_PAID_PREMIUM,1,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_A,ALL,,82,TUNABLE,TRUE
SPECIAL_STANDARD_PAID_PREMIUM,2,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_S,ALL,,16,TUNABLE,TRUE
SPECIAL_STANDARD_PAID_PREMIUM,3,GENERATED_MERCENARY,GEN_MERC_STANDARD_V1,GRADE_SS,ALL,,2,TUNABLE,TRUE
```

### 8.47 `recruitment_pools.csv` (6 rows)

```csv
pool_id,pool_type,pity_group_id,rate_up_group_id,cost_type,cost_amount,starts_at_utc,ends_at_utc,status,enabled
TAVERN_TUTORIAL_FIRST,TAVERN,,,KINGDOM_GOLD,0,,,CONFIRMED,TRUE
TAVERN_L1,TAVERN,,,KINGDOM_GOLD,500,,,TUNABLE,TRUE
TAVERN_L4,TAVERN,,,KINGDOM_GOLD,2000,,,TUNABLE,TRUE
SPECIAL_STANDARD_TICKET,SPECIAL,PITY_GROUP_SPECIAL_STANDARD,,TICKET,1,,,TUNABLE,TRUE
SPECIAL_STANDARD_FREE_PREMIUM,SPECIAL,PITY_GROUP_SPECIAL_STANDARD,,FREE_PREMIUM,300,,,TUNABLE,TRUE
SPECIAL_STANDARD_PAID_PREMIUM,SPECIAL,PITY_GROUP_SPECIAL_STANDARD,,PAID_PREMIUM,300,,,TUNABLE,TRUE
```

### 8.48 `recruitment_rate_up_entries.csv` (0 rows)

```csv
rate_up_group_id,job_id,weight,status,enabled
```

### 8.49 `recruitment_rate_up_groups.csv` (0 rows)

```csv
rate_up_group_id,featured_share,failure_guarantee_mode,status,enabled
```

### 8.50 `refine_options.csv` (11 rows)

```csv
refine_option_id,name_text_key,option_group,stat_key,min_value,max_value,material_item_id,material_quantity,personal_gold_cost,status,enabled
REF_ATK_POWER,TXT_REF_ATK_POWER_NAME,ATTACK,ATTACK_PERCENT,0.03,0.12,MAT_R01_WOLF_FANG,1,2500,TUNABLE,TRUE
REF_CRIT,TXT_REF_CRIT_NAME,ATTACK,CRIT_CHANCE,0.02,0.08,MAT_R01_WOLF_FANG,1,2500,TUNABLE,TRUE
REF_DEF,TXT_REF_DEF_NAME,DEFENSE,DEFENSE_PERCENT,0.04,0.15,MAT_R03_GOLEM_FRAGMENT,1,2500,TUNABLE,TRUE
REF_HP,TXT_REF_HP_NAME,DEFENSE,HP_PERCENT,0.04,0.16,MAT_R04_TROLL_HIDE,1,2500,TUNABLE,TRUE
REF_MATERIAL,TXT_REF_MATERIAL_NAME,COLLECTION,MATERIAL_BONUS,0.02,0.08,MAT_R01_SLIME_GEL,1,2500,TUNABLE,TRUE
REF_RARE_FIND,TXT_REF_RARE_FIND_NAME,COLLECTION,RARE_FIND,0.01,0.05,MAT_R03_ANCIENT_GEAR,1,2500,TUNABLE,TRUE
REF_FIRE,TXT_REF_FIRE_NAME,ELEMENT,FIRE_DAMAGE,0.04,0.15,MAT_BOSS_ASH_CORE,1,2500,TUNABLE,TRUE
REF_FROST,TXT_REF_FROST_NAME,ELEMENT,FROST_RESIST,0.06,0.2,MAT_R05_FROZEN_CORE,1,2500,TUNABLE,TRUE
REF_POISON,TXT_REF_POISON_NAME,ELEMENT,POISON_POWER,0.05,0.18,MAT_R04_TOXIC_GLAND,1,2500,TUNABLE,TRUE
REF_BOSS,TXT_REF_BOSS_NAME,BOSS,BOSS_DAMAGE,0.03,0.12,MAT_BOSS_HYDRA_HEART,1,2500,TUNABLE,TRUE
REF_PART,TXT_REF_PART_NAME,BOSS,PART_DAMAGE,0.04,0.15,MAT_BOSS_DRAGON_HORN,1,2500,TUNABLE,TRUE
```

### 8.51 `regions.csv` (5 rows)

```csv
region_id,name_text_key,order,tier,min_rank_id,unlock_condition_group_id,kingdom_stage_id,recommended_power,max_active,environment_tag,raid_gate_id,offline_efficiency,status,enabled
REGION_R01,TXT_REGION_R01_NAME,1,1,RANK_APPRENTICE,COND_GRP_REGION_R01_UNLOCK,KINGDOM_1,100,6,MEADOW,,0.85,CONFIRMED,TRUE
REGION_R02,TXT_REGION_R02_NAME,2,2,RANK_REGULAR,COND_GRP_REGION_R02_UNLOCK,KINGDOM_2,220,8,FOREST,,0.82,CONFIRMED,TRUE
REGION_R03,TXT_REGION_R03_NAME,3,3,RANK_SKILLED,COND_GRP_REGION_R03_UNLOCK,KINGDOM_3,430,8,MINE,,0.8,CONFIRMED,TRUE
REGION_R04,TXT_REGION_R04_NAME,4,4,RANK_ELITE,COND_GRP_REGION_R04_UNLOCK,KINGDOM_3,760,8,SWAMP,,0.76,CONFIRMED,TRUE
REGION_R05,TXT_REGION_R05_NAME,5,5,RANK_HERO,COND_GRP_REGION_R05_UNLOCK,KINGDOM_4,1250,8,FROST_RUIN,RAID_HYDRA,0.72,CONFIRMED,TRUE
```

### 8.52 `reward_entries.csv` (16 rows)

```csv
reward_group_id,entry_no,reward_type,reward_id,quantity,probability,weight,status,enabled
REWARD_RAID_HYDRA_FIRST_CLEAR,1,REGION_UNLOCK,REGION_R05,1,,,CONFIRMED,TRUE
REWARD_RAID_DRAGON_FIRST_CLEAR,1,PROGRESSION_FLAG,FLAG_V1_ENDING,1,,,CONFIRMED,TRUE
REWARD_RAID_DRAGON_FIRST_CLEAR,2,PROGRESSION_FLAG,FLAG_RAID_DRAGON_VARIANTS,1,,,CONFIRMED,TRUE
REWARD_HYDRA_HEAD_BREAK,1,ITEM,MAT_BOSS_HYDRA_VENOM,1,,,TUNABLE,TRUE
REWARD_HYDRA_BODY_BREAK,1,ITEM,MAT_BOSS_HYDRA_SCALE,1,,,TUNABLE,TRUE
REWARD_HYDRA_HEART_EXPOSED,1,ITEM,MAT_BOSS_HYDRA_HEART,1,,,TUNABLE,TRUE
REWARD_DRAGON_HORN_BREAK,1,ITEM,MAT_BOSS_DRAGON_HORN,1,,,TUNABLE,TRUE
REWARD_DRAGON_WING_BREAK,1,ITEM,MAT_BOSS_ASH_CORE,1,,,TUNABLE,TRUE
REWARD_DRAGON_BODY_BREAK,1,ITEM,MAT_BOSS_DRAGON_SCALE,1,,,TUNABLE,TRUE
REWARD_DRAGON_HEART_EXPOSED,1,ITEM,MAT_BOSS_DRAGON_HEART,1,,,TUNABLE,TRUE
REWARD_RAID_HYDRA_NORMAL_REPEAT,1,ITEM,MAT_BOSS_HYDRA_VENOM,1,,,TUNABLE,TRUE
REWARD_RAID_HYDRA_HARD_REPEAT,1,ITEM,MAT_BOSS_HYDRA_SCALE,2,,,TUNABLE,TRUE
REWARD_RAID_HYDRA_CORRUPTED_REPEAT,1,ITEM,MAT_BOSS_HYDRA_HEART,1,,,TUNABLE,TRUE
REWARD_RAID_DRAGON_NORMAL_REPEAT,1,ITEM,MAT_BOSS_DRAGON_HORN,1,,,TUNABLE,TRUE
REWARD_RAID_DRAGON_HARD_REPEAT,1,ITEM,MAT_BOSS_DRAGON_SCALE,2,,,TUNABLE,TRUE
REWARD_RAID_DRAGON_CORRUPTED_REPEAT,1,ITEM,MAT_BOSS_DRAGON_HEART,1,,,TUNABLE,TRUE
```

### 8.53 `reward_groups.csv` (15 rows)

```csv
reward_group_id,distribution_mode,draw_count,status,enabled
REWARD_RAID_HYDRA_FIRST_CLEAR,ALL,1,CONFIRMED,TRUE
REWARD_RAID_DRAGON_FIRST_CLEAR,ALL,2,CONFIRMED,TRUE
REWARD_HYDRA_HEAD_BREAK,ALL,1,TUNABLE,TRUE
REWARD_HYDRA_BODY_BREAK,ALL,1,TUNABLE,TRUE
REWARD_HYDRA_HEART_EXPOSED,ALL,1,TUNABLE,TRUE
REWARD_DRAGON_HORN_BREAK,ALL,1,TUNABLE,TRUE
REWARD_DRAGON_WING_BREAK,ALL,1,TUNABLE,TRUE
REWARD_DRAGON_BODY_BREAK,ALL,1,TUNABLE,TRUE
REWARD_DRAGON_HEART_EXPOSED,ALL,1,TUNABLE,TRUE
REWARD_RAID_HYDRA_NORMAL_REPEAT,ALL,1,TUNABLE,TRUE
REWARD_RAID_HYDRA_HARD_REPEAT,ALL,1,TUNABLE,TRUE
REWARD_RAID_HYDRA_CORRUPTED_REPEAT,ALL,1,TUNABLE,TRUE
REWARD_RAID_DRAGON_NORMAL_REPEAT,ALL,1,TUNABLE,TRUE
REWARD_RAID_DRAGON_HARD_REPEAT,ALL,1,TUNABLE,TRUE
REWARD_RAID_DRAGON_CORRUPTED_REPEAT,ALL,1,TUNABLE,TRUE
```

### 8.54 `runtime_config.csv` (14 rows)

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
```

### 8.55 `skills.csv` (15 rows)

```csv
skill_id,job_id,name_text_key,kind,unlock_rank_id,target,cooldown_sec,power_coeff,resource_cost,condition,description_text_key,status,enabled
SK_WAR_HEAVY_SLASH,JOB_WARRIOR,TXT_SK_WAR_HEAVY_SLASH_NAME,ACTIVE,RANK_APPRENTICE,ENEMY_SINGLE,6,1.6,0,TARGET_IN_MELEE,TXT_SK_WAR_HEAVY_SLASH_DESCRIPTION,CONFIRMED,TRUE
SK_WAR_BATTLE_RUSH,JOB_WARRIOR,TXT_SK_WAR_BATTLE_RUSH_NAME,ACTIVE,RANK_REGULAR,ENEMY_SINGLE,12,1.2,0,DISTANCE_GT_2,TXT_SK_WAR_BATTLE_RUSH_DESCRIPTION,CONFIRMED,TRUE
SK_WAR_TENACITY,JOB_WARRIOR,TXT_SK_WAR_TENACITY_NAME,PASSIVE,RANK_SKILLED,SELF,0,0,0,ALWAYS,TXT_SK_WAR_TENACITY_DESCRIPTION,CONFIRMED,TRUE
SK_GUA_TAUNT,JOB_GUARDIAN,TXT_SK_GUA_TAUNT_NAME,ACTIVE,RANK_APPRENTICE,ENEMY_AREA,10,0.5,0,ENEMY_NEAR_ALLY,TXT_SK_GUA_TAUNT_DESCRIPTION,CONFIRMED,TRUE
SK_GUA_FORTRESS,JOB_GUARDIAN,TXT_SK_GUA_FORTRESS_NAME,ACTIVE,RANK_REGULAR,SELF,15,0,0,HP_LT_70,TXT_SK_GUA_FORTRESS_DESCRIPTION,CONFIRMED,TRUE
SK_GUA_GUARD_MARK,JOB_GUARDIAN,TXT_SK_GUA_GUARD_MARK_NAME,PASSIVE,RANK_SKILLED,ALLY_LOW_HP,0,0,0,ALLY_HP_LT_50,TXT_SK_GUA_GUARD_MARK_DESCRIPTION,CONFIRMED,TRUE
SK_ARC_QUICK_SHOT,JOB_ARCHER,TXT_SK_ARC_QUICK_SHOT_NAME,ACTIVE,RANK_APPRENTICE,ENEMY_SINGLE,5,1.25,0,TARGET_IN_RANGE,TXT_SK_ARC_QUICK_SHOT_DESCRIPTION,CONFIRMED,TRUE
SK_ARC_PIERCE,JOB_ARCHER,TXT_SK_ARC_PIERCE_NAME,ACTIVE,RANK_REGULAR,ENEMY_LINE,11,1.4,0,MULTI_TARGET_LINE,TXT_SK_ARC_PIERCE_DESCRIPTION,CONFIRMED,TRUE
SK_ARC_HUNTER_EYE,JOB_ARCHER,TXT_SK_ARC_HUNTER_EYE_NAME,PASSIVE,RANK_SKILLED,SELF,0,0,0,ALWAYS,TXT_SK_ARC_HUNTER_EYE_DESCRIPTION,CONFIRMED,TRUE
SK_MAG_FIRE_BURST,JOB_MAGE,TXT_SK_MAG_FIRE_BURST_NAME,ACTIVE,RANK_APPRENTICE,ENEMY_AREA,8,1.35,0,ENEMY_CLUSTER_2,TXT_SK_MAG_FIRE_BURST_DESCRIPTION,CONFIRMED,TRUE
SK_MAG_FROST_NOVA,JOB_MAGE,TXT_SK_MAG_FROST_NOVA_NAME,ACTIVE,RANK_REGULAR,ENEMY_AREA,14,0.9,0,ENEMY_NEAR_SELF,TXT_SK_MAG_FROST_NOVA_DESCRIPTION,CONFIRMED,TRUE
SK_MAG_MANA_FLOW,JOB_MAGE,TXT_SK_MAG_MANA_FLOW_NAME,PASSIVE,RANK_SKILLED,SELF,0,0,0,ALWAYS,TXT_SK_MAG_MANA_FLOW_DESCRIPTION,CONFIRMED,TRUE
SK_CLE_HEAL,JOB_CLERIC,TXT_SK_CLE_HEAL_NAME,ACTIVE,RANK_APPRENTICE,ALLY_LOW_HP,7,1.2,0,ALLY_HP_LT_75,TXT_SK_CLE_HEAL_DESCRIPTION,CONFIRMED,TRUE
SK_CLE_BARRIER,JOB_CLERIC,TXT_SK_CLE_BARRIER_NAME,ACTIVE,RANK_REGULAR,ALLY_LOW_HP,13,0.8,0,ALLY_HP_LT_55,TXT_SK_CLE_BARRIER_DESCRIPTION,CONFIRMED,TRUE
SK_CLE_BLESSING,JOB_CLERIC,TXT_SK_CLE_BLESSING_NAME,ACTIVE,RANK_SKILLED,ALLY_AREA,18,0,0,COMBAT_STARTED_OR_EXPIRED,TXT_SK_CLE_BLESSING_DESCRIPTION,CONFIRMED,TRUE
```

### 8.56 `status_effects.csv` (6 rows)

```csv
status_effect_id,name_text_key,category,stack_rule,duration_sec,effect_key,boss_resistance,status,enabled
STATUS_POISON,TXT_STATUS_POISON_NAME,DEBUFF,STACK_5,12,DAMAGE_OVER_TIME,0.35,TUNABLE,TRUE
STATUS_BURN,TXT_STATUS_BURN_NAME,DEBUFF,REFRESH,8,DAMAGE_OVER_TIME,0.4,TUNABLE,TRUE
STATUS_SLOW,TXT_STATUS_SLOW_NAME,DEBUFF,MAX_VALUE,5,MOVE_SPEED_DOWN,0.65,TUNABLE,TRUE
STATUS_TAUNT,TXT_STATUS_TAUNT_NAME,CONTROL,REFRESH,4,TARGET_LOCK,0.8,TUNABLE,TRUE
STATUS_BARRIER,TXT_STATUS_BARRIER_NAME,BUFF,REPLACE_HIGHER,8,ABSORB_DAMAGE,0,TUNABLE,TRUE
STATUS_BLESSING,TXT_STATUS_BLESSING_NAME,BUFF,REFRESH,18,ATTACK_DEFENSE_UP,0,TUNABLE,TRUE
```

### 8.57 `trait_job_eligibility.csv` (44 rows)

```csv
trait_id,job_id,status,enabled
TRAIT_STRONG,JOB_ARCHER,TUNABLE,TRUE
TRAIT_STRONG,JOB_CLERIC,TUNABLE,TRUE
TRAIT_STRONG,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_STRONG,JOB_MAGE,TUNABLE,TRUE
TRAIT_STRONG,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_STURDY,JOB_ARCHER,TUNABLE,TRUE
TRAIT_STURDY,JOB_CLERIC,TUNABLE,TRUE
TRAIT_STURDY,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_STURDY,JOB_MAGE,TUNABLE,TRUE
TRAIT_STURDY,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_KEEN_EYE,JOB_ARCHER,TUNABLE,TRUE
TRAIT_KEEN_EYE,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_ARCANE,JOB_CLERIC,TUNABLE,TRUE
TRAIT_ARCANE,JOB_MAGE,TUNABLE,TRUE
TRAIT_LUCKY,JOB_ARCHER,TUNABLE,TRUE
TRAIT_LUCKY,JOB_CLERIC,TUNABLE,TRUE
TRAIT_LUCKY,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_LUCKY,JOB_MAGE,TUNABLE,TRUE
TRAIT_LUCKY,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_SCAVENGER,JOB_ARCHER,TUNABLE,TRUE
TRAIT_SCAVENGER,JOB_CLERIC,TUNABLE,TRUE
TRAIT_SCAVENGER,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_SCAVENGER,JOB_MAGE,TUNABLE,TRUE
TRAIT_SCAVENGER,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,JOB_ARCHER,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,JOB_CLERIC,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,JOB_MAGE,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_POISON_RESIST,JOB_ARCHER,TUNABLE,TRUE
TRAIT_POISON_RESIST,JOB_CLERIC,TUNABLE,TRUE
TRAIT_POISON_RESIST,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_POISON_RESIST,JOB_MAGE,TUNABLE,TRUE
TRAIT_POISON_RESIST,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_FROSTBORN,JOB_ARCHER,TUNABLE,TRUE
TRAIT_FROSTBORN,JOB_CLERIC,TUNABLE,TRUE
TRAIT_FROSTBORN,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_FROSTBORN,JOB_MAGE,TUNABLE,TRUE
TRAIT_FROSTBORN,JOB_WARRIOR,TUNABLE,TRUE
TRAIT_SURVIVOR,JOB_ARCHER,TUNABLE,TRUE
TRAIT_SURVIVOR,JOB_CLERIC,TUNABLE,TRUE
TRAIT_SURVIVOR,JOB_GUARDIAN,TUNABLE,TRUE
TRAIT_SURVIVOR,JOB_MAGE,TUNABLE,TRUE
TRAIT_SURVIVOR,JOB_WARRIOR,TUNABLE,TRUE
```

### 8.58 `traits.csv` (10 rows)

```csv
trait_id,name_text_key,category,effect_key,effect_value,description_text_key,status,enabled
TRAIT_STRONG,TXT_TRAIT_STRONG_NAME,STAT,STR_PERCENT,0.06,TXT_TRAIT_STRONG_DESCRIPTION,TUNABLE,TRUE
TRAIT_STURDY,TXT_TRAIT_STURDY_NAME,STAT,HP_PERCENT,0.08,TXT_TRAIT_STURDY_DESCRIPTION,TUNABLE,TRUE
TRAIT_KEEN_EYE,TXT_TRAIT_KEEN_EYE_NAME,COMBAT,CRIT_CHANCE,0.04,TXT_TRAIT_KEEN_EYE_DESCRIPTION,TUNABLE,TRUE
TRAIT_ARCANE,TXT_TRAIT_ARCANE_NAME,COMBAT,MAGIC_DAMAGE,0.07,TXT_TRAIT_ARCANE_DESCRIPTION,TUNABLE,TRUE
TRAIT_LUCKY,TXT_TRAIT_LUCKY_NAME,COLLECTION,RARE_FIND,0.03,TXT_TRAIT_LUCKY_DESCRIPTION,TUNABLE,TRUE
TRAIT_SCAVENGER,TXT_TRAIT_SCAVENGER_NAME,COLLECTION,MATERIAL_BONUS,0.05,TXT_TRAIT_SCAVENGER_DESCRIPTION,TUNABLE,TRUE
TRAIT_BOSS_HUNTER,TXT_TRAIT_BOSS_HUNTER_NAME,BOSS,BOSS_DAMAGE,0.06,TXT_TRAIT_BOSS_HUNTER_DESCRIPTION,TUNABLE,TRUE
TRAIT_POISON_RESIST,TXT_TRAIT_POISON_RESIST_NAME,RESIST,POISON_RESIST,0.12,TXT_TRAIT_POISON_RESIST_DESCRIPTION,TUNABLE,TRUE
TRAIT_FROSTBORN,TXT_TRAIT_FROSTBORN_NAME,RESIST,FROST_RESIST,0.12,TXT_TRAIT_FROSTBORN_DESCRIPTION,TUNABLE,TRUE
TRAIT_SURVIVOR,TXT_TRAIT_SURVIVOR_NAME,AI,RETURN_HP_BONUS,0.08,TXT_TRAIT_SURVIVOR_DESCRIPTION,TUNABLE,TRUE
```

### 8.59 `tutorial_grants.csv` (9 rows)

```csv
grant_id,tutorial_step_id,line_no,reward_type,reward_id,quantity,status,enabled
GRANT_TUTORIAL_INJURY_SAFETY,TUT_03_REGION_R01_PERMISSION,1,PROGRESSION_FLAG,FLAG_TUTORIAL_FIRST_INJURY_INSTANT_TREATMENT,1,CONFIRMED,TRUE
GRANT_TUTORIAL_BLACKSMITH_BUILD_GOLD,TUT_05_RETURN_AND_SELL,1,CURRENCY,KINGDOM_GOLD,800,TUNABLE,TRUE
GRANT_TUTORIAL_BASIC_WEAPON_MATERIALS,TUT_05_RETURN_AND_SELL,1,ITEM,MAT_R01_SOFTWOOD,4,TUNABLE,TRUE
GRANT_TUTORIAL_BASIC_WEAPON_MATERIALS,TUT_05_RETURN_AND_SELL,2,ITEM,MAT_R01_WOLF_FANG,2,TUNABLE,TRUE
GRANT_TUTORIAL_FIRST_ENHANCEMENT_STONE,TUT_06_CRAFT_BASIC_WEAPON,1,ITEM,MAT_ENHANCE_1,1,TUNABLE,TRUE
GRANT_TUTORIAL_ALCHEMY_BUILD_GOLD,TUT_07_EQUIP_BASIC_WEAPON,1,CURRENCY,KINGDOM_GOLD,1000,TUNABLE,TRUE
GRANT_TUTORIAL_HEAL_SMALL_MATERIALS,TUT_07_EQUIP_BASIC_WEAPON,1,ITEM,MAT_R01_WILD_HERB,2,TUNABLE,TRUE
GRANT_TUTORIAL_HEAL_SMALL_MATERIALS,TUT_07_EQUIP_BASIC_WEAPON,2,ITEM,MAT_R01_SLIME_GEL,1,TUNABLE,TRUE
GRANT_TUTORIAL_FIRST_PROMOTION_TOKEN,TUT_08_CRAFT_HEAL_POTION,1,ITEM,MAT_PROMO_BRONZE_EMBLEM,1,TUNABLE,TRUE
```

### 8.60 `tutorial_steps.csv` (10 rows)

```csv
tutorial_step_id,order,action_type,target_id,prerequisite_step_id,skippable,status,enabled
TUT_01_KINGDOM_OVERVIEW,1,VIEW_KINGDOM,KINGDOM_1,,TRUE,CONFIRMED,TRUE
TUT_02_FIRST_RECRUIT,2,RECRUIT_FROM_POOL,TAVERN_TUTORIAL_FIRST,TUT_01_KINGDOM_OVERVIEW,TRUE,CONFIRMED,TRUE
TUT_03_REGION_R01_PERMISSION,3,SET_REGION_PERMISSION,REGION_R01,TUT_02_FIRST_RECRUIT,TRUE,CONFIRMED,TRUE
TUT_04_OBSERVE_AUTO_HUNT,4,OBSERVE_AUTONOMY_HUNT,REGION_R01,TUT_03_REGION_R01_PERMISSION,TRUE,CONFIRMED,TRUE
TUT_05_RETURN_AND_SELL,5,RETURN_AND_SELL,FAC_STORE,TUT_04_OBSERVE_AUTO_HUNT,TRUE,CONFIRMED,TRUE
TUT_06_CRAFT_BASIC_WEAPON,6,BUILD_FACILITY_AND_CRAFT_RECIPE,REC_EQ_T1_WARRIOR_WEAPON,TUT_05_RETURN_AND_SELL,TRUE,CONFIRMED,TRUE
TUT_07_EQUIP_BASIC_WEAPON,7,WAIT_FOR_MERCENARY_BUY_AND_EQUIP,EQ_T1_WARRIOR_WEAPON,TUT_06_CRAFT_BASIC_WEAPON,TRUE,CONFIRMED,TRUE
TUT_08_CRAFT_HEAL_POTION,8,BUILD_FACILITY_AND_CRAFT_RECIPE,REC_POT_HEAL_SMALL,TUT_07_EQUIP_BASIC_WEAPON,TRUE,CONFIRMED,TRUE
TUT_09_PROMOTE_REGULAR,9,COMPLETE_PROMOTION,RANK_REGULAR,TUT_08_CRAFT_HEAL_POTION,TRUE,CONFIRMED,TRUE
TUT_10_UNLOCK_REGION_R02,10,UNLOCK_REGION,REGION_R02,TUT_09_PROMOTE_REGULAR,TRUE,CONFIRMED,TRUE
```

## 9. 전체 legacy column migration matrix

### Decision

입력 단위는 29개 CSV와 같은 디렉터리의 `README.md`를 합친 30개다. 모든 source column은 아래 한 행으로 분류된다.

### Reason

표시 문자열, pipe-list, JSON cell, 조건 문자열과 metadata를 무음 폐기하지 않는다.

### Compatibility

legacy 파일은 one-way migrator 입력일 뿐 runtime package에 들어가지 않는다. 모든 report row는 source file SHA-256, physical row, source column/value, report_code를 가진다.

### Exact output

| source_file | source_column | target_file | target_column | action | conversion_rule | loss_policy | report_code |
|---|---|---|---|---|---|---|---|
| data/csv/README.md | (file) | migration report | source_document | DROPPED | runtime data가 아닌 authoring 안내문 | SHA-256와 원문 경로 보존 | MIG_SOURCE_DOCUMENT_ONLY |
| asset_register.csv | asset_id | migration report | asset_id | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_ASSET_ID |
| asset_register.csv | name | migration report | name | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_NAME |
| asset_register.csv | creator | migration report | creator | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_CREATOR |
| asset_register.csv | source_url | migration report | source_url | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_SOURCE_URL |
| asset_register.csv | version | migration report | version | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_VERSION |
| asset_register.csv | acquired_date | migration report | acquired_date | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_ACQUIRED_DATE |
| asset_register.csv | price_krw | migration report | price_krw | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_PRICE_KRW |
| asset_register.csv | license | migration report | license | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_LICENSE |
| asset_register.csv | commercial_use | migration report | commercial_use | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_COMMERCIAL_USE |
| asset_register.csv | modification_allowed | migration report | modification_allowed | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_MODIFICATION_ALLOWED |
| asset_register.csv | credit_required | migration report | credit_required | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_CREDIT_REQUIRED |
| asset_register.csv | used_in | migration report | used_in | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_USED_IN |
| asset_register.csv | notes | asset_register.csv;localizations.csv | notes_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_ASSET_REGISTER_NOTES |
| asset_register.csv | status | migration report | status | DROPPED | 유일한 ASSET_TEMPLATE row는 runtime 비활성 template | 원문 row 전체 보존; 5 internal asset row로 REPLACED | MIG_ASSET_REGISTER_STATUS |
| balance_parameters.csv | key | runtime_config.csv | config_key | RENAMED | 14-key range/type 결정표 적용 | 무손실; description은 localization | MIG_BALANCE_PARAMETERS_KEY |
| balance_parameters.csv | value | runtime_config.csv | value | DERIVED | 14-key range/type 결정표 적용 | 무손실; description은 localization | MIG_BALANCE_PARAMETERS_VALUE |
| balance_parameters.csv | unit | runtime_config.csv | unit | DERIVED | 14-key range/type 결정표 적용 | 무손실; description은 localization | MIG_BALANCE_PARAMETERS_UNIT |
| balance_parameters.csv | description | runtime_config.csv;localizations.csv | description_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_BALANCE_PARAMETERS_DESCRIPTION |
| balance_parameters.csv | status | runtime_config.csv | status | COPIED | 14-key range/type 결정표 적용 | 무손실; description은 localization | MIG_BALANCE_PARAMETERS_STATUS |
| enhancement_rules.csv | target_level | enhancement_rules.csv | target_level | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_TARGET_LEVEL |
| enhancement_rules.csv | success_chance | enhancement_rules.csv | success_chance | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_SUCCESS_CHANCE |
| enhancement_rules.csv | stone_item_id | enhancement_rules.csv | stone_item_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_STONE_ITEM_ID |
| enhancement_rules.csv | stone_qty | enhancement_rules.csv | stone_quantity | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_ENHANCEMENT_RULES_STONE_QTY |
| enhancement_rules.csv | gold_cost | enhancement_rules.csv | personal_gold_cost | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_ENHANCEMENT_RULES_GOLD_COST |
| enhancement_rules.csv | fail_pity_increment | enhancement_rules.csv | fail_pity_increment | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_FAIL_PITY_INCREMENT |
| enhancement_rules.csv | destroy_on_fail | enhancement_rules.csv | destroy_on_fail | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_DESTROY_ON_FAIL |
| enhancement_rules.csv | downrank_on_fail | enhancement_rules.csv | downrank_on_fail | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_DOWNRANK_ON_FAIL |
| enhancement_rules.csv | status | enhancement_rules.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_ENHANCEMENT_RULES_STATUS |
| equipment_quality.csv | quality_id | equipment_qualities.csv | quality_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_QUALITY_ID |
| equipment_quality.csv | name_ko | equipment_qualities.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_EQUIPMENT_QUALITY_NAME_KO |
| equipment_quality.csv | order | equipment_qualities.csv | order | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_ORDER |
| equipment_quality.csv | stat_multiplier | equipment_qualities.csv | stat_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_STAT_MULTIPLIER |
| equipment_quality.csv | innate_affix_chance | equipment_qualities.csv | innate_affix_chance | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_INNATE_AFFIX_CHANCE |
| equipment_quality.csv | ui_token | equipment_qualities.csv | ui_token | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_UI_TOKEN |
| equipment_quality.csv | status | equipment_qualities.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_QUALITY_STATUS |
| equipment_templates.csv | equipment_id | equipment_templates.csv | equipment_template_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_EQUIPMENT_ID |
| equipment_templates.csv | name_ko | equipment_templates.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_EQUIPMENT_TEMPLATES_NAME_KO |
| equipment_templates.csv | tier | equipment_templates.csv | tier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_TIER |
| equipment_templates.csv | slot | equipment_templates.csv | slot | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_SLOT |
| equipment_templates.csv | allowed_jobs | equipment_job_eligibility.csv | job_id | SPLIT | `\|`를 migration에서만 분해하고 job_id UTF-8 ordinal | 무손실 관계 row | MIG_EQUIPMENT_TEMPLATES_ALLOWED_JOBS |
| equipment_templates.csv | profile | equipment_templates.csv | profile | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_PROFILE |
| equipment_templates.csv | base_power | equipment_templates.csv | base_power | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_BASE_POWER |
| equipment_templates.csv | source | equipment_templates.csv | source | REPLACED | CRAFT→CRAFT; BOSS_CRAFT→BOSS | 1:1 의미 보존 | MIG_EQUIPMENT_TEMPLATES_SOURCE |
| equipment_templates.csv | boss_id | equipment_templates.csv | boss_id | COPIED | source=BOSS이면 raids.raid_id hard FK; 그 외 null | 무손실 | MIG_EQUIPMENT_TEMPLATES_BOSS_ID |
| equipment_templates.csv | status | equipment_templates.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_EQUIPMENT_TEMPLATES_STATUS |
| facilities.csv | facility_id | facilities.csv;facility_levels.csv | facility_id;operation_mode,required_profession_id | DERIVED | 8 registry row와 32 level row; SYSTEM_*/NONE은 mode로 변환 | 원문 migration report 보존 | MIG_FACILITIES_FACILITY_ID |
| facilities.csv | name_ko | facility_levels.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_FACILITIES_NAME_KO |
| facilities.csv | level | facility_levels.csv | level | COPIED | 32 level row | 무손실 | MIG_FACILITIES_LEVEL |
| facilities.csv | required_kingdom_stage | facility_levels.csv | required_kingdom_stage_id | RENAMED | 32 level row | 무손실 | MIG_FACILITIES_REQUIRED_KINGDOM_STAGE |
| facilities.csv | assigned_npc_profession | facilities.csv;facility_levels.csv | facility_id;operation_mode,required_profession_id | DERIVED | 8 registry row와 32 level row; SYSTEM_*/NONE은 mode로 변환 | 원문 migration report 보존 | MIG_FACILITIES_ASSIGNED_NPC_PROFESSION |
| facilities.csv | build_or_upgrade_gold | facility_levels.csv | build_or_upgrade_kingdom_gold | RENAMED | 32 level row | 무손실 | MIG_FACILITIES_BUILD_OR_UPGRADE_GOLD |
| facilities.csv | materials_json | facility_upgrade_materials.csv | item_id,quantity | SPLIT | JSON object key를 UTF-8 ordinal로 분해 | 무손실 관계 row | MIG_FACILITIES_MATERIALS_JSON |
| facilities.csv | effect_summary | facility_levels.csv;localizations.csv | effect_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_FACILITIES_EFFECT_SUMMARY |
| facilities.csv | status | facility_levels.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_FACILITIES_STATUS |
| grades.csv | grade_id | mercenary_grades.csv | grade_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_GRADE_ID |
| grades.csv | display_name | mercenary_grades.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_GRADES_DISPLAY_NAME |
| grades.csv | base_stat_multiplier | mercenary_grades.csv | base_stat_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_BASE_STAT_MULTIPLIER |
| grades.csv | growth_multiplier | mercenary_grades.csv | growth_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_GROWTH_MULTIPLIER |
| grades.csv | initial_trait_count | mercenary_grades.csv | initial_trait_count | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_INITIAL_TRAIT_COUNT |
| grades.csv | promotion_cost_multiplier | mercenary_grades.csv | promotion_cost_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_PROMOTION_COST_MULTIPLIER |
| grades.csv | tavern_eligible | mercenary_grades.csv | tavern_eligible | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_TAVERN_ELIGIBLE |
| grades.csv | special_pool_eligible | mercenary_grades.csv | special_pool_eligible | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_SPECIAL_POOL_ELIGIBLE |
| grades.csv | ui_color | mercenary_grades.csv | ui_color | COPIED | uppercase hex source lexical 유지 | 무손실 | MIG_GRADES_UI_COLOR |
| grades.csv | status | mercenary_grades.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_GRADES_STATUS |
| jobs.csv | job_id | jobs.csv | job_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_JOB_ID |
| jobs.csv | name_ko | jobs.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_JOBS_NAME_KO |
| jobs.csv | role | jobs.csv | role | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_ROLE |
| jobs.csv | armor_profile | jobs.csv | armor_profile | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_ARMOR_PROFILE |
| jobs.csv | weapon_type | jobs.csv | weapon_type | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_WEAPON_TYPE |
| jobs.csv | primary_stat | jobs.csv | primary_stat | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_PRIMARY_STAT |
| jobs.csv | secondary_stat | jobs.csv | secondary_stat | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_SECONDARY_STAT |
| jobs.csv | ai_priority | jobs.csv | ai_priority | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_AI_PRIORITY |
| jobs.csv | initial_skill_ids | job_skill_unlocks.csv | skill_id,slot_no | SPLIT | `\|` source 순서가 slot_no 1..N | 무손실 관계 row | MIG_JOBS_INITIAL_SKILL_IDS |
| jobs.csv | notes | jobs.csv;localizations.csv | notes_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_JOBS_NOTES |
| jobs.csv | status | jobs.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_JOBS_STATUS |
| kingdom_stages.csv | stage_id | kingdom_stages.csv | stage_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_STAGE_ID |
| kingdom_stages.csv | name_ko | kingdom_stages.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_KINGDOM_STAGES_NAME_KO |
| kingdom_stages.csv | order | kingdom_stages.csv | order | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_ORDER |
| kingdom_stages.csv | active_slots | kingdom_stages.csv | active_slots | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_ACTIVE_SLOTS |
| kingdom_stages.csv | roster_slots | kingdom_stages.csv | roster_slots | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_ROSTER_SLOTS |
| kingdom_stages.csv | facility_level_cap | kingdom_stages.csv | facility_level_cap | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_FACILITY_LEVEL_CAP |
| kingdom_stages.csv | unlock_condition | condition_groups.csv;conditions.csv;condition_group_members.csv | condition_group_id;condition_id;member_no | REPLACED | §8 exact condition mapping | 원문 source_value report 보존 | MIG_KINGDOM_STAGES_UNLOCK_CONDITION |
| kingdom_stages.csv | final_raid_unlocked | kingdom_stages.csv | final_raid_unlocked | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_FINAL_RAID_UNLOCKED |
| kingdom_stages.csv | status | kingdom_stages.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_KINGDOM_STAGES_STATUS |
| localization_ko.csv | key | localizations.csv | key | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_LOCALIZATION_KO_KEY |
| localization_ko.csv | ko | localizations.csv | ko | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_LOCALIZATION_KO_KO |
| localization_ko.csv | context | localizations.csv | context | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_LOCALIZATION_KO_CONTEXT |
| localization_ko.csv | status | localizations.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_LOCALIZATION_KO_STATUS |
| loot_entries.csv | loot_table_id | loot_tables.csv;loot_entries.csv | loot_table_id | SPLIT | first appearance로 table row, source ordinal로 entry_no | 무손실 | MIG_LOOT_ENTRIES_LOOT_TABLE_ID |
| loot_entries.csv | reward_type | loot_entries.csv | reward_type | REPLACED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_REWARD_TYPE |
| loot_entries.csv | reward_id | loot_entries.csv | reward_id | REPLACED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_REWARD_ID |
| loot_entries.csv | chance | loot_entries.csv | probability | RENAMED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_CHANCE |
| loot_entries.csv | min_qty | loot_entries.csv | min_quantity | RENAMED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_MIN_QTY |
| loot_entries.csv | max_qty | loot_entries.csv | max_quantity | RENAMED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_MAX_QTY |
| loot_entries.csv | condition | loot_entries.csv | condition_id | REPLACED | MATERIAL→ITEM; random tier 1..5→spec; raid condition→condition_id | 무손실 의미 변환 | MIG_LOOT_ENTRIES_CONDITION |
| loot_entries.csv | status | loot_entries.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_LOOT_ENTRIES_STATUS |
| materials.csv | item_id | items.csv | item_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_ITEM_ID |
| materials.csv | name_ko | items.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_MATERIALS_NAME_KO |
| materials.csv | category | items.csv | category | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_CATEGORY |
| materials.csv | tier | items.csv | tier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_TIER |
| materials.csv | source_region | items.csv | source_type,source_id | SPLIT | REGION_*/RAID_*는 discriminator+FK; 세 source sentinel은 id null | 무손실 | MIG_MATERIALS_SOURCE_REGION |
| materials.csv | base_value | items.csv | sell_price | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_MATERIALS_BASE_VALUE |
| materials.csv | rarity | items.csv | rarity | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_RARITY |
| materials.csv | primary_uses | migration report | source_value | DROPPED | authoring hint이며 runtime executable field가 아님; pipe-list 방출 금지 | 원문·row·column 보존 | MIG_MATERIALS_PRIMARY_USES |
| materials.csv | stack_limit | items.csv | stack_limit | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_STACK_LIMIT |
| materials.csv | status | items.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MATERIALS_STATUS |
| monsters.csv | monster_id | monsters.csv | monster_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_MONSTER_ID |
| monsters.csv | region_id | monsters.csv | region_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_REGION_ID |
| monsters.csv | name_ko | monsters.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_MONSTERS_NAME_KO |
| monsters.csv | type | monsters.csv | type | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_TYPE |
| monsters.csv | level | monsters.csv | level | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_LEVEL |
| monsters.csv | hp | monsters.csv | hp | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_HP |
| monsters.csv | attack | monsters.csv | attack | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_ATTACK |
| monsters.csv | defense | monsters.csv | defense | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_DEFENSE |
| monsters.csv | xp | monsters.csv | xp | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_XP |
| monsters.csv | bounty_gold | monsters.csv | bounty_personal_gold | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_MONSTERS_BOUNTY_GOLD |
| monsters.csv | loot_table_id | monsters.csv | loot_table_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_LOOT_TABLE_ID |
| monsters.csv | behavior_tag | monsters.csv | behavior_tag | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_BEHAVIOR_TAG |
| monsters.csv | spawn_weight | monsters.csv | spawn_weight | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_SPAWN_WEIGHT |
| monsters.csv | raid_id | monsters.csv | raid_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_RAID_ID |
| monsters.csv | status | monsters.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_MONSTERS_STATUS |
| npc_professions.csv | profession_id | npc_professions.csv | profession_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFESSIONS_PROFESSION_ID |
| npc_professions.csv | name_ko | npc_professions.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_NPC_PROFESSIONS_NAME_KO |
| npc_professions.csv | facility_id | npc_professions.csv | facility_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFESSIONS_FACILITY_ID |
| npc_professions.csv | work_unit | npc_professions.csv | work_unit | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFESSIONS_WORK_UNIT |
| npc_professions.csv | primary_effect | npc_professions.csv | primary_effect | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFESSIONS_PRIMARY_EFFECT |
| npc_professions.csv | status | npc_professions.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFESSIONS_STATUS |
| npc_proficiency.csv | proficiency_id | npc_proficiency_levels.csv | proficiency_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_NPC_PROFICIENCY_PROFICIENCY_ID |
| npc_proficiency.csv | name_ko | npc_proficiency_levels.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_NPC_PROFICIENCY_NAME_KO |
| npc_proficiency.csv | order | npc_proficiency_levels.csv | order | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_ORDER |
| npc_proficiency.csv | xp_required | npc_proficiency_levels.csv | xp_required | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_XP_REQUIRED |
| npc_proficiency.csv | speed_multiplier | npc_proficiency_levels.csv | speed_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_SPEED_MULTIPLIER |
| npc_proficiency.csv | material_efficiency | npc_proficiency_levels.csv | material_efficiency | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_MATERIAL_EFFICIENCY |
| npc_proficiency.csv | quality_bonus | npc_proficiency_levels.csv | quality_bonus | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_QUALITY_BONUS |
| npc_proficiency.csv | status | npc_proficiency_levels.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_NPC_PROFICIENCY_STATUS |
| personalities.csv | personality_id | personalities.csv | personality_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_PERSONALITY_ID |
| personalities.csv | name_ko | personalities.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_PERSONALITIES_NAME_KO |
| personalities.csv | buy_threshold_multiplier | personalities.csv | buy_threshold_multiplier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_BUY_THRESHOLD_MULTIPLIER |
| personalities.csv | risk_tolerance | personalities.csv | risk_tolerance | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_RISK_TOLERANCE |
| personalities.csv | return_hp_threshold | personalities.csv | return_hp_threshold | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_RETURN_HP_THRESHOLD |
| personalities.csv | preferred_behavior | personalities.csv | preferred_behavior | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_PREFERRED_BEHAVIOR |
| personalities.csv | description | personalities.csv;localizations.csv | description_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_PERSONALITIES_DESCRIPTION |
| personalities.csv | status | personalities.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PERSONALITIES_STATUS |
| potions.csv | potion_id | potions.csv | potion_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_POTION_ID |
| potions.csv | name_ko | potions.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_POTIONS_NAME_KO |
| potions.csv | effect_type | potions.csv | effect_type | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_EFFECT_TYPE |
| potions.csv | effect_value | potions.csv | effect_value | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_EFFECT_VALUE |
| potions.csv | duration_sec | potions.csv | duration_sec | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_DURATION_SEC |
| potions.csv | auto_use_condition | potions.csv | auto_use_condition | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_AUTO_USE_CONDITION |
| potions.csv | tier | potions.csv | tier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_TIER |
| potions.csv | status | potions.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_POTIONS_STATUS |
| promotion_grade_requirements.csv | grade_id | promotion_grade_requirements.csv | grade_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_GRADE_ID |
| promotion_grade_requirements.csv | from_rank | promotion_grade_requirements.csv | from_rank_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_FROM_RANK |
| promotion_grade_requirements.csv | to_rank | promotion_grade_requirements.csv | to_rank_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_TO_RANK |
| promotion_grade_requirements.csv | item_id | promotion_grade_requirements.csv | item_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_ITEM_ID |
| promotion_grade_requirements.csv | item_qty | promotion_grade_requirements.csv | item_quantity | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_ITEM_QTY |
| promotion_grade_requirements.csv | status | promotion_grade_requirements.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_PROMOTION_GRADE_REQUIREMENTS_STATUS |
| raid_parts.csv | raid_id | raid_parts.csv | raid_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAID_PARTS_RAID_ID |
| raid_parts.csv | part_id | raid_parts.csv | part_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAID_PARTS_PART_ID |
| raid_parts.csv | name_ko | raid_parts.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_RAID_PARTS_NAME_KO |
| raid_parts.csv | max_hp_ratio | raid_parts.csv | max_hp_ratio | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAID_PARTS_MAX_HP_RATIO |
| raid_parts.csv | break_reward_condition | condition_groups.csv;reward_groups.csv | break_condition_group_id;break_reward_group_id | REPLACED | 7 exact condition + guaranteed part reward group | 원문 report 보존 | MIG_RAID_PARTS_BREAK_REWARD_CONDITION |
| raid_parts.csv | behavior_change | raid_part_effects.csv;raid_parts.csv | effect_id;behavior_change | SPLIT | 6 unique effect registry와 7 references | 무손실 | MIG_RAID_PARTS_BEHAVIOR_CHANGE |
| raid_parts.csv | priority_hint | raid_parts.csv;localizations.csv | priority_hint_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_RAID_PARTS_PRIORITY_HINT |
| raid_parts.csv | status | raid_parts.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAID_PARTS_STATUS |
| raids.csv | raid_id | raids.csv | raid_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_RAID_ID |
| raids.csv | name_ko | raids.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_RAIDS_NAME_KO |
| raids.csv | boss_monster_id | raids.csv | boss_monster_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_BOSS_MONSTER_ID |
| raids.csv | min_rank_id | raids.csv | min_rank_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_MIN_RANK_ID |
| raids.csv | party_min | raids.csv | party_min | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_PARTY_MIN |
| raids.csv | party_max | raids.csv | party_max | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_PARTY_MAX |
| raids.csv | unlock_condition | raids.csv;condition_groups.csv;reward_groups.csv | unlock_condition_group_id | REPLACED | §8 exact raid condition/reward mapping | 원문 report 보존 | MIG_RAIDS_UNLOCK_CONDITION |
| raids.csv | first_clear_reward | raids.csv;condition_groups.csv;reward_groups.csv | first_clear_reward_group_id | REPLACED | §8 exact raid condition/reward mapping | 원문 report 보존 | MIG_RAIDS_FIRST_CLEAR_REWARD |
| raids.csv | time_limit_sec | raids.csv | time_limit_sec | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_TIME_LIMIT_SEC |
| raids.csv | repeat_difficulties | raid_difficulties.csv | difficulty | SPLIT | NORMAL,HARD,CORRUPTED 3행으로 고정 | 무손실 관계 row | MIG_RAIDS_REPEAT_DIFFICULTIES |
| raids.csv | status | raids.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RAIDS_STATUS |
| ranks.csv | rank_id | mercenary_ranks.csv | rank_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_RANK_ID |
| ranks.csv | name_ko | mercenary_ranks.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_RANKS_NAME_KO |
| ranks.csv | order | mercenary_ranks.csv | order | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_ORDER |
| ranks.csv | max_level | mercenary_ranks.csv | max_level | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_MAX_LEVEL |
| ranks.csv | min_region_tier | mercenary_ranks.csv | min_region_tier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_MIN_REGION_TIER |
| ranks.csv | skill_slot_count | mercenary_ranks.csv | skill_slot_count | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_SKILL_SLOT_COUNT |
| ranks.csv | trait_slot_bonus | mercenary_ranks.csv | trait_slot_bonus | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_TRAIT_SLOT_BONUS |
| ranks.csv | raid_access | mercenary_ranks.csv | raid_access | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_RAID_ACCESS |
| ranks.csv | promotion_to | mercenary_ranks.csv | promotion_to | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_PROMOTION_TO |
| ranks.csv | promotion_token | mercenary_ranks.csv | promotion_token_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_RANKS_PROMOTION_TOKEN |
| ranks.csv | base_personal_gold | mercenary_ranks.csv | base_personal_gold | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_BASE_PERSONAL_GOLD |
| ranks.csv | contribution_required | mercenary_ranks.csv | contribution_required | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_CONTRIBUTION_REQUIRED |
| ranks.csv | additional_condition | mercenary_ranks.csv | additional_condition | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_ADDITIONAL_CONDITION |
| ranks.csv | status | mercenary_ranks.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RANKS_STATUS |
| recipes.csv | recipe_id | recipes.csv | recipe_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_RECIPE_ID |
| recipes.csv | name_ko | recipes.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_RECIPES_NAME_KO |
| recipes.csv | facility_id | recipes.csv | facility_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_FACILITY_ID |
| recipes.csv | facility_level | recipes.csv | facility_level | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_FACILITY_LEVEL |
| recipes.csv | npc_proficiency | recipes.csv | npc_proficiency_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_RECIPES_NPC_PROFICIENCY |
| recipes.csv | output_type | recipe_outputs.csv | reward_type | RENAMED | MATERIAL→ITEM, EQUIPMENT→EQUIPMENT_TEMPLATE | 무손실 | MIG_RECIPES_OUTPUT_TYPE |
| recipes.csv | output_id | recipe_outputs.csv | reward_id | RENAMED | MATERIAL→ITEM, EQUIPMENT→EQUIPMENT_TEMPLATE | 무손실 | MIG_RECIPES_OUTPUT_ID |
| recipes.csv | output_qty | recipe_outputs.csv | quantity | RENAMED | MATERIAL→ITEM, EQUIPMENT→EQUIPMENT_TEMPLATE | 무손실 | MIG_RECIPES_OUTPUT_QTY |
| recipes.csv | ingredients_json | recipe_materials.csv | item_id,quantity,material_no | SPLIT | JSON key UTF-8 ordinal이 material_no 1..N | 무손실 관계 row | MIG_RECIPES_INGREDIENTS_JSON |
| recipes.csv | craft_seconds | recipes.csv | craft_seconds | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_CRAFT_SECONDS |
| recipes.csv | quality_roll | recipes.csv | quality_roll | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_QUALITY_ROLL |
| recipes.csv | status | recipes.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_RECIPES_STATUS |
| recruitment_pity.csv | pity_id | recruitment_pity_groups.csv;recruitment_pity_rules.csv | pity_id | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_PITY_ID |
| recruitment_pity.csv | pool_category | recruitment_pity_groups.csv;recruitment_pity_rules.csv | pool_category | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_POOL_CATEGORY |
| recruitment_pity.csv | trigger_count | recruitment_pity_groups.csv;recruitment_pity_rules.csv | trigger_count | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_TRIGGER_COUNT |
| recruitment_pity.csv | guaranteed_grade | recruitment_pity_groups.csv;recruitment_pity_rules.csv | guaranteed_grade | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_GUARANTEED_GRADE |
| recruitment_pity.csv | reset_on_grade_or_higher | recruitment_pity_groups.csv;recruitment_pity_rules.csv | reset_on_grade_or_higher | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_RESET_ON_GRADE_OR_HIGHER |
| recruitment_pity.csv | carry_over | recruitment_pity_groups.csv;recruitment_pity_rules.csv | carry_over | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_CARRY_OVER |
| recruitment_pity.csv | status | recruitment_pity_groups.csv;recruitment_pity_rules.csv | status | REPLACED | SPECIAL standard group 2 rules; PITY_RATEUP_JOB는 migration report | rate-up source 원문 보존 | MIG_RECRUITMENT_PITY_STATUS |
| recruitment_pools.csv | pool_id | recruitment_pools.csv;recruitment_pool_entries.csv | pool_id | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_POOL_ID |
| recruitment_pools.csv | pool_type | recruitment_pools.csv;recruitment_pool_entries.csv | pool_type | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_POOL_TYPE |
| recruitment_pools.csv | grade_id | recruitment_pools.csv;recruitment_pool_entries.csv | grade_id | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_GRADE_ID |
| recruitment_pools.csv | weight | recruitment_pools.csv;recruitment_pool_entries.csv | weight | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_WEIGHT |
| recruitment_pools.csv | job_filter | recruitment_pools.csv;recruitment_pool_entries.csv | job_filter | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_JOB_FILTER |
| recruitment_pools.csv | status | recruitment_pools.csv;recruitment_pool_entries.csv | status | REPLACED | 3 legacy logical pools→6 headers/17 entries | 9 weight exact 보존 | MIG_RECRUITMENT_POOLS_STATUS |
| refine_options.csv | refine_id | refine_options.csv | refine_option_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_REFINE_OPTIONS_REFINE_ID |
| refine_options.csv | name_ko | refine_options.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_REFINE_OPTIONS_NAME_KO |
| refine_options.csv | option_group | refine_options.csv | option_group | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_OPTION_GROUP |
| refine_options.csv | stat_key | refine_options.csv | stat_key | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_STAT_KEY |
| refine_options.csv | min_value | refine_options.csv | min_value | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_MIN_VALUE |
| refine_options.csv | max_value | refine_options.csv | max_value | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_MAX_VALUE |
| refine_options.csv | material_item_id | refine_options.csv | material_item_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_MATERIAL_ITEM_ID |
| refine_options.csv | material_qty | refine_options.csv | material_quantity | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_REFINE_OPTIONS_MATERIAL_QTY |
| refine_options.csv | gold_cost | refine_options.csv | personal_gold_cost | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_REFINE_OPTIONS_GOLD_COST |
| refine_options.csv | status | refine_options.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REFINE_OPTIONS_STATUS |
| regions.csv | region_id | regions.csv | region_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_REGION_ID |
| regions.csv | name_ko | regions.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_REGIONS_NAME_KO |
| regions.csv | order | regions.csv | order | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_ORDER |
| regions.csv | tier | regions.csv | tier | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_TIER |
| regions.csv | min_rank_id | regions.csv | min_rank_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_MIN_RANK_ID |
| regions.csv | unlock_type | condition_groups.csv;conditions.csv;condition_group_members.csv;regions.csv | unlock_condition_group_id | REPLACED | 5개 region exact group; R02는 TUT_09 완료, R04는 FAC_ALCHEMY>=2 AND guardian kill | 원문 type/value report 보존 | MIG_REGIONS_UNLOCK_TYPE |
| regions.csv | unlock_value | condition_groups.csv;conditions.csv;condition_group_members.csv;regions.csv | unlock_condition_group_id | REPLACED | 5개 region exact group; R02는 TUT_09 완료, R04는 FAC_ALCHEMY>=2 AND guardian kill | 원문 type/value report 보존 | MIG_REGIONS_UNLOCK_VALUE |
| regions.csv | kingdom_stage | regions.csv | kingdom_stage_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_REGIONS_KINGDOM_STAGE |
| regions.csv | recommended_power | regions.csv | recommended_power | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_RECOMMENDED_POWER |
| regions.csv | max_active | regions.csv | max_active | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_MAX_ACTIVE |
| regions.csv | environment_tag | regions.csv | environment_tag | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_ENVIRONMENT_TAG |
| regions.csv | raid_gate_id | regions.csv | raid_gate_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_RAID_GATE_ID |
| regions.csv | offline_efficiency | regions.csv | offline_efficiency | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_OFFLINE_EFFICIENCY |
| regions.csv | status | regions.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_REGIONS_STATUS |
| skills.csv | skill_id | skills.csv | skill_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_SKILL_ID |
| skills.csv | job_id | skills.csv | job_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_JOB_ID |
| skills.csv | name_ko | skills.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_SKILLS_NAME_KO |
| skills.csv | kind | skills.csv | kind | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_KIND |
| skills.csv | unlock_rank | skills.csv | unlock_rank_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_SKILLS_UNLOCK_RANK |
| skills.csv | target | skills.csv | target | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_TARGET |
| skills.csv | cooldown_sec | skills.csv | cooldown_sec | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_COOLDOWN_SEC |
| skills.csv | power_coeff | skills.csv | power_coeff | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_POWER_COEFF |
| skills.csv | resource_cost | skills.csv | resource_cost | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_RESOURCE_COST |
| skills.csv | condition | skills.csv | condition | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_CONDITION |
| skills.csv | description | skills.csv;localizations.csv | description_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_SKILLS_DESCRIPTION |
| skills.csv | status | skills.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_SKILLS_STATUS |
| status_effects.csv | status_id | status_effects.csv | status_effect_id | RENAMED | lexical normalization 후 값 동일 | 무손실 | MIG_STATUS_EFFECTS_STATUS_ID |
| status_effects.csv | name_ko | status_effects.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_STATUS_EFFECTS_NAME_KO |
| status_effects.csv | category | status_effects.csv | category | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_CATEGORY |
| status_effects.csv | stack_rule | status_effects.csv | stack_rule | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_STACK_RULE |
| status_effects.csv | duration_sec | status_effects.csv | duration_sec | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_DURATION_SEC |
| status_effects.csv | effect_key | status_effects.csv | effect_key | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_EFFECT_KEY |
| status_effects.csv | boss_resistance | status_effects.csv | boss_resistance | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_BOSS_RESISTANCE |
| status_effects.csv | status | status_effects.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_STATUS_EFFECTS_STATUS |
| traits.csv | trait_id | traits.csv | trait_id | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_TRAITS_TRAIT_ID |
| traits.csv | name_ko | traits.csv;localizations.csv | name_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_TRAITS_NAME_KO |
| traits.csv | category | traits.csv | category | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_TRAITS_CATEGORY |
| traits.csv | effect_key | traits.csv | effect_key | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_TRAITS_EFFECT_KEY |
| traits.csv | effect_value | traits.csv | effect_value | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_TRAITS_EFFECT_VALUE |
| traits.csv | applicable_jobs | trait_job_eligibility.csv | job_id | SPLIT | ALL은 jobs 5행, 그 외 `\|` 분해 후 ordinal | 무손실 관계 row | MIG_TRAITS_APPLICABLE_JOBS |
| traits.csv | description | traits.csv;localizations.csv | description_text_key;text_value | SPLIT | TXT_<row-id>_<role> key 생성; ko-KR 원문 보존 | 무손실 | MIG_TRAITS_DESCRIPTION |
| traits.csv | status | traits.csv | status | COPIED | BOM 제거·trim 외 의미 동일 | 무손실 | MIG_TRAITS_STATUS |

## 10. Localization bundle

### Decision

`localizations.csv`의 exact 전체 결과는 8장의 470 rows다. key 후보는 `TXT_` + source StableId + semantic suffix다. 64자를 넘으면 후보의 앞 50자를 underscore 경계에서 자르고 `_` + 원 후보 SHA-256 uppercase 앞 12 hex를 붙인다. `(locale,text_key)`가 PK이며 현재 locale은 `ko-KR`이다.

### Reason

같은 한국어라도 entity/field context가 다르면 별도 key를 유지해야 변경 이력과 source provenance를 잃지 않는다.

### Compatibility

기존 19 key는 byte-for-byte 유지한다. `name_ko`, `display_name`, `description`, `notes`, `effect_summary`, `priority_hint`, runtime config description은 모두 별도 row다. `primary_uses`는 표시 문구가 아니라 pipe-list authoring hint이므로 canonical cell로 옮기지 않고 migration report에 원문을 보존한다. 내부 asset metadata의 `name/creator/license/notes`는 개발·라이선스 registry 원문이므로 UI localization 대상이 아니다.

### Exact output

각 generated row의 migration report는 source CSV/physical row/column, generated text_key를 기록한다. key 충돌 시 문자열이 같아도 자동 merge하지 않고 `CSV_LOCALIZATION_KEY_COLLISION`으로 실패한다.

## 11. FK·tagged-union closure

### Decision

| source | fields | enabled source refs | target | target fields | enabled targets | mode | result |
|---|---|---|---|---|---|---|---|
| condition_group_members.csv | condition_group_id | 38 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| condition_group_members.csv | condition_id | 36 | conditions.csv | condition_id | 34 | HARD | CLOSED |
| condition_group_members.csv | child_group_id | 2 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| currencies.csv | name_text_key | 4 | localizations.csv | text_key | 470 | HARD | CLOSED |
| enhancement_rules.csv | stone_item_id | 10 | items.csv | item_id | 53 | HARD | CLOSED |
| equipment_job_eligibility.csv | equipment_template_id | 110 | equipment_templates.csv | equipment_template_id | 80 | HARD | CLOSED |
| equipment_job_eligibility.csv | job_id | 110 | jobs.csv | job_id | 5 | HARD | CLOSED |
| equipment_qualities.csv | name_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| equipment_quality_weights.csv | quality_id | 20 | equipment_qualities.csv | quality_id | 5 | HARD | CLOSED |
| equipment_templates.csv | name_text_key | 80 | localizations.csv | text_key | 470 | HARD | CLOSED |
| equipment_templates.csv | boss_id | 10 | raids.csv | raid_id | 2 | HARD | CLOSED |
| facilities.csv | name_text_key | 8 | localizations.csv | text_key | 470 | HARD | CLOSED |
| facilities.csv | required_profession_id | 4 | npc_professions.csv | profession_id | 4 | HARD | CLOSED |
| facility_levels.csv | effect_text_key | 32 | localizations.csv | text_key | 470 | HARD | CLOSED |
| facility_levels.csv | facility_id | 32 | facilities.csv | facility_id | 8 | HARD | CLOSED |
| facility_levels.csv | required_kingdom_stage_id | 32 | kingdom_stages.csv | stage_id | 5 | HARD | CLOSED |
| facility_upgrade_materials.csv | facility_id+level | 40 | facility_levels.csv | facility_id+level | 32 | HARD | CLOSED |
| facility_upgrade_materials.csv | item_id | 40 | items.csv | item_id | 53 | HARD | CLOSED |
| items.csv | name_text_key | 53 | localizations.csv | text_key | 470 | HARD | CLOSED |
| job_skill_unlocks.csv | job_id | 15 | jobs.csv | job_id | 5 | HARD | CLOSED |
| job_skill_unlocks.csv | skill_id | 15 | skills.csv | skill_id | 15 | HARD | CLOSED |
| job_skill_unlocks.csv | unlock_rank_id | 15 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| jobs.csv | name_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| jobs.csv | notes_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| kingdom_stages.csv | name_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| kingdom_stages.csv | unlock_condition_group_id | 5 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| loot_entries.csv | loot_table_id | 88 | loot_tables.csv | loot_table_id | 27 | HARD | CLOSED |
| loot_entries.csv | condition_id | 10 | conditions.csv | condition_id | 34 | HARD | CLOSED |
| mercenary_appearance_pool_entries.csv | job_id | 5 | jobs.csv | job_id | 5 | HARD | CLOSED |
| mercenary_appearance_pool_entries.csv | appearance_asset_id | 5 | asset_register.csv | asset_id | 5 | HARD | CLOSED |
| mercenary_generation_profiles.csv | initial_rank_id | 1 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| mercenary_generation_profiles.csv | fixed_personality_id | 0 | personalities.csv | personality_id | 6 | HARD | CLOSED |
| mercenary_grades.csv | name_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| mercenary_ranks.csv | name_text_key | 6 | localizations.csv | text_key | 470 | HARD | CLOSED |
| mercenary_ranks.csv | promotion_to | 5 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| mercenary_ranks.csv | promotion_token_id | 5 | items.csv | item_id | 53 | HARD | CLOSED |
| monsters.csv | name_text_key | 27 | localizations.csv | text_key | 470 | HARD | CLOSED |
| monsters.csv | region_id | 27 | regions.csv | region_id | 5 | HARD | CLOSED |
| monsters.csv | loot_table_id | 27 | loot_tables.csv | loot_table_id | 27 | HARD | CLOSED |
| monsters.csv | raid_id | 2 | raids.csv | raid_id | 2 | HARD | CLOSED |
| npc_professions.csv | name_text_key | 4 | localizations.csv | text_key | 470 | HARD | CLOSED |
| npc_professions.csv | facility_id | 4 | facilities.csv | facility_id | 8 | HARD | CLOSED |
| npc_proficiency_levels.csv | name_text_key | 4 | localizations.csv | text_key | 470 | HARD | CLOSED |
| personalities.csv | name_text_key | 6 | localizations.csv | text_key | 470 | HARD | CLOSED |
| personalities.csv | description_text_key | 6 | localizations.csv | text_key | 470 | HARD | CLOSED |
| potions.csv | name_text_key | 7 | localizations.csv | text_key | 470 | HARD | CLOSED |
| promotion_grade_requirements.csv | grade_id | 6 | mercenary_grades.csv | grade_id | 5 | HARD | CLOSED |
| promotion_grade_requirements.csv | from_rank_id | 6 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| promotion_grade_requirements.csv | to_rank_id | 6 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| promotion_grade_requirements.csv | item_id | 6 | items.csv | item_id | 53 | HARD | CLOSED |
| raid_difficulties.csv | raid_id | 6 | raids.csv | raid_id | 2 | HARD | CLOSED |
| raid_difficulties.csv | reward_group_id | 6 | reward_groups.csv | reward_group_id | 15 | HARD | CLOSED |
| raid_parts.csv | name_text_key | 7 | localizations.csv | text_key | 470 | HARD | CLOSED |
| raid_parts.csv | priority_hint_text_key | 7 | localizations.csv | text_key | 470 | HARD | CLOSED |
| raid_parts.csv | raid_id | 7 | raids.csv | raid_id | 2 | HARD | CLOSED |
| raid_parts.csv | break_condition_group_id | 7 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| raid_parts.csv | break_reward_group_id | 7 | reward_groups.csv | reward_group_id | 15 | HARD | CLOSED |
| raid_parts.csv | behavior_change | 7 | raid_part_effects.csv | effect_id | 6 | HARD | CLOSED |
| raids.csv | name_text_key | 2 | localizations.csv | text_key | 470 | HARD | CLOSED |
| raids.csv | boss_monster_id | 2 | monsters.csv | monster_id | 27 | HARD | CLOSED |
| raids.csv | min_rank_id | 2 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| raids.csv | unlock_condition_group_id | 2 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| raids.csv | first_clear_reward_group_id | 2 | reward_groups.csv | reward_group_id | 15 | HARD | CLOSED |
| random_equipment_tier_specs.csv | fixed_job_id | 0 | jobs.csv | job_id | 5 | HARD | CLOSED |
| recipe_materials.csv | recipe_id | 183 | recipes.csv | recipe_id | 87 | HARD | CLOSED |
| recipe_materials.csv | item_id | 183 | items.csv | item_id | 53 | HARD | CLOSED |
| recipe_outputs.csv | recipe_id | 87 | recipes.csv | recipe_id | 87 | HARD | CLOSED |
| recipes.csv | name_text_key | 87 | localizations.csv | text_key | 470 | HARD | CLOSED |
| recipes.csv | facility_id | 87 | facilities.csv | facility_id | 8 | HARD | CLOSED |
| recipes.csv | npc_proficiency_id | 87 | npc_proficiency_levels.csv | proficiency_id | 4 | HARD | CLOSED |
| recruitment_pity_rules.csv | pity_group_id | 2 | recruitment_pity_groups.csv | pity_group_id | 1 | HARD | CLOSED |
| recruitment_pity_rules.csv | guaranteed_grade_id | 2 | mercenary_grades.csv | grade_id | 5 | HARD | CLOSED |
| recruitment_pity_rules.csv | reset_on_grade_or_higher_id | 2 | mercenary_grades.csv | grade_id | 5 | HARD | CLOSED |
| recruitment_pool_entries.csv | pool_id | 17 | recruitment_pools.csv | pool_id | 6 | HARD | CLOSED |
| recruitment_pool_entries.csv | grade_id | 17 | mercenary_grades.csv | grade_id | 5 | HARD | CLOSED |
| recruitment_pools.csv | pity_group_id | 3 | recruitment_pity_groups.csv | pity_group_id | 1 | HARD | CLOSED |
| recruitment_pools.csv | rate_up_group_id | 0 | recruitment_rate_up_groups.csv | rate_up_group_id | 0 | HARD | CLOSED |
| recruitment_rate_up_entries.csv | rate_up_group_id | 0 | recruitment_rate_up_groups.csv | rate_up_group_id | 0 | HARD | CLOSED |
| recruitment_rate_up_entries.csv | job_id | 0 | jobs.csv | job_id | 5 | HARD | CLOSED |
| refine_options.csv | name_text_key | 11 | localizations.csv | text_key | 470 | HARD | CLOSED |
| refine_options.csv | material_item_id | 11 | items.csv | item_id | 53 | HARD | CLOSED |
| regions.csv | name_text_key | 5 | localizations.csv | text_key | 470 | HARD | CLOSED |
| regions.csv | min_rank_id | 5 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| regions.csv | unlock_condition_group_id | 5 | condition_groups.csv | condition_group_id | 21 | HARD | CLOSED |
| regions.csv | kingdom_stage_id | 5 | kingdom_stages.csv | stage_id | 5 | HARD | CLOSED |
| regions.csv | raid_gate_id | 1 | raids.csv | raid_id | 2 | HARD | CLOSED |
| reward_entries.csv | reward_group_id | 16 | reward_groups.csv | reward_group_id | 15 | HARD | CLOSED |
| runtime_config.csv | description_text_key | 14 | localizations.csv | text_key | 470 | HARD | CLOSED |
| skills.csv | name_text_key | 15 | localizations.csv | text_key | 470 | HARD | CLOSED |
| skills.csv | description_text_key | 15 | localizations.csv | text_key | 470 | HARD | CLOSED |
| skills.csv | job_id | 15 | jobs.csv | job_id | 5 | HARD | CLOSED |
| skills.csv | unlock_rank_id | 15 | mercenary_ranks.csv | rank_id | 6 | HARD | CLOSED |
| status_effects.csv | name_text_key | 6 | localizations.csv | text_key | 470 | HARD | CLOSED |
| trait_job_eligibility.csv | trait_id | 44 | traits.csv | trait_id | 10 | HARD | CLOSED |
| trait_job_eligibility.csv | job_id | 44 | jobs.csv | job_id | 5 | HARD | CLOSED |
| traits.csv | name_text_key | 10 | localizations.csv | text_key | 470 | HARD | CLOSED |
| traits.csv | description_text_key | 10 | localizations.csv | text_key | 470 | HARD | CLOSED |
| tutorial_grants.csv | tutorial_step_id | 9 | tutorial_steps.csv | tutorial_step_id | 10 | HARD | CLOSED |
| tutorial_steps.csv | prerequisite_step_id | 9 | tutorial_steps.csv | tutorial_step_id | 10 | HARD | CLOSED |

동적 discriminator FK closure:

| context | discriminator | enabled source refs | target | enabled targets | result |
|---|---|---|---|---|---|
| loot_entries.csv | ITEM | 63 | items.csv | 53 | CLOSED |
| loot_entries.csv | RANDOM_EQUIPMENT_TIER | 25 | random_equipment_tier_specs.csv | 5 | CLOSED |
| reward_entries.csv | ITEM | 13 | items.csv | 53 | CLOSED |
| reward_entries.csv | PROGRESSION_FLAG | 2 | progression_flags.csv | 3 | CLOSED |
| reward_entries.csv | REGION_UNLOCK | 1 | regions.csv | 5 | CLOSED |
| tutorial_grants.csv | CURRENCY | 2 | currencies.csv | 4 | CLOSED |
| tutorial_grants.csv | ITEM | 6 | items.csv | 53 | CLOSED |
| tutorial_grants.csv | PROGRESSION_FLAG | 1 | progression_flags.csv | 3 | CLOSED |
| recipe_outputs.csv | EQUIPMENT_TEMPLATE | 80 | equipment_templates.csv | 80 | CLOSED |
| recipe_outputs.csv | POTION | 7 | potions.csv | 7 | CLOSED |
| recruitment_pool_entries.csv | GENERATED_MERCENARY | 17 | mercenary_generation_profiles.csv | 1 | CLOSED |
| recruitment_pool_entries.csv | FIXED job selector | 2 | jobs.csv | 5 | CLOSED |
| mercenary_generation_profiles.csv | name_pool_id | 1 | mercenary_name_pool_entries.csv distinct pools | 1 | CLOSED |
| mercenary_generation_profiles.csv | appearance_pool_id | 1 | mercenary_appearance_pool_entries.csv distinct pools | 1 | CLOSED |
| random_equipment_tier_specs.csv | quality_profile_id | 5 | equipment_quality_weights.csv distinct profiles | 5 | CLOSED |
| items.csv source union | REGION | 30 | regions.csv | 5 | CLOSED |
| items.csv source union | RAID | 8 | raids.csv | 2 | CLOSED |
| conditions.csv subject union | FACILITY | 9 | facilities.csv | 8 | CLOSED |
| conditions.csv subject union | REGION | 6 | regions.csv | 5 | CLOSED |
| conditions.csv subject union | MONSTER | 2 | monsters.csv | 27 | CLOSED |
| conditions.csv subject union | MERCENARY_ROSTER | 3 | mercenary_ranks.csv | 6 | CLOSED |
| conditions.csv subject union | RAID | 3 | raids.csv | 2 | CLOSED |
| conditions.csv subject union | KINGDOM_STAGE | 1 | kingdom_stages.csv | 5 | CLOSED |
| conditions.csv subject union | RAID_PART | 7 | raid_parts.csv | 7 | CLOSED |
| conditions.csv subject union | TUTORIAL_STEP | 1 | tutorial_steps.csv | 10 | CLOSED |

### Reason

static FK와 discriminator FK를 모두 닫은 뒤 catalog를 publish해야 한다.

### Compatibility

| context | 허용 discriminator | registry/규칙 |
|---|---|---|
| loot_entries | ITEM,RANDOM_EQUIPMENT_TIER | items/spec; probability non-null, weight null |
| recipe_outputs | ITEM,POTION,EQUIPMENT_TEMPLATE | items/potions/templates; quantity>=1 |
| reward_entries | ITEM,REGION_UNLOCK,PROGRESSION_FLAG | 현재 rows; group mode가 probability/weight 결정 |
| tutorial_grants | ITEM,CURRENCY,PROGRESSION_FLAG | items/currencies/flags; profile grant idempotency |
| recruitment_pool_entries | GENERATED_MERCENARY | generation profile; grade 필수; ALL null/FIXED job FK |

| sentinel/namespace | 허용 위치 | 금지 |
|---|---|---|
| 빈 cell | nullable field | non-null field |
| NONE | job selector/failure mode enum context | FK ID |
| SYSTEM_* | PERSONAL_GOLD/EXP exact registry contract | 임의 생성 |
| RAID_* | raid StableId와 item source_id | null sentinel |
| TEMPLATE | asset authoring status; runtime row disabled | DEV player runtime enabled row |
| SPECIAL_TICKET | server currency key | request enum/local ID |
| SPECIAL_RECRUIT_TICKET | local currency ID | server key |

| 의미 | request/cost enum | local currency ID | authority | server key |
|---|---|---|---|---|
| 왕국 골드 | KINGDOM_GOLD | KINGDOM_GOLD | LOCAL | 없음 |
| 무료 premium | FREE_PREMIUM | PREMIUM_FREE | SERVER | FREE_GEM |
| 유료 premium | PAID_PREMIUM | PREMIUM_PAID | SERVER | PAID_GEM |
| 특별 모집권 | TICKET | SPECIAL_RECRUIT_TICKET | SERVER | SPECIAL_TICKET |

OpenAPI/content/server adapter는 이 표로 명시 변환하며 문자열 passthrough를 금지한다. 적용된 Flyway migration은 불변이고 server key/seed 변경은 더 높은 version의 새 migration으로만 수행한다. KINGDOM_GOLD는 서버 wallet에 보내지 않는다.

`content_aliases`와 두 rate-up registry는 0 rows이고 이를 참조하는 enabled row도 정확히 0이다. item source는 REGION이면 regions FK, RAID이면 raids FK, 나머지 세 source_type이면 source_id null이다. equipment source=CRAFT이면 boss_id null, source=BOSS이면 boss_id non-null raids FK다. random equipment는 source=CRAFT만 후보로 허용하므로 기존 golden 후보 순서가 변하지 않는다. `regions.kingdom_stage_id`는 catalog grouping/권장 진행 단계이고 추가 unlock predicate가 아니다. 실제 unlock의 단일 권위는 `unlock_condition_group_id`다.

| tier | job | eligible count | ordered candidate IDs |
|---|---|---|---|
| 1 | JOB_ARCHER | 4 | EQ_T1_ARCHER_WEAPON,EQ_T1_LIGHT_ARMOR,EQ_T1_LIGHT_HELMET,EQ_T1_POWER_ACCESSORY |
| 1 | JOB_CLERIC | 4 | EQ_T1_CLERIC_WEAPON,EQ_T1_CLOTH_ARMOR,EQ_T1_CLOTH_HELMET,EQ_T1_WISDOM_ACCESSORY |
| 1 | JOB_GUARDIAN | 4 | EQ_T1_GUARDIAN_WEAPON,EQ_T1_GUARD_ACCESSORY,EQ_T1_HEAVY_ARMOR,EQ_T1_HEAVY_HELMET |
| 1 | JOB_MAGE | 4 | EQ_T1_CLOTH_ARMOR,EQ_T1_CLOTH_HELMET,EQ_T1_MAGE_WEAPON,EQ_T1_WISDOM_ACCESSORY |
| 1 | JOB_WARRIOR | 4 | EQ_T1_HEAVY_ARMOR,EQ_T1_HEAVY_HELMET,EQ_T1_POWER_ACCESSORY,EQ_T1_WARRIOR_WEAPON |
| 2 | JOB_ARCHER | 4 | EQ_T2_ARCHER_WEAPON,EQ_T2_LIGHT_ARMOR,EQ_T2_LIGHT_HELMET,EQ_T2_POWER_ACCESSORY |
| 2 | JOB_CLERIC | 4 | EQ_T2_CLERIC_WEAPON,EQ_T2_CLOTH_ARMOR,EQ_T2_CLOTH_HELMET,EQ_T2_WISDOM_ACCESSORY |
| 2 | JOB_GUARDIAN | 4 | EQ_T2_GUARDIAN_WEAPON,EQ_T2_GUARD_ACCESSORY,EQ_T2_HEAVY_ARMOR,EQ_T2_HEAVY_HELMET |
| 2 | JOB_MAGE | 4 | EQ_T2_CLOTH_ARMOR,EQ_T2_CLOTH_HELMET,EQ_T2_MAGE_WEAPON,EQ_T2_WISDOM_ACCESSORY |
| 2 | JOB_WARRIOR | 4 | EQ_T2_HEAVY_ARMOR,EQ_T2_HEAVY_HELMET,EQ_T2_POWER_ACCESSORY,EQ_T2_WARRIOR_WEAPON |
| 3 | JOB_ARCHER | 4 | EQ_T3_ARCHER_WEAPON,EQ_T3_LIGHT_ARMOR,EQ_T3_LIGHT_HELMET,EQ_T3_POWER_ACCESSORY |
| 3 | JOB_CLERIC | 4 | EQ_T3_CLERIC_WEAPON,EQ_T3_CLOTH_ARMOR,EQ_T3_CLOTH_HELMET,EQ_T3_WISDOM_ACCESSORY |
| 3 | JOB_GUARDIAN | 4 | EQ_T3_GUARDIAN_WEAPON,EQ_T3_GUARD_ACCESSORY,EQ_T3_HEAVY_ARMOR,EQ_T3_HEAVY_HELMET |
| 3 | JOB_MAGE | 4 | EQ_T3_CLOTH_ARMOR,EQ_T3_CLOTH_HELMET,EQ_T3_MAGE_WEAPON,EQ_T3_WISDOM_ACCESSORY |
| 3 | JOB_WARRIOR | 4 | EQ_T3_HEAVY_ARMOR,EQ_T3_HEAVY_HELMET,EQ_T3_POWER_ACCESSORY,EQ_T3_WARRIOR_WEAPON |
| 4 | JOB_ARCHER | 4 | EQ_T4_ARCHER_WEAPON,EQ_T4_LIGHT_ARMOR,EQ_T4_LIGHT_HELMET,EQ_T4_POWER_ACCESSORY |
| 4 | JOB_CLERIC | 4 | EQ_T4_CLERIC_WEAPON,EQ_T4_CLOTH_ARMOR,EQ_T4_CLOTH_HELMET,EQ_T4_WISDOM_ACCESSORY |
| 4 | JOB_GUARDIAN | 4 | EQ_T4_GUARDIAN_WEAPON,EQ_T4_GUARD_ACCESSORY,EQ_T4_HEAVY_ARMOR,EQ_T4_HEAVY_HELMET |
| 4 | JOB_MAGE | 4 | EQ_T4_CLOTH_ARMOR,EQ_T4_CLOTH_HELMET,EQ_T4_MAGE_WEAPON,EQ_T4_WISDOM_ACCESSORY |
| 4 | JOB_WARRIOR | 4 | EQ_T4_HEAVY_ARMOR,EQ_T4_HEAVY_HELMET,EQ_T4_POWER_ACCESSORY,EQ_T4_WARRIOR_WEAPON |
| 5 | JOB_ARCHER | 4 | EQ_T5_ARCHER_WEAPON,EQ_T5_LIGHT_ARMOR,EQ_T5_LIGHT_HELMET,EQ_T5_POWER_ACCESSORY |
| 5 | JOB_CLERIC | 4 | EQ_T5_CLERIC_WEAPON,EQ_T5_CLOTH_ARMOR,EQ_T5_CLOTH_HELMET,EQ_T5_WISDOM_ACCESSORY |
| 5 | JOB_GUARDIAN | 4 | EQ_T5_GUARDIAN_WEAPON,EQ_T5_GUARD_ACCESSORY,EQ_T5_HEAVY_ARMOR,EQ_T5_HEAVY_HELMET |
| 5 | JOB_MAGE | 4 | EQ_T5_CLOTH_ARMOR,EQ_T5_CLOTH_HELMET,EQ_T5_MAGE_WEAPON,EQ_T5_WISDOM_ACCESSORY |
| 5 | JOB_WARRIOR | 4 | EQ_T5_HEAVY_ARMOR,EQ_T5_HEAVY_HELMET,EQ_T5_POWER_ACCESSORY,EQ_T5_WARRIOR_WEAPON |

raid part guaranteed reward와 raid loot conditional roll은 의도적으로 별개다. false→true part operation은 group의 1개 보상을 확정 지급하고, terminal boss loot operation은 legacy probability/min/max를 추가 roll한다. 서로 다른 operation domain과 idempotency key를 사용하므로 같은 item ID가 나와도 중복 버그가 아니다. tutorial grant는 별도 profile/grant key이고 loot/reward operation과 공유하지 않는다.

### Exact output

closure가 하나라도 BROKEN이면 `CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET`으로 전체 package를 거부한다.

## 12. Deterministic writer와 package contract

### Decision

1) source decoder는 UTF-8 BOM을 첫 파일 첫 scalar에서만 제거한다. 2) legacy parser는 RFC 4180이며 JSON/pipe cell은 migration 단계에서만 허용한다. 3) source physical row 순서를 보존하고, split JSON object key는 UTF-8 ordinal, pipe-list는 source token 순서, 신규 관계 row는 문서 block 순서를 사용한다. 4) output은 UTF-8 BOM 없음, RFC 4180 minimal quote, CRLF, final CRLF 정확히 하나다. 5) null은 빈 cell, bool은 TRUE/FALSE, decimal은 source의 canonical plain lexical, UTC/date/Seed64는 domain 표를 따른다. 6) rowCount는 header 제외 physical records다. 7) sha256은 exact CSV bytes다. 8) tables는 file UTF-8 ordinal이다. 9) manifest는 RFC 8785 JCS UTF-8, BOM/final LF 없음이다.

### Reason

OS·locale·Python·Unity 버전에 따른 byte drift를 제거한다.

### Compatibility

`generatedAtUtc`만 SOURCE_DATE_EPOCH=1784332800에서 얻는다. wall clock을 쓰지 않는다. schema registry→CSV writer→descriptor 순서이며 descriptor를 손으로 편집하지 않는다. 같은 contentVersion의 다른 bytes는 `CONTENT_RELEASE_SPLIT_BRAIN`이다.

### Exact output

전체 package fingerprint는 `SHA-256(ASCII("KTCPKG1") || U64BE(manifestLength) || manifestJcsBytes || 각 file에 대해 U64BE(fileNameUtf8Length) || fileNameUtf8 || U64BE(fileLength) || fileBytes)`다. file 반복 순서는 UTF-8 ordinal이다. 이 값은 구현 build가 기록하는 `BUILD_DERIVED`이며 설계 문서에는 별도 상수를 두지 않는다.

## 13. Generation·reward golden fixtures

### 13.1 SplitMix64/selection contract

unsigned 64-bit modulo 연산과 rejection sampling을 사용한다. weighted selection은 accepted raw mod sumWeight의 cumulative upper-bound 첫 row다. 용병 소비 순서는 grade, pity(no RNG), job, name, appearance, growth, personality, grade trait count다. FIXED도 `nextBounded(1)`을 소비한다.

### 13.2 Mercenary verified golden

| stage | ordered candidates/bound | raw uint64 | bounded | result |
|---|---|---|---|---|
| grade | C75,B23,A2 / 100 | 2466975172287755897 | 97 | GRADE_B |
| job | JOB_ARCHER,JOB_CLERIC,JOB_GUARDIAN,JOB_MAGE,JOB_WARRIOR / 5 | 8832083440362974766 | 1 | JOB_CLERIC |
| name | 20 entries×100 / 2000 | 3534771765162737125 | 1125 | 벨라 |
| appearance | cleric 1×100 / 100 | 9592110948284743397 | 97 | ASSET_MERC_PLACEHOLDER_CLERIC_V1 |
| growth | full uint64 | 1881757512419323243 | — | 1881757512419323243 |
| personality | PERSONALITY_BRAVE,PERSONALITY_CAUTIOUS,PERSONALITY_COLLECTOR,PERSONALITY_FRUGAL,PERSONALITY_GEARHEAD,PERSONALITY_PRACTICAL / 6 | 12920672458450473694 | 4 | PERSONALITY_GEARHEAD |
| trait | TRAIT_ARCANE,TRAIT_BOSS_HUNTER,TRAIT_FROSTBORN,TRAIT_LUCKY,TRAIT_POISON_RESIST,TRAIT_SCAVENGER,TRAIT_STRONG,TRAIT_STURDY,TRAIT_SURVIVOR / 9 | 15403818807231698370 | 3 | TRAIT_LUCKY |

input은 contentVersion=1.0.0-content.1, pool=TAVERN_L1, operationSeed=123456789, pity=0이다. growthSeed=1881757512419323243다.

| stat | domain input | domainSeed | raw | bounded | factorBps |
|---|---|---|---|---|---|
| STR | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|STR | 7878342255428084581 | 9175445527846922935 | 611 | 10111 |
| VIT | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|VIT | 13609651172696602368 | 8078483884478546374 | 836 | 10336 |
| DEX | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|DEX | 3688853455365606363 | 268289597894771620 | 167 | 9667 |
| LUK | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|LUK | 10071029371569647019 | 5021897472405649083 | 248 | 9748 |
| INT | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|INT | 1355071110395857733 | 7332160931031653338 | 622 | 10122 |
| WIS | KT\|GROWTH_FACTOR_V1\|1.0.0-content.1\|1881757512419323243\|WIS | 8173817200245823402 | 12322579078042097024 | 160 | 9660 |

### 13.3 Existing random equipment golden

| stage | raw | bounded | result |
|---|---|---|---|
| template | 12744715263588028796 | 0 | EQ_T1_CLERIC_WEAPON |
| quality | 16192141852193020578 | 78 | QUALITY_COMMON |
| affix gate | 16161435109270938784 | 8784 | fail; refineOption=null |

input은 spec=RANDOM_EQ_T1_ANY, killerJob=JOB_CLERIC, operationSeed=987654321이다. ordered template candidates는 `EQ_T1_CLERIC_WEAPON,EQ_T1_CLOTH_ARMOR,EQ_T1_CLOTH_HELMET,EQ_T1_WISDOM_ACCESSORY`, quality candidates는 `COMMON80,FINE18,RARE2`다. consumed raw는 표의 3개가 전부다.

### 13.4 Pity boundary fixtures

| fixture | before S+/SS | base grade | trigger after increment | final | after S+/SS |
|---|---|---|---|---|---|
| PITY_9_TO_10 | 9/20 | GRADE_A | S+ | GRADE_S | 0/21 |
| PITY_79_TO_80 | 3/79 | GRADE_A | SS | GRADE_SS | 0/0 |
| PITY_BOTH_AT_80 | 9/79 | GRADE_A | S+ and SS; SS wins | GRADE_SS | 0/0 |

rule order는 guaranteed grade order 내림차순 후 pity_rule_id UTF-8 ordinal이다. 이 세 fixture의 입력 경계는 base grade 결정 직후이며 `consumedRaw=[]`; pity 적용 자체는 RNG를 소비하지 않는다.

### 13.5 Tutorial fixed-job consumption

| stage | bound | raw | bounded | result |
|---|---|---|---|---|
| tutorial grade | 98 | 2466975172287755897 | 19 | GRADE_C |
| FIXED job | 1 | 8832083440362974766 | 0 | JOB_WARRIOR |

### 13.6 Multi-draw seed fixture

| drawIndex | canonical UTF-8 input | operationSeed big-endian |
|---|---|---|
| 0 | KT\|SUMMON_SEED_V1\|1.0.0-content.1\|00000000-0000-7000-8000-000000000001\|00000000-0000-7000-8000-000000000002\|SPECIAL_STANDARD_TICKET\|0 | 4778811787865032672 |
| 9 | KT\|SUMMON_SEED_V1\|1.0.0-content.1\|00000000-0000-7000-8000-000000000001\|00000000-0000-7000-8000-000000000002\|SPECIAL_STANDARD_TICKET\|9 | 5649623414433526989 |

### 13.7 Affix-success and inclusive refine fixture

| stage | ordered candidates/bound | raw | bounded | result |
|---|---|---|---|---|
| template | EQ_T5_CLERIC_WEAPON,EQ_T5_CLOTH_ARMOR,EQ_T5_CLOTH_HELMET,EQ_T5_WISDOM_ACCESSORY / 4 | 10451216379200822465 | 1 | EQ_T5_CLOTH_ARMOR |
| quality | COMMON10,FINE30,RARE38,LEGACY18,RELIC4 / 100 | 13757245211066428519 | 19 | QUALITY_FINE |
| gate | 10000; success < 1000 | 17911839290282890590 | 590 | SUCCESS |
| option | REF_ATK_POWER,REF_BOSS,REF_CRIT,REF_DEF,REF_FIRE,REF_FROST,REF_HP,REF_MATERIAL,REF_PART,REF_POISON,REF_RARE_FIND / 11 | 8196980753821780235 | 7 | REF_MATERIAL |
| value | inclusive 200..800 | 8195237237126968761 | 226 | 426 bps |

operationSeed=1. inclusive bound는 max-min+1이다.

### 13.8 Crash retry snapshot verbatim

trace canonical bytes:

```json
{"candidateIds":["EQ_T1_CLERIC_WEAPON","EQ_T1_CLOTH_ARMOR","EQ_T1_CLOTH_HELMET","EQ_T1_WISDOM_ACCESSORY"],"equipmentTemplateId":"EQ_T1_CLERIC_WEAPON","killerJobId":"JOB_CLERIC","operationSeed":987654321,"qualityId":"QUALITY_COMMON","rawDraws":[12744715263588028796,16192141852193020578,16161435109270938784],"refineOption":null,"specId":"RANDOM_EQ_T1_ANY"}
```

trace SHA-256=`11a39fbb855f367a9cefe06e09d873bf848c56c6fe45411a7b841b7c669853f0`. PREPARED snapshot:

```json
{
  "instanceId": "00000000-0000-7000-8000-000000000101",
  "equipmentTemplateId": "EQ_T1_CLERIC_WEAPON",
  "tier": 1,
  "qualityId": "QUALITY_COMMON",
  "enhancementLevel": 0,
  "refineOption": null,
  "sourceContentVersion": "1.0.0-content.1",
  "generationOperationId": "00000000-0000-7000-8000-000000000102",
  "randomTraceHash": "11a39fbb855f367a9cefe06e09d873bf848c56c6fe45411a7b841b7c669853f0"
}
```

snapshot JCS SHA-256=`f6ac557c8bc649b8f55db8e19f699bc30471487d6182e74dd513b4a48a5d6c6a`. crash retry는 이 9 fields를 byte-semantic verbatim 적용한다. current catalog reroll은 `SAVE_EQUIPMENT_REROLL_FORBIDDEN`, 같은 operation/instance 다른 결과는 `SAVE_EQUIPMENT_GENERATION_SPLIT_BRAIN`이다.

### 13.9 Offline 6-type line identity

| lineNo | type | subjectType | subjectId | canonical UTF-8 input | SHA-256 |
|---|---|---|---|---|---|
| 1 | HUNT | MERCENARY | 00000000-0000-7000-8000-000000000301 | 00000000-0000-7000-8000-000000000201\|1\|HUNT\|MERCENARY\|00000000-0000-7000-8000-000000000301 | a356710098eaba2e8a1e231fdbf220ae3811cd7dd01590028c1ecd97deda910d |
| 2 | FACILITY | FACILITY | FAC_BLACKSMITH | 00000000-0000-7000-8000-000000000201\|2\|FACILITY\|FACILITY\|FAC_BLACKSMITH | 21a07d0e01f6539e2115bc9dbb206b7710e4e22ba63ae4f0f0faf368281ffca0 |
| 3 | NPC_PROFICIENCY | MANAGEMENT_NPC | 00000000-0000-7000-8000-000000000302 | 00000000-0000-7000-8000-000000000201\|3\|NPC_PROFICIENCY\|MANAGEMENT_NPC\|00000000-0000-7000-8000-000000000302 | 63a419de2ae9e82b74c8c38fc9c24acbc2f5cbb769dd18846c54829fdfac45a3 |
| 4 | POTION_CONSUMPTION | MERCENARY | 00000000-0000-7000-8000-000000000301 | 00000000-0000-7000-8000-000000000201\|4\|POTION_CONSUMPTION\|MERCENARY\|00000000-0000-7000-8000-000000000301 | a018dbae5e394efbe9f765378552fd7b038315607bfd6eeaff96c89af47be6f0 |
| 5 | INJURY_RECOVERY | MERCENARY | 00000000-0000-7000-8000-000000000301 | 00000000-0000-7000-8000-000000000201\|5\|INJURY_RECOVERY\|MERCENARY\|00000000-0000-7000-8000-000000000301 | 9f0e1d466fda78769a07270ecfcdf6d111bd1b4aa57f72a95f061e8023ab6321 |
| 6 | PROMOTION_REVIEW | PROMOTION | 00000000-0000-7000-8000-000000000303 | 00000000-0000-7000-8000-000000000201\|6\|PROMOTION_REVIEW\|PROMOTION\|00000000-0000-7000-8000-000000000303 | f08acc2cd7c5d6ee153fd6c1acfdc7564e6da6de3b22a650a571c2b738773ae0 |

### 13.10 Tutorial multi-line grant idempotency

key input=`00000000-0000-7000-8000-000000000401|TUTORIAL|GRANT_TUTORIAL_BASIC_WEAPON_MATERIALS`; key SHA-256=`e990504b90ddab922d3cb0f6167b31be329327f4688109ccf27aa69bc1266017`. result canonical bytes:

```json
{"grantId":"GRANT_TUTORIAL_BASIC_WEAPON_MATERIALS","lines":[{"lineNo":1,"quantity":4,"rewardId":"MAT_R01_SOFTWOOD","rewardType":"ITEM"},{"lineNo":2,"quantity":2,"rewardId":"MAT_R01_WOLF_FANG","rewardType":"ITEM"}],"profileId":"00000000-0000-7000-8000-000000000401"}
```

result SHA-256=`79126cdfc1f1040b742748a07682afa1b5144c6ee894d1ef1be1c2f222bc48de`. line 1만 적용 후 crash한 상태는 성공으로 인정하지 않으며 같은 key로 두 line을 원자 재적용하거나 기존 delta를 rollback한다.

## 14. Placeholder asset·Addressables contract

### Decision

| job | prefab path | Addressables address | body RGBA | 16×16 role mark |
|---|---|---|---|---|
| WARRIOR | Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders/Warrior.prefab | ASSET_MERC_PLACEHOLDER_WARRIOR_V1 | #C94F4FFF | SWORD |
| GUARDIAN | Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders/Guardian.prefab | ASSET_MERC_PLACEHOLDER_GUARDIAN_V1 | #4F78C9FF | SHIELD |
| ARCHER | Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders/Archer.prefab | ASSET_MERC_PLACEHOLDER_ARCHER_V1 | #4FAE68FF | BOW |
| MAGE | Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders/Mage.prefab | ASSET_MERC_PLACEHOLDER_MAGE_V1 | #8B5CC7FF | STAR |
| CLERIC | Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders/Cleric.prefab | ASSET_MERC_PLACEHOLDER_CLERIC_V1 | #E0C85AFF | CROSS |

Addressables group은 `Content-Mercenary-Placeholders-v1`이다. prefab root 이름은 address, root component는 Transform과 `MercenaryPlaceholderMarker(jobId,assetId)`다. child `Body`는 Transform+SpriteRenderer, child `RoleMark`는 Transform+SpriteRenderer다. generator가 64×64 RGBA Texture2D `.asset`과 centered Sprite subasset을 만들고 body pivot=(0.5,0.5), pixelsPerUnit=64, sortingLayer=`Characters`, order=0으로 설정한다. mark는 16×16, pivot=(0.5,0.5), PPU=64, sortingLayer=`Characters`, order=1이다.

### Reason

외부 texture/font/material 없이 player build에서 production-valid address를 제공한다.

### Compatibility

animation 없음은 Animator, Animation, AnimationClip, AnimatorController, Timeline component/asset가 모두 없다는 뜻이다. scene/prefab YAML을 텍스트로 쓰지 않고 Unity Editor API `PrefabUtility.SaveAsPrefabAsset`, `AssetDatabase`, Addressables settings API만 사용한다.

### Exact output

editor generator는 expected path/address 집합을 계산하고 동일 fingerprint면 no-op, managed root 아래 marker가 있는 stale asset만 삭제한다. 다른 user asset은 삭제하지 않는다. registry/address/prefab 1:1, duplicate address, missing component를 build 전 검증한다. missing은 `CONTENT_PLACEHOLDER_ASSET_MISSING`, duplicate는 `CONTENT_ADDRESSABLE_DUPLICATE_ADDRESS`, registry mismatch는 `CONTENT_ASSET_REGISTRY_MISMATCH`다.

## 15. Validator 오류 목록

### Decision

모든 오류는 severity ERROR이며 import/publish/player build를 차단한다.

### Reason

fail-closed 오류 계약이 구현자별 복구 추측을 막는다.

### Compatibility

기존 P03 code가 같은 의미의 code를 이미 가지면 exact code를 유지하고, 아래 신규 code만 추가한다.

### Exact output

| code | failure condition |
|---|---|
| CONTENT_MANIFEST_UNKNOWN_FIELD | manifest unknown property |
| CONTENT_MANIFEST_REQUIRED_FIELD_MISSING | required property 없음 |
| CONTENT_MANIFEST_CONTRACT_VERSION_UNSUPPORTED | contract !=2 |
| CONTENT_MANIFEST_BASE_VERSION_INVALID | packageKind/baseContentVersion matrix |
| CONTENT_MANIFEST_TABLE_ORDER_INVALID | tables 비정렬/중복 |
| CONTENT_MANIFEST_HASH_FORMAT_INVALID | HEX64 lexical 실패 |
| CONTENT_MANIFEST_HASH_MISMATCH | CSV bytes hash 불일치 |
| CONTENT_MANIFEST_ROW_COUNT_MISMATCH | rowCount 불일치 |
| CONTENT_MANIFEST_HEADER_MISMATCH | field/header drift |
| CONTENT_RELEASE_SPLIT_BRAIN | same version different bytes |
| SAVE_CONTENT_FUTURE_UNSUPPORTED | Save contentVersion이 설치 최고보다 미래 |
| SAVE_CONTENT_HARD_FK_UNRESOLVED | Save hard FK alias 해석 실패 |
| CSV_DOMAIN_INVALID | generic lexical/range 실패 |
| CSV_STATUS_ENABLED_INVALID | status/enabled 조합 |
| CSV_PRIMARY_KEY_DUPLICATE | PK duplicate |
| CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET | enabled hard FK target 없음 |
| CSV_PIPE_LIST_FORBIDDEN | canonical pipe-list |
| CSV_JSON_CELL_FORBIDDEN | canonical embedded JSON |
| CSV_LOCALIZATION_KEY_COLLISION | generated key collision |
| CSV_CONTENT_ALIAS_INVALID | alias union/chain/cycle/target |
| CSV_TRAIT_CATEGORY_INVALID | 6-value enum 밖 |
| CSV_EQUIPMENT_SOURCE_INVALID | source/boss_id matrix |
| CSV_ITEM_SOURCE_INVALID | source_type/source_id matrix |
| CSV_AUTONOMY_RULE_INVALID | state priority/default rule |
| CSV_RAID_DIFFICULTY_INVALID | 2 raids×3 difficulty/FK/power |
| CSV_REWARD_DISCRIMINATOR_MISMATCH | reward registry/null matrix |
| CSV_RECRUITMENT_RESULT_MISMATCH | generation/grade/job matrix |
| CSV_PITY_CONFIGURATION_INVALID | counter key/rule/reset |
| CSV_RANDOM_EQUIPMENT_NO_ELIGIBLE_TEMPLATE | tier×job candidate 0 |
| CSV_RANDOM_EQUIPMENT_SPEC_INVALID | quality sum/slot/job |
| CSV_TUTORIAL_DAG_INVALID | order/prerequisite/cycle |
| CSV_OFFLINE_RULE_SET_INVALID | 6 type/cap |
| CSV_CONDITION_UNION_INVALID | condition subject/value matrix |
| CSV_CONDITION_GROUP_INVALID | member union/cycle/depth |
| SAVE_EQUIPMENT_SNAPSHOT_REQUIRED | 장비 reward snapshot null |
| SAVE_EQUIPMENT_SNAPSHOT_MISMATCH | spec/template/quality mismatch |
| SAVE_EQUIPMENT_REROLL_FORBIDDEN | retry current RNG reroll |
| SAVE_EQUIPMENT_GENERATION_SPLIT_BRAIN | same IDs different snapshot |
| CONTENT_PLACEHOLDER_ASSET_MISSING | prefab/address 없음 |
| CONTENT_ADDRESSABLE_DUPLICATE_ADDRESS | duplicate address |
| CONTENT_ASSET_REGISTRY_MISMATCH | registry/prefab/address mismatch |

## 16. Implementation acceptance checklist

아래 `[x]`는 설계가 해당 수용 조건과 expected result를 정의했다는 뜻이며, 저장소에서 구현·테스트·build를 이미 실행했다는 뜻이 아니다.

- [x] contract v2 strict schema와 60 descriptor 생성
- [x] 60 schema registry/header/rowCount/hash 일치
- [x] 29 CSV+README 30 input migration report 완전성
- [x] localizations 470 rows와 모든 text FK closure
- [x] currency 4 rows/namespace adapter
- [x] trait 6 category 1:1
- [x] BOSS_CRAFT→BOSS와 boss_id raids FK
- [x] autonomy 37 rows
- [x] raid difficulty 6 rows/repeat group 6개
- [x] facility effect 32 dispatch parameters
- [x] runtime config 14 rows/range
- [x] static+dynamic enabled FK closure
- [x] two legacy RNG golden과 신규 boundary/idempotency golden
- [x] placeholder 5 prefab/address build validation
- [x] Unity EditMode/PlayMode/player build
- [x] server regression; applied Flyway immutable

## 17. Superseded 문구 목록

| 폐기 문구 | 최종 문구 |
|---|---|
| manifest contractVersion=1에 새 property 추가 | contractVersion=2/schemaId v2 |
| csv_schema_set_version=1 | csvSchemaSetVersion=2 |
| trait STAT/COMBAT/ECONOMY/SURVIVAL/UTILITY | STAT/COMBAT/COLLECTION/BOSS/RESIST/AI |
| BOSS_CRAFT canonical 유지 | BOSS |
| equipment boss_id→monsters | BOSS source의 boss_id→raids |
| items source_region_id 단일 FK | source_type/source_id tagged union |
| regions unlock_type/id1/id2 | unlock_condition_group_id; R02 tutorial cycle 해소 |
| facility effect_summary 폐기 | effect_key+effect_text_key |
| priority_hint raw string | priority_hint_text_key |
| runtime_config description raw | description_text_key |
| manifest CSV 이중 원본 | content_manifest.json 단일 원본 |
| package hash 설계 상수 | BUILD_DERIVED |

## 18. 남은 UNRESOLVED

### Decision

`NONE`.

### Reason

60개 schema, 전체 row, 모든 source column, localization, FK, manifest, golden, asset와 Phase 경계가 닫혔다.

### Compatibility

이 문서는 구현을 시작할 입력이지만 P03 완료 판정이나 P04 진입을 자동 승인하지 않는다. 구현·테스트·player build 결과에 대한 별도 gate가 필요하다.

### Exact output

추가 상세 설계 요청 없이 P03 구현을 재개할 수 있다.
