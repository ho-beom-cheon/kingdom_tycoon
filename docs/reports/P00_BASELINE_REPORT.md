# P00 기준선 분석 보고서

- 기준일: 2026-07-17
- 관련 이슈: [#6 문서: P00 저장소 기준선 분석 및 진입 보고서 작성](https://github.com/ho-beom-cheon/kingdom_tycoon/issues/6)
- 기준 브랜치: `codex/issue-6-p00-baseline-review`
- 기준 커밋: `origin/main`의 `46202de`
- 단계 문서: `tycoon_v1_0_pro_codex_handoff/phases/P00_BASELINE_REVIEW.md`
- 판정: **기준선 분석 완료, P01 조건부 진입 가능**

## 1. 요약 판정

현재 Git 기준선에는 Git Hook 관련 파일만 들어 있고, 제품 설계 패키지와 서버 후보 구현은 모두 추적되지 않은 로컬 파일로만 존재한다. Unity 프로젝트는 없다.

P01은 저장소 기반을 정리하는 단계이므로 다음 조건을 지키면 진입할 수 있다.

1. 진행 중인 Git Hook PR #3과 CI/CD PR #5의 반영 순서를 먼저 확정한다.
2. 설계 패키지의 `docs/`, `data/`, `phases/`, `prompts/`, 검증 스크립트를 저장소 루트 구조로 선별 편입한다.
3. 미추적 `server-api/`, Gradle Wrapper, `infra/`는 채택 결정 전까지 보존하되 P01 변경에 섞지 않는다.
4. P01에서 깨끗한 clone에서도 동작하도록 콘텐츠 검증기, `.gitignore`, `.gitattributes`, Hook, CI 경로를 일치시킨다.

JDK 25는 분석 도중 workspace-local `.tools/`에 추가되어 명시적 `JAVA_HOME`으로 서버 후보 테스트를 통과했다. 다만 기본 `JAVA_HOME`은 여전히 JDK 17이고 `.tools/`는 Git에 포함되지 않으므로 깨끗한 개발 환경에서의 재현 절차가 필요하다. 이 문제는 서버 후보를 격리하면 P01 자체를 막지는 않는다. Unity 6.3 LTS 정확한 패치 선택과 프로젝트 생성은 P02 진입 전에 필요하다.

## 2. 분석 범위와 명세 우선순위

다음 순서로 판단했다.

1. `tycoon_v1_0_pro_codex_handoff/AGENTS.md`
2. `docs/02_DECISION_REGISTER.md`
3. `docs/04_V1_SCOPE.md`
4. 관련 시스템 문서와 `docs/33_OPEN_DECISIONS.md`
5. `data/`의 `TUNABLE` 초기값
6. `phases/P00_BASELINE_REVIEW.md`

이번 단계에서는 애플리케이션 소스와 설정을 수정하지 않았다. 새로 추가한 파일은 이 보고서뿐이다.

## 3. 저장소 현황

### 3.1 Git에 추적된 기준선

`origin/main`에서 확인한 추적 파일은 5개다.

```text
.gitattributes
.githooks/pre-commit
.githooks/pre-push
README.md
scripts/setup-git-hooks.ps1
```

따라서 `main`만 새로 clone하면 다음 항목은 존재하지 않는다.

- 제품·시스템 설계 문서
- 콘텐츠 CSV와 JSON Schema
- 콘텐츠 검증기
- Unity 프로젝트
- 서버 프로젝트와 Gradle Wrapper
- PostgreSQL 로컬 인프라
- GitHub Actions 워크플로우

GitHub Actions는 Draft PR #5에 구현되어 있지만 아직 `main` 기준선은 아니다. Git Hook 진행 로그 개선도 Draft PR #3에만 존재한다.

### 3.2 추적되지 않은 로컬 항목

```text
.gitignore
build.gradle
gradle/
gradlew
gradlew.bat
infra/
server-api/
settings.gradle
tycoon_v1_0_pro_codex_handoff/
tycoon_v1_0_pro_codex_handoff.zip
```

이 항목들은 기존 작업물일 수 있으므로 삭제·재작성·일괄 커밋하지 않았다. 현재는 분석 증거와 채택 후보로만 취급한다.

### 3.3 권장 구조와 현재 구조 차이

| 영역 | 설계 패키지 권장 | 현재 Git 기준선 | 로컬 후보 | 판정 |
|---|---|---|---|---|
| 설계 문서 | 루트 `docs/` | 없음 | 중첩 패키지에 34개 | P01 선별 편입 필요 |
| 콘텐츠 데이터 | 루트 `data/` | 없음 | 중첩 패키지에 34개 파일 | P01 선별 편입 필요 |
| 단계·프롬프트 | `phases/`, `prompts/` | 없음 | 중첩 패키지에 존재 | P01 선별 편입 필요 |
| 콘텐츠 검증 | `scripts/validate_content.py` | 없음 | 중첩 패키지에 존재 | Hook 정상화를 위해 P01 필수 |
| Unity | `client-unity/` | 없음 | 없음 | P02에서 생성 |
| 서버 | `server-api/` | 없음 | 미추적 후보 존재 | 채택 결정 전 격리 |
| 로컬 DB | `infra/` | 없음 | 미추적 후보 존재 | 서버 결정과 함께 검토 |
| CI | `.github/workflows/` | 없음 | PR #5에 존재 | PR 병합 필요 |

### 3.4 설계 패키지 무결성

- `SHA256SUMS.txt`의 110개 항목을 PowerShell SHA-256으로 다시 계산했고 모두 일치했다.
- ZIP 목록 검사는 성공했다.
- 제공된 콘텐츠 검증 결과를 로컬에서 재실행했고 동일하게 통과했다.
- 압축 해제 폴더의 최상위 `ci-cd.md`는 원본 ZIP과 `MANIFEST.txt`에 없다. PR #5에서 파생된 로컬 문서로 보이며 설계 원본으로 취급하면 안 된다.
- `MANIFEST.txt`와 `SHA256SUMS.txt` 자체가 manifest 목록에 없는 것은 자기 기술 파일이므로 패키지 변조 증거로 보지 않았다.

## 4. 설계 문서 인덱스

설계 패키지는 제품·범위 5개, 게임 시스템 9개, 화면·경험 5개, 개발·운영 15개로 총 34개 문서를 제공한다.

| 분류 | 문서 | 기준 상태 |
|---|---|---|
| 상태·결정 | `01`, `02`, `33` | CONFIRMED/TUNABLE/UNRESOLVED 통제 |
| 제품·범위 | `03`~`05` | 1.0 핵심 루프와 제외 범위 |
| 게임 시스템 | `06`~`14` | 주요 구조 확정, 수치는 주로 TUNABLE |
| UI·저장·밸런스 | `15`~`19` | 구조 확정, 일부 테스트 결정 |
| Unity·서버·Git | `20`~`23` | 기술 기준 확정, 운영 배포는 OPS_LATER |
| 성능·보안·에셋 | `24`~`26` | 목표와 경계 확정 |
| 운영·후속·위험 | `27`~`29` | OPS_LATER/후속 범위 분리 |
| 용어·완료·버전 | `30`~`32` | 구현·검증 기준 |

`phases/P00`부터 `P17`까지 18개 단계가 있으며, 선행 Phase 완료 전 다음 Phase로 이동하지 않는 순차 계약이다.

## 5. Unity 기준선

### 5.1 현재 상태

- `client-unity/` 없음
- `ProjectSettings/ProjectVersion.txt` 없음
- `Packages/manifest.json` 없음
- `Assets/`, Scene, Prefab 없음
- Assembly Definition 없음
- EditMode/PlayMode 테스트 없음
- Visible Meta Files와 Force Text 확인 불가

### 5.2 설치 환경

- 설치된 Editor: `6000.4.11f1`
- 명세: Unity 6.3 LTS 계열을 선택하고 `ProjectVersion.txt`에 정확한 패치 고정
- 판정: 설치 버전과 명세 계열이 다르다.

Unity 공식 지원 문서는 Unity 6.3을 현재 LTS로 설명하고 2027년 12월까지 지원한다고 명시한다. P02 시작 시 Unity Hub에서 당시 최신 6000.3 LTS 패치를 확인·설치하고, 프로젝트 생성 즉시 버전을 고정해야 한다.

참고: <https://unity.com/releases/unity-6/support>

### 5.3 P02 전 필수 확인

- 정확한 6000.3 패치 버전
- Android Build Support, SDK/NDK/OpenJDK 모듈 설치
- Built-in과 URP 2D의 버티컬 슬라이스 비교 기준
- Version Control `Visible Meta Files`
- Asset Serialization `Force Text`
- uGUI, TextMeshPro, Input System, Localization, Test Framework 패키지 버전

## 6. 서버·PostgreSQL 기준선

### 6.1 미추적 서버 후보

로컬 `server-api/`에는 다음이 존재한다.

- Java 소스 28개
- 패키지: `config`, `content`, `idempotency`, `outbox`, `player`, `reward`, `summon`, `support`, `wallet`
- Flyway 소스 Migration 16개: versioned 14개, repeatable 2개
- Spring Boot Actuator/JDBC/Validation/Flyway/PostgreSQL/Testcontainers 의존성 선언
- `/api/v1` context path
- 환경 변수 기반 DB 사용자·비밀번호 설정
- Testcontainers 통합 테스트 소스 4개
- JUnit XML 기준 테스트 10개, 실패·오류·건너뜀 0개

다음은 존재하지 않거나 확인되지 않았다.

- `@RestController`, `@Controller`, HTTP mapping
- OpenAPI 구현과 Controller 간 계약 테스트
- Git 이력과 코드 출처

Migration 번호 `V006`, `V010`, `V014`가 비어 있다. Flyway가 연속 번호를 강제하지는 않지만, 의도적으로 예약한 것인지 누락인지 기록이 없어 채택 시 확인해야 한다.

### 6.2 버전 적합도

| 항목 | 명세 | 후보 설정/환경 | 판정 |
|---|---|---|---|
| Java | 25 LTS | 빌드 toolchain 25, 로컬 JDK 17만 설치 | 환경 불일치 |
| Spring Boot | 4.1.x | 4.1.0 | 일치 |
| Gradle | Wrapper | 9.2.1 | 구조 일치 |
| PostgreSQL | 18.x, 기준 18.4 | Compose `postgres:18.4` | 일치 |
| DB 변경 | Flyway 전용 | Flyway SQL 존재 | 구조 일치, 실행 미검증 |
| 테스트 | JUnit 5 + Testcontainers | 통합 테스트 4개 클래스·10개 테스트 통과 | 후보 내부 적합, Git 미채택 |

공식 기준도 Spring Boot 4.1.0, PostgreSQL 18.4, Java 25의 사용 가능성을 확인했다. Gradle 공식 호환표는 Java 25 toolchain과 실행을 Gradle 9.1 이상에서 지원한다.

- Spring Boot: <https://spring.io/projects/spring-boot/>
- PostgreSQL 18.4: <https://www.postgresql.org/docs/release/18.4/>
- OpenJDK 25: <https://openjdk.org/projects/jdk/25/>
- Gradle Java 호환표: <https://docs.gradle.org/current/userguide/compatibility.html>

### 6.3 실행 검증

```text
./gradlew.bat --version
→ Gradle 9.2.1 실행 성공, Launcher JVM 17

./gradlew.bat --no-daemon :server-api:test
→ 실패: Java 25 toolchain을 찾을 수 없고 자동 다운로드 저장소가 설정되지 않음

JAVA_HOME=.tools/jdk25-extracted/jdk-25.0.3+9 ./gradlew.bat --no-daemon :server-api:test
→ 성공: 4 actionable tasks, JUnit XML 기준 10 tests, failures=0, errors=0, skipped=0

docker compose -f infra/docker-compose.local.yml config --quiet
→ 성공
```

Docker Engine과 Compose는 실행 가능하다. 이 P00 작업에서는 영속 로컬 데이터베이스를 직접 기동하거나 Flyway를 적용하지 않았지만, 분석 중 동시에 생성된 DB 구현 보고서는 별도 작업 흐름에서 로컬 DB와 Testcontainers 검증을 완료했다고 기록한다. JDK 25는 `.tools/`의 로컬 설치를 명시해 서버 테스트를 재현했다. 후보 서버 자체와 JDK 설치 절차는 아직 Git에 채택되지 않았다.

### 6.4 보안 설정 관찰

- 실제 비밀번호는 코드 기본값으로 제공하지 않고 환경 변수로 요구한다.
- `.env.example`에는 교체용 placeholder만 있다.
- PostgreSQL 초기화 스크립트는 비밀번호를 `psql` 변수로 전달한다.
- 고신뢰 비밀값 패턴은 발견되지 않았다. 단순 `password` 패턴은 placeholder와 환경 변수 참조를 탐지했다.

## 7. Git·Hook·CI 기준선

### 7.1 정상 확인

- `core.hooksPath=.githooks`
- Git LFS 3.7.1 설치
- Hook 파일 LF 강제 속성 적용
- 현재 Hook Bash 문법 통과 가능
- `.env`, `.env.*`, Gradle build 디렉터리는 로컬 `.gitignore` 후보에서 제외

### 7.2 기준선 결함

1. 추적된 Hook은 콘텐츠 검증기를 반드시 요구하지만 검증기는 Git에 없다.
   - 로컬에서는 중첩 패키지를 찾아 통과한다.
   - 깨끗한 clone에서는 `Content validator not found.`로 모든 커밋·푸시가 막힌다.
2. `pre-push`는 `server-api/gradlew`만 찾지만 현재 Wrapper 후보는 저장소 루트에 있다.
   - 서버 후보가 존재해도 테스트를 건너뛴다.
3. `.gitignore`는 아직 미추적이며 Unity `Library/`, `Temp/`, `Logs/`, `Builds/`, keystore, OS 파일을 충분히 제외하지 않는다.
4. `.gitattributes`는 Hook LF만 지정한다.
   - `core.autocrlf=true` 환경에서 CSV, Unity YAML, Markdown의 줄바꿈 정책이 고정되지 않는다.
5. Git LFS 추적 패턴과 LFS 파일이 없다.
6. CI/CD는 PR #5에만 있고 `main`에는 없다.
7. branch protection 상태는 공개 API만으로 확정하지 않았다.

### 7.3 P01 조치 기준

- 콘텐츠 검증기를 루트 `scripts/`에 먼저 추적
- Hook과 CI가 같은 검증 명령을 호출하도록 단일화
- Wrapper 위치를 루트 또는 모듈 내부 중 하나로 확정하고 Hook/CI 경로 통일
- Unity·Java·비밀값·OS 산출물까지 `.gitignore` 보강
- 텍스트·Unity YAML·CSV LF 정책을 `.gitattributes`에 명시
- PSD/Aseprite/WAV 등은 실제 에셋 도입 전 선택적으로 LFS 활성화
- PR #3과 #5 병합 후 P01 브랜치를 최신 `main`에 맞춤

## 8. 콘텐츠 데이터 검증

### 8.1 파일과 규모

- CSV 29개
- 데이터 행 532개
- 모든 CSV UTF-8 BOM 확인
- JSON Schema 2개
- UI token JSON 1개
- OpenAPI 3.1 초안 1개

핵심 규모는 명세와 맞는다.

| 항목 | 데이터 | 1.0 명세 | 판정 |
|---|---:|---:|---|
| 직업 | 5 | 5 | 일치 |
| 기본 등급 | 5 | 5 | 일치 |
| 성장 랭크 | 6 | 6 | 일치 |
| 왕국 단계 | 5 | 5 | 일치 |
| 일반 지역 | 5 | 5 | 일치 |
| 레이드 | 2 | 2 | 일치 |
| 일반·정예 몬스터 | 20+5 | 25 | 일치 |
| 레이드 보스 | 2 | 2 레이드 | 일치 |
| 핵심 시설 | 8종×4레벨 | 8종 | 일치 |
| 관리 NPC | 4 | 4 | 일치 |

### 8.2 제공 검증기

```text
CONTENT VALIDATION PASSED
jobs=5, grades=5, ranks=6, regions=5, monsters=27, materials=53, equipment=80
```

검증 범위:

- 주요 ID 고유성
- 직업·스킬·랭크·지역·레이드 참조
- 시설·제작·강화·제련 재료 참조
- 모집 Pool 확률 합계
- JSON 파싱

### 8.3 추가 검증

- SHA-256 110개: 통과
- ZIP 목록 검사: 통과
- CSV BOM·열 구조: 통과
- Save JSON Schema Draft 2020-12 compile: 통과
- Content Manifest JSON Schema Draft 2020-12 compile: 통과
- OpenAPI Redocly 권장 규칙: **14 errors, 9 warnings**

OpenAPI 오류는 7개 operation의 `summary` 부재와 7개 operation의 security 정의 부재다. 경고는 license, localhost server, 각 operation의 4xx 응답 부재다. YAML/OpenAPI 구조는 읽히지만 구현 계약으로 사용하기에는 부족하다.

### 8.4 데이터 계약 공백

다음은 실제 오류라고 단정하지 않았지만, validator가 판단할 수 없는 다형 필드다.

| 파일·필드 | 현재 값 예 | 문서화가 필요한 규칙 |
|---|---|---|
| `asset_register.status` | `TEMPLATE` | 상태 표기 예외 허용 여부 |
| `materials.source_region` | `REGION_*`, `RAID_*`, `DISMANTLE`, `PROMOTION_CONTENT` | 지역 ID와 source category의 합성 규칙 |
| `equipment_templates.boss_id` | `RAID_HYDRA`, `RAID_DRAGON` | monster ID가 아니라 raid ID를 저장하는 이유 또는 필드명 수정 |
| `facilities.assigned_npc_profession` | `NPC_*`, `SYSTEM_*`, `NONE` | NPC ID와 sentinel 허용 집합 |
| `recruitment_pity` | rate-up 행의 빈 `guaranteed_grade` | 직업 보장 상태와 reset 조건 표현 방식 |
| `loot_entries.reward_id` | material ID 또는 숫자 Tier | `reward_type`별 tagged-union 규칙 |

P01에서 파일을 편입할 수는 있지만, P03 콘텐츠 importer 전에 field domain과 참조 규칙을 상세 설계해야 한다.

## 9. Save·API 계약 기준선

### 9.1 Save

`save.schema.json`은 최상위 필수 키와 `additionalProperties: false`를 정의한다. 그러나 `profile`, `kingdom`, `inventory`, `regions`, 각 mercenary/facility 원소는 빈 object 구조다.

다음 구현 계약이 없다.

- entity별 필드·ID·수치 타입
- dictionary와 array 선택 기준
- 정수 범위와 음수 허용 여부
- saveVersion별 Migration 예시
- 원자 저장·3개 백업의 파일명과 복구 순서
- checksum 포함 범위와 계산 순서
- contentVersion 불일치 처리
- 오프라인 정산 중 중단·재시도 규칙

따라서 P03 저장 구현 전에 상세 Save 설계서가 필요하다.

### 9.2 API

OpenAPI 초안에는 6개 경로와 7개 operation이 있으나 response schema와 공통 component가 없다.

다음 구현 계약이 없다.

- 개발용 session 발급·인증 방식
- 인증 제외 endpoint
- 표준 Error Code와 error body
- `traceId` 전달 위치와 생성 주체
- Wallet·Recruitment·Reward·Cloud Save DTO
- `Idempotency-Key` 재사용·충돌·만료 응답
- 낙관적 잠금과 version conflict 응답
- Cloud Save payload 크기·압축·버전 규칙

운영 로그인·결제는 OPS_LATER로 유지하되, P16 개발 서버용 계약은 별도로 필요하다.

## 10. 명세 차이와 충돌

| ID | 차이 | 영향 | 판정 |
|---|---|---|---|
| GAP-01 | 설계 패키지가 Git에 없음 | 새 clone에서 구현 계약 소실 | P01 필수 |
| GAP-02 | validator가 Git에 없음 | Hook이 깨끗한 clone에서 실패 | P01 필수 |
| GAP-03 | 서버 후보가 P16보다 앞서 존재 | Phase 순서·검증 이력 불명 | 채택 결정 필요 |
| GAP-04 | 기본 JDK 17, JDK 25는 미추적 `.tools/`에만 존재 | 깨끗한 환경에서 서버 테스트 재현 불가 | P16 전 설치 절차 필수 |
| GAP-05 | Unity 6.4만 설치, 프로젝트 없음 | Unity 6.3 고정 불가 | P02 전 필수 |
| GAP-06 | Git ignore/attributes/LFS 불완전 | Unity·대용량·줄바꿈 위험 | P01 필수 |
| GAP-07 | 서버 통합 테스트는 통과했지만 Controller 없음 | P16 API 완료 조건 미충족 | 서버 채택 시 보완 |
| GAP-08 | Save schema가 얕음 | P03 구현을 추측하게 됨 | 추가 설계 필요 |
| GAP-09 | API security/error/schema 없음 | P16 구현을 추측하게 됨 | 추가 설계 필요 |
| GAP-10 | 일부 CSV field domain 불명 | importer·validator 분기 불명 | 추가 설계 필요 |

## 11. 기술 위험

### 높음

1. 미추적 설계·서버 파일은 삭제, PC 장애, 잘못된 정리로 유실될 수 있다.
2. 서버 후보를 바로 커밋하면 P01~P15 검증 순서를 건너뛰고 대규모 미검증 변경을 도입한다.
3. validator 없는 Hook은 새 개발자의 첫 커밋부터 막는다.
4. Save·API의 얕은 계약은 구현 후 대규모 Migration과 DTO 재작성을 유발할 수 있다.

### 중간

1. `core.autocrlf=true`와 제한적인 `.gitattributes` 조합이 Unity YAML·CSV diff를 오염시킬 수 있다.
2. Git LFS 정책이 없어 원본 에셋을 일반 Git에 잘못 넣을 수 있다.
3. OpenAPI가 구현체와 연결되지 않아 service 코드와 외부 계약이 어긋날 수 있다.
4. Android 목표 기기가 미정이라 성능 완료 기준을 재현하기 어렵다.

### 낮음 또는 후속

- Built-in과 URP 2D 선택은 P02 버티컬 슬라이스 측정으로 결정 가능하다.
- 운영 배포·로그인·결제·클라우드 운영은 OPS_LATER이며 현재 구현하지 않는다.

## 12. UNRESOLVED와 추가 상세 설계 요청

### UR-01: 미추적 서버 후보의 지위

결정 필요:

- P16 선행 구현 후보로 보존·감사할지
- 참고 코드로만 보존하고 P16 계약에 맞춰 점진적으로 채택할지
- 별도 실험 결과로 격리할지

요청 설계 내용:

- `TYCOON_DB_INTERFACE_DESIGN_v1.0.md` 원문과 저장소 내 권위 경로
- 기존 P00~P17 로드맵보다 DB P0를 우선한 변경 승인 기록
- 코드 생성·작성 배경과 신뢰 가능한 기준
- 채택 대상 package·Migration 목록
- 기존 Phase 순서와 병합 전략
- Flyway 번호 공백의 의도
- 검증·롤백·호환성 전략

### UR-02: Save 상세 계약

P03 전에 다음을 포함한 상세 설계서가 필요하다.

- 전체 Save JSON 예시
- object별 field/type/required/default/range
- stable ID와 runtime ID 규칙
- `saveVersion` Migration 표
- `contentVersion` 호환 정책
- checksum·백업·원자 교체·복구 순서
- 오프라인 정산 재진입과 중복 방지
- 손상·부분 저장·구버전 테스트 사례

### UR-03: 콘텐츠 field domain과 sentinel

P03 importer 전에 다음을 포함한 상세 설계서가 필요하다.

- CSV별 primary/composite key
- field별 enum과 nullable 규칙
- tagged-union 필드의 discriminator 규칙
- `NONE`, `SYSTEM_*`, `RAID_*`, `TEMPLATE` 허용 위치
- FK와 soft reference 구분
- rate-up pity 상태 표현
- validator가 실패해야 하는 사례

### UR-04: 개발 서버 API 계약

P16 전에 다음을 포함한 상세 설계서가 필요하다.

- 개발용 session과 인증 흐름
- 공통 success/error envelope
- `traceId`, error code, HTTP status mapping
- Wallet·Recruitment·Reward·Cloud Save DTO
- 멱등성 상태 전이와 conflict 응답
- transaction/optimistic lock 경계
- OpenAPI 예시 payload와 계약 테스트 기준

### UR-05: 테스트로 확정할 항목

다음은 지금 상세 설계로 고정하지 않고 원래 문서대로 테스트를 통해 결정할 수 있다.

- Built-in vs URP 2D
- Android 최소 사양과 목표 기기
- 강화 확률, 모집 확률·천장, 오프라인 최대 시간
- 무료 에셋 조합과 장비 내구도 비활성 유지 여부

## 13. P00 실행·검증 결과

| 검사 | 결과 |
|---|---|
| Git 상태·추적 범위 | 완료 |
| 설계 문서 34개 제목·상태 인덱싱 | 완료 |
| data/phases/prompts/bootstrap 구조 | 완료 |
| 패키지 SHA-256 110개 | 통과 |
| ZIP 목록 검사 | 통과 |
| 콘텐츠 공식 validator | 통과 |
| CSV 29개 BOM·열 구조 | 통과 |
| JSON Schema 2개 compile | 통과 |
| OpenAPI 권장 lint | 실패: 14 errors, 9 warnings |
| Gradle Wrapper 실행 | 통과: 9.2.1/JVM 17 |
| 서버 test, 기본 `JAVA_HOME` | 실패: JDK 17에서 Java 25 toolchain을 찾지 못함 |
| 서버 test, workspace-local JDK 25 명시 | 통과: 10 tests, 실패·오류·건너뜀 0 |
| Docker Compose config | 통과 |
| Unity 설정·테스트 | 미실행: 프로젝트 없음 |
| 고신뢰 비밀값 패턴 | 발견 없음 |

OpenAPI lint 실패와 기본 JDK 환경의 재현 실패는 숨기지 않는다. 서버 후보의 현재 Testcontainers 테스트는 workspace-local JDK 25에서 통과했지만, 후보 구현과 도구 설치가 Git에 채택되지 않아 깨끗한 clone 검증은 아직 완료되지 않았다.

## 14. 데이터·Save·API·성능 영향

- 데이터 변경: 없음
- Save 변경: 없음
- API 변경: 없음
- DB Migration 변경·실행: 없음
- 애플리케이션 소스 변경: 없음
- 런타임 성능 영향: 없음
- 화면 변경·캡처: 없음

## 15. P01 실행 제안

### 수정 예상 파일

```text
AGENTS.md
README.md
.editorconfig
.gitignore
.gitattributes
.githooks/*
.github/workflows/*
docs/**
data/**
phases/**
prompts/**
scripts/validate_content.py
scripts/setup-git-hooks.ps1
```

P01에서는 `server-api/`, Gradle Wrapper, `infra/`, Unity 프로젝트를 같은 커밋에 넣지 않는다.

### 검증 명령

```text
python scripts/validate_content.py
bash -n .githooks/pre-commit
bash -n .githooks/pre-push
git check-ignore -v <forbidden-path-samples>
git check-attr -a -- <text-and-asset-samples>
git diff --check
```

### 완료 조건

- 깨끗한 clone에서 Hook 설치·콘텐츠 검증 가능
- Git·콘텐츠 CI 성공
- Unity·서버 모듈이 없어도 `SKIPPED` 사유가 명확함
- 설계 원본과 파생 문서가 구분됨
- 미추적 서버 후보를 실수로 포함하지 않음

### 롤백

- P01을 저장소 기반, 설계·데이터 편입, CI 조정의 작은 커밋으로 분리한다.
- 각 커밋은 `git revert`로 독립 롤백 가능해야 한다.
- 서버·DB·Save 변경은 P01에 포함하지 않으므로 데이터 역방향 Migration은 필요 없다.

## 16. 다음 승인 지점

P01 시작 전 다음 두 가지를 승인받아야 한다.

1. 설계 패키지를 중첩 폴더째 커밋하지 않고, 권장 루트 구조로 선별 편입한다.
2. 미추적 서버·Gradle·infra 후보는 보존하되 P01에서 제외하고, UR-01 결정 후 별도 이슈로 감사한다.

Save·콘텐츠·API 상세 설계서는 각각 P03/P16 이전에 필요하다. 전달받기 전에는 해당 구현을 추측해 시작하지 않는다.

## 17. 분석 중 동시 변경 보정

첫 P00 커밋 직후인 2026-07-17 10:24 KST에 이 작업이 생성하지 않은 다음 변경이 작업 트리에 나타났다.

```text
README.md 수정
docs/P0_DB_IMPLEMENTATION_REPORT.md 추가
server-api/src/test/** 추가
server-api/build.gradle 수정
server-api 소스·테스트 결과 갱신
```

이 변경들은 별도 작업 흐름의 결과로 간주해 수정·stage·commit하지 않았다. P00 보고서는 읽기 전용으로 다시 확인한 최신 증거만 반영했다.

추가 확인 결과:

- `.tools/jdk25-extracted/jdk-25.0.3+9`가 존재한다.
- 해당 JDK를 명시하면 서버 Testcontainers 테스트 10개가 통과한다.
- 새 DB 보고서는 로컬 PostgreSQL 18.4와 Flyway 17개 성공을 주장한다.
- 실제 source migration 파일은 versioned 14개와 repeatable 2개로 16개이므로, DB 보고서의 `17개` 산정 기준을 확인해야 한다.
- DB 보고서의 기준 문서 `TYCOON_DB_INTERFACE_DESIGN_v1.0.md`는 현재 workspace와 설계 패키지에서 찾을 수 없다.
- DB 보고서는 기존 P16 순서보다 DB P0를 우선한 최신 요청이 있었다고 기록하지만, 그 변경 통제 결정과 원문 설계가 현재 Git에 없다.

따라서 UR-01은 단순 코드 채택 문제가 아니라 **설계 권위와 Phase 순서 변경 승인 문제**다. `TYCOON_DB_INTERFACE_DESIGN_v1.0.md` 원문, 기존 핸드오프 문서와의 우선순위, DB P0를 공식 선행 단계로 채택할지에 대한 상세 설계·결정 기록을 전달받기 전에는 이 서버를 기준 구현으로 확정하지 않는다.
