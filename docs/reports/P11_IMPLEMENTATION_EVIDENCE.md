# P11 성장·승급 구현 및 검증 보고서

- 상태: `COMPLETE`
- 이슈: `#35`
- 브랜치: `codex/issue-35-p11-progression-promotion`
- 게임 버전: `1.0.0-p11`
- 콘텐츠 버전: `1.0.0-content.9`
- 기준 설계: `docs/design/TYCOON_P11_PROGRESSION_PROMOTION_COMPLETE_DESIGN_v1.0.md`
- 설계 판정: `FINAL`, `IMPLEMENTATION_READY: YES`, `UNRESOLVED: NONE`

## 설계 완결성 검토 결과

| 검토 영역 | 판정 | 구현 근거 |
|---|---|---|
| 전투 경험치와 레벨 성장 | 완결 | 참가 용병 정렬, 몫·나머지 분배, 다중 레벨업, 만렙 경험치 폐기 규칙을 결정론적으로 적용 |
| 승급 준비 조건 | 완결 | 레벨·누적 기여도·엘리트/보스 기록·길드 레벨·지역 개방 조건을 데이터 기반으로 평가 |
| 승급 비용과 재료 | 완결 | 등급별 올림 배율, 개인/왕국 골드, 아이템 소모를 하나의 Save 트랜잭션으로 처리 |
| 승급 심사와 복구 | 완결 | `READY → IN_REVIEW → COMPLETED`, 종료 시각 정규화, 재실행·재접속 복구 계약을 구현 |
| 안전 승급권 | 완결 | 용병별 1회 사용, 심사 시간 즉시 완료, 사용 이력 영구 보존 |
| Save·Migration | 완결 | `content.8 → content.9` 단방향 Migration, schema/registry/template/golden을 함께 생성 |
| 원자성·멱등성·감사 | 완결 | revision CAS, request hash, operation journal, 경제 ledger, progression event stream 연결 |
| UI·모바일 | 완결 | 성장 개요, 승급 조건, 심사 중, 완료 상태와 16:9·20:9 레이아웃을 실제 렌더로 검증 |
| P10/P12 경계 | 완결 | P10 장비 성장은 점수·추천 정보로만 참조하고 자동 장비 교체는 P12 이후로 제외 |

## 구현 범위

- `content.9` 84개 CSV 테이블과 Save schema/registry/new-game template/golden 생성
- 레벨 곡선 270행, 승급 심사 규칙 5행, 기록 조건 5행, 재료 조건 5행 추가
- 전투 종료와 경험치·기여도·전투 기록 갱신을 동일 Save draft에 반영
- 승급 준비 확인, 심사 시작, 시간 완료 정규화, 승급 적용, 안전 승급권 사용 구현
- 최대 200건 진행 이벤트와 경제 ledger pruning digest 연결
- 모험가 길드 성장 화면, Bootstrap 진입 버튼, 생성 자산 검증기, Android 빌드 진입점 구현
- P11 전용 Git Hook·CI 게이트와 GitHub Actions 증빙 업로드 구성

## 콘텐츠 무결성

| 항목 | 값 |
|---|---|
| 테이블 수 | `84` |
| manifest SHA-256 | `eab5d67fb59e3ab11be15f6696232e4ad9302a2516e91eebe7908a5fb45708f6` |
| Save schema SHA-256 | `7eb2ee415a4500ecfb6985ea388405050110cac06431e748e45b661730ccd1e2` |
| package fingerprint | `88983e8811f915bb46ffe88eee67ceb550dcb855a4ee1e65c6073e6ead4323fc` |

## 자동 검증 결과

| 검증 | 결과 |
|---|---|
| P11 콘텐츠 생성기 `--check` | 84/84 테이블 통과 |
| P11 EditMode 전용 테스트 | 8/8 통과 |
| P11 PlayMode 전용 테스트 | 2/2 통과 |
| 전체 Unity EditMode 회귀 | 104/104 통과 |
| 전체 Unity PlayMode 회귀 | 31 통과, 실패 0, 기존 P05 비배치 캡처 1건 의도적 제외 |
| 저장소 전체 `scripts/ci/run-ci.sh` | 통과 |
| 서버 PostgreSQL 통합 테스트 | 10/10 통과 |
| P07~P11 누적 콘텐츠·Save 게이트 | 전체 통과 |
| Unity 메타·직렬화·프로젝트 정책 | 통과 |
| `git diff --check` | 통과 |

전체 CI는 Temurin JDK `25.0.3`을 사용했다. PlayMode에서 제외된 기존 P05 1건은 비배치 Game View 캡처 전용 테스트이며 P11 기능 실패가 아니다.

## 실제 렌더 증거

| 파일 | 상태 | 크기 |
|---|---|---:|
| `docs/reports/captures/P11/p11_01_overview.png` | 성장 개요 | 123,018 bytes |
| `docs/reports/captures/P11/p11_02_review.png` | 승급 심사 중 | 125,617 bytes |
| `docs/reports/captures/P11/p11_03_completed.png` | 승급 완료 | 125,905 bytes |
| `docs/reports/captures/P11/p11_04_20x9.png` | 20:9 대응 | 133,611 bytes |

네 장 모두 Unity GPU 렌더 결과이며 100KB 최소 증빙 기준을 통과했다.

## 빌드 경계

`P11AndroidBuilder.Build`와 `P11_RUN_UNITY=1` CI 진입점은 구현했다. 현재 고정 Unity Editor에는 Android Player/SDK 모듈이 없어 APK 산출 성공을 증빙하지 않았으며, Android 모듈이 설치된 라이선스 러너에서 동일 게이트가 APK까지 검증한다.

## 다음 단계 경계

P11은 전투 보상에서 성장·승급으로 이어지는 플레이 루프까지 닫는다. P12는 자동 장비 선택/교체, 장기 오프라인 진행, 상용화 수준의 튜토리얼·밸런스·라이브 운영 UX를 별도 최종 설계로 확정한 뒤 진행해야 한다.
