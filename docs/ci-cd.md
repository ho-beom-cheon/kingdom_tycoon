# CI/CD 운영 가이드

## 목적

이 저장소는 검증 위치에 따라 자동화를 세 단계로 나눈다.

| 단계 | 실행 위치 | 역할 |
|---|---|---|
| Git Hook | 개발자 PC | 커밋·푸시 전 빠른 피드백 |
| Continuous Integration | GitHub Actions | PR과 `main`에서 반복 가능한 중앙 검증 |
| Continuous Delivery | GitHub Actions | 검증된 빌드 결과와 체크섬을 다운로드 가능한 아티팩트로 생성 |

운영 서버 배포, Google Play 업로드, 클라우드 자격 증명, 데이터베이스
마이그레이션 실행은 `OPS_LATER`이며 현재 파이프라인에서 수행하지 않는다.

## Repository CI

`.github/workflows/ci.yml`은 다음 이벤트에서 실행된다.

- Pull Request 생성·업데이트
- `main` push
- 수동 `workflow_dispatch`

검사 순서는 다음과 같다.

1. 저장소 관리 Git Hook의 Bash 구문 검사
2. 콘텐츠 검증기 탐색 및 CSV/JSON 검증
3. Gradle Wrapper가 있으면 서버 `clean test`
4. Unity 프로젝트가 있으면 구조, Visible Meta Files, Force Text, `.meta` 검사

프로젝트 초기 단계에 모듈이 아직 없다면 검사를 통과한 것처럼 표현하지
않고 GitHub Job Summary에 `SKIPPED`와 사유를 기록한다.

## Release Delivery

`.github/workflows/delivery.yml`은 다음 이벤트에서 실행된다.

- `v*` 형식의 Git tag push
- GitHub Actions 화면의 수동 실행

Delivery는 CI 계약을 `clean build` 모드로 다시 실행한 뒤 다음을 수집한다.

- `server-api/build/libs/*.jar`
- `client-unity/Builds/` 아래의 기존 Unity 빌드 산출물
- `DELIVERY_MANIFEST.md`
- `SHA256SUMS.txt`

빌드 가능한 모듈이 없다면 `NO_BUILD_ARTIFACTS.txt`를 포함한다. 따라서
성공한 Workflow가 실제 게임 또는 서버 바이너리 생성을 의미한다고
오해하지 않도록 한다.

## 로컬 재현

Git Bash에서 중앙 CI와 같은 검사를 실행한다.

```bash
scripts/ci/run-ci.sh
```

Delivery를 로컬에서 확인할 때는 기존 경로를 덮어쓰지 않는 새 출력 경로를
지정한다.

```bash
RELEASE_NAME=local-check \
DELIVERY_DIR=/tmp/kingdom-tycoon-delivery-local-check \
scripts/ci/run-delivery.sh
```

## 현재 제한

- Gradle Wrapper가 Git에 포함되기 전에는 서버 테스트·빌드를 실행하지 않는다.
- Unity 프로젝트와 라이선스 실행 환경이 준비되기 전에는 정적 정책만 검사한다.
- Unity EditMode·PlayMode와 Android 빌드는 라이선스 또는 self-hosted runner
  정책을 승인한 별도 이슈에서 활성화한다.
- 실제 배포와 롤백은 인프라, 환경, 비밀값, 운영 승인 절차가 결정된 뒤 별도
  Workflow로 구현한다.
