# P01 저장소 기반 완료 보고서

- 기준일: 2026-07-17
- 관련 이슈: [#8 CI: P01 저장소 기반과 콘텐츠 검증 계약 정리](https://github.com/ho-beom-cheon/kingdom_tycoon/issues/8)
- 작업 브랜치: `codex/issue-8-p01-repository-foundation`
- 기준 커밋: `origin/main`의 `fb491ff`
- 단계 문서: `phases/P01_REPOSITORY_FOUNDATION.md`
- 판정: **P01 완료, P02 환경 조건부 진입 가능**

## 1. 변경 요약

P00에서 확인한 깨끗한 clone 기준 결함을 해소했다.

1. 검증된 인계 패키지의 설계 문서, 단계 계약, 프롬프트를 저장소 루트로
   선별 편입했다.
2. CSV 29개, JSON Schema 2개, UI token, OpenAPI 초안을 기준 콘텐츠
   데이터로 편입했다.
3. 콘텐츠 검증기를 `scripts/validate_content.py` 한 경로로 고정했다.
4. pre-commit, pre-push, 로컬 CI, GitHub Actions가 같은 검증기를 호출하도록
   경로를 통일했다.
5. Unity·Gradle·IDE 산출물, 비밀값, OS 파일을 제외하는 `.gitignore`를
   추가했다.
6. 텍스트와 Unity YAML의 LF 정책, 이미지·음원·원본 자산의 binary 정책을
   `.gitattributes`에 추가했다.
7. 서버와 Unity 모듈이 아직 없을 때는 성공으로 숨기지 않고 명시적인
   `SKIPPED` 사유를 출력한다.
8. 설계 누락이나 충돌을 임의 확정하지 않고 `UNRESOLVED`로 보고한 뒤
   상세 설계를 요청하는 규칙을 `AGENTS.md`에 고정했다.

기존 작업 폴더의 미추적 `server-api/`, 루트 Gradle Wrapper, `infra/`,
`docs/P0_DB_IMPLEMENTATION_REPORT.md`와 수정된 `README.md`는 이 브랜치에
복사하거나 stage하지 않았다.

## 2. 변경 파일

### 설계 계약

- `AGENTS.md`
- `CODEX_MASTER_PROMPT.md`
- `SOURCES.md`
- `README.md`
- `docs/00_MASTER_INDEX.md`부터 `docs/33_OPEN_DECISIONS.md`까지 34개
- `phases/P00_BASELINE_REVIEW.md`부터 `phases/P17_BALANCE_OPTIMIZE_RC.md`까지 18개
- `prompts/` 5개

### 콘텐츠 기준선

- `data/csv/`의 CSV 29개와 안내 문서
- `data/schemas/`의 Draft 2020-12 JSON Schema 2개
- `data/ui/ui_tokens.json`
- `data/api/openapi-v1.yaml`

### 저장소·검증 정책

- `.editorconfig`
- `.gitignore`
- `.gitattributes`
- `.githooks/pre-commit`
- `.githooks/pre-push`
- `.github/workflows/ci.yml`
- `.github/workflows/delivery.yml`
- `scripts/ci/run-ci.sh`
- `scripts/validate_content.py`

### 보고

- `docs/reports/P01_REPOSITORY_FOUNDATION_REPORT.md`

## 3. 실행 명령

```powershell
python .\scripts\validate_content.py
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-git-hooks.ps1
git check-ignore -v --no-index -- <ignore-samples>
git check-attr -a -- <text-and-asset-samples>
git lfs ls-files
git diff --check
npx.cmd --yes yaml-lint .github/workflows/ci.yml .github/workflows/delivery.yml
npx.cmd --yes --package ajv-cli --package ajv-formats ajv compile --spec=draft2020 -c ajv-formats -s "data/schemas/*.json"
```

```bash
bash -n .githooks/pre-commit
bash -n .githooks/pre-push
.githooks/pre-commit
.githooks/pre-push origin local-clean-clone
scripts/ci/run-ci.sh
```

깨끗한 clone 검증은 현재 브랜치를 별도 임시 디렉터리에 `--no-local`로
clone한 뒤 Hook 설치부터 위 명령을 다시 실행했다.

## 4. 테스트 결과

| 검사 | 결과 |
|---|---|
| 콘텐츠 ID·참조·확률·JSON 검증 | 통과 |
| 콘텐츠 핵심 규모 | jobs 5, grades 5, ranks 6, regions 5, monsters 27, materials 53, equipment 80 |
| Git Hook Bash 문법 | 통과 |
| pre-commit 전체 단계 | 통과 |
| pre-push 콘텐츠 단계 | 통과 |
| pre-push 서버 단계 | `SKIPPED`: Gradle Wrapper 없음 |
| 로컬 중앙 CI 계약 | 통과 |
| CI 서버 단계 | `SKIPPED`: 서버 모듈·Wrapper 없음 |
| CI Unity 정책 단계 | `SKIPPED`: Unity 프로젝트 없음 |
| GitHub Actions YAML lint | 통과 |
| JSON Schema Draft 2020-12 compile | Ajv + formats 기준 2개 통과 |
| ignore sample 11개 | 모두 기대 패턴과 일치 |
| text·Unity YAML LF 속성 | 일치 |
| 이미지·음원·PSD binary 속성 | 일치 |
| 활성 Git LFS 파일 | 0개, 정책상 의도된 상태 |
| 고신뢰 비밀값 패턴 | 발견 없음 |
| 선택 편입본과 원본 인계 패키지 비교 | data/phases/prompts 및 번호 문서 일치 |
| 깨끗한 clone Hook 설치·검증 | 통과, 작업 트리 청결 유지 |
| 테스트 실패 | 0 |

시스템 기본 Python에는 선택 패키지 `jsonschema`가 없어 해당 라이브러리를
사용한 첫 compile 시도는 실행되지 않았다. 저장소 의존성을 늘리지 않고
일회성 Ajv 2020 + `ajv-formats`로 대체해 두 스키마의 유효성을 확인했다.

## 5. 수동 검증

1. `core.hooksPath`가 `.githooks`로 설정되는 것을 확인했다.
2. Hook이 단계 번호, 현재 단계, 검증기 경로, 소요 시간, 통과·건너뜀 이유를
   콘솔에 출력하는 것을 확인했다.
3. 깨끗한 clone에서 중첩 인계 폴더 없이 루트 검증기만 사용함을 확인했다.
4. 서버와 Unity가 없는 기준선에서 실행하지 않은 검사를 성공으로 오인할
   표현이 없는지 확인했다.
5. P01 브랜치에 애플리케이션 소스, DB Migration, Unity Asset이 포함되지
   않았음을 확인했다.

화면과 런타임 UI 변경이 없어 P01의 화면 캡처는 해당하지 않는다.

## 6. 명세와 차이

### 의도적으로 제외한 항목

- `server-api/`, 루트 Gradle Wrapper, `infra/`: 서버 후보의 설계 권위와
  Phase 순서 변경이 승인되지 않아 P01에 포함하지 않았다.
- `client-unity/`: P02 범위이므로 생성하지 않았다.
- LFS 필터 활성화: 실제 PSD/Aseprite/WAV/MP3 도입과 저장소 비용을 확인한
  뒤 선택적으로 켜도록 주석 정책만 남겼다. 현재는 binary로 보호한다.
- 운영 배포: `OPS_LATER`이므로 Delivery는 검증된 산출물 패키징까지만
  담당한다.

### UNRESOLVED

- `UR-01` 서버 후보 채택과 DB 선행 Phase 승인
- `UR-02` P03 전 Save 객체·Migration·복구 상세 계약
- `UR-03` P03 전 CSV field domain·sentinel·tagged-union 계약
- `UR-04` P16 전 인증·오류·DTO·멱등성 API 계약

이 항목은 P01 저장소 기반을 막지 않지만, 영향 Phase의 구현 전에 상세
설계서가 필요하다. 전달 전에는 임의로 구현하지 않는다.

## 7. 남은 위험과 영향

### 남은 위험

1. 설치된 Unity Editor는 `6000.4.11f1`뿐이며 명세의 Unity 6.3 LTS 계열과
   다르다.
2. `data/api/openapi-v1.yaml`은 P00에서 확인한 summary, security, error
   response, schema 공백이 남아 있어 P16 구현 계약으로는 부족하다.
3. Save Schema는 최상위 구조만 있고 내부 객체 계약은 비어 있다.
4. 콘텐츠 다형 필드의 도메인 규칙이 없어 P03 importer 검증 범위를 아직
   확정할 수 없다.
5. Branch protection의 필수 check 설정은 저장소 정책에서 별도 확인이
   필요하다.

### 변경 영향

- 콘텐츠 데이터: 기준 파일 편입, 값의 의미 변경 없음
- Save: 초안 Schema 편입, 런타임·Migration 변경 없음
- API: OpenAPI 초안 편입, Controller·DTO·동작 변경 없음
- DB: Migration·실행 변경 없음
- 애플리케이션 소스: 변경 없음
- 성능: 런타임 영향 없음
- 배포: 운영 배포 변경 없음

## 8. 다음 단계 진입 가능 여부

P01 완료 조건인 Git·콘텐츠 검증, 테스트 실패 0, 깨끗한 clone 재현을
충족했다. 커밋은 다음 세 단위로 독립 롤백할 수 있다.

1. `a7bcdab docs: add authoritative project specification`
2. `4a2c41a chore: add baseline content data`
3. `b4c491b ci: align repository validation contract`

따라서 저장소 기준으로는 P02 진입이 가능하다. 다만 현재 설치된
`6000.4.11f1`로 프로젝트를 만들면 Unity 6.3 LTS 고정 계약을 위반한다.
P02 시작 전 당시 사용할 정확한 Unity 6000.3 LTS 패치를 확인·설치하고,
Android Build Support 모듈까지 준비해야 한다.

P02의 Built-in 대 URP 2D 선택은 기존 결정대로 버티컬 슬라이스 측정으로
확정할 수 있어 추가 상세 설계가 당장 필요하지 않다. 반면 P03에 들어가기
전에는 `UR-02`와 `UR-03` 상세 설계서를 받아야 한다.
