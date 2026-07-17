# P01_REPOSITORY_FOUNDATION — 저장소 기반

## 목적

모노레포 폴더, Git 정책, 검증 스크립트, 기본 CI를 구축한다.

## 선행 조건

- P00_BASELINE_REVIEW

## 구현 범위

- 폴더 구조
-  hooks
-  content validator
-  CI skeleton

## 제외 범위

- 다음 Phase의 기능
- DEFERRED·OPS_LATER
- 명세 밖 대규모 리팩터링

## 작업 절차

1. 관련 문서를 읽는다.
2. 저장소 현황과 차이를 기록한다.
3. 작은 변경 단위로 구현한다.
4. 자동 테스트를 추가·실행한다.
5. 수동 검증과 화면 캡처를 남긴다.
6. Phase 보고서를 작성한다.

## 필수 보고

- 수정 파일
- 실행·테스트 명령
- 데이터·Save/API 영향
- 성능 영향
- 남은 위험

## 완료 조건

- Git·콘텐츠 검증 통과
- 테스트 실패 0
- 다음 Phase 진입 가능 여부 명시

## 롤백

- Phase 단위 커밋을 되돌릴 수 있어야 한다.
- DB/Save 변경은 역방향 또는 호환 Migration 전략을 기록한다.
