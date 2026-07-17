# Codex 최초 실행 프롬프트

저장소의 `AGENTS.md`, `README.md`, `docs/00_MASTER_INDEX.md`, `docs/02_DECISION_REGISTER.md`, `docs/04_V1_SCOPE.md`를 먼저 읽어라. 이후 `docs/` 전체의 제목과 상태를 인덱싱하고, `data/`, `phases/`, `prompts/`, `scripts/`, `.github/` 구조를 확인하라.

이번 작업에서는 코드를 수정하지 않는다.

## 수행할 일

1. 현재 저장소 구조와 이 설계 패키지의 권장 구조를 비교한다.
2. Unity 프로젝트 존재 여부, 버전, 패키지, 씬, Assembly Definition, 테스트 상태를 확인한다.
3. 서버 모듈 존재 여부, Java/Spring/Gradle/PostgreSQL/Flyway 상태를 확인한다.
4. Git의 Visible Meta Files, Force Text, `.gitignore`, `.gitattributes`, Git LFS 정책을 점검한다.
5. `data/csv` 참조 무결성과 스키마를 검증한다.
6. 명세 간 충돌, 누락, 구현 위험을 분류한다.
7. `phases/P00_BASELINE_REVIEW.md`에 따라 P00 실행 계획을 작성한다.
8. 실행 계획에는 수정 예상 파일, 명령, 테스트, 완료 조건, 롤백을 포함한다.
9. 명세에 없는 기능은 제안할 수 있으나 구현 대상으로 넣지 않는다.
10. 결과를 `docs/reports/P00_BASELINE_REPORT.md`로 작성할 계획을 제시한다.

## 응답 형식

- 저장소 현황
- 명세 적합도
- 차이와 충돌
- 기술 위험
- 데이터 검증 결과
- P00 계획
- UNRESOLVED
- 다음 승인 지점
