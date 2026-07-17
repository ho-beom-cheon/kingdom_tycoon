# 운영 단계 후속 처리

## 방침

게임 1.0 개발과 로컬 검증을 먼저 완료하고, 실제 운영 준비는 이후 하나씩 결정한다. 아래 항목은 삭제하지 않고 운영 백로그로 보존한다.

## OPS_LATER 목록

- 클라우드 공급자
- 서버 리전
- dev/stage/prod
- 운영 PostgreSQL
- Object Storage·CDN
- 도메인·HTTPS
- 로그인 방식
- 계정 복구
- 클라우드 세이브 충돌 정책
- Google Play 결제 검증
- 프리미엄 재화 상품
- 확률 공시
- 개인정보 처리방침
- 이용약관·환불
- 분석·크래시 수집
- 로그·모니터링·알림
- DB 백업·복구
- 배포·롤백
- 고객 지원
- Android 최소 지원 버전
- 스토어 등록·심사

## 개발 중 확보할 연결 지점

- `IAuthService`
- `ISaveRepository`
- `IRecruitmentService`
- `IWalletService`
- `IPurchaseVerificationService`
- `IAnalyticsService`
- `ICrashReporter`
- 환경별 Server URL
- Mock/Real Adapter 분리
- API `/api/v1`
- Flyway Migration
- Save Versioning

## 운영 전 게이트

실제 유료 모집을 켜기 전에:

- 서버 권한 검증
- 결제 영수증 검증
- 재화 원장
- 천장 복구
- 확률 공시
- 환불·중복 결제 처리
- 백업·장애 복구 훈련
