# 기술 버전 출처 스냅샷

기준일: 2026-07-16

프로젝트는 최신 기능보다 안정적인 고정 버전을 우선한다.

## Unity

- 선택: Unity 6.3 LTS 계열
- 이유: 제작 버전 고정과 장기 지원 우선
- 최신 일반 Supported Release가 별도로 있어도 프로젝트 중간 자동 업그레이드 금지
- 공식:
  - https://unity.com/releases/editor/archive
  - https://unity.com/releases/unity-6/support

## Spring Boot

- 선택: Spring Boot 4.1.x
- 공식 프로젝트 페이지 기준 4.1.0 확인
- https://spring.io/projects/spring-boot

## Java

- 선택: Java 25 LTS
- Temurin/OpenJDK 배포판 권장
- Oracle 페이지에서 JDK 25가 최신 LTS임을 확인
- https://www.oracle.com/java/technologies/downloads/
- https://openjdk.org/

## PostgreSQL

- 선택: PostgreSQL 18.x
- 기준 minor: 18.4
- 공식 Current 문서:
  - https://www.postgresql.org/docs/current/
  - https://www.postgresql.org/about/news/postgresql-184-1710-1614-1518-and-1423-released-3297/

## GitHub Actions

- CI 플랫폼
- https://docs.github.com/en/actions/get-started/continuous-integration

정확한 패치 버전은 프로젝트 생성일에 다시 확인하여 저장소에 고정한다.
