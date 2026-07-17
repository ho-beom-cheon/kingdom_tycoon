# DB P0 설계 채택 및 구현 감사 보고서

- 기준일: 2026-07-17
- 관련 이슈: [#10 서버: DB 인터페이스 설계 채택과 P0 후보 감사](https://github.com/ho-beom-cheon/kingdom_tycoon/issues/10)
- 기준 문서: `docs/design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md`
- 작업 브랜치: `codex/issue-10-db-interface-p0-audit`
- 판정: **DB P0 후보 채택 가능, 전체 DB 설계 완료는 아님**

## 1. 변경 요약

사용자가 전달한 DB 인터페이스 설계서를 저장소의 서버·DB 상세 기준선으로
편입하고, 기존 로컬 P0 후보를 소스·Flyway·테스트 단위로 감사했다.

감사 결과 역할·5개 스키마·콘텐츠 release·플레이어 계정·재화 원장·보상·
모집·멱등성·outbox의 P0 구조는 설계 방향과 일치했고, Java 25와
Testcontainers PostgreSQL 18.4에서 통합 테스트 10개가 통과했다.

다음 두 결함은 P0 계약 안에서 수정했다.

1. 게시된 모집 배너와 모집 pool이 수정될 수 있던 경로를 repeatable
   trigger로 차단했다.
2. 콘텐츠 승인 checksum이 runtime config만 반영하던 문제를 수정해 현재
   P0의 직업·용병·스킬·아이템·장비·보상·모집 행까지 결정적 순서로
   포함했다.

이 작업은 DB P0 트랙을 채택하는 것이며 Unity P02를 완료 처리하거나,
DB 설계의 P1/P2 범위를 선행 구현하는 작업은 아니다.

## 2. 변경 파일

### 기준과 결정

- `docs/design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md`
- `docs/00_MASTER_INDEX.md`
- `docs/02_DECISION_REGISTER.md`
- `docs/33_OPEN_DECISIONS.md`
- `README.md`
- `docs/reports/P0_DB_IMPLEMENTATION_REPORT.md`

### 빌드와 로컬 DB

- `settings.gradle`
- `build.gradle`
- `gradlew`, `gradlew.bat`, `gradle/wrapper/`
- `infra/.env.example`
- `infra/docker-compose.local.yml`
- `infra/postgres/init/001-create-roles.sh`

### 서버

- `server-api/build.gradle`
- `server-api/src/main/java/`
- `server-api/src/main/resources/application.yml`
- `server-api/src/main/resources/db/migration/`
- `server-api/src/test/java/`

빌드 산출물, `.tools/`, 실제 `.env`, 로컬 DB volume, 인계 ZIP은 포함하지
않았다.

## 3. 실행 명령

```powershell
$env:JAVA_HOME = '<JDK 25 path>'
$env:Path = "$env:JAVA_HOME\bin;$env:Path"
java -version
docker compose -f infra/docker-compose.local.yml --env-file infra/.env.example config --quiet
.\gradlew.bat --no-daemon :server-api:cleanTest :server-api:test
python .\scripts\validate_content.py
```

```bash
scripts/ci/run-ci.sh
```

## 4. 테스트 결과

| 검사 | 결과 |
|---|---|
| Java | Temurin 25.0.3 LTS |
| PostgreSQL | Testcontainers 18.4 |
| Flyway versioned | 14개: V001~V017, 의도된 번호 공백 포함 |
| Flyway repeatable | 2개 |
| Flyway 성공 history | 17행: schema creation 1 + versioned 14 + repeatable 2 |
| 스키마 | `audit`, `billing`, `game`, `master`, `ops` |
| 테이블 | audit 5 + Flyway history 1, billing 0, game 22, master 21, ops 2 |
| JUnit/Testcontainers | 테스트 메서드 10개 통과, 실패 0 |
| 동시 재화 차감 | 20개 병렬 요청, 음수 잔액 없음 |
| 콘텐츠 게시·롤백 | 검증 오류·version 충돌 차단, 이력·감사 기록 |
| 게시 콘텐츠 불변성 | master revision, 모집 banner/pool 수정 차단 |
| 콘텐츠 checksum | P0 release-scoped 데이터 포함 확인 |

테스트 메서드는 10개지만 한 메서드에서 원자성·rollback·roster 제약처럼
여러 설계 시나리오를 함께 검증한다. 설계서 17장의 18개 항목 전체가
완료됐다는 의미는 아니다. 결제·전체 캐시 교체 등 P2 항목은 제외했다.

## 5. 수동 검증

1. 빈 PostgreSQL 18.4 Testcontainer에 실제 Flyway chain이 적용됨을
   확인했다.
2. `public` 스키마에 애플리케이션 객체가 없음을 확인했다.
3. `tycoon_owner`는 로그인 불가이고 app/ops/migrator/readonly 역할이
   분리됨을 확인했다.
4. `tycoon_app`이 append-only wallet ledger를 UPDATE할 수 없음을
   역할 전환 연결에서 확인했다.
5. 게시된 runtime config와 모집 banner/pool을 수정하면 SQLSTATE 55000
   계열 오류로 거부되는 것을 확인했다.
6. 기존 작업 폴더의 파일은 읽기·선별 복사만 했고 원본 변경을 stage하지
   않았다.

화면과 Unity 변경이 없어 화면 캡처는 해당하지 않는다.

## 6. 명세와 차이

### P0에서 채택한 범위

- PostgreSQL 18.4와 5개 업무 스키마
- owner/migrator/app/ops/readonly 역할
- content release/channel/validation/publish/rollback
- 계정·프로필·설정·save checkpoint
- 용병·장비·아이템의 P0 보유 구조
- wallet/ledger/reward grant
- summon/pity/idempotency/outbox/audit
- Repository/Application Service와 Testcontainers 검증

### 의도적으로 남긴 P1/P2

- 지역·스테이지·레이드·시설·오프라인 보상 전체 테이블
- 전체 CSV master data import/validate Gradle task
- 전체 `MasterDataProvider`와 immutable in-memory snapshot 교체
- 관리자 HTTP API와 ops 전용 datasource/transaction adapter
- billing 테이블, 실제 영수증 provider port
- Redis, 외부 outbox adapter, 운영 UI

### Flyway 번호

로컬 후보는 P0에서 필요하지 않은 설계상의 V006, V010, V014를 생성하지
않은 상태로 V017까지 적용됐다. `out-of-order=false`이고 적용된 versioned
migration을 수정하지 않는 원칙 때문에 P1부터는 빈 번호를 재사용하지 않고
V018 이후의 새 migration으로 확장한다.

## 7. 남은 위험과 영향

### 남은 위험

1. 현재 콘텐츠 관리 Service는 Testcontainers의 migration 사용자 권한으로
   검증됐다. 실제 관리자 API를 열기 전 `tycoon_ops` 전용 datasource와
   transaction 경계를 구성해야 한다.
2. P0 checksum은 현재 P0 테이블을 포함하지만 P1 콘텐츠 테이블이 추가되면
   같은 canonical material에 반드시 편입해야 한다.
3. 콘텐츠 캐시는 아직 요청 시 JDBC 조회 방식이며 원자 snapshot 교체는
   P2 범위다.
4. API Controller와 공통 오류/DTO 계약은 아직 없다.

### 영향

- DB: 신규 저장소 기준으로 V001~V017와 repeatable 2개를 편입
- Save: 서버 복구용 checkpoint만 편입, Unity 로컬 Save 계약 변경 없음
- API: Controller/외부 API 변경 없음
- 콘텐츠 CSV: 값과 validator 규칙 변경 없음
- Unity: 변경 없음
- 운영 배포: 변경 없음

### 계속 필요한 상세 설계

전달된 DB 문서는 다음 두 P03 설계를 대체하지 않는다.

- Unity 로컬 Save 전체 객체·Migration·checksum·원자 저장·3중 백업 복구
- CSV 파일별 key·enum·nullable·sentinel·tagged-union·validator 오류 사례

## 8. 다음 단계 진입 가능 여부

DB P0 후보는 저장소에 채택할 수 있다. P1은 별도 이슈에서 V018 이후
migration과 master import를 함께 구현해야 한다. DB P2는 관리자 API,
ops datasource, 캐시, 결제 경계를 별도 검증해야 한다.

프로젝트 본선의 다음 단계는 여전히 P02 Unity 기반이다. Unity 6000.3 LTS와
Android Build Support를 준비하면 P02를 시작할 수 있다. P03 시작 전에는
위 Save/CSV 상세 설계서가 반드시 필요하다.
