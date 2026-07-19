# P04 왕국·시설 v1.0.2 최소 정정 요청

## 1. 요청 목적

`TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md`와
`TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.1_CORRECTION_APPENDIX.md`를
합쳐 실제 P04 62-table package를 구성해 검증한 결과, v1.0.1의 full manifest가
P03에서 이미 확정된 descriptor 정정 2개를 되돌려 Unity importer가
`CSV_DOMAIN_INVALID` 4건으로 fail closed한다.

P04 구현을 시작하려면 아래 두 descriptor만 바로잡은
`TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.2_CORRECTION_APPENDIX.md`가
필요하다. 다른 CSV bytes, Save golden, schema, 시설 상태·경제·UI 계약은 변경하지
않는다.

## 2. 검증 기준선

| 항목 | 값 |
|---|---|
| issue | #16 |
| branch | `codex/issue-16-p04-kingdom-facilities` |
| v1.0.1 기준 commit | `5100086` |
| Unity importer | `CsvContentImporter.ValidateCell`의 manifest domain 검증 |
| P03 확정 근거 | `TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md` 7~9행 및 canonical `.1` manifest |
| 검증 package | P03 60 CSV + P04 수정 3 CSV + 신규 2 CSV, 총 62 tables |

v1.0.1에 포함된 manifest 자체의 RFC 8785 길이 67264와 SHA-256
`73e493ca508b5af96435a2cc1ad72d6c33ffdeae2f594863d9d3ee0dc302fd07`은
재현된다. 문제는 그 manifest가 가리키는 실제 CSV value와 descriptor domain이
일치하지 않는다는 점이다.

## 3. 재현된 차단 오류 전체

| File | CSV row | Value | v1.0.1 descriptor | 결과 |
|---|---:|---|---|---|
| `monsters.csv` | 27 | `type=RAID` | `enumValues=[NORMAL,ELITE,BOSS]` | `CSV_DOMAIN_INVALID` |
| `monsters.csv` | 28 | `type=RAID` | `enumValues=[NORMAL,ELITE,BOSS]` | `CSV_DOMAIN_INVALID` |
| `runtime_config.csv` | 5 | `value=TRUE` | `domain=DECIMAL` | `CSV_DOMAIN_INVALID` |
| `runtime_config.csv` | 16 | `value=FALSE` | `domain=DECIMAL` | `CSV_DOMAIN_INVALID` |

전체 62-table의 파일 집합, header, rowCount, CSV SHA-256, PK, HARD FK는
일치했다. 모든 field domain을 실제 cell에 적용했을 때 발생한 오류는 위 4건뿐이다.

## 4. 필수 정정 1 — `monsters.csv.type`

v1.0.1 manifest의 `monsters.csv` descriptor에서 `type` field를 아래 exact
object로 교체한다. property 순서도 아래와 같이 유지한다.

```json
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
}
```

P03에서 `BOSS_HYDRA`, `BOSS_DRAGON`은 이미 `type=RAID`로 확정됐으며
`monsters.csv` bytes와 SHA-256은 바꾸지 않는다.

## 5. 필수 정정 2 — `runtime_config.csv.value`

v1.0.1 manifest의 `runtime_config.csv` descriptor에서 `value` field를 아래
exact object로 교체한다.

```json
{
  "name": "value",
  "domain": "STRING",
  "nullable": false,
  "enumValues": []
}
```

`runtime_config.value`는 `value_type` tagged union의 lexical payload다. 공통
domain은 `STRING`이고 semantic validator가 `INTEGER|DECIMAL|BOOLEAN|STRING`별
lexical과 min/max 규칙을 검사한다. `BOOLEAN` 값 `TRUE|FALSE`를 DECIMAL로 먼저
검사하면 CR-01 정정 자체가 import될 수 없다.

`runtime_config.csv`의 v1.0.1 bytes, 18 rows, SHA-256
`610576da67809118f8dd87f4526be69682c5657de0852baa3fd8b3efc28efd25`는
바꾸지 않는다.

## 6. 정정 후 exact golden

위 두 field만 수정하고 v1.0.1 manifest의 다른 property·array 순서·descriptor를
그대로 유지하면 다음 값이 재현된다.

| Artifact | Exact value |
|---|---|
| `content_manifest.json` RFC 8785 byte length | `67270` |
| `content_manifest.json` RFC 8785 SHA-256 | `668a4d4084d903e0a1dfb6394ed62287f3dc3a3f3c1fcc1d2d81d4f2af537c63` |
| `content_manifest.schema.json` RFC 8785 byte length | `2724` 유지 |
| `content_manifest.schema.json` RFC 8785 SHA-256 | `451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41` 유지 |
| CSV 변경 | 없음 |
| Save golden 변경 | 없음 |
| contentVersion | `1.0.0-content.2` 유지 |
| csvSchemaSetVersion | `3` 유지 |

v1.0.2 부록에서는 v1.0.1의 2.1 full manifest를 위 두 field가 반영된 full JSON으로
교체하고, 2.2/7.1/8/9절 등 manifest 길이·SHA-256을 인용하는 모든 위치를 새
golden으로 함께 갱신해야 한다.

## 7. 비변경 요구

- `raid_part_effects.csv.value=DECIMAL?`를 포함한 나머지 v1.0.1 full manifest는
  이번 최소 정정에서 변경하지 않는다.
- 62개 CSV의 row 순서, bytes, SHA-256을 변경하지 않는다.
- schema set 3 JSON과 SHA-256을 변경하지 않는다.
- Save v1 shape, saveVersion, P04 Save golden 3종을 변경하지 않는다.
- 시설 상태 전이, duration, 비용, NPC, uGUI hierarchy, Safe Area 계약을 변경하지
  않는다.
- 새 기능이나 구현 선택지를 추가하지 않는다.

## 8. v1.0.2 수용 체크리스트

- [ ] `monsters.csv.type.enumValues`가 exact `NORMAL,ELITE,BOSS,RAID`다.
- [ ] `runtime_config.csv.value.domain`이 exact `STRING`이다.
- [ ] full manifest RFC 8785 길이가 `67270`이다.
- [ ] full manifest RFC 8785 SHA-256이
      `668a4d4084d903e0a1dfb6394ed62287f3dc3a3f3c1fcc1d2d81d4f2af537c63`이다.
- [ ] 62-table 모든 cell의 manifest domain 검증이 0 errors다.
- [ ] header, rowCount, CSV hash, PK, HARD FK 검증이 0 errors다.
- [ ] v1.0.1의 다른 계약과 golden은 byte-semantic 그대로다.
- [ ] `UNRESOLVED`는 `NONE`이다.

## 9. 구현 재개 조건

위 조건을 만족하는 v1.0.2 정정 부록을 저장소에서 다시 기계 검증한 뒤 P04
canonical package, 시설 Domain/Application/Save, Kingdom uGUI, Unity 테스트와
Android APK 구현을 한 흐름으로 재개한다.
