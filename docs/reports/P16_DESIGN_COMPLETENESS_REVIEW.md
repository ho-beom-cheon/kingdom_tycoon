# P16 설계 완결성 검토

> 검토 대상: `docs/design/TYCOON_P16_SERVER_SKELETON_COMPLETE_DESIGN_v1.0.md`
> 기준 이슈: #45
> 결과: 구현 가능 (`PASS`)

## 검토 결과

| 항목 | 결과 | 근거 |
|---|---|---|
| 범위 경계 | PASS | 서버 권한 3개 API와 Unity Adapter로 제한 |
| 기존 구현 충돌 | PASS | V001~V017 수정 없이 기존 서비스·테이블 재사용 |
| API 입출력 | PASS | 경로, 헤더, success/error envelope, 식별자 정의 |
| 인증 | PASS | 개발 전용 세션, 해시 저장, 만료·비활성 조건 정의 |
| 트랜잭션 | PASS | 멱등→지갑→천장→결과→outbox 순서와 롤백 정의 |
| Unity 교체 경계 | PASS | Mock/HTTP 선택, 자동 fallback 금지, 오류 매핑 정의 |
| Save/데이터 호환 | PASS | schema·content version 변경 없음 |
| 테스트 가능성 | PASS | Testcontainers/API/Unity 계약·회귀 기준 정의 |
| 롤백 | PASS | migration 없음, Adapter/REST 계층 독립 롤백 가능 |
| 보안·운영 경계 | PASS | 개발 세션 운영 비활성, secret/원문 저장 금지 |

## 해소된 차이

- 문서의 `device_session` 표현은 기존 `account_identity`와 `refresh_token`으로 구현한다.
- 서버 DB에 없는 표시용 용병 속성은 서버 권위 결과 UUID에서 결정론적으로 파생하며 판정값으로 사용하지 않는다.
- P16은 서버 미실행 상태의 게임 실행성을 보존하기 위해 Mock을 기본으로 두되, SERVER 선택 후 실패할 때 로컬 결과로 대체하지 않는다.

## 미해결 항목

구현을 막는 `UNRESOLVED`는 없다. 실제 로그인 사업자, 운영 token 형식, 배포 인프라는 문서대로 `OPS_LATER`다.

## 구현 승인 결론

설계는 저장소 현황, DB 불변성, API 경계, Unity 교체 방식, 실패·재시도 계약, 검증·롤백까지 닫혀 있다. 이슈 #45 구현을 진행한다.
