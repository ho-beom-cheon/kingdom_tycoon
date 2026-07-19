# P03 콘텐츠 파이프라인·Save 구현 보고서

## 1. 완료 판정

- Phase: `P03_CONTENT_PIPELINE_SAVE`
- GitHub Issue: `#14`
- 상태: **완료**
- canonical content: `1.0.0-content.1`, manifest contract `v2`, CSV schema set `v2`
- 기준 설계: `docs/design/TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md`
- 다음 Phase 진입: **가능**. P03 구현·테스트·Android player build gate를 통과했다.

Save strict schema·원자 저장·복구 계약과 canonical content 60-table package, runtime importer,
semantic validator, deterministic fixture, placeholder Addressables를 모두 구현했다. 기존 `data/csv`
29개 파일은 migration input으로 보존하고 runtime은 `StreamingAssets/Content`만 읽는다.

## 2. 변경 요약

| 영역 | 결과 |
|---|---|
| Save | 40개 strict `$defs`, JCS 해시, atomic replace, 3단 backup, split-brain/future 차단 |
| Canonical writer | 최종 설계 Markdown에서 60개 exact CSV를 CRLF/BOM 없음으로 재현 |
| Manifest | v2 strict descriptor 60개, 504 fields, 99 FK, JCS JSON |
| Runtime importer | 15 domain, table ordering, hash/header/row/PK/FK/lifecycle/tagged-union fail-closed |
| Semantic validator | item/equipment source, autonomy, raid, offline, tutorial DAG, condition graph, runtime config, random equipment |
| Determinism | SplitMix64, rejection bounded selection, SHA-256 big-endian seed, 성장 계수·idempotency golden |
| Assets | 직업 5종 prefab, body/role sprite subasset, marker, Addressables group/address |
| Build gate | package/registry/address/prefab/animation 검증 후 Android player build |
| CI/CD | hook·공유 CI에서 package drift 검사, delivery bundle에 canonical package 포함 |

## 3. Canonical package 계약

단일 authoring input은 최종 설계서이며 `scripts/generate_canonical_content.py`가 다음을 수행한다.

1. manifest JSON Schema, 60-table manifest fixture, 60개 CSV block을 추출한다.
2. legacy input이 29 CSV + README인지 확인한다.
3. CSV heading rowCount, 열 폭, PK duplicate, lifecycle, static FK closure를 검사한다.
4. UTF-8 BOM 없음, RFC 4180, CRLF, final CRLF 1개로 canonical bytes를 만든다.
5. 각 table SHA-256을 descriptor와 비교하고 manifest를 RFC 8785 subset JCS로 직렬화한다.
6. `--check`에서 schema와 `StreamingAssets/Content` 61개 runtime 파일의 drift/stale 파일을 거부한다.

최종 파생값은 다음과 같다.

- manifest SHA-256: `7b385938bcd120988abb4157401e535a0cf3c09c146cb60f859819efebadfa8b`
- package fingerprint: `75f9c92eb6ef9cc52d099f37501c11ad5972acd2e6034af7d06bf7528ebbfc56`
- tables: `60`
- localizations: `470 rows`
- manifest fields/FK: `504 / 99`

## 4. Runtime importer와 semantic validator

manifest root/nested object의 missing/unknown property를 구분하고 다음 metadata를 강제한다.

- `schemaId=urn:tycoon:content-manifest:v2`
- `contractVersion=2`, `csvSchemaSetVersion=2`
- `packageKind=BASE`, `baseContentVersion=null`
- table file은 UTF-8 ordinal ascending, 각 file은 `schemaVersion=1`, `required=true`
- hash는 lowercase HEX64, header와 descriptor field 순서는 exact match

field domain은 `STRING`, `STABLE_ID`, `BOOL`, `INT32`, `INT64`, `SAFE_INT`, `DECIMAL`,
`UTC_INSTANT`, `DATE`, `LOCALE`, `SEMVER`, `HEX64`, `SEED64`, `STATUS`, `ENUM` 15개다.
canonical pipe-list와 embedded JSON을 각각 `CSV_PIPE_LIST_FORBIDDEN`,
`CSV_JSON_CELL_FORBIDDEN`으로 거부한다. 오류 하나라도 있으면 immutable catalog를 공개하지 않는다.

semantic validator는 P03 실행 범위만 검사한다. 실제 모집 비용 차감, 용병 instance 생성, offline 정산,
tutorial grant 적용, random equipment inventory 적용은 후속 Phase 기능이며 P03에서 선행 구현하지 않았다.

## 5. Deterministic fixture

`DeterministicContentRandom`은 unsigned 64-bit SplitMix64와 rejection sampling을 구현한다.
테스트는 다음 설계 golden을 exact value로 고정한다.

- mercenary operation seed `123456789`: 7개 raw draw와 6개 성장 factor
- random equipment operation seed `987654321`: template/quality/affix draw
- affix success seed `1`: inclusive refine value `426 bps`
- summon seed SHA-256 big-endian prefix: draw index `0`, `9`
- offline line identity와 tutorial grant idempotency SHA-256

## 6. Placeholder asset·Addressables

`P03ContentAssetGenerator`는 Unity Editor API로만 다음을 생성한다.

- group: `Content-Mercenary-Placeholders-v1`
- prefab: `Warrior`, `Guardian`, `Archer`, `Mage`, `Cleric`
- address: `ASSET_MERC_PLACEHOLDER_{JOB}_V1`
- root: `MercenaryPlaceholderMarker(jobId, assetId, fingerprint)`
- child: `Body`, `RoleMark` SpriteRenderer
- sorting layer: `Characters`, body order `0`, mark order `1`
- animation component/asset: 없음

build preprocessor는 package 60-table load, `asset_register.csv` 1:1, address 중복, prefab/marker,
animation 부재를 검사한다. Addressables는 player build와 함께 build하도록 설정했다.

## 7. 설계 적용 중 발견한 정정

최종 설계의 CSV row/hash는 모두 일치했지만 descriptor와 row 사이에 두 자기모순이 있었다.
데이터와 tagged-union 의도가 명백해 추가 설계 요청 없이 저장소 설계서에 정정을 기록했다.

| 항목 | 원문 | 저장소 적용 | 영향 |
|---|---|---|---|
| `monsters.type` | `NORMAL|ELITE|BOSS` | `RAID` 추가 | raid boss 2 rows domain 검증 가능 |
| `runtime_config.value` | `DECIMAL` | `STRING` | INTEGER/DECIMAL/BOOLEAN/STRING discriminator 수용 |

CSV row와 60개 table hash는 변경하지 않았다. descriptor 변경 때문에 manifest JCS digest와 package
fingerprint만 build-derived 값으로 다시 계산했다.

## 8. 검증 결과

실행 환경은 Unity `6000.3.20f1 (c9ba695d4f07)`과 Android Build Support다.

```powershell
python scripts/generate_canonical_content.py --check
python scripts/generate_save_schema.py --check
python scripts/validate_content.py

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics `
  -projectPath .\client-unity -runTests -testPlatform EditMode `
  -testResults .\artifacts\p03-editmode.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics `
  -projectPath .\client-unity -runTests -testPlatform PlayMode `
  -testResults .\artifacts\p03-playmode.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics -quit `
  -buildTarget Android -projectPath .\client-unity `
  -executeMethod KingdomTycoon.Editor.P03PlayerBuild.BuildAndroid

.\gradlew.bat test --no-daemon
```

- canonical package: `60 tables verified`
- Save schema: `40 definitions, 2 outputs`, drift 없음
- legacy content validator: passed
- Unity EditMode: `23 passed / 0 failed`
- Unity PlayMode: `1 passed / 0 failed`
- Android player: success, APK `43,367,828 bytes`
- server regression: `BUILD SUCCESSFUL`, 4 tasks

## 9. 파일 영향

- 설계/보고: 최종 통합 설계서와 이 보고서
- authoring: `scripts/generate_canonical_content.py`
- runtime package: `client-unity/Assets/StreamingAssets/Content`
- runtime: importer, semantic validator, deterministic random, placeholder marker
- editor/build: placeholder generator, build guard, Android build entrypoint, Addressables group/settings
- tests: canonical 60-table import·v2 manifest 오류·deterministic golden
- CI/CD: pre-commit, pre-push, shared CI, delivery content artifact

서버 Java source와 Flyway migration은 변경하지 않았다. 기존 적용 migration은 immutable 상태다.

## 10. 남은 위험과 후속 Phase 경계

- Android APK는 개발 build이며 app-store signing/publication은 수행하지 않았다.
- 실제 device의 전원 차단 중 Save durability는 별도 장치 테스트가 필요하다.
- 같은 contentVersion의 다른 manifest는 `CONTENT_RELEASE_SPLIT_BRAIN`으로 차단해야 하며,
  server publish adapter가 이 digest를 DB에 쓰는 동작은 해당 운영/publish Phase에서 연결한다.
- Save content alias table은 현재 header-only다. 공개 release 후 ID 변경 시 새 contentVersion과 alias가 필요하다.
- P04 이후 gameplay orchestration은 이 P03 catalog/fixture를 소비하되 P03 데이터를 재정의하지 않는다.

P03의 `UNRESOLVED`는 `NONE`이며 P04 구현을 시작할 수 있다.
