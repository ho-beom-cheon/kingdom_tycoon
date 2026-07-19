# TYCOON P15 오프라인 정산·튜토리얼·UX 통합 최종 설계 v1.0

- 문서 상태: **FINAL / IMPLEMENTATION READY**
- 적용 단계: `P15_OFFLINE_TUTORIAL`
- 기준 브랜치: `codex/issue-41-p14-raids`
- 구현 이슈: `#43`
- 게임 버전: `1.0.0-p15`
- 콘텐츠 버전: `1.0.0-content.13`
- Save schema set: `11`

## 1. 목적

P14까지 연결된 왕국 운영, 자율 사냥, 생산, 경제, 성장, 모집, 레이드 계약 위에
최대 8시간의 오프라인 정산과 행동 기반 10단계 온보딩을 추가한다. 동시에 P08~P14에서
화면별로 축적한 UI를 따뜻한 다크 판타지 제품 언어로 통합해 신규 설치부터 최종
레이드까지 실제 실행 가능한 1.0 클라이언트 흐름을 닫는다.

P15 완료 후 남는 단계는 HTTP 서버 Adapter(P16), 상용화 판단·유료 에셋 승인·RC QA(P17)다.
P15는 서버 권한, 결제, 클라우드 저장, 유료 에셋을 구현하지 않는다.

## 2. 권위 문서와 우선순위

1. `AGENTS.md`
2. `docs/02_DECISION_REGISTER.md`
3. `docs/04_V1_SCOPE.md`
4. `docs/17_TUTORIAL_ONBOARDING.md`
5. `docs/18_SAVE_OFFLINE_PROGRESS.md`
6. `docs/15_UI_UX_DESIGN_SYSTEM.md`
7. P03 Save·CSV 계약과 P04~P14 FINAL 설계
8. 이 문서

충돌 시 이 문서가 P15 런타임 의미를 확정하되, 상위 문서의 `DEFERRED`, `OPS_LATER`,
`REFERENCE_ONLY`, 금지 기능을 구현 범위로 승격하지 않는다.

## 3. 설계 완결성 검토와 충돌 해소

| ID | 기존 모호성·충돌 | P15 확정 | 근거 |
|---|---|---|---|
| P15-C01 | 오프라인 최대 시간이 미정 목록에도 남아 있다. | `OFFLINE_MAX_HOURS=8`을 TUNABLE 초기값으로 사용하며 CSV 외 상수화하지 않는다. | 저장·오프라인 문서의 1.0 기준 |
| P15-C02 | 각 규칙의 `max_seconds`와 전역 상한이 중복된다. | `eligibleSeconds=min(elapsed, globalMax, rule.max_seconds)`다. | 가장 보수적인 상한 |
| P15-C03 | 시계 역행 처리와 잠금 해제 시점이 없다. | 2초 초과 역행은 보상 0, `CLOCK_ROLLBACK` 진단만 기록한다. cursor는 전진시키지 않는다. | 보상 복제 방지 |
| P15-C04 | 재시작 중 정산 중복 가능성이 있다. | cursor·endUtc·contentVersion으로 settlementId를 만들고 한 Save CAS에서 보상과 receipt를 함께 기록한다. | 원자성·멱등성 |
| P15-C05 | 시설·사냥 실시간 시뮬레이션을 8시간 그대로 재생할 수 없다. | 이벤트 재생이 아니라 정수 summary 공식을 사용한다. 최대 6개 line만 만든다. | 성능·결정성 |
| P15-C06 | 오프라인 사망과 희귀 드롭 의미가 없다. | 사망·정예·보스·최초 발견·고품질 장비·레이드 보상은 생성하지 않는다. 사냥 효율은 75%다. | 범위 축소 원칙 |
| P15-C07 | 튜토리얼 기존 current ID가 CSV ID와 다르다. | migration에서 미완료 상태를 `TUT_01_KINGDOM_OVERVIEW`로 정규화한다. | CSV 권위 |
| P15-C08 | 모든 step이 skippable인데 skip grant 의미가 없다. | 개별 skip과 전체 skip 모두 해당 단계까지의 grant를 동일 원자 트랜잭션으로 1회 지급한다. | 진행 차단 방지 |
| P15-C09 | 튜토리얼 보상과 실제 행동 보상 중복 가능성이 있다. | `grant_id` receipt가 권위이며 실제 행동 보상과 별도 line으로 지급한다. 같은 grant_id 재적용은 no-op다. | P03 tagged reward 계약 |
| P15-C10 | 첫 모집이 서버 권한 특별 모집과 혼동될 수 있다. | `TAVERN_TUTORIAL_FIRST` 로컬 고정 pool만 사용하고 특별 모집·premium·pity를 변경하지 않는다. | P13 경계 |
| P15-C11 | 튜토리얼이 실제 화면을 가릴 수 있다. | 단일 overlay, 64px 이상 target, 배경 dim 60% 이하, 닫기/재개 가능, 손가락 애니메이션 필수 아님. | UX 원칙 |
| P15-C12 | P15 전체 UX 통합 범위가 무한하다. | 신규 P15 hub와 공통 navigation의 색상·타이포·간격·상태 표현만 통합하고 기존 기능 화면 재배치는 하지 않는다. | 대규모 리팩터링 금지 |
| P15-C13 | 레이드와 모집의 오프라인 처리 경계가 없다. | 레이드 시도·보상, 일반·특별 모집, 강화·제련은 오프라인 정산 대상이 아니다. | P10·P13·P14 FINAL |
| P15-C14 | pending settlement를 claim해야 하는지 모호하다. | bootstrap에서 즉시 원자 적용하고 summary만 보존한다. 별도 claim 재화 변경은 없다. | 강제 종료 안전성 |
| P15-C15 | 20~30분을 자동 테스트로 실제 대기할 수 없다. | 10단계 행동 sequence와 목표 25분 metadata를 검증하고 QA에서는 deterministic clock으로 25분 시나리오를 재생한다. | 테스트 실행성 |

검토 결과 `UNRESOLVED=0`, 구현 차단 항목 `0`, 외부 설계 입력 필요 항목 `0`이다.

## 4. 범위

### 4.1 구현

- content `.13`과 Save content `.13` schema
- P14→P15 content migration과 신규 게임 template
- 결정론적 6종 오프라인 summary 및 원자 적용
- clock rollback, cap, 멱등 receipt, bounded history
- CSV 기반 10단계 튜토리얼 상태기계
- 개별 완료·개별 skip·전체 skip·resume·grant receipt
- 오프라인 결과, 튜토리얼 여정, 1.0 준비 상태를 보여주는 P15 hub
- Loading/Content/Empty/Error/Locked/Offline 상태 계약
- 1920×1080 및 2400×1080 수용 화면
- EditMode, PlayMode, Android Development APK, P15 CI gate

### 4.2 제외

- 자동 레이드, 특별 모집, 강화, 제련의 오프라인 실행
- 클라우드 Save, HTTP 동기화, 로그인, 결제 검증
- 운영 분석·푸시·원격 콘텐츠 배포
- 유료 에셋 구매·도입
- P08~P14 화면 구조 전체 재작성
- 실제 25분을 기다리는 자동 테스트

## 5. 버전·데이터 계약

| 항목 | P14 | P15 |
|---|---|---|
| gameVersion | `1.0.0-p14` | `1.0.0-p15` |
| contentVersion | `1.0.0-content.12` | `1.0.0-content.13` |
| Save schema set | 10 | 11 |
| CSV table count | 95 | 95 |

`.13`은 `.12`의 95개 table을 모두 포함한다. 아래 파일만 의미 변경을 허용한다.

- `runtime_config.csv`
- `offline_reward_rules.csv`
- `tutorial_steps.csv`
- `tutorial_grants.csv`
- `localizations.csv`
- `content_manifest.json`

generator는 `.12`에서 복사한 뒤 canonical ordering, LF, UTF-8 no BOM, manifest SHA-256,
Save payload/file hash를 재생성한다. `--check`는 byte-for-byte 동일성을 요구한다.

### 5.1 P15 runtime config

| config_key | type | 초기값 | 단위 | 범위 |
|---|---|---:|---|---|
| P15_OFFLINE_MIN_SECONDS | INTEGER | 60 | SECONDS | 0..3600 |
| P15_OFFLINE_MAX_SECONDS | INTEGER | 28800 | SECONDS | 0..86400 |
| P15_CLOCK_ROLLBACK_TOLERANCE_SECONDS | INTEGER | 2 | SECONDS | 0..60 |
| P15_OFFLINE_HISTORY_MAX_ENTRIES | INTEGER | 20 | COUNT | 1..100 |
| P15_TUTORIAL_TARGET_MINUTES | INTEGER | 25 | MINUTES | 20..30 |

## 6. Save content `.13`

기존 root와 payload 필드는 유지한다. `tutorial`과 `offline`만 확장하며
`additionalProperties=false`를 유지한다.

### 6.1 tutorial

```json
{
  "tutorialVersion": 1,
  "currentStepId": "TUT_01_KINGDOM_OVERVIEW",
  "completedStepIds": [],
  "grantedRewardIds": [],
  "actionReceipts": [],
  "skipped": false,
  "startedAtUtc": null,
  "lastAdvancedAtUtc": null,
  "completedAtUtc": null,
  "lastOperationId": null
}
```

`actionReceipts[]`는 `{operationId,stepId,actionType,targetId,requestHash,appliedAtUtc,result}`다.
최대 32개이며 오래된 순으로 제거한다. `result`는 `COMPLETED`, `SKIPPED`,
`REPLAYED` 중 하나다.

### 6.2 offline

```json
{
  "offlineVersion": 1,
  "accrualCursorUtc": "2026-07-22T00:00:00.000Z",
  "lastTrustedUtc": "2026-07-22T00:00:00.000Z",
  "lastSettlementId": null,
  "lastStatus": "NONE",
  "lastElapsedSeconds": 0,
  "lastEligibleSeconds": 0,
  "pendingSettlement": null,
  "history": []
}
```

`history[]`는 `{settlementId,startUtc,endUtc,elapsedSeconds,eligibleSeconds,status,lines,digest}`다.
`lines[]`는 `{type,quantity,labelTextKey}`이며 type은 CSV의 6개 settlement_type만 허용한다.
history는 `P15_OFFLINE_HISTORY_MAX_ENTRIES` 이하로 유지한다.

### 6.3 migration

P14→P15 migration은 다음 순서다.

1. 원본 deep clone
2. tutorial ID를 CSV ID로 정규화
3. 이미 완료된 step과 grant는 보존·정렬·중복 제거
4. tutorial 확장 필드 기본값 추가
5. offline cursor와 lastTrustedUtc 보존
6. offline 확장 필드와 빈 history 추가
7. `gameVersion`, `contentVersion` 갱신
8. payload/file digest 재생성은 Save repository에 위임

migration은 동일 입력에 같은 JSON을 반환하고 원본을 변경하지 않는다.

## 7. 오프라인 정산 계약

### 7.1 입력

- `operationId` UUID
- `requestHash`
- `expectedRevision`
- `nowUtc`는 command 시작 시 `ITrustedUtcClock`에서 한 번만 읽음
- `accrualCursorUtc`
- active content `.13`

### 7.2 시간 계산

```text
elapsed = floor(nowUtc - accrualCursorUtc).TotalSeconds
if elapsed < -rollbackTolerance: CLOCK_ROLLBACK
else elapsed = max(0, elapsed)
globalEligible = min(elapsed, P15_OFFLINE_MAX_SECONDS)
ruleEligible = min(globalEligible, rule.max_seconds)
effective = floor(ruleEligible * efficiency)
```

`elapsed < P15_OFFLINE_MIN_SECONDS`면 `BELOW_MINIMUM` no-op이며 cursor만 now로 전진한다.
8시간을 초과한 시간은 버리고 다음 세션으로 이월하지 않는다.

### 7.3 6종 summary

| 순서 | type | 적용 |
|---:|---|---|
| 1 | HUNT | HUNT 상태 active mercenary에 60초당 EXP 6, 개인 gold 3을 75% 효율로 지급. 300초당 R01 일반 재료 1개. 사망·정예·보스·장비 드롭 없음. |
| 2 | POTION_CONSUMPTION | HUNT 대상 1명당 유효 1800초마다 하급 회복 포션 1개, 보유량 한도 내 소비. |
| 3 | FACILITY | active 생산 queue의 currentTick을 초당 10 tick 전진. 완료 산출물은 기존 production 규칙의 고정 recipe만 허용하고 랜덤 장비는 최저 품질로 제한. |
| 4 | NPC_PROFICIENCY | working NPC에 유효 300초당 숙련 EXP 1 지급. |
| 5 | INJURY_RECOVERY | `recoverAtUtc <= nowUtc` 부상을 `RECOVERED`로 정규화. 신규 부상 없음. |
| 6 | PROMOTION_REVIEW | `REVIEWING`이고 `finishesAtUtc <= nowUtc`인 승급을 `READY`로 정규화. 승급 적용은 수동. |

모든 수량은 64-bit 정수, 음수 금지, checked arithmetic을 사용한다. inventory capacity를
넘는 재료·포션은 버리지 않고 해당 line 수량을 capacity까지 clamp한다.

### 7.4 멱등성과 원자성

`settlementId = SHA256(profileId|cursorUtc|nowUtc|contentVersion)`이다.
같은 settlementId가 last 또는 history에 있으면 `REPLAYED`로 보상 0이다.
보상, progression 정규화, cursor, lastTrustedUtc, history, operationJournal은 하나의
draft에서 변경하고 `ISaveRepository.Save(expectedRevision)` 한 번으로 커밋한다.

Save 실패 시 메모리 draft를 폐기하고 cursor도 전진하지 않는다. 다음 실행은 같은
settlementId를 다시 계산해 안전하게 재시도한다.

## 8. 튜토리얼 계약

### 8.1 상태기계

```text
NOT_STARTED -> ACTIVE -> COMPLETED
                    \-> SKIPPED
```

currentStepId는 enabled step의 order를 따른다. 선행 step 미완료, actionType 불일치,
targetId 불일치는 각각 `P15_TUTORIAL_PREREQUISITE`, `P15_TUTORIAL_ACTION_MISMATCH`,
`P15_TUTORIAL_TARGET_MISMATCH`다.

### 8.2 10단계

| order | step | 실제 행동 | 안내 대상 |
|---:|---|---|---|
| 1 | TUT_01_KINGDOM_OVERVIEW | 왕국 보기 | KINGDOM_1 |
| 2 | TUT_02_FIRST_RECRUIT | 첫 고정 모집 | TAVERN_TUTORIAL_FIRST |
| 3 | TUT_03_REGION_R01_PERMISSION | R01 출입 허가 | REGION_R01 |
| 4 | TUT_04_OBSERVE_AUTO_HUNT | 자동 사냥 관찰 | REGION_R01 |
| 5 | TUT_05_RETURN_AND_SELL | 귀환·판매 | FAC_STORE |
| 6 | TUT_06_CRAFT_BASIC_WEAPON | 대장간·기본 무기 제작 | REC_EQ_T1_WARRIOR_WEAPON |
| 7 | TUT_07_EQUIP_BASIC_WEAPON | 구매·자동 착용 관찰 | EQ_T1_WARRIOR_WEAPON |
| 8 | TUT_08_CRAFT_HEAL_POTION | 연금술·하급 포션 제작 | REC_POT_HEAL_SMALL |
| 9 | TUT_09_PROMOTE_REGULAR | 정식 승급 | RANK_REGULAR |
| 10 | TUT_10_UNLOCK_REGION_R02 | 어둠숲 개방 | REGION_R02 |

### 8.3 command와 replay

`ObserveTutorialAction(operationId,requestHash,expectedRevision,actionType,targetId)`와
`SkipTutorialStep(...)`, `SkipAllTutorial(...)`을 제공한다. 같은 operationId·같은 hash는
저장된 결과를 반환하고 다른 hash는 `P15_OPERATION_HASH_MISMATCH`다.

step 완료/skip과 해당 `tutorial_grants.csv` 모든 line, grantedRewardIds, 다음 currentStepId,
action receipt를 한 Save 트랜잭션에서 적용한다. 마지막 step은 currentStepId=null,
completedAtUtc 설정 후 상태 `COMPLETED`다.

## 9. UI/UX

### 9.1 P15 hub

하단 메뉴의 `여정` 버튼으로 열며 세 영역을 제공한다.

1. 좌측: 복귀 보고서 — 경과/적용 시간, cap, 6종 line, 시계 경고
2. 중앙: 왕국 재건 여정 — 10단계 rail, 현재 행동, 진행률, 완료/skip
3. 우측: 1.0 준비 상태 — 저장·오프라인·튜토리얼·레이드·Android 증거

주요 행동은 `계속 진행` 하나만 강조한다. 전체 skip은 보조 버튼이며 확인 문구를 표시한다.

### 9.2 시각 토큰

- background: `#081014`
- surface: `#1B2729`, elevated `#263536`
- bronze primary: `#E0B768`
- teal success/info: `#79B8AA`
- warning: `#E5A15E`
- error: `#C85C58`
- text primary: `#EEE7D6`, secondary `#9EB4AE`
- corner는 직사각형 기반, 카드 중첩 금지, 8px spacing rhythm
- 버튼 최소 64px, 본문 18px 이상, 중요 수치 26px 이상

### 9.3 상태

| 상태 | 표시 | 행동 |
|---|---|---|
| Loading | skeleton 3개와 상태 label | 입력 잠금 |
| Content | summary와 현재 step | 계속 진행/skip |
| Empty | 정산 없음, 현재 tutorial만 표시 | 여정 계속 |
| Error | 오류 code와 재시도 | 재시도/닫기 |
| Locked | 선행 행동과 진행도 | 해당 화면 이동 |
| Offline | cloud 미연결 badge, 로컬 권위 정상 | 로컬 플레이 계속 |

16:9~20:9 Safe Area에서 텍스트 잘림, 버튼 겹침, 64px 미만 target을 허용하지 않는다.
색상만으로 상태를 구분하지 않고 glyph·label을 함께 사용한다. reduce motion이면 transition 0초다.

## 10. 런타임 구조

```text
Application/OfflineTutorial
  OfflineTutorialContracts.cs
Domain/OfflineTutorial
  OfflineTutorialDomain.cs
Infrastructure/OfflineTutorial
  P14ToP15ContentMigration.cs
  OfflineTutorialInfrastructure.cs
Presentation/OfflineTutorial
  OfflineTutorialEntryButton.cs
  OfflineTutorialScreenPresenter.cs
  OfflineTutorialScreenView.cs
```

View는 DTO만 렌더링하고 Save/JObject/Service를 직접 호출하지 않는다. Presenter가 command를
구성하고 service 변경 이벤트를 받아 다시 렌더링한다. domain은 UnityEngine을 참조하지 않는다.

## 11. 성능·복구

- 오프라인 계산은 mercenary, queue, NPC, injury, promotion collection을 각각 1회 순회한다.
- 복잡도 `O(M+Q+N+I+P)`, 8시간을 tick-by-tick 재생하지 않는다.
- summary line 최대 6, history 최대 20, tutorial receipt 최대 32다.
- bootstrap P15 정산 목표 50ms 이하, UI allocation 목표 1MB 이하.
- 앱 강제 종료 시 AtomicSaveRepository의 main/tmp/bak1~3 복구 계약을 그대로 사용한다.
- checksum은 우발 손상 탐지이며 치팅 방지로 과장하지 않는다.

## 12. 테스트와 수용 기준

### 12.1 EditMode

- 0초, 59초, 60초, 1시간, 4시간, 8시간, 9시간 clamp
- 2초 허용과 3초 역행 거부
- 동일 settlement replay 보상 0
- 6종 정산 순서와 정수 수량
- 레이드·모집·강화·제련 오프라인 미실행
- P14→P15 migration deep clone·idempotency·golden
- tutorial 10단계 순서, target mismatch, 개별 skip, 전체 skip
- multi-line grant 1회와 operation hash mismatch
- schema set 11과 content `.13` validation

### 12.2 PlayMode

- P15 hub open/close
- summary Empty/Content/Error/Offline
- tutorial step 진행률과 마지막 완료
- skip 후 재시작 resume
- 1920×1080, 2400×1080 touch target와 overflow

### 12.3 수동·실행 증거

- 신규 설치 첫 화면
- 1시간 복귀 summary
- 8시간 cap summary
- tutorial active
- tutorial completed
- clock rollback error
- 20:9 화면
- Android ARM64 Development APK

PNG는 실제 Unity camera render이고 각 100KB 초과여야 한다. Android APK가 존재하고 0바이트가
아니어야 한다. P00~P15 CI, Unity EditMode/PlayMode, PostgreSQL Testcontainers 테스트 실패는 0이다.

## 13. 완료 조건

- content `.13`, Save `.13`, migration, goldens가 deterministic하다.
- 오프라인 6종 summary가 최대 8시간 범위에서 원자·멱등 적용된다.
- 10단계 tutorial이 완료·skip·resume·grant replay를 지원한다.
- P15 hub가 16:9~20:9에서 동작하고 6상태 계약을 가진다.
- 신규 설치, 업데이트, 오프라인 1/4/8시간, 시계 역행이 검증된다.
- Android Development APK와 수용 캡처가 생성된다.
- 전체 저장소 CI와 서버 통합 테스트가 통과한다.

## 14. 롤백과 다음 단계

P15 기능 커밋을 되돌리면 active content provider와 AppRoot가 P14 `.12`로 돌아간다.
이미 `.13`으로 저장된 Save를 P14 런타임이 열지 않는 것은 정상적인 forward-only 정책이다.
데이터 손실 없는 역방향 변환이 필요하면 별도 도구를 만들며 런타임 migration으로 넣지 않는다.

다음 단계 P16은 이 로컬 권위 계약 뒤에 HTTP Adapter를 추가한다. P16은 offline settlement의
로컬 결과를 premium·특별 모집·클라우드 권위로 승격하지 않으며 서버 DTO·오류·멱등 계약을
별도 FINAL 설계로 확정해야 한다.
