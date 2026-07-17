# P17_BALANCE_OPTIMIZE_RC — 밸런스·최적화·RC

## 목적

밸런스, 성능, 전체 QA, Android RC 빌드를 수행한다.

## 선행 조건

- P16_SERVER_SKELETON

## 구현 범위

- Profiling
-  QA
-  license register
-  RC

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

- Definition of Done 충족
- 테스트 실패 0
- 다음 Phase 진입 가능 여부 명시

## 롤백

- Phase 단위 커밋을 되돌릴 수 있어야 한다.
- DB/Save 변경은 역방향 또는 호환 Migration 전략을 기록한다.
