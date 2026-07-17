# Kingdom Tycoon

폐허가 된 왕국을 재건하고 자율적으로 생활하는 용병들이 몬스터를 사냥해
얻은 재료로 장비를 만드는 모바일 가로형 2D 픽셀아트 타이쿤 프로젝트다.

## 현재 단계

- 완료: [P00 기준선 분석](docs/reports/P00_BASELINE_REPORT.md)
- 완료: [P01 저장소 기반](docs/reports/P01_REPOSITORY_FOUNDATION_REPORT.md)
- 완료: [DB P0 설계 채택·감사](docs/reports/P0_DB_IMPLEMENTATION_REPORT.md)
- 완료: [P02 Unity 기반](docs/reports/P02_UNITY_FOUNDATION_REPORT.md)
- 다음 준비: `P03_CONTENT_PIPELINE_SAVE` 상세 Save·CSV 계약 확정

한 Phase의 구현과 검증이 끝나기 전에는 다음 Phase로 이동하지 않는다.

## 구현 계약

문서 충돌 시 다음 순서를 적용한다.

1. [AGENTS.md](AGENTS.md)
2. [결정 등록부](docs/02_DECISION_REGISTER.md)
3. [1.0 범위](docs/04_V1_SCOPE.md)
4. 개별 시스템 문서
5. `data/`의 `TUNABLE` 초기값
6. `phases/`의 단계별 지시

전체 문서는 [마스터 인덱스](docs/00_MASTER_INDEX.md)에서 확인한다.

## 저장소 구조

```text
docs/       제품·시스템·기술 설계와 Phase 보고서
data/       CSV 원본, JSON Schema, UI token, OpenAPI 초안
phases/     P00~P17 단계별 구현 계약
prompts/    검토·구현·회귀·릴리스용 Codex 프롬프트
scripts/    콘텐츠 검증, Git Hook 설정, CI 재현 스크립트
client-unity/ Unity 6000.3.20f1 클라이언트와 EditMode·PlayMode 테스트
server-api/ Java 25·Spring Boot 4.1 서버 모듈
infra/      PostgreSQL 18.4 로컬 개발 환경
```

`client-unity/`는 P02에서 추가했다. 서버·DB 기준은
[DB 인터페이스 설계서](docs/design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md)를
따른다. 로컬의 실험 코드나 인계 ZIP을 이 구조와 중복해 커밋하지 않는다.

## 로컬 검증

Git Hook을 저장소 관리 버전으로 설정한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-git-hooks.ps1
```

콘텐츠 데이터를 직접 검증한다.

```powershell
python .\scripts\validate_content.py
```

Git Bash에서 중앙 CI 계약을 재현한다.

```bash
scripts/ci/run-ci.sh
```

자동화 동작과 Delivery 경계는 [CI/CD 운영 가이드](docs/ci-cd.md)를 참고한다.

### Unity 테스트

Unity `6000.3.20f1`에서 EditMode와 PlayMode를 각각 실행한다. `<UNITY_EDITOR>`는
해당 버전의 설치 경로로 바꾼다.

```powershell
& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics `
  -projectPath .\client-unity -runTests -testPlatform EditMode `
  -testResults .\client-unity\Logs\editmode-results.xml

& <UNITY_EDITOR>\Editor\Unity.exe -batchmode -nographics `
  -projectPath .\client-unity -runTests -testPlatform PlayMode `
  -testResults .\client-unity\Logs\playmode-results.xml
```

구현 내용과 검증 결과는 [P02 Unity 기반 구현 보고서](docs/reports/P02_UNITY_FOUNDATION_REPORT.md)에
기록한다.

### 서버 테스트

Java 25와 Docker Engine이 필요하다. Testcontainers가 빈 PostgreSQL 18.4에
Flyway를 적용한 뒤 통합 테스트를 실행한다.

```powershell
.\gradlew.bat --no-daemon :server-api:cleanTest :server-api:test
```

로컬 PostgreSQL 설정은 실제 비밀번호를 Git에 넣지 않고 예제 파일을
복사해 사용한다.

```powershell
Copy-Item .\infra\.env.example .\infra\.env.local
docker compose --env-file .\infra\.env.local -f .\infra\docker-compose.local.yml up -d
```
