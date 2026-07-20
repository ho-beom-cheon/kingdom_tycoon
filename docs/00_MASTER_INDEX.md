# 마스터 인덱스

## 제품·범위

- `01_STATUS_LEGEND.md`: 상태 표기와 변경 통제
- `02_DECISION_REGISTER.md`: 확정·보류·거부 결정
- `03_PRODUCT_VISION.md`: 게임 비전과 차별점
- `04_V1_SCOPE.md`: 1.0 포함·제외 범위
- `05_CORE_LOOP_AND_PROGRESS.md`: 전체 플레이 루프

## 게임 시스템

- `06_MERCENARY_SYSTEM.md`
- `07_CLASSES_SKILLS_COMBAT_AI.md`
- `08_GRADE_RANK_PROMOTION.md`
- `09_RECRUITMENT_GACHA.md`
- `10_KINGDOM_FACILITIES_NPCS.md`
- `11_REGIONS_MONSTERS_RAIDS.md`
- `12_ITEMS_MATERIALS_EQUIPMENT.md`
- `13_CRAFT_ENHANCE_REFINE.md`
- `14_ECONOMY_MARKET.md`

## 화면·경험

- `15_UI_UX_DESIGN_SYSTEM.md`
- `16_UI_SCREEN_FLOWS.md`
- `17_TUTORIAL_ONBOARDING.md`
- `18_SAVE_OFFLINE_PROGRESS.md`
- `19_BALANCE_BASELINE.md`

## 개발·운영

- `20_UNITY_ARCHITECTURE.md`
- `21_SERVER_API_POSTGRES.md`
- `22_GIT_CI_CD.md`
- `23_TEST_QA.md`
- `24_PERFORMANCE_TARGETS.md`
- `25_SECURITY_ANTI_CHEAT.md`
- `26_ASSET_AUDIO_LICENSE_BUDGET.md`
- `27_OPERATIONS_LATER.md`
- `28_POST_V1_BACKLOG.md`
- `29_RISK_SCOPE_REDUCTION.md`
- `30_GLOSSARY.md`
- `31_DEFINITION_OF_DONE.md`
- `32_TECH_VERSION_SOURCES.md`
- `33_OPEN_DECISIONS.md`
- `design/TYCOON_DB_INTERFACE_DESIGN_v1.0.md`: 서버 DB 스키마, 권한,
  트랜잭션, Flyway, 운영 콘텐츠 배포 상세 기준선
- `design/TYCOON_CORE_CONTINUOUS_AUTO_HUNT_CORRECTION_COMPLETE_DESIGN_v1.0.md`: 일반 사냥을 용병별 영구 배치·독립 순환으로 정정한 최종 계약
- `reviews/CORE_CONTINUOUS_AUTO_HUNT_CORRECTION_COMPLETENESS_REVIEW_v1.0.md`: 정정 설계 완결성 검토
- `design/TYCOON_CORE_AUTOMATIC_GROWTH_COMPLETE_DESIGN_v1.0.md`: 귀환 후 스킬 훈련·장비 강화·자동 복귀를 연결한 최종 계약
- `reviews/CORE_AUTOMATIC_GROWTH_COMPLETENESS_REVIEW_v1.0.md`: 자동 성장 설계 완결성 및 기존 계약 충돌 검토
- `design/TYCOON_CORE_WORLD_HUNT_ECONOMY_COMPLETE_DESIGN_v1.0.md`: 통합 월드맵·실제 전투·전리품 경제 순환 최종 계약
- `reviews/CORE_WORLD_HUNT_ECONOMY_COMPLETENESS_REVIEW_v1.0.md`: 월드 사냥 경제 설계 완결성 및 구현 준비도 검토
- `design/TYCOON_CORE_HUNT_GAME_FEEL_COMPLETE_DESIGN_v1.0.md`: 전투·보상·성장 피드백과 지역 차이를 강화하는 게임 체감 최종 계약
- `reviews/CORE_HUNT_GAME_FEEL_COMPLETENESS_REVIEW_v1.0.md`: 게임 체감 설계 완결성 및 선행 계약 불변 검토

## 구현 데이터

`data/csv/README.md`에 각 표의 목적과 권한을 기록했다. CSV 값은 초기 기준이며 `TUNABLE`이다.

## 단계별 구현

`phases/P00`부터 `P17`까지 순서대로 진행한다. 한 Phase를 완료·검증하기 전 다음 Phase로 이동하지 않는다.

### 완료 보고서

- `reports/P08_ECONOMY_STORE_REPORT.md`: P08 경제·상점·자율 거래, Save content.6, UI, Android 검증
- `design/TYCOON_P09_PRODUCTION_NPC_COMPLETE_DESIGN_v1.0.md`: P09 생산·NPC 최종 구현 계약
- `reports/P09_PRODUCTION_NPC_REPORT.md`: P09 생산 큐·재고 목표·NPC 숙련·치료, Save content.7 검증
- `reports/P16_SERVER_SKELETON_IMPLEMENTATION_EVIDENCE.md`: P16 서버 권위 API, Unity 게이트웨이, PostgreSQL 통합 검증
- `design/TYCOON_P17_BALANCE_OPTIMIZATION_RC_COMPLETE_DESIGN_v1.0.md`: P17 밸런스·최적화·한국어 UI·Android RC 최종 계약
- `reports/P17_BALANCE_OPTIMIZATION_RC_IMPLEMENTATION_EVIDENCE.md`: P17 출시 자산, 전체 QA, 무경고 Android RC 구현 증적
- `reports/CORE_CONTINUOUS_AUTO_HUNT_CORRECTION_IMPLEMENTATION_EVIDENCE.md`: 상시 자동 사냥 구현·저장 호환·UI·회귀 검증 증적
- `reports/CORE_AUTOMATIC_GROWTH_IMPLEMENTATION_EVIDENCE.md`: 귀환 후 자동 스킬·강화·복귀 구현과 전체 회귀 검증 증적
- `reports/CORE_WORLD_HUNT_ECONOMY_IMPLEMENTATION_EVIDENCE.md`: 통합 월드맵·필드 전투·전리품·상점·성장 순환 구현 증적
- `reports/CORE_HUNT_GAME_FEEL_IMPLEMENTATION_EVIDENCE.md`: 타격·보상·성장·지역 차이·접근성 피드백 구현과 자동 검증 증적
