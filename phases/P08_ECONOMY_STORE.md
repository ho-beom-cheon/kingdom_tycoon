# P08_ECONOMY_STORE — 경제·상점

> 상태: **완료** (`docs/reports/P08_ECONOMY_STORE_REPORT.md`, GitHub Issue #28)

## 목적

개인·왕국 골드, 판매·구매, 가격 정책을 구현한다.

## 선행 조건

- P07_LOOT_INVENTORY_EQUIPMENT

## 구현 범위

- 개인 골드·왕국 금고 Wallets
- 가격 정책, 상점 재고, 판매·구매·보급 transactions
- 사냥 귀환 후 sell→potion→equipment store autonomy
- 거래 ledger·journal·idempotency와 P07→P08 Save migration
- Kingdom 상점 화면, 6개 상태, Addressables, Android 개발 빌드

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

- [x] 경제 루프 정지·음수 없음
- [x] 전체 Unity EditMode 80/80, PlayMode 22 passed/1 skipped/0 failed
- [x] Java 25 + PostgreSQL 18 server integration 10/10
- [x] 고정 Unity 6000.3.20f1 Android IL2CPP ARM64 APK 생성
- [x] 다음 Phase P09 진입 가능 여부와 선행 설계 검토 조건 명시

## 롤백

- Phase 단위 커밋을 되돌릴 수 있어야 한다.
- DB/Save 변경은 역방향 또는 호환 Migration 전략을 기록한다.
