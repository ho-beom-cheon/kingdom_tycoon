# P09 생산·NPC 구현 보고서

## 결과

P09 생산·NPC 단계를 `1.0.0-p09` / `1.0.0-content.7`로 구현했다. P08의 시스템 보급은 생산 소유 모드로 전환되고, 대장간·연금 공방·진료소의 큐, 재료 예약, 결정론적 tick, 상점 입고, 치료, NPC 숙련 경험치가 한 Save revision에서 처리된다.

## 구현 범위

- 최종 설계와 완결성 감사: `docs/design/TYCOON_P09_PRODUCTION_NPC_COMPLETE_DESIGN_v1.0.md`
- 78-table Content package와 3개 P09 테이블
- Save content.7 schema, registry, new-game template, P08 migration goldens
- `ProductionGameService`, RFC 8785 request hash, replay/revision 보호
- 저장 초안 기반 `IStoreStockSink`로 포션/장비 생산 입고 원자성 확보
- 재고 목표 6개, 포션 emergency floor, 시설별 자동 보충 상한
- 시설 레벨/숙련도별 큐·속도·재료 효율, XP 승급
- 부상 용병 진료소 치료
- Bootstrap 글로벌 제작 진입, 모바일 가로 생산 화면, 공통 UI 상태
- 생성 에셋 검증, 수용 캡처, Android IL2CPP ARM64 빌드 진입점
- 로컬/CI 공통 P09 게이트와 GitHub Actions 증거 업로드

## 설계 완결성 검토

| 검토 항목 | 판정 | 근거 |
|---|---|---|
| P08 공급 경계 | 완료 | migration에서 `PRODUCTION_OWNED`, 기존 재고 보존 |
| 재료/큐/출력 원자성 | 완료 | clone mutation 후 Save CAS 1회 |
| 결정성 | 완료 | 정수 tick, 결정론적 job/equipment UUID, 벽시계 비사용 |
| 시설/NPC/레시피 책임 | 완료 | 독립 content rules와 자격 검증 |
| 자동화 안전장치 | 완료 | 목표/큐 합산, 시설당 1개·전체 2개, 중단 코드 |
| 치료 | 완료 | INJURED→IDLE_TOWN, 중복 대상 거부 |
| Save migration/recovery | 완료 | content.6/7 동시 검증, 예약 재료와 큐 동시 저장 |
| UI/접근성 | 완료 | 16:9·20:9, 64px, 텍스트 중단 사유 |
| P10 경계 | 완료 | 강화·제련·분해 미포함 |
| 미결정 | 없음 | `UNRESOLVED: NONE` |

## 검증 결과

- `python scripts/generate_p09_content.py --check`: 78 tables, package fingerprint 일치
- Unity P09 complete setup/verifier: 통과
- EditMode `P09ProductionTests`: 7/7 통과
- PlayMode `P09ProductionScreenTests`: 3/3 통과
- 전체 EditMode 회귀: 88/88 통과
- 전체 PlayMode 회귀: 27/27 통과, 기존 P05 비배치 캡처 1개 의도적 제외, 실패 0
- Acceptance captures: 6개, 1920×1080/2400×1080 렌더 확인
- repository CI portable gate: 통과. P03~P09 콘텐츠/Save 계약, PostgreSQL 통합 테스트 10개, Unity 프로젝트 정책 검증 완료
- Android: `P09AndroidBuilder.Build` 진입점 구현. 현재 고정 Unity 6000.3.20f1 설치에는 Android 모듈이 없어 로컬 APK 생성은 실행하지 못한다.

## 실행 방법

Unity 6000.3.20f1에서 `client-unity`를 열고 Play를 누른다. 하단 `제작` 버튼으로 P09 화면을 열 수 있다. 시설을 건설하고 맞는 NPC를 배치한 뒤 사냥 재료를 확보하면 `자동 보충 1회`와 `생산 10 tick 진행`이 동작한다.

## P10 인계

P10은 `PRODUCTION` 출처 장비를 대상으로 강화·제련·분해를 추가한다. P09의 queue, stock target, reserved materials, NPC XP와 P08 ledger는 변경하지 않는다.
