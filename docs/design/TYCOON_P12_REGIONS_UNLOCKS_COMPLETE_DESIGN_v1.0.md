# TYCOON P12 지역 개방·배치·사냥 정책 최종 설계 v1.0

## 0. 문서 상태

- 문서 상태: `FINAL`
- 구현 준비: `IMPLEMENTATION_READY: YES`
- 미해결 항목: `UNRESOLVED: NONE`
- 적용 게임 버전: `1.0.0-p12`
- 적용 콘텐츠 버전: `1.0.0-content.10`
- Save schema ID: `urn:tycoon:schema:save:v1:content.10`
- 선행 기준: P11 `1.0.0-content.9`
- 관련 이슈: `#37`
- 구현 브랜치: `codex/issue-37-p12-regions-unlocks`

이 문서는 P12의 유일한 구현 계약이다. 본문의 `CONFIRMED`는 그대로 구현하고,
`TUNABLE` 수치는 CSV에서만 조정한다. 문서에 없는 기능을 추가하지 않는다.

## 1. 목표와 플레이 결과

P12는 P06의 R01 전투 vertical slice를 5개 일반 지역으로 확장하고 P11의 성장 랭크를
실제 출입 자격으로 연결한다. 플레이어는 다음 흐름을 수행할 수 있어야 한다.

1. 지역 지도에서 R01~R05의 개방 상태와 다음 조건을 확인한다.
2. 왕국 진행·이전 지역 진행·정예 처치·시설·레이드 조건을 만족하면 지역이 순서대로 열린다.
3. 개방된 지역의 왕국 출입 정책을 열거나 닫는다.
4. 지역 최소 랭크를 만족하는 마을 대기 용병 1~4명을 배치한다.
5. 기존 P06 전투를 해당 지역의 실제 조우 데이터로 실행한다.
6. 승리 정산으로 방문·사냥·정예 처치·진행률을 갱신하고 다음 지역 조건을 재평가한다.

## 2. 기준 문서 충돌 해결

| ID | 충돌 | 최종 결정 | 근거 |
|---|---|---|---|
| P12-C01 | P11 인계 문구는 레이드를 P12로 표기하지만 공식 Phase는 P14다. | P12는 지역 지도·개방·출입·배치·사냥 정책만 구현한다. 레이드는 P14다. | `phases/P12_REGIONS_UNLOCKS.md`, `phases/P14_RAIDS.md` |
| P12-C02 | P11 보고서에 자동 장비와 오프라인·튜토리얼이 P12 후보로 적혀 있다. | 자동 장비 교체는 도입하지 않고 기존 추천 점수만 유지한다. 오프라인·튜토리얼은 P15다. | `phases/P15_OFFLINE_TUTORIAL.md` |
| P12-C03 | R02 기존 condition group은 아직 실행되지 않는 튜토리얼 9단계를 요구한다. | P12 runtime 권위는 신규 `region_unlock_rules.csv`다. R02는 R01 진행 100%와 KINGDOM_2로 개방한다. 기존 condition group은 P15 호환 메타데이터로 보존한다. | P12가 P15보다 먼저 실제 5지역 진행을 제공해야 함 |
| P12-C04 | `regions.csv.max_active`는 6~8이지만 P06 일반 사냥 파티는 1~4명이다. | P12 일반 사냥 배치는 1~4명을 유지한다. `max_active`는 지역 동시 활동 표시 상한이며 레이드 파티 크기로 재해석하지 않는다. | P06 명령 계약 보존 |
| P12-C05 | R05는 P14 히드라 최초 토벌이 선행 조건이다. | P12에서 R05 조건과 잠금 사유를 완성하되 P14 전에는 정상적으로 잠긴다. 개발 fixture만 조건 충족을 검증한다. | `docs/11_REGIONS_MONSTERS_RAIDS.md` |

## 3. 범위

### 3.1 포함

- 5개 지역 지도와 상태 요약
- R01~R05 결정론적 순차 개방
- 왕국 단계, 이전 지역 진행률, 정예 처치, 시설 레벨, 레이드 토벌 조건
- 지역별 용병 최소 성장 랭크
- 마을 대기 용병 1~4명 배치와 기존 P06 전투 시작
- 지역 출입 허용/중지 정책
- R02~R05 일반·정예 조우 데이터
- 승리 후 지역 진행률·사냥 횟수·정예 처치·최고 진입 랭크 갱신
- content.9 → content.10 Save Migration
- 지역 사건 이력, request hash, revision CAS, operation replay
- Loading/Content/Empty/Locked/Error/Offline 상태
- 16:9~20:9와 64px 이상 터치 영역
- EditMode·PlayMode·실제 렌더·CI·Android 빌드 진입점

### 3.2 제외

- P13 주점·특별 모집
- P14 레이드 준비·부위 전투·결과
- P15 오프라인 정산·튜토리얼 실행
- 자동 장비 교체, 장비 재련 정책 변경
- 유료 에셋, 서버 권위 전환, 운영 콘텐츠 배포
- PvP, 길드전, 영구 사망

## 4. 콘텐츠 계약

### 4.1 패키지

`1.0.0-content.9`의 84개 테이블을 byte-copy 기반으로 승계하고 다음 네 테이블을 추가한다.
`region_encounter_profiles.csv`는 R01의 5행을 보존하고 R02~R05 각 5행을 추가한다.

- 총 테이블 수: `88`
- CSV schema set version: `8`
- minimum game version: `1.0.0-p12`
- 조우 행 수: `25`(지역당 일반 4 + 정예 1)

### 4.2 `region_unlock_rules.csv`

Primary key는 `region_id`다.

| 필드 | domain | nullable | 의미 |
|---|---|---:|---|
| region_id | STABLE_ID | NO | 지역 |
| previous_region_id | STABLE_ID | YES | 직전 지역 |
| previous_progress_required | INT32 | NO | 직전 지역 진행률 |
| kingdom_stage_id | STABLE_ID | NO | 최소 왕국 단계 |
| elite_source_region_id | STABLE_ID | YES | 정예 처치 확인 지역 |
| elite_kill_required | SAFE_INT | NO | 요구 정예 처치 수 |
| facility_id | STABLE_ID | YES | 요구 시설 |
| facility_level_required | INT32 | NO | 요구 시설 레벨 |
| raid_id | STABLE_ID | YES | 요구 레이드 |
| raid_clear_required | SAFE_INT | NO | 요구 토벌 수 |
| status | STATUS | NO | `CONFIRMED` 또는 `TUNABLE` |
| enabled | BOOL | NO | 활성 여부 |

초기값은 다음과 같다.

| 지역 | 이전 지역 | 진행률 | 왕국 단계 | 정예 조건 | 시설 조건 | 레이드 조건 |
|---|---|---:|---|---|---|---|
| R01 | 없음 | 0 | KINGDOM_1 | 없음 | 없음 | 없음 |
| R02 | R01 | 100 | KINGDOM_2 | 없음 | 없음 | 없음 |
| R03 | R02 | 100 | KINGDOM_3 | R02 정예 1 | 없음 | 없음 |
| R04 | R03 | 100 | KINGDOM_3 | R03 정예 1 | FAC_ALCHEMY Lv.2 | 없음 |
| R05 | R04 | 100 | KINGDOM_4 | 없음 | 없음 | RAID_HYDRA 1회 |

모든 non-null 조건은 `AND`다. 왕국 단계는 `kingdom_stages.csv.order`로 비교한다.

### 4.3 `region_access_policy_rules.csv`

| 필드 | domain | 의미 |
|---|---|---|
| region_id | STABLE_ID | 지역 PK/FK |
| party_min | INT32 | 일반 사냥 최소 인원, 초기값 1 |
| party_max | INT32 | 일반 사냥 최대 인원, 초기값 4 |
| default_allowed_on_unlock | BOOL | 개방 시 출입 정책 기본값 |
| manual_toggle_allowed | BOOL | 플레이어 정책 변경 가능 여부 |
| require_town_safe | BOOL | 배치 시 마을 안전 상태 요구 |
| status | STATUS | 상태 |
| enabled | BOOL | 활성 여부 |

모든 지역은 `default_allowed_on_unlock=TRUE`, `manual_toggle_allowed=TRUE`,
`require_town_safe=TRUE`다. 지역 잠금 상태에서는 정책을 열 수 없다.

### 4.4 `region_progress_rules.csv`

| 지역 | 승리 기본 진행 | 정예 1마리 추가 | 최대 | 다음 지역 판정 |
|---|---:|---:|---:|---:|
| R01 | 20 | 25 | 100 | 100 |
| R02 | 12 | 20 | 100 | 100 |
| R03 | 10 | 20 | 100 | 100 |
| R04 | 8 | 18 | 100 | 100 |
| R05 | 6 | 15 | 100 | 100 |

실제 증가량은 `min(100, before + victory_progress + eliteKills * elite_bonus_progress)`다.
패배·귀환·취소는 진행률과 사냥 횟수를 증가시키지 않는다.

### 4.5 `region_hunt_policy_rules.csv`

| 필드 | 의미 |
|---|---|
| region_id | 지역 PK/FK |
| recommended_party_size | UI 권장 인원 |
| recommended_potion_quantity | UI 포션 경고 기준 |
| danger_power_ratio_bps | 파티 점수/권장 점수가 이 비율 미만이면 위험 경고 |
| inventory_reserve_slots | 출전 전 권장 빈 슬롯 |
| status, enabled | 데이터 상태 |

초기 권장 인원은 R01=2, R02=3, R03~R05=4다. 포션 권장은 2, 3, 4, 5, 6개,
위험 경고는 모두 8000bps, 빈 슬롯 권장은 4, 5, 6, 7, 8개다. 이 값은 차단 조건이 아니라 경고다.

### 4.6 조우 데이터

각 지역은 `monsters.csv.region_id`와 일치하는 일반 몬스터 4종을 weight 24,
정예 1종을 weight 4로 구성해 합계 100을 만족한다. 일반 wave는 2~5,
정예 wave는 1이다. RAID 타입 몬스터는 일반 지역 조우에 포함하지 않는다.

## 5. Save content.10 계약

### 5.1 `payload.regions`

기존 `progress`, `raids`를 보존하고 다음 필드를 추가한다.

```json
{
  "regionVersion": 1,
  "nextEventSequence": 1,
  "progress": [],
  "raids": [],
  "events": []
}
```

`events`는 최대 200건이며 다음 필드를 갖는다.

- `sequence`: 1부터 증가하는 SAFE_INT
- `eventType`: `RegionUnlocked`, `AccessPolicyChanged`, `HuntSettled`
- `regionId`: 대상 지역
- `mercenaryInstanceIds`: 정렬된 고유 용병 ID, 없으면 빈 배열
- `progressBefore`, `progressAfter`: 0~100 또는 null
- `allowedBefore`, `allowedAfter`: boolean 또는 null
- `resultCode`: stable ID
- `createdAtUtc`: UTC millisecond instant

200건 초과 시 가장 오래된 사건을 제거하되 `nextEventSequence`는 감소시키지 않는다.

### 5.2 `kingdom.regionAccessPolicies`

정확히 R01~R05 하나씩 보유하며 `regionId` 순서로 정렬한다. Migration 초기값은 다음과 같다.

- 기존 정책이 있으면 동일 값을 보존한다.
- R01 누락 시 `allowed=true`를 추가한다.
- R02~R05 누락 시 `allowed=false`를 추가한다.
- 지역이 새로 개방될 때 해당 정책을 `default_allowed_on_unlock`로 전환한다.

### 5.3 Migration

Migration은 단방향·결정론적·멱등이다.

1. content.9 schema와 checksum을 먼저 검증한다.
2. `payload.regions`에 regionVersion, nextEventSequence, events를 추가한다.
3. 5개 region progress가 모두 있는지 검증하며 기존 진행값을 변경하지 않는다.
4. 5개 access policy를 보완하고 정렬한다.
5. gameVersion/contentVersion과 integrity hash를 갱신한다.
6. 원본 document는 변경하지 않는다.

역방향 Migration은 제공하지 않는다. 롤백은 P12 커밋을 되돌리고 content.9 백업 Save를 사용한다.

## 6. 지역 개방 정규화

정규화는 R01→R05 순서로 한 번의 draft에서 실행한다.

1. 이미 개방된 지역은 다시 잠그지 않는다.
2. 잠긴 지역은 unlock rule의 모든 조건을 평가한다.
3. 조건이 충족되면 `unlocked=true`, `firstUnlockedAtUtc=now`를 기록한다.
4. access policy를 기본 허용값으로 변경한다.
5. `RegionUnlocked` 사건을 추가한다.
6. 같은 pass에서 다음 지역도 평가할 수 있다.
7. 변경이 없으면 Save revision을 증가시키지 않는다.

정규화 진입점은 bootstrap, 지역 화면 refresh, 지역 승리 정산, P14 레이드 정산,
P15 오프라인 정산 종료다. 잠금 사유는 다음 우선순위의 첫 미충족 조건을 표시한다.

`PREVIOUS_REGION → KINGDOM_STAGE → ELITE_KILL → FACILITY → RAID_CLEAR`

## 7. 출입 정책 명령

`SET_REGION_ACCESS_POLICY`는 `operationId`, `expectedRevision`, `requestHash`,
`regionId`, `allowed`를 포함한다. request hash는 requestHash를 제외한 명령의 RFC 8785
canonical JSON SHA-256이다.

처리 순서:

1. operation replay와 hash를 검사한다.
2. revision CAS를 검사한다.
3. 지역 존재·개방·수동 변경 허용 여부를 검사한다.
4. `allowed=false`일 때 해당 지역에서 활동 중인 용병이 없어야 한다.
5. 정책을 변경하고 `AccessPolicyChanged` 사건을 추가한다.
6. operation journal/result를 기록하고 revision을 1 증가시킨다.

동일 operationId+동일 hash는 최초 결과를 반환하고 상태를 재변경하지 않는다.
동일 operationId+다른 hash는 실패한다. 이미 같은 값인 정책 변경도 성공 결과를 journal에
기록하지만 사건은 하나만 추가한다.

## 8. 배치와 전투 연동

기존 `StartHuntCommand`와 P06 전투 세션을 유지한다. P12 region policy가 전투 시작 전에
다음을 추가 검증한다.

- 지역이 개방되었다.
- 왕국 access policy가 allowed다.
- 파티 인원이 정책의 1~4 범위다.
- instance ID가 고유하다.
- 모든 용병이 active다.
- 모든 용병이 마을 안전 상태이며 promotion review 중이 아니다.
- 각 용병 rank order가 `regions.csv.min_rank_id` 이상이다.

권장 전투력·포션·인벤토리 여유는 경고일 뿐 출전을 차단하지 않는다.
배치 성공 시 P06이 `TRAVEL_TO_REGION`과 `currentRegionId`를 동일하게 저장한다.

## 9. 승리 정산

P06/P07/P11의 terminal Save draft 안에서 다음 순서로 실행한다.

1. 전투·loot·경험치 결과를 계산한다.
2. P12가 대상 region progress를 갱신한다.
3. 파티 최고 rank를 `highestRankReachedId`에 반영한다.
4. `lastVisitedAtUtc`, huntCount, eliteKillCount, progressPercent를 갱신한다.
5. `HuntSettled` 사건을 추가한다.
6. 다음 지역 개방을 같은 draft에서 정규화한다.
7. P11 progression settlement와 기존 terminal journal을 기록한다.
8. 단 한 번의 Save replace로 commit한다.

따라서 지역 진행, loot, 개인 골드, 경험치 중 일부만 저장되는 상태는 허용하지 않는다.

## 10. 오류 코드

- `P12_CONTENT_INVALID`, `P12_CONTENT_VERSION_UNSUPPORTED`
- `P12_SAVE_REVISION_CONFLICT`, `P12_OPERATION_HASH_MISMATCH`
- `P12_REGION_NOT_FOUND`, `P12_REGION_LOCKED`, `P12_REGION_POLICY_CLOSED`
- `P12_POLICY_CHANGE_NOT_ALLOWED`, `P12_REGION_HAS_ACTIVE_PARTY`
- `P12_PARTY_SIZE_INVALID`, `P12_PARTY_DUPLICATE`
- `P12_MERCENARY_NOT_FOUND`, `P12_MERCENARY_NOT_TOWN_SAFE`
- `P12_MERCENARY_RANK_REQUIRED`, `P12_SETTLEMENT_INVALID`
- `P12_SAVE_WRITE_FAILED`

모든 검증 실패는 Save와 runtime session을 변경하지 않는다.

## 11. UI/UX 계약

### 11.1 stable ID

- HUD 진입 버튼: `P12_REGION_NAV_BUTTON`, 표시 `지역`
- 화면: `P12_REGION_MAP_SCREEN`
- 닫기: `P12_CLOSE`
- 정책: `P12_ACCESS_POLICY`
- 출전: `P12_DEPLOY`
- 지역 노드: `P12_REGION_R01`~`P12_REGION_R05`
- 파티 선택: `P12_PARTY_SLOT_1`~`P12_PARTY_SLOT_4`

### 11.2 첫 화면 구조

1. 상단: `탐험 지도`, 왕국 단계, content.10, 개방 지역 수
2. 중앙 상단: R01~R05를 잇는 원정 경로와 잠금/개방/활성 상태
3. 좌측: 선택 지역 환경·최소 랭크·추천 전투력·주요 드롭
4. 중앙: 진행률, 정예 기록, 다음 개방 조건, 위험도
5. 우측: 출입 정책, 추천 준비, 파티 1~4명
6. 하단: 최근 지역 사건과 단 하나의 주요 행동 `출전`

### 11.3 시각 방향

P10~P11의 다크 브론즈·청록 계열을 유지하되 지역별 환경색을 제한적으로 사용한다.

- R01 초원: 올리브
- R02 숲: 짙은 청록
- R03 광산: 황동
- R04 늪지: 독성 녹색
- R05 유적: 빙결 청색

지도 경로와 실제 지역 데이터가 첫 viewport의 주인공이어야 한다. 장식용 카드 중첩을 피하고,
경로·상태·행동의 시선 순서를 명확히 한다. 잠금은 색상만이 아니라 자물쇠 모양과 조건 문구로
표시한다.

### 11.4 상태·접근성

- Loading: 입력 차단, 지도 로딩 문구
- Content: 지도·정책·파티·출전 사용 가능
- Empty: 배치 가능한 용병 없음
- Locked: 조건과 진행 수치 표시
- Error: 오류 코드와 재시도
- Offline: 로컬 지역 관리는 가능하나 P15 전에는 시간 경과 정산 0임을 표시

기준 해상도는 1920×1080, 지원 범위는 16:9~20:9다. 모든 행동 버튼은 최소 64×64px,
본문 28px 이상, 핵심 수치 32px 이상이며 한국어/영어에서 overflow와 overlap이 없어야 한다.

## 12. 테스트와 증거

### 12.1 Content/Save

- 총 88개 테이블, 지역 조우 25행, 지역별 weight 100
- 네 신규 테이블의 PK/FK/domain/null/sentinel/status 검증
- content.9→content.10 Migration before/after/new-game golden
- schema registry content.5~content.10 보존
- 5개 progress와 5개 policy 고유성·정렬·상태 일치
- region event sequence와 최대 200건 검증

### 12.2 EditMode

- 5지역 catalog와 rank/stage order
- Migration 보존성·멱등성
- R01~R05 순차 개방과 첫 미충족 사유
- 여러 지역의 단일 pass 개방
- policy CAS/replay/hash mismatch/active party 차단
- locked/closed/rank/town-safe/party-size 배치 gate
- 승리 정산 진행률 cap, 정예 가산, 최고 rank, 다음 지역 개방
- 실패 시 Save 불변

### 12.3 PlayMode·렌더

- Bootstrap→지역 지도 진입
- 개방 경로, 잠긴 상세, 출입 중지, 파티 배치 상태
- 출전 버튼이 선택 지역을 P06 전투에 전달
- 1920×1080과 2400×1080 실제 GPU 캡처
- touch target·stable ID·문자 넘침 검사

### 12.4 CI/Android

- `scripts/ci/p12.sh`가 generator, migration, runtime/UI entrypoint, 생성 asset, 캡처를 검증한다.
- Git Hook과 `scripts/ci/run-ci.sh`, GitHub Actions에 P12 게이트를 추가한다.
- licensed runner에서 P12 EditMode/PlayMode와 Android IL2CPP ARM64 Development 빌드를 실행한다.

## 13. 성능·안전

- 지역 수는 5로 고정되어 모든 평가가 O(5)다.
- 파티 검증은 최대 4명, 사건 배열은 최대 200건이다.
- 프레임마다 Save JSON을 재파싱하지 않고 화면 refresh·명령·terminal settlement에서만 평가한다.
- RNG는 기존 P06 combat seed를 그대로 사용하며 P12는 새 랜덤 소스를 추가하지 않는다.
- View는 Save, 저장소, combat service를 직접 변경하지 않고 Presenter를 통한다.

## 14. 후속 Phase 인계

- P13은 모집으로 신규 용병을 추가하되 P12 rank/party/access gate를 우회하지 않는다.
- P14는 `RAID_HYDRA` 최초 토벌 후 P12 공개 정규화 진입점을 호출해 R05를 연다.
- P15는 일반 사냥 오프라인 정산 후 P12 승리 정산 입력 계약을 재사용한다.
- P16 서버 권위 전환은 region command의 operationId/hash/CAS 계약을 보존한다.

## 15. 설계 완결성 감사

| 검토 항목 | 판정 |
|---|---|
| 공식 Phase와 P11 인계 충돌 | 해결 |
| 5지역 콘텐츠와 실제 조우 | 확정 |
| 개방 조건·순서·재평가 | 확정 |
| 출입 정책·배치·랭크 gate | 확정 |
| 전투·loot·XP·지역 진행 원자성 | 확정 |
| Save schema·Migration·복구 | 확정 |
| CAS·replay·journal·event | 확정 |
| UI 상태·접근성·화면비 | 확정 |
| P13~P16 경계 | 확정 |
| 미해결 항목 | 없음 |

P12 구현을 막는 설계 공백은 없다. 수치는 모두 content.10의 `TUNABLE` 데이터이며 코드 상수로
숨기지 않는다.
