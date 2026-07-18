# TYCOON P06 이동·전투·AI 최종 통합 설계 v1.1

## 1. 문서 상태·권위·supersedes

**상태: CONFIRMED**

| 항목 | Exact value |
|---|---|
| 문서 | TYCOON_P06_MOVEMENT_COMBAT_AI_COMPLETE_DESIGN_v1.1.md |
| Phase | P06_MOVEMENT_COMBAT_AI |
| 권위 | P05 구현 보고서/head 3a1179edfb4b29db4fa7dd530a8a02b1f3e55e08 → P05 설계 → P04 → P03 |
| Unity | 6000.3.20f1 (c9ba695d4f07) |
| contentVersion | 1.0.0-content.4 |
| saveVersion/schemaId | 1 / urn:tycoon:save:v1 |
| supersedes | P06 phase 초안의 지역·포션·마법사 미확정 문구 |
| 구현 차단점 | NONE |

모든 수치는 이 문서의 CONFIRMED 또는 TUNABLE 초기값이다. TUNABLE은 content package에서만 변경하고 같은 golden을 재생성한다.

## 2. P06 범위·Phase 경계

**상태: CONFIRMED**

| Capability | P06 behavior | Later behavior | Save owner | UI entry | Unavailable error |
|---|---|---|---|---|---|
| 일반 사냥 | REGION_R01 production StartHunt/Recall | P12가 R02-R05 unlock·assignment·policy | P06 autonomy/records | Kingdom/roster → Region | P06_REGION_NOT_AVAILABLE |
| 포션 | 수량만 읽고 소비 0; 수량 0이면 POTION_LOW 안전 귀환 | P07 실제 소비 | P07 | HUD 수량/귀환 이유 | P06_POTION_RUNTIME_DEFERRED |
| Loot | RewardIntent digest만 생성하고 Save 지급 0 | P07 snapshot/inventory transaction | P07 | 전투 결과에 지급 보류 표시 없음; bounty는 P06 | NONE |
| 5직업 | 4 starters production + test-only Mage fixture | P13이 실제 Mage 모집 | P05/P13 | Mage fixture는 test assembly 전용 | P06_FIXTURE_NOT_PLAYER_VISIBLE |
| 전투 | grid/path/AI/skill/status/threat/contribution | P14 raid orchestration | P06 | Region scene | P06_COMBAT_INVARIANT |
| 오프라인 | background에서 simulation pause, 보상 계산 0 | P15 | P15 | OFFLINE badge | NONE |

R01 unlock은 기존 `regions.progress[REGION_R01].unlocked=true`가 권위다. P06은 region progress를 생성하거나 다른 지역을 열지 않는다. production party는 UI가 명시적으로 선택한 1..4명을 사용한다. Domain은 요청 순서를 formation으로 보존하면서 active, town-safe, unique를 검증한다. 빈 배열은 오류이며 자동 선택은 UI helper에만 존재한다.

## 3. 충돌 해결·correction·migration 요약

**상태: CORRECTION / MIGRATION / CONFIRMED**

| ID | 최종 결정 | Compatibility |
|---|---|---|
| P06-C01 | R01 vertical slice를 production 제공; P12가 R02-R05와 정책 UI 소유 | Save region shape 불변 |
| P06-C02 | P06은 potion 소비·inventory mutation 0; empty potion은 POTION_LOW return | P07 .5부터 활성 |
| P06-C03 | Mage는 `00000000-0000-7006-8000-000000000005` test fixture만 생성 | Save/owned limit/player UI 영향 0 |
| P06-M01 | .3→.4는 contentVersion/revision/integrity만 변경 | payload byte-semantic 동일 |
| P06-C04 | combat session/HP/path/cooldown/status/encounter는 transient | crash load는 IDLE_TOWN 정규화, reward 0 |

## 4. simulation clock·determinism

**상태: CONFIRMED / TUNABLE**

SIM은 10Hz, tick=100ms, frame과 분리한다. catch-up은 frame당 최대5 tick이고 초과 누적은 버리며 metric을 남긴다. pause/background는 SIM을 멈추고 wall clock 보상은 만들지 않는다. command는 `(receivedTick, RECALL=0/START=1/PAUSE=2, operationId UTF-8)` 순서로 tick 시작에 적용한다. entity는 runtimeEntityId, effect는 `(targetId,effectPriority,statusEffectId,sourceId)` 순서다.

RNG는 unsigned SplitMix64 표준 상수와 64-bit wrap을 사용한다. bounded 함수는 `limit=2^64-(2^64 mod bound)`이고 raw가 limit 미만일 때만 `raw mod bound`를 수락한다. 거부 시 다음 raw를 소비한다. Unity Random, GetHashCode, float 확률은 금지한다. encounter root는 SHA-256 UTF-8 `KT|P06_ENCOUNTER_V1|contentVersion|profileId|huntOperationId|encounterIndex`의 앞 8byte big-endian이다.

BUILD_DERIVED soak digest는 최초 승인 실행에서 `docs/goldens/P06/soak_30m.digest.json`으로 커밋한다. 이후 `P06CombatGoldenGenerator --fixture SOAK_30M --check`는 새 expected를 만들지 않고 committed bytes와 비교한다.

## 5. Region world·grid·coordinate·combat geometry

**상태: CONFIRMED**

grid cell은 signed int `(x,y)`, world는 milli-tile signed int다. tile=1000 milli=1 Unity unit, origin=(-12000,-8000). center=`origin+(x*1000+500,y*1000+500)`. inverse는 음수 floor division이다. actor radius=350 milli; blocked cell과 두 actor center distance<700은 통과 불가다. tick 이동량=`floor(moveSpeedMilli/10)`이고 remainder numerator를 actor에 보존해 10 tick 합이 정확히 moveSpeedMilli가 된다. segment가 cell boundary를 넘으면 reservation을 가진 경우만 다음 cell에 들어가며 남은 이동량을 같은 tick에 소비한다.

reservation은 destination cell별 runtimeId 최소가 승자다. A↔B 교차 swap은 둘 다 금지하고 낮은 runtimeId만 대기 없이 기존 cell 유지, 높은 ID도 유지한다. 1-cell corridor 추월은 없다. path는 Manhattan; target/attack/AOE는 squared Euclidean milli distance; range는 `distanceSquared <= rangeMilli^2` 포함 경계다. LOS는 사용하지 않는다.

DASH는 current cell부터 target 방향 A* prefix 최대5 cell을 사용하고 매 tick2 cell, actor 충돌 직전 cell에서 종료한다. 무적 없음. circle AOE는 squared distance<=radius², line AOE는 integer cross-product distance와 forward dot>=0, fan은 dot²*10000 >= len²*cosHalfAngleBps²로 판정한다. 경계 포함, runtimeId tie다. projectile speed=8000 milli/s, tick distance800; impactTick=`launchTick+ceil(distance/800)`. target death 시 locked impact cell에 area면 resolve, single이면 cancel.

골든: speed3600은 tick당360 milli로 10 tick=3600; start center(500,500)에서 east 3 tick 후(1580,500). diagonal target은 A* N/E sequence로만 이동. range1500에서 distance exactly1500 hit,1501 miss. circle radius2400에서 (2400,0) 포함,(2401,0) 제외. corridor swap은 두 actor 모두 원 cell 유지 후 낮은 ID가 다음 tick 우선 예약한다.

## 6. pathfinding·queue·cache·local avoidance

**상태: CONFIRMED / TUNABLE**

A*는 4-way N,E,S,W, edge destination cost10, Manhattan×10이다. heap comparator는 `(f,h,insertionSequence,y,x)`. insertionSequence는 request-local uint64이며 첫 enqueue0, 매 push +1이다. 같은 cell의 tentativeG가 기존보다 작을 때만 g/parent를 교체한다; 같으면 최초 parent를 유지한다. decrease-key 대신 새 entry를 push하고 pop 시 `(entry.g != bestG[cell] or entry.parentVersion != version[cell])`이면 stale로 버린다. closed cell은 consistent heuristic 때문에 재개방하지 않는다.

`A_STAR_TIE`: 5×3, blocked(2,1), start(0,1),goal(4,1). final=`[(0,1),(0,2),(1,2),(2,2),(3,2),(4,2),(4,1)]`, cost60.

| Step | dequeue | enqueued in comparator order |
|---|---|---|
| 0 | (0,1) | (0,2),(1,1),(0,0) |
| 1 | (0,2) | (1,2) |
| 2 | (1,2) | (2,2) |
| 3 | (2,2) | (3,2) |
| 4 | (3,2) | (4,2),(3,1) |
| 5 | (4,2) | (4,1) |
| 6 | (4,1) | goal |

첫 실행 cache miss, expanded7, insertions10이다. 동일 staticGridHash/obstacleRevision 요청은 cache hit, expanded0, 반환 path 동일이다. obstacleRevision+1은 miss다. start=goal은 `[start]`, cost0, expanded0. x=2 전체 blocked는 UNREACHABLE, path empty. queue priority/budget/cache 128 LRU와 replan5/stuck20/max3은 v1.0 값을 유지한다.

## 7. encounter·spawn·entity lifecycle

**상태: CONFIRMED**

Encounter selects one enabled R01 profile by weight in monsterId UTF-8 order, then inclusive wave size. Spawn cells are walkable cells at Manhattan>=6 from party and >=2 from another spawn, ordered y/x; Fisher-Yates consumes one SPAWN draw per selected position. Runtime IDs are `{huntId:N}:{encounterIndex:0000}:{kind M|P}:{ordinal:000}` and are not Save UUIDs. Spawn collision advances to next ordered cell; none causes `P06_ENCOUNTER_SPAWN_EXHAUSTED`. Aggro 7 tiles; leash 12; corpse 10 ticks; VFX 8 ticks. Normal waves do not respawn; after clear executor enters LOOT. All party down ends hunt with zero uncommitted bounty, every member INJURED in memory, then terminal Save sets INJURED/HP_LOW. Pools: 60 entity views, 64 health bars, 96 projectiles, 128 effects. Exhaustion suppresses cosmetic effect first; entity pool exhaustion aborts encounter and returns safely.

## 8. autonomy 17-state transition matrix·CSV authority

**상태: CONFIRMED**

`autonomy_rules.csv`가 상태 전이의 단일 데이터 권위다. evaluator 순서는 hard safety(inactive/injury/invalid target) → phase gate(P07/P08/P11/P14 owned state) → enabled rule priority 내림차순/rule_no 오름차순 → state fallback이다. DSL value empty는 null operand, decimal은 invariant culture, boolean은 TRUE/FALSE다. 동일 priority는 rule_no가 작다.

StartHunt transaction은 selected party 검증 후 memory-only IDLE→PREPARE 이벤트, PREPARE rule 평가, persisted TRAVEL_TO_REGION을 한 revision에 쓴다. PREPARE는 checkpoint가 아니다. potion0 rule은 TRAVEL/POTION_LOW, RETURN_TOWN arrived는 IDLE/NONE이다. COMBAT/LOOT inventory ratio row는 .4에서 disabled다.

```csv
state,rule_no,priority,condition_type,condition_value,reason_code,next_state,status,enabled
IDLE_TOWN,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
IDLE_TOWN,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
IDLE_TOWN,3,80,HAS_ACTIVE_ASSIGNMENT,,POLICY,PREPARE,TUNABLE,TRUE
IDLE_TOWN,4,0,ALWAYS,,NONE,IDLE_TOWN,TUNABLE,TRUE
PREPARE,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
PREPARE,2,90,POTION_COUNT_LTE,0,POTION_LOW,TRAVEL_TO_REGION,TUNABLE,TRUE
PREPARE,3,0,ALWAYS,,POLICY,TRAVEL_TO_REGION,TUNABLE,TRUE
TRAVEL_TO_REGION,1,0,ARRIVED_REGION,,POLICY,FIND_TARGET,TUNABLE,TRUE
FIND_TARGET,1,100,TARGET_AVAILABLE,,TARGET_FOUND,COMBAT,TUNABLE,TRUE
FIND_TARGET,2,0,NO_TARGET_AVAILABLE,,POLICY,RETURN_TOWN,TUNABLE,TRUE
COMBAT,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
COMBAT,2,90,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,CONFIRMED,FALSE
COMBAT,3,80,TARGET_DEFEATED,,LOOT_COMPLETE,LOOT,TUNABLE,TRUE
COMBAT,4,0,ALWAYS,,POLICY,COMBAT,TUNABLE,TRUE
LOOT,1,100,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,CONFIRMED,FALSE
LOOT,2,0,ALWAYS,,LOOT_COMPLETE,CONTINUE_DECISION,TUNABLE,TRUE
CONTINUE_DECISION,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,2,90,POTION_COUNT_LTE,0,POTION_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,3,0,ALWAYS,,POLICY,FIND_TARGET,TUNABLE,TRUE
RETURN_TOWN,1,0,ARRIVED_TOWN,,NONE,IDLE_TOWN,TUNABLE,TRUE
SELL_LOOT,1,100,HAS_INJURY,,HP_LOW,HEAL,TUNABLE,TRUE
SELL_LOOT,2,0,ALWAYS,,LOOT_COMPLETE,EVALUATE_EQUIPMENT,TUNABLE,TRUE
HEAL,1,100,INJURY_CLEARED,,POLICY,BUY_CONSUMABLES,TUNABLE,TRUE
HEAL,2,0,ALWAYS,,POLICY,HEAL,TUNABLE,TRUE
BUY_CONSUMABLES,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,EVALUATE_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,1,100,UPGRADE_PURCHASE_AVAILABLE,0.05,POLICY,BUY_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
EVALUATE_EQUIPMENT,3,0,ALWAYS,,POLICY,PREPARE,TUNABLE,TRUE
BUY_EQUIPMENT,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,PREPARE,TUNABLE,TRUE
PROMOTION_READY,1,100,PROMOTION_REVIEW_STARTED,,PROMOTION_AVAILABLE,PROMOTION_PROCESS,TUNABLE,TRUE
PROMOTION_READY,2,0,ALWAYS,,PROMOTION_AVAILABLE,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,1,100,PROMOTION_REVIEW_COMPLETE,,POLICY,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,2,0,ALWAYS,,POLICY,PROMOTION_PROCESS,TUNABLE,TRUE
INJURED,1,100,IS_IN_TOWN,,HP_LOW,HEAL,TUNABLE,TRUE
INJURED,2,0,ALWAYS,,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
RAID_READY,1,100,PLAYER_RECALL_ACTIVE,,PLAYER_RECALL,IDLE_TOWN,TUNABLE,TRUE
RAID_READY,2,0,ALWAYS,,POLICY,RAID_READY,TUNABLE,TRUE
```

17-state 행렬은 위 CSV 결과를 projection하며 owner phase state는 phase gate가 IDLE_TOWN으로 normalize한다. persisted transient state load는 한 revision IDLE/NONE 정규화, settlement0이다.

## 9. combat stat·growth·Trait·Personality formula

**상태: CONFIRMED / TUNABLE**

growthSeed is decimal uint64 0..18446744073709551615. Stat order is `MAX_HP,ATTACK,DEFENSE,HEAL_POWER,ATTACK_SPEED,MOVE_SPEED,RANGE,CRIT_CHANCE,CRIT_MULTIPLIER,ACCURACY,EVASION,THREAT_MULTIPLIER,STATUS_POWER,STATUS_RESIST`. Per stat bytes are `UTF8(KT|P06_GROWTH_V1|1.0.0-content.4|) + growthSeed unsigned 8-byte big-endian + UTF8(|STAT)`. SHA-256 first8 big-endian is SplitMix64 initial state. One accepted bounded1001 draw produces `growthFactorBps=9500+value`; rejection consumes subsequent draws in the same stream. Stat streams are independent and vector generation visits the listed order.

For each job base row, `x=floor(base*levelFactor/10000)`, then grade, rank, growth with floor after every multiply. levelFactor=`10000+(level-1)*300`; grade C/B/A/S/SS=`9000/10000/11200/12800/15000`; rank Apprentice/Regular/Skilled/Elite/Hero/Legend=`10000/10800/11800/13000/14500/16500`. Apply additive flat equipment, additive bps trait/refine/personality, then multiplicative channel modifier. Checked Int64; HP/attack/defense/heal clamp1..2e9, speed100..5000, chance0..10000, crit multiplier10000..30000.

| Job | Level | Grade | Rank | growthSeed | Final stat vector |
|---|---|---|---|---|---|
| JOB_WARRIOR | 1 | GRADE_C | RANK_APPRENTICE | 3001 | MAX_HP=552,ATTACK=48,DEFENSE=27,HEAL_POWER=0,ATTACK_SPEED=880,MOVE_SPEED=3159,RANGE=1410,CRIT_CHANCE=729,CRIT_MULTIPLIER=13921,ACCURACY=8360,EVASION=459,THREAT_MULTIPLIER=8613,STATUS_POWER=9277,STATUS_RESIST=431 |
| JOB_GUARDIAN | 10 | GRADE_B | RANK_REGULAR | 3002 | MAX_HP=1035,ATTACK=48,DEFENSE=64,HEAL_POWER=0,ATTACK_SPEED=1189,MOVE_SPEED=4359,RANGE=1892,CRIT_CHANCE=711,CRIT_MULTIPLIER=21028,ACCURACY=12100,EVASION=560,THREAT_MULTIPLIER=20785,STATUS_POWER=13854,STATUS_RESIST=1316 |
| JOB_ARCHER | 20 | GRADE_A | RANK_SKILLED | 3003 | MAX_HP=909,ATTACK=123,DEFENSE=38,HEAL_POWER=0,ATTACK_SPEED=2381,MOVE_SPEED=7959,RANGE=13119,CRIT_CHANCE=2561,CRIT_MULTIPLIER=34486,ACCURACY=19489,EVASION=1832,THREAT_MULTIPLIER=16462,STATUS_POWER=23350,STATUS_RESIST=633 |
| JOB_MAGE | 30 | GRADE_S | RANK_ELITE | 3004 | MAX_HP=1164,ATTACK=205,DEFENSE=53,HEAL_POWER=35,ATTACK_SPEED=2986,MOVE_SPEED=10974,RANGE=19335,CRIT_CHANCE=2744,CRIT_MULTIPLIER=46493,ACCURACY=28799,EVASION=1958,THREAT_MULTIPLIER=21225,STATUS_POWER=39277,STATUS_RESIST=1896 |
| JOB_CLERIC | 40 | GRADE_SS | RANK_HERO | 3005 | MAX_HP=2323,ATTACK=153,DEFENSE=107,HEAL_POWER=249,ATTACK_SPEED=4168,MOVE_SPEED=16501,RANGE=24830,CRIT_CHANCE=3409,CRIT_MULTIPLIER=69549,ACCURACY=46401,EVASION=2376,THREAT_MULTIPLIER=40036,STATUS_POWER=56047,STATUS_RESIST=3819 |


| Trait | semantic/bps | Stage/target | Before→after golden |
|---|---|---|---|
| TRAIT_STRONG | STR_PERCENT/600 | ATTACK multiplicative | ATTACK100→106 |
| TRAIT_STURDY | HP_PERCENT/800 | MAX_HP multiplicative | MAX_HP1000→1080 |
| TRAIT_KEEN_EYE | CRIT_CHANCE/400 | CRIT_CHANCE additive | 800→1200 |
| TRAIT_ARCANE | MAGIC_DAMAGE/700 | magic damage channel multiplier | damage100→107 |
| TRAIT_LUCKY | RARE_FIND/300 | P07 rare probability multiplier sidecar | 10000→10300 |
| TRAIT_SCAVENGER | MATERIAL_BONUS/500 | P07 material quantity chance sidecar | 10000→10500 |
| TRAIT_BOSS_HUNTER | BOSS_DAMAGE/600 | P14 boss damage channel | 100→106 |
| TRAIT_POISON_RESIST | POISON_RESIST/1200 | typed status resist additive | 0→1200 |
| TRAIT_FROSTBORN | FROST_RESIST/1200 | typed status resist additive | 0→1200 |
| TRAIT_SURVIVOR | RETURN_HP_BONUS/800 | return threshold additive | 3000→3800 |


| Personality | buyBps | riskBps | returnHpBps | equipThresholdBps | lootRetentionBps | Exact behavior |
|---|---|---|---|---|---|---|
| PERSONALITY_PRACTICAL | 10000 | 10000 | 3000 | 500 | 10000 | balanced; no stat delta |
| PERSONALITY_FRUGAL | 12000 | 9000 | 3500 | 600 | 10000 | consumable only below3500 |
| PERSONALITY_GEARHEAD | 8200 | 10000 | 3000 | 410 | 10000 | equipment candidate score +500bps |
| PERSONALITY_BRAVE | 10000 | 11800 | 2200 | 500 | 10000 | finish feature weight ×11800 |
| PERSONALITY_CAUTIOUS | 10500 | 8200 | 4200 | 525 | 10000 | survival/ally-danger weight ×11800 |
| PERSONALITY_COLLECTOR | 10000 | 9500 | 3200 | 500 | 10500 | rare/material retention score +500bps |


Same seed/stat bytes always produce the same raw and vector. Example growthSeed3001/ATTACK seed=`7427919417722131004`, raw=`[14217133234673237858]`, factor=`10490`.

## 10. basic attack·skill·status·passive runtime

**상태: CONFIRMED / TUNABLE**

Basic attack coefficient=10000bps. Initial basic ready tick=spawnTick+1. Range comes from job profile, interval=`max(1,ceil(10000/attackSpeedMilli))`. Warrior/Guardian/Archer physical; Mage magic; Cleric physical. Cast starts only in inclusive range; cooldown starts on successful impact, cancel before impact consumes none. Target death or range exit before melee impact cancels and retargets next tick; projectile single target cancels on death. Basic can hit/crit and uses the common armor/min1 formula.

Hostile status chance=`clamp(500,9500,baseChanceBps+source.STATUS_POWER-target.STATUS_RESIST-typedResist-bossResistanceBps)`. IMMUNE consumes no draw; otherwise exactly one STATUS stream bounded10000 draw, success `< chance`. Base chance POISON7000,BURN7500,SLOW8000,TAUNT10000. Friendly BARRIER/BLESSING chance10000 and consume0 RNG. Duration ticks=`max(1,floor(baseSeconds*10*clamp(5000,15000,10000+power-resist)/10000))`. magnitude uses the same factor and floor.

DOT snapshot fields=`effectId,sourceId,channel,attackSnapshot,coefficientBps,stackCount,nextTick,expiresTick`; no crit, defense applies once per tick, min1. HOT snapshot uses healPower/coefficient, effective heal only, no overheal. SLOW modifies MOVE_SPEED only, MAX_VALUE retains largest bps, final move clamp100. BARRIER amount=`floor((HEAL_POWER+MAX_HP/10)*effectValueBps/10000)`, REPLACE_HIGHER, absorbs oldest expiry then sourceId. BLESSING adds ATTACK/DEFENSE 1000bps in additive-bps stage, no stack, refresh. TAUNT threat max+1 and target lock; unreachable3 ticks or source death clears.

Per tick order is impact damage/heal→status chance/apply/refresh→existing due status tick→expire→death. Refresh at a due tick replaces schedule before old tick, so it does not double tick.

| Tick | Order | BURN state |
|---|---|---|
| 0 | apply after impact | snapshot; nextTick10; expires80 |
| 10 | DOT tick | damage; next20 |
| 20 | DOT tick | damage; next30 |
| 30 | impact applies REFRESH before status-tick phase | expires110; nextTick40; no old tick30 |
| 40 | DOT tick | damage |
| 50 | DOT tick | damage |
| 60 | DOT tick | damage |
| 70 | DOT tick | damage |
| 80 | DOT tick | damage |
| 90 | DOT tick | damage |
| 100 | DOT tick | damage |
| 110 | DOT tick then expire | removed |



Passives: WAR_TENACITY triggers once when HP crosses from >3000 to <=3000bps, adds DEFENSE2000bps until HP>3000, no cooldown/RNG; GUA_GUARD_MARK continuously selects lowest `(hpRatio,instanceId)` ally below5000 and redirects 2500bps post-shield damage to Guardian, clears on guardian down; ARC_HUNTER_EYE always adds ACCURACY1000 and CRIT_CHANCE1000 in additive stage; MAG_MANA_FLOW always adds STATUS_POWER1500. Passive evaluation occurs after base/equipment and before active decision; events sort passive skillId.

## 11. 5-job AI feature integer contract

**상태: CONFIRMED / TUNABLE**

| Feature | Input | Target | Integer formula | Round | Clamp | Tie | Cache |
|---|---|---|---|---|---|---|---|
| lowHP | target current/max HP | enemy/ally | 10000-floor(hp*10000/max) | floor | 0..10000 | runtimeId | tick; HP change |
| threat | target threat/max threat | enemy | max=0?0:floor(value*10000/max) | floor | 0..10000 | runtimeId | tick; threat event |
| weakness | channel multiplier table | enemy | clamp((multiplierBps-10000)*5,0,10000) | floor | 0..10000 | runtimeId | encounter; status/profile |
| cluster | enemies within AOE radius | cluster | count<=1?0:min(10000,(count-1)*2500) | exact | 0..10000 | centroid x,y then IDs | tick; move/death |
| allyDanger | missingHP+incoming threat | ally | clamp(lowHP+floor(incomingThreatBps/2),0,10000) | floor | 0..10000 | ally instanceId | tick; HP/threat |
| distanceBand | distance d,min,max | enemy | inside=10000; outside=max(0,10000-floor(gap*10000/max(1,max))) | floor | 0..10000 | runtimeId | tick; movement |
| finish | targetHP/actor expectedDamage | enemy | expected>=hp?10000:min(9999,floor(expected*10000/hp)) | floor | 0..10000 | runtimeId | tick; stat/HP |

Each feature golden: zero input→0, half ratio→5000, max→10000. Equal total score keeps current target if hysteresis not met; otherwise lower runtimeId. Cleric candidate order is ally HEAL effective amount>0 → ally BARRIER → party BLESSING → escape movement → enemy basic attack; absence of ally candidates never treats enemy as heal target. Other five-job weights and priority CSV remain authoritative.

## 12. threat·survival·injury·return

**상태: CONFIRMED**

Damage threat=`effectiveDamage*attackerThreatMultiplier/10000`; effective heal threat=`effectiveHeal*5000/10000` distributed equally with remainder to lowest monster runtimeId; taunt sets taunter to currentMax+1 and locks target for STATUS_TAUNT duration. Every tick after no hostile action for 30 ticks, each threat=`floor(threat*9950/10000)`; tie higher threat then lower runtimeId. HP0 becomes DOWNED, untargetable, no permanent death. Encounter victory revives downed at 1 HP and forces RETURN_TOWN/SURVIVAL_LOW; wipe returns all INJURED. P06 has no timed healing, so INJURED persists for P08.

Return HP threshold base 3000bps; CAUTIOUS 4000, BRAVE 2000, PRACTICAL 3000, FRUGAL 3000. Survival score=`hpRatioBps + livingAllies*1000 - hostileCount*750`; below 2500 returns. PLAYER_RECALL is applied at next tick before AI; current cast cancels, enemies cease new aggro, party paths to exit. Empty potion never consumes and causes return at CONTINUE_DECISION; inventory-full condition is false in P06 because loot is transient. Emergency warp after path retry preserves no uncommitted combat reward. Return terminal clears region/target and sets nextDecisionAtUtc=trusted now.

## 13. contribution·records·personal gold·terminal settlement

**상태: CONFIRMED / TUNABLE**

P06 settlement owner is `P06HuntTerminalService`; P07 replaces only materialization through the shared `ICommitHuntSettlement` seam. Encounter records and loot intent are transient. `huntCount` increments once per deployed member only when the hunt has at least one completed encounter; killCount uses total attributed kills; contribution and bounty aggregate across the hunt. RETURN_TOWN arrival invokes one atomic terminal commit, revision+1, operationType=REWARD one row keyed by huntOperationId. P06-only build has zero item/equipment/potion grant; P07 build includes the full PendingLootBuffer. Process death before terminal commit loses all transient data and shows no result screen.

### Shared event/transaction order

1. command 수신 후 operationId lookup; 동일 requestHash의 COMMITTED/ACKNOWLEDGED는 stored result를 반환하고 다른 hash는 split-brain 오류다.
2. expectedRevision, contentVersion, active/town-safe/capacity guard를 검증한다.
3. tick 시작에 entity·catalog·policy·inventory revision의 immutable input snapshot을 만든다.
4. runtimeEntityId 순으로 AI feature와 candidate를 계산하고 결정을 고정한다.
5. path reservation과 fixed-point movement를 적용한다.
6. basic attack/cast를 시작하고 cost·cooldown 시작 조건을 기록한다.
7. impact tick에 hit→crit→shield→damage 또는 effective heal을 적용한다.
8. 생존 대상에 status chance를 draw하고 apply/refresh/replace한다.
9. 기존 status tick을 처리한 뒤 duration 0 effect를 expire한다.
10. HP 0, kill attribution, passive death reactions을 처리한다.
11. hostile 0이면 encounter terminal record를 고정한다.
12. loot intent를 draw하여 PendingLootBuffer에 canonical order로 추가한다. persistent write는 0회다.
13. autonomy rule CSV로 CONTINUE_DECISION 또는 RETURN_TOWN을 선택한다.
14. RETURN_TOWN 도착이면 CommitHuntSettlement를 정확히 한 번 호출한다.
15. clone mutation→semantic validation→REWARD journal COMMITTED→revision+1→RFC8785 digests→atomic Save 후 domain event를 publish한다.

### Shared compatibility table

| 계약 | 권위 문서/모듈 | 저장 경로 | 쓰기 시점 | revision | journal | crash 정책 |
|---|---|---|---|---|---|---|
| hunt party | P06 Combat application | payload.mercenaries[].autonomy(state,currentRegionId,targetInstanceId,reasonCode,times); canonical party in START_HUNT result | StartHunt atomic checkpoint | +1 | operationType=REWARD 1 row; purpose START_HUNT | pre-commit kill: old Save; post-commit kill: TRAVEL state then load normalizes to IDLE with zero settlement |
| pending loot | P07 CombatLoot runtime | transient PendingLootBuffer keyed by huntOperationId | each encounter memory only | 0 | 0 | RETURN_TOWN commit 전에 process death: entire buffer lost and never shown |
| hunt settlement | P07 CommitHuntSettlement + P06 terminal seam | inventory, mercenaries records/wallet/equipment/potions/autonomy, operationJournal | RETURN_TOWN exactly once | +1 | operationType=REWARD exactly 1 row keyed by huntOperationId | old candidate or full terminal candidate; COMMITTED replay returns stored digest |
| inventory policy | P05 Kingdom + P07 InventoryPolicyService | payload.kingdom.inventoryPolicies | SetInventoryPolicies; settlement read-only snapshot | +1 for command; 0 for read | operationType=REWARD 1 row for policy command | pre-commit old policy; post-commit new policy; hash mismatch fails |
| potion ownership | P05/P07 PotionTransferService | payload.inventory.warehousePotions and payload.mercenaries[].potions | transfer/return command or terminal consumption | +1 per transfer; included in settlement +1 | operationType=REWARD 1 row per transfer; settlement shares its one row | atomic source/destination quantities; no partial transfer |
| equipment stats | P07 EquipmentProjection → P06 CombatStatPipeline | payload.inventory.equipment + payload.mercenaries[].equipmentSlots; derived cache transient | town equip command or terminal auto-equip | +1 command or included settlement +1 | operationType=REWARD 1 row per manual command; settlement shares its one row | bidirectional links are old or complete; cache rebuilt after load/event |

## 14. P06 content package·localization

**상태: MIGRATION / CONFIRMED**

| file | rowCount | SHA-256 | materialization |
|---|---|---|---|
| asset_register.csv | 33 | bfc8fa900d721b522801bb5ef860aeddc1ecd3fbe669d8ceb9bf3f28a0fd0506 | FINAL CSV IN THIS DOCUMENT |
| autonomy_rules.csv | 37 | 9187e57b2c47af733bcf1db0f7528c0906def85d7fd369d879ade3e28cb3ff17 | FINAL CSV IN THIS DOCUMENT |
| combat_ai_profiles.csv | 5 | bc49e513fbef323ab7b2f3c74a7a10d89f65e67206c163097a09df9455abe599 | FINAL CSV IN THIS DOCUMENT |
| combat_job_profiles.csv | 5 | 10edf82c53e3cf7d1d2cfc533bf8fabfb1d222c28da75136f9076b54da82bbf3 | FINAL CSV IN THIS DOCUMENT |
| condition_group_members.csv | 38 | d07c0ab53279eaf11806cd8e90b3a6415ef704f6e197628205dc77e3df74f2ba | BYTE_COPY_FROM_1.0.0-content.3 |
| condition_groups.csv | 21 | e3b888ec7dc13f63b388a5bcbebcfe0f4f953d97136843f19d5138526ab88dff | BYTE_COPY_FROM_1.0.0-content.3 |
| conditions.csv | 34 | a94dac2c8f5dd2c0da3427d5c1b0e978a48532cda8e7729ead442e92cc6a0fbf | BYTE_COPY_FROM_1.0.0-content.3 |
| content_aliases.csv | 0 | a398fb76d2c1b0698e90bb0e5369d330b013a75f3d568c96a8c4a81e9b24986a | BYTE_COPY_FROM_1.0.0-content.3 |
| currencies.csv | 4 | a2214c4c527aa654dab12449f3e528e9e1b44ea1048b8f9851b13d8cdd53120b | BYTE_COPY_FROM_1.0.0-content.3 |
| enhancement_rules.csv | 10 | 2c9edde3c79aa4588e749f6718f812638b45b7b6f674eddcb937011bc72866ac | BYTE_COPY_FROM_1.0.0-content.3 |
| equipment_job_eligibility.csv | 110 | 7c915d0a9c956cbcc2c2390913af9720e9c825387357cbeacfdcf82e135525d8 | BYTE_COPY_FROM_1.0.0-content.3 |
| equipment_qualities.csv | 5 | 150f6181c6130397d21ac56722b61656caec92ff01a95100336a7fe6795360f1 | BYTE_COPY_FROM_1.0.0-content.3 |
| equipment_quality_weights.csv | 20 | c2bfbc11d56bc2318f56cb0c5d6cf876fa1c080778e599525dfa3bb699d0db5c | BYTE_COPY_FROM_1.0.0-content.3 |
| equipment_templates.csv | 80 | 5a844437f39c496406db16f6604f6e16de8dec31c5c6230eae40848ec4372227 | BYTE_COPY_FROM_1.0.0-content.3 |
| facilities.csv | 8 | 5290ae6f1db2b1a9341b6b017d1cf18114116d44ac2b6645a3cf6b9dc2a731cc | BYTE_COPY_FROM_1.0.0-content.3 |
| facility_construction_rules.csv | 32 | a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5 | BYTE_COPY_FROM_1.0.0-content.3 |
| facility_levels.csv | 32 | 9f1dc6409d5ebdf6426cb29536f86bc23dc15c2776bee83ed4cef347c7e8b9c0 | BYTE_COPY_FROM_1.0.0-content.3 |
| facility_upgrade_materials.csv | 40 | 510dcd986ef754906b9c77da8ece2b0c35a51ef7d088c7492dd8c6dee5a4f9f4 | BYTE_COPY_FROM_1.0.0-content.3 |
| facility_world_assets.csv | 13 | d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f | BYTE_COPY_FROM_1.0.0-content.3 |
| items.csv | 53 | a18b362f3c118304e8a9363b7758015056d1572b496b563b697b2db67061aa2f | BYTE_COPY_FROM_1.0.0-content.3 |
| job_skill_unlocks.csv | 15 | 028a3ff48a9d3f13ab6204c5a2404130dc9a6621a5bd22303519f30715a676a7 | BYTE_COPY_FROM_1.0.0-content.3 |
| jobs.csv | 5 | 72ad4296c33ddf1d6fda43dcd51f9fbdcccdbdc7f4e57f931cbcbc829d5275f8 | BYTE_COPY_FROM_1.0.0-content.3 |
| kingdom_stages.csv | 5 | 87eaf7cd0dec0ec615aeabb3d7b8c7ef815a4948989bc1b7c62188a5ace5cdfe | BYTE_COPY_FROM_1.0.0-content.3 |
| localizations.csv | 868 | 215286bfe6b80a2ed19dc794af1bdc4d6d31e57bbcfc84a8e7b1be760dd47442 | FINAL CSV IN THIS DOCUMENT |
| loot_entries.csv | 88 | 6437c651ce648d298f3ee59a8074ded1b4bb3a6023cd611832d759da409451fb | BYTE_COPY_FROM_1.0.0-content.3 |
| loot_tables.csv | 27 | 2359b43dfd323fbf263062fee98910bee9d2f3fcbfe29b7ae3ffcd922c49a9cd | BYTE_COPY_FROM_1.0.0-content.3 |
| mercenary_appearance_pool_entries.csv | 5 | 74a27c611876c1e969563cc3b4fbb4a4537aa67a0982ba63afa6e8691992ebb7 | BYTE_COPY_FROM_1.0.0-content.3 |
| mercenary_generation_profiles.csv | 1 | 7702394c9a2c24a3e8bdabd83573f485966e3f0ebbb45912e3f7866b51f55533 | BYTE_COPY_FROM_1.0.0-content.3 |
| mercenary_grades.csv | 5 | f4aaff18498325c99836d945be898632e1f7903282e82cff8e21fadb682f6079 | BYTE_COPY_FROM_1.0.0-content.3 |
| mercenary_name_pool_entries.csv | 20 | b3077cf682d4e5aeb0c0ed9d8ce1326640d4a1a0e2c2deb4088ca4f4f18724d9 | BYTE_COPY_FROM_1.0.0-content.3 |
| mercenary_ranks.csv | 6 | 3fbae56a6acab8fa6c2cc44a7e0cafac5b080704470956140e81aea0aeebbd4d | BYTE_COPY_FROM_1.0.0-content.3 |
| monsters.csv | 27 | 92ae3f14fa06328bdedf3682c332aa9e390fe6f8816349d7f9c3aa3012fa0cb8 | BYTE_COPY_FROM_1.0.0-content.3 |
| npc_professions.csv | 4 | bef0f486b935b740e32d5660915226c9e08515bcb8716d5b187c131f4c870722 | BYTE_COPY_FROM_1.0.0-content.3 |
| npc_proficiency_levels.csv | 4 | 125ac69e78cd8ea50d04142e49b3aa178ef2a486d72e191bd0a1bae469a4757a | BYTE_COPY_FROM_1.0.0-content.3 |
| offline_reward_rules.csv | 6 | 36fc0e26c34f03d61c081c1fc1f981503e98997166a26705df157993cb6a54e6 | BYTE_COPY_FROM_1.0.0-content.3 |
| personalities.csv | 6 | 10114e5c65713cdcf7783c1b8d93f61a0f75f244586f16adcfd0d9c02bf41d4e | BYTE_COPY_FROM_1.0.0-content.3 |
| potions.csv | 7 | 22e9dd956e3149e2976dfc6de11635785d9046df5bf3ddd10a50b975b231377b | BYTE_COPY_FROM_1.0.0-content.3 |
| progression_flags.csv | 3 | 06bfcd7f830d060d34a5a984392b7bf78fd9d4ea2093670ca00572b5c842fcc0 | BYTE_COPY_FROM_1.0.0-content.3 |
| promotion_grade_requirements.csv | 6 | 4669a10f62cc2b2ab43509a3f8f68bf6757e774281e39b36b2ce390a03a84e9c | BYTE_COPY_FROM_1.0.0-content.3 |
| raid_difficulties.csv | 6 | cc6fc17186facc92c8a047ae2929bfee7fe0b1459bdb7fe9611d08cd6489506e | BYTE_COPY_FROM_1.0.0-content.3 |
| raid_part_effects.csv | 6 | 05e69d150777edbf6abf0c09d034ba2b251d5f7e1c6537120e452b31a8ef8ee4 | BYTE_COPY_FROM_1.0.0-content.3 |
| raid_parts.csv | 7 | 009baa859fc58ab25650c89ffbc4a9ba36f95e97bfb30ad793679ea13e382942 | BYTE_COPY_FROM_1.0.0-content.3 |
| raids.csv | 2 | 82b1fc2fa049daeb6942ff536ec52507b2efea02389dd38ceb20ca008adb180a | BYTE_COPY_FROM_1.0.0-content.3 |
| random_equipment_tier_specs.csv | 5 | 58307716e9f2f1a217fc46075e4578fc36e5e9586fd62525badee6c6819a1c86 | BYTE_COPY_FROM_1.0.0-content.3 |
| recipe_materials.csv | 183 | 1a27d1d79a6040698eb2950f0f199a22e03db74d189c830f8f0a8cf13c18f9a3 | BYTE_COPY_FROM_1.0.0-content.3 |
| recipe_outputs.csv | 87 | f07d94b866f4be4bcc035261c5a912f583ad544989b7ffe2078f212fea0b4a6b | BYTE_COPY_FROM_1.0.0-content.3 |
| recipes.csv | 87 | 9a99e41c90612f5ae32d5218e40b39ed4a037fb9c3c209ec7521b557c8a8481c | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_pity_groups.csv | 1 | ad5410752817ce425b02dc6aee4a167b78d5d6db51f32422a01c0615c917ed73 | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_pity_rules.csv | 2 | 40662eb78418ef2d32cc6bbea3b3d7d6e6416018fd3b7fa464b38e044957f849 | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_pool_entries.csv | 17 | 37a9fc6166b861bffe1e7077a51a0da6a5698176c1d05538c26b3751ad19f1c3 | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_pools.csv | 6 | 35545b565d7092ff0337d9968fc3b4697b66e98a6d66d93c614c2879ab0368fc | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_rate_up_entries.csv | 0 | bb922b63227eeaaa922b3e98a6ca457c65588890ba745b0f0219c7294a96613d | BYTE_COPY_FROM_1.0.0-content.3 |
| recruitment_rate_up_groups.csv | 0 | 13b199e7d6348e05a15413af28c90c063db68aea0489c88c5af9fa8880e08762 | BYTE_COPY_FROM_1.0.0-content.3 |
| refine_options.csv | 11 | b86ee167473b89b98078429b4fe832650526852b830fc218b9ac377c80ae9866 | BYTE_COPY_FROM_1.0.0-content.3 |
| region_encounter_profiles.csv | 5 | 0d0d1cbfe2386d3d8cb3116e6eead76987756e17053d6bfddee441d35ceb5a4f | FINAL CSV IN THIS DOCUMENT |
| regions.csv | 5 | 80d6c173d7e2379e8b5f914d2d1b6f4551d07dbd03e032e4d15010028824d336 | BYTE_COPY_FROM_1.0.0-content.3 |
| reward_entries.csv | 16 | 8eadd36ca99cb817a95b2f526cbf6c4601253d769c21a086d818829e564d3863 | BYTE_COPY_FROM_1.0.0-content.3 |
| reward_groups.csv | 15 | d26f5793228068d6be1bcaae3944761cc1348a03d16b40e8f502820c01d9d7da | BYTE_COPY_FROM_1.0.0-content.3 |
| runtime_config.csv | 32 | f8d6fd9f8e5e6b58c3c66ce28aad441835419f04961e5179efd914e39342da79 | FINAL CSV IN THIS DOCUMENT |
| skill_runtime_rules.csv | 15 | 7da4a13b14c6b47d79a59459520c7a9b95cfdb6388441b97995879ac52329a9e | FINAL CSV IN THIS DOCUMENT |
| skills.csv | 15 | 4bae7415a237c7ed40a2d8453139eafc8c651f3cd302236173aba70b1b084ef7 | BYTE_COPY_FROM_1.0.0-content.3 |
| status_effects.csv | 6 | 43514690856fce638242b754f6fe1dc2cd98318dc11c5ba71d157f1da4e0b118 | BYTE_COPY_FROM_1.0.0-content.3 |
| trait_job_eligibility.csv | 44 | f2964966cbd9258b85a9ed541ba22ae0d7d51e0146206a4c85575e6b6d5e59e4 | BYTE_COPY_FROM_1.0.0-content.3 |
| traits.csv | 10 | bf211a89b515e166c5bddab3294688ae9fca28ba33877ab7a8d3a8a219af9f86 | BYTE_COPY_FROM_1.0.0-content.3 |
| tutorial_grants.csv | 9 | 5782d9c3da2fdb7d8693c7bdf7f871d74a47d5d53357b6191000909826c4a450 | BYTE_COPY_FROM_1.0.0-content.3 |
| tutorial_steps.csv | 10 | 67ad17e3832c69d303e02db1803e36e401f46b2dedff4f34420cc49e531d3e8b | BYTE_COPY_FROM_1.0.0-content.3 |

```json
{
  "schemaId": "urn:tycoon:content-manifest:v2",
  "contractVersion": 2,
  "contentVersion": "1.0.0-content.4",
  "csvSchemaSetVersion": 3,
  "packageKind": "BASE",
  "baseContentVersion": null,
  "minimumGameVersion": "1.0.0-p06",
  "channel": "DEV",
  "generatedAtUtc": "2026-07-21T00:00:00.000Z",
  "tables": [
    {
      "file": "asset_register.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bfc8fa900d721b522801bb5ef860aeddc1ecd3fbe669d8ceb9bf3f28a0fd0506",
      "rowCount": 33,
      "primaryKey": [
        "asset_id"
      ],
      "fields": [
        {
          "name": "asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "creator",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "source_url",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "version",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "acquired_date",
          "domain": "DATE",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "price_krw",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "license",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "commercial_use",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "modification_allowed",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "credit_required",
          "domain": "BOOL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "used_in",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "notes",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "autonomy_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "9187e57b2c47af733bcf1db0f7528c0906def85d7fd369d879ade3e28cb3ff17",
      "rowCount": 37,
      "primaryKey": [
        "state",
        "rule_no"
      ],
      "fields": [
        {
          "name": "state",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "IDLE_TOWN",
            "PREPARE",
            "TRAVEL_TO_REGION",
            "FIND_TARGET",
            "COMBAT",
            "LOOT",
            "CONTINUE_DECISION",
            "RETURN_TOWN",
            "SELL_LOOT",
            "HEAL",
            "BUY_CONSUMABLES",
            "EVALUATE_EQUIPMENT",
            "BUY_EQUIPMENT",
            "PROMOTION_READY",
            "PROMOTION_PROCESS",
            "INJURED",
            "RAID_READY"
          ]
        },
        {
          "name": "rule_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "priority",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "reason_code",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "POLICY",
            "HP_LOW",
            "POTION_LOW",
            "INVENTORY_FULL",
            "SURVIVAL_LOW",
            "PLAYER_RECALL",
            "TARGET_FOUND",
            "LOOT_COMPLETE",
            "PROMOTION_AVAILABLE"
          ]
        },
        {
          "name": "next_state",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "IDLE_TOWN",
            "PREPARE",
            "TRAVEL_TO_REGION",
            "FIND_TARGET",
            "COMBAT",
            "LOOT",
            "CONTINUE_DECISION",
            "RETURN_TOWN",
            "SELL_LOOT",
            "HEAL",
            "BUY_CONSUMABLES",
            "EVALUATE_EQUIPMENT",
            "BUY_EQUIPMENT",
            "PROMOTION_READY",
            "PROMOTION_PROCESS",
            "INJURED",
            "RAID_READY"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "combat_ai_profiles.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bc49e513fbef323ab7b2f3c74a7a10d89f65e67206c163097a09df9455abe599",
      "rowCount": 5,
      "primaryKey": [
        "job_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "low_hp_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "threat_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weakness_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cluster_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ally_danger_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "distance_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "finish_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "retarget_ticks",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "hysteresis_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "preferred_min_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "preferred_max_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "combat_job_profiles.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "10edf82c53e3cf7d1d2cfc533bf8fabfb1d222c28da75136f9076b54da82bbf3",
      "rowCount": 5,
      "primaryKey": [
        "job_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_hp",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "attack",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "defense",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "heal_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "attack_speed_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "move_speed_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "range_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "crit_chance_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "crit_multiplier_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "accuracy_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "evasion_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "threat_multiplier_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status_power_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status_resist_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "condition_group_members.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d07c0ab53279eaf11806cd8e90b3a6415ef704f6e197628205dc77e3df74f2ba",
      "rowCount": 38,
      "primaryKey": [
        "condition_group_id",
        "member_no"
      ],
      "fields": [
        {
          "name": "condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "member_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "member_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "CONDITION",
            "GROUP"
          ]
        },
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "child_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "negate",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "condition_id"
          ],
          "targetFile": "conditions.csv",
          "targetFields": [
            "condition_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "child_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "condition_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "e3b888ec7dc13f63b388a5bcbebcfe0f4f953d97136843f19d5138526ab88dff",
      "rowCount": 21,
      "primaryKey": [
        "condition_group_id"
      ],
      "fields": [
        {
          "name": "condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "logic",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALL",
            "ANY"
          ]
        },
        {
          "name": "description_key",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "conditions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a94dac2c8f5dd2c0da3427d5c1b0e978a48532cda8e7729ead442e92cc6a0fbf",
      "rowCount": 34,
      "primaryKey": [
        "condition_id"
      ],
      "fields": [
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALWAYS_TRUE",
            "FACILITY_UPGRADE_COUNT",
            "FACILITY_LEVEL",
            "REGION_UNLOCKED",
            "REGION_PROGRESS_PERCENT",
            "MONSTER_KILL_COUNT",
            "MERCENARY_COUNT_AT_RANK_OR_HIGHER",
            "RAID_CLEAR_COUNT",
            "KINGDOM_STAGE_REACHED",
            "RAID_PART_BROKEN",
            "RAID_PART_EXPOSED",
            "TUTORIAL_STEP_COMPLETED"
          ]
        },
        {
          "name": "subject_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SYSTEM",
            "KINGDOM",
            "FACILITY",
            "REGION",
            "MONSTER",
            "MERCENARY_ROSTER",
            "RAID",
            "KINGDOM_STAGE",
            "RAID_PART",
            "TUTORIAL_STEP"
          ]
        },
        {
          "name": "subject_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "subject_sub_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "operator",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "EQ",
            "NE",
            "GTE",
            "LTE",
            "GT",
            "LT"
          ]
        },
        {
          "name": "value_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BOOLEAN",
            "INTEGER",
            "DECIMAL",
            "STABLE_ID"
          ]
        },
        {
          "name": "expected_value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "content_aliases.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a398fb76d2c1b0698e90bb0e5369d330b013a75f3d568c96a8c4a81e9b24986a",
      "rowCount": 0,
      "primaryKey": [
        "entity_type",
        "from_id",
        "from_content_version"
      ],
      "fields": [
        {
          "name": "entity_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "JOB",
            "SKILL",
            "TRAIT",
            "PERSONALITY",
            "MERCENARY_GRADE",
            "MERCENARY_RANK",
            "MERCENARY_GENERATION_PROFILE",
            "ITEM",
            "POTION",
            "EQUIPMENT_TEMPLATE",
            "EQUIPMENT_QUALITY",
            "REFINE_OPTION",
            "FACILITY",
            "NPC_PROFESSION",
            "NPC_PROFICIENCY",
            "KINGDOM_STAGE",
            "REGION",
            "MONSTER",
            "RAID",
            "RAID_PART",
            "RECIPE",
            "REWARD_GROUP",
            "TUTORIAL_STEP",
            "STATUS_EFFECT",
            "RECRUITMENT_POOL",
            "PITY_GROUP",
            "PITY_RULE",
            "RATE_UP_GROUP",
            "CONDITION_GROUP",
            "PROGRESSION_FLAG",
            "RUNTIME_CONFIG"
          ]
        },
        {
          "name": "from_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "from_content_version",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "alias_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "RENAME",
            "MERGE",
            "TOMBSTONE"
          ]
        },
        {
          "name": "to_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "currencies.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a2214c4c527aa654dab12449f3e528e9e1b44ea1048b8f9851b13d8cdd53120b",
      "rowCount": 4,
      "primaryKey": [
        "currency_id"
      ],
      "fields": [
        {
          "name": "currency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "currency_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "KINGDOM",
            "PREMIUM_FREE",
            "PREMIUM_PAID",
            "TICKET"
          ]
        },
        {
          "name": "authority",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "LOCAL",
            "SERVER"
          ]
        },
        {
          "name": "max_balance",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "enhancement_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "2c9edde3c79aa4588e749f6718f812638b45b7b6f674eddcb937011bc72866ac",
      "rowCount": 10,
      "primaryKey": [
        "target_level"
      ],
      "fields": [
        {
          "name": "target_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "success_chance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stone_item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stone_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personal_gold_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "fail_pity_increment",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "destroy_on_fail",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "downrank_on_fail",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "stone_item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_job_eligibility.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "7c915d0a9c956cbcc2c2390913af9720e9c825387357cbeacfdcf82e135525d8",
      "rowCount": 110,
      "primaryKey": [
        "equipment_template_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "equipment_template_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "equipment_template_id"
          ],
          "targetFile": "equipment_templates.csv",
          "targetFields": [
            "equipment_template_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_qualities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "150f6181c6130397d21ac56722b61656caec92ff01a95100336a7fe6795360f1",
      "rowCount": 5,
      "primaryKey": [
        "quality_id"
      ],
      "fields": [
        {
          "name": "quality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stat_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "innate_affix_chance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ui_token",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_quality_weights.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "c2bfbc11d56bc2318f56cb0c5d6cf876fa1c080778e599525dfa3bb699d0db5c",
      "rowCount": 20,
      "primaryKey": [
        "quality_profile_id",
        "quality_id"
      ],
      "fields": [
        {
          "name": "quality_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "quality_id"
          ],
          "targetFile": "equipment_qualities.csv",
          "targetFields": [
            "quality_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "equipment_templates.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5a844437f39c496406db16f6604f6e16de8dec31c5c6230eae40848ec4372227",
      "rowCount": 80,
      "primaryKey": [
        "equipment_template_id"
      ],
      "fields": [
        {
          "name": "equipment_template_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "WEAPON",
            "ARMOR",
            "HELMET",
            "ACCESSORY"
          ]
        },
        {
          "name": "profile",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "base_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "source",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "CRAFT",
            "DROP",
            "BOSS",
            "RAID"
          ]
        },
        {
          "name": "boss_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "boss_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facilities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5290ae6f1db2b1a9341b6b017d1cf18114116d44ac2b6645a3cf6b9dc2a731cc",
      "rowCount": 8,
      "primaryKey": [
        "facility_id"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "operation_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SYSTEM",
            "MANAGED"
          ]
        },
        {
          "name": "required_profession_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "required_profession_id"
          ],
          "targetFile": "npc_professions.csv",
          "targetFields": [
            "profession_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_construction_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5",
      "rowCount": 32,
      "primaryKey": [
        "facility_id",
        "level"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "build_or_upgrade_duration_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cancel_refund_ratio",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "facility_id",
            "level"
          ],
          "targetFile": "facility_levels.csv",
          "targetFields": [
            "facility_id",
            "level"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_levels.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "9f1dc6409d5ebdf6426cb29536f86bc23dc15c2776bee83ed4cef347c7e8b9c0",
      "rowCount": 32,
      "primaryKey": [
        "facility_id",
        "level"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "required_kingdom_stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "build_or_upgrade_kingdom_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "effect_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "required_kingdom_stage_id"
          ],
          "targetFile": "kingdom_stages.csv",
          "targetFields": [
            "stage_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_upgrade_materials.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "510dcd986ef754906b9c77da8ece2b0c35a51ef7d088c7492dd8c6dee5a4f9f4",
      "rowCount": 40,
      "primaryKey": [
        "facility_id",
        "level",
        "item_id"
      ],
      "fields": [
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "facility_id",
            "level"
          ],
          "targetFile": "facility_levels.csv",
          "targetFields": [
            "facility_id",
            "level"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "facility_world_assets.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f",
      "rowCount": 13,
      "primaryKey": [
        "asset_id"
      ],
      "fields": [
        {
          "name": "asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "address",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "asset_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "PREFAB",
            "SPRITE"
          ]
        },
        {
          "name": "width_px",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "height_px",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pivot_x",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pivot_y",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pixels_per_unit",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "state_variant",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BASE",
            "BACKGROUND",
            "PLOT",
            "LOCKED",
            "CONSTRUCTION",
            "STOPPED"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "asset_id"
          ],
          "targetFile": "asset_register.csv",
          "targetFields": [
            "asset_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "items.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "a18b362f3c118304e8a9363b7758015056d1572b496b563b697b2db67061aa2f",
      "rowCount": 53,
      "primaryKey": [
        "item_id"
      ],
      "fields": [
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALCHEMY",
            "BOSS",
            "CRAFT",
            "ENHANCE",
            "MAGIC",
            "MONSTER",
            "ORE",
            "PROMOTION",
            "REFINE",
            "RELIC"
          ]
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "source_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "REGION",
            "RAID",
            "DISMANTLE",
            "ELITE_AND_RAID",
            "PROMOTION_CONTENT"
          ]
        },
        {
          "name": "source_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "sell_price",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "rarity",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stack_limit",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "job_skill_unlocks.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "028a3ff48a9d3f13ab6204c5a2404130dc9a6621a5bd22303519f30715a676a7",
      "rowCount": 15,
      "primaryKey": [
        "job_id",
        "skill_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "skill_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "skill_id"
          ],
          "targetFile": "skills.csv",
          "targetFields": [
            "skill_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "jobs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "72ad4296c33ddf1d6fda43dcd51f9fbdcccdbdc7f4e57f931cbcbc829d5275f8",
      "rowCount": 5,
      "primaryKey": [
        "job_id"
      ],
      "fields": [
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "role",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BRUISER",
            "TANK",
            "RANGED_DPS",
            "AOE_DPS",
            "HEALER"
          ]
        },
        {
          "name": "armor_profile",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HEAVY",
            "LIGHT",
            "CLOTH"
          ]
        },
        {
          "name": "weapon_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "primary_stat",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "secondary_stat",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ai_priority",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "notes_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "notes_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "kingdom_stages.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "87eaf7cd0dec0ec615aeabb3d7b8c7ef815a4948989bc1b7c62188a5ace5cdfe",
      "rowCount": 5,
      "primaryKey": [
        "stage_id"
      ],
      "fields": [
        {
          "name": "stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "active_slots",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "roster_slots",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_level_cap",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "final_raid_unlocked",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "localizations.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "215286bfe6b80a2ed19dc794af1bdc4d6d31e57bbcfc84a8e7b1be760dd47442",
      "rowCount": 868,
      "primaryKey": [
        "locale",
        "text_key"
      ],
      "fields": [
        {
          "name": "locale",
          "domain": "LOCALE",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "text_value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "context",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "loot_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "6437c651ce648d298f3ee59a8074ded1b4bb3a6023cd611832d759da409451fb",
      "rowCount": 88,
      "primaryKey": [
        "loot_table_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "CURRENCY",
            "PERSONAL_GOLD",
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER",
            "PLAYER_EXP",
            "KINGDOM_EXP"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "probability",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "min_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "loot_table_id"
          ],
          "targetFile": "loot_tables.csv",
          "targetFields": [
            "loot_table_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "condition_id"
          ],
          "targetFile": "conditions.csv",
          "targetFields": [
            "condition_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "loot_tables.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "2359b43dfd323fbf263062fee98910bee9d2f3fcbfe29b7ae3ffcd922c49a9cd",
      "rowCount": 27,
      "primaryKey": [
        "loot_table_id"
      ],
      "fields": [
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "draw_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "INDEPENDENT",
            "ONE_WEIGHTED",
            "N_WEIGHTED"
          ]
        },
        {
          "name": "draw_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "mercenary_appearance_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "74a27c611876c1e969563cc3b4fbb4a4537aa67a0982ba63afa6e8691992ebb7",
      "rowCount": 5,
      "primaryKey": [
        "appearance_pool_id",
        "job_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "appearance_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "appearance_asset_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "appearance_asset_id"
          ],
          "targetFile": "asset_register.csv",
          "targetFields": [
            "asset_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_generation_profiles.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "7702394c9a2c24a3e8bdabd83573f485966e3f0ebbb45912e3f7866b51f55533",
      "rowCount": 1,
      "primaryKey": [
        "generation_profile_id"
      ],
      "fields": [
        {
          "name": "generation_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "algorithm_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GENERATED_MERCENARY_V1"
          ]
        },
        {
          "name": "initial_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "appearance_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personality_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "WEIGHTED_ALL",
            "FIXED"
          ]
        },
        {
          "name": "fixed_personality_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "growth_seed_min",
          "domain": "SEED64",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "growth_seed_max",
          "domain": "SEED64",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "initial_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "fixed_personality_id"
          ],
          "targetFile": "personalities.csv",
          "targetFields": [
            "personality_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_grades.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f4aaff18498325c99836d945be898632e1f7903282e82cff8e21fadb682f6079",
      "rowCount": 5,
      "primaryKey": [
        "grade_id"
      ],
      "fields": [
        {
          "name": "grade_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GRADE_C",
            "GRADE_B",
            "GRADE_A",
            "GRADE_S",
            "GRADE_SS"
          ]
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "base_stat_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "growth_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "initial_trait_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "promotion_cost_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tavern_eligible",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "special_pool_eligible",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "ui_color",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "mercenary_name_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "b3077cf682d4e5aeb0c0ed9d8ce1326640d4a1a0e2c2deb4088ca4f4f18724d9",
      "rowCount": 20,
      "primaryKey": [
        "name_pool_id",
        "locale",
        "entry_no"
      ],
      "fields": [
        {
          "name": "name_pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "locale",
          "domain": "LOCALE",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "display_name",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "mercenary_ranks.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "3fbae56a6acab8fa6c2cc44a7e0cafac5b080704470956140e81aea0aeebbd4d",
      "rowCount": 6,
      "primaryKey": [
        "rank_id"
      ],
      "fields": [
        {
          "name": "rank_id",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "RANK_APPRENTICE",
            "RANK_REGULAR",
            "RANK_SKILLED",
            "RANK_ELITE",
            "RANK_HERO",
            "RANK_LEGEND"
          ]
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_region_tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "skill_slot_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "trait_slot_bonus",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_access",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "promotion_to",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "promotion_token_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "base_personal_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "contribution_required",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "additional_condition",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "promotion_to"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "promotion_token_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "monsters.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "92ae3f14fa06328bdedf3682c332aa9e390fe6f8816349d7f9c3aa3012fa0cb8",
      "rowCount": 27,
      "primaryKey": [
        "monster_id"
      ],
      "fields": [
        {
          "name": "monster_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "region_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NORMAL",
            "ELITE",
            "BOSS",
            "RAID"
          ]
        },
        {
          "name": "level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "hp",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "attack",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "defense",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "xp",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "bounty_personal_gold",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "loot_table_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "behavior_tag",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "spawn_weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "region_id"
          ],
          "targetFile": "regions.csv",
          "targetFields": [
            "region_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "loot_table_id"
          ],
          "targetFile": "loot_tables.csv",
          "targetFields": [
            "loot_table_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "npc_professions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bef0f486b935b740e32d5660915226c9e08515bcb8716d5b187c131f4c870722",
      "rowCount": 4,
      "primaryKey": [
        "profession_id"
      ],
      "fields": [
        {
          "name": "profession_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "work_unit",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "primary_effect",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "npc_proficiency_levels.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "125ac69e78cd8ea50d04142e49b3aa178ef2a486d72e191bd0a1bae469a4757a",
      "rowCount": 4,
      "primaryKey": [
        "proficiency_id"
      ],
      "fields": [
        {
          "name": "proficiency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "xp_required",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "speed_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_bonus",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "offline_reward_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "36fc0e26c34f03d61c081c1fc1f981503e98997166a26705df157993cb6a54e6",
      "rowCount": 6,
      "primaryKey": [
        "rule_id"
      ],
      "fields": [
        {
          "name": "rule_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "settlement_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HUNT",
            "FACILITY",
            "NPC_PROFICIENCY",
            "POTION_CONSUMPTION",
            "INJURY_RECOVERY",
            "PROMOTION_REVIEW"
          ]
        },
        {
          "name": "max_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "personalities.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "10114e5c65713cdcf7783c1b8d93f61a0f75f244586f16adcfd0d9c02bf41d4e",
      "rowCount": 6,
      "primaryKey": [
        "personality_id"
      ],
      "fields": [
        {
          "name": "personality_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "buy_threshold_multiplier",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "risk_tolerance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "return_hp_threshold",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "preferred_behavior",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "potions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "22e9dd956e3149e2976dfc6de11635785d9046df5bf3ddd10a50b975b231377b",
      "rowCount": 7,
      "primaryKey": [
        "potion_id"
      ],
      "fields": [
        {
          "name": "potion_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "HEAL_FLAT",
            "CURE_POISON",
            "POISON_RESIST",
            "FROST_RESIST",
            "BOSS_DAMAGE"
          ]
        },
        {
          "name": "effect_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "duration_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "auto_use_condition",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "progression_flags.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "06bfcd7f830d060d34a5a984392b7bf78fd9d4ea2093670ca00572b5c842fcc0",
      "rowCount": 3,
      "primaryKey": [
        "progression_flag_id"
      ],
      "fields": [
        {
          "name": "progression_flag_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "scope",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "PROFILE",
            "KINGDOM"
          ]
        },
        {
          "name": "repeatable",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "promotion_grade_requirements.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "4669a10f62cc2b2ab43509a3f8f68bf6757e774281e39b36b2ce390a03a84e9c",
      "rowCount": 6,
      "primaryKey": [
        "grade_id",
        "from_rank_id",
        "to_rank_id",
        "item_id"
      ],
      "fields": [
        {
          "name": "grade_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "from_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "to_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "from_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "to_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raid_difficulties.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "cc6fc17186facc92c8a047ae2929bfee7fe0b1459bdb7fe9611d08cd6489506e",
      "rowCount": 6,
      "primaryKey": [
        "raid_id",
        "difficulty"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "difficulty",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NORMAL",
            "HARD",
            "CORRUPTED"
          ]
        },
        {
          "name": "recommended_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raid_part_effects.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "05e69d150777edbf6abf0c09d034ba2b251d5f7e1c6537120e452b31a8ef8ee4",
      "rowCount": 6,
      "primaryKey": [
        "effect_id"
      ],
      "fields": [
        {
          "name": "effect_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ABILITY_SCALE",
            "STAT_MODIFIER",
            "STATE_TRANSITION"
          ]
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "raid_parts.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "009baa859fc58ab25650c89ffbc4a9ba36f95e97bfb30ad793679ea13e382942",
      "rowCount": 7,
      "primaryKey": [
        "raid_id",
        "part_id"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "part_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_hp_ratio",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "break_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "break_reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "behavior_change",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "priority_hint_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "priority_hint_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "break_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "break_reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "behavior_change"
          ],
          "targetFile": "raid_part_effects.csv",
          "targetFields": [
            "effect_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "raids.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "82b1fc2fa049daeb6942ff536ec52507b2efea02389dd38ceb20ca008adb180a",
      "rowCount": 2,
      "primaryKey": [
        "raid_id"
      ],
      "fields": [
        {
          "name": "raid_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "boss_monster_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "party_min",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "party_max",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "first_clear_reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "time_limit_sec",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "boss_monster_id"
          ],
          "targetFile": "monsters.csv",
          "targetFields": [
            "monster_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "min_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "first_clear_reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "random_equipment_tier_specs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "58307716e9f2f1a217fc46075e4578fc36e5e9586fd62525badee6c6819a1c86",
      "rowCount": 5,
      "primaryKey": [
        "spec_id"
      ],
      "fields": [
        {
          "name": "spec_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "slot_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ANY",
            "FIXED"
          ]
        },
        {
          "name": "fixed_slot",
          "domain": "STRING",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "job_policy",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ELIGIBLE_ANY",
            "KILLER_JOB",
            "FIXED"
          ]
        },
        {
          "name": "fixed_job_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "quality_profile_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "fixed_job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipe_materials.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "1a27d1d79a6040698eb2950f0f199a22e03db74d189c830f8f0a8cf13c18f9a3",
      "rowCount": 183,
      "primaryKey": [
        "recipe_id",
        "material_no"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "recipe_id"
          ],
          "targetFile": "recipes.csv",
          "targetFields": [
            "recipe_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipe_outputs.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f07d94b866f4be4bcc035261c5a912f583ad544989b7ffe2078f212fea0b4a6b",
      "rowCount": 87,
      "primaryKey": [
        "recipe_id",
        "output_no"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "output_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "EQUIPMENT_TEMPLATE"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "recipe_id"
          ],
          "targetFile": "recipes.csv",
          "targetFields": [
            "recipe_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recipes.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "9a99e41c90612f5ae32d5218e40b39ed4a037fb9c3c209ec7521b557c8a8481c",
      "rowCount": 87,
      "primaryKey": [
        "recipe_id"
      ],
      "fields": [
        {
          "name": "recipe_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "facility_level",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "npc_proficiency_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "craft_seconds",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quality_roll",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "facility_id"
          ],
          "targetFile": "facilities.csv",
          "targetFields": [
            "facility_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "npc_proficiency_id"
          ],
          "targetFile": "npc_proficiency_levels.csv",
          "targetFields": [
            "proficiency_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pity_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "ad5410752817ce425b02dc6aee4a167b78d5d6db51f32422a01c0615c917ed73",
      "rowCount": 1,
      "primaryKey": [
        "pity_group_id"
      ],
      "fields": [
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "SPECIAL",
            "SPECIAL_RATEUP"
          ]
        },
        {
          "name": "carry_over",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "recruitment_pity_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "40662eb78418ef2d32cc6bbea3b3d7d6e6416018fd3b7fa464b38e044957f849",
      "rowCount": 2,
      "primaryKey": [
        "pity_group_id",
        "pity_rule_id"
      ],
      "fields": [
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pity_rule_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "trigger_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "guaranteed_grade_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "reset_on_grade_or_higher_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "guarantee_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GRADE_AT_LEAST",
            "FEATURED_JOB"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pity_group_id"
          ],
          "targetFile": "recruitment_pity_groups.csv",
          "targetFields": [
            "pity_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "guaranteed_grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "reset_on_grade_or_higher_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pool_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "37a9fc6166b861bffe1e7077a51a0da6a5698176c1d05538c26b3751ad19f1c3",
      "rowCount": 17,
      "primaryKey": [
        "pool_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "result_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "GENERATED_MERCENARY",
            "ITEM",
            "EQUIPMENT_TEMPLATE"
          ]
        },
        {
          "name": "result_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "grade_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "job_selection_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "ALL",
            "FIXED",
            "RATE_UP_GROUP"
          ]
        },
        {
          "name": "job_selection_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pool_id"
          ],
          "targetFile": "recruitment_pools.csv",
          "targetFields": [
            "pool_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "grade_id"
          ],
          "targetFile": "mercenary_grades.csv",
          "targetFields": [
            "grade_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_pools.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "35545b565d7092ff0337d9968fc3b4697b66e98a6d66d93c614c2879ab0368fc",
      "rowCount": 6,
      "primaryKey": [
        "pool_id"
      ],
      "fields": [
        {
          "name": "pool_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "pool_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "TAVERN",
            "SPECIAL",
            "SPECIAL_RATEUP"
          ]
        },
        {
          "name": "pity_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "cost_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "KINGDOM_GOLD",
            "TICKET",
            "FREE_PREMIUM",
            "PAID_PREMIUM"
          ]
        },
        {
          "name": "cost_amount",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "starts_at_utc",
          "domain": "UTC_INSTANT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "ends_at_utc",
          "domain": "UTC_INSTANT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "pity_group_id"
          ],
          "targetFile": "recruitment_pity_groups.csv",
          "targetFields": [
            "pity_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "rate_up_group_id"
          ],
          "targetFile": "recruitment_rate_up_groups.csv",
          "targetFields": [
            "rate_up_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_rate_up_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bb922b63227eeaaa922b3e98a6ca457c65588890ba745b0f0219c7294a96613d",
      "rowCount": 0,
      "primaryKey": [
        "rate_up_group_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "rate_up_group_id"
          ],
          "targetFile": "recruitment_rate_up_groups.csv",
          "targetFields": [
            "rate_up_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "recruitment_rate_up_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "13b199e7d6348e05a15413af28c90c063db68aea0489c88c5af9fa8880e08762",
      "rowCount": 0,
      "primaryKey": [
        "rate_up_group_id"
      ],
      "fields": [
        {
          "name": "rate_up_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "featured_share",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "failure_guarantee_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "NONE",
            "NEXT_S_OR_SS_FEATURED"
          ]
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "refine_options.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "b86ee167473b89b98078429b4fe832650526852b830fc218b9ac377c80ae9866",
      "rowCount": 11,
      "primaryKey": [
        "refine_option_id"
      ],
      "fields": [
        {
          "name": "refine_option_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "option_group",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "stat_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_item_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "material_quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "personal_gold_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "material_item_id"
          ],
          "targetFile": "items.csv",
          "targetFields": [
            "item_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "region_encounter_profiles.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "0d0d1cbfe2386d3d8cb3116e6eead76987756e17053d6bfddee441d35ceb5a4f",
      "rowCount": 5,
      "primaryKey": [
        "region_id",
        "monster_id"
      ],
      "fields": [
        {
          "name": "region_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "monster_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "wave_min",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "wave_max",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "spawn_group",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "region_id"
          ],
          "targetFile": "regions.csv",
          "targetFields": [
            "region_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "monster_id"
          ],
          "targetFile": "monsters.csv",
          "targetFields": [
            "monster_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "regions.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "80d6c173d7e2379e8b5f914d2d1b6f4551d07dbd03e032e4d15010028824d336",
      "rowCount": 5,
      "primaryKey": [
        "region_id"
      ],
      "fields": [
        {
          "name": "region_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tier",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unlock_condition_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "kingdom_stage_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "recommended_power",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "max_active",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "environment_tag",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "raid_gate_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "offline_efficiency",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "min_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_condition_group_id"
          ],
          "targetFile": "condition_groups.csv",
          "targetFields": [
            "condition_group_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "kingdom_stage_id"
          ],
          "targetFile": "kingdom_stages.csv",
          "targetFields": [
            "stage_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "raid_gate_id"
          ],
          "targetFile": "raids.csv",
          "targetFields": [
            "raid_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "reward_entries.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "8eadd36ca99cb817a95b2f526cbf6c4601253d769c21a086d818829e564d3863",
      "rowCount": 16,
      "primaryKey": [
        "reward_group_id",
        "entry_no"
      ],
      "fields": [
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "entry_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "POTION",
            "CURRENCY",
            "PERSONAL_GOLD",
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER",
            "PLAYER_EXP",
            "KINGDOM_EXP",
            "REGION_UNLOCK",
            "PROGRESSION_FLAG"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "probability",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "weight",
          "domain": "SAFE_INT",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "reward_group_id"
          ],
          "targetFile": "reward_groups.csv",
          "targetFields": [
            "reward_group_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "reward_groups.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "d26f5793228068d6be1bcaae3944761cc1348a03d16b40e8f502820c01d9d7da",
      "rowCount": 15,
      "primaryKey": [
        "reward_group_id"
      ],
      "fields": [
        {
          "name": "reward_group_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "distribution_mode",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ALL",
            "ONE_PROBABILITY",
            "ONE_WEIGHTED",
            "N_WEIGHTED"
          ]
        },
        {
          "name": "draw_count",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": []
    },
    {
      "file": "runtime_config.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f8d6fd9f8e5e6b58c3c66ce28aad441835419f04961e5179efd914e39342da79",
      "rowCount": 32,
      "primaryKey": [
        "config_key"
      ],
      "fields": [
        {
          "name": "config_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "value_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "INTEGER",
            "DECIMAL",
            "BOOLEAN",
            "STRING"
          ]
        },
        {
          "name": "value",
          "domain": "STRING",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "unit",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "min_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "max_value",
          "domain": "DECIMAL",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "skill_runtime_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "7da4a13b14c6b47d79a59459520c7a9b95cfdb6388441b97995879ac52329a9e",
      "rowCount": 15,
      "primaryKey": [
        "skill_id"
      ],
      "fields": [
        {
          "name": "skill_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cast_range_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cast_time_ticks",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "recovery_ticks",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "target_count",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "radius_milli",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "coefficient_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status_effect_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "effect_value_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "interruptible",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "skill_id"
          ],
          "targetFile": "skills.csv",
          "targetFields": [
            "skill_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "status_effect_id"
          ],
          "targetFile": "status_effects.csv",
          "targetFields": [
            "status_effect_id"
          ],
          "mode": "SOFT_SENTINEL_EMPTY"
        }
      ]
    },
    {
      "file": "skills.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "4bae7415a237c7ed40a2d8453139eafc8c651f3cd302236173aba70b1b084ef7",
      "rowCount": 15,
      "primaryKey": [
        "skill_id"
      ],
      "fields": [
        {
          "name": "skill_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "kind",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ACTIVE",
            "PASSIVE"
          ]
        },
        {
          "name": "unlock_rank_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "target",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "cooldown_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "power_coeff",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "resource_cost",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "condition",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "unlock_rank_id"
          ],
          "targetFile": "mercenary_ranks.csv",
          "targetFields": [
            "rank_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "status_effects.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "43514690856fce638242b754f6fe1dc2cd98318dc11c5ba71d157f1da4e0b118",
      "rowCount": 6,
      "primaryKey": [
        "status_effect_id"
      ],
      "fields": [
        {
          "name": "status_effect_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "BUFF",
            "DEBUFF",
            "CONTROL",
            "INJURY"
          ]
        },
        {
          "name": "stack_rule",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "duration_sec",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "boss_resistance",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "trait_job_eligibility.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "f2964966cbd9258b85a9ed541ba22ae0d7d51e0146206a4c85575e6b6d5e59e4",
      "rowCount": 44,
      "primaryKey": [
        "trait_id",
        "job_id"
      ],
      "fields": [
        {
          "name": "trait_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "job_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "trait_id"
          ],
          "targetFile": "traits.csv",
          "targetFields": [
            "trait_id"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "job_id"
          ],
          "targetFile": "jobs.csv",
          "targetFields": [
            "job_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "traits.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "bf211a89b515e166c5bddab3294688ae9fca28ba33877ab7a8d3a8a219af9f86",
      "rowCount": 10,
      "primaryKey": [
        "trait_id"
      ],
      "fields": [
        {
          "name": "trait_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "name_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "category",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "STAT",
            "COMBAT",
            "COLLECTION",
            "BOSS",
            "RESIST",
            "AI"
          ]
        },
        {
          "name": "effect_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "effect_value",
          "domain": "DECIMAL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "description_text_key",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "name_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        },
        {
          "sourceFields": [
            "description_text_key"
          ],
          "targetFile": "localizations.csv",
          "targetFields": [
            "text_key"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "tutorial_grants.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "5782d9c3da2fdb7d8693c7bdf7f871d74a47d5d53357b6191000909826c4a450",
      "rowCount": 9,
      "primaryKey": [
        "grant_id",
        "line_no"
      ],
      "fields": [
        {
          "name": "grant_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "tutorial_step_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "line_no",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "reward_type",
          "domain": "ENUM",
          "nullable": false,
          "enumValues": [
            "ITEM",
            "CURRENCY",
            "PROGRESSION_FLAG"
          ]
        },
        {
          "name": "reward_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "quantity",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "tutorial_step_id"
          ],
          "targetFile": "tutorial_steps.csv",
          "targetFields": [
            "tutorial_step_id"
          ],
          "mode": "HARD"
        }
      ]
    },
    {
      "file": "tutorial_steps.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "67ad17e3832c69d303e02db1803e36e401f46b2dedff4f34420cc49e531d3e8b",
      "rowCount": 10,
      "primaryKey": [
        "tutorial_step_id"
      ],
      "fields": [
        {
          "name": "tutorial_step_id",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "order",
          "domain": "INT32",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "action_type",
          "domain": "STABLE_ID",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "target_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "prerequisite_step_id",
          "domain": "STABLE_ID",
          "nullable": true,
          "enumValues": []
        },
        {
          "name": "skippable",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "status",
          "domain": "STATUS",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "enabled",
          "domain": "BOOL",
          "nullable": false,
          "enumValues": []
        }
      ],
      "foreignKeys": [
        {
          "sourceFields": [
            "prerequisite_step_id"
          ],
          "targetFile": "tutorial_steps.csv",
          "targetFields": [
            "tutorial_step_id"
          ],
          "mode": "HARD"
        }
      ]
    }
  ]
}
```

| Manifest vector | JCS bytes | SHA-256 |
|---|---|---|
| 1.0.0-content.4 | 72812 | 062f09739be767ea8ea469087ae0a4c4af6630a2cb898be590fb6790888dd5ef |

Writer는 UTF-8(no BOM), RFC 4180, CRLF, 최소 인용, table file UTF-8 ordinal, row PK UTF-8 ordinal을 사용한다. manifest는 RFC 8785 bytes로 digest한다. `--check`는 staging 생성물과 player package를 byte 비교하고 차이가 있으면 종료 코드 1이다. 이전 package는 삭제하거나 수정하지 않는다.

#### `asset_register.csv` final bytes

```csv
asset_id,name,creator,source_url,version,acquired_date,price_krw,license,commercial_use,modification_allowed,credit_required,used_in,notes,status,enabled
ASSET_FACILITY_CONSTRUCTION_V1,Asset Facility Construction V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Construction; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_LOCKED_V1,Asset Facility Locked V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Locked; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_PLOT_V1,Asset Facility Plot V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Plot; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_STOPPED_V1,Asset Facility Stopped V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Stopped; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_ALCHEMY_PLACEHOLDER_V1,Asset Fac Alchemy Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_ALCHEMY; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1,Asset Fac Blacksmith Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_BLACKSMITH; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_GUILD_PLACEHOLDER_V1,Asset Fac Guild Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_GUILD; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_INFIRMARY_PLACEHOLDER_V1,Asset Fac Infirmary Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_INFIRMARY; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_LODGE_PLACEHOLDER_V1,Asset Fac Lodge Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_LODGE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_STORE_PLACEHOLDER_V1,Asset Fac Store Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_STORE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_TAVERN_PLACEHOLDER_V1,Asset Fac Tavern Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_TAVERN; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1,Asset Fac Warehouse Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_WAREHOUSE; generated placeholder,CONFIRMED,TRUE
ASSET_KINGDOM_BACKGROUND_V1,Asset Kingdom Background V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Background; generated placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_ARCHER_V1,Internal Archer Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_CLERIC_V1,Internal Cleric Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_GUARDIAN_V1,Internal Guardian Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_MAGE_V1,Internal Mage Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_WARRIOR_V1,Internal Warrior Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_P06_CHARACTER_ARCHER,P06 Archer Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_ARCHER; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_CLERIC,P06 Cleric Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_CLERIC; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_GUARDIAN,P06 Guardian Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_GUARDIAN; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_MAGE,P06 Mage Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_MAGE; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_WARRIOR,P06 Warrior Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_WARRIOR; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BARRIER,P06 STATUS_BARRIER Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BARRIER; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BLESSING,P06 STATUS_BLESSING Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BLESSING; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BURN,P06 STATUS_BURN Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BURN; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_POISON,P06 STATUS_POISON Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_POISON; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_SLOW,P06 STATUS_SLOW Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_SLOW; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_TAUNT,P06 STATUS_TAUNT Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_TAUNT; logical asset,CONFIRMED,TRUE
ASSET_P06_MONSTER_ELITE,P06 Elite Monster,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 128px sprite,CONFIRMED,TRUE
ASSET_P06_MONSTER_MELEE,P06 Melee Monster,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 96px sprite,CONFIRMED,TRUE
ASSET_P06_PROJECTILE,P06 Projectile,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 24px sprite,CONFIRMED,TRUE
ASSET_P06_TILE_MEADOW,P06 Meadow Tile,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 64px tile,CONFIRMED,TRUE
```

#### `autonomy_rules.csv` final bytes

```csv
state,rule_no,priority,condition_type,condition_value,reason_code,next_state,status,enabled
IDLE_TOWN,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
IDLE_TOWN,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
IDLE_TOWN,3,80,HAS_ACTIVE_ASSIGNMENT,,POLICY,PREPARE,TUNABLE,TRUE
IDLE_TOWN,4,0,ALWAYS,,NONE,IDLE_TOWN,TUNABLE,TRUE
PREPARE,1,100,HAS_INJURY,,HP_LOW,INJURED,TUNABLE,TRUE
PREPARE,2,90,POTION_COUNT_LTE,0,POTION_LOW,TRAVEL_TO_REGION,TUNABLE,TRUE
PREPARE,3,0,ALWAYS,,POLICY,TRAVEL_TO_REGION,TUNABLE,TRUE
TRAVEL_TO_REGION,1,0,ARRIVED_REGION,,POLICY,FIND_TARGET,TUNABLE,TRUE
FIND_TARGET,1,100,TARGET_AVAILABLE,,TARGET_FOUND,COMBAT,TUNABLE,TRUE
FIND_TARGET,2,0,NO_TARGET_AVAILABLE,,POLICY,RETURN_TOWN,TUNABLE,TRUE
COMBAT,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
COMBAT,2,90,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,CONFIRMED,FALSE
COMBAT,3,80,TARGET_DEFEATED,,LOOT_COMPLETE,LOOT,TUNABLE,TRUE
COMBAT,4,0,ALWAYS,,POLICY,COMBAT,TUNABLE,TRUE
LOOT,1,100,INVENTORY_RATIO_GTE,0.9,INVENTORY_FULL,RETURN_TOWN,CONFIRMED,FALSE
LOOT,2,0,ALWAYS,,LOOT_COMPLETE,CONTINUE_DECISION,TUNABLE,TRUE
CONTINUE_DECISION,1,100,HP_RATIO_LTE,0.3,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,2,90,POTION_COUNT_LTE,0,POTION_LOW,RETURN_TOWN,TUNABLE,TRUE
CONTINUE_DECISION,3,0,ALWAYS,,POLICY,FIND_TARGET,TUNABLE,TRUE
RETURN_TOWN,1,0,ARRIVED_TOWN,,NONE,IDLE_TOWN,TUNABLE,TRUE
SELL_LOOT,1,100,HAS_INJURY,,HP_LOW,HEAL,TUNABLE,TRUE
SELL_LOOT,2,0,ALWAYS,,LOOT_COMPLETE,EVALUATE_EQUIPMENT,TUNABLE,TRUE
HEAL,1,100,INJURY_CLEARED,,POLICY,BUY_CONSUMABLES,TUNABLE,TRUE
HEAL,2,0,ALWAYS,,POLICY,HEAL,TUNABLE,TRUE
BUY_CONSUMABLES,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,EVALUATE_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,1,100,UPGRADE_PURCHASE_AVAILABLE,0.05,POLICY,BUY_EQUIPMENT,TUNABLE,TRUE
EVALUATE_EQUIPMENT,2,90,PROMOTION_AVAILABLE,,PROMOTION_AVAILABLE,PROMOTION_READY,TUNABLE,TRUE
EVALUATE_EQUIPMENT,3,0,ALWAYS,,POLICY,PREPARE,TUNABLE,TRUE
BUY_EQUIPMENT,1,0,PURCHASE_DECISION_COMPLETE,,POLICY,PREPARE,TUNABLE,TRUE
PROMOTION_READY,1,100,PROMOTION_REVIEW_STARTED,,PROMOTION_AVAILABLE,PROMOTION_PROCESS,TUNABLE,TRUE
PROMOTION_READY,2,0,ALWAYS,,PROMOTION_AVAILABLE,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,1,100,PROMOTION_REVIEW_COMPLETE,,POLICY,IDLE_TOWN,TUNABLE,TRUE
PROMOTION_PROCESS,2,0,ALWAYS,,POLICY,PROMOTION_PROCESS,TUNABLE,TRUE
INJURED,1,100,IS_IN_TOWN,,HP_LOW,HEAL,TUNABLE,TRUE
INJURED,2,0,ALWAYS,,HP_LOW,RETURN_TOWN,TUNABLE,TRUE
RAID_READY,1,100,PLAYER_RECALL_ACTIVE,,PLAYER_RECALL,IDLE_TOWN,TUNABLE,TRUE
RAID_READY,2,0,ALWAYS,,POLICY,RAID_READY,TUNABLE,TRUE
```

#### `combat_ai_profiles.csv` final bytes

```csv
job_id,low_hp_weight,threat_weight,weakness_weight,cluster_weight,ally_danger_weight,distance_weight,finish_weight,retarget_ticks,hysteresis_bps,preferred_min_milli,preferred_max_milli,status,enabled
JOB_WARRIOR,150,20,40,0,20,-10,180,5,1500,0,1600,TUNABLE,TRUE
JOB_GUARDIAN,30,180,0,20,220,-5,20,5,2000,0,1800,TUNABLE,TRUE
JOB_ARCHER,60,-30,180,30,0,25,80,5,1800,4500,6500,TUNABLE,TRUE
JOB_MAGE,30,-20,40,240,0,20,20,5,1800,3800,6000,TUNABLE,TRUE
JOB_CLERIC,0,-60,0,0,300,15,0,5,2500,4200,5500,TUNABLE,TRUE
```

#### `combat_job_profiles.csv` final bytes

```csv
job_id,max_hp,attack,defense,heal_power,attack_speed_milli,move_speed_milli,range_milli,crit_chance_bps,crit_multiplier_bps,accuracy_bps,evasion_bps,threat_multiplier_bps,status_power_bps,status_resist_bps,status,enabled
JOB_WARRIOR,620,52,32,0,1000,3600,1500,800,15000,9200,500,10000,10000,500,TUNABLE,TRUE
JOB_GUARDIAN,780,38,48,0,900,3200,1400,500,15000,9000,400,15000,10000,1000,TUNABLE,TRUE
JOB_ARCHER,430,58,20,0,1200,3900,6500,1200,16000,9500,900,8000,11000,300,TUNABLE,TRUE
JOB_MAGE,390,66,18,12,950,3500,6000,900,15500,9300,600,7000,12500,600,TUNABLE,TRUE
JOB_CLERIC,470,34,24,54,900,3400,5500,700,15000,9400,500,8500,12000,800,TUNABLE,TRUE
```

#### `localizations.csv` final bytes

```csv
locale,text_key,text_value,context,status,enabled
en-US,TXT_P04_ACTION_ASSIGN,Assign NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BACK,Back,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_BUILD,Build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CANCEL,Cancel,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLAIM,Complete,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CLOSE,Close,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_CONFIRM,Confirm,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_RETRY,Retry,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UNASSIGN,Unassign,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ACTION_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 autosave debounce,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_GOLD,Kingdom Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DEV_CLOCK_DESC,P04 test clock mode,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_DRAWER_DURATION_DESC,Facility drawer animation duration,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_GOLD,Not enough Kingdom Gold.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_NOT_READY,The facility job is not ready.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_JOB_RUNNING,A facility job is already in progress.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MATERIAL,Not enough materials.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_MAX_LEVEL,Maximum level reached.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_ASSIGNED,This NPC is already assigned.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_NONE,No management NPC is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_NPC_PROFESSION,This NPC has the wrong profession.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_PROFILE_AMBIGUOUS,Multiple local kingdoms were found and cannot be selected automatically.,P04_PROFILE,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_PROFILE_DISCOVERY,The local kingdom save location could not be inspected.,P04_PROFILE,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_REVISION,Another change was saved first. Reloading.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_CREATE,Could not save the new kingdom.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_LOAD,Could not load save data.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_SAVE_WRITE,Could not save changes.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_ERROR_STAGE,A higher Kingdom Stage is required.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_GOLD,Gold {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_PREMIUM,Premium {amount},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_RECOVERED,A backup save was recovered.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_HUD_STAGE,Kingdom Stage {stage},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_ALCHEMY,Alchemy unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_BLACKSMITH,Smithing unlocks in P10.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_GUILD,Guild requests unlock in P11.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_INFIRMARY,Treatment unlocks in P09.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_STORE,Trading unlocks in P08.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_TAVERN,Recruitment unlocks in P13.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_PHASE_WAREHOUSE,Warehouse operations unlock in P07.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_LOCKED_STAGE,Unlocks at Kingdom Stage {stage}.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_ASSIGN_TITLE,Assign Management NPC,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_BUILD_TITLE,Build Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_MODAL_UPGRADE_TITLE,Upgrade Facility,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_CRAFT,Craft,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_KINGDOM,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MENU,Menu,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_MERCENARIES,Mercenaries,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_RECRUIT,Recruit,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_NAV_REGION,Region,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_ACTIVE,Active,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDABLE,Ready to build,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_BUILDING,Building,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_LOCKED,Locked,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_STOPPED,Stopped,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STATE_UPGRADING,Upgrading,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_STOP_NPC_REQUIRED,Assign the required management NPC to activate.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OPERATIONS,Operations,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_OVERVIEW,Overview,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_STAFF,Staff,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TAB_UPGRADE,Upgrade,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIMER_TICK_DESC,Facility remaining-time display interval,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_FINISHES,Finishes at {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_TIME_REMAINING,Time remaining: {time},P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_CONTENT,Kingdom,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_EMPTY,No facility data is available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_ERROR,Kingdom cannot be displayed.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOADING,Loading kingdom…,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_LOCKED,This facility is locked.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P04_UI_OFFLINE,Offline: local facility management remains available.,P04_UI,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_ACTIVATE,Set Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLEAR_SEARCH,Clear Search,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_CLOSE,Close,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_DEACTIVATE,Set Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RESET_FILTERS,Reset Filters,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTION_RETRY,Retry,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ACTIVE_COUNT,Active {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_CONSUMABLES,Buy Consumables,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_BUY_EQUIPMENT,Buy Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_COMBAT,Combat,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_CONTINUE_DECISION,Continue Decision,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,Evaluate Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_FIND_TARGET,Find Target,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_HEAL,Heal,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_IDLE_TOWN,Idle Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_INJURED,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_LOOT,Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PREPARE,Prepare,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_PROCESS,Promotion Process,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_PROMOTION_READY,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RAID_READY,Raid Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_RETURN_TOWN,Return Town,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_SELL_LOOT,Sell Loot,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,Travel To Region,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_COMBAT_STATS,Combat stats unlock in P06.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_EQUIPMENT,Equipment changes unlock in P07.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_PROMOTION,Promotion review unlocks in P11.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_DEFER_RECRUITMENT,Recruitment unlocks in P13.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_ACTIVE_LIMIT,All active mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_LOAD,Could not load mercenary save data.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_NOT_FOUND,Mercenary not found.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_OWNED_LIMIT,All owned mercenary slots are full.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_REVISION,Another change was saved first. Reloading.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_SAVE,Could not save mercenary changes.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ERROR_STATE_FORBIDDEN,Activity cannot be changed in the current state.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACCESSORY,Accessory,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ACTION_REASON,Action Reason,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ARMOR,Armor,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CONTRIBUTION,Contribution {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_CURRENT_ACTION,Current Action,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_EXP,EXP {exp},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_GRADE,Base Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HELMET,Helmet,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_HUNT_COUNT,Hunts {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_ITEMS_COLLECTED,Items Collected {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_KILL_COUNT,Kills {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_LEVEL,Level {level}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONALITY,Personality,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_PERSONAL_GOLD,Personal Gold {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_POTIONS,Potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RAID_CLEAR,Raid Clears {amount},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_RANK,Growth Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_TRAITS,Traits,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FIELD_WEAPON,Weapon,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ACTIVE,Activity,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_ALL,All,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_INJURY,Injured,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_JOB,Job,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_NO_RESULTS,No mercenaries match these filters.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_PROMOTION,Promotion Ready,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_FILTER_STATUS,Status,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_HP_LOW,Low HP,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_INVENTORY_FULL,Inventory full,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_LOOT_COMPLETE,Loot complete,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_NONE,None,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PLAYER_RECALL,Player recall,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POLICY,Kingdom policy,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_POTION_LOW,Low potions,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_PROMOTION_AVAILABLE,Promotion available,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_SURVIVAL_LOW,Low survival chance,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_REASON_TARGET_FOUND,Target found,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_ROSTER_COUNT,Owned {current}/{max},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SEARCH_PLACEHOLDER,Search mercenary name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SLOT_SOURCE,Mercenary Lodge Level {level},P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_ASC,Ascending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DEFAULT,Default Order,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_DESC,Descending,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_GRADE,Grade,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_LEVEL,Level,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_NAME,Name,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_SORT_RANK,Rank,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_ACTIVE,Active,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_STATUS_INACTIVE,Inactive,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_EQUIPMENT,Equipment,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_GROWTH,Growth,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_OVERVIEW,Overview,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TAB_RECORDS,Records,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_TITLE,Mercenaries,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_CONTENT,Mercenary Roster,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_EMPTY,No mercenaries are owned.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_ERROR,Mercenary data cannot be displayed.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOADING,Loading mercenaries…,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_LOCKED,The mercenary roster is still locked.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P05_UI_OFFLINE,Offline: local roster management remains available.,P05_ROSTER,CONFIRMED,TRUE
en-US,TXT_P06_COMBAT_RESULT,Kills {kills} · Contribution +{contribution} · Personal Gold +{gold},P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_CONTENT_MISSING,Combat content is invalid.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_DISABLED_INACTIVE,Only active mercenaries can deploy.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_DISABLED_STATE,Only town-idle mercenaries can deploy.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_EMPTY,No active mercenary can deploy.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_ERROR,Combat cannot continue.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_LOADING,Loading region…,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_LOCKED,The first region is locked.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_NO_POTION,Returning safely because no potion is available.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_OFFLINE,Offline: normal hunting runs locally.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PARTY_INVALID,Check the selected party.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PARTY_REQUIRED,Select at least one mercenary.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PARTY_SELECT,Select Party,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PATH_FAILED,No movement path was found.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PAUSE,Pause,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_PERFORMANCE_GUARD,The hunt ended to preserve stability.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_POOL_EXHAUSTED,Combat display capacity was exceeded.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RECALL,Recall,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RECALL_CONFIRM,End this hunt and return?,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RECOVERED,The interrupted combat was cancelled and the party returned to town.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_REGION_TITLE,Meadow Road,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RESULT_TITLE,Hunt Result,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RESUME,Resume,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RETRY,Reload,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_RETURN_FAILED,Safe return was used because the route failed.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_SPEED,Observe Speed ×{speed},P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_START_HUNT,Start Hunt,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_STATE_DEFERRED,This action is not available yet.,P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_STATUS_ACTION,Action: {action},P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_STATUS_HP,HP {current}/{max},P06_COMBAT,CONFIRMED,TRUE
en-US,TXT_P06_STATUS_REASON,Reason: {reason},P06_COMBAT,CONFIRMED,TRUE
ko-KR,NARRATIVE_FINAL_CLEAR,왕국은 다시 세워졌지만 세계의 위협은 아직 끝나지 않았다.,CORE,CONFIRMED,TRUE
ko-KR,NARRATIVE_OPENING_1,몬스터의 침공으로 왕국은 폐허가 되었다.,CORE,CONFIRMED,TRUE
ko-KR,NARRATIVE_OPENING_2,마지막 거점을 복구하고 용병과 장인을 모아야 한다.,CORE,CONFIRMED,TRUE
ko-KR,TXT_ACTIVE_MERC_CAP_V1_DESCRIPTION,1.0 활동 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_BOSS_DRAGON_NAME,잿빛 고룡,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_BOSS_HYDRA_NAME,역병 히드라,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_CURRENCY_KINGDOM_GOLD_NAME,왕국 골드,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_PREMIUM_FREE_NAME,무료 프리미엄,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_PREMIUM_PAID_NAME,유료 프리미엄,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_CURRENCY_SPECIAL_RECRUIT_TICKET_NAME,특별 모집권,CURRENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_DEFAULT_HP_RETURN_THRESHOLD_DESCRIPTION,기본 귀환 체력,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_DISMANTLE_ENHANCE_REFUND_DESCRIPTION,강화 재료 환급,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_EQUIPMENT_UPGRADE_THRESHOLD_DESCRIPTION,자동 교체 최소 개선,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_ARCHER_NAME,고룡 날개활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_CLERIC_NAME,재의 성휘,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_GUARDIAN_NAME,잿빛 용린갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_MAGE_NAME,고룡심장 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_DRAGON_WARRIOR_NAME,잿빛 고룡검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_ARCHER_NAME,독사의 눈 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_CLERIC_NAME,정화의 성배,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_GUARDIAN_NAME,히드라 비늘 수호갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_MAGE_NAME,역병 가지 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_BOSS_HYDRA_WARRIOR_NAME,히드라 송곳니 대검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_ARCHER_WEAPON_NAME,개척자의 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLERIC_WEAPON_NAME,개척자의 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLOTH_ARMOR_NAME,개척자의 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_CLOTH_HELMET_NAME,개척자의 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_GUARDIAN_WEAPON_NAME,개척자의 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_GUARD_ACCESSORY_NAME,개척자의 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_HEAVY_ARMOR_NAME,개척자의 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_HEAVY_HELMET_NAME,개척자의 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_LIGHT_ARMOR_NAME,개척자의 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_LIGHT_HELMET_NAME,개척자의 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_MAGE_WEAPON_NAME,개척자의 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_POWER_ACCESSORY_NAME,개척자의 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_WARRIOR_WEAPON_NAME,개척자의 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T1_WISDOM_ACCESSORY_NAME,개척자의 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_ARCHER_WEAPON_NAME,어둠숲 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLERIC_WEAPON_NAME,어둠숲 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLOTH_ARMOR_NAME,어둠숲 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_CLOTH_HELMET_NAME,어둠숲 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_GUARDIAN_WEAPON_NAME,어둠숲 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_GUARD_ACCESSORY_NAME,어둠숲 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_HEAVY_ARMOR_NAME,어둠숲 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_HEAVY_HELMET_NAME,어둠숲 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_LIGHT_ARMOR_NAME,어둠숲 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_LIGHT_HELMET_NAME,어둠숲 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_MAGE_WEAPON_NAME,어둠숲 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_POWER_ACCESSORY_NAME,어둠숲 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_WARRIOR_WEAPON_NAME,어둠숲 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T2_WISDOM_ACCESSORY_NAME,어둠숲 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_ARCHER_WEAPON_NAME,철맥 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLERIC_WEAPON_NAME,철맥 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLOTH_ARMOR_NAME,철맥 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_CLOTH_HELMET_NAME,철맥 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_GUARDIAN_WEAPON_NAME,철맥 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_GUARD_ACCESSORY_NAME,철맥 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_HEAVY_ARMOR_NAME,철맥 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_HEAVY_HELMET_NAME,철맥 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_LIGHT_ARMOR_NAME,철맥 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_LIGHT_HELMET_NAME,철맥 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_MAGE_WEAPON_NAME,철맥 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_POWER_ACCESSORY_NAME,철맥 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_WARRIOR_WEAPON_NAME,철맥 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T3_WISDOM_ACCESSORY_NAME,철맥 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_ARCHER_WEAPON_NAME,독안개 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLERIC_WEAPON_NAME,독안개 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLOTH_ARMOR_NAME,독안개 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_CLOTH_HELMET_NAME,독안개 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_GUARDIAN_WEAPON_NAME,독안개 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_GUARD_ACCESSORY_NAME,독안개 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_HEAVY_ARMOR_NAME,독안개 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_HEAVY_HELMET_NAME,독안개 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_LIGHT_ARMOR_NAME,독안개 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_LIGHT_HELMET_NAME,독안개 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_MAGE_WEAPON_NAME,독안개 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_POWER_ACCESSORY_NAME,독안개 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_WARRIOR_WEAPON_NAME,독안개 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T4_WISDOM_ACCESSORY_NAME,독안개 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_ARCHER_WEAPON_NAME,빙결 유적 활,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLERIC_WEAPON_NAME,빙결 유적 성직 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLOTH_ARMOR_NAME,빙결 유적 로브,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_CLOTH_HELMET_NAME,빙결 유적 로브 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_GUARDIAN_WEAPON_NAME,빙결 유적 수호 철퇴,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_GUARD_ACCESSORY_NAME,빙결 유적 수호 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_HEAVY_ARMOR_NAME,빙결 유적 중갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_HEAVY_HELMET_NAME,빙결 유적 중갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_LIGHT_ARMOR_NAME,빙결 유적 경갑,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_LIGHT_HELMET_NAME,빙결 유적 경갑 투구,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_MAGE_WEAPON_NAME,빙결 유적 지팡이,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_POWER_ACCESSORY_NAME,빙결 유적 힘의 부적,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_WARRIOR_WEAPON_NAME,빙결 유적 검,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_EQ_T5_WISDOM_ACCESSORY_NAME,빙결 유적 지혜의 펜던트,EQUIPMENT_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L1_EFFECT,하급 포션,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L2_EFFECT,중급·해독제,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L3_EFFECT,상급·저항약,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_L4_EFFECT,레이드 포션,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_ALCHEMY_NAME,연금술 공방,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L1_EFFECT,T1 제작·분해,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L2_EFFECT,T2·강화 +5,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L3_EFFECT,T3/T4·강화 +8·제련,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_L4_EFFECT,T5·보스 장비·강화 +10,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_BLACKSMITH_NAME,대장간,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L1_EFFECT,정식 승급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L2_EFFECT,숙련 승급·지역 정책,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L3_EFFECT,정예 승급·레이드 준비,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_L4_EFFECT,영웅·전설 승급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_GUILD_NAME,모험가 길드,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L1_EFFECT,기본 치료,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L2_EFFECT,치료 시간 -15%,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L3_EFFECT,상태 이상 치료,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_L4_EFFECT,치료 시간 -35%,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_INFIRMARY_NAME,치료소,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L1_EFFECT,활동 4/보유 8,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L2_EFFECT,활동 8/보유 12,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L3_EFFECT,활동 12/보유 18,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_L4_EFFECT,활동 16/보유 24,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_LODGE_NAME,용병 숙소,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L1_EFFECT,기본 매입·판매,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L2_EFFECT,가격 정책·재고 목표,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L3_EFFECT,희귀 장비 취급,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_L4_EFFECT,보스 장비 진열,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_STORE_NAME,상점,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L1_EFFECT,"후보 3명, C/B/A",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L2_EFFECT,"후보 4명, B/A 확률 증가",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L3_EFFECT,"후보 5명, A 확률 증가",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_L4_EFFECT,"후보 잠금 2칸, 최고 A 확률",FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_TAVERN_NAME,주점,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L1_EFFECT,용량 200,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L2_EFFECT,용량 500·예약 재고,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L3_EFFECT,용량 1200,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_L4_EFFECT,용량 3000·필터,FACILITY_EFFECT,TUNABLE,TRUE
ko-KR,TXT_FAC_WAREHOUSE_NAME,창고,FACILITY_NAME,TUNABLE,TRUE
ko-KR,TXT_GRADE_A_NAME,A,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_B_NAME,B,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_C_NAME,C,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_SS_NAME,SS,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_GRADE_S_NAME,S,MERCENARY_GRADE_NAME,CONFIRMED,TRUE
ko-KR,TXT_INVENTORY_RETURN_THRESHOLD_DESCRIPTION,귀환 임계치,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_JOB_ARCHER_NAME,궁수,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_ARCHER_NOTES,원거리 단일 딜러,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_CLERIC_NAME,성직자,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_CLERIC_NOTES,회복·보조,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_GUARDIAN_NAME,수호자,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_GUARDIAN_NOTES,도발·피해 흡수,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_MAGE_NAME,마법사,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_MAGE_NOTES,범위·속성 공격,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_JOB_WARRIOR_NAME,전사,JOB_NAME,CONFIRMED,TRUE
ko-KR,TXT_JOB_WARRIOR_NOTES,근접 균형 딜러,JOB_NOTES,CONFIRMED,TRUE
ko-KR,TXT_KINGDOM_1_NAME,폐허 전초기지,KINGDOM_STAGE_NAME,CONFIRMED,TRUE
ko-KR,TXT_KINGDOM_2_NAME,정착 마을,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_3_NAME,요새 도시,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_4_NAME,성채 수도,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_KINGDOM_5_NAME,복구된 왕국,KINGDOM_STAGE_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_ASH_CORE_NAME,잿불 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_HEART_NAME,고룡의 심장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_HORN_NAME,고룡의 뿔,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_DRAGON_SCALE_NAME,잿빛 용비늘,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_FANG_NAME,히드라 송곳니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_HEART_NAME,히드라 심장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_SCALE_NAME,히드라 비늘,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_BOSS_HYDRA_VENOM_NAME,원초 독액,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_1_NAME,하급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_2_NAME,중급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_3_NAME,상급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_ENHANCE_4_NAME,최상급 강화석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_ANCIENT_AWAKENING_NAME,고대 각성석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_AWAKENING_STONE_NAME,각성석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_BRONZE_EMBLEM_NAME,청동 승급 문장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_ELITE_SIGIL_NAME,정예의 인장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_FATE_CHALICE_NAME,운명의 성배,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_HERO_CREST_NAME,영웅의 증표,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_LEGEND_CREST_NAME,전설의 증표,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_RARE_CORE_NAME,희귀 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_SILVER_BADGE_NAME,은빛 승급패,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_PROMO_STAR_SIGIL_NAME,별의 인장,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_BOAR_HIDE_NAME,멧돼지 가죽,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_MEADOW_CRYSTAL_NAME,초원 결정,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_SLIME_GEL_NAME,슬라임 젤,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_SOFTWOOD_NAME,부드러운 목재,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_WILD_HERB_NAME,들풀 약초,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R01_WOLF_FANG_NAME,늑대 송곳니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_DARKWOOD_NAME,어둠목,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_FOREST_CORE_NAME,숲의 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_MOONLEAF_NAME,달빛잎,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_OGRE_BONE_NAME,오우거 뼈,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_SPIDER_SILK_NAME,거미 비단,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R02_VENOM_SAC_NAME,독주머니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_ANCIENT_GEAR_NAME,고대 톱니,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_BAT_WING_NAME,동굴 박쥐 날개,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_COAL_NAME,고열 석탄,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_GOLEM_FRAGMENT_NAME,골렘 파편,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_IRON_ORE_NAME,철광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R03_SILVER_ORE_NAME,은광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_BOG_CRYSTAL_NAME,늪 결정,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_MIASMA_MOSS_NAME,독안개 이끼,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_SWAMP_REED_NAME,늪지 갈대,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_TOXIC_GLAND_NAME,맹독선,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_TROLL_HIDE_NAME,늪 트롤 가죽,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R04_WITCHWATER_NAME,마녀수,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_ANCIENT_RUNE_NAME,고대 룬,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_FROST_ORE_NAME,서리 광석,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_FROZEN_CORE_NAME,빙결 핵,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_ICE_BLOOM_NAME,얼음꽃,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_WRAITH_DUST_NAME,망령 가루,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_R05_YETI_FUR_NAME,설인 모피,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MAT_REFINE_STABILIZER_NAME,제련 안정제,ITEM_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_BOAR_NAME,들멧돼지,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_ELITE_DIRE_WOLF_NAME,광포한 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_GOBLIN_NAME,떠돌이 고블린,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_SLIME_NAME,초원 슬라임,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R01_WOLF_NAME,회색 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_ELITE_OGRE_NAME,숲 오우거 족장,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_LIZARD_NAME,숲도마뱀,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_SHAMAN_NAME,고블린 주술사,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_SPIDER_NAME,그늘 거미,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R02_TREANT_NAME,어린 트렌트,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_BAT_NAME,철광 박쥐,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_CRAWLER_NAME,동굴 포식자,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_ELITE_GUARDIAN_NAME,광산 수호자,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_GOLEM_NAME,철 골렘,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R03_MINER_NAME,망령 광부,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_ELITE_HYDRA_SPAWN_NAME,히드라 유생,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_FROG_NAME,맹독 개구리,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_PLAGUE_BEAST_NAME,역병 마수,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_TROLL_NAME,늪 트롤,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R04_WITCH_NAME,늪지 마녀,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_ELITE_FROST_GIANT_NAME,서리 거인,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_ICE_WOLF_NAME,빙설 늑대,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_RUNE_SENTINEL_NAME,룬 파수병,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_WRAITH_NAME,서리 망령,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_MON_R05_YETI_NAME,설인,MONSTER_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_ALCHEMIST_NAME,연금술사,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_APPRENTICE_NAME,견습,NPC_PROFICIENCY_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_ARTISAN_NAME,장인,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_BLACKSMITH_NAME,대장장이,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_HEALER_NAME,치료사,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_MASTER_NAME,명장,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_NPC_MERCHANT_NAME,상점 주인,NPC_PROFESSION_NAME,CONFIRMED,TRUE
ko-KR,TXT_NPC_SKILLED_NAME,숙련,NPC_PROFICIENCY_NAME,TUNABLE,TRUE
ko-KR,TXT_OFFLINE_HUNT_EFFICIENCY_DESCRIPTION,온라인 대비 기본 효율,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_OFFLINE_MAX_HOURS_DESCRIPTION,오프라인 정산 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_P04_ACTION_ASSIGN,NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BACK,뒤로,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_BUILD,건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CANCEL,취소,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLAIM,완료,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CLOSE,닫기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_CONFIRM,확인,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_RETRY,다시 시도,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UNASSIGN,배치 해제,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ACTION_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,P04 자동 저장 debounce,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_GOLD,왕국 골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_COST_MATERIAL,{item} × {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DEV_CLOCK_DESC,P04 테스트 시계 모드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_DRAWER_DURATION_DESC,시설 Drawer 애니메이션 시간,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_GOLD,왕국 골드가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_NOT_READY,아직 완료되지 않았습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_JOB_RUNNING,이미 진행 중인 작업이 있습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MATERIAL,건설 재료가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_MAX_LEVEL,최대 레벨입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_ASSIGNED,이미 다른 시설에 배치된 NPC입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_NONE,배치할 관리 NPC가 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_NPC_PROFESSION,이 시설에 맞는 직종이 아닙니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_PROFILE_AMBIGUOUS,여러 로컬 왕국이 발견되어 자동으로 선택할 수 없습니다.,P04_PROFILE,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_PROFILE_DISCOVERY,로컬 왕국 저장소를 확인하지 못했습니다.,P04_PROFILE,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_CREATE,새 왕국을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_LOAD,저장 데이터를 불러오지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_SAVE_WRITE,변경 사항을 저장하지 못했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_ERROR_STAGE,왕국 단계가 부족합니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_GOLD,골드 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_PREMIUM,프리미엄 {amount},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_RECOVERED,백업 저장을 복구했습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_HUD_STAGE,왕국 단계 {stage},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_ALCHEMY,연금술 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_BLACKSMITH,제작 기능은 P10에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_GUILD,의뢰 기능은 P11에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_INFIRMARY,치료 기능은 P09에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_STORE,거래 기능은 P08에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_TAVERN,모집 기능은 P13에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_PHASE_WAREHOUSE,창고 기능은 P07에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_LOCKED_STAGE,왕국 단계 {stage}에서 개방됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_ASSIGN_TITLE,관리 NPC 배치,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_BUILD_TITLE,시설 건설,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_MODAL_UPGRADE_TITLE,시설 업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_CRAFT,제작,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_KINGDOM,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MENU,메뉴,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_MERCENARIES,용병,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_RECRUIT,모집,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_NAV_REGION,지역,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_ACTIVE,가동 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDABLE,건설 가능,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_BUILDING,건설 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_LOCKED,잠김,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_STOPPED,가동 정지,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STATE_UPGRADING,업그레이드 중,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_STOP_NPC_REQUIRED,올바른 관리 NPC를 배치하면 가동됩니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OPERATIONS,운영,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_OVERVIEW,개요,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_STAFF,직원,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TAB_UPGRADE,업그레이드,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIMER_TICK_DESC,시설 남은 시간 표시 주기,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_FINISHES,완료 예정 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_TIME_REMAINING,남은 시간 {time},P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_CONTENT,왕국,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_EMPTY,시설 정보를 찾을 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_ERROR,왕국을 표시할 수 없습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOADING,왕국을 불러오는 중…,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_LOCKED,아직 잠긴 시설입니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P04_UI_OFFLINE,오프라인: 로컬 시설 관리는 사용할 수 있습니다.,P04_UI,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_ACTIVATE,활동 배치,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLEAR_SEARCH,검색 지우기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_CLOSE,닫기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_DEACTIVATE,활동 해제,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RESET_FILTERS,필터 초기화,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTION_RETRY,다시 시도,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ACTIVE_COUNT,활동 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_CONSUMABLES,소모품 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_BUY_EQUIPMENT,장비 구매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_COMBAT,전투,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_CONTINUE_DECISION,계속 여부 판단,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_EVALUATE_EQUIPMENT,장비 평가,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_FIND_TARGET,대상 탐색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_HEAL,치료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_IDLE_TOWN,마을 대기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_INJURED,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_LOOT,전리품 회수,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PREPARE,준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_PROCESS,승급 심사,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_PROMOTION_READY,승급 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RAID_READY,레이드 준비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_RETURN_TOWN,마을 귀환,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_SELL_LOOT,전리품 판매,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_AUTONOMY_TRAVEL_TO_REGION,지역 이동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_COMBAT_STATS,전투 능력치는 P06에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_EQUIPMENT,장비 변경은 P07에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_PROMOTION,승급 심사는 P11에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_DEFER_RECRUITMENT,새 용병 모집은 P13에서 개방됩니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_ACTIVE_LIMIT,활동 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_LOAD,용병 저장 데이터를 불러오지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_NOT_FOUND,용병을 찾을 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_OWNED_LIMIT,보유 용병 슬롯이 가득 찼습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_REVISION,다른 변경이 먼저 저장되었습니다. 다시 불러옵니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_SAVE,용병 변경 사항을 저장하지 못했습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ERROR_STATE_FORBIDDEN,현재 상태에서는 활동 여부를 바꿀 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACCESSORY,장신구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ACTION_REASON,행동 이유,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ARMOR,갑옷,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CONTRIBUTION,기여도 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_CURRENT_ACTION,현재 행동,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_EXP,경험치 {exp},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_GRADE,기본 등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HELMET,투구,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_HUNT_COUNT,사냥 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_ITEMS_COLLECTED,수집 아이템 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_KILL_COUNT,처치 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_LEVEL,레벨 {level}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONALITY,성격,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_PERSONAL_GOLD,개인 골드 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_POTIONS,보유 포션,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RAID_CLEAR,레이드 클리어 {amount},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_RANK,성장 랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_TRAITS,특성,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FIELD_WEAPON,무기,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ACTIVE,활동 여부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_ALL,전체,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_INJURY,부상,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_JOB,직업,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_NO_RESULTS,필터와 일치하는 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_PROMOTION,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_FILTER_STATUS,상태,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_HP_LOW,체력 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_INVENTORY_FULL,인벤토리 가득 참,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_LOOT_COMPLETE,전리품 회수 완료,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_NONE,없음,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PLAYER_RECALL,플레이어 귀환 명령,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POLICY,왕국 정책,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_POTION_LOW,포션 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_PROMOTION_AVAILABLE,승급 가능,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_SURVIVAL_LOW,생존 가능성 부족,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_REASON_TARGET_FOUND,대상 발견,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_ROSTER_COUNT,보유 {current}/{max},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SEARCH_PLACEHOLDER,용병 이름 검색,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SLOT_SOURCE,용병 숙소 레벨 {level},P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_ASC,오름차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DEFAULT,기본 순서,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_DESC,내림차순,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_GRADE,등급,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_LEVEL,레벨,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_NAME,이름,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_SORT_RANK,랭크,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_ACTIVE,활동 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_STATUS_INACTIVE,대기 중,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_EQUIPMENT,장비,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_GROWTH,성장,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_OVERVIEW,개요,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TAB_RECORDS,기록,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_TITLE,용병,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_CONTENT,용병 명부,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_EMPTY,보유한 용병이 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_ERROR,용병 정보를 표시할 수 없습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOADING,용병 정보를 불러오는 중…,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_LOCKED,용병 명부가 아직 잠겨 있습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P05_UI_OFFLINE,오프라인: 로컬 용병 관리는 사용할 수 있습니다.,P05_ROSTER,CONFIRMED,TRUE
ko-KR,TXT_P06_COMBAT_RESULT,처치 {kills} · 기여도 +{contribution} · 개인 골드 +{gold},P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_CONTENT_MISSING,전투 콘텐츠가 올바르지 않습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_DISABLED_INACTIVE,활동 중인 용병만 출전할 수 있습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_DISABLED_STATE,마을 대기 상태의 용병만 출전할 수 있습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_EMPTY,출전 가능한 활동 용병이 없습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_ERROR,전투를 계속할 수 없습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_LOADING,지역을 불러오는 중…,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_LOCKED,첫 지역이 잠겨 있습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_NO_POTION,포션이 없어 안전 귀환합니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_OFFLINE,오프라인: 일반 사냥은 로컬에서 진행됩니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PARTY_INVALID,출전 인원을 다시 확인해 주세요.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PARTY_REQUIRED,용병을 1명 이상 선택하세요.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PARTY_SELECT,출전 인원 선택,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PATH_FAILED,이동 경로를 찾지 못했습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PAUSE,일시정지,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_PERFORMANCE_GUARD,안정성을 위해 사냥을 종료했습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_POOL_EXHAUSTED,전투 표시 한도를 초과했습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RECALL,귀환,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RECALL_CONFIRM,현재 사냥을 끝내고 귀환하시겠습니까?,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RECOVERED,중단된 전투를 취소하고 마을로 복귀했습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_REGION_TITLE,초원의 길,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RESULT_TITLE,사냥 결과,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RESUME,계속,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RETRY,다시 불러오기,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_RETURN_FAILED,귀환 경로를 완료하지 못해 안전 복귀했습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_SPEED,관전 속도 ×{speed},P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_START_HUNT,사냥 시작,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_STATE_DEFERRED,이 행동은 아직 사용할 수 없습니다.,P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_STATUS_ACTION,행동: {action},P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_STATUS_HP,체력 {current}/{max},P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_P06_STATUS_REASON,이유: {reason},P06_COMBAT,CONFIRMED,TRUE
ko-KR,TXT_PERSONALITY_BRAVE_DESCRIPTION,위험한 지역과 강한 적을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_BRAVE_NAME,용감함,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_CAUTIOUS_DESCRIPTION,생존 가능성이 높은 지역과 이른 귀환을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_CAUTIOUS_NAME,신중함,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_COLLECTOR_DESCRIPTION,희귀 재료가 있는 지역을 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_COLLECTOR_NAME,수집벽,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_FRUGAL_DESCRIPTION,큰 개선이 있을 때만 장비를 구매한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_FRUGAL_NAME,절약가,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_GEARHEAD_DESCRIPTION,작은 개선에도 장비 구매를 선호한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_GEARHEAD_NAME,장비광,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_PRACTICAL_DESCRIPTION,가격과 성능을 균형 있게 판단한다.,PERSONALITY_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_PERSONALITY_PRACTICAL_NAME,실용주의,PERSONALITY_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_ANTIDOTE_NAME,해독제,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_FROST_RESIST_NAME,빙결 저항약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_LARGE_NAME,상급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_MEDIUM_NAME,중급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_HEAL_SMALL_NAME,하급 회복 포션,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_POISON_RESIST_NAME,독 저항약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_POT_RAID_POWER_NAME,레이드 전투약,POTION_NAME,TUNABLE,TRUE
ko-KR,TXT_QUALITY_COMMON_NAME,일반,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_FINE_NAME,정교,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_LEGACY_NAME,유산,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_RARE_NAME,희귀,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_QUALITY_RELIC_NAME,유물,EQUIPMENT_QUALITY_NAME,CONFIRMED,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_BODY_NAME,몸통,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_BODY_PRIORITY,방어구 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HEART_NAME,심장,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HEART_PRIORITY,최상위 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HORN_NAME,뿔,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_HORN_PRIORITY,무기 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_WING_NAME,날개,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_DRAGON_WING_PRIORITY,공중 패턴 차단,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_DRAGON_NAME,잿빛 고룡,RAID_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_BODY_NAME,몸통,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_BODY_PRIORITY,비늘 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEAD_NAME,머리,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEAD_PRIORITY,독 공격 약화,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEART_NAME,심장,RAID_PART_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_HYDRA_HEART_PRIORITY,희귀 핵심 재료,RAID_PART_PRIORITY,TUNABLE,TRUE
ko-KR,TXT_RAID_HYDRA_NAME,역병 히드라,RAID_NAME,TUNABLE,TRUE
ko-KR,TXT_RAID_PARTY_MAX_DESCRIPTION,레이드 최대,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_RAID_PARTY_MIN_DESCRIPTION,레이드 최소,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_RANK_APPRENTICE_NAME,수습 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_ELITE_NAME,정예 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_HERO_NAME,영웅,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_LEGEND_NAME,전설,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_REGULAR_NAME,정식 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RANK_SKILLED_NAME,숙련 용병,MERCENARY_RANK_NAME,CONFIRMED,TRUE
ko-KR,TXT_RARE_EQUIPMENT_AUTO_PROTECT_DESCRIPTION,희귀 이상 자동 보호,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_ARCHER_NAME,고룡 날개활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_CLERIC_NAME,재의 성휘,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_GUARDIAN_NAME,잿빛 용린갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_MAGE_NAME,고룡심장 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_DRAGON_WARRIOR_NAME,잿빛 고룡검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_ARCHER_NAME,독사의 눈 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_CLERIC_NAME,정화의 성배,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_GUARDIAN_NAME,히드라 비늘 수호갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_MAGE_NAME,역병 가지 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_BOSS_HYDRA_WARRIOR_NAME,히드라 송곳니 대검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_ARCHER_WEAPON_NAME,개척자의 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLERIC_WEAPON_NAME,개척자의 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLOTH_ARMOR_NAME,개척자의 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_CLOTH_HELMET_NAME,개척자의 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_GUARDIAN_WEAPON_NAME,개척자의 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_GUARD_ACCESSORY_NAME,개척자의 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_HEAVY_ARMOR_NAME,개척자의 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_HEAVY_HELMET_NAME,개척자의 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_LIGHT_ARMOR_NAME,개척자의 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_LIGHT_HELMET_NAME,개척자의 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_MAGE_WEAPON_NAME,개척자의 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_POWER_ACCESSORY_NAME,개척자의 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_WARRIOR_WEAPON_NAME,개척자의 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T1_WISDOM_ACCESSORY_NAME,개척자의 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_ARCHER_WEAPON_NAME,어둠숲 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLERIC_WEAPON_NAME,어둠숲 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLOTH_ARMOR_NAME,어둠숲 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_CLOTH_HELMET_NAME,어둠숲 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_GUARDIAN_WEAPON_NAME,어둠숲 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_GUARD_ACCESSORY_NAME,어둠숲 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_HEAVY_ARMOR_NAME,어둠숲 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_HEAVY_HELMET_NAME,어둠숲 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_LIGHT_ARMOR_NAME,어둠숲 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_LIGHT_HELMET_NAME,어둠숲 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_MAGE_WEAPON_NAME,어둠숲 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_POWER_ACCESSORY_NAME,어둠숲 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_WARRIOR_WEAPON_NAME,어둠숲 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T2_WISDOM_ACCESSORY_NAME,어둠숲 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_ARCHER_WEAPON_NAME,철맥 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLERIC_WEAPON_NAME,철맥 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLOTH_ARMOR_NAME,철맥 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_CLOTH_HELMET_NAME,철맥 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_GUARDIAN_WEAPON_NAME,철맥 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_GUARD_ACCESSORY_NAME,철맥 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_HEAVY_ARMOR_NAME,철맥 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_HEAVY_HELMET_NAME,철맥 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_LIGHT_ARMOR_NAME,철맥 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_LIGHT_HELMET_NAME,철맥 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_MAGE_WEAPON_NAME,철맥 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_POWER_ACCESSORY_NAME,철맥 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_WARRIOR_WEAPON_NAME,철맥 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T3_WISDOM_ACCESSORY_NAME,철맥 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_ARCHER_WEAPON_NAME,독안개 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLERIC_WEAPON_NAME,독안개 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLOTH_ARMOR_NAME,독안개 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_CLOTH_HELMET_NAME,독안개 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_GUARDIAN_WEAPON_NAME,독안개 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_GUARD_ACCESSORY_NAME,독안개 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_HEAVY_ARMOR_NAME,독안개 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_HEAVY_HELMET_NAME,독안개 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_LIGHT_ARMOR_NAME,독안개 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_LIGHT_HELMET_NAME,독안개 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_MAGE_WEAPON_NAME,독안개 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_POWER_ACCESSORY_NAME,독안개 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_WARRIOR_WEAPON_NAME,독안개 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T4_WISDOM_ACCESSORY_NAME,독안개 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_ARCHER_WEAPON_NAME,빙결 유적 활,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLERIC_WEAPON_NAME,빙결 유적 성직 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLOTH_ARMOR_NAME,빙결 유적 로브,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_CLOTH_HELMET_NAME,빙결 유적 로브 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_GUARDIAN_WEAPON_NAME,빙결 유적 수호 철퇴,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_GUARD_ACCESSORY_NAME,빙결 유적 수호 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_HEAVY_ARMOR_NAME,빙결 유적 중갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_HEAVY_HELMET_NAME,빙결 유적 중갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_LIGHT_ARMOR_NAME,빙결 유적 경갑,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_LIGHT_HELMET_NAME,빙결 유적 경갑 투구,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_MAGE_WEAPON_NAME,빙결 유적 지팡이,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_POWER_ACCESSORY_NAME,빙결 유적 힘의 부적,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_WARRIOR_WEAPON_NAME,빙결 유적 검,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_EQ_T5_WISDOM_ACCESSORY_NAME,빙결 유적 지혜의 펜던트,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_ANTIDOTE_NAME,해독제,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_FROST_RESIST_NAME,빙결 저항약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_LARGE_NAME,상급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_MEDIUM_NAME,중급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_HEAL_SMALL_NAME,하급 회복 포션,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_POISON_RESIST_NAME,독 저항약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REC_POT_RAID_POWER_NAME,레이드 전투약,RECIPE_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_ATK_POWER_NAME,공격력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_BOSS_NAME,보스 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_CRIT_NAME,치명타 확률,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_DEF_NAME,방어력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_FIRE_NAME,화염 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_FROST_NAME,빙결 저항,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_HP_NAME,최대 체력 증가,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_MATERIAL_NAME,재료 추가 획득,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_PART_NAME,부위 파괴 피해,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_POISON_NAME,독 피해·저항,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REF_RARE_FIND_NAME,희귀 재료 발견,REFINE_OPTION_NAME,TUNABLE,TRUE
ko-KR,TXT_REGION_R01_NAME,왕국 외곽 초원,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R02_NAME,어둠숲,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R03_NAME,버려진 광산,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R04_NAME,독안개 늪지,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_REGION_R05_NAME,얼어붙은 유적,REGION_NAME,CONFIRMED,TRUE
ko-KR,TXT_ROSTER_CAP_V1_DESCRIPTION,1.0 보유 상한,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_SK_ARC_HUNTER_EYE_DESCRIPTION,정예·보스 약점 피해 증가,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_HUNTER_EYE_NAME,사냥꾼의 눈,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_PIERCE_DESCRIPTION,직선상의 적 관통,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_PIERCE_NAME,관통 화살,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_QUICK_SHOT_DESCRIPTION,빠른 원거리 공격,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_ARC_QUICK_SHOT_NAME,속사,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BARRIER_DESCRIPTION,피해 흡수 보호막,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BARRIER_NAME,보호막,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BLESSING_DESCRIPTION,공격·방어 보조 버프,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_BLESSING_NAME,축복,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_HEAL_DESCRIPTION,가장 체력이 낮은 아군 회복,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_CLE_HEAL_NAME,치유,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_FORTRESS_DESCRIPTION,일정 시간 방어력 증가,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_FORTRESS_NAME,요새 태세,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_GUARD_MARK_DESCRIPTION,낮은 체력 아군 피해 일부 대신 받음,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_GUARD_MARK_NAME,수호 표식,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_TAUNT_DESCRIPTION,주변 적의 위협도를 집중,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_GUA_TAUNT_NAME,방패 도발,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FIRE_BURST_DESCRIPTION,범위 화염 피해,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FIRE_BURST_NAME,화염 폭발,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FROST_NOVA_DESCRIPTION,범위 피해와 이동 둔화,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_FROST_NOVA_NAME,서리 파동,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_MANA_FLOW_DESCRIPTION,스킬 재사용 대기시간 소폭 감소,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_MAG_MANA_FLOW_NAME,마력 순환,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_BATTLE_RUSH_DESCRIPTION,대상에게 돌진하고 짧게 경직,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_BATTLE_RUSH_NAME,전투 돌진,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_HEAVY_SLASH_DESCRIPTION,대상에게 강한 물리 피해,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_HEAVY_SLASH_NAME,강타,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_TENACITY_DESCRIPTION,체력이 낮을수록 피해 감소,SKILL_DESCRIPTION,CONFIRMED,TRUE
ko-KR,TXT_SK_WAR_TENACITY_NAME,강인함,SKILL_NAME,CONFIRMED,TRUE
ko-KR,TXT_STATUS_BARRIER_NAME,보호막,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_BLESSING_NAME,축복,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_BURN_NAME,화상,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_POISON_NAME,중독,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_SLOW_NAME,둔화,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STATUS_TAUNT_NAME,도발,STATUS_EFFECT_NAME,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_HIGH_DESCRIPTION,고가 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_LOW_DESCRIPTION,저가 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_STORE_PRICE_STANDARD_DESCRIPTION,표준 정책,RUNTIME_CONFIG_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_ARCANE_DESCRIPTION,마법·신성 효과가 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_ARCANE_NAME,마력 친화,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_BOSS_HUNTER_DESCRIPTION,보스 피해가 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_BOSS_HUNTER_NAME,거수 사냥꾼,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_FROSTBORN_DESCRIPTION,빙결 피해와 둔화가 감소한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_FROSTBORN_NAME,설원의 자식,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_KEEN_EYE_DESCRIPTION,치명타 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_KEEN_EYE_NAME,예리한 눈,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_LUCKY_DESCRIPTION,희귀 재료 발견 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_LUCKY_NAME,행운아,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_POISON_RESIST_DESCRIPTION,독 피해와 지속시간이 감소한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_POISON_RESIST_NAME,독 내성,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SCAVENGER_DESCRIPTION,재료 추가 획득 확률이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SCAVENGER_NAME,수집가,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STRONG_DESCRIPTION,힘이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STRONG_NAME,완력,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STURDY_DESCRIPTION,최대 체력이 증가한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_STURDY_NAME,튼튼함,TRAIT_NAME,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SURVIVOR_DESCRIPTION,더 안전한 시점에 귀환한다.,TRAIT_DESCRIPTION,TUNABLE,TRUE
ko-KR,TXT_TRAIT_SURVIVOR_NAME,생존 본능,TRAIT_NAME,TUNABLE,TRUE
ko-KR,UI_FACILITY_STOP_MATERIAL,재료 부족으로 생산이 중단되었습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_FACILITY_STOP_NPC,담당 NPC가 없어 시설이 멈췄습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_CRAFT,제작,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_KINGDOM,왕국,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_MENU,메뉴,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_MERCENARY,용병,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_RECRUIT,모집,CORE,CONFIRMED,TRUE
ko-KR,UI_NAV_REGION,지역,CORE,CONFIRMED,TRUE
ko-KR,UI_PROMOTION_READY,승급 가능,CORE,CONFIRMED,TRUE
ko-KR,UI_RAID_ROLE_WARNING,권장 역할이 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_RECRUIT_ROSTER_FULL,용병 보유 슬롯이 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_REGION_LOCK_RANK,용병 성장 랭크가 부족합니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_EMPTY,표시할 내용이 없습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_ERROR,처리 중 문제가 발생했습니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_LOADING,불러오는 중입니다.,CORE,CONFIRMED,TRUE
ko-KR,UI_STATE_OFFLINE,오프라인 상태입니다.,CORE,CONFIRMED,TRUE
```

#### `region_encounter_profiles.csv` final bytes

```csv
region_id,monster_id,weight,wave_min,wave_max,spawn_group,status,enabled
REGION_R01,MON_R01_SLIME,24,3,5,R01_NORMAL,TUNABLE,TRUE
REGION_R01,MON_R01_WOLF,24,3,5,R01_NORMAL,TUNABLE,TRUE
REGION_R01,MON_R01_BOAR,24,2,4,R01_NORMAL,TUNABLE,TRUE
REGION_R01,MON_R01_GOBLIN,24,3,5,R01_NORMAL,TUNABLE,TRUE
REGION_R01,MON_R01_ELITE_DIRE_WOLF,4,1,1,R01_ELITE,TUNABLE,TRUE
```

#### `runtime_config.csv` final bytes

```csv
config_key,value_type,value,unit,min_value,max_value,description_text_key,status,enabled
ACTIVE_MERC_CAP_V1,INTEGER,16,COUNT,1,24,TXT_ACTIVE_MERC_CAP_V1_DESCRIPTION,TUNABLE,TRUE
DEFAULT_HP_RETURN_THRESHOLD,DECIMAL,0.3,RATIO,0,1,TXT_DEFAULT_HP_RETURN_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
DISMANTLE_ENHANCE_REFUND,DECIMAL,0.7,RATIO,0,1,TXT_DISMANTLE_ENHANCE_REFUND_DESCRIPTION,TUNABLE,TRUE
EQUIPMENT_UPGRADE_THRESHOLD,DECIMAL,0.05,RATIO,0,1,TXT_EQUIPMENT_UPGRADE_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
INVENTORY_RETURN_THRESHOLD,DECIMAL,0.9,RATIO,0,1,TXT_INVENTORY_RETURN_THRESHOLD_DESCRIPTION,TUNABLE,TRUE
OFFLINE_HUNT_EFFICIENCY,DECIMAL,0.75,RATIO,0,1,TXT_OFFLINE_HUNT_EFFICIENCY_DESCRIPTION,TUNABLE,TRUE
OFFLINE_MAX_HOURS,INTEGER,8,HOURS,0,24,TXT_OFFLINE_MAX_HOURS_DESCRIPTION,TUNABLE,TRUE
P04_AUTOSAVE_DEBOUNCE_SECONDS,DECIMAL,0.5,SECONDS,0,5,TXT_P04_AUTOSAVE_DEBOUNCE_DESC,TUNABLE,TRUE
P04_DRAWER_ANIMATION_SECONDS,DECIMAL,0.22,SECONDS,0,1,TXT_P04_DRAWER_DURATION_DESC,TUNABLE,TRUE
P04_TEST_CLOCK_MODE,BOOLEAN,FALSE,BOOL,,,TXT_P04_DEV_CLOCK_DESC,CONFIRMED,TRUE
P04_TIMER_UI_TICK_SECONDS,DECIMAL,1,SECONDS,0.1,5,TXT_P04_TIMER_TICK_DESC,TUNABLE,TRUE
P06_ARMOR_K,INTEGER,100,POINT,1,1000,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_BASIC_ATTACK_COEFFICIENT_BPS,INTEGER,10000,BPS,1000,50000,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_CONTRIBUTION_COMBAT_CAP,INTEGER,1000,POINT,1,100000,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_DAMAGE_MIN,INTEGER,1,HP,1,100,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_LEASH_DISTANCE_MILLI,INTEGER,12000,MILLI_TILE,1000,30000,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_MAX_CATCHUP_TICKS,INTEGER,5,TICKS,0,10,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_MAX_PATH_RETRIES,INTEGER,3,COUNT,0,10,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_MAX_RUNTIME_ENTITIES,INTEGER,60,COUNT,10,60,TXT_P06_REGION_TITLE,CONFIRMED,TRUE
P06_PATH_CACHE_CAPACITY,INTEGER,128,ENTRIES,0,512,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_PATH_EXPANSIONS_PER_TICK,INTEGER,240,NODES,32,2048,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_PATH_REPLAN_COOLDOWN_TICKS,INTEGER,5,TICKS,1,50,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_SIMULATION_TICK_HZ,INTEGER,10,HZ,5,30,TXT_P06_REGION_TITLE,CONFIRMED,TRUE
P06_STUCK_TICKS,INTEGER,20,TICKS,5,100,TXT_P06_REGION_TITLE,TUNABLE,TRUE
P06_THREAT_DECAY_BPS_PER_TICK,INTEGER,50,BPS,0,1000,TXT_P06_REGION_TITLE,TUNABLE,TRUE
RAID_PARTY_MAX,INTEGER,8,COUNT,1,16,TXT_RAID_PARTY_MAX_DESCRIPTION,TUNABLE,TRUE
RAID_PARTY_MIN,INTEGER,6,COUNT,1,8,TXT_RAID_PARTY_MIN_DESCRIPTION,TUNABLE,TRUE
RARE_EQUIPMENT_AUTO_PROTECT,BOOLEAN,TRUE,BOOL,,,TXT_RARE_EQUIPMENT_AUTO_PROTECT_DESCRIPTION,TUNABLE,TRUE
ROSTER_CAP_V1,INTEGER,24,COUNT,1,24,TXT_ROSTER_CAP_V1_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_HIGH,DECIMAL,1.15,RATIO,0.5,2,TXT_STORE_PRICE_HIGH_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_LOW,DECIMAL,0.9,RATIO,0.5,2,TXT_STORE_PRICE_LOW_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_STANDARD,DECIMAL,1.0,RATIO,0.5,2,TXT_STORE_PRICE_STANDARD_DESCRIPTION,TUNABLE,TRUE
```

#### `skill_runtime_rules.csv` final bytes

```csv
skill_id,cast_range_milli,cast_time_ticks,recovery_ticks,target_count,radius_milli,coefficient_bps,effect_type,status_effect_id,effect_value_bps,interruptible,status,enabled
SK_WAR_HEAVY_SLASH,1500,2,3,1,0,16000,DAMAGE,,0,TRUE,TUNABLE,TRUE
SK_WAR_BATTLE_RUSH,5000,2,4,1,0,12000,DASH_DAMAGE,,0,TRUE,TUNABLE,TRUE
SK_WAR_TENACITY,0,0,0,1,0,0,PASSIVE_LOW_HP_DEFENSE,,2000,FALSE,TUNABLE,TRUE
SK_GUA_TAUNT,4200,1,3,6,4200,5000,DAMAGE_STATUS,STATUS_TAUNT,0,TRUE,TUNABLE,TRUE
SK_GUA_FORTRESS,0,1,3,1,0,0,STATUS,STATUS_BARRIER,8000,TRUE,TUNABLE,TRUE
SK_GUA_GUARD_MARK,6000,0,0,1,0,0,PASSIVE_ALLY_GUARD,,2500,FALSE,TUNABLE,TRUE
SK_ARC_QUICK_SHOT,6500,1,2,1,0,12500,DAMAGE,,0,TRUE,TUNABLE,TRUE
SK_ARC_PIERCE,7000,2,4,3,1000,14000,LINE_DAMAGE,,0,TRUE,TUNABLE,TRUE
SK_ARC_HUNTER_EYE,0,0,0,1,0,0,PASSIVE_ACCURACY_CRIT,,1000,FALSE,TUNABLE,TRUE
SK_MAG_FIRE_BURST,6000,3,4,5,2400,13500,AREA_DAMAGE_STATUS,STATUS_BURN,2500,TRUE,TUNABLE,TRUE
SK_MAG_FROST_NOVA,3200,2,5,6,3200,9000,AREA_DAMAGE_STATUS,STATUS_SLOW,3000,TRUE,TUNABLE,TRUE
SK_MAG_MANA_FLOW,0,0,0,1,0,0,PASSIVE_STATUS_POWER,,1500,FALSE,TUNABLE,TRUE
SK_CLE_HEAL,5500,2,3,1,0,12000,HEAL,,0,TRUE,TUNABLE,TRUE
SK_CLE_BARRIER,5500,2,4,1,0,8000,STATUS,STATUS_BARRIER,8000,TRUE,TUNABLE,TRUE
SK_CLE_BLESSING,5000,2,4,6,5000,0,STATUS,STATUS_BLESSING,1000,TRUE,TUNABLE,TRUE
```

## 15. P05→P06 Save migration·command/journal goldens

**상태: MIGRATION / CONFIRMED**

P05→P06 initial migration fields remain unchanged; schema is the P05 full schema SHA-256 `d0f20a54d5096f6dd4cf1b4b859177fa730477634e10e16a237ea8a995d614d5` selected for content .1-.4. New game/before/after full JSON and digests below are executable.

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.4",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 1,
  "createdAtUtc": "2026-07-21T00:00:00.000Z",
  "savedAtUtc": "2026-07-21T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "3261e57e670ab82d5dec52557af69b3f174456057410a2c149ea944acaa7d9e9",
    "fileSha256": "95a9509be823ac6b9f1bd3f093493eec727bed5ae6043c2da50149006c79c759"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 5000,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000001",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1001",
        "appearanceSeed": "2001",
        "growthSeed": "3001",
        "displayName": "레온",
        "jobId": "JOB_WARRIOR",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_PRACTICAL",
        "traitIds": [
          "TRAIT_STRONG"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-21T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-21T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000002",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1002",
        "appearanceSeed": "2002",
        "growthSeed": "3002",
        "displayName": "미라",
        "jobId": "JOB_GUARDIAN",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_CAUTIOUS",
        "traitIds": [
          "TRAIT_STURDY"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-21T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-21T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000003",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1009",
        "appearanceSeed": "2003",
        "growthSeed": "3003",
        "displayName": "아린",
        "jobId": "JOB_ARCHER",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_BRAVE",
        "traitIds": [
          "TRAIT_KEEN_EYE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-21T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-21T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000004",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1004",
        "appearanceSeed": "2004",
        "growthSeed": "3004",
        "displayName": "세라",
        "jobId": "JOB_CLERIC",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_FRUGAL",
        "traitIds": [
          "TRAIT_ARCANE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-21T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-21T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      }
    ],
    "managementNpcs": [
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000004",
        "professionId": "NPC_HEALER",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      }
    ],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-21T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-21T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-21T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.3",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 1,
  "createdAtUtc": "2026-07-20T00:00:00.000Z",
  "savedAtUtc": "2026-07-20T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b",
    "fileSha256": "a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 5000,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000001",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1001",
        "appearanceSeed": "2001",
        "growthSeed": "3001",
        "displayName": "레온",
        "jobId": "JOB_WARRIOR",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_PRACTICAL",
        "traitIds": [
          "TRAIT_STRONG"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000002",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1002",
        "appearanceSeed": "2002",
        "growthSeed": "3002",
        "displayName": "미라",
        "jobId": "JOB_GUARDIAN",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_CAUTIOUS",
        "traitIds": [
          "TRAIT_STURDY"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000003",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1009",
        "appearanceSeed": "2003",
        "growthSeed": "3003",
        "displayName": "아린",
        "jobId": "JOB_ARCHER",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_BRAVE",
        "traitIds": [
          "TRAIT_KEEN_EYE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000004",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1004",
        "appearanceSeed": "2004",
        "growthSeed": "3004",
        "displayName": "세라",
        "jobId": "JOB_CLERIC",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_FRUGAL",
        "traitIds": [
          "TRAIT_ARCANE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      }
    ],
    "managementNpcs": [
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000004",
        "professionId": "NPC_HEALER",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      }
    ],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-20T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-20T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-20T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.4",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 2,
  "createdAtUtc": "2026-07-20T00:00:00.000Z",
  "savedAtUtc": "2026-07-21T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b",
    "fileSha256": "42fa5de8ab52657b1d721a948ba41607f61b552b1cc892f485ca5b7bec100aa3"
  },
  "payload": {
    "profile": {
      "nickname": "왕국",
      "locale": "ko-KR",
      "playerExp": 0,
      "progressionFlagIds": []
    },
    "kingdom": {
      "kingdomStageId": "KINGDOM_1",
      "kingdomGold": 5000,
      "activeMercenaryLimit": 4,
      "ownedMercenaryLimit": 8,
      "kingdomExp": 0,
      "progressionFlagIds": [],
      "facilityUpgradeCount": 0,
      "pricingPolicy": "STANDARD",
      "regionAccessPolicies": [
        {
          "regionId": "REGION_R01",
          "allowed": true
        }
      ],
      "inventoryPolicies": {
        "autoSellMaxTier": 0,
        "protectQualityIds": []
      }
    },
    "mercenaries": [
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000001",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1001",
        "appearanceSeed": "2001",
        "growthSeed": "3001",
        "displayName": "레온",
        "jobId": "JOB_WARRIOR",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_PRACTICAL",
        "traitIds": [
          "TRAIT_STRONG"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000002",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1002",
        "appearanceSeed": "2002",
        "growthSeed": "3002",
        "displayName": "미라",
        "jobId": "JOB_GUARDIAN",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_CAUTIOUS",
        "traitIds": [
          "TRAIT_STURDY"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000003",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1009",
        "appearanceSeed": "2003",
        "growthSeed": "3003",
        "displayName": "아린",
        "jobId": "JOB_ARCHER",
        "gradeId": "GRADE_B",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_BRAVE",
        "traitIds": [
          "TRAIT_KEEN_EYE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      },
      {
        "instanceId": "019f7cd2-8800-7002-8000-000000000004",
        "generationProfileId": "GEN_MERC_STANDARD_V1",
        "nameSeed": "1004",
        "appearanceSeed": "2004",
        "growthSeed": "3004",
        "displayName": "세라",
        "jobId": "JOB_CLERIC",
        "gradeId": "GRADE_C",
        "rankId": "RANK_APPRENTICE",
        "level": 1,
        "exp": 0,
        "personalityId": "PERSONALITY_FRUGAL",
        "traitIds": [
          "TRAIT_ARCANE"
        ],
        "personalGold": 500,
        "contribution": 0,
        "active": true,
        "autonomy": {
          "state": "IDLE_TOWN",
          "currentRegionId": null,
          "targetInstanceId": null,
          "reasonCode": "NONE",
          "stateStartedAtUtc": "2026-07-20T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-20T00:00:00.000Z"
        },
        "potions": [],
        "equipmentSlots": {
          "WEAPON": null,
          "ARMOR": null,
          "HELMET": null,
          "ACCESSORY": null
        },
        "promotion": {
          "status": "NONE",
          "targetRankId": null,
          "operationId": null,
          "startedAtUtc": null,
          "finishesAtUtc": null,
          "costSnapshot": null
        },
        "records": {
          "huntCount": 0,
          "killCount": 0,
          "raidClearCount": 0,
          "itemsCollected": 0
        }
      }
    ],
    "managementNpcs": [
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000001",
        "professionId": "NPC_MERCHANT",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000002",
        "professionId": "NPC_BLACKSMITH",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000003",
        "professionId": "NPC_ALCHEMIST",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      },
      {
        "instanceId": "019f7cd2-8800-7001-8000-000000000004",
        "professionId": "NPC_HEALER",
        "proficiencyId": "NPC_APPRENTICE",
        "proficiencyExp": 0,
        "assignedFacilityId": null,
        "working": false
      }
    ],
    "facilities": [
      {
        "facilityId": "FAC_TAVERN",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_LODGE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_GUILD",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_STORE",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_BLACKSMITH",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_ALCHEMY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_WAREHOUSE",
        "level": 1,
        "state": "ACTIVE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      },
      {
        "facilityId": "FAC_INFIRMARY",
        "level": 1,
        "state": "BUILDABLE",
        "assignedNpcInstanceId": null,
        "job": null,
        "storage": []
      }
    ],
    "inventory": {
      "itemStacks": [],
      "equipment": [],
      "warehousePotions": []
    },
    "regions": {
      "progress": [
        {
          "regionId": "REGION_R01",
          "unlocked": true,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": "2026-07-20T00:00:00.000Z",
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R02",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R03",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R04",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        },
        {
          "regionId": "REGION_R05",
          "unlocked": false,
          "progressPercent": 0,
          "highestRankReachedId": null,
          "huntCount": 0,
          "eliteKillCount": 0,
          "firstUnlockedAtUtc": null,
          "lastVisitedAtUtc": null
        }
      ],
      "raids": []
    },
    "recruitmentMockState": {
      "authority": "MOCK_ONLY",
      "serverRevision": null,
      "premiumWalletCache": {
        "freePremium": 0,
        "paidPremium": 0,
        "specialRecruitTickets": 0
      },
      "pityCounters": [],
      "featuredGuarantees": [],
      "pendingRequests": []
    },
    "tutorial": {
      "currentStepId": "TUTORIAL_KINGDOM_INTRO",
      "completedStepIds": [],
      "grantedRewardIds": [],
      "skipped": false,
      "completedAtUtc": null
    },
    "offline": {
      "accrualCursorUtc": "2026-07-20T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-20T00:00:00.000Z",
      "pendingSettlement": null
    },
    "operationJournal": [],
    "settings": {
      "masterVolume": 1,
      "musicVolume": 0.8,
      "sfxVolume": 1,
      "vibrationEnabled": true,
      "battleSpeed": 1
    },
    "extensions": {}
  }
}
```

| Vector | payload JCS bytes | payload SHA-256 | envelope SHA-256 |
|---|---|---|---|
| P06_NEW | 7195 | 3261e57e670ab82d5dec52557af69b3f174456057410a2c149ea944acaa7d9e9 | 95a9509be823ac6b9f1bd3f093493eec727bed5ae6043c2da50149006c79c759 |
| P05_BEFORE | 7195 | 46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b | a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a |
| P06_AFTER | 7195 | 46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b | 42fa5de8ab52657b1d721a948ba41607f61b552b1cc892f485ca5b7bec100aa3 |


### START_HUNT executable golden

Request full JSON:

```json
{
  "commandType": "START_HUNT",
  "operationId": "019f8320-1800-7000-8000-000000000001",
  "huntOperationId": "019f8320-1800-7000-8000-000000000002",
  "expectedRevision": 2,
  "regionId": "REGION_R01",
  "partyMercenaryInstanceIds": [
    "019f7cd2-8800-7002-8000-000000000001",
    "019f7cd2-8800-7002-8000-000000000002",
    "019f7cd2-8800-7002-8000-000000000003",
    "019f7cd2-8800-7002-8000-000000000004"
  ],
  "requestHash": "97f7c3d1c799948c48cbd00ffe54188ed4b7084c3544a13886620deb2a94faa1"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"START_HUNT","expectedRevision":2,"huntOperationId":"019f8320-1800-7000-8000-000000000002","operationId":"019f8320-1800-7000-8000-000000000001","partyMercenaryInstanceIds":["019f7cd2-8800-7002-8000-000000000001","019f7cd2-8800-7002-8000-000000000002","019f7cd2-8800-7002-8000-000000000003","019f7cd2-8800-7002-8000-000000000004"],"regionId":"REGION_R01"}
```

requestHash=`97f7c3d1c799948c48cbd00ffe54188ed4b7084c3544a13886620deb2a94faa1`. Result full JSON:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000001",
  "huntOperationId": "019f8320-1800-7000-8000-000000000002",
  "revisionBefore": 2,
  "revisionAfter": 3,
  "regionId": "REGION_R01",
  "canonicalPartyMercenaryInstanceIds": [
    "019f7cd2-8800-7002-8000-000000000001",
    "019f7cd2-8800-7002-8000-000000000002",
    "019f7cd2-8800-7002-8000-000000000003",
    "019f7cd2-8800-7002-8000-000000000004"
  ],
  "persistedState": "TRAVEL_TO_REGION",
  "eventCodes": [
    "HuntPrepared",
    "HuntStarted"
  ],
  "replayed": false,
  "resultDigest": "44b1cbbbca4845168ea9f9016b2b3f511e01a97657de4b7d2306dfddcf3c77c9"
}
```

resultDigest=`44b1cbbbca4845168ea9f9016b2b3f511e01a97657de4b7d2306dfddcf3c77c9`. Journal full row:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000001",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "97f7c3d1c799948c48cbd00ffe54188ed4b7084c3544a13886620deb2a94faa1",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-21T01:00:00.000Z",
  "updatedAtUtc": "2026-07-21T01:00:00.000Z",
  "completedAtUtc": "2026-07-21T01:00:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "44b1cbbbca4845168ea9f9016b2b3f511e01a97657de4b7d2306dfddcf3c77c9",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P06_OPERATION_HASH_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


### RECALL_HUNT/P06 terminal executable golden

Request full JSON:

```json
{
  "commandType": "RECALL_HUNT",
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "expectedRevision": 3,
  "reasonCode": "PLAYER_RECALL",
  "requestHash": "027b3f13dd4dc316455ae6b668d5f8cbcf6d4805f472b16d3303eb7bebb4e920"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"RECALL_HUNT","expectedRevision":3,"operationId":"019f8320-1800-7000-8000-000000000002","reasonCode":"PLAYER_RECALL"}
```

requestHash=`027b3f13dd4dc316455ae6b668d5f8cbcf6d4805f472b16d3303eb7bebb4e920`. Result full JSON:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "revisionBefore": 3,
  "revisionAfter": 4,
  "terminalState": "IDLE_TOWN",
  "killCountDelta": 4,
  "huntCountDeltaPerPartyMember": 1,
  "contributionDelta": [
    103,
    96,
    121,
    110
  ],
  "personalGoldDelta": [
    7,
    7,
    7,
    7
  ],
  "rewardJournalCount": 1,
  "replayed": false,
  "resultDigest": "a4503d54e3c8082d7ab49a019f92bf8eb5673e869b494c822c8434c79fe692e8"
}
```

resultDigest=`a4503d54e3c8082d7ab49a019f92bf8eb5673e869b494c822c8434c79fe692e8`. Journal full row:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "027b3f13dd4dc316455ae6b668d5f8cbcf6d4805f472b16d3303eb7bebb4e920",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-21T01:02:00.000Z",
  "updatedAtUtc": "2026-07-21T01:02:00.000Z",
  "completedAtUtc": "2026-07-21T01:02:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "a4503d54e3c8082d7ab49a019f92bf8eb5673e869b494c822c8434c79fe692e8",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P06_OPERATION_HASH_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


START_HUNT journal purpose uses existing operationType=REWARD because saveVersion1 enum is immutable. Start checkpoint is revision2→3. Recall flag is transient; RETURN_TOWN terminal commit is revision3→4 and owns the second REWARD row. Crash immediately after START journal produces valid TRAVEL state and load normalization; crash after terminal journal atomic replace recovers COMMITTED and never reapplies.

## 16. command·query·DTO·party authority

**상태: CONFIRMED**

`StartHuntCommand(OperationId,HuntOperationId,ExpectedRevision,RequestHash,RegionId,PartyMercenaryInstanceIds)` requires 1..4 IDs. UI selection order defines formation slots after validation; Domain rejects empty, duplicate, unknown, inactive, non-IDLE_TOWN, INJURED/downed and >4. It never auto-selects. UI helper may preselect instanceId-ascending active town-safe IDs before request creation. Result returns the canonical request order unchanged. Retry requires identical party/hash. Any member changed after expectedRevision yields revision conflict and no partial party.

`RecallHuntCommand(OperationId=HuntOperationId,ExpectedRevision,RequestHash,ReasonCode=PLAYER_RECALL)` sets a transient flag at next tick; terminal result is produced at town arrival. Public interfaces, query DTO isolation, cancellation and Presentation→Application→Domain→Infrastructure direction from v1.0 remain binding.

## 17. event·error registry

**상태: CONFIRMED**

| Code | Exact condition | Rollback | Retryable | Mapping | ko key | Recovery action |
|---|---|---|---|---|---|---|
| P06_PARTY_INVALID | selection count/duplicate/unknown/inactive/non-town-safe/downed | YES | FALSE | 400/domain | TXT_P06_PARTY_INVALID | return to party selection |
| P06_SAVE_REVISION_CONFLICT | expectedRevision mismatch | YES | TRUE | 409/UI reload | TXT_P05_ERROR_REVISION | reload and reselect |
| P06_OPERATION_HASH_MISMATCH | same operationId different requestHash | YES | FALSE | 409/fatal operation | TXT_P06_ERROR | new operation after reload |
| P06_REGION_NOT_AVAILABLE | region not R01/unlocked | YES | FALSE | 423/UI locked | TXT_P06_LOCKED | back |
| P06_PATH_UNREACHABLE | A* no route | NO transient | TRUE | domain return | TXT_P06_PATH_FAILED | retry/replan |
| P06_PATH_POOL_EXHAUSTED | path pool empty | NO transient | TRUE | 503/local | TXT_P06_POOL_EXHAUSTED | safe return |
| P06_TARGET_INVALID | target missing/dead | NO transient | TRUE | domain retarget | TXT_P06_ERROR | retarget |
| P06_STATE_INVALID | autonomy invariant | YES | FALSE | 500/error | TXT_P06_ERROR | reload recovery |
| P06_STATE_DEFERRED | later-owner state in P06 | YES normalization | FALSE | domain normalize | TXT_P06_STATE_DEFERRED | return town |
| P06_RETURN_FAILED | three return replans fail | NO terminal safe warp | FALSE | domain fallback | TXT_P06_RETURN_FAILED | automatic safe return |
| P06_ENCOUNTER_SPAWN_EXHAUSTED | no valid spawn | NO transient | TRUE | 503/local | TXT_P06_ERROR | end hunt |
| P06_COMBAT_INVARIANT | HP/threat/status invalid | YES terminal | FALSE | 500/error | TXT_P06_ERROR | abort and recover |
| P06_COMBAT_NUMERIC_OVERFLOW | checked Int64 overflow | YES | FALSE | 500/error | TXT_P06_ERROR | abort and report |
| P06_CONTENT_MISSING | required content/asset absent | YES | FALSE | 500/content | TXT_P06_CONTENT_MISSING | content error screen |
| P06_ENTITY_POOL_EXHAUSTED | entity view >60 | NO transient | FALSE | 503/local | TXT_P06_POOL_EXHAUSTED | end hunt |
| P06_PERFORMANCE_GUARD | queue age/dropped tick threshold | NO terminal | TRUE | 503/local | TXT_P06_PERFORMANCE_GUARD | safe return |

Test-only Mage visibility assertion is `P06T_FIXTURE_NOT_PLAYER_VISIBLE`, not a runtime error. Potion deferral is a capability flag, not an error. Validator extracts every returned/raised `P06_` code from C# and requires equality with this registry; documentation check result: MATCH.

## 18. Region UI exact hierarchy·states·accessibility

**상태: CONFIRMED**

| Stable UI ID | Parent | Anchor/pivot | Rect | Touch | Key | Visible | Interactable | Disabled key | Focus | Block |
|---|---|---|---|---|---|---|---|---|---|---|
| P06_UI_PARTY_MODAL | RegionScreen | center/.5 | 560,140,800,800 | 64 | TXT_P06_PARTY_SELECT | pre-hunt | 1..4 valid | TXT_P06_PARTY_REQUIRED | 1 | modal |
| P06_UI_PARTY_CARD | P06_UI_PARTY_MODAL/List | stretch/.5 | 24,row,752,112 | 72 | TXT_P05_STATUS_ACTIVE | eligible roster | active+town-safe | inactive/state key | 2..25 | row |
| P06_UI_MEMBER_CARD | PartyPanel | top/.5 | 16,row,388,176 | 64 | TXT_P06_STATUS_HP | hunt | read-only |  | 10..13 | none |
| P06_UI_HP_BAR | P06_UI_MEMBER_CARD | top stretch/.5 | 16,48,356,24 | 0 | TXT_P06_STATUS_HP | alive/downed | none |  |  | none |
| P06_UI_BARRIER_BAR | P06_UI_MEMBER_CARD | top stretch/.5 | 16,76,356,12 | 0 | TXT_P06_STATUS_HP | barrier>0 | none |  |  | none |
| P06_UI_STATUS_STRIP | P06_UI_MEMBER_CARD | bottom stretch/.5 | 16,112,356,40 | 0 |  | effect count>0 | none |  |  | none |
| P06_UI_ENCOUNTER | WorldViewport | top/.5 | 510,16,480,72 | 0 | TXT_P06_REGION_TITLE | hunt | none |  |  | none |
| P06_UI_AUTONOMY | BottomBar | left/.5 | 24,20,720,72 | 0 | TXT_P06_STATUS_ACTION | hunt | none |  |  | none |
| P06_UI_RECALL | BottomBar | right/.5 | 1640,20,240,72 | 72 | TXT_P06_RECALL | hunt | not settling | TXT_P06_ERROR | 30 | screen |
| P06_UI_RECALL_MODAL | RegionScreen | center/.5 | 660,330,600,420 | 64 | TXT_P06_RECALL_CONFIRM | recall tap | confirm/cancel |  | 1,2 | modal |
| P06_UI_PAUSE | TopBar | right/.5 | 1510,20,88,72 | 72 | TXT_P06_PAUSE | content | session exists |  | 28 | screen |
| P06_UI_RESULT | RegionScreen | center/.5 | 480,120,960,840 | 64 | TXT_P06_RESULT_TITLE | settlement committed | close |  | 1 | modal |
| P06_UI_STATE_OVERLAY | RegionScreen | stretch/.5 | 0,112,1920,856 | 64 | TXT_P06_LOADING | non-content state | state action |  | 1 | screen |

At 130% text, cards grow to220 and their parent ScrollRect owns overflow; long Korean wraps, no ellipsis. Gamepad focus follows numeric order. Modal blocks all screen input. Android Back closes recall/result/party modal, then pauses active hunt, then navigates back.

## 19. asset·Addressables·animation·Editor generation

**상태: CONFIRMED**

| assetId | Address | Subasset |
|---|---|---|
| ASSET_P06_CHARACTER_WARRIOR | P06/Characters/JOB_WARRIOR | CharactersAtlas/WARRIOR |
| ASSET_P06_CHARACTER_GUARDIAN | P06/Characters/JOB_GUARDIAN | CharactersAtlas/GUARDIAN |
| ASSET_P06_CHARACTER_ARCHER | P06/Characters/JOB_ARCHER | CharactersAtlas/ARCHER |
| ASSET_P06_CHARACTER_MAGE | P06/Characters/JOB_MAGE | CharactersAtlas/MAGE |
| ASSET_P06_CHARACTER_CLERIC | P06/Characters/JOB_CLERIC | CharactersAtlas/CLERIC |
| ASSET_P06_ICON_POISON | P06/Status/STATUS_POISON | StatusAtlas/POISON |
| ASSET_P06_ICON_BURN | P06/Status/STATUS_BURN | StatusAtlas/BURN |
| ASSET_P06_ICON_SLOW | P06/Status/STATUS_SLOW | StatusAtlas/SLOW |
| ASSET_P06_ICON_TAUNT | P06/Status/STATUS_TAUNT | StatusAtlas/TAUNT |
| ASSET_P06_ICON_BARRIER | P06/Status/STATUS_BARRIER | StatusAtlas/BARRIER |
| ASSET_P06_ICON_BLESSING | P06/Status/STATUS_BLESSING | StatusAtlas/BLESSING |

Characters atlas address `P06/Atlases/Characters`, each job sheet 480×96 with five 96×96 frames: Idle2(loop),Move4(loop),Attack4(no loop),Cast4(no loop),Down1(hold), 8fps; frames can reuse geometry but retain distinct subasset names `{JOB}_{STATE}_{00..03}`. Pivot(.5,.2),PPU64. Animator parameters `Speed float,Attack trigger,Cast trigger,Down bool`; AnyState Down, Idle↔Move Speed, trigger states return Idle. Prefab requires SpriteRenderer(Character),Animator,WorldEntityView,Collider2D disabled-for-physics; sorting Character. Status atlas has six32×32 subassets. Missing asset uses magenta `P06/Missing` only in Development and fails CI/player build. `P06GeneratedAssetVerifier --all-addresses` loads every listed address/subasset and rejects missing/duplicate/case mismatch.

```csv
asset_id,name,creator,source_url,version,acquired_date,price_krw,license,commercial_use,modification_allowed,credit_required,used_in,notes,status,enabled
ASSET_FACILITY_CONSTRUCTION_V1,Asset Facility Construction V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Construction; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_LOCKED_V1,Asset Facility Locked V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Locked; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_PLOT_V1,Asset Facility Plot V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Plot; generated placeholder,CONFIRMED,TRUE
ASSET_FACILITY_STOPPED_V1,Asset Facility Stopped V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Stopped; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_ALCHEMY_PLACEHOLDER_V1,Asset Fac Alchemy Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_ALCHEMY; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1,Asset Fac Blacksmith Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_BLACKSMITH; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_GUILD_PLACEHOLDER_V1,Asset Fac Guild Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_GUILD; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_INFIRMARY_PLACEHOLDER_V1,Asset Fac Infirmary Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_INFIRMARY; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_LODGE_PLACEHOLDER_V1,Asset Fac Lodge Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_LODGE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_STORE_PLACEHOLDER_V1,Asset Fac Store Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_STORE; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_TAVERN_PLACEHOLDER_V1,Asset Fac Tavern Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_TAVERN; generated placeholder,CONFIRMED,TRUE
ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1,Asset Fac Warehouse Placeholder V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/FAC_WAREHOUSE; generated placeholder,CONFIRMED,TRUE
ASSET_KINGDOM_BACKGROUND_V1,Asset Kingdom Background V1,KingdomTycoon Editor Generator,,1,2026-07-19,0,PROJECT_INTERNAL,TRUE,TRUE,FALSE,P04 Kingdom,address=P04/Kingdom/Background; generated placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_ARCHER_V1,Internal Archer Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_CLERIC_V1,Internal Cleric Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_GUARDIAN_V1,Internal Guardian Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_MAGE_V1,Internal Mage Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_MERC_PLACEHOLDER_WARRIOR_V1,Internal Warrior Placeholder,KINGDOM_TYCOON_TEAM,,1,2026-07-18,0,INTERNAL,TRUE,TRUE,FALSE,MERCENARY_APPEARANCE,Built-in production-valid placeholder,CONFIRMED,TRUE
ASSET_P06_CHARACTER_ARCHER,P06 Archer Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_ARCHER; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_CLERIC,P06 Cleric Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_CLERIC; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_GUARDIAN,P06 Guardian Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_GUARDIAN; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_MAGE,P06 Mage Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_MAGE; logical asset,CONFIRMED,TRUE
ASSET_P06_CHARACTER_WARRIOR,P06 Warrior Character,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Characters/JOB_WARRIOR; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BARRIER,P06 STATUS_BARRIER Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BARRIER; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BLESSING,P06 STATUS_BLESSING Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BLESSING; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_BURN,P06 STATUS_BURN Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_BURN; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_POISON,P06 STATUS_POISON Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_POISON; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_SLOW,P06 STATUS_SLOW Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_SLOW; logical asset,CONFIRMED,TRUE
ASSET_P06_ICON_TAUNT,P06 STATUS_TAUNT Icon,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,address=P06/Status/STATUS_TAUNT; logical asset,CONFIRMED,TRUE
ASSET_P06_MONSTER_ELITE,P06 Elite Monster,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 128px sprite,CONFIRMED,TRUE
ASSET_P06_MONSTER_MELEE,P06 Melee Monster,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 96px sprite,CONFIRMED,TRUE
ASSET_P06_PROJECTILE,P06 Projectile,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 24px sprite,CONFIRMED,TRUE
ASSET_P06_TILE_MEADOW,P06 Meadow Tile,INTERNAL,,1,2026-07-21,0,Project,TRUE,TRUE,FALSE,P06,Editor-generated 64px tile,CONFIRMED,TRUE
```

## 20. lifecycle·performance budget

**상태: CONFIRMED**

| Metric | Acceptance |
|---|---|
| simulation | 10Hz; 60 entities; tick p95<=4ms, p99<=8ms on Android reference |
| render | 30fps minimum; frame p95<=33.3ms |
| GC | steady combat <=1KiB/frame and <=4KiB/s simulation |
| path | 240 expansions/tick; oldest queue<=20 ticks; cache hit fixture exact |
| memory | 30min managed+native sustained growth<=8MiB after first 5min |
| listeners/handles | scene cycles10, counts return baseline |
| unhandled | exceptions0, invariant0, unrecovered Save0 |
| write | Start and terminal only; no per-tick Save |

## 21. EditMode·PlayMode·soak golden

**상태: CONFIRMED**

The v1.0 test matrix remains and adds: `P06-E-011 AStarQueueTrace`, `012 MovementFixedPointCollision`, `013 EncounterSpawnRawTrace`, `014 BasicAttack`, `015..024 TraitEach`, `025..030 PersonalityEach`, `031..034 PassiveEach`, `035 StatusFullTimeline`, `036 FiveJobRepresentativeSkills`, `037 StartRecallJournalHash`, `038 CheckpointKillRecovery`, `039 AssetAddressAll`, `040 ErrorRegistryCoverage`. Every test uses injected clock/UUID/RNG; sleep0. `P06-S-001` writes approved soak golden only with explicit `--approve-golden`; CI uses `--check` and cannot rewrite it.

## 22. Android build·수동 검증·capture

**상태: CONFIRMED**

| CaptureId | Fixture | Resolution | Filename | Required |
|---|---|---|---|---|
| P06_CAP_01 | R01 start | 1920×1080 | p06_01_region_start.png | party4/grid/HUD |
| P06_CAP_02 | path | 1920×1080 | p06_02_path.png | diagnostic path/tick |
| P06_CAP_03 | combat | 1920×1080 | p06_03_combat.png | HP/status/action |
| P06_CAP_04 | guardian | 1920×1080 | p06_04_taunt.png | taunt/threat |
| P06_CAP_05 | cleric | 1920×1080 | p06_05_heal.png | effective heal |
| P06_CAP_06 | recall | 1920×1080 | p06_06_recall.png | PLAYER_RECALL |
| P06_CAP_07 | error | 1920×1080 | p06_07_error.png | retry/diagnostic |
| P06_CAP_08 | 20:9 | 2400×1080 | p06_08_20x9.png | safe area/no clip |

Build entry `KingdomTycoon.Editor.P06AndroidBuilder.Build`, output `Builds/Android/KingdomTycoon-P06-Development.apk`, scenes Bootstrap/Kingdom/Region/Raid, IL2CPP ARM64, development+script debugging off. Success requires exit0, file exists, APK size/hash recorded BUILD_DERIVED, package content .1-.4, 30min soak pass and physical Android smoke of back/pause/resume/Safe Area.

## 23. 구현 파일 지도·커밋 분리

**상태: CONFIRMED**

| Logical slice | Exact new files | Exact modified files | Generated files | Verification command | Commit type | Commit message |
|---|---|---|---|---|---|---|
| design | docs/design/TYCOON_P06_MOVEMENT_COMBAT_AI_COMPLETE_DESIGN_v1.1.md | none | none | markdown gate | docs | docs: adopt P06 movement combat AI contract |
| content | StreamingAssets/Content/1.0.0-content.4; scripts/generate_p06_content.py | validate_content.py | 66 CSV/manifest | python scripts/generate_p06_content.py --check | feat | feat: add P06 deterministic combat content |
| domain | Runtime/Domain/Combat; Application/Combat | AppRoot composition | none | Unity EditMode | feat | feat: implement deterministic hunt simulation |
| world/UI | Runtime/Presentation/Combat; Editor/P06CombatSetup.cs | Region scene via Editor API | ContentGenerated/P06Combat | Unity PlayMode | feat | feat: add P06 region combat presentation |
| tests | Tests/EditMode/Combat; Tests/PlayMode/Combat | none | captures | Unity tests | test | test: cover P06 combat acceptance |
| ci | scripts/ci/p06.sh | .githooks/pre-commit;.githooks/pre-push | artifacts | scripts/ci/run-ci.sh | ci | ci: validate P06 package and Android build |
| report | docs/reports/P06_MOVEMENT_COMBAT_AI_REPORT.md | Draft PR | capture table | report checklist | docs | docs: report P06 implementation evidence |

## 24. P06 완료 checklist·P07 handoff

**상태: CONFIRMED**

- [x] v1.0과 수정 사항을 합친 독립 통합본
- [x] A* comparator/queue/path/cache goldens 일치
- [x] growth/Traits10/Personalities6/stat vectors 완결
- [x] basic/passives4/status timeline 완결
- [x] AI feature and fixed-point geometry exact
- [x] autonomy CSV single authority and Start party authority
- [x] StartHunt/Recall real hashes/journal
- [x] wildcard asset address 0 and UI exact hierarchy
- [x] error registry coverage MATCH
- [x] JSON/CSV/manifest/Save hashes recomputed
- [x] P06/P07 shared compatibility rows identical
- [x] soak approved-golden workflow fixed
- [x] Android/30-minute/P05 regression gate retained

## 25. 최종 선언

DOCUMENT_CONTENT_SHA256_SCOPE: UTF-8 bytes before this scope line

DOCUMENT_CONTENT_SHA256: ea905cc2c04f40130ae89b0279157da618ed4572830a6b92e282887f6c62675e

UNRESOLVED: NONE

IMPLEMENTATION_READY: YES
