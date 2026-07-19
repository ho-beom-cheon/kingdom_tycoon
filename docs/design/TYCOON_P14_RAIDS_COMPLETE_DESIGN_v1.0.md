# TYCOON P14 레이드 완전 설계 v1.0

- 문서 상태: `FINAL`
- 구현 준비 상태: `IMPLEMENTATION_READY = YES`
- 대상 게임 버전: `1.0.0-p14`
- 대상 콘텐츠 버전: `1.0.0-content.12`
- 대상 Save 스키마: `save.content.12`
- 기준 브랜치: `codex/issue-39-p13-recruitment`
- 관련 이슈: `#41`

## 1. 목적과 완료 플레이 루프

P14는 P06 전투, P07 보상·인벤토리, P11 성장 랭크, P12 지역 개방, P13 모집 결과를
두 개의 레이드로 연결한다. 플레이어는 다음 흐름을 실제로 수행할 수 있어야 한다.

1. 레이드 지휘소에서 역병 히드라 또는 잿빛 고룡과 난이도를 선택한다.
2. 6~8명의 마을 대기 용병을 편성하고 랭크·역할·포션 경고를 확인한다.
3. 공략 목표 부위를 선택한다.
4. 결정론적 보스 전투에서 보스 HP, 부위 내구도, 제한 시간, 파티 상태를 확인한다.
5. 성공 또는 실패 결과에서 부위 파괴, 기여도, 포션 소비, 부상을 확인한다.
6. 성공 시 부위·반복 보상과 최초 토벌 보상을 한 번만 수령한다.
7. 히드라 최초 토벌은 R05를 개방하고, 고룡 최초 토벌은 1차 엔딩과 변종 플래그를 연다.

P14 완료 후 새 게임부터 최종 레이드까지의 핵심 진행 계약은 연결되며, P15는 튜토리얼과
오프라인 정산을 이 흐름 위에 추가한다.

## 2. 기준 자료 감사와 충돌 해결

| ID | 관찰 또는 충돌 | 최종 결정 |
|---|---|---|
| P14-C01 | 공식 Phase는 2레이드만 요구하지만 기존 데이터에는 각 레이드의 NORMAL/HARD/CORRUPTED가 이미 있다. | 2종 × 3난이도를 보존한다. 공식 최초 토벌과 지역·엔딩 해금은 NORMAL 최초 성공에만 연결한다. |
| P14-C02 | `raid_parts.csv`는 부위 자체 조건만 참조해 심장 노출 선행조건과 공략 순서를 표현하지 못한다. | `raid_part_target_rules.csv`를 추가해 선행 부위, 노출 HP, 피해 공유, 표시 순서를 명시한다. |
| P14-C03 | 기존 명세에 보스 AI의 실행 순서와 수치 계약이 없다. | P06의 직업 전투 프로필을 입력으로 사용하는 결정론적 10Hz `RaidSimulationEngine`을 P14 권위로 확정한다. |
| P14-C04 | 역할 경고가 시작 차단인지 불명확하다. | 파티 수·중복·활성·랭크·마을 안전·부상은 차단한다. TANK/HEALER/RANGED 역할과 포션 부족은 경고이며 확인 후 시작 가능하다. |
| P14-C05 | 실패 시 부위 보상을 유지하는지 정의되지 않았다. | 초기 정책은 실패 시 보상 없음이다. 사용한 포션과 부상은 유지하고 시도 기록은 남긴다. 정책 변경은 이후 데이터 버전에서만 한다. |
| P14-C06 | 기존 Save는 난이도별 `firstClearRewardOperationId`를 가진다. | 공식 `first_clear_reward_group_id`는 NORMAL 최초 성공에서만 지급한다. HARD/CORRUPTED 최초 성공은 해당 난이도 기록만 만든다. |
| P14-C07 | R05는 P14 이전까지 의도적으로 잠겨 있다. | 히드라 NORMAL 성공 정산에서 P12 공개 정규화 진입점을 같은 draft에 적용해 R05를 연다. |
| P14-C08 | 신규 Save는 4명·수습 랭크라 즉시 레이드가 불가능하다. | 실제 진행 조건을 우회하지 않는다. 런타임 화면은 잠금·부족 이유를 보여주며 테스트와 캡처만 전용 fixture를 사용한다. |
| P14-C09 | 오프라인 정산 대상에 레이드는 없다. | 레이드는 명시적 플레이어 명령에서만 실행한다. P15 오프라인 계산은 레이드 시도·보상을 만들지 않는다. |
| P14-C10 | 프리미엄 재화·이벤트 보상은 서버 권위다. | P14 기본 최초 보상은 기존 REGION_UNLOCK/PROGRESSION_FLAG만 사용한다. 특별 모집권 등 서버 권위 보상은 P16 Adapter 계약으로 미룬다. |

## 3. 범위

### 3.1 포함

- 역병 히드라와 잿빛 고룡
- NORMAL/HARD/CORRUPTED 난이도 개방과 권장 전투력
- 6~8명 편성, 성장 랭크 검증, 역할·포션 경고
- 목표 부위, 선행 부위, 부위 내구도, 파괴 효과
- 보스 HP 단계 기반 결정론적 AI
- 제한 시간, 파티 HP, 자동 회복 포션, 전투 기여도
- 성공·전멸·시간 초과, 부상과 포션 소비
- 부위 보상, 반복 보상, NORMAL 최초 토벌 보상
- R05 개방, 1차 엔딩과 고룡 변종 플래그
- operation replay, request hash, Save CAS, 실패 원자성
- P13→P14 Save Migration, 신규 게임 템플릿, golden
- 준비·전투·결과 UI와 16:9~20:9 Safe Area
- EditMode·PlayMode·실제 렌더·Android·CI 증거

### 3.2 제외

- 실시간 협동, 매칭, 길드·PvP
- 수동 이동·액션 조작과 복잡한 실시간 패턴 회피
- 자동 오프라인 레이드
- 레이드 입장권·에너지·유료 재도전
- 장비 내구도와 소지품 손실
- 영구 사망
- 서버 이벤트 보상 지급과 운영 난이도 로테이션
- 유료 보스 에셋과 고급 시네마틱
- P15 튜토리얼·오프라인 정산, P16 HTTP 서버 Adapter

## 4. 콘텐츠 패키지 계약

### 4.1 버전과 테이블 수

- `contentVersion`: `1.0.0-content.12`
- `minimumGameVersion`: `1.0.0-p14`
- `csvSchemaSetVersion`: `10`
- 필수 테이블 수: `95`
- P13의 91개 테이블을 그대로 보존하고 다음 네 테이블을 추가한다.

1. `raid_unlock_rules.csv`
2. `raid_role_requirements.csv`
3. `raid_part_target_rules.csv`
4. `raid_boss_phase_rules.csv`

모든 PK·FK·enum·범위·정렬·활성 상태는 import 전에 검증한다. 모든 밸런스 수치는
`TUNABLE`, 진행 의미와 보상 종류는 `CONFIRMED`다.

### 4.2 `raid_unlock_rules.csv`

| 필드 | domain | 의미 |
|---|---|---|
| raid_id | STABLE_ID | `raids.csv` FK |
| difficulty | ENUM | NORMAL/HARD/CORRUPTED |
| kingdom_stage_id | STABLE_ID? | 최소 왕국 단계 |
| region_id | STABLE_ID? | 요구 지역 |
| region_progress_required | INT32 | 요구 진행률 0~100 |
| previous_difficulty | ENUM? | 선행 난이도 |
| previous_clear_required | SAFE_INT | 선행 성공 횟수 |
| progression_flag_id | STABLE_ID? | 추가 플래그 |
| status, enabled | STATUS, BOOL | 상태 |

초기값은 다음과 같다.

| 레이드 | 난이도 | 왕국 | 지역 진행 | 선행 성공 | 플래그 |
|---|---|---|---|---|---|
| HYDRA | NORMAL | KINGDOM_4 | R04 100% | 없음 | 없음 |
| HYDRA | HARD | KINGDOM_4 | R04 100% | NORMAL 1 | 없음 |
| HYDRA | CORRUPTED | KINGDOM_5 | R04 100% | HARD 3 | FLAG_RAID_DRAGON_VARIANTS |
| DRAGON | NORMAL | KINGDOM_5 | R05 100% | 없음 | 없음 |
| DRAGON | HARD | KINGDOM_5 | R05 100% | NORMAL 1 | 없음 |
| DRAGON | CORRUPTED | KINGDOM_5 | R05 100% | HARD 3 | FLAG_RAID_DRAGON_VARIANTS |

non-null 조건은 모두 AND다. 난이도 정렬은 NORMAL → HARD → CORRUPTED다.

### 4.3 `raid_role_requirements.csv`

| 필드 | 의미 |
|---|---|
| raid_id, requirement_no | 복합 PK |
| role | `jobs.csv.role` 값 |
| minimum_count | 권장 최소 수 |
| severity | P14에서는 `WARNING`만 허용 |
| message_text_key | 사용자 경고 문구 |

두 레이드 모두 TANK 1, HEALER 1, RANGED_DPS 또는 AOE_DPS 합계 1을 권장한다.
역할 경고는 전투를 차단하지 않으며 결과 계산에서 해당 역할의 이점이 자연스럽게 반영된다.

### 4.4 `raid_part_target_rules.csv`

| 필드 | 의미 |
|---|---|
| raid_id, part_id | `raid_parts.csv` 복합 FK/PK |
| prerequisite_part_id | 먼저 파괴해야 하는 부위, nullable |
| expose_boss_hp_bps | 공격 가능해지는 보스 HP 비율 |
| boss_damage_share_bps | 부위 피해 중 보스 HP에도 반영되는 비율 |
| target_order | UI와 결정론적 fallback 순서 |

히드라 심장은 HYDRA_BODY 파괴와 보스 HP 50% 이하가 모두 필요하다. 고룡 심장은
DRAGON_BODY 파괴와 보스 HP 40% 이하가 필요하다. 다른 부위는 즉시 공격 가능하다.

### 4.5 `raid_boss_phase_rules.csv`

| 필드 | 의미 |
|---|---|
| raid_id, phase_no | 복합 PK |
| trigger_boss_hp_bps | 해당 phase 진입 HP 상한 |
| ability_tag | 보스 행동 stable tag |
| action_interval_ticks | 행동 간격 |
| attack_multiplier_bps | 기본 공격 배율 |
| status_effect_id | 적용 상태, nullable |

trigger는 10000에서 내림차순이고 phase_no는 오름차순이다. 한 tick에 phase 전이는 한 번만
발생하며 이미 진입한 phase로 돌아가지 않는다.

초기 보스 흐름:

- 히드라: `VENOM_BITE` → 70% `POISON_BREATH` → 35% `MULTIHEAD_ENRAGE`
- 고룡: `ASH_CLAW` → 75% `FLYING_CHARGE` → 40% `ASH_STORM`

### 4.6 기존 테이블의 P14 사용

- `raids.csv`: 보스, 최소 랭크, 6~8명, 최초 보상, 제한 시간
- `raid_difficulties.csv`: 권장 전투력과 반복 보상
- `raid_parts.csv`: 내구도 비율, 파괴 보상, 행동 변화
- `raid_part_effects.csv`: 파괴 후 AI/방어/상태 전이 modifier
- `reward_groups.csv`, `reward_entries.csv`: 부위·반복·최초 보상
- `combat_job_profiles.csv`, `jobs.csv`: 파티 전투 스냅샷과 역할
- `potions.csv`: 자동 회복·저항·레이드 공격 포션
- `runtime_config.csv`: 아래 P14 상한과 시간

P14 runtime config 초기값:

| key | value |
|---|---:|
| P14_SIMULATION_TICK_HZ | 10 |
| P14_TRACE_INTERVAL_TICKS | 10 |
| P14_HISTORY_MAX_ENTRIES | 50 |
| P14_EVENT_MAX_ENTRIES | 200 |
| P14_SUCCESS_INJURY_SECONDS | 600 |
| P14_FAILURE_INJURY_SECONDS | 1800 |
| P14_MAX_TRACE_FRAMES | 360 |

## 5. 파티 준비 계약

### 5.1 시작 차단 조건

- raid/difficulty가 콘텐츠와 Save에서 개방되어야 한다.
- 파티는 중복 없는 6~8명이다.
- 모든 용병은 존재하고 `active=true`다.
- 모든 용병은 해당 레이드 최소 랭크 이상이다.
- `promotion.status != IN_REVIEW`다.
- `autonomy.state == IDLE_TOWN`, `currentRegionId == null`이다.
- `autonomy.state == INJURED`인 용병은 참여할 수 없다.
- 목표 부위가 해당 레이드에 존재한다.

### 5.2 경고

- TANK, HEALER, 원거리 공격 역할 부족
- 전체 전투력 < 권장 전투력
- 치유 포션 합계 < 파티 인원 × 2
- 히드라: 해독·독 저항 없음
- 고룡: 냉기 저항 또는 레이드 공격 포션 없음

경고는 stable code 배열로 정렬하며 사용자가 확인한 뒤 같은 명령 hash에
`warningsAccepted=true`를 포함해야 시작할 수 있다.

### 5.3 전투 스냅샷

명령 시작 시 용병 ID, 이름, job/role, grade/rank/level, 전투 프로필, 장비 참조,
포션 수량을 immutable snapshot으로 만든다. 시뮬레이션 중 다른 화면의 Save 변경은 불가능하고
최종 commit은 시작 revision CAS를 통과해야 한다.

## 6. 결정론적 레이드 시뮬레이션

### 6.1 입력과 seed

`operationId`, contentVersion, raidId, difficulty, 정렬된 party IDs, targetPartId의 RFC 8785
canonical JSON SHA-256을 seed와 requestHash 경계로 사용한다. Unity 전역 RNG, 프레임 시간,
문자열 runtime hash code를 사용하지 않는다.

현재 P14 보스 AI는 확률 추첨이 없어 동일 입력이 항상 동일 trace와 결과를 만든다. 추후
확률 패턴이 추가되어도 seed에서만 난수를 파생한다.

### 6.2 tick 순서

1 tick은 100ms다.

1. terminal 상태와 제한 시간을 확인한다.
2. 보스 HP에 따라 phase를 한 단계 전이한다.
3. 노출 가능한 목표 부위를 계산한다.
4. 살아 있는 용병을 instanceId 순으로 행동시킨다.
5. HEALER는 가장 낮은 HP 아군을 회복하고, 나머지는 선택 부위 또는 보스를 공격한다.
6. 부위가 0이 되면 한 번만 파괴하고 effect를 보스 상태에 적용한다.
7. 보스 행동 간격이면 threat와 HP를 기준으로 대상을 선택해 공격·상태를 적용한다.
8. HP 45% 미만 용병은 보유 치유 포션을 자동 사용한다.
9. HP 0 용병은 전투 불능 처리하며 다시 행동하지 않는다.
10. 1초마다 bounded trace frame을 만든다.

보스 HP가 0이면 `SUCCESS`, 모든 용병이 전투 불능이면 `PARTY_DEFEATED`, 제한 시간 도달은
`TIME_LIMIT`다.

### 6.3 부위 효과

- `POISON_BREATH_REDUCED`: 히드라 독 행동 피해 감소
- `DEFENSE_DOWN`: 보스 방어 감소
- `ENRAGE`: 심장 노출 후 공격 증가와 행동 간격 단축
- `CHARGE_REDUCED`: 고룡 돌진 피해 감소
- `FLIGHT_DISABLED`: 비행 행동 제거
- `FINAL_ENRAGE`: 고룡 최종 phase 공격 증가

effect는 stable ID 집합으로 한 번만 적용한다. part break와 effect 적용은 같은 tick에서
완료된다.

### 6.4 기여도와 부상

기여도는 `damage + healing × 0.75 + preventedDamage × 0.5`를 정수 point로 기록하고
동률은 instanceId로 정렬한다. 성공 시 전투 불능 용병, 실패 시 HP 30% 미만 용병을
`INJURED`로 전환한다. 영구 사망과 장비·소지품 손실은 없다.

## 7. Save content.12 계약

### 7.1 `payload.regions`

P14는 `regionVersion=2`, `raidVersion=1`을 사용한다.

```json
{
  "regionVersion": 2,
  "raidVersion": 1,
  "nextEventSequence": 1,
  "nextRaidHistorySequence": 1,
  "progress": [],
  "raids": [],
  "raidHistory": [],
  "events": []
}
```

`raids`는 콘텐츠의 2×3 난이도 행을 모두 갖고 raidId,difficulty로 정렬한다.

### 7.2 난이도 진행

각 `RaidProgress`는 다음을 가진다.

- raidId, difficulty, unlocked
- attemptCount, clearCount
- bestClearTimeMs, firstClearedAtUtc, lastClearedAtUtc
- firstClearRewardOperationId, lastResultCode
- partStates

`partStates`는 partId, breakCount, exposedCount, bestBreakTimeMs,
lastRewardOperationId를 가진다. 파괴 보상 operation은 현재 전투 operationId이며 replay에서
다시 증가하거나 지급되지 않는다.

### 7.3 이력

`raidHistory`는 최대 50건이다.

- sequence, operationId, raidId, difficulty
- resultCode, success, durationMs, targetPartId
- partyMercenaryInstanceIds, brokenPartIds, injuredMercenaryInstanceIds
- potionConsumptions
- contributions
- rewards
- firstClear, unlockedRegionIds, progressionFlagIds
- resultDigest, settledAtUtc

오래된 행을 제거해도 sequence는 감소하지 않는다. operation journal의 resultPayload와
history digest는 일치해야 한다.

### 7.4 P13→P14 Migration

Migration은 단방향·결정론적·멱등이며 원본을 변경하지 않는다.

1. content.11 schema, version, checksum을 검증한다.
2. 기존 region progress, events, recruitment, mercenaries, inventory를 보존한다.
3. regionVersion=2, raidVersion=1, nextRaidHistorySequence, raidHistory를 추가한다.
4. 2×3 `RaidProgress`를 생성하고 기존 raid 행이 있으면 합성 키로 병합한다.
5. 현재 Save 조건으로 unlock rules를 평가해 NORMAL/HARD/CORRUPTED 상태를 정규화한다.
6. gameVersion/contentVersion과 integrity hash를 갱신한다.

역방향 Migration은 제공하지 않는다. 롤백은 P14 커밋을 되돌리고 content.11 백업 Save를
사용한다.

## 8. 명령·원자 정산·복구

### 8.1 조회

`IRaidService.GetOverview()`는 레이드/난이도 잠금, 파티 후보, 경고, 부위, 최근 결과를 DTO로
반환한다. View는 Save와 콘텐츠를 직접 읽지 않는다.

### 8.2 `RESOLVE_RAID`

명령 필드:

- operationId UUIDv7
- expectedRevision
- requestHash
- raidId, difficulty
- partyMercenaryInstanceIds
- targetPartId
- warningsAccepted

처리 순서:

1. requestHash와 replay를 확인한다.
2. expectedRevision CAS와 모든 준비 조건을 검증한다.
3. immutable 전투 snapshot을 생성한다.
4. 엔진으로 bounded trace와 결과를 계산한다.
5. 새 Save draft에 포션 소비·부상·용병 기록·raid progress를 반영한다.
6. 성공이면 부위·반복·NORMAL 최초 보상을 적용한다.
7. 진행 플래그를 적용하고 P12 region unlock 정규화를 같은 draft에서 실행한다.
8. raid history, region event, operation journal과 result digest를 추가한다.
9. Save를 한 번 CAS replace한다.
10. commit 성공 후 DTO와 UI 이벤트를 발행한다.

Save 실패 시 포션·부상·보상·진행·history가 모두 불변이다.

### 8.3 보상

- 실패: 보상 없음
- 성공: 파괴한 모든 부위 reward group + 난이도 repeat reward group
- NORMAL 첫 성공: raid의 first clear reward group을 추가
- 같은 itemId는 한 operation 안에서 합산한 뒤 inventory stack에 더한다.
- REGION_UNLOCK은 직접 잠금 값을 조작하지 않고 raid clear 기록 후 P12 정규화로 적용한다.
- PROGRESSION_FLAG는 중복 없는 set으로 적용한다.

### 8.4 replay와 복구

- 동일 operationId + 동일 hash: 최초 resultPayload를 반환하고 상태를 재변경하지 않는다.
- 동일 operationId + 다른 hash: `P14_OPERATION_HASH_MISMATCH`
- expectedRevision 불일치: `P14_SAVE_REVISION_CONFLICT`
- 시뮬레이션 후 commit 전 종료: Save가 바뀌지 않아 같은 명령 재시도 가능
- commit 후 UI 재생 중 종료: journal/history의 결과를 복구하고 보상을 다시 지급하지 않음
- 결과 digest 불일치: `P14_RESULT_DIGEST_MISMATCH`

## 9. 오류 코드

- `P14_CONTENT_INVALID`, `P14_CONTENT_VERSION_UNSUPPORTED`
- `P14_SAVE_REVISION_CONFLICT`, `P14_OPERATION_HASH_MISMATCH`
- `P14_RAID_NOT_FOUND`, `P14_DIFFICULTY_NOT_FOUND`, `P14_RAID_LOCKED`
- `P14_PARTY_SIZE_INVALID`, `P14_PARTY_DUPLICATE`, `P14_PARTY_MEMBER_NOT_FOUND`
- `P14_PARTY_MEMBER_INACTIVE`, `P14_PARTY_RANK_TOO_LOW`, `P14_PARTY_MEMBER_BUSY`
- `P14_PARTY_MEMBER_INJURED`, `P14_TARGET_PART_INVALID`
- `P14_WARNINGS_NOT_ACCEPTED`, `P14_SIMULATION_INVALID`
- `P14_REWARD_INVALID`, `P14_RESULT_DIGEST_MISMATCH`, `P14_SAVE_WRITE_FAILED`

모든 검증 오류는 Save와 trace를 변경하지 않는다.

## 10. UI/UX 계약

### 10.1 stable ID

- HUD 진입: `P14_RAID_NAV_BUTTON`, 표시 `레이드`
- 화면: `P14_RAID_SCREEN`
- 레이드: `P14_RAID_HYDRA`, `P14_RAID_DRAGON`
- 난이도: `P14_DIFFICULTY_NORMAL`, `P14_DIFFICULTY_HARD`, `P14_DIFFICULTY_CORRUPTED`
- 파티: `P14_PARTY_SLOT_1`~`P14_PARTY_SLOT_8`
- 부위: `P14_PART_TARGET_1`~`P14_PART_TARGET_4`
- 시작: `P14_START_RAID`
- 경고 확인: `P14_ACCEPT_WARNINGS`
- 전투: `P14_COMBAT_STAGE`
- 결과: `P14_RESULT_PANEL`
- 재도전: `P14_RETRY`
- 닫기: `P14_CLOSE`

### 10.2 준비 화면

첫 viewport의 주인공은 보스와 선택 부위다.

1. 상단: `원정 지휘소`, 레이드 이름, 난이도, 권장 전투력, 최초 보상 상태
2. 좌측: 히드라/고룡 선택과 난이도 segmented control
3. 중앙: 보스 실루엣, 부위 노드, 파괴 효과와 예상 재료
4. 우측: 6~8 파티 슬롯, 역할 분포, 전투력, 포션 준비
5. 하단: 잠금·경고 요약과 단일 primary action `레이드 시작`

히드라는 독안개 녹색·산성 황금, 고룡은 잿빛 남청·용암 적색을 사용한다. P10~P13의
다크 브론즈와 청록 계층을 유지하되 보스별 accent만 교체한다. 반복 카드 안에 다시 카드를
중첩하지 않는다.

### 10.3 전투 화면

- 상단 중앙: 보스 HP와 phase
- 보스 주변: 현재 목표 부위 내구도와 파괴 effect
- 좌측: 제한 시간과 난이도
- 하단: 6~8명 HP, 역할, 포션 사용 상태
- 우측: 최근 보스 행동과 파괴 로그

시뮬레이션 결과 trace를 4~6초의 가속 재생으로 보여주며 탭으로 결과까지 건너뛸 수 있다.
UI 재생은 Save 결과를 바꾸지 않는다.

### 10.4 결과 화면

- 성공/실패와 완료 시간
- 최초 토벌·R05 개방·1차 엔딩 banner
- 파괴 부위와 획득 재료
- 기여도 순위와 부상·포션 소비
- `재도전` 또는 `준비로 돌아가기`

### 10.5 상태

- Loading: 콘텐츠·Save 로드 중 입력 차단
- Locked: 가장 우선인 미충족 조건과 진행 수치
- Ready: 파티·목표가 유효함
- Warning: 역할·포션·전투력 경고 확인 필요
- Simulating: trace 재생, skip 가능
- Success / Failed: 정산된 결과
- Empty: 편성 가능 용병 부족과 모집 이동
- Error: stable 오류 코드, 새로고침·재시도
- Offline: 로컬 레이드는 가능하며 서버 권위 보상은 표시하지 않음

1920×1080 기준 버튼 최소 64×64px, 본문 28px 이상, 핵심 수치 32px 이상이다.
16:9와 2400×1080 20:9에서 한글/영문 overflow, overlap, 잘린 Safe Area가 없어야 한다.

## 11. 테스트와 증거

### 11.1 콘텐츠·Save

- 95 tables, schema set 10, 모든 신규 FK/enum/range/정렬
- raid 2, difficulty 6, parts 7, boss phases 6
- 심장 선행 부위와 HP 노출 조건
- content.11→content.12 before/after/new-game golden
- Migration 원본 불변·멱등·기존 P12/P13 상태 보존

### 11.2 EditMode

- 히드라/고룡 NORMAL unlock 경계와 난이도 순차 개방
- 5/6/8/9명, 중복, 비활성, 낮은 랭크, 승급 중, 부상 차단
- 역할·포션·낮은 전투력 warning과 확인 hash
- 동일 입력 deterministic trace/digest
- 부위 선행·노출·파괴 효과와 보스 phase 전이
- 성공, 전멸, 제한 시간
- 실패 시 포션·부상 유지와 보상 0
- 부위·반복·최초 보상, 최초 중복 방지
- 히드라→R05, 고룡→엔딩·변종 플래그
- replay/hash mismatch/CAS/Save 실패 원자성

### 11.3 PlayMode·렌더

- Bootstrap→레이드 화면 진입
- 잠긴 히드라와 조건 표시
- 준비 완료 fixture의 8명 역할·포션·부위 선택
- 히드라 전투 HP/부위/시간/파티 trace
- 히드라 성공 결과와 R05 개방
- 고룡 전투와 1차 엔딩 결과
- 실패 결과·부상·포션 소비
- 1920×1080과 2400×1080 GPU 캡처

### 11.4 CI·Android

- `scripts/ci/p14.sh`는 generator, schema, migration, runtime, UI, generated asset, 캡처를 검증한다.
- Git Hook, `scripts/ci/run-ci.sh`, GitHub Actions에 P14 gate를 추가한다.
- Unity `6000.3.20f1`에서 P14 EditMode/PlayMode, GPU capture, Android ARM64 IL2CPP
  Development APK를 실행한다.

## 12. 성능·안전

- 최대 8명, 1보스, 4부위, 3600 ticks, 360 trace frames로 상한을 둔다.
- 시뮬레이션은 순수 C# 계산이며 프레임당 Save/CSV/JSON 파싱을 하지 않는다.
- UI trace는 전체 hierarchy rebuild 대신 기존 bar와 label을 갱신한다.
- 결과 trace와 history는 bounded collection이다.
- View는 DTO만 받고 Save·콘텐츠·보상 저장소를 직접 변경하지 않는다.
- 레이드 보상은 operationId/hash/CAS/history/journal digest를 약화하지 않는다.
- P14는 프리미엄 재화나 서버 이벤트 보상을 생성하지 않는다.

## 13. 수동 확인 체크리스트

- [ ] 히드라/고룡 선택 시 색과 부위·보상이 명확히 바뀐다.
- [ ] 잠금 이유가 실제 현재/요구 수치로 보인다.
- [ ] 역할과 포션 경고는 보이지만 확인 후 시작 가능하다.
- [ ] 보스 HP, 목표 부위, 시간, 파티 HP가 trace 동안 변한다.
- [ ] 부위 파괴 effect와 결과 보상이 일치한다.
- [ ] 실패 후 포션과 부상만 반영되고 보상은 없다.
- [ ] 히드라 최초 성공 후 R05가 열린다.
- [ ] 고룡 최초 성공 후 엔딩·변종 플래그가 보인다.
- [ ] 재시작 후 마지막 결과를 복구하고 보상을 중복 지급하지 않는다.
- [ ] 16:9/20:9에서 터치 타깃과 텍스트가 잘리지 않는다.

## 14. 후속 Phase 인계

- P15는 레이드를 오프라인 정산 대상에서 제외하고, 첫 부상 치료와 첫 레이드 준비 튜토리얼을
  이 P14 stable ID와 결과 event에 연결한다.
- P16 서버 연결은 향후 이벤트성 최초 보상이 추가될 때 `IRaidRewardGateway` Adapter를
  도입하되 P14의 operationId/hash/resultDigest 계약을 보존한다.
- P17은 실제 기기 3~6분 전투 템포, 난이도별 성공률, 부위 선택률, 메모리와 프레임을 조정한다.

## 15. 설계 완결성 감사

| 검토 항목 | 판정 |
|---|---|
| 공식 P14 범위와 다음 Phase 경계 | 해결 |
| 두 레이드와 세 난이도 개방 | 확정 |
| 6~8명 파티·랭크·역할·포션 | 확정 |
| 보스 AI·tick·phase·terminal | 확정 |
| 부위 노출·파괴·effect | 확정 |
| 성공·실패·부상·소비 | 확정 |
| 부위·반복·최초 보상 | 확정 |
| R05·엔딩·변종 진행 연결 | 확정 |
| Save schema·Migration·복구 | 확정 |
| replay·hash·CAS·원자성 | 확정 |
| UI 상태·접근성·화면비 | 확정 |
| 테스트·렌더·Android·CI | 확정 |
| 미해결 항목 | 없음 |

P14 구현을 막는 설계 공백은 없다. 외부 상세 설계서 추가 요청 없이 이 문서로 구현한다.
