# AGENTS.md

## 1. 역할

이 저장소는 모바일 가로형 2D 픽셀아트 판타지 왕국 재건 타이쿤 1.0을 개발한다. Codex는 설계 문서를 구현 계약으로 취급한다.

## 2. 작업 전 필수

- `docs/00_MASTER_INDEX.md`를 읽는다.
- 현재 단계 문서와 관련 시스템 문서를 읽는다.
- 저장소 상태를 확인한다.
- 기존 코드를 무시하고 재작성하지 않는다.
- 애매한 내용은 추측하지 않고 `UNRESOLVED`로 보고한다.

## 3. 범위 원칙

- `CONFIRMED`만 필수 구현한다.
- `TUNABLE` 값은 데이터 파일로 분리한다.
- `DEFERRED`, `OPS_LATER`, `REFERENCE_ONLY`를 1.0 런타임 기능으로 구현하지 않는다.
- 명세에 없는 새 기능을 임의로 추가하지 않는다.
- 펫, PvP, 길드전, 정치, 외교, 귀족, 시대 전환은 금지한다.

## 4. Unity 규칙

- Unity 6.3 LTS 계열을 사용하고 `ProjectVersion.txt`로 정확히 고정한다.
- Version Control: Visible Meta Files.
- Asset Serialization: Force Text.
- 런타임 UI는 uGUI + TextMeshPro를 기본으로 한다.
- View에서 게임 상태, 저장소, HTTP API를 직접 변경·호출하지 않는다.
- MonoBehaviour에 도메인 로직을 집중시키지 않는다.
- ScriptableObject는 런타임 참조 카탈로그로 사용하되 원본 밸런스 데이터는 CSV/JSON으로 관리한다.
- `.meta` 파일은 대응 Asset과 함께 커밋한다.
- 씬·프리팹 YAML을 일반 텍스트 생성으로 임의 조작하지 않는다.
- UI는 1920×1080 기준, 16:9~20:9 Safe Area를 지원한다.
- 모바일 최소 터치 영역은 기준 해상도 64×64 px다.

## 5. 서버 규칙

- Java 25 LTS, Spring Boot 4.1.x, Gradle Wrapper.
- Base API: `/api/v1`.
- PostgreSQL 변경은 Flyway로만 관리한다.
- 프리미엄 재화, 특별 모집, 천장, 결제, 보상 중복 방지는 서버 권한이다.
- 모든 재화 변경은 원장(transaction ledger)을 남긴다.
- 모집·보상·결제 요청은 `Idempotency-Key`를 지원한다.
- 시간은 DB에 UTC로 저장하고 ISO-8601로 응답한다.
- 비밀값을 코드·설정 기본값·로그에 남기지 않는다.

## 6. 데이터 규칙

- ID는 영문 대문자 스네이크 케이스를 사용한다.
- 밸런스 수치를 코드 상수로 흩어놓지 않는다.
- 데이터 import 전에 스키마·ID 중복·참조 무결성을 검증한다.
- Save에는 `saveVersion`, `contentVersion`을 둔다.
- 변경 시 Save Migration과 회귀 테스트를 추가한다.

## 7. Git 규칙

- 기본 브랜치: `main`.
- 작업 브랜치: `codex/issue-<번호>-<설명>`.
- 하나의 커밋은 하나의 논리적 변경 단위다.
- 빌드 산출물, Library, Temp, Logs, 비밀값을 커밋하지 않는다.
- PR에는 명세 링크, 테스트 결과, 화면 캡처, 데이터·세이브 호환성 영향을 기록한다.

## 8. 테스트 규칙

- Unity: EditMode + PlayMode.
- Server: JUnit 5 + Testcontainers PostgreSQL.
- 다음을 삭제하거나 약화해 테스트를 통과시키지 않는다.
- 테스트 실패 상태에서 다음 Phase로 이동하지 않는다.
- UI 변경은 Loading/Content/Empty/Error/Locked/Offline 상태를 확인한다.

## 9. 작업 결과 보고 형식

1. 변경 요약
2. 변경 파일
3. 실행 명령
4. 테스트 결과
5. 수동 검증
6. 명세와 차이
7. 남은 위험
8. 다음 단계 진입 가능 여부

## 10. 설계 보강 요청

- 작업 중 설계 누락·충돌·복수 해석을 발견하면 임의로 확정하지 않는다.
- 부족한 항목, 현재 문서로 결정할 수 없는 이유, 영향 Phase와 시스템을
  `UNRESOLVED`로 보고한다.
- 사용자에게 결정 질문, 선택지, 권장안, 필요한 상세 설계서 구성을 함께
  제시한다.
- 영향받지 않는 안전한 범위는 계속 진행할 수 있지만, 설계가 필요한 구현은
  사용자가 ChatGPT에서 정리한 상세 설계서를 전달할 때까지 시작하지 않는다.
