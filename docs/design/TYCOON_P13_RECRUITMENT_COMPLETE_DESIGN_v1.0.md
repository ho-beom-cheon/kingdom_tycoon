# TYCOON P13 주점·특별 모집·천장·기록 최종 설계 v1.0

## 0. 문서 상태

- 문서 상태: `FINAL`
- 구현 준비: `IMPLEMENTATION_READY: YES`
- 미해결 항목: `UNRESOLVED: NONE`
- 적용 게임 버전: `1.0.0-p13`
- 적용 콘텐츠 버전: `1.0.0-content.11`
- Save schema ID: `urn:tycoon:schema:save:v1:content.11`
- 선행 기준: P12 `1.0.0-content.10`
- 관련 이슈: `#39`
- 구현 브랜치: `codex/issue-39-p13-recruitment`

이 문서는 P13의 유일한 구현 계약이다. `CONFIRMED`는 그대로 구현하고 `TUNABLE` 수치는
CSV에서만 변경한다. 상용 결제·환불·확률 공시·배너 운영은 이 문서로 구현하지 않는다.

## 1. 목표와 플레이 결과

P13은 P05 로스터와 P04 주점을 실제 신규 용병 획득 흐름으로 연결한다. 플레이어는 다음을
수행할 수 있어야 한다.

1. 주점에서 시설 레벨에 맞는 C~A 후보 3~5명을 확인한다.
2. 원하는 후보를 잠근 뒤 무료 시간이 지나거나 왕국 골드를 사용해 나머지 후보를 갱신한다.
3. 왕국 골드를 지불하고 후보를 고용해 고유 용병 Instance를 로스터에 추가한다.
4. 개발 Mock 특별 모집에서 티켓 또는 무료 프리미엄 재화를 사용해 A~SS 용병을 획득한다.
5. S 이상 보장과 SS 하드 천장 진행, 이월 그룹, 결과 기록을 확인한다.
6. 획득 용병을 기존 성장·장비·지역 배치 흐름에서 즉시 사용할 수 있다.

## 2. 기준 문서 충돌 해결

| ID | 충돌 | 최종 결정 |
|---|---|---|
| P13-C01 | 특별 모집은 서버 권한이지만 P13 Phase는 Mock/Adapter를 요구한다. | `IRecruitmentGateway`를 경계로 둔다. Development Build는 `MOCK_ONLY`, 서버 연결은 `SERVER_CACHE`다. `SERVER_CACHE`에서는 클라이언트가 재화·RNG·천장을 계산하지 않는다. |
| P13-C02 | P0 서버는 단일 하드 천장 vertical slice이고 콘텐츠는 S+와 SS 두 규칙을 가진다. | P13 Mock은 content.11 다중 규칙을 완전히 구현한다. 서버 Adapter는 다중 규칙 결과가 포함된 영수증만 수용하며 P0 단일 규칙 서비스의 직접 상용 연결은 금지한다. |
| P13-C03 | 기존 Save에는 `recruitmentMockState` 골격만 있고 주점 후보·이력이 없다. | Save content.11에서 기존 필드를 보존하고 주점·이력·사건 필드를 확장한다. |
| P13-C04 | 특별 모집 10회 필드가 스키마에 있으나 기본 숙소 여유는 4칸이다. | P13 UI와 명령은 1회 모집만 제공한다. 10회 모집은 숙소 초과 원자 실패와 결과 표시 정책을 별도 확정할 때까지 후속으로 둔다. |
| P13-C05 | 동일 캐릭터 중복 처리 요구와 생성형 용병 원칙이 충돌한다. | 모든 결과는 새 고유 Instance다. 고정 영웅·중복 돌파·조각 변환은 구현하지 않는다. |
| P13-C06 | 상용 확률 공시가 필요하지만 운영 정책은 `OPS_LATER`다. | P13은 콘텐츠 확률과 천장 수치를 UI에서 정확히 표시하되 법무 문구·결제·환불·스토어 공시는 구현하지 않는다. |

## 3. 범위

### 3.1 포함

- 주점 레벨 1~4 후보 3~5명
- 주점 C/B/A 한정, 특별 모집 A/S/SS 한정
- 후보 잠금, 최대 잠금 수, 무료 갱신 시각, 왕국 골드 유료 갱신
- 후보별 고용비와 숙소 보유 한도 검사
- 특별 모집 티켓·무료 프리미엄·유료 프리미엄 Gateway 계약
- S 이상 보장, SS 하드 천장, 같은 pity group 이월
- 직업 확률 상승과 픽업 실패 후 다음 S/SS 직업 보장
- 매 결과 고유 용병 Instance 생성
- content.10 → content.11 Save Migration
- request hash, revision CAS, operation replay, 경제 원장, 모집 이력과 사건
- Loading/Content/Empty/Error/Offline/Capacity 상태
- 1920×1080과 2400×1080, 64px 이상 터치 영역
- EditMode·PlayMode·GPU 렌더·CI·Android ARM64 IL2CPP 검증

### 3.2 제외

- 실제 결제, 환불, 영수증 검증, 상용 배너 운영과 법적 공시
- 광고 시청 모집, 10회 모집, 고정 영웅, 중복 돌파
- P14 레이드, P15 오프라인·튜토리얼 실행
- 서버 인증·세션 UI, 클라우드 Save 전환
- PvP, 길드, 정치, 외교

## 4. 콘텐츠 계약

### 4.1 패키지

`1.0.0-content.10`의 88개 테이블을 승계한다. 기존 모집 6개 테이블은 content.11에서
검증·보강하고 다음 3개 테이블을 추가한다.

- 총 테이블 수: `91`
- CSV schema set version: `9`
- minimum game version: `1.0.0-p13`
- 신규: `recruitment_tavern_rules.csv`
- 신규: `recruitment_grade_cost_rules.csv`
- 신규: `recruitment_history_rules.csv`

### 4.2 기존 모집 테이블

- `recruitment_pools.csv`: TAVERN과 SPECIAL, cost type과 단가, pity/rate-up group
- `recruitment_pool_entries.csv`: 등급·직업 선택 방식·weight
- `recruitment_pity_groups.csv`: 같은 category 이월 여부
- `recruitment_pity_rules.csv`: S+ 10회, SS 80회 초기값
- `recruitment_rate_up_groups.csv`: featured share와 실패 보장 방식
- `recruitment_rate_up_entries.csv`: 직업별 featured 분포

각 pool의 활성 entry weight 합은 정확히 100이어야 한다. TAVERN에는 C/B/A만,
SPECIAL에는 A/S/SS만 존재해야 한다. pity rule의 trigger는 같은 group에서 오름차순이며
보장 등급도 낮은 등급에서 높은 등급 순으로 증가해야 한다.

### 4.3 `recruitment_tavern_rules.csv`

| 필드 | domain | 의미 |
|---|---|---|
| tavern_level | INT32 | 주점 레벨 PK, 1~4 |
| pool_id | STABLE_ID | 사용할 TAVERN pool |
| candidate_count | INT32 | 전체 후보 3~5 |
| max_locked | INT32 | 잠금 가능 수 1~2 |
| free_refresh_seconds | SAFE_INT | 무료 갱신 간격 |
| paid_refresh_cost | SAFE_INT | 즉시 갱신 왕국 골드 |
| status, enabled | STATUS, BOOL | 상태 |

초기값은 다음과 같다.

| 레벨 | pool | 후보 | 잠금 | 무료 갱신 | 즉시 갱신 |
|---:|---|---:|---:|---:|---:|
| 1 | TAVERN_L1 | 3 | 1 | 14400초 | 250 |
| 2 | TAVERN_L1 | 4 | 1 | 10800초 | 300 |
| 3 | TAVERN_L1 | 4 | 2 | 7200초 | 400 |
| 4 | TAVERN_L4 | 5 | 2 | 3600초 | 500 |

갱신은 잠긴 후보를 유지하고 빈 자리를 새 후보로 채운다. 후보 수보다 잠금 수가 작아야 한다.

### 4.4 `recruitment_grade_cost_rules.csv`

| grade_id | hire_cost_multiplier_bps |
|---|---:|
| GRADE_C | 10000 |
| GRADE_B | 15000 |
| GRADE_A | 25000 |

주점 고용비는 `ceil(pool.cost_amount × multiplier / 10000)`이다. S/SS 행은 금지한다.

### 4.5 `recruitment_history_rules.csv`

단일 `RECRUITMENT_HISTORY_V1` 행을 사용한다.

| 필드 | 초기값 |
|---|---:|
| history_max_entries | 100 |
| event_max_entries | 200 |
| pending_request_max_entries | 50 |
| result_reveal_seconds | 1 |

최대 건수 초과 시 오래된 항목부터 제거하되 sequence는 감소시키지 않는다.

### 4.6 가중치와 결정론

명령의 `operationId` UUID bytes와 contentVersion을 SHA-256으로 결합해 Mock seed를 만든다.
가중치 선택은 정렬된 row 순서와 배타 상한 `[0,totalWeight)`를 사용한다. 문자열 hash code,
프레임 시간, Unity 전역 RNG를 사용하지 않는다. 동일 operationId+동일 requestHash replay는
최초 결과를 반환하고 새 RNG를 소비하지 않는다.

## 5. 용병 생성 계약

모든 주점 후보와 특별 모집 결과는 `GENERATED_MERCENARY_V1`로 생성한다.

1. pool entry에서 grade와 job 선택 방식을 결정한다.
2. `jobs.csv` order와 rate-up 규칙으로 직업을 선택한다.
3. `mercenary_name_pool_entries.csv`에서 이름을 선택한다.
4. appearance·growth·personality seed를 각각 저장한다.
5. grade의 `initial_trait_count`만큼 해당 job에 허용된 고유 trait를 선택한다.
6. rank는 `RANK_APPRENTICE`, level 1, exp 0, 개인 골드 0, 마을 대기 상태로 만든다.
7. 고용 또는 특별 모집 commit 시 UUIDv7 `instanceId`를 새로 발급한다.

후보는 `candidateId`로만 식별하며 고용 전에는 로스터 Instance가 아니다. 동일 이름·직업·등급이
나와도 instanceId와 seed가 다르므로 중복이 아니다.

## 6. Save content.11 계약

### 6.1 `payload.recruitmentMockState`

기존 필드를 보존하고 다음 구조로 확장한다.

```json
{
  "stateVersion": 1,
  "authority": "MOCK_ONLY",
  "serverRevision": null,
  "nextHistorySequence": 1,
  "nextEventSequence": 1,
  "premiumWalletCache": {
    "freePremium": 600,
    "paidPremium": 0,
    "specialRecruitTickets": 3
  },
  "tavern": {
    "refreshSequence": 0,
    "lastRefreshAtUtc": null,
    "nextFreeRefreshAtUtc": null,
    "candidates": []
  },
  "pityCounters": [],
  "featuredGuarantees": [],
  "pendingRequests": [],
  "history": [],
  "events": []
}
```

초기 premium 값은 Development Mock 플레이 증명용이며 서버 권위 재화가 아니다.
`SERVER_CACHE`에서는 서버 영수증 없이 이 값을 변경할 수 없다.

### 6.2 주점 후보

후보는 candidateId, poolId, generationProfileId, name/appearance/growth seed, displayName,
jobId, gradeId, personalityId, traitIds, hireCost, locked, generatedAtUtc를 가진다. candidateId는
고유·정렬되며 후보 수와 잠금 수는 현재 tavern rule을 만족한다.

### 6.3 이력과 사건

이력은 sequence, operationId, poolId, poolType, paymentType, count, costAmount,
resultInstanceIds, resultGradeIds, guaranteeReasonIds, serverReceiptId, createdAtUtc를 기록한다.
사건 type은 `TavernRefreshed`, `CandidateLockChanged`, `CandidateHired`,
`SpecialRecruitmentCompleted`다.

### 6.4 Migration

Migration은 단방향·결정론적·멱등이다.

1. content.10 schema와 checksum을 검증한다.
2. 기존 authority/serverRevision/wallet/pity/featured/pending을 보존한다.
3. stateVersion, sequence, tavern, history, events를 추가한다.
4. `MOCK_ONLY`이고 모든 Mock 재화가 0인 기존 개발 Save에만 600 free premium과 ticket 3을 준다.
5. gameVersion/contentVersion과 integrity hash를 갱신한다.
6. 원본 document는 변경하지 않는다.

역방향 Migration은 제공하지 않는다. 롤백은 P13 커밋을 되돌리고 content.10 백업 Save를 사용한다.

## 7. 주점 명령

모든 명령은 operationId, expectedRevision, requestHash를 포함한다. hash는 requestHash를 제외한
RFC 8785 canonical JSON SHA-256이다.

### 7.1 `REFRESH_TAVERN`

- free=true면 `now >= nextFreeRefreshAtUtc` 또는 최초 빈 후보 상태여야 한다.
- free=false면 kingdomGold가 paid_refresh_cost 이상이어야 한다.
- 잠긴 후보를 보존하고 나머지만 새로 생성한다.
- 새 nextFreeRefreshAtUtc는 `now + free_refresh_seconds`다.
- 유료면 왕국 골드와 P08 경제 원장의 `TAVERN_REFRESH`가 같은 draft에서 변경된다.

### 7.2 `SET_TAVERN_CANDIDATE_LOCK`

- 후보가 존재하고 고용되지 않았어야 한다.
- 잠금 수가 max_locked를 넘으면 실패한다.
- 동일 값은 성공 replay 결과를 반환하되 사건을 중복 기록하지 않는다.

### 7.3 `HIRE_TAVERN_CANDIDATE`

- 후보 존재, kingdomGold, ownedMercenaryLimit 여유를 모두 확인한다.
- 새 UUIDv7 Instance를 생성하고 P05 validator를 통과시킨다.
- 왕국 골드 차감, 경제 원장 `TAVERN_HIRE`, 로스터 추가, 후보 제거, 이력·사건·journal을
  단 한 번의 Save replace로 commit한다.

검증 실패 또는 Save 실패 시 재화·후보·로스터·이력은 모두 불변이다.

## 8. 특별 모집 Gateway와 천장

### 8.1 Gateway

`IRecruitmentGateway`는 banner/pool, paymentType, count=1, idempotencyKey, requestHash,
expectedServerRevision을 받고 다음 영수증을 반환한다.

- serverReceiptId, serverRevision
- 실제 차감 재화와 잔액
- pity counter와 featured guarantee after-state
- result grade/job/generation seeds/instance public ID
- contentVersion과 resultDigest

`SERVER_CACHE`는 영수증 검증 성공 후에만 캐시와 로스터를 갱신한다. Timeout은 `SENT` 요청을
남기며 같은 Idempotency-Key로 조회/재시도한다. 임의 재호출이나 로컬 재추첨은 금지한다.

`MOCK_ONLY`는 같은 영수증 형태를 로컬 결정론 엔진에서 생성하며 UI에 `개발 Mock`을 표시한다.

### 8.2 천장

각 pull 직전에 `pullCount + 1 >= trigger_count`인 규칙을 찾고 가장 높은 보장 등급을 적용한다.
결과가 rule의 reset 등급 이상이면 해당 counter를 0, 아니면 1 증가시킨다. 같은 pity group을
사용하는 ticket/free/paid pool은 counter를 공유한다. pool 변경·앱 재실행으로 초기화하지 않는다.

### 8.3 직업 확률 상승

S/SS 결과일 때만 rate-up 직업 분포를 적용한다. featured 실패 후 mode가
`NEXT_S_OR_SS_FEATURED`면 다음 S/SS 결과를 featured 직업으로 보장하고 상태를 `NONE`으로
되돌린다. A 결과는 featured guarantee를 소비하지 않는다.

### 8.4 원자성

Mock에서는 재화 차감, pity, featured state, 로스터, pending request, 이력, 사건, journal을 한
Save draft로 commit한다. 서버에서는 wallet ledger, summon transaction/result, pity, mercenary,
outbox가 서버 단일 transaction이며 클라이언트는 영수증 cache만 반영한다.

## 9. operation replay·복구

- 동일 operationId+동일 hash: 최초 resultPayload 반환, 상태 재변경 없음
- 동일 operationId+다른 hash: `P13_OPERATION_HASH_MISMATCH`
- expectedRevision 불일치: `P13_SAVE_REVISION_CONFLICT`
- `PREPARED`: 전송 전 안전 취소 가능
- `SENT`: 동일 Idempotency-Key 상태 조회만 허용
- `RECEIVED`: 영수증 digest 검증 후 한 번만 적용
- 적용 후 앱 종료: operation journal과 history로 완료 상태 복구
- 결과 적용 실패: 영수증을 보존하고 `P13_RECEIPT_APPLY_REQUIRED`로 재시도

## 10. 오류 코드

- `P13_CONTENT_INVALID`, `P13_CONTENT_VERSION_UNSUPPORTED`
- `P13_SAVE_REVISION_CONFLICT`, `P13_OPERATION_HASH_MISMATCH`
- `P13_TAVERN_RULE_NOT_FOUND`, `P13_TAVERN_REFRESH_NOT_READY`
- `P13_CANDIDATE_NOT_FOUND`, `P13_CANDIDATE_LOCK_LIMIT`
- `P13_KINGDOM_GOLD_INSUFFICIENT`, `P13_OWNED_LIMIT_REACHED`
- `P13_POOL_NOT_FOUND`, `P13_PAYMENT_NOT_ALLOWED`, `P13_SPECIAL_COUNT_UNSUPPORTED`
- `P13_PREMIUM_BALANCE_INSUFFICIENT`, `P13_GATEWAY_OFFLINE`
- `P13_REQUEST_IN_PROGRESS`, `P13_RECEIPT_INVALID`, `P13_RECEIPT_APPLY_REQUIRED`
- `P13_MERCENARY_GENERATION_INVALID`, `P13_SAVE_WRITE_FAILED`

모든 검증 실패는 Save와 gateway request state를 변경하지 않는다.

## 11. UI/UX 계약

### 11.1 stable ID

- HUD 진입: `P13_RECRUITMENT_NAV_BUTTON`, 표시 `모집`
- 화면: `P13_RECRUITMENT_SCREEN`
- 탭: `P13_TAB_TAVERN`, `P13_TAB_SPECIAL`, `P13_TAB_HISTORY`
- 주점 갱신: `P13_TAVERN_REFRESH`
- 후보: `P13_CANDIDATE_1`~`P13_CANDIDATE_5`
- 후보 잠금: `P13_CANDIDATE_LOCK_1`~`P13_CANDIDATE_LOCK_5`
- 고용: `P13_HIRE`
- 특별 모집: `P13_SPECIAL_RECRUIT`
- 기록 닫기: `P13_CLOSE`

### 11.2 첫 화면 구조

1. 상단: `왕실 모집소`, 왕국 골드, 티켓, 무료 프리미엄, 숙소 사용량
2. 좌측 rail: 주점/특별 모집/기록 탭과 authority badge
3. 중앙: 실제 후보 3~5명 또는 특별 배너와 등급 확률
4. 우측: 선택 용병 상세, 고용비·잠금·주요 행동
5. 하단: 무료 갱신 남은 시간 또는 S+/SS 천장 진행 bar

주점은 따뜻한 호박색 목재, 특별 모집은 청록·금색 마법 인장으로 구분한다. P10~P12의 다크
브론즈·청록 토큰을 유지하며 후보 카드 중첩을 피한다. 첫 viewport의 주인공은 실제 후보와
천장 수치다. 주요 행동은 화면당 하나만 가장 강하게 표시한다.

### 11.3 상태

- Loading: 입력 차단과 모집 데이터 로딩
- Content: 후보/배너/천장/행동 사용 가능
- Empty: 주점 후보 없음, 무료 갱신 행동 제공
- Error: stable 오류 코드와 재시도
- Offline: 주점은 사용 가능, 특별 모집은 Gateway 상태에 따라 Mock 또는 차단
- Capacity: 숙소 가득 참, 고용·특별 모집 비활성화와 로스터 이동 제공
- Reveal: 1초 TUNABLE 연출, 탭/스킵으로 즉시 결과 표시

모든 버튼은 1920×1080 기준 최소 64×64px, 본문 28px 이상, 핵심 수치 32px 이상이다.
16:9~20:9 Safe Area에서 한국어/영어 overflow와 overlap이 없어야 한다.

## 12. 테스트와 증거

### 12.1 Content/Save

- 91 tables, schema set 9, 모든 pool weight 합 100
- TAVERN C/B/A 및 SPECIAL A/S/SS 경계
- tavern rule 후보·잠금·시간·비용 범위
- pity trigger/grade order/reset/carry-over 참조
- content.10→content.11 before/after/new-game golden
- Migration 원본 불변·멱등·기존 pity/pending 보존

### 12.2 EditMode

- 레벨별 후보 3/4/4/5와 C~A 한정
- 잠긴 후보 refresh 보존, 잠금 상한, 무료 시간, 유료 비용
- 고용비 계산, 숙소 full, 골드 부족, 고유 Instance와 P05 validator
- SPECIAL A/S/SS 한정, pool weight 경계
- S+ 10회, SS 80회, 높은 보장 우선, counter reset·carry-over
- featured 실패 후 다음 S/SS 직업 보장
- operation replay/hash mismatch/CAS
- Save 실패 시 재화·pity·로스터·history 불변

### 12.3 PlayMode·렌더

- Bootstrap→모집 화면 진입
- 후보 선택·잠금·갱신·고용 후 숙소 수 증가
- 특별 모집 결과 reveal과 천장 bar 갱신
- 기록 탭에서 주점/특별 결과 확인
- Loading/Empty/Error/Offline/Capacity
- 1920×1080과 2400×1080 GPU 캡처

### 12.4 CI/Android

- `scripts/ci/p13.sh`가 generator, migration, runtime/gateway/UI entrypoint, 생성 asset, 캡처를 검증한다.
- Git Hook, `scripts/ci/run-ci.sh`, GitHub Actions에 P13 gate를 추가한다.
- Unity 6000.3.20f1에서 P13 EditMode/PlayMode, GPU capture, Android IL2CPP ARM64 Development APK를 실행한다.

## 13. 성능·안전

- 후보는 최대 5, 단일 모집 결과 1, history 100, event 200으로 제한한다.
- 콘텐츠 catalog는 bootstrap에서 한 번 인덱싱하고 프레임마다 CSV/Save를 파싱하지 않는다.
- UI는 DTO만 받고 Save·Gateway를 직접 변경하지 않는다.
- 유료 프리미엄 재화와 특별 모집 RNG는 Production에서 서버 권위다.
- requestHash, idempotency, CAS, 원장·history·receipt digest를 약화하지 않는다.

## 14. 후속 Phase 인계

- P14 레이드 최초 보상은 특별 모집권을 서버 보상 계약으로 지급할 수 있다.
- P15 튜토리얼은 `TAVERN_TUTORIAL_FIRST`를 사용하고 특별 모집을 오프라인 정산하지 않는다.
- P16 서버 연결은 `IRecruitmentGateway`를 HTTP Adapter로 교체하며 command/receipt/idempotency 계약을 보존한다.
- P17 운영 준비는 확률 공시, 결제·환불, 배너 시작/종료, 감사 로그를 검증한다.

## 15. 설계 완결성 감사

| 검토 항목 | 판정 |
|---|---|
| 공식 P13 범위와 후속 Phase 경계 | 해결 |
| 주점 레벨·후보·잠금·갱신·고용 | 확정 |
| 특별 모집 authority와 Mock/Adapter | 확정 |
| 확률·다중 천장·이월·rate-up | 확정 |
| 고유 용병 생성과 P05 로스터 연동 | 확정 |
| 재화·원장·이력·원자성 | 확정 |
| Save schema·Migration·복구 | 확정 |
| replay·CAS·idempotency·receipt | 확정 |
| UI 상태·접근성·화면비 | 확정 |
| 테스트·렌더·Android·CI | 확정 |
| 미해결 항목 | 없음 |

P13 구현을 막는 설계 공백은 없다. 외부 상세 설계서 추가 요청 없이 이 문서로 구현한다.
