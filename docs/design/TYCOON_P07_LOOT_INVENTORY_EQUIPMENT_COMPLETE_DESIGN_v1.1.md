# TYCOON P07 전리품·인벤토리·장비 최종 통합 설계 v1.1

## 1. 문서 상태·권위·supersedes

**상태: CONFIRMED**

| 항목 | Exact value |
|---|---|
| 문서 | TYCOON_P07_LOOT_INVENTORY_EQUIPMENT_COMPLETE_DESIGN_v1.1.md |
| Phase | P07_LOOT_INVENTORY_EQUIPMENT |
| 권위 | P06 v1.0 → P05 implementation → P05 design → P04 → P03 |
| Unity | 6000.3.20f1 |
| contentVersion | 1.0.0-content.5 |
| saveVersion/schemaId | 1 / urn:tycoon:save:v1 |
| supersedes | P07 auto-sale ownership and carried inventory alternatives |
| 구현 차단점 | NONE |

## 2. P07 범위·Phase 경계

**상태: CONFIRMED**

| Capability | P07 | Deferred owner |
|---|---|---|
| loot resolution/snapshot | IMPLEMENT local R01 | P14 raid/P15 offline |
| global inventory/equipment/potions | IMPLEMENT | P10 enhancement/refine |
| auto/manual equip/lock/policies | IMPLEMENT | none |
| auto/manual liquidation | IMPLEMENT personalGold only | P08 merchant price/purchase/kingdomGold |
| combat modifier | IMPLEMENT cache invalidation | P11 growth |
| shop/production/craft | NO | P08/P09/P10 |
| full region | NO | P12 |
| server | NO HTTP | P16 |

## 3. P06 handoff·conflict resolution·migration summary

**상태: CORRECTION / MIGRATION / CONFIRMED**

`payload.kingdom.inventoryPolicies` is the only persistent policy authority. `payload.inventory` remains exactly itemStacks/equipment/warehousePotions. P07 terminal owner is CommitHuntSettlement at RETURN_TOWN once per hunt; encounter writes0. Auto-sell candidate and score are transient, actual sell/equip is town terminal. Enhancement is0..10. Warehouse↔mercenary potion commands are part of P07.

### Shared compatibility table

| 계약 | 권위 문서/모듈 | 저장 경로 | 쓰기 시점 | revision | journal | crash 정책 |
|---|---|---|---|---|---|---|
| hunt party | P06 Combat application | payload.mercenaries[].autonomy(state,currentRegionId,targetInstanceId,reasonCode,times); canonical party in START_HUNT result | StartHunt atomic checkpoint | +1 | operationType=REWARD 1 row; purpose START_HUNT | pre-commit kill: old Save; post-commit kill: TRAVEL state then load normalizes to IDLE with zero settlement |
| pending loot | P07 CombatLoot runtime | transient PendingLootBuffer keyed by huntOperationId | each encounter memory only | 0 | 0 | RETURN_TOWN commit 전에 process death: entire buffer lost and never shown |
| hunt settlement | P07 CommitHuntSettlement + P06 terminal seam | inventory, mercenaries records/wallet/equipment/potions/autonomy, operationJournal | RETURN_TOWN exactly once | +1 | operationType=REWARD exactly 1 row keyed by huntOperationId | old candidate or full terminal candidate; COMMITTED replay returns stored digest |
| inventory policy | P05 Kingdom + P07 InventoryPolicyService | payload.kingdom.inventoryPolicies | SetInventoryPolicies; settlement read-only snapshot | +1 for command; 0 for read | operationType=REWARD 1 row for policy command | pre-commit old policy; post-commit new policy; hash mismatch fails |
| potion ownership | P05/P07 PotionTransferService | payload.inventory.warehousePotions and payload.mercenaries[].potions | transfer/return command or terminal consumption | +1 per transfer; included in settlement +1 | operationType=REWARD 1 row per transfer; settlement shares its one row | atomic source/destination quantities; no partial transfer |
| equipment stats | P07 EquipmentProjection → P06 CombatStatPipeline | payload.inventory.equipment + payload.mercenaries[].equipmentSlots; derived cache transient | town equip command or terminal auto-equip | +1 command or included settlement +1 | operationType=REWARD 1 row per manual command; settlement shares its one row | bidirectional links are old or complete; cache rebuilt after load/event |

### Shared tick/transaction sequence

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

## 4. loot table·RNG·reward resolution

**상태: CONFIRMED**

All production tables are INDEPENDENT. Entry order is entry_no; disabled/condition-false consumes0 draws. Each enabled true entry consumes probability bounded1,000,000; success `< probability*1,000,000`. Successful min<max consumes inclusive quantity bound; equal consumes0. SplitMix64 and unbiased limit are the shared P06 function. No direct `% n` without acceptance test is permitted.

Stream bytes=`UTF8(KT|P07_LOOT_V1|1.0.0-content.5|huntOperationId|encounterIndex)`, SHA-256 first8 big-endian seed. Fixture seed=`3933518812831075410`.

| drawIndex | raw uint64 | bound | rejection limit | reject | accepted |
|---|---|---|---|---|---|
| 0 | 11373512251824306030 | 1000000 | 18446744073709000000 | NO | 306030 |
| 1 | 6514924633081954734 | 2 | 18446744073709551616 | NO | 0 |
| 2 | 17447235620859875722 | 1000000 | 18446744073709000000 | NO | 875722 |
| 3 | 9487372518197105740 | 1000000 | 18446744073709000000 | NO | 105740 |
| 4 | 5267149625346456796 | 4 | 18446744073709551616 | NO | 0 |
| 5 | 18430482160183153902 | 100 | 18446744073709551600 | NO | 2 |
| 6 | 13872711261042295418 | 10000 | 18446744073709550000 | NO | 5418 |


draw0 entry1 probability, draw1 quantity1..2, draw2 entry2 probability, draw3 entry3 probability; if random equipment succeeds, its independent equipment stream uses template/quality/affix sequence. In this trace result is derived strictly from accepted values. no-drop fixture uses probability accepted>=threshold. Boundary fixture injects accepted749999/750000 for .75 success/fail. Condition false shifts no later index. Quantity accepted0/1 yields1/2.

Generic rejection golden uses seed0, bound9223372036854775809, limit=`9223372036854775809`, raws=`[16294208416658607535, 7960286522194355700]`, first accepted index=`1`, value=`7960286522194355700`. Same seed reproduces byte-identical trace.

Existing equipment golden remains: seed987654321 raws12744715263588028796,16192141852193020578,16161435109270938784 → EQ_T1_CLERIC_WEAPON/QUALITY_COMMON/null, trace hash11a39fbb855f367a9cefe06e09d873bf848c56c6fe45411a7b841b7c669853f0. EQUIPMENT_TEMPLATE and RANDOM_EQUIPMENT_TIER require the full 9-field generated snapshot before commit.

## 5. ownership·container·capacity·overflow

**상태: CONFIRMED / TUNABLE**

Global inventory is authoritative. PendingLootBuffer is transient and keyed by huntOperationId; capacity is snapshotted at hunt start with inventory revision, and manual inventory commands are rejected while the local hunt owns the inventory mutation gate. Terminal commit still compares expected revision.

Capacity computes merged stack rows first. It removes terminal auto-sell candidates before counting. If new loot still overflows, only newly acquired buffer lines are deterministic discard candidates: unprotected `(tier asc, qualityOrder asc, acquisitionOrdinal asc, canonicalId asc)`, then protected `(protectionPriority firstDiscovery=1,quality=2,boss=3,locked=4 asc, same keys)`. Existing inventory and equipped items are never discarded. Discarded lines appear in result and telemetry `P07_LOOT_DISCARDED_CAPACITY`; settlement still commits counters/autonomy and finishes. No quarantine Save field exists. Buffer reaching its configured line cap stops further encounters and returns; it never drops the current completed encounter.

Fill ratio and L1..4 capacity rows remain v1.0. Exactly-full after merge succeeds; one over follows discard. Policy revision conflict aborts before terminal mutation, reloads policy, recomputes once with same snapshots, then commits or returns conflict.

## 6. item stack·equipment instance·potion ownership

**상태: CONFIRMED**

EquipmentInstance fields remain the P05 ten fields; `equipmentTemplateId` has no alias. enhancementLevel is integer0..10 everywhere. Immutable: instance/template/tier/quality/sourceContentVersion/generationOperationId. Mutable: enhancement/refine(P10),locked,equip link. UUIDv7 is allocated after all reward draws in reward canonical order. randomTraceHash is inside terminal generated snapshot/resultDigest; committed instance is complete recovery authority. Item rows merge atomically and zero rows are removed.

Potion ownership paths are `payload.inventory.warehousePotions[]` and `payload.mercenaries[].potions[]`. Each mercenary allows four distinct potion IDs and safe-int quantity; same ID merges. Transfer and return are full-quantity atomic, town-safe, no partial success. Source quantity, destination distinct-slot capacity, known active or inactive owned mercenary are validated; inactive is allowed in town, unknown is rejected.

## 7. five-job equipment score·P06 combat stat modifier

**상태: CONFIRMED / TUNABLE**

Eligible profiles are exact below; source is BOSS or CRAFT. A mismatched profile fails before score. `Pq=floor(base_power*qualityBps/10000)`, then `floor(Pq*(10000+enhancementLevel*500)/10000)`, enhancement0..10. Slot/profile flat mapping: SWORD ATTACK100%; HAMMER_SHIELD ATTACK60%+DEF40%; BOW ATTACK95%+CRIT min(1000,Pq*5); STAFF ATTACK90%+STATUS_POWER min(1500,Pq*6); MACE ATTACK65%+HEAL55%; HEAVY HP4Pq+DEF50%; LIGHT HP3Pq+DEF33%+MOVE additive min(500,Pq); CLOTH HP2Pq+DEF25%+STATUS_POWER min(1000,Pq*3); POWER ATTACK70%+CRIT min(800,Pq*4); GUARD HP3Pq+DEF50%+STATUS_RESIST min(1000,Pq*3); WISDOM HEAL80%+STATUS_POWER min(1200,Pq*4).

Refine options apply after flat: ATTACK/DEFENSE/HP percent to that stat; CRIT additive bps; FIRE/BOSS/PART/POISON/MATERIAL/RARE typed sidecar; FROST typed resist. Slot order WEAPON,ARMOR,HELMET,ACCESSORY. P06 pipeline is job growth→equipment flat→trait/refine additive bps→channel multiplier→clamp. ATTACK_SPEED/cooldown/RANGE never change in P07; LIGHT changes MOVE_SPEED only. Equip/unequip invalidates cache. currentHP after max change=`min(oldCurrentHP,newMaxHP)`; never scale ratio.

| Job | Allowed profiles |
|---|---|
| JOB_WARRIOR | SWORD,HEAVY,POWER |
| JOB_GUARDIAN | HAMMER_SHIELD,HEAVY,GUARD |
| JOB_ARCHER | BOW,LIGHT,POWER |
| JOB_MAGE | STAFF,CLOTH,WISDOM |
| JOB_CLERIC | MACE,CLOTH,WISDOM |


| Job | T1 COMMON +0 weapon | Before | After |
|---|---|---|---|
| JOB_WARRIOR | EQ_T1_WARRIOR_WEAPON | MAX_HP=552,ATTACK=48,DEFENSE=27,HEAL_POWER=0,ATTACK_SPEED=880,MOVE_SPEED=3159,RANGE=1410,CRIT_CHANCE=729,CRIT_MULTIPLIER=13921,ACCURACY=8360,EVASION=459,THREAT_MULTIPLIER=8613,STATUS_POWER=9277,STATUS_RESIST=431 | MAX_HP=552,ATTACK=148,DEFENSE=27,HEAL_POWER=0,ATTACK_SPEED=880,MOVE_SPEED=3159,RANGE=1410,CRIT_CHANCE=729,CRIT_MULTIPLIER=13921,ACCURACY=8360,EVASION=459,THREAT_MULTIPLIER=8613,STATUS_POWER=9277,STATUS_RESIST=431 |
| JOB_GUARDIAN | EQ_T1_GUARDIAN_WEAPON | MAX_HP=1035,ATTACK=48,DEFENSE=64,HEAL_POWER=0,ATTACK_SPEED=1189,MOVE_SPEED=4359,RANGE=1892,CRIT_CHANCE=711,CRIT_MULTIPLIER=21028,ACCURACY=12100,EVASION=560,THREAT_MULTIPLIER=20785,STATUS_POWER=13854,STATUS_RESIST=1316 | MAX_HP=1035,ATTACK=108,DEFENSE=104,HEAL_POWER=0,ATTACK_SPEED=1189,MOVE_SPEED=4359,RANGE=1892,CRIT_CHANCE=711,CRIT_MULTIPLIER=21028,ACCURACY=12100,EVASION=560,THREAT_MULTIPLIER=20785,STATUS_POWER=13854,STATUS_RESIST=1316 |
| JOB_ARCHER | EQ_T1_ARCHER_WEAPON | MAX_HP=909,ATTACK=123,DEFENSE=38,HEAL_POWER=0,ATTACK_SPEED=2381,MOVE_SPEED=7959,RANGE=13119,CRIT_CHANCE=2561,CRIT_MULTIPLIER=34486,ACCURACY=19489,EVASION=1832,THREAT_MULTIPLIER=16462,STATUS_POWER=23350,STATUS_RESIST=633 | MAX_HP=909,ATTACK=218,DEFENSE=38,HEAL_POWER=0,ATTACK_SPEED=2381,MOVE_SPEED=7959,RANGE=13119,CRIT_CHANCE=3061,CRIT_MULTIPLIER=34486,ACCURACY=19489,EVASION=1832,THREAT_MULTIPLIER=16462,STATUS_POWER=23350,STATUS_RESIST=633 |
| JOB_MAGE | EQ_T1_MAGE_WEAPON | MAX_HP=1164,ATTACK=205,DEFENSE=53,HEAL_POWER=35,ATTACK_SPEED=2986,MOVE_SPEED=10974,RANGE=19335,CRIT_CHANCE=2744,CRIT_MULTIPLIER=46493,ACCURACY=28799,EVASION=1958,THREAT_MULTIPLIER=21225,STATUS_POWER=39277,STATUS_RESIST=1896 | MAX_HP=1164,ATTACK=295,DEFENSE=53,HEAL_POWER=35,ATTACK_SPEED=2986,MOVE_SPEED=10974,RANGE=19335,CRIT_CHANCE=2744,CRIT_MULTIPLIER=46493,ACCURACY=28799,EVASION=1958,THREAT_MULTIPLIER=21225,STATUS_POWER=39877,STATUS_RESIST=1896 |
| JOB_CLERIC | EQ_T1_CLERIC_WEAPON | MAX_HP=2323,ATTACK=153,DEFENSE=107,HEAL_POWER=249,ATTACK_SPEED=4168,MOVE_SPEED=16501,RANGE=24830,CRIT_CHANCE=3409,CRIT_MULTIPLIER=69549,ACCURACY=46401,EVASION=2376,THREAT_MULTIPLIER=40036,STATUS_POWER=56047,STATUS_RESIST=3819 | MAX_HP=2323,ATTACK=218,DEFENSE=107,HEAL_POWER=304,ATTACK_SPEED=4168,MOVE_SPEED=16501,RANGE=24830,CRIT_CHANCE=3409,CRIT_MULTIPLIER=69549,ACCURACY=46401,EVASION=2376,THREAT_MULTIPLIER=40036,STATUS_POWER=56047,STATUS_RESIST=3819 |


Quality uses exact multipliers 10000/10800/11800/13200/15000. enhancement0 factor10000, max10 factor15000. Score tie remains quality order desc,tier desc,templateId asc,instanceId asc. Wrong profile, hunt-state equip and link conflict fail with no cache mutation.

## 8. auto-equip·manual equip·unequip town-safe transaction

**상태: CONFIRMED**

LOOT phase records candidate score/protection only. At RETURN_TOWN CommitHuntSettlement order is materialize loot→merge/capacity→protection set→auto-sell candidates/remove/credit→auto-equip from retained equipment→counters/bounty→kingdom policy read/autonomy→one journal. Auto-sell cannot select protected set; auto-equip candidate IDs are removed from sale candidates before sale. Equal scores use the section7 tie. Already-equipped candidate is no-op. Slot conflict aborts the draft before any mutation. Manual EquipItem/Unequip/SetLocked remain town-safe and one atomic REWARD journal each.

## 9. protection·policy·sale·personal gold

**상태: CONFIRMED**

Only `payload.kingdom.inventoryPolicies` is read/written. PATCH-like omission means preserve the existing field; explicit null is invalid for every field. protectQualityIds/discovered IDs are unique UTF-8-sorted hard FKs. SetInventoryPolicies never permits callers to replace discoveredEquipmentTemplateIds; Domain unions new discoveries internally. auto sale and manual sale formulas from v1.0 remain: item sell_price×quantity; equipment checked formula with 2500bps. Results sort removed/protected/rejected by `(kind,id)` and credit only personalGold.

Equipped, locked, boss, protected quality, first discovery and unknown lines cannot be manually sold; one invalid requested line aborts the whole manual command. Auto sale skips them. Undo is unsupported; session history50 remains nonpersistent.

## 10. P06 combat integration·single terminal transaction·journal goldens

**상태: CONFIRMED**

Encounter persistent mutation count is zero. Pending intents accumulate by `(encounterIndex,lootTableId,entryNo,rewardOrdinal)`. RETURN_TOWN calls CommitHuntSettlement once using huntOperationId. It commits inventory/potions/equipment/auto-sale/auto-equip, total kills, huntCount once per deployed member, contribution, bounty, terminal autonomy, revision+1 and exactly one REWARD row. Result UI opens only after commit, so pre-return process loss cannot lose observed loot.

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

### COMMIT_HUNT_SETTLEMENT executable golden

Request full JSON:

```json
{
  "commandType": "COMMIT_HUNT_SETTLEMENT",
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "expectedRevision": 3,
  "regionId": "REGION_R01",
  "partyMercenaryInstanceIds": [
    "019f7cd2-8800-7002-8000-000000000001",
    "019f7cd2-8800-7002-8000-000000000002",
    "019f7cd2-8800-7002-8000-000000000003",
    "019f7cd2-8800-7002-8000-000000000004"
  ],
  "encounterCount": 1,
  "pendingLootIntentDigests": [
    "3633f3b5d1cbe753c86c5e720872af6cf3458e831f657517db5365f17551955e"
  ],
  "requestHash": "b4a35b9875880c9fd8802f760b2ad8c2c4e63ae5b4ebc51420a619bb024c8863"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"COMMIT_HUNT_SETTLEMENT","encounterCount":1,"expectedRevision":3,"operationId":"019f8320-1800-7000-8000-000000000002","partyMercenaryInstanceIds":["019f7cd2-8800-7002-8000-000000000001","019f7cd2-8800-7002-8000-000000000002","019f7cd2-8800-7002-8000-000000000003","019f7cd2-8800-7002-8000-000000000004"],"pendingLootIntentDigests":["3633f3b5d1cbe753c86c5e720872af6cf3458e831f657517db5365f17551955e"],"regionId":"REGION_R01"}
```

requestHash=`b4a35b9875880c9fd8802f760b2ad8c2c4e63ae5b4ebc51420a619bb024c8863`. Result full JSON:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "revisionBefore": 3,
  "revisionAfter": 4,
  "retainedItems": [
    {
      "itemId": "MAT_R01_SOFTWOOD",
      "quantity": 2
    }
  ],
  "retainedPotions": [],
  "retainedEquipment": [
    {
      "instanceId": "019f8373-7c00-7000-8000-000000000101",
      "equipmentTemplateId": "EQ_T1_CLERIC_WEAPON",
      "tier": 1,
      "qualityId": "QUALITY_COMMON",
      "enhancementLevel": 0,
      "refineOption": null,
      "locked": true,
      "equippedByMercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000004",
      "sourceContentVersion": "1.0.0-content.5",
      "generationOperationId": "019f8320-1800-7000-8000-000000000002"
    }
  ],
  "discardedLoot": [],
  "autoSold": [],
  "equipmentChanges": [
    {
      "mercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000004",
      "slot": "WEAPON",
      "oldEquipmentInstanceId": null,
      "newEquipmentInstanceId": "019f8373-7c00-7000-8000-000000000101"
    }
  ],
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
  "terminalState": "IDLE_TOWN",
  "rewardJournalCount": 1,
  "replayed": false,
  "resultDigest": "5228ec7b5fe1e1231552d7bd3ea6de1e9519d8e822999393ac653b687ffcc4ab"
}
```

resultDigest=`5228ec7b5fe1e1231552d7bd3ea6de1e9519d8e822999393ac653b687ffcc4ab`. Journal full row:

```json
{
  "operationId": "019f8320-1800-7000-8000-000000000002",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "b4a35b9875880c9fd8802f760b2ad8c2c4e63ae5b4ebc51420a619bb024c8863",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-22T01:02:00.000Z",
  "updatedAtUtc": "2026-07-22T01:02:00.000Z",
  "completedAtUtc": "2026-07-22T01:02:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "5228ec7b5fe1e1231552d7bd3ea6de1e9519d8e822999393ac653b687ffcc4ab",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P07_OPERATION_DUPLICATE_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


### SELL_INVENTORY executable golden

Request full JSON:

```json
{
  "commandType": "SELL_INVENTORY",
  "operationId": "019f8373-7c00-7000-8000-000000000201",
  "expectedRevision": 4,
  "mercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000001",
  "lines": [
    {
      "kind": "ITEM",
      "id": "MAT_R01_SOFTWOOD",
      "quantity": 2
    }
  ],
  "requestHash": "1ec1e420c0a49c3b87b2106fbc668572ad1fe00bdecf4f5ef4ba7e1eb66fa88c"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"SELL_INVENTORY","expectedRevision":4,"lines":[{"id":"MAT_R01_SOFTWOOD","kind":"ITEM","quantity":2}],"mercenaryInstanceId":"019f7cd2-8800-7002-8000-000000000001","operationId":"019f8373-7c00-7000-8000-000000000201"}
```

requestHash=`1ec1e420c0a49c3b87b2106fbc668572ad1fe00bdecf4f5ef4ba7e1eb66fa88c`. Result full JSON:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000201",
  "revisionBefore": 4,
  "revisionAfter": 5,
  "removed": [
    {
      "kind": "ITEM",
      "id": "MAT_R01_SOFTWOOD",
      "quantity": 2
    }
  ],
  "protected": [],
  "rejected": [],
  "personalGoldDelta": 12,
  "replayed": false,
  "resultDigest": "c42dce018fc83c230409244b52ca88d56c1aa56505fa4bac43bc760facd466d7"
}
```

resultDigest=`c42dce018fc83c230409244b52ca88d56c1aa56505fa4bac43bc760facd466d7`. Journal full row:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000201",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "1ec1e420c0a49c3b87b2106fbc668572ad1fe00bdecf4f5ef4ba7e1eb66fa88c",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-22T01:05:00.000Z",
  "updatedAtUtc": "2026-07-22T01:05:00.000Z",
  "completedAtUtc": "2026-07-22T01:05:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "c42dce018fc83c230409244b52ca88d56c1aa56505fa4bac43bc760facd466d7",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P07_OPERATION_DUPLICATE_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


### SET_INVENTORY_POLICIES executable golden

Request full JSON:

```json
{
  "commandType": "SET_INVENTORY_POLICIES",
  "operationId": "019f8373-7c00-7000-8000-000000000202",
  "expectedRevision": 5,
  "policy": {
    "autoSellMaxTier": 1,
    "protectQualityIds": [
      "QUALITY_RARE",
      "QUALITY_LEGACY",
      "QUALITY_RELIC"
    ],
    "autoEquipEnabled": true,
    "upgradeThresholdBps": 500,
    "autoSellEnabled": true,
    "protectBossEquipment": true,
    "protectFirstDiscovery": true
  },
  "requestHash": "4faedc060fd83e90cc6f267320969a8b75310d22832ebedafdf717d33b3e334c"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"SET_INVENTORY_POLICIES","expectedRevision":5,"operationId":"019f8373-7c00-7000-8000-000000000202","policy":{"autoEquipEnabled":true,"autoSellEnabled":true,"autoSellMaxTier":1,"protectBossEquipment":true,"protectFirstDiscovery":true,"protectQualityIds":["QUALITY_RARE","QUALITY_LEGACY","QUALITY_RELIC"],"upgradeThresholdBps":500}}
```

requestHash=`4faedc060fd83e90cc6f267320969a8b75310d22832ebedafdf717d33b3e334c`. Result full JSON:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000202",
  "revisionBefore": 5,
  "revisionAfter": 6,
  "policy": {
    "autoSellMaxTier": 1,
    "protectQualityIds": [
      "QUALITY_RARE",
      "QUALITY_LEGACY",
      "QUALITY_RELIC"
    ],
    "autoEquipEnabled": true,
    "upgradeThresholdBps": 500,
    "autoSellEnabled": true,
    "protectBossEquipment": true,
    "protectFirstDiscovery": true,
    "discoveredEquipmentTemplateIds": [
      "EQ_T1_CLERIC_WEAPON"
    ]
  },
  "replayed": false,
  "resultDigest": "8aba831b55af29a3a285aa5cc78c999e7264578561a5fb639a323aaba85e9897"
}
```

resultDigest=`8aba831b55af29a3a285aa5cc78c999e7264578561a5fb639a323aaba85e9897`. Journal full row:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000202",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "4faedc060fd83e90cc6f267320969a8b75310d22832ebedafdf717d33b3e334c",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-22T01:06:00.000Z",
  "updatedAtUtc": "2026-07-22T01:06:00.000Z",
  "completedAtUtc": "2026-07-22T01:06:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "8aba831b55af29a3a285aa5cc78c999e7264578561a5fb639a323aaba85e9897",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P07_OPERATION_DUPLICATE_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


### TRANSFER_POTION_TO_MERCENARY executable golden

Request full JSON:

```json
{
  "commandType": "TRANSFER_POTION_TO_MERCENARY",
  "operationId": "019f8373-7c00-7000-8000-000000000203",
  "expectedRevision": 6,
  "mercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000001",
  "potionId": "POT_HEAL_SMALL",
  "quantity": 2,
  "requestHash": "8aeedfb54c270940d8e85d3659d38f02c0024a91ec5e20660c234f048cfbbf6e"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"TRANSFER_POTION_TO_MERCENARY","expectedRevision":6,"mercenaryInstanceId":"019f7cd2-8800-7002-8000-000000000001","operationId":"019f8373-7c00-7000-8000-000000000203","potionId":"POT_HEAL_SMALL","quantity":2}
```

requestHash=`8aeedfb54c270940d8e85d3659d38f02c0024a91ec5e20660c234f048cfbbf6e`. Result full JSON:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000203",
  "revisionBefore": 6,
  "revisionAfter": 7,
  "warehouseQuantityBefore": 3,
  "warehouseQuantityAfter": 1,
  "mercenaryQuantityBefore": 2,
  "mercenaryQuantityAfter": 4,
  "replayed": false,
  "resultDigest": "55ceeeb72d889cc304f1b20baf3e55412fa09b8eea078c71bf49f6348c173556"
}
```

resultDigest=`55ceeeb72d889cc304f1b20baf3e55412fa09b8eea078c71bf49f6348c173556`. Journal full row:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000203",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "8aeedfb54c270940d8e85d3659d38f02c0024a91ec5e20660c234f048cfbbf6e",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-22T01:07:00.000Z",
  "updatedAtUtc": "2026-07-22T01:07:00.000Z",
  "completedAtUtc": "2026-07-22T01:07:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "55ceeeb72d889cc304f1b20baf3e55412fa09b8eea078c71bf49f6348c173556",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P07_OPERATION_DUPLICATE_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


### RETURN_POTION_TO_WAREHOUSE executable golden

Request full JSON:

```json
{
  "commandType": "RETURN_POTION_TO_WAREHOUSE",
  "operationId": "019f8373-7c00-7000-8000-000000000204",
  "expectedRevision": 7,
  "mercenaryInstanceId": "019f7cd2-8800-7002-8000-000000000001",
  "potionId": "POT_HEAL_SMALL",
  "quantity": 1,
  "requestHash": "55dbef72fbdf93fb9ed02360b6654733c297b094df2f94d52f123497f28da336"
}
```

RFC 8785 request input UTF-8:

```text
{"commandType":"RETURN_POTION_TO_WAREHOUSE","expectedRevision":7,"mercenaryInstanceId":"019f7cd2-8800-7002-8000-000000000001","operationId":"019f8373-7c00-7000-8000-000000000204","potionId":"POT_HEAL_SMALL","quantity":1}
```

requestHash=`55dbef72fbdf93fb9ed02360b6654733c297b094df2f94d52f123497f28da336`. Result full JSON:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000204",
  "revisionBefore": 7,
  "revisionAfter": 8,
  "warehouseQuantityBefore": 1,
  "warehouseQuantityAfter": 2,
  "mercenaryQuantityBefore": 4,
  "mercenaryQuantityAfter": 3,
  "replayed": false,
  "resultDigest": "6d787d17aff54cf54a8857ff86f1b9eb6ca092438262d1c9440e968cbf75e66e"
}
```

resultDigest=`6d787d17aff54cf54a8857ff86f1b9eb6ca092438262d1c9440e968cbf75e66e`. Journal full row:

```json
{
  "operationId": "019f8373-7c00-7000-8000-000000000204",
  "operationType": "REWARD",
  "facilityJobType": null,
  "requestHash": "55dbef72fbdf93fb9ed02360b6654733c297b094df2f94d52f123497f28da336",
  "status": "COMMITTED",
  "createdAtUtc": "2026-07-22T01:08:00.000Z",
  "updatedAtUtc": "2026-07-22T01:08:00.000Z",
  "completedAtUtc": "2026-07-22T01:08:00.000Z",
  "serverReceiptId": null,
  "errorCode": null,
  "resultDigest": "6d787d17aff54cf54a8857ff86f1b9eb6ca092438262d1c9440e968cbf75e66e",
  "failureResolution": null,
  "resolvedAtUtc": null
}
```

동일 operationId/requestHash 재시도는 revision을 올리지 않고 위 result를 `replayed=true` projection으로 반환한다. 동일 operationId와 다른 requestHash는 `P07_OPERATION_DUPLICATE_MISMATCH`, mutation 0이다. atomic replace 전 crash는 이전 revision, replace 후 crash는 위 COMMITTED row가 있는 새 revision을 복구한다.


Each potion transfer result uses a fixture warehouse quantity declared in request fixture setup; source/destination quantities cross-check the P07 migrated mercenary IDs. All command journals use the existing saveVersion1 REWARD enum and facilityJobType=null.

## 11. P07 content package·localization

**상태: MIGRATION / CONFIRMED**

| file | rowCount | SHA-256 | materialization |
|---|---|---|---|
| asset_register.csv | 60 | e1ff92b40b7c576ec0f7e8fb945fa618dd8689d333d3b43d9fa8ed9d54dcefac | FINAL CSV IN THIS DOCUMENT |
| autonomy_rules.csv | 37 | 9187e57b2c47af733bcf1db0f7528c0906def85d7fd369d879ade3e28cb3ff17 | BYTE_COPY_FROM_1.0.0-content.4 |
| combat_ai_profiles.csv | 5 | bc49e513fbef323ab7b2f3c74a7a10d89f65e67206c163097a09df9455abe599 | BYTE_COPY_FROM_1.0.0-content.4 |
| combat_job_profiles.csv | 5 | 10edf82c53e3cf7d1d2cfc533bf8fabfb1d222c28da75136f9076b54da82bbf3 | BYTE_COPY_FROM_1.0.0-content.4 |
| condition_group_members.csv | 38 | d07c0ab53279eaf11806cd8e90b3a6415ef704f6e197628205dc77e3df74f2ba | BYTE_COPY_FROM_1.0.0-content.4 |
| condition_groups.csv | 21 | e3b888ec7dc13f63b388a5bcbebcfe0f4f953d97136843f19d5138526ab88dff | BYTE_COPY_FROM_1.0.0-content.4 |
| conditions.csv | 34 | a94dac2c8f5dd2c0da3427d5c1b0e978a48532cda8e7729ead442e92cc6a0fbf | BYTE_COPY_FROM_1.0.0-content.4 |
| content_aliases.csv | 0 | a398fb76d2c1b0698e90bb0e5369d330b013a75f3d568c96a8c4a81e9b24986a | BYTE_COPY_FROM_1.0.0-content.4 |
| currencies.csv | 4 | a2214c4c527aa654dab12449f3e528e9e1b44ea1048b8f9851b13d8cdd53120b | BYTE_COPY_FROM_1.0.0-content.4 |
| enhancement_rules.csv | 10 | 2c9edde3c79aa4588e749f6718f812638b45b7b6f674eddcb937011bc72866ac | BYTE_COPY_FROM_1.0.0-content.4 |
| equipment_job_eligibility.csv | 110 | 7c915d0a9c956cbcc2c2390913af9720e9c825387357cbeacfdcf82e135525d8 | BYTE_COPY_FROM_1.0.0-content.4 |
| equipment_qualities.csv | 5 | 150f6181c6130397d21ac56722b61656caec92ff01a95100336a7fe6795360f1 | BYTE_COPY_FROM_1.0.0-content.4 |
| equipment_quality_weights.csv | 20 | c2bfbc11d56bc2318f56cb0c5d6cf876fa1c080778e599525dfa3bb699d0db5c | BYTE_COPY_FROM_1.0.0-content.4 |
| equipment_score_weights.csv | 5 | 4fa0159ccb883bdeebda84cfa96b7773b42a9bb3577a7a40cd3aafa9fd97193e | FINAL CSV IN THIS DOCUMENT |
| equipment_templates.csv | 80 | 5a844437f39c496406db16f6604f6e16de8dec31c5c6230eae40848ec4372227 | BYTE_COPY_FROM_1.0.0-content.4 |
| facilities.csv | 8 | 5290ae6f1db2b1a9341b6b017d1cf18114116d44ac2b6645a3cf6b9dc2a731cc | BYTE_COPY_FROM_1.0.0-content.4 |
| facility_construction_rules.csv | 32 | a0fefa7b1a65a8c4984d66e2fb4b85641dc81ccdff83b5c92d7d2810515eaad5 | BYTE_COPY_FROM_1.0.0-content.4 |
| facility_levels.csv | 32 | 9f1dc6409d5ebdf6426cb29536f86bc23dc15c2776bee83ed4cef347c7e8b9c0 | BYTE_COPY_FROM_1.0.0-content.4 |
| facility_upgrade_materials.csv | 40 | 510dcd986ef754906b9c77da8ece2b0c35a51ef7d088c7492dd8c6dee5a4f9f4 | BYTE_COPY_FROM_1.0.0-content.4 |
| facility_world_assets.csv | 13 | d99f56f91ed12add01cd7097bdbd397afc7da491b611602bba43ef94e905db1f | BYTE_COPY_FROM_1.0.0-content.4 |
| inventory_capacity_rules.csv | 4 | adf604de5c8f9dc0b38ed1a6c11630175b72258c229b434fb05cc9514c5cb5a0 | FINAL CSV IN THIS DOCUMENT |
| items.csv | 53 | a18b362f3c118304e8a9363b7758015056d1572b496b563b697b2db67061aa2f | BYTE_COPY_FROM_1.0.0-content.4 |
| job_skill_unlocks.csv | 15 | 028a3ff48a9d3f13ab6204c5a2404130dc9a6621a5bd22303519f30715a676a7 | BYTE_COPY_FROM_1.0.0-content.4 |
| jobs.csv | 5 | 72ad4296c33ddf1d6fda43dcd51f9fbdcccdbdc7f4e57f931cbcbc829d5275f8 | BYTE_COPY_FROM_1.0.0-content.4 |
| kingdom_stages.csv | 5 | 87eaf7cd0dec0ec615aeabb3d7b8c7ef815a4948989bc1b7c62188a5ace5cdfe | BYTE_COPY_FROM_1.0.0-content.4 |
| localizations.csv | 942 | a0a784d762d2fda6710480050843f6cbfa539c27cf67801f7b3296db8919db10 | FINAL CSV IN THIS DOCUMENT |
| loot_entries.csv | 88 | 6437c651ce648d298f3ee59a8074ded1b4bb3a6023cd611832d759da409451fb | BYTE_COPY_FROM_1.0.0-content.4 |
| loot_tables.csv | 27 | 2359b43dfd323fbf263062fee98910bee9d2f3fcbfe29b7ae3ffcd922c49a9cd | BYTE_COPY_FROM_1.0.0-content.4 |
| mercenary_appearance_pool_entries.csv | 5 | 74a27c611876c1e969563cc3b4fbb4a4537aa67a0982ba63afa6e8691992ebb7 | BYTE_COPY_FROM_1.0.0-content.4 |
| mercenary_generation_profiles.csv | 1 | 7702394c9a2c24a3e8bdabd83573f485966e3f0ebbb45912e3f7866b51f55533 | BYTE_COPY_FROM_1.0.0-content.4 |
| mercenary_grades.csv | 5 | f4aaff18498325c99836d945be898632e1f7903282e82cff8e21fadb682f6079 | BYTE_COPY_FROM_1.0.0-content.4 |
| mercenary_name_pool_entries.csv | 20 | b3077cf682d4e5aeb0c0ed9d8ce1326640d4a1a0e2c2deb4088ca4f4f18724d9 | BYTE_COPY_FROM_1.0.0-content.4 |
| mercenary_ranks.csv | 6 | 3fbae56a6acab8fa6c2cc44a7e0cafac5b080704470956140e81aea0aeebbd4d | BYTE_COPY_FROM_1.0.0-content.4 |
| monsters.csv | 27 | 92ae3f14fa06328bdedf3682c332aa9e390fe6f8816349d7f9c3aa3012fa0cb8 | BYTE_COPY_FROM_1.0.0-content.4 |
| npc_professions.csv | 4 | bef0f486b935b740e32d5660915226c9e08515bcb8716d5b187c131f4c870722 | BYTE_COPY_FROM_1.0.0-content.4 |
| npc_proficiency_levels.csv | 4 | 125ac69e78cd8ea50d04142e49b3aa178ef2a486d72e191bd0a1bae469a4757a | BYTE_COPY_FROM_1.0.0-content.4 |
| offline_reward_rules.csv | 6 | 36fc0e26c34f03d61c081c1fc1f981503e98997166a26705df157993cb6a54e6 | BYTE_COPY_FROM_1.0.0-content.4 |
| personalities.csv | 6 | 10114e5c65713cdcf7783c1b8d93f61a0f75f244586f16adcfd0d9c02bf41d4e | BYTE_COPY_FROM_1.0.0-content.4 |
| potions.csv | 7 | 22e9dd956e3149e2976dfc6de11635785d9046df5bf3ddd10a50b975b231377b | BYTE_COPY_FROM_1.0.0-content.4 |
| progression_flags.csv | 3 | 06bfcd7f830d060d34a5a984392b7bf78fd9d4ea2093670ca00572b5c842fcc0 | BYTE_COPY_FROM_1.0.0-content.4 |
| promotion_grade_requirements.csv | 6 | 4669a10f62cc2b2ab43509a3f8f68bf6757e774281e39b36b2ce390a03a84e9c | BYTE_COPY_FROM_1.0.0-content.4 |
| raid_difficulties.csv | 6 | cc6fc17186facc92c8a047ae2929bfee7fe0b1459bdb7fe9611d08cd6489506e | BYTE_COPY_FROM_1.0.0-content.4 |
| raid_part_effects.csv | 6 | 05e69d150777edbf6abf0c09d034ba2b251d5f7e1c6537120e452b31a8ef8ee4 | BYTE_COPY_FROM_1.0.0-content.4 |
| raid_parts.csv | 7 | 009baa859fc58ab25650c89ffbc4a9ba36f95e97bfb30ad793679ea13e382942 | BYTE_COPY_FROM_1.0.0-content.4 |
| raids.csv | 2 | 82b1fc2fa049daeb6942ff536ec52507b2efea02389dd38ceb20ca008adb180a | BYTE_COPY_FROM_1.0.0-content.4 |
| random_equipment_tier_specs.csv | 5 | 58307716e9f2f1a217fc46075e4578fc36e5e9586fd62525badee6c6819a1c86 | BYTE_COPY_FROM_1.0.0-content.4 |
| recipe_materials.csv | 183 | 1a27d1d79a6040698eb2950f0f199a22e03db74d189c830f8f0a8cf13c18f9a3 | BYTE_COPY_FROM_1.0.0-content.4 |
| recipe_outputs.csv | 87 | f07d94b866f4be4bcc035261c5a912f583ad544989b7ffe2078f212fea0b4a6b | BYTE_COPY_FROM_1.0.0-content.4 |
| recipes.csv | 87 | 9a99e41c90612f5ae32d5218e40b39ed4a037fb9c3c209ec7521b557c8a8481c | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_pity_groups.csv | 1 | ad5410752817ce425b02dc6aee4a167b78d5d6db51f32422a01c0615c917ed73 | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_pity_rules.csv | 2 | 40662eb78418ef2d32cc6bbea3b3d7d6e6416018fd3b7fa464b38e044957f849 | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_pool_entries.csv | 17 | 37a9fc6166b861bffe1e7077a51a0da6a5698176c1d05538c26b3751ad19f1c3 | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_pools.csv | 6 | 35545b565d7092ff0337d9968fc3b4697b66e98a6d66d93c614c2879ab0368fc | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_rate_up_entries.csv | 0 | bb922b63227eeaaa922b3e98a6ca457c65588890ba745b0f0219c7294a96613d | BYTE_COPY_FROM_1.0.0-content.4 |
| recruitment_rate_up_groups.csv | 0 | 13b199e7d6348e05a15413af28c90c063db68aea0489c88c5af9fa8880e08762 | BYTE_COPY_FROM_1.0.0-content.4 |
| refine_options.csv | 11 | b86ee167473b89b98078429b4fe832650526852b830fc218b9ac377c80ae9866 | BYTE_COPY_FROM_1.0.0-content.4 |
| region_encounter_profiles.csv | 5 | 0d0d1cbfe2386d3d8cb3116e6eead76987756e17053d6bfddee441d35ceb5a4f | BYTE_COPY_FROM_1.0.0-content.4 |
| regions.csv | 5 | 80d6c173d7e2379e8b5f914d2d1b6f4551d07dbd03e032e4d15010028824d336 | BYTE_COPY_FROM_1.0.0-content.4 |
| reward_entries.csv | 16 | 8eadd36ca99cb817a95b2f526cbf6c4601253d769c21a086d818829e564d3863 | BYTE_COPY_FROM_1.0.0-content.4 |
| reward_groups.csv | 15 | d26f5793228068d6be1bcaae3944761cc1348a03d16b40e8f502820c01d9d7da | BYTE_COPY_FROM_1.0.0-content.4 |
| runtime_config.csv | 38 | 4c976d9f325699540eb2cd94462f07cca6a31e615206f67d69c723a20f20baa5 | FINAL CSV IN THIS DOCUMENT |
| skill_runtime_rules.csv | 15 | 7da4a13b14c6b47d79a59459520c7a9b95cfdb6388441b97995879ac52329a9e | BYTE_COPY_FROM_1.0.0-content.4 |
| skills.csv | 15 | 4bae7415a237c7ed40a2d8453139eafc8c651f3cd302236173aba70b1b084ef7 | BYTE_COPY_FROM_1.0.0-content.4 |
| status_effects.csv | 6 | 43514690856fce638242b754f6fe1dc2cd98318dc11c5ba71d157f1da4e0b118 | BYTE_COPY_FROM_1.0.0-content.4 |
| trait_job_eligibility.csv | 44 | f2964966cbd9258b85a9ed541ba22ae0d7d51e0146206a4c85575e6b6d5e59e4 | BYTE_COPY_FROM_1.0.0-content.4 |
| traits.csv | 10 | bf211a89b515e166c5bddab3294688ae9fca28ba33877ab7a8d3a8a219af9f86 | BYTE_COPY_FROM_1.0.0-content.4 |
| tutorial_grants.csv | 9 | 5782d9c3da2fdb7d8693c7bdf7f871d74a47d5d53357b6191000909826c4a450 | BYTE_COPY_FROM_1.0.0-content.4 |
| tutorial_steps.csv | 10 | 67ad17e3832c69d303e02db1803e36e401f46b2dedff4f34420cc49e531d3e8b | BYTE_COPY_FROM_1.0.0-content.4 |

```json
{
  "schemaId": "urn:tycoon:content-manifest:v2",
  "contractVersion": 2,
  "contentVersion": "1.0.0-content.5",
  "csvSchemaSetVersion": 3,
  "packageKind": "BASE",
  "baseContentVersion": null,
  "minimumGameVersion": "1.0.0-p07",
  "channel": "DEV",
  "generatedAtUtc": "2026-07-22T00:00:00.000Z",
  "tables": [
    {
      "file": "asset_register.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "e1ff92b40b7c576ec0f7e8fb945fa618dd8689d333d3b43d9fa8ed9d54dcefac",
      "rowCount": 60,
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
      "file": "equipment_score_weights.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "4fa0159ccb883bdeebda84cfa96b7773b42a9bb3577a7a40cd3aafa9fd97193e",
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
          "name": "primary_weight_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "secondary_weight_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "survival_weight_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "refine_weight_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "boss_weight_bps",
          "domain": "SAFE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "price_penalty_bps",
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
      "file": "inventory_capacity_rules.csv",
      "schemaVersion": 1,
      "required": true,
      "sha256": "adf604de5c8f9dc0b38ed1a6c11630175b72258c229b434fb05cc9514c5cb5a0",
      "rowCount": 4,
      "primaryKey": [
        "warehouse_level"
      ],
      "fields": [
        {
          "name": "warehouse_level",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "item_stack_slots",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "equipment_slots",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "potion_stack_slots",
          "domain": "POSITIVE_INT",
          "nullable": false,
          "enumValues": []
        },
        {
          "name": "hunt_buffer_slots",
          "domain": "POSITIVE_INT",
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
      "sha256": "a0a784d762d2fda6710480050843f6cbfa539c27cf67801f7b3296db8919db10",
      "rowCount": 942,
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
      "sha256": "4c976d9f325699540eb2cd94462f07cca6a31e615206f67d69c723a20f20baa5",
      "rowCount": 38,
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
| 1.0.0-content.5 | 74609 | 69f79179b2dac1f57d2e59bc079426434d0ae975d24cd2a86efebdd7963ee866 |

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
ASSET_P07_ITEM_ALCHEMY,P07 Item ALCHEMY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ALCHEMY; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_BOSS,P07 Item BOSS,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/BOSS; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_CRAFT,P07 Item CRAFT,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/CRAFT; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_ENHANCE,P07 Item ENHANCE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ENHANCE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_MAGIC,P07 Item MAGIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/MAGIC; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_MONSTER,P07 Item MONSTER,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/MONSTER; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_ORE,P07 Item ORE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ORE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_PROMOTION,P07 Item PROMOTION,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/PROMOTION; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_REFINE,P07 Item REFINE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/REFINE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_RELIC,P07 Item RELIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/RELIC; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_COMMON,P07 Quality QUALITY_COMMON,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_COMMON; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_FINE,P07 Quality QUALITY_FINE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_FINE; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_LEGACY,P07 Quality QUALITY_LEGACY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_LEGACY; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_RARE,P07 Quality QUALITY_RARE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_RARE; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_RELIC,P07 Quality QUALITY_RELIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_RELIC; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_ACCESSORY,P07 Slot ACCESSORY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/ACCESSORY; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_ARMOR,P07 Slot ARMOR,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/ARMOR; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_HELMET,P07 Slot HELMET,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/HELMET; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_WEAPON,P07 Slot WEAPON,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/WEAPON; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_EMPTY,P07 State EMPTY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/EMPTY; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_ERROR,P07 State ERROR,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/ERROR; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_LOCKED,P07 State LOCKED,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/LOCKED; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_1,P07 Tier 1,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/1; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_2,P07 Tier 2,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/2; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_3,P07 Tier 3,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/3; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_4,P07 Tier 4,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/4; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_5,P07 Tier 5,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/5; logical asset,CONFIRMED,TRUE
```

#### `equipment_score_weights.csv` final bytes

```csv
job_id,primary_weight_bps,secondary_weight_bps,survival_weight_bps,refine_weight_bps,boss_weight_bps,price_penalty_bps,status,enabled
JOB_WARRIOR,10000,4500,5000,6000,3000,1000,TUNABLE,TRUE
JOB_GUARDIAN,7000,4000,11000,7000,2500,1000,TUNABLE,TRUE
JOB_ARCHER,11000,6000,3000,7000,3500,1000,TUNABLE,TRUE
JOB_MAGE,11500,6500,2500,8000,3500,1000,TUNABLE,TRUE
JOB_CLERIC,10000,7000,6500,8000,2000,1000,TUNABLE,TRUE
```

#### `inventory_capacity_rules.csv` final bytes

```csv
warehouse_level,item_stack_slots,equipment_slots,potion_stack_slots,hunt_buffer_slots,status,enabled
1,40,40,10,12,TUNABLE,TRUE
2,60,60,14,16,TUNABLE,TRUE
3,90,90,18,20,TUNABLE,TRUE
4,120,120,24,24,TUNABLE,TRUE
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
en-US,TXT_P07_AUTO_EQUIP,Auto-equip,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_AUTO_SELL,Auto-sell,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_CAPACITY,Storage {used}/{capacity},P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_CAPACITY_ERROR,Not enough storage space.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_COMBAT_GUARD,Equipment can be changed only in town.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_EMPTY,No items are owned.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_EQUIP,Equip,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_EQUIPPED_ERROR,Equipped equipment cannot be sold.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_ERROR,Inventory cannot be displayed.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_FILTER,Filter,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_HUNT_BUFFER_FULL,Returning because the hunt loot buffer is full.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_INVENTORY_TITLE,Inventory,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_LOADING,Loading inventory…,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_LOCK,Protect,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_LOCKED,Inventory is still locked.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_LOOT_RESULT,Loot {items} · Auto-sale +{gold},P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_OFFLINE,Offline: local inventory management remains available.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_OVERFLOW_BODY,Loot beyond storage capacity was discarded in deterministic order.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_OVERFLOW_TITLE,Loot Not Stored,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_POLICY_INVALID,Inventory policy values are invalid.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_POTION_CAPACITY,Mercenary potion slots are full.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_POTION_RETURN,Return to Warehouse,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_POTION_TRANSFER,Assign to Mercenary,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_PROTECTED_ERROR,Protected equipment cannot be sold.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_PROTECT_QUALITY,Quality protection,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_SCORE,Equipment score {score},P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_SEARCH,Search by name,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_SELL,Sell,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_SELL_CONFIRM,Sell {name} for {gold} personal gold.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_SORT,Sort,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_STAT_DELTA,Change {delta},P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_TAB_EQUIPMENT,Equipment,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_TAB_ITEMS,Materials,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_TAB_POTIONS,Potions,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_TRANSFER_INVALID,The potion transfer could not be completed.,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_UNEQUIP,Unequip,P07_INVENTORY,CONFIRMED,TRUE
en-US,TXT_P07_UNLOCK,Unprotect,P07_INVENTORY,CONFIRMED,TRUE
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
ko-KR,TXT_P07_AUTO_EQUIP,자동 장착,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_AUTO_SELL,자동 판매,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_CAPACITY,창고 {used}/{capacity},P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_CAPACITY_ERROR,창고 공간이 부족합니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_COMBAT_GUARD,마을에서만 장비를 변경할 수 있습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_EMPTY,보유한 항목이 없습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_EQUIP,장착,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_EQUIPPED_ERROR,장착 중인 장비는 판매할 수 없습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_ERROR,인벤토리를 표시할 수 없습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_FILTER,필터,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_HUNT_BUFFER_FULL,전리품 운반 한도에 도달해 귀환합니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_INVENTORY_TITLE,인벤토리,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_LOADING,인벤토리를 불러오는 중…,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_LOCK,보호,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_LOCKED,인벤토리가 아직 잠겨 있습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_LOOT_RESULT,획득 {items} · 자동 판매 +{gold},P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_OFFLINE,오프라인: 로컬 인벤토리 관리는 사용할 수 있습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_OVERFLOW_BODY,창고 한도를 넘은 전리품은 정해진 순서로 폐기되었습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_OVERFLOW_TITLE,보관하지 못한 전리품,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_POLICY_INVALID,인벤토리 정책 값이 올바르지 않습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_POTION_CAPACITY,용병 포션 슬롯이 가득 찼습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_POTION_RETURN,창고로 회수,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_POTION_TRANSFER,용병에게 배정,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_PROTECTED_ERROR,보호된 장비는 판매할 수 없습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_PROTECT_QUALITY,품질 보호,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_SCORE,장비 점수 {score},P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_SEARCH,이름 검색,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_SELL,판매,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_SELL_CONFIRM,{name}을(를) {gold} 개인 골드에 판매합니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_SORT,정렬,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_STAT_DELTA,변화 {delta},P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_TAB_EQUIPMENT,장비,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_TAB_ITEMS,재료,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_TAB_POTIONS,포션,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_TRANSFER_INVALID,포션 이동 요청을 완료할 수 없습니다.,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_UNEQUIP,해제,P07_INVENTORY,CONFIRMED,TRUE
ko-KR,TXT_P07_UNLOCK,보호 해제,P07_INVENTORY,CONFIRMED,TRUE
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
P07_AUTO_EQUIP_ENABLED,BOOLEAN,TRUE,BOOL,,,TXT_P07_AUTO_EQUIP,TUNABLE,TRUE
P07_AUTO_SELL_ENABLED,BOOLEAN,TRUE,BOOL,,,TXT_P07_AUTO_SELL,TUNABLE,TRUE
P07_AUTO_SELL_MAX_TIER,INTEGER,1,TIER,0,5,TXT_P07_AUTO_SELL,TUNABLE,TRUE
P07_EQUIPMENT_SELL_RATE_BPS,INTEGER,2500,BPS,0,10000,TXT_P07_SELL,TUNABLE,TRUE
P07_MAX_LIVE_ITEM_VIEWS,INTEGER,30,COUNT,8,40,TXT_P07_INVENTORY_TITLE,CONFIRMED,TRUE
P07_MAX_PAGE_SIZE,INTEGER,100,COUNT,1,100,TXT_P07_INVENTORY_TITLE,CONFIRMED,TRUE
RAID_PARTY_MAX,INTEGER,8,COUNT,1,16,TXT_RAID_PARTY_MAX_DESCRIPTION,TUNABLE,TRUE
RAID_PARTY_MIN,INTEGER,6,COUNT,1,8,TXT_RAID_PARTY_MIN_DESCRIPTION,TUNABLE,TRUE
RARE_EQUIPMENT_AUTO_PROTECT,BOOLEAN,TRUE,BOOL,,,TXT_RARE_EQUIPMENT_AUTO_PROTECT_DESCRIPTION,TUNABLE,TRUE
ROSTER_CAP_V1,INTEGER,24,COUNT,1,24,TXT_ROSTER_CAP_V1_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_HIGH,DECIMAL,1.15,RATIO,0.5,2,TXT_STORE_PRICE_HIGH_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_LOW,DECIMAL,0.9,RATIO,0.5,2,TXT_STORE_PRICE_LOW_DESCRIPTION,TUNABLE,TRUE
STORE_PRICE_STANDARD,DECIMAL,1.0,RATIO,0.5,2,TXT_STORE_PRICE_STANDARD_DESCRIPTION,TUNABLE,TRUE
```

## 12. P06→P07 Save migration·full schema·full golden

**상태: MIGRATION / CONFIRMED**

Minimal pre-parser reads only schemaId/saveVersion/contentVersion with UTF-8 JSON duplicate-key rejection and size<=16MiB. It chooses full schema by the following registry, then validates the entire document once. No allOf field extension is used.

```json
{
  "registryVersion": 1,
  "preparseRequired": [
    "schemaId",
    "saveVersion",
    "contentVersion"
  ],
  "entries": [
    {
      "contentVersions": [
        "1.0.0-content.1",
        "1.0.0-content.2",
        "1.0.0-content.3",
        "1.0.0-content.4"
      ],
      "schemaFile": "save.schema.json",
      "sha256": "d0f20a54d5096f6dd4cf1b4b859177fa730477634e10e16a237ea8a995d614d5"
    },
    {
      "contentVersions": [
        "1.0.0-content.5"
      ],
      "schemaFile": "save.content.5.schema.json",
      "sha256": "d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392"
    }
  ]
}
```

P07 `.5` full schema bytes are UTF-8, two-space JSON plus LF: 53081 bytes, SHA-256 `d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392`. Every `$ref` resolves. The complete implementation file follows.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:tycoon:schema:save:v1:content.5",
  "title": "Kingdom Tycoon Save v1",
  "type": "object",
  "additionalProperties": false,
  "required": [
    "schemaId",
    "saveVersion",
    "gameVersion",
    "contentVersion",
    "saveId",
    "profileId",
    "revision",
    "createdAtUtc",
    "savedAtUtc",
    "integrity",
    "payload"
  ],
  "properties": {
    "schemaId": {
      "const": "urn:tycoon:save:v1"
    },
    "saveVersion": {
      "const": 1
    },
    "gameVersion": {
      "type": "string",
      "format": "semver"
    },
    "contentVersion": {
      "type": "string",
      "minLength": 1,
      "maxLength": 64
    },
    "saveId": {
      "type": "string",
      "format": "uuid-v7"
    },
    "profileId": {
      "type": "string",
      "format": "uuid-v7"
    },
    "revision": {
      "type": "integer",
      "minimum": 0,
      "maximum": 9007199254740991
    },
    "createdAtUtc": {
      "type": "string",
      "format": "utc-instant"
    },
    "savedAtUtc": {
      "type": "string",
      "format": "utc-instant"
    },
    "integrity": {
      "$ref": "#/$defs/Integrity"
    },
    "payload": {
      "$ref": "#/$defs/Payload"
    }
  },
  "$defs": {
    "RefineOption": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "optionId",
        "value"
      ],
      "properties": {
        "optionId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "value": {
          "type": "number",
          "minimum": -9007199254740991,
          "maximum": 9007199254740991
        }
      }
    },
    "GeneratedEquipmentSnapshot": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "instanceId",
        "equipmentTemplateId",
        "tier",
        "qualityId",
        "enhancementLevel",
        "refineOption",
        "sourceContentVersion",
        "generationOperationId",
        "randomTraceHash"
      ],
      "properties": {
        "instanceId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "equipmentTemplateId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "tier": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5
        },
        "qualityId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "enhancementLevel": {
          "const": 0
        },
        "refineOption": {
          "anyOf": [
            {
              "$ref": "#/$defs/RefineOption"
            },
            {
              "type": "null"
            }
          ]
        },
        "sourceContentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "generationOperationId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "randomTraceHash": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        }
      }
    },
    "RewardSnapshot": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "rewardType",
        "rewardId",
        "quantity",
        "destination",
        "targetId",
        "generatedEquipmentSnapshot"
      ],
      "properties": {
        "rewardType": {
          "type": "string",
          "enum": [
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
        "rewardId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "destination": {
          "type": "string",
          "enum": [
            "INVENTORY",
            "FACILITY_STORAGE",
            "MERCENARY",
            "PROFILE",
            "KINGDOM",
            "SERVER_WALLET",
            "REGION"
          ]
        },
        "targetId": {
          "anyOf": [
            {
              "type": "string",
              "minLength": 1,
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "generatedEquipmentSnapshot": {
          "anyOf": [
            {
              "$ref": "#/$defs/GeneratedEquipmentSnapshot"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "AssetAmount": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "assetType",
        "assetId",
        "quantity",
        "targetMercenaryInstanceId"
      ],
      "properties": {
        "assetType": {
          "type": "string",
          "enum": [
            "ITEM",
            "POTION",
            "PERSONAL_GOLD",
            "KINGDOM_GOLD"
          ]
        },
        "assetId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "targetMercenaryInstanceId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "CostItem": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "itemId",
        "quantity"
      ],
      "properties": {
        "itemId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        }
      }
    },
    "PromotionCostSnapshot": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "contentVersion",
        "fromRankId",
        "toRankId",
        "personalGold",
        "kingdomGold",
        "contributionRequired",
        "reviewSeconds",
        "items"
      ],
      "properties": {
        "contentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "fromRankId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "toRankId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "personalGold": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "kingdomGold": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "contributionRequired": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "reviewSeconds": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "items": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/CostItem"
          },
          "minItems": 0
        }
      }
    },
    "Promotion": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "status",
        "targetRankId",
        "operationId",
        "startedAtUtc",
        "finishesAtUtc",
        "costSnapshot"
      ],
      "properties": {
        "status": {
          "type": "string",
          "enum": [
            "NONE",
            "READY",
            "IN_REVIEW",
            "COMPLETED_PENDING_APPLY"
          ]
        },
        "targetRankId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "operationId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid-v7"
            },
            {
              "type": "null"
            }
          ]
        },
        "startedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "finishesAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "costSnapshot": {
          "anyOf": [
            {
              "$ref": "#/$defs/PromotionCostSnapshot"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "PotionStack": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "potionId",
        "quantity"
      ],
      "properties": {
        "potionId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        }
      }
    },
    "EquipmentSlots": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "WEAPON",
        "ARMOR",
        "HELMET",
        "ACCESSORY"
      ],
      "properties": {
        "WEAPON": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "ARMOR": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "HELMET": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "ACCESSORY": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "MercenaryAutonomy": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "state",
        "currentRegionId",
        "targetInstanceId",
        "reasonCode",
        "stateStartedAtUtc",
        "nextDecisionAtUtc"
      ],
      "properties": {
        "state": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "currentRegionId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "targetInstanceId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "reasonCode": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "stateStartedAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "nextDecisionAtUtc": {
          "type": "string",
          "format": "utc-instant"
        }
      }
    },
    "MercenaryRecords": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "huntCount",
        "killCount",
        "raidClearCount",
        "itemsCollected"
      ],
      "properties": {
        "huntCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "killCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "raidClearCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "itemsCollected": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        }
      }
    },
    "Mercenary": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "instanceId",
        "generationProfileId",
        "nameSeed",
        "appearanceSeed",
        "growthSeed",
        "displayName",
        "jobId",
        "gradeId",
        "rankId",
        "level",
        "exp",
        "personalityId",
        "traitIds",
        "personalGold",
        "contribution",
        "active",
        "autonomy",
        "potions",
        "equipmentSlots",
        "promotion",
        "records"
      ],
      "properties": {
        "instanceId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "generationProfileId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "nameSeed": {
          "type": "string",
          "pattern": "^(0|[1-9][0-9]{0,19})$"
        },
        "appearanceSeed": {
          "type": "string",
          "pattern": "^(0|[1-9][0-9]{0,19})$"
        },
        "growthSeed": {
          "type": "string",
          "pattern": "^(0|[1-9][0-9]{0,19})$"
        },
        "displayName": {
          "type": "string",
          "minLength": 1,
          "maxLength": 20
        },
        "jobId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "gradeId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "rankId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "level": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "exp": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "personalityId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "traitIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        },
        "personalGold": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "contribution": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "active": {
          "type": "boolean"
        },
        "autonomy": {
          "$ref": "#/$defs/MercenaryAutonomy"
        },
        "potions": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/PotionStack"
          },
          "minItems": 0
        },
        "equipmentSlots": {
          "$ref": "#/$defs/EquipmentSlots"
        },
        "promotion": {
          "$ref": "#/$defs/Promotion"
        },
        "records": {
          "$ref": "#/$defs/MercenaryRecords"
        }
      }
    },
    "ManagementNpc": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "instanceId",
        "professionId",
        "proficiencyId",
        "proficiencyExp",
        "assignedFacilityId",
        "working"
      ],
      "properties": {
        "instanceId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "professionId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "proficiencyId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "proficiencyExp": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "assignedFacilityId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "working": {
          "type": "boolean"
        }
      }
    },
    "FacilityJob": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "operationId",
        "jobType",
        "status",
        "recipeId",
        "targetLevel",
        "treatmentTargetInstanceId",
        "contentVersion",
        "startedAtUtc",
        "finishesAtUtc",
        "claimedAtUtc",
        "cancelledAtUtc",
        "cycleCount",
        "inputSnapshot",
        "outputSnapshot"
      ],
      "properties": {
        "operationId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "jobType": {
          "type": "string",
          "enum": [
            "BUILD",
            "UPGRADE",
            "PRODUCTION",
            "CRAFT",
            "TREATMENT"
          ]
        },
        "status": {
          "type": "string",
          "enum": [
            "RUNNING",
            "READY",
            "CLAIMED",
            "CANCELLED"
          ]
        },
        "recipeId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "targetLevel": {
          "anyOf": [
            {
              "type": "integer",
              "minimum": 1,
              "maximum": 4
            },
            {
              "type": "null"
            }
          ]
        },
        "treatmentTargetInstanceId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "contentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "startedAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "finishesAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "claimedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "cancelledAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "cycleCount": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "inputSnapshot": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/AssetAmount"
          },
          "minItems": 0
        },
        "outputSnapshot": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RewardSnapshot"
          },
          "minItems": 0
        }
      }
    },
    "FacilityStorageEntry": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "storageType",
        "resourceId",
        "quantity",
        "sourceOperationId"
      ],
      "properties": {
        "storageType": {
          "type": "string",
          "enum": [
            "ITEM",
            "POTION"
          ]
        },
        "resourceId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "sourceOperationId": {
          "type": "string",
          "format": "uuid-v7"
        }
      }
    },
    "Facility": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "facilityId",
        "level",
        "state",
        "assignedNpcInstanceId",
        "job",
        "storage"
      ],
      "properties": {
        "facilityId": {
          "type": "string",
          "enum": [
            "FAC_TAVERN",
            "FAC_LODGE",
            "FAC_GUILD",
            "FAC_STORE",
            "FAC_BLACKSMITH",
            "FAC_ALCHEMY",
            "FAC_WAREHOUSE",
            "FAC_INFIRMARY"
          ]
        },
        "level": {
          "type": "integer",
          "minimum": 1,
          "maximum": 4
        },
        "state": {
          "type": "string",
          "enum": [
            "LOCKED",
            "BUILDABLE",
            "BUILDING",
            "ACTIVE",
            "UPGRADING",
            "STOPPED"
          ]
        },
        "assignedNpcInstanceId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "job": {
          "anyOf": [
            {
              "$ref": "#/$defs/FacilityJob"
            },
            {
              "type": "null"
            }
          ]
        },
        "storage": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/FacilityStorageEntry"
          },
          "minItems": 0
        }
      }
    },
    "ItemStack": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "itemId",
        "quantity"
      ],
      "properties": {
        "itemId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "quantity": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        }
      }
    },
    "EquipmentInstance": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "instanceId",
        "equipmentTemplateId",
        "tier",
        "qualityId",
        "enhancementLevel",
        "refineOption",
        "locked",
        "equippedByMercenaryInstanceId",
        "sourceContentVersion",
        "generationOperationId"
      ],
      "properties": {
        "instanceId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "equipmentTemplateId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "tier": {
          "type": "integer",
          "minimum": 1,
          "maximum": 5
        },
        "qualityId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "enhancementLevel": {
          "type": "integer",
          "minimum": 0,
          "maximum": 10
        },
        "refineOption": {
          "anyOf": [
            {
              "$ref": "#/$defs/RefineOption"
            },
            {
              "type": "null"
            }
          ]
        },
        "locked": {
          "type": "boolean"
        },
        "equippedByMercenaryInstanceId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid"
            },
            {
              "type": "null"
            }
          ]
        },
        "sourceContentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "generationOperationId": {
          "type": "string",
          "format": "uuid-v7"
        }
      }
    },
    "Inventory": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "itemStacks",
        "equipment",
        "warehousePotions"
      ],
      "properties": {
        "itemStacks": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ItemStack"
          },
          "minItems": 0
        },
        "equipment": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/EquipmentInstance"
          },
          "minItems": 0
        },
        "warehousePotions": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/PotionStack"
          },
          "minItems": 0
        }
      }
    },
    "RegionProgress": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "regionId",
        "unlocked",
        "progressPercent",
        "highestRankReachedId",
        "huntCount",
        "eliteKillCount",
        "firstUnlockedAtUtc",
        "lastVisitedAtUtc"
      ],
      "properties": {
        "regionId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "unlocked": {
          "type": "boolean"
        },
        "progressPercent": {
          "type": "integer",
          "minimum": 0,
          "maximum": 100
        },
        "highestRankReachedId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "huntCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "eliteKillCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "firstUnlockedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "lastVisitedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "RaidPartProgress": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "partId",
        "breakCount",
        "exposedCount",
        "bestBreakTimeMs",
        "lastRewardOperationId"
      ],
      "properties": {
        "partId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "breakCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "exposedCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "bestBreakTimeMs": {
          "anyOf": [
            {
              "type": "integer",
              "minimum": 0,
              "maximum": 9007199254740991
            },
            {
              "type": "null"
            }
          ]
        },
        "lastRewardOperationId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid-v7"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "RaidProgress": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "raidId",
        "difficulty",
        "unlocked",
        "clearCount",
        "bestClearTimeMs",
        "firstClearedAtUtc",
        "lastClearedAtUtc",
        "firstClearRewardOperationId",
        "partStates"
      ],
      "properties": {
        "raidId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "difficulty": {
          "type": "string",
          "enum": [
            "NORMAL",
            "HARD",
            "CORRUPTED"
          ]
        },
        "unlocked": {
          "type": "boolean"
        },
        "clearCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "bestClearTimeMs": {
          "anyOf": [
            {
              "type": "integer",
              "minimum": 0,
              "maximum": 9007199254740991
            },
            {
              "type": "null"
            }
          ]
        },
        "firstClearedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "lastClearedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "firstClearRewardOperationId": {
          "anyOf": [
            {
              "type": "string",
              "format": "uuid-v7"
            },
            {
              "type": "null"
            }
          ]
        },
        "partStates": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RaidPartProgress"
          },
          "minItems": 0
        }
      }
    },
    "Regions": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "progress",
        "raids"
      ],
      "properties": {
        "progress": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RegionProgress"
          },
          "minItems": 0
        },
        "raids": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RaidProgress"
          },
          "minItems": 0
        }
      }
    },
    "PremiumWalletCache": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "freePremium",
        "paidPremium",
        "specialRecruitTickets"
      ],
      "properties": {
        "freePremium": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "paidPremium": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "specialRecruitTickets": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        }
      }
    },
    "PityCounter": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "pityGroupId",
        "pityRuleId",
        "pullCount",
        "lastUpdatedContentVersion"
      ],
      "properties": {
        "pityGroupId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "pityRuleId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "pullCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "lastUpdatedContentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        }
      }
    },
    "FeaturedGuarantee": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "pityGroupId",
        "rateUpGroupId",
        "state",
        "lastUpdatedContentVersion"
      ],
      "properties": {
        "pityGroupId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "rateUpGroupId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "state": {
          "type": "string",
          "enum": [
            "NONE",
            "NEXT_S_OR_SS_FEATURED"
          ]
        },
        "lastUpdatedContentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        }
      }
    },
    "PendingRecruitmentRequest": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "operationId",
        "poolId",
        "paymentType",
        "count",
        "requestHash",
        "status",
        "createdAtUtc",
        "serverReceiptId"
      ],
      "properties": {
        "operationId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "poolId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "paymentType": {
          "type": "string",
          "enum": [
            "KINGDOM_GOLD",
            "TICKET",
            "FREE_PREMIUM",
            "PAID_PREMIUM"
          ]
        },
        "count": {
          "type": "integer",
          "enum": [
            1,
            10
          ]
        },
        "requestHash": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        },
        "status": {
          "type": "string",
          "enum": [
            "PREPARED",
            "SENT",
            "RECEIVED"
          ]
        },
        "createdAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "serverReceiptId": {
          "anyOf": [
            {
              "type": "string",
              "minLength": 1,
              "maxLength": 128
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "RecruitmentMockState": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "authority",
        "serverRevision",
        "premiumWalletCache",
        "pityCounters",
        "featuredGuarantees",
        "pendingRequests"
      ],
      "properties": {
        "authority": {
          "type": "string",
          "enum": [
            "MOCK_ONLY",
            "SERVER_CACHE"
          ]
        },
        "serverRevision": {
          "anyOf": [
            {
              "type": "integer",
              "minimum": 0,
              "maximum": 9007199254740991
            },
            {
              "type": "null"
            }
          ]
        },
        "premiumWalletCache": {
          "$ref": "#/$defs/PremiumWalletCache"
        },
        "pityCounters": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/PityCounter"
          },
          "minItems": 0
        },
        "featuredGuarantees": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/FeaturedGuarantee"
          },
          "minItems": 0
        },
        "pendingRequests": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/PendingRecruitmentRequest"
          },
          "minItems": 0
        }
      }
    },
    "Tutorial": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "currentStepId",
        "completedStepIds",
        "grantedRewardIds",
        "skipped",
        "completedAtUtc"
      ],
      "properties": {
        "currentStepId": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "completedStepIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        },
        "grantedRewardIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        },
        "skipped": {
          "type": "boolean"
        },
        "completedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "OfflineSettlementLine": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "lineNo",
        "settlementType",
        "subjectType",
        "subjectId",
        "elapsedSeconds",
        "quantityDelta",
        "rewards",
        "consumptions"
      ],
      "properties": {
        "lineNo": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        },
        "settlementType": {
          "type": "string",
          "enum": [
            "HUNT",
            "FACILITY",
            "NPC_PROFICIENCY",
            "POTION_CONSUMPTION",
            "INJURY_RECOVERY",
            "PROMOTION_REVIEW"
          ]
        },
        "subjectType": {
          "type": "string",
          "enum": [
            "MERCENARY",
            "FACILITY",
            "MANAGEMENT_NPC",
            "PROMOTION"
          ]
        },
        "subjectId": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "elapsedSeconds": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "quantityDelta": {
          "type": "integer",
          "minimum": -9007199254740991,
          "maximum": 9007199254740991
        },
        "rewards": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RewardSnapshot"
          },
          "minItems": 0
        },
        "consumptions": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/AssetAmount"
          },
          "minItems": 0
        }
      }
    },
    "OfflinePendingSettlement": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "operationId",
        "status",
        "fromUtc",
        "toUtc",
        "cursorAfterUtc",
        "eligibleSeconds",
        "contentVersion",
        "ruleSnapshotVersion",
        "lines",
        "attemptCount",
        "lastAttemptAtUtc",
        "serverReceiptId",
        "errorCode"
      ],
      "properties": {
        "operationId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "status": {
          "type": "string",
          "enum": [
            "PREPARED",
            "APPLYING",
            "COMMITTED",
            "ACKNOWLEDGED",
            "FAILED_PERMANENT"
          ]
        },
        "fromUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "toUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "cursorAfterUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "eligibleSeconds": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "contentVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "ruleSnapshotVersion": {
          "type": "string",
          "minLength": 1,
          "maxLength": 64
        },
        "lines": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/OfflineSettlementLine"
          },
          "minItems": 0
        },
        "attemptCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 100
        },
        "lastAttemptAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "serverReceiptId": {
          "anyOf": [
            {
              "type": "string",
              "minLength": 1,
              "maxLength": 128
            },
            {
              "type": "null"
            }
          ]
        },
        "errorCode": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "Offline": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "accrualCursorUtc",
        "lastTrustedUtc",
        "pendingSettlement"
      ],
      "properties": {
        "accrualCursorUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "lastTrustedUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "pendingSettlement": {
          "anyOf": [
            {
              "$ref": "#/$defs/OfflinePendingSettlement"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "OperationJournalEntry": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "operationId",
        "operationType",
        "facilityJobType",
        "requestHash",
        "status",
        "createdAtUtc",
        "updatedAtUtc",
        "completedAtUtc",
        "serverReceiptId",
        "errorCode",
        "resultDigest",
        "failureResolution",
        "resolvedAtUtc"
      ],
      "properties": {
        "operationId": {
          "type": "string",
          "format": "uuid-v7"
        },
        "operationType": {
          "type": "string",
          "enum": [
            "OFFLINE_SETTLEMENT",
            "FACILITY_JOB",
            "PROMOTION",
            "RECRUITMENT",
            "REWARD",
            "SAVE_RECOVERY"
          ]
        },
        "facilityJobType": {
          "anyOf": [
            {
              "type": "string",
              "enum": [
                "BUILD",
                "UPGRADE",
                "PRODUCTION",
                "CRAFT",
                "TREATMENT"
              ]
            },
            {
              "type": "null"
            }
          ]
        },
        "requestHash": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        },
        "status": {
          "type": "string",
          "enum": [
            "PREPARED",
            "APPLYING",
            "COMMITTED",
            "ACKNOWLEDGED",
            "FAILED_PERMANENT"
          ]
        },
        "createdAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "updatedAtUtc": {
          "type": "string",
          "format": "utc-instant"
        },
        "completedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        },
        "serverReceiptId": {
          "anyOf": [
            {
              "type": "string",
              "minLength": 1,
              "maxLength": 128
            },
            {
              "type": "null"
            }
          ]
        },
        "errorCode": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
              "maxLength": 64
            },
            {
              "type": "null"
            }
          ]
        },
        "resultDigest": {
          "anyOf": [
            {
              "type": "string",
              "pattern": "^[0-9a-f]{64}$"
            },
            {
              "type": "null"
            }
          ]
        },
        "failureResolution": {
          "anyOf": [
            {
              "type": "string",
              "enum": [
                "DISCARDED_BY_USER"
              ]
            },
            {
              "type": "null"
            }
          ]
        },
        "resolvedAtUtc": {
          "anyOf": [
            {
              "type": "string",
              "format": "utc-instant"
            },
            {
              "type": "null"
            }
          ]
        }
      }
    },
    "Settings": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "masterVolume",
        "musicVolume",
        "sfxVolume",
        "vibrationEnabled",
        "battleSpeed"
      ],
      "properties": {
        "masterVolume": {
          "type": "number",
          "minimum": 0,
          "maximum": 1
        },
        "musicVolume": {
          "type": "number",
          "minimum": 0,
          "maximum": 1
        },
        "sfxVolume": {
          "type": "number",
          "minimum": 0,
          "maximum": 1
        },
        "vibrationEnabled": {
          "type": "boolean"
        },
        "battleSpeed": {
          "type": "integer",
          "minimum": 1,
          "maximum": 9007199254740991
        }
      }
    },
    "RegionAccessPolicy": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "regionId",
        "allowed"
      ],
      "properties": {
        "regionId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "allowed": {
          "type": "boolean"
        }
      }
    },
    "InventoryPolicies": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "autoSellMaxTier",
        "protectQualityIds",
        "autoEquipEnabled",
        "upgradeThresholdBps",
        "autoSellEnabled",
        "protectBossEquipment",
        "protectFirstDiscovery",
        "discoveredEquipmentTemplateIds"
      ],
      "properties": {
        "autoSellMaxTier": {
          "type": "integer",
          "minimum": 0,
          "maximum": 5
        },
        "protectQualityIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        },
        "autoEquipEnabled": {
          "type": "boolean"
        },
        "upgradeThresholdBps": {
          "type": "integer",
          "minimum": 0,
          "maximum": 10000
        },
        "autoSellEnabled": {
          "type": "boolean"
        },
        "protectBossEquipment": {
          "type": "boolean"
        },
        "protectFirstDiscovery": {
          "type": "boolean"
        },
        "discoveredEquipmentTemplateIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        }
      }
    },
    "Profile": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "nickname",
        "locale",
        "playerExp",
        "progressionFlagIds"
      ],
      "properties": {
        "nickname": {
          "type": "string",
          "minLength": 1,
          "maxLength": 20
        },
        "locale": {
          "type": "string",
          "minLength": 2,
          "maxLength": 16
        },
        "playerExp": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "progressionFlagIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        }
      }
    },
    "Kingdom": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "kingdomStageId",
        "kingdomGold",
        "kingdomExp",
        "activeMercenaryLimit",
        "ownedMercenaryLimit",
        "pricingPolicy",
        "regionAccessPolicies",
        "inventoryPolicies",
        "progressionFlagIds",
        "facilityUpgradeCount"
      ],
      "properties": {
        "kingdomStageId": {
          "type": "string",
          "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
          "maxLength": 64
        },
        "kingdomGold": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "kingdomExp": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        },
        "activeMercenaryLimit": {
          "type": "integer",
          "enum": [
            4,
            8,
            12,
            16
          ]
        },
        "ownedMercenaryLimit": {
          "type": "integer",
          "minimum": 8,
          "maximum": 24
        },
        "pricingPolicy": {
          "type": "string",
          "enum": [
            "LOW",
            "STANDARD",
            "HIGH"
          ]
        },
        "regionAccessPolicies": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/RegionAccessPolicy"
          },
          "minItems": 0
        },
        "inventoryPolicies": {
          "$ref": "#/$defs/InventoryPolicies"
        },
        "progressionFlagIds": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$",
            "maxLength": 64
          },
          "minItems": 0,
          "uniqueItems": true
        },
        "facilityUpgradeCount": {
          "type": "integer",
          "minimum": 0,
          "maximum": 9007199254740991
        }
      }
    },
    "Payload": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "profile",
        "kingdom",
        "mercenaries",
        "managementNpcs",
        "facilities",
        "inventory",
        "regions",
        "recruitmentMockState",
        "tutorial",
        "offline",
        "operationJournal",
        "settings",
        "extensions"
      ],
      "properties": {
        "profile": {
          "$ref": "#/$defs/Profile"
        },
        "kingdom": {
          "$ref": "#/$defs/Kingdom"
        },
        "mercenaries": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Mercenary"
          },
          "minItems": 0
        },
        "managementNpcs": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/ManagementNpc"
          },
          "minItems": 0
        },
        "facilities": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Facility"
          },
          "minItems": 8,
          "maxItems": 8
        },
        "inventory": {
          "$ref": "#/$defs/Inventory"
        },
        "regions": {
          "$ref": "#/$defs/Regions"
        },
        "recruitmentMockState": {
          "$ref": "#/$defs/RecruitmentMockState"
        },
        "tutorial": {
          "$ref": "#/$defs/Tutorial"
        },
        "offline": {
          "$ref": "#/$defs/Offline"
        },
        "operationJournal": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/OperationJournalEntry"
          },
          "minItems": 0
        },
        "settings": {
          "$ref": "#/$defs/Settings"
        },
        "extensions": {
          "type": "object",
          "maxProperties": 0,
          "additionalProperties": false
        }
      }
    },
    "Integrity": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "integrityVersion",
        "algorithm",
        "canonicalization",
        "payloadSha256",
        "fileSha256"
      ],
      "properties": {
        "integrityVersion": {
          "const": 1
        },
        "algorithm": {
          "const": "SHA-256"
        },
        "canonicalization": {
          "const": "RFC8785"
        },
        "payloadSha256": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        },
        "fileSha256": {
          "type": "string",
          "pattern": "^[0-9a-f]{64}$"
        }
      }
    }
  }
}
```

P06 `.4` uses base P05 schema hash `d0f20a54d5096f6dd4cf1b4b859177fa730477634e10e16a237ea8a995d614d5`. `.4` valid passes base; `.5` valid passes P07; `.4` carrying new Kingdom policy fields fails base additionalProperties; `payload.inventory.inventoryPolicies` fails Inventory additionalProperties in both; unknown field fails; Draft202012Validator.check_schema passes.

Migration P06_TO_P07_001 expands existing Kingdom policy without changing its current autoSellMaxTier/protectQualityIds, adds two small heal potions to each active starter, sets .5/revision+1 and commits atomically. Inventory remains three fields.

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.5",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 1,
  "createdAtUtc": "2026-07-22T00:00:00.000Z",
  "savedAtUtc": "2026-07-22T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "549da561612263e7c5312dab6f1178d76fa40b81c76680a755630664972ffa1f",
    "fileSha256": "290e7c6984115f73aacd271ffc687fd4d7622b7ad3b21a891401b9495c8a76f4"
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
        "protectQualityIds": [],
        "autoEquipEnabled": true,
        "upgradeThresholdBps": 500,
        "autoSellEnabled": true,
        "protectBossEquipment": true,
        "protectFirstDiscovery": true,
        "discoveredEquipmentTemplateIds": []
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
          "stateStartedAtUtc": "2026-07-22T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-22T00:00:00.000Z"
        },
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
          "stateStartedAtUtc": "2026-07-22T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-22T00:00:00.000Z"
        },
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
          "stateStartedAtUtc": "2026-07-22T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-22T00:00:00.000Z"
        },
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
          "stateStartedAtUtc": "2026-07-22T00:00:00.000Z",
          "nextDecisionAtUtc": "2026-07-22T00:00:00.000Z"
        },
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
          "firstUnlockedAtUtc": "2026-07-22T00:00:00.000Z",
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
      "accrualCursorUtc": "2026-07-22T00:00:00.000Z",
      "lastTrustedUtc": "2026-07-22T00:00:00.000Z",
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

```json
{
  "schemaId": "urn:tycoon:save:v1",
  "saveVersion": 1,
  "gameVersion": "1.0.0",
  "contentVersion": "1.0.0-content.5",
  "saveId": "019f7cd2-8800-7000-8000-000000000101",
  "profileId": "019f7cd2-8800-7000-8000-000000000102",
  "revision": 3,
  "createdAtUtc": "2026-07-20T00:00:00.000Z",
  "savedAtUtc": "2026-07-22T00:00:00.000Z",
  "integrity": {
    "integrityVersion": 1,
    "algorithm": "SHA-256",
    "canonicalization": "RFC8785",
    "payloadSha256": "805f4d1fb388e0a1fbf4259e94d3f5c6e4d5f294b76c7e70d21b2a892c21afef",
    "fileSha256": "43c64e452f62c88fbb6c626d29f1167ee91230477b7f7cf5bd1630f585727334"
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
        "protectQualityIds": [],
        "autoEquipEnabled": true,
        "upgradeThresholdBps": 500,
        "autoSellEnabled": true,
        "protectBossEquipment": true,
        "protectFirstDiscovery": true,
        "discoveredEquipmentTemplateIds": []
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
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
        "potions": [
          {
            "potionId": "POT_HEAL_SMALL",
            "quantity": 2
          }
        ],
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
| P07_NEW | 7529 | 549da561612263e7c5312dab6f1178d76fa40b81c76680a755630664972ffa1f | 290e7c6984115f73aacd271ffc687fd4d7622b7ad3b21a891401b9495c8a76f4 |
| P06_BEFORE | 7195 | 46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b | 42fa5de8ab52657b1d721a948ba41607f61b552b1cc892f485ca5b7bec100aa3 |
| P07_AFTER | 7529 | 805f4d1fb388e0a1fbf4259e94d3f5c6e4d5f294b76c7e70d21b2a892c21afef | 43c64e452f62c88fbb6c626d29f1167ee91230477b7f7cf5bd1630f585727334 |

## 13. command·query·DTO·potion transfer contract

**상태: CONFIRMED**

Existing inventory/equipment commands remain. Add `TransferPotionToMercenaryCommand(OperationId,ExpectedRevision,RequestHash,MercenaryInstanceId,PotionId,Quantity)` and `ReturnPotionToWarehouseCommand` with identical fields. Quantity1..safe-int, partial=false. Handler validates request hash→revision→owned mercenary→town-safe→source quantity→destination distinct slot max4/warehouse capacity, then moves all quantity in one clone and REWARD journal revision. Retry same hash returns stored result; different hash conflicts. Events `PotionTransferredToMercenary` and `PotionReturnedToWarehouse` carry operationId,mercenaryId,potionId,quantity,revision. Inventory query DTO reads policy only from Kingdom projection.

## 14. event·error registry

**상태: CONFIRMED**

| Code | Condition | Rollback | Retryable | ko key | Recovery |
|---|---|---|---|---|---|
| P07_CAPACITY_EXCEEDED | exact validator/guard named by suffix | draft or transient mutation discarded | TRUE | TXT_P07_CAPACITY_ERROR | retry/reload |
| P07_STACK_OVERFLOW | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPMENT_LINK_CONFLICT | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPMENT_JOB_INELIGIBLE | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPMENT_SLOT_WRONG | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_ITEM_PROTECTED | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPPED_ITEM_SALE_FORBIDDEN | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_SAVE_REVISION_CONFLICT | exact validator/guard named by suffix | draft or transient mutation discarded | TRUE | TXT_P07_ERROR | retry/reload |
| P07_OPERATION_DUPLICATE_MISMATCH | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_CONTENT_MISSING | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_REWARD_UNION_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPMENT_GENERATION_SPLIT_BRAIN | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_COMBAT_STATE_FORBIDDEN | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_POTION_EMPTY | exact validator/guard named by suffix | draft or transient mutation discarded | TRUE | TXT_P07_ERROR | retry/reload |
| P07_EQUIPMENT_CANDIDATE_EMPTY | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_EQUIPMENT_DUPLICATE | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_HUNT_BUFFER_FULL | exact validator/guard named by suffix | draft or transient mutation discarded | TRUE | TXT_P07_HUNT_BUFFER_FULL | retry/reload |
| P07_ITEM_STACK_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_LOOT_DRAW_MODE_UNSUPPORTED | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_LOOT_QUANTITY_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_LOOT_RNG_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_OWNERSHIP_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_POTION_STACK_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_QUALITY_WEIGHT_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_REFINE_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_ERROR | error detail/back |
| P07_POLICY_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_POLICY_INVALID | error detail/back |
| P07_POTION_TRANSFER_INVALID | exact validator/guard named by suffix | draft or transient mutation discarded | FALSE | TXT_P07_TRANSFER_INVALID | error detail/back |

Validator extracts raised/returned P07 codes and registry equality is MATCH. Telemetry-only `P07_LOOT_DISCARDED_CAPACITY` is an event code, not an error.

## 15. inventory/equipment/potion UI exact hierarchy

**상태: CONFIRMED**

| Stable ID | Parent | Rect | Touch | Key | Visible | Interactable | Disabled | Focus | Block |
|---|---|---|---|---|---|---|---|---|---|
| P07_UI_GRID | InventoryScreen | 24,300,1192,756 | 0 | TXT_P07_INVENTORY_TITLE | content | scroll/select |  | 1..30 | none |
| P07_UI_CATEGORY | Toolbar | 24,0,520,72 | 64 | TXT_P07_FILTER | content | enabled |  | 1 | none |
| P07_UI_SORT | Toolbar | 560,0,240,72 | 64 | TXT_P07_SORT | content | enabled |  | 2 | none |
| P07_UI_ITEM_DETAIL | DetailDrawer | 0,0,656,756 | 64 | TXT_P07_INVENTORY_TITLE | selection | actions by guard | guard key | 10..14 | drawer |
| P07_UI_COMPARE | DetailDrawer/Compare | 0,160,656,360 | 0 | TXT_P07_SCORE | equipment | read-only |  |  | drawer |
| P07_UI_EQUIP | DetailDrawer | 24,660,180,72 | 72 | TXT_P07_EQUIP | equipment | town+eligible | TXT_P07_COMBAT_GUARD | 15 | drawer |
| P07_UI_UNEQUIP | DetailDrawer | 216,660,180,72 | 72 | TXT_P07_UNEQUIP | equipped | town | TXT_P07_COMBAT_GUARD | 16 | drawer |
| P07_UI_SELL | DetailDrawer | 408,660,180,72 | 72 | TXT_P07_SELL | sellable | not protected | TXT_P07_PROTECTED_ERROR | 17 | drawer |
| P07_UI_SELL_MODAL | InventoryScreen | 660,330,600,420 | 64 | TXT_P07_SELL_CONFIRM | sell tap | confirm/cancel |  | 1,2 | modal all |
| P07_UI_POLICY_MODAL | InventoryScreen | 560,180,800,720 | 64 | TXT_P07_AUTO_SELL | policy tap | valid fields | TXT_P07_POLICY_INVALID | 1..12 | modal all |
| P07_UI_POTION_PANEL | DetailDrawer | 0,120,656,500 | 64 | TXT_P07_TAB_POTIONS | potion | select merc |  | 5..12 | drawer |
| P07_UI_TRANSFER | P07_UI_POTION_PANEL | 24,380,280,72 | 72 | TXT_P07_POTION_TRANSFER | warehouse qty>0 | town+capacity | TXT_P07_POTION_CAPACITY | 13 | drawer |
| P07_UI_RETURN | P07_UI_POTION_PANEL | 328,380,280,72 | 72 | TXT_P07_POTION_RETURN | merc qty>0 | town+warehouse | TXT_P07_CAPACITY_ERROR | 14 | drawer |
| P07_UI_OVERFLOW | InventoryScreen | 480,120,960,840 | 64 | TXT_P07_OVERFLOW_TITLE | discardedLoot>0 | close |  | 1 | modal all |
| P07_UI_STATE | InventoryScreen | 0,112,1920,968 | 64 | TXT_P07_LOADING | state | retry/back |  | 1 | screen |

Comparison shows each P06 stat before/after/delta plus score. 130% text increases row height and scrolls; long Korean wraps. Modal blocks all lower scopes. Back closes top modal→drawer→screen.

## 16. asset·Addressables·Editor generation

**상태: CONFIRMED**

| assetId | Address | Subasset |
|---|---|---|
| ASSET_P07_ITEM_ALCHEMY | P07/Items/ALCHEMY | ITEM_ALCHEMY |
| ASSET_P07_ITEM_BOSS | P07/Items/BOSS | ITEM_BOSS |
| ASSET_P07_ITEM_CRAFT | P07/Items/CRAFT | ITEM_CRAFT |
| ASSET_P07_ITEM_ENHANCE | P07/Items/ENHANCE | ITEM_ENHANCE |
| ASSET_P07_ITEM_MAGIC | P07/Items/MAGIC | ITEM_MAGIC |
| ASSET_P07_ITEM_MONSTER | P07/Items/MONSTER | ITEM_MONSTER |
| ASSET_P07_ITEM_ORE | P07/Items/ORE | ITEM_ORE |
| ASSET_P07_ITEM_PROMOTION | P07/Items/PROMOTION | ITEM_PROMOTION |
| ASSET_P07_ITEM_REFINE | P07/Items/REFINE | ITEM_REFINE |
| ASSET_P07_ITEM_RELIC | P07/Items/RELIC | ITEM_RELIC |
| ASSET_P07_SLOT_WEAPON | P07/Slots/WEAPON | SLOT_WEAPON |
| ASSET_P07_SLOT_ARMOR | P07/Slots/ARMOR | SLOT_ARMOR |
| ASSET_P07_SLOT_HELMET | P07/Slots/HELMET | SLOT_HELMET |
| ASSET_P07_SLOT_ACCESSORY | P07/Slots/ACCESSORY | SLOT_ACCESSORY |
| ASSET_P07_TIER_1 | P07/Tier/1 | TIER_1 |
| ASSET_P07_TIER_2 | P07/Tier/2 | TIER_2 |
| ASSET_P07_TIER_3 | P07/Tier/3 | TIER_3 |
| ASSET_P07_TIER_4 | P07/Tier/4 | TIER_4 |
| ASSET_P07_TIER_5 | P07/Tier/5 | TIER_5 |
| ASSET_P07_QUALITY_QUALITY_COMMON | P07/Quality/QUALITY_COMMON | QUALITY_QUALITY_COMMON |
| ASSET_P07_QUALITY_QUALITY_FINE | P07/Quality/QUALITY_FINE | QUALITY_QUALITY_FINE |
| ASSET_P07_QUALITY_QUALITY_RARE | P07/Quality/QUALITY_RARE | QUALITY_QUALITY_RARE |
| ASSET_P07_QUALITY_QUALITY_LEGACY | P07/Quality/QUALITY_LEGACY | QUALITY_QUALITY_LEGACY |
| ASSET_P07_QUALITY_QUALITY_RELIC | P07/Quality/QUALITY_RELIC | QUALITY_QUALITY_RELIC |
| ASSET_P07_STATE_EMPTY | P07/State/EMPTY | STATE_EMPTY |
| ASSET_P07_STATE_ERROR | P07/State/ERROR | STATE_ERROR |
| ASSET_P07_STATE_LOCKED | P07/State/LOCKED | STATE_LOCKED |

Atlas addresses: P07/Atlases/Items,Slots,Tiers,Qualities,States. Each address above resolves an explicit subasset of the same logical suffix; 64×64 item/slot,32×32 tier,96×96 quality,128×128 state. Equipment profile set is `BOW,CLOTH,GUARD,HAMMER_SHIELD,HEAVY,LIGHT,MACE,POWER,STAFF,SWORD,WISDOM`; source set BOSS,CRAFT and validator compares exact case. `P07GeneratedAssetVerifier --all-addresses` rejects missing/duplicate/case mismatch and loads every row.

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
ASSET_P07_ITEM_ALCHEMY,P07 Item ALCHEMY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ALCHEMY; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_BOSS,P07 Item BOSS,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/BOSS; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_CRAFT,P07 Item CRAFT,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/CRAFT; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_ENHANCE,P07 Item ENHANCE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ENHANCE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_MAGIC,P07 Item MAGIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/MAGIC; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_MONSTER,P07 Item MONSTER,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/MONSTER; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_ORE,P07 Item ORE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/ORE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_PROMOTION,P07 Item PROMOTION,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/PROMOTION; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_REFINE,P07 Item REFINE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/REFINE; logical asset,CONFIRMED,TRUE
ASSET_P07_ITEM_RELIC,P07 Item RELIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Items/RELIC; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_COMMON,P07 Quality QUALITY_COMMON,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_COMMON; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_FINE,P07 Quality QUALITY_FINE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_FINE; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_LEGACY,P07 Quality QUALITY_LEGACY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_LEGACY; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_RARE,P07 Quality QUALITY_RARE,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_RARE; logical asset,CONFIRMED,TRUE
ASSET_P07_QUALITY_QUALITY_RELIC,P07 Quality QUALITY_RELIC,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Quality/QUALITY_RELIC; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_ACCESSORY,P07 Slot ACCESSORY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/ACCESSORY; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_ARMOR,P07 Slot ARMOR,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/ARMOR; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_HELMET,P07 Slot HELMET,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/HELMET; logical asset,CONFIRMED,TRUE
ASSET_P07_SLOT_WEAPON,P07 Slot WEAPON,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Slots/WEAPON; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_EMPTY,P07 State EMPTY,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/EMPTY; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_ERROR,P07 State ERROR,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/ERROR; logical asset,CONFIRMED,TRUE
ASSET_P07_STATE_LOCKED,P07 State LOCKED,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/State/LOCKED; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_1,P07 Tier 1,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/1; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_2,P07 Tier 2,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/2; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_3,P07 Tier 3,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/3; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_4,P07 Tier 4,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/4; logical asset,CONFIRMED,TRUE
ASSET_P07_TIER_5,P07 Tier 5,INTERNAL,,1,2026-07-22,0,Project,TRUE,TRUE,FALSE,P07,address=P07/Tier/5; logical asset,CONFIRMED,TRUE
```

## 17. lifecycle·performance budget

**상태: CONFIRMED**

| Metric | Acceptance |
|---|---|
| 100-row query | p95<=3ms; allocation<=96KiB per refresh |
| virtualization | live views<=30; scroll steady 0B/frame |
| loot batch | 100 reward lines resolve+score p95<=8ms editor reference |
| score batch | 100 equipment×5 jobs p95<=5ms |
| Save writes | one per terminal loot/manual command; no per-line write |
| 30min loop | memory sustained growth<=8MiB after warmup; exceptions0 |
| frame | inventory scroll p95<=16.67ms desktop reference; Android p95<=33.3ms |
| lifecycle | 10 open/close cycles listener/handle/pool baseline |

## 18. EditMode·PlayMode·integration golden

**상태: CONFIRMED**

Retain v1.0 fixtures and add: schema selector .4/.5 success; .4 mixed field fail; duplicate policy path fail; unknown fail; schema compile/all refs; single Kingdom policy authority; one settlement per hunt; encounter writes0; crash at each terminal step; killCount total/huntCount once; transfer/return/capacity; five job stat modifier; enhancement -1/0/10/11; protected/stack/auto-sell overflow; exact RNG traces/rejection; all journal hashes; every asset; every disabled reason; error registry equality. Tests use injected clock/UUID/RNG and zero sleep. 30-minute loop requires one journal per hunt and no persistent encounter writes.

## 19. Android build·수동 검증·capture

**상태: CONFIRMED**

| CaptureId | Fixture | Resolution | Filename | Required |
|---|---|---|---|---|
| P07_CAP_01 | inventory items | 1920×1080 | p07_01_items.png | materials/grid/capacity |
| P07_CAP_02 | equipment | 1920×1080 | p07_02_equipment.png | quality/slot/lock |
| P07_CAP_03 | compare | 1920×1080 | p07_03_compare.png | scores/stat deltas |
| P07_CAP_04 | auto equip | 1920×1080 | p07_04_auto_equip.png | swap/toast |
| P07_CAP_05 | policy | 1920×1080 | p07_05_policy.png | threshold/protection |
| P07_CAP_06 | sale confirm | 1920×1080 | p07_06_sale_confirm.png | lines/personal gold |
| P07_CAP_07 | loot result | 1920×1080 | p07_07_loot.png | retained/sold |
| P07_CAP_08 | empty | 1920×1080 | p07_08_empty.png | empty state |
| P07_CAP_09 | error | 1920×1080 | p07_09_error.png | retry/diagnostic |
| P07_CAP_10 | 20:9 | 2400×1080 | p07_10_20x9.png | safe area |

Build entry `KingdomTycoon.Editor.P07AndroidBuilder.Build`, output `Builds/Android/KingdomTycoon-P07-Development.apk`, IL2CPP ARM64. Record BUILD_DERIVED size/SHA in report. Acceptance requires 30min integrated hunt→potion→loot→equip→auto/manual sale loop, 100-row list, physical Android Back/Safe Area/Korean text smoke, failure0.

## 20. 구현 파일 지도·커밋 분리

**상태: CONFIRMED**

| Logical slice | Exact new files | Exact modified files | Generated files | Verification command | Commit type | Commit message |
|---|---|---|---|---|---|---|
| design | docs/design/TYCOON_P07_LOOT_INVENTORY_EQUIPMENT_COMPLETE_DESIGN_v1.1.md | none | none | markdown gate | docs | docs: adopt P07 loot inventory equipment contract |
| content | StreamingAssets/Content/1.0.0-content.5;scripts/generate_p07_content.py | validate_content.py | 68 CSV/manifest | python scripts/generate_p07_content.py --check | feat | feat: add P07 inventory content |
| Save/domain | Runtime/Domain/Inventory;Application/Inventory;P06ToP07Migration.cs | Save v1 conditional schema registry | none | Unity EditMode | feat | feat: implement P07 loot transactions |
| UI | Runtime/Presentation/Inventory;Editor/P07InventorySetup.cs | P05 roster equipment tab via Editor API | ContentGenerated/P07Inventory | Unity PlayMode | feat | feat: add P07 inventory and equipment UI |
| tests | Tests/EditMode/Inventory;Tests/PlayMode/Inventory | none | captures | Unity tests | test | test: cover P07 loot and equipment acceptance |
| ci | scripts/ci/p07.sh | hooks/workflow | artifacts | scripts/ci/run-ci.sh | ci | ci: validate P07 package and Android build |
| report | docs/reports/P07_LOOT_INVENTORY_EQUIPMENT_REPORT.md | Draft PR | capture table | report checklist | docs | docs: report P07 implementation evidence |

## 21. P07 완료 checklist·P08 handoff

**상태: CONFIRMED**

- [x] v1.0 + corrections full standalone document
- [x] Kingdom inventoryPolicies sole authority
- [x] full .5 schema compiled and refs resolved
- [x] enhancement0..10
- [x] encounter transient; terminal once
- [x] town-safe auto-sale/auto-equip order
- [x] potion transfer/return atomic commands
- [x] five-job equipment combat stat vectors
- [x] overflow cannot deadlock terminal state
- [x] raw loot RNG and rejection golden
- [x] real request/result/journal hashes
- [x] all runtime errors in registry
- [x] wildcard assets0 and exact UI
- [x] full content/Save/schema hashes recomputed
- [x] P06/P07 shared table identical
- [x] Android/100-item/30-minute gates retained

## 22. 최종 선언

DOCUMENT_CONTENT_SHA256_SCOPE: UTF-8 bytes before this scope line

DOCUMENT_CONTENT_SHA256: 52f4717e5b9a11204c0389d21adc32d99046dc7a25c2cba440c518791f000a3c

UNRESOLVED: NONE

IMPLEMENTATION_READY: YES
