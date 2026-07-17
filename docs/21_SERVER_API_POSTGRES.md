# 서버 API·PostgreSQL

## 개발 스택

- Java 25 LTS, Temurin/OpenJDK 권장
- Spring Boot 4.1.x
- Gradle Wrapper
- PostgreSQL 18.x
- Flyway
- Spring Validation
- Spring Data JPA 또는 JDBC 선택
- Spring Boot Actuator
- OpenAPI
- JUnit 5 + Testcontainers

## 1.0 개발 서버 범위

- Health
- 개발용 세션
- Wallet
- Special Recruitment
- Recruitment Pity
- Reward Claim
- Cloud Save 계약 초안

일반 사냥 시뮬레이션은 Unity 로컬 도메인에서 수행한다.

## 권한

서버 권한:

- 무료·유료 프리미엄 재화
- 특별 모집 결과
- 천장
- 보상 중복
- 결제 반영

로컬 허용:

- 일반 사냥
- 시설 생산 시뮬레이션
- UI 설정
- 개발용 오프라인 정산

## PostgreSQL

기본 테이블:

- account
- device_session
- wallet
- wallet_transaction
- recruitment_history
- recruitment_pity
- reward_claim
- cloud_save
- content_version
- audit_event

## API 공통

- Prefix `/api/v1`
- JSON
- UTC
- ISO-8601
- `traceId`
- 표준 Error Code
- `Idempotency-Key`
- Optimistic Lock 또는 명시적 Transaction

## 배포

운영 플랫폼과 인프라는 `OPS_LATER`. 개발에서는 Docker Compose PostgreSQL과 로컬 Spring Boot를 사용한다.
