# TYCOON P11 레벨·기여도·승급 최종 설계서 v1.0

> 문서 상태: FINAL
> 구현 기준: P10 `1.0.0-content.8` / Save v1
> 산출 버전: Game `1.0.0-p11`, Content `1.0.0-content.9`
> IMPLEMENTATION_READY: YES
> UNRESOLVED: NONE

## 1. 목적과 완료 정의

P11은 사냥에서 얻은 경험치와 기여도를 용병의 성장 랭크로 연결하고, 모험가 길드 심사를 거쳐 수습부터 전설까지 성장하는 장기 루프를 완성한다. 기본 등급 C/B/A/S/SS는 모집 잠재력과 비용 배율로만 사용하며 승급 과정에서 변경하지 않는다.

P11 완료 조건은 다음과 같다.

1. `content.8` 저장을 손실 없이 `content.9`로 마이그레이션한다.
2. 사냥 정산에서 몬스터 XP를 파티원에게 결정론적으로 배분하고 한 번의 정산에서 여러 레벨 상승을 처리한다.
3. 랭크별 최대 레벨, 기여도, 전투 실적, 길드 상태와 레벨을 모두 충족하면 승급 준비 상태를 만든다.
4. 개인 골드, 왕국 골드, 기본 승급 증표와 등급별 추가 재료를 심사 시작 시 한 번만 소비한다.
5. 심사 만료를 재접속·오프라인·화면 진입 시 정규화하고, 적용 시 랭크를 한 단계 올린 뒤 레벨 1과 EXP 0으로 초기화한다.
6. 장비, 기본 등급, 개인 골드 잔액, 기여도, 특성, 성격, 전투·수집 기록은 승급 후 유지한다.
7. revision CAS, RFC 8785 request hash, operation replay, 경제 원장, 진행 이벤트를 동일 Save revision에 기록한다.
8. 16:9~20:9 모바일 화면에서 XP, 준비 조건, 비용, 남은 시간, 장비 권장 점수를 실행 전에 확인할 수 있다.
9. C~SS 전 등급의 수습→전설 경로, migration, recovery, EditMode, PlayMode, 렌더 캡처, CI와 Android 빌드 진입점을 제공한다.

## 2. 선행 계약과 충돌 해소

### 2.1 보존하는 계약

- P03 Save v1 envelope, checksum, 원자 교체, 3중 백업, schema registry
- P05 용병 `gradeId`, `rankId`, `level`, `exp`, `contribution`, `promotion`, `records`
- P06 사냥 정산과 용병별 기여도, `PROMOTION_READY`·`PROMOTION_PROCESS` 상태
- P07 inventory item stack, 장비 링크와 직업별 장비 점수
- P08 개인/왕국 골드와 경제 원장
- P10 장비 강화·제련 필드와 수동 성장 명령
- UUIDv7, expected revision/CAS, operation journal, deterministic replay

### 2.2 설계 충돌 판정

P10 구현 보고서의 다음 단계 제안에는 자동 강화·자동 제련·자동 분해가 포함되지만 P10 FINAL 설계 2.3은 이를 후속 업데이트로 제외한다. `phases/P11_PROGRESSION_PROMOTION.md`와 `docs/08_GRADE_RANK_PROMOTION.md`는 P11을 XP·기여도·승급·길드 심사로 고정한다.

따라서 P11은 장비 점수를 승급 준비 권장 정보로 읽기만 한다. P10 성장 명령을 자동 실행하거나 장비를 임의 변경하지 않는다. 이 판정은 1.0 범위와 상위 FINAL 계약을 동시에 보존한다.

### 2.3 구현 범위

- 랭크별 레벨 XP 곡선과 다중 레벨 상승
- 승급 준비 조건과 길드 심사
- C~SS 등급별 비용 배율과 A/S/SS 추가 재료
- 승급 증표의 자격 달성 1회 지원 공급
- 심사 시작, 만료 정규화, 승급 적용
- 장비 점수 기반 준비 권장 표시
- `content.9`, Save migration/recovery, UI, 테스트와 증거

### 2.4 제외 범위

- 기본 등급 변경, 중복 돌파, 승급 실패 확률
- 자동 강화·자동 제련·자동 분해와 P10 명령 자동 호출
- 승급 취소와 소비 재료 환불
- 용병 영구 사망 또는 승급 실패 페널티
- 레이드 전투와 신규 지역 실제 플레이 구현
- 특성 재추첨, 스킬 직접 선택, 장비 내구도
- DEFERRED·OPS_LATER 기능

## 3. Content `1.0.0-content.9`

`content.8`의 80개 표를 보존하고 4개 표를 추가한다. 총 표 수는 84개이며 `csvSchemaSetVersion=7`, `minimumGameVersion=1.0.0-p11`이다.

### 3.1 `mercenary_level_curves.csv`

| 필드 | domain | 규칙 |
|---|---|---|
| `rank_id` | STABLE_ID | `mercenary_ranks.rank_id` HARD FK |
| `level` | INT32 | 1..해당 랭크 max_level |
| `xp_to_next` | SAFE_INT | 비최대 레벨 1 이상, 최대 레벨 0 |
| `cumulative_xp` | SAFE_INT | 레벨 1은 0, 이전 행 누적과 일치 |
| `status` | STATUS | `TUNABLE` |
| `enabled` | BOOL | `TRUE` |

PK는 `(rank_id,level)`이다. 런타임은 CSV 값만 사용한다. 생성기는 다음 정수식을 초기 TUNABLE 값 생성에 사용한다.

```text
n = level - 1
xp_to_next = rank_order*20 + rank_order*4*n + floor(rank_order*n*n/2)
```

각 랭크 최대 레벨 행은 `xp_to_next=0`이다. 행 수는 수습 20 + 정식 30 + 숙련 40 + 정예 50 + 영웅 60 + 전설 70 = 270이다.

### 3.2 `promotion_review_rules.csv`

| from | to | 길드 Lv | 왕국 골드 기본 | 심사 초 | 권장 장비 점수 |
|---|---|---:|---:|---:|---:|
| 수습 | 정식 | 1 | 200 | 60 | 400 |
| 정식 | 숙련 | 2 | 800 | 300 | 900 |
| 숙련 | 정예 | 2 | 3,000 | 900 | 1,800 |
| 정예 | 영웅 | 3 | 12,000 | 1,800 | 3,500 |
| 영웅 | 전설 | 4 | 40,000 | 3,600 | 6,000 |

필드는 `from_rank_id`, `to_rank_id`, `guild_level_required`, `base_kingdom_gold`, `review_seconds`, `recommended_equipment_score`, `status`, `enabled`이다. PK는 `from_rank_id`, 양쪽 랭크는 HARD FK다. `mercenary_ranks.promotion_to`와 반드시 일치해야 한다.

개인 골드 기본값과 기여도 요구량, 기본 승급 증표는 `mercenary_ranks.csv`를 단일 권위로 사용한다. 개인 골드와 왕국 골드는 `mercenary_grades.promotion_cost_multiplier`를 곱한 뒤 양의 무한대 방향 정수 올림한다.

### 3.3 `promotion_record_requirements.csv`

| from rank | record_type | subject | required_count |
|---|---|---|---:|
| 수습 | `HUNT_COUNT` | null | 3 |
| 정식 | `REGION_BATTLE_COUNT` | `REGION_R02` | 20 |
| 숙련 | `ELITE_KILL_COUNT` | null | 10 |
| 정예 | `BOSS_CONTRIBUTION_COUNT` | null | 3 |
| 영웅 | `RAID_CLEAR_COUNT` | null | 3 |

필드는 `from_rank_id`, `requirement_no`, `record_type`, `subject_id`, `required_count`, `status`, `enabled`이다. PK는 `(from_rank_id,requirement_no)`이며 각 승급 경로에 정확히 1개 행이 존재한다. `subject_id`는 `REGION_BATTLE_COUNT`에서만 필수이고 나머지는 null이다.

P05의 기존 `mercenary_ranks.additional_condition`은 표시/이전 호환용이다. P11 런타임 권위는 이 정규화 표이며 generator가 기존 문자열과 1:1 의미 일치를 검증한다.

### 3.4 `promotion_supply_rules.csv`

| rule_id | from rank | 지급 재료 | 수량 |
|---|---|---|---:|
| `SUPPLY_PROMO_APPRENTICE` | 수습 | 청동 승급 문장 | 1 |
| `SUPPLY_PROMO_REGULAR` | 정식 | 은빛 승급패 | 1 |
| `SUPPLY_PROMO_SKILLED` | 숙련 | 정예의 인장 | 1 |
| `SUPPLY_PROMO_ELITE` | 정예 | 영웅의 증표 | 1 |
| `SUPPLY_PROMO_HERO` | 영웅 | 전설의 증표 | 1 |

필드는 `supply_rule_id`, `from_rank_id`, `item_id`, `quantity`, `status`, `enabled`이다. 해당 용병이 3.3의 전투 실적 조건을 처음 충족할 때 공유 inventory에 1회 지급한다. 지급 키는 `supply_rule_id:mercenaryInstanceId`이며 Save에 보존한다. 이는 확률 드롭 실패로 승급이 영구 정지하는 것을 방지하는 최소 안전 공급이다. A/S/SS 추가 재료는 이 안전 공급 대상이 아니다.

### 3.5 기존 표 사용

- `mercenary_ranks.csv`: 순서, max level, 다음 랭크, 개인 골드 기본, 기여도, 기본 증표
- `mercenary_grades.csv`: C/B/A/S/SS 비용 배율
- `promotion_grade_requirements.csv`: A/S/SS 상위 승급 추가 재료
- `monsters.csv`: 전투 정산 XP
- `facility_levels.csv`: 모험가 길드 레벨
- `items.csv`: 모든 승급 재료 식별자

## 4. Save v1 `content.9`

### 4.1 용병 records 확장

기존 `huntCount`, `killCount`, `raidClearCount`, `itemsCollected`를 유지하고 다음 필드를 추가한다.

- `region2BattleCount`: `REGION_R02`에서 완료한 전투 수
- `eliteKillCount`: ELITE 몬스터 처치 수
- `bossContributionCount`: 유효 보스 기여 판정 횟수

모든 값은 0 이상 safe integer이며 migration 기본값은 0이다. `raidClearCount`는 기존 필드를 사용한다.

### 4.2 `payload.progression`

```json
{
  "progressionVersion": 1,
  "nextEventSequence": 1,
  "issuedSupplyKeys": [],
  "events": []
}
```

- `issuedSupplyKeys`: `SUPPLY_*:<uuid-v7>` 정렬·중복 없음, 최대 120개
- `events`: sequence 오름차순, 최대 200개. 초과 시 가장 오래된 행부터 제거하되 sequence는 재사용하지 않는다.
- event type: `ExperienceAwarded`, `PromotionReady`, `PromotionSupportIssued`, `PromotionReviewStarted`, `PromotionReviewCompleted`, `PromotionApplied`
- event 공통 필드: `sequence`, `eventType`, `mercenaryInstanceId`, `fromRankId`, `toRankId`, `levelBefore`, `levelAfter`, `experienceDelta`, `resultCode`, `createdAtUtc`
- 적용 불가 값은 명시적 null을 사용한다.

### 4.3 promotion 상태 불변식

| status | target | operation | started/finishes | costSnapshot | autonomy |
|---|---|---|---|---|---|
| `NONE` | null | null | null | null | `IDLE_TOWN` 등 |
| `READY` | 다음 랭크 | null | null | null | `PROMOTION_READY` |
| `IN_REVIEW` | 다음 랭크 | UUIDv7 | UTC | snapshot | `PROMOTION_PROCESS` |
| `COMPLETED_PENDING_APPLY` | 다음 랭크 | UUIDv7 | UTC | snapshot | `PROMOTION_PROCESS` |

전설은 다음 랭크가 없으므로 항상 `NONE`이다. `READY`는 최대 레벨, 기여도, 전투 실적, 길드 ACTIVE/레벨 조건을 만족했음을 뜻한다. 골드·재료 보유 여부는 `READY`와 분리해 UI에서 부족 상태로 표시한다.

### 4.4 operation journal과 경제 원장

- 기존 `operationType=PROMOTION`을 사용한다.
- `transactionType`에 `PROMOTION_START`를 추가한다.
- ledger `productKind`에 `SERVICE`를 추가하고 `productId=PROMOTION_<toRankId>`, quantity 1로 기록한다.
- `personalGoldDelta`와 `kingdomGoldDelta`는 음수, `stockVersionAfter`는 상점 재고를 바꾸지 않으므로 현재 값을 유지한다.
- 소비 재료는 `costSnapshot.items`, progression event, operation result payload에 기록한다.
- 승급 적용은 골드 이동이 없으므로 경제 원장을 추가하지 않는다.

### 4.5 `content.8 → content.9` migration

1. 모든 용병 records에 신규 카운터 3개를 0으로 추가한다.
2. `payload.progression`을 기본값으로 추가한다.
3. 기존 promotion 상태와 비용 snapshot, P10 장비 성장 필드는 그대로 보존한다.
4. `gameVersion=1.0.0-p11`, `contentVersion=1.0.0-content.9`로 변경한다.
5. payload/file hash를 RFC 8785로 다시 계산한다.

Migration은 단방향이고 원본을 변경하지 않는다. 실패 시 content.8 schema로 원본/백업을 계속 검증할 수 있어야 한다.

## 5. XP와 준비 상태 계약

### 5.1 전투 XP 배분

한 사냥 정산의 몬스터별 `xp × 실제 처치 수` 합계를 `totalXp`라 한다. 파티 instance ID를 ordinal 오름차순 정렬하고 다음과 같이 분배한다.

```text
base = floor(totalXp / partyCount)
remainder = totalXp mod partyCount
앞의 remainder명은 base+1, 나머지는 base
```

사냥 정산의 contribution/gold/loot/XP/레벨/준비 상태/지원 증표는 동일 Save revision에 커밋한다. recall/replay가 같은 XP를 두 번 지급하지 않는다.

### 5.2 레벨 상승

현재 `(rankId,level)` 행의 `xp_to_next`를 차감할 수 있는 동안 반복해 레벨을 올린다. 최대 레벨 도달 시 남은 EXP는 0으로 버린다. 랭크를 넘는 자동 레벨 상승은 없다.

계산은 checked safe integer로 수행한다. 누적 XP가 비정상이거나 curve 행이 누락되면 전체 사냥 정산을 실패시키고 Save를 변경하지 않는다.

### 5.3 승급 준비 판정

다음 조건을 모두 만족하면 `READY`로 전이한다.

1. 다음 랭크가 존재한다.
2. 현재 레벨이 현재 랭크 max level이다.
3. `contribution >= contribution_required`다.
4. 현재 랭크의 모든 record requirement를 만족한다.
5. `FAC_GUILD`가 ACTIVE이고 요구 레벨 이상이다.
6. 현재 promotion이 `NONE`이고 현재 지역이 null이다.

조건이 충족되기 전에는 `NONE`이다. 한번 `READY`가 된 뒤 콘텐츠 hotfix로 수치가 올라가더라도 심사 시작 전 조회에서 다시 검증한다.

### 5.4 권장 장비 점수

현재 장착 4부위의 P07 직업별 `EquipmentScore` 합계를 계산한다. 권장 점수 미만이어도 승급을 차단하지 않고 `권장 미달` 경고만 제공한다. 장비 없음은 0이다. P10 강화·제련 효과는 기존 점수 계산 결과를 그대로 사용한다.

## 6. 승급 명령 계약

모든 명령은 `operationId`, `expectedRevision`, `requestHash`, `commandType`, `mercenaryInstanceId`를 포함한다. request hash는 `requestHash` 필드를 제외한 전체 명령을 RFC 8785 canonicalize 후 SHA-256으로 계산한다.

### 6.1 `START_PROMOTION_REVIEW`

처리 순서:

1. operation replay와 request hash를 확인한다.
2. expired review를 먼저 정규화한 현재 revision을 기준으로 CAS를 확인한다.
3. 대상 용병, `READY`, `PROMOTION_READY`, 지역 null을 확인한다.
4. 레벨·기여도·record·길드 조건을 다시 확인한다.
5. grade multiplier를 적용해 비용 snapshot을 생성한다.
6. 개인/왕국 골드와 item stack 전체를 사전 검증한다.
7. 골드와 재료를 원자적으로 차감한다. 기여도는 차감하지 않는다.
8. `IN_REVIEW`, target, operation, started/finishes, cost snapshot과 `PROMOTION_PROCESS`를 저장한다.
9. ledger, progression event, operation journal/result를 추가하고 revision을 1 증가시킨다.

### 6.2 만료 정규화

`now >= finishesAtUtc`인 `IN_REVIEW`를 `COMPLETED_PENDING_APPLY`로 변경하고 `PromotionReviewCompleted` 이벤트를 기록한다. 비용은 이미 심사 시작 시 소비됐으므로 추가 이동이 없다.

정규화 진입점은 bootstrap, 화면/조회 refresh, 오프라인 정산 종료다. 변경 대상이 없으면 Save revision을 증가시키지 않는다.

### 6.3 `APPLY_PROMOTION`

1. operation replay/hash/CAS와 `COMPLETED_PENDING_APPLY`를 확인한다.
2. cost snapshot의 from/to가 현재 rank/target과 일치하는지 확인한다.
3. `rankId=targetRankId`, `level=1`, `exp=0`을 적용한다.
4. promotion을 `NONE`, autonomy를 `IDLE_TOWN`, reason을 `NONE`으로 초기화한다.
5. 기본 등급, 장비, 잔여 골드, 기여도, 특성, 성격, records를 보존한다.
6. `PromotionApplied` event와 operation journal/result를 기록하고 revision을 1 증가시킨다.

## 7. 실패·재시도·복구 계약

오류 코드는 다음을 사용한다.

- `P11_CONTENT_INVALID`, `P11_CONTENT_VERSION_UNSUPPORTED`
- `P11_SAVE_REVISION_CONFLICT`, `P11_OPERATION_HASH_MISMATCH`
- `P11_MERCENARY_NOT_FOUND`, `P11_PROMOTION_NOT_READY`
- `P11_PROMOTION_ALREADY_IN_REVIEW`, `P11_PROMOTION_NOT_COMPLETE`
- `P11_GUILD_INACTIVE`, `P11_GUILD_LEVEL_REQUIRED`
- `P11_LEVEL_REQUIRED`, `P11_CONTRIBUTION_REQUIRED`, `P11_RECORD_REQUIRED`
- `P11_PERSONAL_GOLD_INSUFFICIENT`, `P11_KINGDOM_GOLD_INSUFFICIENT`
- `P11_PROMOTION_MATERIAL_INSUFFICIENT`, `P11_INVENTORY_CAPACITY`
- `P11_PROMOTION_SNAPSHOT_INVALID`, `P11_SAVE_WRITE_FAILED`

동일 operationId+동일 hash는 최초 결과를 반환하고 revision/골드/재료/event를 다시 변경하지 않는다. 동일 operationId+다른 hash는 mismatch다. 모든 오류는 Save와 RNG가 없으므로 상태를 전혀 변경하지 않는다.

## 8. UI/UX 계약

### 8.1 진입과 stable ID

- Bootstrap 전역 버튼: `P11_PROGRESSION_NAV_BUTTON`, 표시 `승급`
- 화면: `P11_PROGRESSION_SCREEN`
- 제목: `모험가 길드 · 성장 심사`

첫 viewport는 다음 순서다.

1. 상단: 길드 레벨/상태, content.9, 심사 중 인원, 왕국 골드
2. 좌측: 용병 목록, 등급·랭크·레벨, 준비/심사/완료 배지
3. 중앙: 6단계 랭크 경로, 현재 XP bar, 다음 랭크와 남은 시간
4. 우측: 레벨·기여도·전투 실적·길드·골드·재료 체크리스트
5. 하단: 장비 점수/권장 점수와 `심사 시작` 또는 `승급 완료`

### 8.2 상태

- Content: 정상 목록과 명령
- Loading: 입력 차단과 진행 표시
- Empty: 보유 용병 없음
- Locked: 길드 미건설/비활성 또는 레벨 부족
- Error: 오류 코드와 재시도
- Offline: 만료 심사 정규화 결과와 경과 시간 표시

`READY`와 `AFFORDABLE`을 분리한다. 조건은 충족했지만 재료가 부족하면 심사 버튼은 비활성화하고 부족 수량을 표시한다. 장비 권장 미달은 경고일 뿐 버튼을 비활성화하지 않는다.

### 8.3 시각·접근성

P10의 다크 브론즈·청록 계열을 유지하고 길드 문장, rank step, 진행 bar로 계층을 만든다. 색만으로 상태를 구분하지 않고 텍스트와 모양을 함께 사용한다.

- 기준 1920×1080, 16:9~20:9 Safe Area
- 최소 터치 영역 64×64 px
- 본문 28 px 이상, 중요 수치 32 px 이상
- 긴 한글에서 overflow/overlap 없음
- 주 행동은 화면당 하나만 강조
- 닫기/뒤로가기와 모달 규칙은 공통 UI를 따른다.

## 9. 테스트와 증거

### 9.1 Content/Save

- 84개 표, 270 XP curve 행, 5 review/record/supply 규칙
- PK/FK/domain/sentinel/status/hash 검증
- `content.8 → .9` records/progression migration
- new game content.9와 before/after golden
- schema registry .5~.9 보존
- checksum, 원자 저장, 백업 recovery, unknown/duplicate 필드 실패

### 9.2 EditMode

- XP 분배 합계 보존과 instance ID remainder 순서
- 다중 레벨 상승, 최대 레벨 EXP 0, 전설 상한
- 준비 상태의 레벨/기여도/record/guild 각 gate
- C/B/A/S/SS 비용 올림과 추가 재료 병합/정렬
- 수습→전설 5단계 경로와 grade 불변
- 심사 시작 원자 차감, time boundary, apply 보존 필드
- replay/hash mismatch/revision conflict/실패 무변경
- 지원 증표 용병별 1회 지급
- content migration과 이전 schema 선택

### 9.3 PlayMode/렌더

- Bootstrap→Kingdom→승급 화면 진입
- 준비 전, READY, IN_REVIEW, COMPLETED 상태
- 심사 시작 후 골드/재료/남은 시간 갱신
- 적용 후 rank 상승, level 1, 장비·grade 유지
- 1920×1080과 2400×1080 캡처
- 모든 행동 버튼 64 px 이상, 텍스트 잘림 없음

### 9.4 CI/Android

- `scripts/ci/p11.sh`가 generator, source entrypoint, migration, 생성 asset, capture를 검증한다.
- licensed runner에서 P11 EditMode/PlayMode, setup/verifier/capture를 실행한다.
- Android IL2CPP ARM64 Development 빌드 진입점을 제공한다.

## 10. P12 인계 경계

P12는 레이드 준비·전투·결과를 구현하되 P11의 `rankId`, `raidClearCount`, `bossContributionCount`, promotion cost snapshot과 event sequence를 재해석하거나 삭제하지 않는다. 레이드 참여 자격은 P11 랭크 결과를 읽고, 레이드 보상과 전투 실적은 P11 progression settlement의 공개 입력 계약으로 전달한다.

P11은 레이드 UI나 보스 전투를 선행 구현하지 않는다.

## 11. 완결성 감사

| 검토 항목 | 판정 |
|---|---|
| P05 용병/grade 불변 호환 | 확정 |
| P06 XP·기여도 원자 정산 | 확정 |
| P07 장비 점수 권장 연동 | 확정 |
| P08 개인/왕국 골드 원장 | 확정 |
| P10 장비 성장 범위 보존 | 확정 |
| XP 곡선/상한/초과 처리 | 확정 |
| 레벨·기여도·실적·길드 gate | 확정 |
| C~SS 비용과 추가 재료 | 확정 |
| 심사 시간/만료/적용 | 확정 |
| 승급 증표 안전 공급 | 확정 |
| Save migration/recovery | 확정 |
| CAS/replay/ledger/event | 확정 |
| UI/접근성/증거 | 확정 |
| P12 인계 | 확정 |
| 미결정 | 없음 |

P11 구현을 막는 미결정 사항은 없다. XP, 비용, 심사 시간, 권장 점수와 지급 수량은 모두 Content CSV의 `TUNABLE` 값이며 코드 상수로 숨기지 않는다.
