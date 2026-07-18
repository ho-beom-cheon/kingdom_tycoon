# P04 왕국·시설 v1.0.1 단일 정정 부록 요청서

> 이 문서는 `TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md`의
> 구현 전 기계 검수에서 확인된 충돌과 누락을 한 번에 정정하기 위한 요청서다.
> ChatGPT에는 v1.0 본문과 이 요청서를 함께 전달한다.

## 0. 요청 결과물

다음 파일 하나만 반환한다.

```text
TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.1_CORRECTION_APPENDIX.md
```

정정 부록은 v1.0에서 변경되는 항목만 다루되, 아래 7개 정정의 exact 최종값을 모두
포함해야 한다. 구현 필수 `UNRESOLVED`는 `NONE`이어야 한다.

## 1. 기계 검수에서 통과한 항목

아래 항목은 재작성하지 않아도 되며, 정정으로 값이 바뀌지 않는 한 v1.0을 유지한다.

- `P04_NEW_GAME_GOLDEN`
  - payload JCS bytes: `3679`
  - payload SHA-256:
    `81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719`
  - envelope SHA-256:
    `6434fbccf5dd968a5c76782cb9fb5c938a737c7577caaa62358c97b565b0a22c`
- `P03_TO_P04_GOLDEN_BEFORE`
  - payload JCS bytes: `2952`
  - payload SHA-256:
    `ea91321e2fa15caf0eef1ef7e30499a7f2ea116f44334a3e89e2b8195628953d`
  - envelope SHA-256:
    `c1e7262a1d7612812cafc3f2f4cf90991b4f8dbd7f6fd3ac6a6a175e1427c053`
- `P03_TO_P04_GOLDEN_AFTER`
  - payload JCS bytes: `3679`
  - payload SHA-256:
    `81ff9df5566613ea07b883f5c08375b62c259dd48c39d355d416a21ae45e0719`
  - envelope SHA-256:
    `ebe58e5afd487c23980f1bf89e368ccf4010d1cd4f1b80c5394e1b8af638870c`
- `facility_construction_rules.csv`
  - 32 rows
  - SHA-256:
    `a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5`
- `facility_world_assets.csv`
  - 13 rows
  - SHA-256:
    `d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f`
- `asset_register.csv`
  - 18 rows
  - SHA-256:
    `f44f551c6929a08cbb781e71cbadc24f37dbcc721725843b3729498e4c6fd149`
- `localizations.csv`
  - 기존 470 rows + P04 138 rows = 608 rows
  - SHA-256:
    `56347e8e9c1fc61c45b18f5e024a31df966574aefdfbb5cb27703bfbd3c946bf`
- v1.0의 상태 전이, 신규 게임, 비용, NPC, UI 좌표, Save golden은 현재 검수 범위에서
  자체 일관성을 통과했다.

## 2. CR-01 — runtime BOOLEAN lexical 충돌

### 발견된 충돌

v1.0은 다음 exact row를 제공한다.

```csv
P04_TEST_CLOCK_MODE,BOOLEAN,0,BOOL,0,1,TXT_P04_DEV_CLOCK_DESC,CONFIRMED,TRUE
```

P03 strict 계약은 `runtime_config.value_type=BOOLEAN`의 `value`를 정확히 `TRUE|FALSE`로
제한한다. 현재 Unity `ContentSemanticValidator`도 이 규칙을 강제한다. 따라서 v1.0 row와
그 row를 포함한 hash는 현재 계약에서 반드시 실패한다.

### 권장 정정

```csv
P04_TEST_CLOCK_MODE,BOOLEAN,FALSE,BOOL,,,TXT_P04_DEV_CLOCK_DESC,CONFIRMED,TRUE
```

### 필수 출력

- BOOLEAN lexical을 `TRUE|FALSE`로 유지할지, v1.0에서 `0|1`로 정정할지 하나를 확정한다.
- 권장안을 채택하면 `runtime_config.csv` 전체 18 rows와 새 CRLF SHA-256을 제공한다.
- `0|1`을 채택하면 P03 domain vocabulary에 대한 명시적 `CORRECTION`, validator 규칙,
  기존 TRUE/FALSE row의 migration/호환 matrix를 제공한다.
- 이 변경을 반영한 manifest JCS byte length와 SHA-256을 CR-02와 함께 다시 계산한다.

## 3. CR-02 — schema set 3 manifest descriptor와 schema 본문 누락

### 발견된 누락

v1.0은 table hash와 최종 manifest hash를 제공하지만 다음 본문을 제공하지 않는다.

- `facility_construction_rules.csv`의 full manifest descriptor
- `facility_world_assets.csv`의 full manifest descriptor
- `csvSchemaSetVersion=3`을 허용하는 `content_manifest.schema.json`

manifest descriptor에는 field별 `domain`, `nullable`, `enumValues`, FK 배열과 순서가
포함된다. 이 값은 SHA-256 digest의 입력이다. checksum만으로 원본 descriptor를 복원할 수
없으므로 현재 제공된 `4be272...` digest는 재현 가능한 구현 입력이 아니다.

### 필수 출력

다음 중 설명이 아닌 exact JSON을 모두 제공한다.

1. 최종 `content_manifest.json` 전체 JSON
2. 최종 `content_manifest.schema.json` 전체 JSON
3. 신규 2개 table descriptor의 field/FK semantic validator 규칙
4. 최종 manifest JCS byte length와 SHA-256
5. `runtime_config.csv` CR-01 정정값을 반영한 table hash

신규 descriptor에는 최소 다음을 명시한다.

```text
file
schemaVersion
required
sha256
rowCount
primaryKey
fields[].name/domain/nullable/enumValues
foreignKeys[].sourceFields/targetFile/targetFields/mode
```

enum 배열과 FK 배열의 순서도 digest 입력이므로 생략하지 않는다.

## 4. CR-03 — immutable package 경로와 active content 선택

### 발견된 충돌

- 현재 P03 실제 package는 `StreamingAssets/Content/` 루트에 평면으로 존재한다.
- v1.0은 P03 `.1` 디렉터리를 보존하고 `.2` 디렉터리를 추가한다고 서술한다.
- 현재 `AppRoot`는 `StreamingAssets/Content/content_manifest.json`만 로드한다.
- v1.0에는 active version pointer나 loader가 `.2`를 선택하는 exact 규칙이 없다.

### 권장 정정

```text
StreamingAssets/Content/
├─ 1.0.0-content.1/   # P03 60-table immutable BASE
└─ 1.0.0-content.2/   # P04 62-table immutable BASE, active at P04 build
```

P04 build의 compile-time constant `ActiveContentVersion = "1.0.0-content.2"`로 loader가
정확히 해당 디렉터리를 읽고, 런타임 fallback이나 자동 latest 선택은 하지 않는다.

### 필수 출력

- 최종 디렉터리 tree
- 기존 P03 평면 package의 move/copy/delete 판정
- AppRoot/ContentCatalogService가 active package를 선택하는 exact API와 error
- development/test override 허용 여부와 release build 차단 규칙
- generator가 `.1`과 `.2`를 각각 생성·검사하는 명령
- `.gitattributes`에서 중첩 CSV CRLF bytes를 보존하는 exact glob
- Android build에 포함되는 package와 이전 version 보존 범위

## 5. CR-04 — 로컬 profile 발견 규칙 누락

### 발견된 누락

v1.0 Save sequence는 “profile slot을 찾는다”고만 정의한다. 현재 Save 구현은 profileId를
알고 있을 때만 `<persistentDataPath>/saves/<profileId>/save.json`을 load할 수 있으며 profile
index나 `ListProfiles` API가 없다. 앱을 다시 시작했을 때 어떤 profileId를 선택하는지 현재
문서로 결정할 수 없다.

### 권장 정정

P04는 single local profile 정책을 사용한다.

1. `saves/` 바로 아래 canonical UUID directory만 UTF-8 ordinal로 열거한다.
2. 각 directory는 기존 repository recovery/strict validation을 통과해야 candidate다.
3. valid 0개면 새 profile을 생성한다.
4. valid 1개면 해당 profile을 사용한다.
5. valid 2개 이상이면 자동 선택하지 않고 `SAVE_PROFILE_AMBIGUOUS`로 fail closed한다.
6. invalid directory는 자동 삭제하지 않고 진단 목록에만 남긴다.

### 필수 출력

- `IProfileLocator` 또는 대체 type의 exact namespace/API
- directory enumeration, validation, ordering, 0/1/N 처리
- tmp/bak만 남은 profile directory의 발견·복구 규칙
- malformed directory와 symlink/reparse point 처리
- exact error code와 UI localization mapping
- 새 profile commit 성공 전/후 directory 노출 원자성
- EditMode golden tests

새 `profile-index.json`을 선택한다면 전체 schema, atomic write/backup/recovery, Save와의 commit
순서까지 제공해야 한다.

## 6. CR-05 — operationId/requestHash 생성 책임 충돌

### 발견된 충돌

- v1.0 command input DTO는 `operationId`와 `requestHash`를 이미 받는다.
- type 표의 Start handler dependency에는 `clock,id`가 있어 handler가 ID를 만드는 것처럼
  서술한다.
- View는 DTO만 사용하고 Save/Catalog를 직접 호출하지 않아야 하지만, 누가 ID와 JCS hash를
  만들고 retry 중 유지하는지 정해지지 않았다.

### 권장 정정

- `FacilityOperationRequestFactory`를 Application 계층에 둔다.
- Presenter는 `CreateBuild/CreateUpgrade/CreateClaim/CreateAssign/CreateUnassign`을 호출해
  operationId와 requestHash가 완성된 immutable command를 받는다.
- Handler는 ID를 새로 생성하지 않고 hash를 재계산·검증한다.
- command 결과를 받기 전 UI retry는 동일 command instance를 재사용한다.
- revision conflict로 reload한 뒤 사용자가 다시 누르면 새 operationId를 만든다.

### 필수 출력

- factory, hasher, command DTO, handler의 exact namespace와 public API
- Assign/Unassign command도 journal/idempotency를 사용하는지 exact 판정
- retry command 보존 범위와 앱 재시작 뒤 replay 방식
- operationId와 facilityJobOperationId의 구분
- requestHash JCS object를 command 5종 모두 full JSON shape로 제공

## 7. CR-06 — Kingdom world prefab component model 충돌

### 발견된 충돌

- `WorldCanvas`는 현재 `ScreenSpaceOverlay` uGUI Canvas다.
- v1.0 layout은 normalized anchor/RectTransform/Drawer와 같은 uGUI 계약이다.
- 동시에 SpriteRenderer sorting layer, PPU, orthographic camera 계약을 요구한다.
- `FacilityWorldView` prefab child의 exact Unity component가 없어 uGUI Image와
  SpriteRenderer 중 하나를 선택할 수 없다.

### 권장 정정

P04 facility world는 uGUI로 고정한다.

- root: `RectTransform + Canvas + FacilityWorldView`
- BaseSprite/StateOverlay/StoppedIcon: `RectTransform + Image`
- Label: `RectTransform + TextMeshProUGUI`
- HitTarget: `RectTransform + Button + Image(alpha=0)`
- facility root nested Canvas의 `overrideSorting=true`, `sortingLayerName=KingdomWorld`,
  `sortingOrder=20..27`
- Sprite subasset의 PPU/pivot은 향후 world-space 교체 호환 metadata로 검증하되 P04 uGUI
  배치 계산에는 사용하지 않는다.

### 필수 출력

- 13개 generated asset과 8개 prefab의 exact component hierarchy
- 각 component의 raycastTarget, preserveAspect, canvas/sorting 설정
- background/plot이 prefab 공통 child인지 scene-level element인지
- StateOverlay가 6개 state를 표현하는 exact color/sprite/active matrix
- camera/PixelPerfect 설정 중 P04 uGUI에서 실제 수용 기준으로 쓰이는 항목
- 64×64 touch rect와 Safe Area anchor 계산식

## 8. CR-07 — invariant error registry 완결성

### 발견된 누락

v1.0 invariant 표에는 다음 error가 있으나 17장 registry에는 일부가 없다.

```text
SAVE_FACILITY_SET_INVALID
SAVE_FACILITY_NPC_FORBIDDEN
SAVE_FACILITY_STOPPED_INVALID
SAVE_FACILITY_TERMINAL_JOURNAL_MISMATCH
SAVE_FACILITY_STATE_DERIVATION_MISMATCH
FACILITY_CLOCK_INVALID
```

또한 bootstrap 절의 다음 code도 registry에 없다.

```text
P04_BOOTSTRAP_SIGNATURE_MISMATCH
P04_BOOTSTRAP_UUID_FAILED
P04_BOOTSTRAP_VALIDATION_FAILED
P04_BOOTSTRAP_SAVE_FAILED
```

### 필수 출력

누락 code 전부를 다음 exact 표에 추가한다.

```text
Code | Trigger | UserMessageKey | Retryable | LoggingLevel | SaveMutationAllowed
```

필요한 ko-KR/en-US localization row가 추가되면 전체 추가 rows, 최종 localization row count,
CSV SHA-256, manifest digest를 CR-01/CR-02와 함께 다시 제공한다.

## 9. 최종 정정 부록 수용 게이트

- [ ] CR-01 BOOLEAN lexical과 runtime_config exact hash가 확정됐다.
- [ ] CR-02 final manifest 전체 JSON과 schema 전체 JSON이 있다.
- [ ] 신규 descriptor의 field/enum/FK 배열 순서가 exact다.
- [ ] manifest JCS byte length와 SHA-256이 재계산됐다.
- [ ] CR-03 `.1/.2` package tree와 active loader가 exact다.
- [ ] CR-04 profile discovery의 0/1/N/recovery/error가 exact다.
- [ ] CR-05 command ID/hash 생성·retry 책임이 exact다.
- [ ] CR-06 uGUI/SpriteRenderer 선택과 prefab component가 exact다.
- [ ] CR-07 invariant/bootstrap error registry가 완전하다.
- [ ] 변경된 CSV의 전체 rows·row count·SHA-256이 있다.
- [ ] P04 구현 필수 `UNRESOLVED`가 `NONE`이다.

이 부록이 수용되면 Codex는 v1.0 + v1.0.1을 하나의 최종 계약으로 채택하고 추가 설계 요청
없이 canonical package → facility domain/application/Save → Kingdom UI/assets → tests → Android
APK → captures → P04 완료 보고까지 같은 Issue #16/branch/PR에서 진행한다.
