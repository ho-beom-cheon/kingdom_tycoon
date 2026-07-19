#!/usr/bin/env python3
"""Generate the strict P03 save JSON Schema from the approved v1.2.2 contract."""

from __future__ import annotations

import json
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUTS = (
    ROOT / "data" / "schemas" / "save.schema.json",
    ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts" / "save.schema.json",
)


def ref(name: str) -> dict:
    return {"$ref": f"#/$defs/{name}"}


def nullable(schema: dict) -> dict:
    return {"anyOf": [schema, {"type": "null"}]}


def array(item: dict, *, minimum: int = 0, maximum: int | None = None, unique: bool = False) -> dict:
    result = {"type": "array", "items": item, "minItems": minimum}
    if maximum is not None:
        result["maxItems"] = maximum
    if unique:
        result["uniqueItems"] = True
    return result


def obj(properties: dict, *, required: list[str] | None = None) -> dict:
    return {
        "type": "object",
        "additionalProperties": False,
        "required": required if required is not None else list(properties),
        "properties": properties,
    }


def enum(*values: str) -> dict:
    return {"type": "string", "enum": list(values)}


def integer(minimum: int | None = None, maximum: int | None = None) -> dict:
    result = {"type": "integer"}
    if minimum is not None:
        result["minimum"] = minimum
    if maximum is not None:
        result["maximum"] = maximum
    return result


SAFE_MAX = 9_007_199_254_740_991
stable_id = {"type": "string", "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", "maxLength": 64}
content_version = {"type": "string", "minLength": 1, "maxLength": 64}
uuid = {"type": "string", "format": "uuid"}
uuid_v7 = {"type": "string", "format": "uuid-v7"}
utc = {"type": "string", "format": "utc-instant"}
hash256 = {"type": "string", "pattern": "^[0-9a-f]{64}$"}
non_negative = integer(0, SAFE_MAX)
positive = integer(1, SAFE_MAX)
safe_int = integer(-SAFE_MAX, SAFE_MAX)
seed64 = {"type": "string", "pattern": "^(0|[1-9][0-9]{0,19})$"}
finite_number = {"type": "number", "minimum": -SAFE_MAX, "maximum": SAFE_MAX}


defs: dict[str, dict] = {}

defs["RefineOption"] = obj({"optionId": stable_id, "value": finite_number})
defs["GeneratedEquipmentSnapshot"] = obj(
    {
        "instanceId": uuid_v7,
        "equipmentTemplateId": stable_id,
        "tier": integer(1, 5),
        "qualityId": stable_id,
        "enhancementLevel": {"const": 0},
        "refineOption": nullable(ref("RefineOption")),
        "sourceContentVersion": content_version,
        "generationOperationId": uuid_v7,
        "randomTraceHash": hash256,
    }
)
defs["RewardSnapshot"] = obj(
    {
        "rewardType": enum(
            "ITEM",
            "POTION",
            "CURRENCY",
            "PERSONAL_GOLD",
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER",
            "PLAYER_EXP",
            "KINGDOM_EXP",
            "REGION_UNLOCK",
            "PROGRESSION_FLAG",
        ),
        "rewardId": stable_id,
        "quantity": positive,
        "destination": enum(
            "INVENTORY",
            "FACILITY_STORAGE",
            "MERCENARY",
            "PROFILE",
            "KINGDOM",
            "SERVER_WALLET",
            "REGION",
        ),
        "targetId": nullable({"type": "string", "minLength": 1, "maxLength": 64}),
        "generatedEquipmentSnapshot": nullable(ref("GeneratedEquipmentSnapshot")),
    }
)
defs["AssetAmount"] = obj(
    {
        "assetType": enum("ITEM", "POTION", "PERSONAL_GOLD", "KINGDOM_GOLD"),
        "assetId": stable_id,
        "quantity": positive,
        "targetMercenaryInstanceId": nullable(uuid),
    }
)
defs["CostItem"] = obj({"itemId": stable_id, "quantity": positive})
defs["PromotionCostSnapshot"] = obj(
    {
        "contentVersion": content_version,
        "fromRankId": stable_id,
        "toRankId": stable_id,
        "personalGold": non_negative,
        "kingdomGold": non_negative,
        "contributionRequired": non_negative,
        "reviewSeconds": non_negative,
        "items": array(ref("CostItem")),
    }
)
defs["Promotion"] = obj(
    {
        "status": enum("NONE", "READY", "IN_REVIEW", "COMPLETED_PENDING_APPLY"),
        "targetRankId": nullable(stable_id),
        "operationId": nullable(uuid_v7),
        "startedAtUtc": nullable(utc),
        "finishesAtUtc": nullable(utc),
        "costSnapshot": nullable(ref("PromotionCostSnapshot")),
    }
)
defs["PotionStack"] = obj({"potionId": stable_id, "quantity": positive})
defs["EquipmentSlots"] = obj(
    {slot: nullable(uuid) for slot in ("WEAPON", "ARMOR", "HELMET", "ACCESSORY")}
)
defs["MercenaryAutonomy"] = obj(
    {
        "state": stable_id,
        "currentRegionId": nullable(stable_id),
        "targetInstanceId": nullable(uuid),
        "reasonCode": stable_id,
        "stateStartedAtUtc": utc,
        "nextDecisionAtUtc": utc,
    }
)
defs["MercenaryRecords"] = obj(
    {name: non_negative for name in ("huntCount", "killCount", "raidClearCount", "itemsCollected")}
)
defs["Mercenary"] = obj(
    {
        "instanceId": uuid_v7,
        "generationProfileId": stable_id,
        "nameSeed": seed64,
        "appearanceSeed": seed64,
        "growthSeed": seed64,
        "displayName": {"type": "string", "minLength": 1, "maxLength": 20},
        "jobId": stable_id,
        "gradeId": stable_id,
        "rankId": stable_id,
        "level": integer(1, SAFE_MAX),
        "exp": non_negative,
        "personalityId": stable_id,
        "traitIds": array(stable_id, unique=True),
        "personalGold": non_negative,
        "contribution": non_negative,
        "active": {"type": "boolean"},
        "autonomy": ref("MercenaryAutonomy"),
        "potions": array(ref("PotionStack")),
        "equipmentSlots": ref("EquipmentSlots"),
        "promotion": ref("Promotion"),
        "records": ref("MercenaryRecords"),
    }
)
defs["ManagementNpc"] = obj(
    {
        "instanceId": uuid_v7,
        "professionId": stable_id,
        "proficiencyId": stable_id,
        "proficiencyExp": non_negative,
        "assignedFacilityId": nullable(stable_id),
        "working": {"type": "boolean"},
    }
)
defs["FacilityJob"] = obj(
    {
        "operationId": uuid_v7,
        "jobType": enum("BUILD", "UPGRADE", "PRODUCTION", "CRAFT", "TREATMENT"),
        "status": enum("RUNNING", "READY", "CLAIMED", "CANCELLED"),
        "recipeId": nullable(stable_id),
        "targetLevel": nullable(integer(1, 4)),
        "treatmentTargetInstanceId": nullable(uuid),
        "contentVersion": content_version,
        "startedAtUtc": utc,
        "finishesAtUtc": utc,
        "claimedAtUtc": nullable(utc),
        "cancelledAtUtc": nullable(utc),
        "cycleCount": positive,
        "inputSnapshot": array(ref("AssetAmount")),
        "outputSnapshot": array(ref("RewardSnapshot")),
    }
)
defs["FacilityStorageEntry"] = obj(
    {
        "storageType": enum("ITEM", "POTION"),
        "resourceId": stable_id,
        "quantity": positive,
        "sourceOperationId": uuid_v7,
    }
)
defs["Facility"] = obj(
    {
        "facilityId": enum(
            "FAC_TAVERN",
            "FAC_LODGE",
            "FAC_GUILD",
            "FAC_STORE",
            "FAC_BLACKSMITH",
            "FAC_ALCHEMY",
            "FAC_WAREHOUSE",
            "FAC_INFIRMARY",
        ),
        "level": integer(1, 4),
        "state": enum("LOCKED", "BUILDABLE", "BUILDING", "ACTIVE", "UPGRADING", "STOPPED"),
        "assignedNpcInstanceId": nullable(uuid),
        "job": nullable(ref("FacilityJob")),
        "storage": array(ref("FacilityStorageEntry")),
    }
)
defs["ItemStack"] = obj({"itemId": stable_id, "quantity": positive})
defs["EquipmentInstance"] = obj(
    {
        "instanceId": uuid_v7,
        "equipmentTemplateId": stable_id,
        "tier": integer(1, 5),
        "qualityId": stable_id,
        "enhancementLevel": integer(0, 10),
        "refineOption": nullable(ref("RefineOption")),
        "locked": {"type": "boolean"},
        "equippedByMercenaryInstanceId": nullable(uuid),
        "sourceContentVersion": content_version,
        "generationOperationId": uuid_v7,
    }
)
defs["Inventory"] = obj(
    {
        "itemStacks": array(ref("ItemStack")),
        "equipment": array(ref("EquipmentInstance")),
        "warehousePotions": array(ref("PotionStack")),
    }
)
defs["RegionProgress"] = obj(
    {
        "regionId": stable_id,
        "unlocked": {"type": "boolean"},
        "progressPercent": integer(0, 100),
        "highestRankReachedId": nullable(stable_id),
        "huntCount": non_negative,
        "eliteKillCount": non_negative,
        "firstUnlockedAtUtc": nullable(utc),
        "lastVisitedAtUtc": nullable(utc),
    }
)
defs["RaidPartProgress"] = obj(
    {
        "partId": stable_id,
        "breakCount": non_negative,
        "exposedCount": non_negative,
        "bestBreakTimeMs": nullable(non_negative),
        "lastRewardOperationId": nullable(uuid_v7),
    }
)
defs["RaidProgress"] = obj(
    {
        "raidId": stable_id,
        "difficulty": enum("NORMAL", "HARD", "CORRUPTED"),
        "unlocked": {"type": "boolean"},
        "clearCount": non_negative,
        "bestClearTimeMs": nullable(non_negative),
        "firstClearedAtUtc": nullable(utc),
        "lastClearedAtUtc": nullable(utc),
        "firstClearRewardOperationId": nullable(uuid_v7),
        "partStates": array(ref("RaidPartProgress")),
    }
)
defs["Regions"] = obj({"progress": array(ref("RegionProgress")), "raids": array(ref("RaidProgress"))})
defs["PremiumWalletCache"] = obj(
    {name: non_negative for name in ("freePremium", "paidPremium", "specialRecruitTickets")}
)
defs["PityCounter"] = obj(
    {
        "pityGroupId": stable_id,
        "pityRuleId": stable_id,
        "pullCount": non_negative,
        "lastUpdatedContentVersion": content_version,
    }
)
defs["FeaturedGuarantee"] = obj(
    {
        "pityGroupId": stable_id,
        "rateUpGroupId": stable_id,
        "state": enum("NONE", "NEXT_S_OR_SS_FEATURED"),
        "lastUpdatedContentVersion": content_version,
    }
)
defs["PendingRecruitmentRequest"] = obj(
    {
        "operationId": uuid_v7,
        "poolId": stable_id,
        "paymentType": enum("KINGDOM_GOLD", "TICKET", "FREE_PREMIUM", "PAID_PREMIUM"),
        "count": {"type": "integer", "enum": [1, 10]},
        "requestHash": hash256,
        "status": enum("PREPARED", "SENT", "RECEIVED"),
        "createdAtUtc": utc,
        "serverReceiptId": nullable({"type": "string", "minLength": 1, "maxLength": 128}),
    }
)
defs["RecruitmentMockState"] = obj(
    {
        "authority": enum("MOCK_ONLY", "SERVER_CACHE"),
        "serverRevision": nullable(non_negative),
        "premiumWalletCache": ref("PremiumWalletCache"),
        "pityCounters": array(ref("PityCounter")),
        "featuredGuarantees": array(ref("FeaturedGuarantee")),
        "pendingRequests": array(ref("PendingRecruitmentRequest")),
    }
)
defs["Tutorial"] = obj(
    {
        "currentStepId": nullable(stable_id),
        "completedStepIds": array(stable_id, unique=True),
        "grantedRewardIds": array(stable_id, unique=True),
        "skipped": {"type": "boolean"},
        "completedAtUtc": nullable(utc),
    }
)
defs["OfflineSettlementLine"] = obj(
    {
        "lineNo": positive,
        "settlementType": enum(
            "HUNT",
            "FACILITY",
            "NPC_PROFICIENCY",
            "POTION_CONSUMPTION",
            "INJURY_RECOVERY",
            "PROMOTION_REVIEW",
        ),
        "subjectType": enum("MERCENARY", "FACILITY", "MANAGEMENT_NPC", "PROMOTION"),
        "subjectId": {"type": "string", "minLength": 1, "maxLength": 64},
        "elapsedSeconds": non_negative,
        "quantityDelta": safe_int,
        "rewards": array(ref("RewardSnapshot")),
        "consumptions": array(ref("AssetAmount")),
    }
)
operation_status = enum("PREPARED", "APPLYING", "COMMITTED", "ACKNOWLEDGED", "FAILED_PERMANENT")
defs["OfflinePendingSettlement"] = obj(
    {
        "operationId": uuid_v7,
        "status": operation_status,
        "fromUtc": utc,
        "toUtc": utc,
        "cursorAfterUtc": utc,
        "eligibleSeconds": non_negative,
        "contentVersion": content_version,
        "ruleSnapshotVersion": content_version,
        "lines": array(ref("OfflineSettlementLine")),
        "attemptCount": integer(0, 100),
        "lastAttemptAtUtc": nullable(utc),
        "serverReceiptId": nullable({"type": "string", "minLength": 1, "maxLength": 128}),
        "errorCode": nullable(stable_id),
    }
)
defs["Offline"] = obj(
    {
        "accrualCursorUtc": utc,
        "lastTrustedUtc": utc,
        "pendingSettlement": nullable(ref("OfflinePendingSettlement")),
    }
)
defs["OperationJournalEntry"] = obj(
    {
        "operationId": uuid_v7,
        "operationType": enum(
            "OFFLINE_SETTLEMENT", "FACILITY_JOB", "PROMOTION", "RECRUITMENT", "REWARD", "SAVE_RECOVERY"
        ),
        "facilityJobType": nullable(enum("BUILD", "UPGRADE", "PRODUCTION", "CRAFT", "TREATMENT")),
        "requestHash": hash256,
        "status": operation_status,
        "createdAtUtc": utc,
        "updatedAtUtc": utc,
        "completedAtUtc": nullable(utc),
        "serverReceiptId": nullable({"type": "string", "minLength": 1, "maxLength": 128}),
        "errorCode": nullable(stable_id),
        "resultDigest": nullable(hash256),
        "failureResolution": nullable(enum("DISCARDED_BY_USER")),
        "resolvedAtUtc": nullable(utc),
    }
)
defs["Settings"] = obj(
    {
        "masterVolume": {"type": "number", "minimum": 0, "maximum": 1},
        "musicVolume": {"type": "number", "minimum": 0, "maximum": 1},
        "sfxVolume": {"type": "number", "minimum": 0, "maximum": 1},
        "vibrationEnabled": {"type": "boolean"},
        "battleSpeed": integer(1, SAFE_MAX),
    }
)
defs["RegionAccessPolicy"] = obj({"regionId": stable_id, "allowed": {"type": "boolean"}})
defs["InventoryPolicies"] = obj(
    {"autoSellMaxTier": integer(0, 5), "protectQualityIds": array(stable_id, unique=True)}
)
defs["Profile"] = obj(
    {
        "nickname": {"type": "string", "minLength": 1, "maxLength": 20},
        "locale": {"type": "string", "minLength": 2, "maxLength": 16},
        "playerExp": non_negative,
        "progressionFlagIds": array(stable_id, unique=True),
    }
)
defs["Kingdom"] = obj(
    {
        "kingdomStageId": stable_id,
        "kingdomGold": non_negative,
        "kingdomExp": non_negative,
        "activeMercenaryLimit": {"type": "integer", "enum": [4, 8, 12, 16]},
        "ownedMercenaryLimit": integer(8, 24),
        "pricingPolicy": enum("LOW", "STANDARD", "HIGH"),
        "regionAccessPolicies": array(ref("RegionAccessPolicy")),
        "inventoryPolicies": ref("InventoryPolicies"),
        "progressionFlagIds": array(stable_id, unique=True),
        "facilityUpgradeCount": non_negative,
    }
)
defs["Payload"] = obj(
    {
        "profile": ref("Profile"),
        "kingdom": ref("Kingdom"),
        "mercenaries": array(ref("Mercenary")),
        "managementNpcs": array(ref("ManagementNpc")),
        "facilities": array(ref("Facility"), minimum=8, maximum=8),
        "inventory": ref("Inventory"),
        "regions": ref("Regions"),
        "recruitmentMockState": ref("RecruitmentMockState"),
        "tutorial": ref("Tutorial"),
        "offline": ref("Offline"),
        "operationJournal": array(ref("OperationJournalEntry")),
        "settings": ref("Settings"),
        "extensions": {"type": "object", "maxProperties": 0, "additionalProperties": False},
    }
)
defs["Integrity"] = obj(
    {
        "integrityVersion": {"const": 1},
        "algorithm": {"const": "SHA-256"},
        "canonicalization": {"const": "RFC8785"},
        "payloadSha256": hash256,
        "fileSha256": hash256,
    }
)

schema = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "$id": "urn:tycoon:schema:save:v1",
    "title": "Kingdom Tycoon Save v1",
    **obj(
        {
            "schemaId": {"const": "urn:tycoon:save:v1"},
            "saveVersion": {"const": 1},
            "gameVersion": {"type": "string", "format": "semver"},
            "contentVersion": content_version,
            "saveId": uuid_v7,
            "profileId": uuid_v7,
            "revision": non_negative,
            "createdAtUtc": utc,
            "savedAtUtc": utc,
            "integrity": ref("Integrity"),
            "payload": ref("Payload"),
        }
    ),
    "$defs": defs,
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="fail when generated schemas are stale")
    args = parser.parse_args()
    rendered = json.dumps(schema, ensure_ascii=False, indent=2) + "\n"
    stale = []
    for output in OUTPUTS:
        if args.check:
            if not output.exists() or output.read_text(encoding="utf-8") != rendered:
                stale.append(output.relative_to(ROOT))
            continue
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")
        print(f"generated {output.relative_to(ROOT)} with {len(defs)} definitions")
    if stale:
        print("stale generated save schemas:")
        for output in stale:
            print(f"- {output}")
        raise SystemExit(1)
    if args.check:
        print(f"save schema generation check passed ({len(defs)} definitions, {len(OUTPUTS)} outputs)")


if __name__ == "__main__":
    main()
