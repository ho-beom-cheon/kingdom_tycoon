# 미정 사항

## 개발 중 테스트로 확정

- Built-in vs URP 2D
- Android 최소 사양과 목표 기준 기기
- 최종 강화 확률
- 특별 모집 확률·천장 횟수
- 오프라인 최대 시간
- 일부 시설·지역 수치
- 무료 에셋 최종 조합
- 장비 내구도 도입 여부: 1.0 기본 비활성

## 운영 단계

`27_OPERATIONS_LATER.md` 참조.

## 처리 규칙

Codex는 미정 사항을 임의 확정하지 않는다. 구현에 값이 필요하면 `TUNABLE` 초기값을 사용하고 설정·데이터로 분리한다.

## 상세 설계 보강 상태

- 서버·DB 인터페이스: `docs/design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md`로
  기준선 확보. P0/P1/P2 범위는 각각 별도 검증한다.
- Unity 로컬 Save: 전체 객체 필드, `saveVersion` Migration, checksum,
  원자 저장, 3중 백업과 복구 순서가 아직 필요하다.
- 콘텐츠 CSV: 파일별 key, enum, nullable, sentinel, tagged-union과
  validator 실패 사례가 아직 필요하다.
- 서버 API: 인증, 공통 오류, DTO, 멱등성 응답 계약은 P16 전에 필요하다.
