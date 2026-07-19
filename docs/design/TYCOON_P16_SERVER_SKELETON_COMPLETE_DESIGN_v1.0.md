# 타이쿤 P16 서버 골격 최종 설계서 v1.0

> 대상: Spring Boot 4.1 / Java 25 / PostgreSQL 18.4 / Unity 6.3 LTS
> 상태: `CONFIRMED`
> 기준 이슈: #45
> 선행 기준선: P15 완료 커밋 `5d7b57d`

## 1. 목표와 완료 정의

P16은 오프라인 게임을 온라인 게임으로 전면 전환하는 단계가 아니다. 서버 권한이 필요한 개발용 세션, 지갑 조회, 특별 모집만 `/api/v1` REST 계약으로 노출하고 Unity가 개발 Mock과 실제 HTTP 어댑터를 설정으로 교체할 수 있게 한다.

완료 조건은 다음과 같다.

1. PostgreSQL 18.4 Testcontainers에서 세션 생성, 지갑 조회, 특별 모집, 멱등 재생, 충돌, 잔액 부족을 API 경계까지 검증한다.
2. 특별 모집은 지갑 원장, 천장, 모집 결과, 용병 생성, outbox를 하나의 서버 트랜잭션으로 처리한다.
3. Unity의 `RecruitmentGameService`는 게이트웨이를 직접 고정 생성하지 않고 주입받는다.
4. HTTP 어댑터는 Bearer 개발 세션과 `Idempotency-Key`를 전송하고 표준 오류를 도메인 오류로 변환한다.
5. 서버 미설정 개발 빌드는 기존 오프라인 Mock을 명시적으로 사용한다. 네트워크 실패 시 서버 요청을 로컬에서 재실행하지 않는다.
6. 기존 Flyway V001~V017은 수정하지 않는다.

## 2. 저장소·설계 차이와 채택안

| 구분 | 현재 저장소 | 설계 요구 | P16 채택안 |
|---|---|---|---|
| DB | V001~V017, 계정·세션용 토큰·지갑·원장·모집·천장 존재 | PostgreSQL 서버 권한 | 기존 구조 재사용, 신규 migration 없음 |
| 서비스 | 계정, 지갑, 모집 Application Service 존재 | 외부 API | Web Controller와 조회 Projection 추가 |
| HTTP | Actuator만 있고 Web starter/Controller 없음 | `/api/v1` JSON API | Spring MVC와 공통 응답·예외 처리 추가 |
| 인증 | `account_identity`, `refresh_token` 구조만 존재 | 개발용 세션 | DEV_DEVICE identity + 해시된 opaque token 사용 |
| Unity | `DevelopmentRecruitmentGateway` 직접 생성 | 교체 가능한 Adapter | 생성자 주입 + HTTP Gateway + 명시적 Mock 선택 |
| 세이브 | 서버 캐시와 pending request 필드 존재 | 재연결/중복 방지 | operation UUID를 그대로 멱등 키로 사용 |

`docs/21_SERVER_API_POSTGRES.md`의 `device_session` 명칭은 물리 테이블 요구가 아니라 개발용 세션 기능명으로 해석한다. 이미 존재하는 `account_identity`와 `refresh_token`으로 같은 보안·수명 계약을 충족하므로 중복 테이블을 추가하지 않는다.

## 3. API 공통 계약

- Base path: `/api/v1`
- Content-Type: `application/json; charset=UTF-8`
- 시간: UTC ISO-8601
- 플레이어 식별자: 외부에는 UUID `playerId`만 노출
- 인증: `Authorization: Bearer <opaque-token>`
- 변경 요청: `Idempotency-Key: <UUID>` 필수
- 추적: 요청의 `X-Trace-Id`가 유효한 UUID면 유지하고, 없으면 서버가 UUID를 생성한다.
- 모든 성공 응답: `data`, `traceId`, `serverTimeUtc`
- 모든 오류 응답: `code`, `messageKo`, `traceId`, `serverTimeUtc`, 선택적 `fieldErrors`
- 내부 bigint PK, SQL, stack trace, secret은 응답하지 않는다.

표준 오류 코드는 `VALIDATION_FAILED`, `AUTH_REQUIRED`, `AUTH_INVALID`, `PLAYER_NOT_FOUND`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_CONFLICT`, `REQUEST_IN_PROGRESS`, `INSUFFICIENT_BALANCE`, `BANNER_NOT_FOUND`, `DOMAIN_CONFLICT`, `INTERNAL_ERROR`다.

## 4. 엔드포인트

### 4.1 `POST /dev/sessions`

개발 환경에서만 활성화한다.

요청:

```json
{"deviceInstallId":"local-device-uuid","locale":"ko-KR","nickname":"초기 영주"}
```

동일 `deviceInstallId`는 기존 계정을 재사용한다. 원문 device ID와 token은 저장하지 않고 SHA-256 해시만 저장한다. 새 계정에는 모든 마스터 재화 지갑 행을 0으로 생성한다.

응답 data: `playerId`, `accessToken`, `expiresAtUtc`, `contentVersion`.

### 4.2 `GET /players/me/wallet`

Bearer token으로 식별된 플레이어의 통화별 `currencyKey`, `balance`, `version`을 키 오름차순으로 반환한다.

### 4.3 `POST /players/me/recruitments/special`

헤더 `Idempotency-Key`가 필수다.

요청:

```json
{"bannerKey":"SPECIAL_STANDARD_TICKET","pullCount":1}
```

응답 data:

- `receiptId`: 모집 transaction public UUID
- `replayed`
- `contentVersion`
- `walletAfter`: 통화별 잔액
- `pityAfter`: 서버 천장 카운터
- `results`: 생성 entity UUID, template key, job key, rarity, 보장 여부

동일 키·동일 payload는 최초 결과를 재조회한다. 동일 키·다른 payload는 409 `IDEMPOTENCY_CONFLICT`다. 원자적 실패는 지갑·원장·천장·용병·결과·outbox를 모두 롤백한다.

## 5. 개발 세션 계약

- `account_identity.provider = 'DEV_DEVICE'`
- `provider_subject_hash = SHA-256(deviceInstallId)`
- `refresh_token.token_hash = SHA-256(accessToken)`
- 기본 만료는 24시간이며 환경 설정으로만 변경한다.
- 비활성/탈퇴 계정, 만료·폐기 token은 인증하지 않는다.
- `dev-session.enabled=false`가 기본 운영 안전값이다. 로컬/테스트 프로필에서만 true로 켠다.
- P16 token은 개발 전용 opaque token이며 실제 플랫폼 로그인/JWT는 `OPS_LATER`다.

## 6. Unity Adapter 계약

`IRecruitmentGateway`는 특별 모집의 서버 권한 포트다.

- `DevelopmentRecruitmentGateway`: 오프라인·테스트 전용, authority `MOCK_ONLY`.
- `HttpRecruitmentGateway`: 개발 서버 전용, authority `SERVER`.
- 선택은 `ServerApiSettings`의 `mode`, `baseUrl`, `accessToken`, `timeoutSeconds`로 한다.
- 기본값은 `MOCK_ONLY`; URL/token이 비어 있는데 SERVER를 선택하면 부팅 시 명시적 구성 오류를 낸다.
- HTTP 응답의 `receiptId`, `walletAfter`, `pityAfter`, `results`, `contentVersion`을 검증한 뒤에만 로컬 캐시와 Save를 갱신한다.
- timeout/5xx/응답 파싱 실패는 `P16_SERVER_UNAVAILABLE`; 로컬 모집으로 자동 대체하지 않는다.
- 401은 `P16_SESSION_INVALID`, 409 멱등 충돌은 `P16_IDEMPOTENCY_CONFLICT`, 잔액 부족은 기존 `P13_PREMIUM_BALANCE_INSUFFICIENT`로 매핑한다.

P16에서는 서버가 반환한 최소 용병 스냅샷을 로컬 표시 모델로 변환하되, 서버 결과 UUID·template/job/rarity를 권위값으로 유지한다. 이름·외형 seed 등 서버 스키마에 없는 표현 속성은 UUID 기반 결정론적 로컬 파생값이며 재화·천장 판정에 사용하지 않는다.

## 7. 트랜잭션·동시성·멱등성

특별 모집 트랜잭션 순서는 다음과 같다.

1. 멱등 요청을 `(scope, playerId, key)`로 선점하고 request hash를 비교한다.
2. 활성 banner와 content release를 조회한다.
3. 지갑 행을 `FOR UPDATE`로 잠근다.
4. 비용 원장 작성 후 낙관 버전으로 잔액을 변경한다.
5. 천장 행을 잠그고 확률 결과를 계산한다.
6. 용병·모집 transaction·result를 기록한다.
7. outbox와 멱등 성공 응답을 기록한다.
8. commit 후 API projection을 조회한다.

`READ_COMMITTED`를 유지하며 지갑/천장 잠금 순서를 바꾸지 않는다. 서버가 예외를 던지면 Spring transaction 전체가 롤백된다.

## 8. 검증 계약

서버 자동 검증:

- Spring context와 Flyway 적용
- 개발 세션 신규/재사용/만료/비활성
- 무인증·잘못된 token 거부
- wallet 외부 UUID 계약과 정렬
- 모집 성공, 원장/천장/결과/용병/outbox 원자성
- 동일 멱등 요청 재생, payload 충돌, 잔액 부족 롤백
- 오류 응답에 한국어 메시지·traceId·UTC 포함

Unity 자동 검증:

- 설정 누락 시 안전한 Mock 선택
- SERVER 구성 누락 시 즉시 실패
- 요청 URL/header/body 직렬화
- 성공 응답 영수증 변환
- 오류 코드 매핑과 서버 실패 시 로컬 모집 미실행
- 기존 P04~P15 EditMode/PlayMode 회귀 0건

수동 검증은 로컬 PostgreSQL·서버를 실행한 상태에서 개발 세션 → wallet → 특별 모집 → 동일 요청 재전송 순서로 수행한다.

## 9. 데이터·Save·성능·보안 영향

- DB: 신규 migration 없음. 기존 V001~V017 checksum 유지.
- Save: schema/content version 변경 없음. 기존 recruitment server cache 필드만 사용.
- API: P16에서 `/dev/sessions`, `/players/me/wallet`, `/players/me/recruitments/special` 신규 추가.
- 성능: API p95 목표는 로컬 개발 환경 300ms 이하, DB connection pool 32 이하. 네트워크 호출은 UI 중복 입력을 막는다.
- 보안: 개발 세션은 운영 비활성, token/device 원문 미저장, 민감정보 로그 금지.

## 10. 롤백

서버 REST 계층과 Unity HTTP 어댑터 커밋을 되돌리면 P15 Mock 흐름으로 복귀한다. DB migration이 없으므로 schema rollback은 없다. SERVER 모드 장애 시 운영자가 빌드 설정을 MOCK으로 바꾸는 것은 개발 빌드에서만 허용하며, 이미 전송한 요청을 다른 멱등 키로 재시도하지 않는다.

## 11. 제외 범위

- 실제 Google/Apple 로그인과 JWT
- 결제 영수증 검증
- 일반 사냥·시설 생산 서버 시뮬레이션
- 클라우드 배포, Redis, 관리자 웹
- 운영용 rate limiting/WAF/관측 플랫폼

위 항목은 `OPS_LATER`이며 P16 완료를 막지 않는다.
