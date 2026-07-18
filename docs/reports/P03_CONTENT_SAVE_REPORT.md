# P03 콘텐츠 파이프라인·Save 구현 보고서

## 1. 상태

- Phase: `P03_CONTENT_PIPELINE_SAVE`
- GitHub Issue: `#14`
- 상태: **핵심 인프라 구현 완료 / canonical gameplay data 변환은 UNRESOLVED**
- 기준 계약: `TYCOON_SAVE_CSV_CONTRACT_DESIGN_v1.1` + v1.2 + v1.2.1 + v1.2.2
- 다음 Phase 진입: **불가**. 8장의 누락 데이터가 확정되어야 P03 완료 판정을 내릴 수 있다.

Save 구조·해시·복구와 schema 기반 CSV importer는 구현하고 자동 테스트로 고정했다. 반면 현재
`data/csv`에 존재하지 않는 모집 비용, 생성 profile, random equipment spec 등을 임의로 만들지는 않았다.
현재 CSV는 migration input으로만 유지하며 canonical runtime package로 승격하지 않는다.

## 2. 기준선 차이

| 영역 | 구현 전 | 이번 구현 |
|---|---|---|
| Save schema | root 일부만 검사하는 placeholder | 40개 strict `$defs`, 모든 object의 unknown field 거부 |
| JSON | 일반 parser 의존 | duplicate key, comment, trailing content 거부 |
| 무결성 | `checksum` placeholder | RFC 8785 canonicalization + payload/file SHA-256 분리 |
| 저장 | repository 없음 | profile lock, tmp 검증, atomic replace, 3단 backup |
| 복구 | 없음 | 후보 전수 검증, revision/time/source 정렬, split-brain 차단, 새 revision 승격 |
| Migration | 없음 | 연속 1-version migration registry와 future-version 차단 |
| CSV | JSON/pipe cell을 직접 해석 | RFC 4180 + manifest SHA-256 + field domain + PK/FK + tagged-union 검증 |
| Runtime catalog | 없음 | immutable table catalog와 lazy `ContentCatalogService` |

## 3. Save 구현 계약

### 3.1 strict document

- `saveVersion=1`, `schemaId=urn:tycoon:save:v1`
- root와 payload 13개 필드를 포함한 모든 object에 `additionalProperties=false`
- SafeInt는 `±9,007,199,254,740,991` 범위로 제한
- 신규 save/instance/operation ID는 RFC 9562 UUIDv7 형식으로 제한
- UTC는 millisecond 단위 `yyyy-MM-ddTHH:mm:ss.fffZ`만 허용
- `Seed64`는 canonical unsigned 64-bit decimal lexical form을 추가 검사
- 시설 8종 집합, 시설 state/job matrix, terminal time, journal subtype을 의미 검증
- `kingdom.facilityUpgradeCount == Σ(facilities.level - 1)` 검증
- equipment 양방향 장착 관계와 중복 점유 차단
- 모집 pending receipt 상태, journal terminal/result/error 상태 검증
- v1.2.2 generated equipment reward snapshot discriminator 검증

schema 원본은 `data/schemas/save.schema.json`, Unity runtime 사본은
`Assets/KingdomTycoon/Resources/Contracts/save.schema.json`이다. 두 파일은
`scripts/generate_save_schema.py` 한 소스에서 생성하며 `--check`로 drift를 차단한다.

### 3.2 해시

- `payloadSha256 = SHA-256(JCS(payload))`
- `fileSha256 = SHA-256(JCS(EnvelopeDigestInput))`
- `EnvelopeDigestInput`은 payload 원문과 `fileSha256` 자신을 제외하고 `payloadSha256`을 포함한다.
- 승인된 `JCS-PAYLOAD-001` 42-byte/hash와 `JCS-ENVELOPE-001` 455-byte/hash를
  EditMode test에서 그대로 검증한다.

### 3.3 저장·복구

경로는 `Application.persistentDataPath/saves/{profileId}/`이고 후보 우선순위는 다음과 같다.

1. `save.json`
2. `save.tmp`
3. `save.bak1.json`
4. `save.bak2.json`
5. `save.bak3.json`

저장 순서는 process semaphore → OS exclusive `save.lock` → expected revision 확인 → revision +1 →
tmp write-through/flush → tmp 재읽기 검증 → 검증된 backup rotation → atomic replace → active 재검증이다.
active를 먼저 삭제하지 않는다. 후보 선택은 `revision DESC → savedAtUtc DESC → source priority`이며,
동일 revision에 다른 `fileSha256`이 있으면 `SAVE_SPLIT_BRAIN`으로 fail closed한다. backup/tmp 복구는
선택 파일을 그대로 active로 이름만 바꾸지 않고 새 revision으로 다시 commit한다.

## 4. canonical CSV importer 계약

- RFC 4180 quote, escaped quote, CRLF/LF 처리
- manifest table별 SHA-256, row count, header와 순서 검증
- header는 `snake_case`, PK는 composite key까지 deterministic하게 검사
- domain: `STRING`, `STABLE_ID`, `BOOL`, `INT32`, `SAFE_INT`, `DECIMAL`,
  `UTC_INSTANT`, `STATUS`, `ENUM`
- bool은 `TRUE|FALSE`, null은 nullable field의 빈 셀만 허용
- canonical cell에서 pipe list와 embedded JSON 금지
- enabled row가 disabled FK target을 참조하면 실패
- condition group member discriminator의 두 FK 중 정확히 하나만 허용
- reward entry에서 `GENERATED_MERCENARY` 금지
- system reward sentinel과 reward type별 registry를 분리 검증

`ContentCatalogService.Initialize`는 파일 I/O를 수행하지 않는다. 실제 package load를 명시적으로 호출할
때만 manifest와 모든 table을 검증하고, 오류가 하나라도 있으면 catalog를 공개하지 않는다.

## 5. UNRESOLVED canonical data

아래 값은 기존 CSV와 전달 설계서 어느 쪽에도 실제 row 값이 없다. P03 runtime package를 만들기 전에
ChatGPT 상세 설계에서 값을 확정해야 한다.

| 누락 데이터 | 필요한 확정 값 | 임의 변환 시 문제 |
|---|---|---|
| recruitment pools | pool별 `cost_type`, `cost_amount`, pity/rate-up group ID, 기간 | 결제·서버 권위 계약 변경 |
| recruitment entries | `result_type`, generation profile ID, job selection discriminator | 생성 결과 재현 불가 |
| pity/rate-up | canonical group/rule/entry ID와 featured share | 서버 cache와 Save FK 불일치 |
| mercenary generation | generation profile, name pool, appearance set, growth/stat rule | 동일 seed 결과 재현 불가 |
| random equipment | tier spec ID별 slot/job/tier/quality 허용 집합 | reward snapshot 검증 불가 |
| tutorial | step, prerequisite, grant idempotency key와 reward rows | 신규 Save 기본값 결정 불가 |
| offline rules | settlement type별 max seconds, efficiency, reward group | 정산 snapshot 생성 불가 |
| content release | canonical contentVersion, generatedAt/publish kind, file manifest 목록 | Save contentVersion 호환 판정 불가 |

필요한 보완 문서는 위 8개 표의 canonical CSV row를 값까지 제공해야 한다. 새 enum이나 다음 Phase
게임 기능 구현은 필요하지 않다.

## 6. 검증

실행 환경은 Unity `6000.3.20f1 (c9ba695d4f07)`이다.

```powershell
python scripts/generate_save_schema.py --check
python scripts/validate_content.py

& <UNITY_EDITOR>\Editor\Unity.exe `
  -batchmode -nographics `
  -projectPath .\client-unity `
  -runTests -testPlatform EditMode `
  -testResults .\build\p03-editmode-results.xml
```

- EditMode: `15 passed / 0 failed / 0 skipped`
- PlayMode: `1 passed / 0 failed / 0 skipped`
- repository CI: `passed` (`22s`, PostgreSQL 18.4 Testcontainers 통합 테스트 10건 포함)
- 승인 JCS vector: payload/envelope 모두 일치
- Save: strict schema, unknown property, counter mismatch, backup recovery, split-brain fixture 통과
- Content: canonical import, pipe cell, bool lexical, forbidden reward union fixture 통과

## 7. 영향과 남은 위험

- Save/API: `saveVersion=1` 파일 계약을 새로 도입한다. 서버 API나 DB schema는 변경하지 않았다.
- 데이터: 기존 `data/csv`는 수정하지 않았고 아직 canonical runtime source가 아니다.
- 성능: load 시 최대 5개 파일(각 16 MiB 제한)을 검증한다. 정상 save는 tmp와 active를 재검증한다.
- 플랫폼: atomic replace를 지원하지 않는 플랫폼은 비원자 fallback을 쓰지 않고
  `SAVE_ATOMIC_REPLACE_UNSUPPORTED`로 실패한다.
- durability: managed API가 directory fsync를 보장하지 않는 플랫폼의 power-loss window는 남는다.
- lock: 현재 구현은 OS exclusive file lock을 사용한다. 해당 의미가 신뢰 불가능한 filesystem을 위한
  lease adapter와 실제 Android 종료/전원 차단 장치 테스트는 후속 P03 검증 항목이다.
- content: 8개 묶음의 누락 값이 확정되기 전에는 `StreamingAssets/Content` package를 생성하지 않는다.

## 8. 롤백·호환 전략

- schema와 runtime validator/repository는 한 기능 commit으로 되돌릴 수 있다.
- 현재 공개된 이전 saveVersion이 없으므로 v0→v1 가상 migration은 만들지 않았다.
- future saveVersion은 fail closed하고, 실제 v2가 생길 때 `1→2` 한 단계 migration을 등록한다.
- canonical content는 기존 legacy CSV와 동시에 runtime source로 사용하지 않는다. 보완 데이터 확정 후
  별도 migration commit에서 단방향 package를 생성하고 legacy reader를 제거한다.
