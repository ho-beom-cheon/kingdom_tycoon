# Git·Git Hook·CI/CD

## 구분

- Git Hook: 개발자 PC에서 빠른 검사
- GitHub Actions: 반복 가능한 CI
- 운영 CD: OPS_LATER

## 브랜치

- main 보호
- `codex/issue-<number>-<slug>`
- PR 병합
- 작은 커밋

## Unity

- Visible Meta Files
- Force Text
- `.meta` 필수
- Library/Temp/Logs/Build 제외

## Git Hook

pre-commit:

- 금지 파일
- 비밀값
- Unity Asset `.meta`
- CSV/JSON 검증

pre-push:

- 서버 테스트
- 콘텐츠 검증
- 가능한 경우 Unity EditMode 테스트

## GitHub Actions

PR:

- 콘텐츠 검증
- 서버 clean test
- Flyway/Testcontainers
- Unity 테스트(라이선스 또는 self-hosted runner 준비 후)

main/tag:

- Android Development build artifact
- Server JAR/Docker build validation
- 운영 자동 배포는 하지 않음
