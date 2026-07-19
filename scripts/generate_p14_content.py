#!/usr/bin/env python3
"""Build and verify P14 raid content and Save contracts."""

from __future__ import annotations

import argparse
import copy
import csv
import io
import json
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "TYCOON_P14_RAIDS_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.12"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.11.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.12.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p13-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p14-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P14"

EXPECTED_DOCUMENT_SHA256 = "3a3a5faed1999eb6f3253eac6d589cbcdf60f1a207ed815445582168f6607ec8"
EXPECTED_SCHEMA_SHA256 = "bf02726a377ddb95940a5e8e33aa8f8d7a174a3e8a5bfc4de445c1bdb442e76b"
EXPECTED_TEMPLATE_SHA256 = "40a356b4b854320527ec3dee744b17ebd4f828581416512a49386e123267ddc4"
GENERATED_AT = "2026-07-19T15:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p13_content as P13  # noqa: E402

ContractError = P13.ContractError
Package = P13.Package
P12 = P13.P12
P03 = P13.P03


UNLOCK_ROWS = [
    ["RAID_HYDRA", "NORMAL", "KINGDOM_4", "REGION_R04", "100", "", "0", "", "CONFIRMED", "TRUE"],
    ["RAID_HYDRA", "HARD", "KINGDOM_4", "REGION_R04", "100", "NORMAL", "1", "", "TUNABLE", "TRUE"],
    ["RAID_HYDRA", "CORRUPTED", "KINGDOM_5", "REGION_R04", "100", "HARD", "3", "FLAG_RAID_DRAGON_VARIANTS", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "NORMAL", "KINGDOM_5", "REGION_R05", "100", "", "0", "", "CONFIRMED", "TRUE"],
    ["RAID_DRAGON", "HARD", "KINGDOM_5", "REGION_R05", "100", "NORMAL", "1", "", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "CORRUPTED", "KINGDOM_5", "REGION_R05", "100", "HARD", "3", "FLAG_RAID_DRAGON_VARIANTS", "TUNABLE", "TRUE"],
]

ROLE_ROWS = [
    [raid, str(number), selector, "1", "WARNING", message, "TUNABLE", "TRUE"]
    for raid in ("RAID_HYDRA", "RAID_DRAGON")
    for number, selector, message in (
        (1, "TANK", "TXT_P14_WARNING_TANK"),
        (2, "HEALER", "TXT_P14_WARNING_HEALER"),
        (3, "RANGED_DPS_OR_AOE_DPS", "TXT_P14_WARNING_RANGED"),
    )
]

PART_TARGET_ROWS = [
    ["RAID_HYDRA", "HYDRA_HEAD", "", "10000", "6500", "1", "TUNABLE", "TRUE"],
    ["RAID_HYDRA", "HYDRA_BODY", "", "10000", "8000", "2", "TUNABLE", "TRUE"],
    ["RAID_HYDRA", "HYDRA_HEART", "HYDRA_BODY", "5000", "10000", "3", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "DRAGON_HORN", "", "10000", "6500", "1", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "DRAGON_WING", "", "10000", "7000", "2", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "DRAGON_BODY", "", "10000", "8500", "3", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "DRAGON_HEART", "DRAGON_BODY", "4000", "10000", "4", "TUNABLE", "TRUE"],
]

PHASE_ROWS = [
    ["RAID_HYDRA", "1", "10000", "VENOM_BITE", "20", "10000", "STATUS_POISON", "TUNABLE", "TRUE"],
    ["RAID_HYDRA", "2", "7000", "POISON_BREATH", "25", "11500", "STATUS_POISON", "TUNABLE", "TRUE"],
    ["RAID_HYDRA", "3", "3500", "MULTIHEAD_ENRAGE", "15", "13500", "STATUS_POISON", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "1", "10000", "ASH_CLAW", "20", "10000", "STATUS_BURN", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "2", "7500", "FLYING_CHARGE", "30", "12500", "STATUS_BURN", "TUNABLE", "TRUE"],
    ["RAID_DRAGON", "3", "4000", "ASH_STORM", "18", "14500", "STATUS_BURN", "TUNABLE", "TRUE"],
]

TABLES: dict[str, tuple[list[str], list[list[str]], list[str], set[str]]] = {
    "raid_unlock_rules.csv": (
        ["raid_id", "difficulty", "kingdom_stage_id", "region_id", "region_progress_required", "previous_difficulty", "previous_clear_required", "progression_flag_id", "status", "enabled"],
        UNLOCK_ROWS,
        ["raid_id", "difficulty"],
        {"kingdom_stage_id", "region_id", "previous_difficulty", "progression_flag_id"},
    ),
    "raid_role_requirements.csv": (
        ["raid_id", "requirement_no", "role_selector", "minimum_count", "severity", "message_text_key", "status", "enabled"],
        ROLE_ROWS,
        ["raid_id", "requirement_no"],
        set(),
    ),
    "raid_part_target_rules.csv": (
        ["raid_id", "part_id", "prerequisite_part_id", "expose_boss_hp_bps", "boss_damage_share_bps", "target_order", "status", "enabled"],
        PART_TARGET_ROWS,
        ["raid_id", "part_id"],
        {"prerequisite_part_id"},
    ),
    "raid_boss_phase_rules.csv": (
        ["raid_id", "phase_no", "trigger_boss_hp_bps", "ability_tag", "action_interval_ticks", "attack_multiplier_bps", "status_effect_id", "status", "enabled"],
        PHASE_ROWS,
        ["raid_id", "phase_no"],
        {"status_effect_id"},
    ),
}


def csv_bytes(header: list[str], rows: list[list[str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.writer(stream, lineterminator="\r\n")
    writer.writerow(header)
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def parse_csv(raw: bytes) -> tuple[list[str], list[list[str]]]:
    rows = list(csv.reader(io.StringIO(raw.decode("utf-8-sig"), newline="")))
    if not rows:
        raise ContractError("P14 source CSV is empty")
    return rows[0], rows[1:]


def field(name: str, nullable: bool) -> dict[str, Any]:
    integers = {
        "requirement_no", "minimum_count", "region_progress_required", "previous_clear_required",
        "expose_boss_hp_bps", "boss_damage_share_bps", "target_order", "phase_no",
        "trigger_boss_hp_bps", "action_interval_ticks", "attack_multiplier_bps",
    }
    enums = {
        "difficulty": ["NORMAL", "HARD", "CORRUPTED"],
        "previous_difficulty": ["NORMAL", "HARD", "CORRUPTED"],
        "severity": ["WARNING"],
    }
    if name == "enabled":
        domain, values = "BOOL", []
    elif name == "status":
        domain, values = "STATUS", []
    elif name in integers:
        domain, values = "INT32", []
    elif name in enums:
        domain, values = "ENUM", enums[name]
    elif name in {"role_selector", "ability_tag"}:
        domain, values = "STRING", []
    else:
        domain, values = "STABLE_ID", []
    return {"domain": domain, "enumValues": values, "name": name, "nullable": nullable}


def table_entry(name: str, raw: bytes, header: list[str], rows: list[list[str]], primary_key: list[str], nullable: set[str]) -> dict[str, Any]:
    targets = {
        "raid_id": ("raids.csv", "raid_id"),
        "kingdom_stage_id": ("kingdom_stages.csv", "stage_id"),
        "region_id": ("regions.csv", "region_id"),
        "progression_flag_id": ("progression_flags.csv", "progression_flag_id"),
        "part_id": ("raid_parts.csv", "part_id"),
        "prerequisite_part_id": ("raid_parts.csv", "part_id"),
        "status_effect_id": ("status_effects.csv", "status_effect_id"),
        "message_text_key": ("localizations.csv", "text_key"),
    }
    foreign_keys = [
        {"mode": "HARD", "sourceFields": [source], "targetFields": [target], "targetFile": target_file}
        for source, (target_file, target) in targets.items() if source in header
    ]
    return {
        "fields": [field(value, value in nullable) for value in header],
        "file": name,
        "foreignKeys": foreign_keys,
        "primaryKey": primary_key,
        "required": True,
        "rowCount": len(rows),
        "schemaVersion": 1,
        "sha256": P12.sha(raw),
    }


def update_table(name: str, raw: bytes, table_map: dict[str, dict[str, Any]], data: dict[str, bytes]) -> None:
    _, rows = parse_csv(raw)
    entry = copy.deepcopy(table_map[name])
    entry.update({"rowCount": len(rows), "sha256": P12.sha(raw)})
    data[name] = raw
    table_map[name] = entry


def add_localizations(data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> None:
    header, rows = parse_csv(data["localizations.csv"])
    values = {
        "TXT_P14_WARNING_TANK": ("Tank role is recommended.", "방어 역할 용병을 권장합니다."),
        "TXT_P14_WARNING_HEALER": ("Healer role is recommended.", "치유 역할 용병을 권장합니다."),
        "TXT_P14_WARNING_RANGED": ("Ranged damage role is recommended.", "원거리 공격 역할 용병을 권장합니다."),
        "TXT_P14_SIMULATION_TICK_HZ": ("P14 raid simulation ticks", "P14 레이드 시뮬레이션 틱"),
        "TXT_P14_TRACE_INTERVAL_TICKS": ("P14 raid trace interval", "P14 레이드 trace 간격"),
        "TXT_P14_HISTORY_MAX_ENTRIES": ("P14 raid history limit", "P14 레이드 이력 상한"),
        "TXT_P14_EVENT_MAX_ENTRIES": ("P14 raid event limit", "P14 레이드 사건 상한"),
        "TXT_P14_SUCCESS_INJURY_SECONDS": ("P14 success injury duration", "P14 성공 부상 시간"),
        "TXT_P14_FAILURE_INJURY_SECONDS": ("P14 failure injury duration", "P14 실패 부상 시간"),
        "TXT_P14_MAX_TRACE_FRAMES": ("P14 raid trace frame limit", "P14 레이드 trace 프레임 상한"),
    }
    existing = {(row[0], row[1]) for row in rows}
    for key, (english, korean) in values.items():
        if ("en-US", key) not in existing:
            rows.append(["en-US", key, english, "P14_RAID", "CONFIRMED", "TRUE"])
        if ("ko-KR", key) not in existing:
            rows.append(["ko-KR", key, korean, "P14_RAID", "CONFIRMED", "TRUE"])
    rows.sort(key=lambda value: (value[0], value[1]))
    update_table("localizations.csv", csv_bytes(header, rows), table_map, data)


def add_runtime_config(data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> None:
    header, rows = parse_csv(data["runtime_config.csv"])
    rows.extend([
        ["P14_SIMULATION_TICK_HZ", "INTEGER", "10", "HZ", "5", "30", "TXT_P14_SIMULATION_TICK_HZ", "CONFIRMED", "TRUE"],
        ["P14_TRACE_INTERVAL_TICKS", "INTEGER", "10", "TICKS", "1", "100", "TXT_P14_TRACE_INTERVAL_TICKS", "TUNABLE", "TRUE"],
        ["P14_HISTORY_MAX_ENTRIES", "INTEGER", "50", "COUNT", "10", "100", "TXT_P14_HISTORY_MAX_ENTRIES", "TUNABLE", "TRUE"],
        ["P14_EVENT_MAX_ENTRIES", "INTEGER", "200", "COUNT", "50", "500", "TXT_P14_EVENT_MAX_ENTRIES", "TUNABLE", "TRUE"],
        ["P14_SUCCESS_INJURY_SECONDS", "INTEGER", "600", "SECONDS", "0", "86400", "TXT_P14_SUCCESS_INJURY_SECONDS", "TUNABLE", "TRUE"],
        ["P14_FAILURE_INJURY_SECONDS", "INTEGER", "1800", "SECONDS", "0", "86400", "TXT_P14_FAILURE_INJURY_SECONDS", "TUNABLE", "TRUE"],
        ["P14_MAX_TRACE_FRAMES", "INTEGER", "360", "COUNT", "60", "600", "TXT_P14_MAX_TRACE_FRAMES", "CONFIRMED", "TRUE"],
    ])
    rows.sort(key=lambda value: value[0])
    update_table("runtime_config.csv", csv_bytes(header, rows), table_map, data)


def fill_effect_values(data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> None:
    header, rows = parse_csv(data["raid_part_effects.csv"])
    values = {
        "POISON_BREATH_REDUCED": "7000", "DEFENSE_DOWN": "8000", "ENRAGE": "12500",
        "CHARGE_REDUCED": "7000", "FLIGHT_DISABLED": "0", "FINAL_ENRAGE": "13500",
    }
    value_index = header.index("value")
    for row in rows:
        row[value_index] = values[row[0]]
    update_table("raid_part_effects.csv", csv_bytes(header, rows), table_map, data)


def validate_raids(data: dict[str, bytes]) -> None:
    parsed: dict[str, list[dict[str, str]]] = {}
    for name in ("raids.csv", "raid_difficulties.csv", "raid_parts.csv", *TABLES.keys()):
        header, rows = parse_csv(data[name])
        parsed[name] = [dict(zip(header, row, strict=True)) for row in rows]
    if len(parsed["raids.csv"]) != 2 or len(parsed["raid_difficulties.csv"]) != 6:
        raise ContractError("P14 must contain two raids and six difficulties")
    if len(parsed["raid_parts.csv"]) != 7 or len(parsed["raid_boss_phase_rules.csv"]) != 6:
        raise ContractError("P14 raid part/phase coverage invalid")
    for raid_id in ("RAID_HYDRA", "RAID_DRAGON"):
        difficulties = [row["difficulty"] for row in parsed["raid_unlock_rules.csv"] if row["raid_id"] == raid_id]
        if difficulties != ["NORMAL", "HARD", "CORRUPTED"]:
            raise ContractError(f"P14 difficulty order invalid: {raid_id}")
        phases = [int(row["trigger_boss_hp_bps"]) for row in parsed["raid_boss_phase_rules.csv"] if row["raid_id"] == raid_id]
        if phases != sorted(phases, reverse=True):
            raise ContractError(f"P14 phase thresholds must descend: {raid_id}")
    part_ids = {row["part_id"] for row in parsed["raid_parts.csv"]}
    for row in parsed["raid_part_target_rules.csv"]:
        if row["part_id"] not in part_ids or (row["prerequisite_part_id"] and row["prerequisite_part_id"] not in part_ids):
            raise ContractError("P14 part target reference invalid")


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.11"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({
        "contentVersion": "1.0.0-content.12",
        "csvSchemaSetVersion": 10,
        "minimumGameVersion": "1.0.0-p14",
        "generatedAtUtc": GENERATED_AT,
    })
    table_map = {value["file"]: value for value in manifest["tables"]}
    add_localizations(data, table_map)
    add_runtime_config(data, table_map)
    fill_effect_values(data, table_map)
    for name, (header, rows, primary_key, nullable) in TABLES.items():
        raw = csv_bytes(header, rows)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, rows, primary_key, nullable)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 95:
        raise ContractError("P14 package must contain 95 tables")
    validate_raids(data)
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P14 schema input drifted from content.11")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.12"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.12"
    defs = schema["$defs"]
    stable_id = {"maxLength": 64, "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", "type": "string"}
    safe = {"maximum": 9007199254740991, "minimum": 0, "type": "integer"}
    positive = {"maximum": 9007199254740991, "minimum": 1, "type": "integer"}
    nullable_utc = {"oneOf": [{"format": "utc-instant", "type": "string"}, {"type": "null"}]}
    defs["RaidPotionConsumption"] = object_schema(
        ["mercenaryInstanceId", "potionId", "quantity"],
        {"mercenaryInstanceId": {"format": "uuid", "type": "string"}, "potionId": stable_id, "quantity": positive},
    )
    defs["RaidContribution"] = object_schema(
        ["mercenaryInstanceId", "damage", "healing", "preventedDamage", "total"],
        {"mercenaryInstanceId": {"format": "uuid", "type": "string"}, "damage": safe, "healing": safe, "preventedDamage": safe, "total": safe},
    )
    defs["RaidReward"] = object_schema(
        ["rewardType", "rewardId", "quantity"],
        {"rewardType": {"enum": ["ITEM", "REGION_UNLOCK", "PROGRESSION_FLAG"], "type": "string"}, "rewardId": stable_id, "quantity": positive},
    )
    defs["RaidHistoryEntry"] = object_schema(
        ["sequence", "operationId", "raidId", "difficulty", "resultCode", "success", "durationMs", "targetPartId",
         "partyMercenaryInstanceIds", "brokenPartIds", "injuredMercenaryInstanceIds", "potionConsumptions", "contributions",
         "rewards", "firstClear", "unlockedRegionIds", "progressionFlagIds", "resultDigest", "settledAtUtc"],
        {
            "sequence": positive, "operationId": {"format": "uuid-v7", "type": "string"}, "raidId": stable_id,
            "difficulty": {"enum": ["NORMAL", "HARD", "CORRUPTED"], "type": "string"}, "resultCode": stable_id,
            "success": {"type": "boolean"}, "durationMs": safe, "targetPartId": stable_id,
            "partyMercenaryInstanceIds": {"items": {"format": "uuid", "type": "string"}, "maxItems": 8, "minItems": 6, "type": "array", "uniqueItems": True},
            "brokenPartIds": {"items": stable_id, "maxItems": 4, "type": "array", "uniqueItems": True},
            "injuredMercenaryInstanceIds": {"items": {"format": "uuid", "type": "string"}, "maxItems": 8, "type": "array", "uniqueItems": True},
            "potionConsumptions": {"items": {"$ref": "#/$defs/RaidPotionConsumption"}, "maxItems": 32, "type": "array"},
            "contributions": {"items": {"$ref": "#/$defs/RaidContribution"}, "maxItems": 8, "type": "array"},
            "rewards": {"items": {"$ref": "#/$defs/RaidReward"}, "maxItems": 16, "type": "array"},
            "firstClear": {"type": "boolean"},
            "unlockedRegionIds": {"items": stable_id, "maxItems": 5, "type": "array", "uniqueItems": True},
            "progressionFlagIds": {"items": stable_id, "maxItems": 8, "type": "array", "uniqueItems": True},
            "resultDigest": {"pattern": "^[0-9a-f]{64}$", "type": "string"},
            "settledAtUtc": {"format": "utc-instant", "type": "string"},
        },
    )
    progress = defs["RaidProgress"]
    for name in ("attemptCount", "lastResultCode"):
        if name not in progress["required"]:
            progress["required"].append(name)
    progress["properties"]["attemptCount"] = safe
    progress["properties"]["lastResultCode"] = {"oneOf": [stable_id, {"type": "null"}]}
    regions = defs["Regions"]
    regions["properties"]["regionVersion"]["const"] = 2
    for name in ("raidVersion", "nextRaidHistorySequence", "raidHistory"):
        if name not in regions["required"]:
            regions["required"].append(name)
    regions["properties"].update({
        "raidVersion": {"const": 1, "type": "integer"},
        "nextRaidHistorySequence": positive,
        "raidHistory": {"items": {"$ref": "#/$defs/RaidHistoryEntry"}, "maxItems": 50, "type": "array"},
    })
    operation_enum = defs["OperationJournalEntry"]["properties"]["operationType"]["enum"]
    if "RAID_RESOLVE" not in operation_enum:
        operation_enum.append("RAID_RESOLVE")
    return P12.pretty(schema)


def rank_order(rank_id: str) -> int:
    return {"RANK_APPRENTICE": 1, "RANK_REGULAR": 2, "RANK_SKILLED": 3, "RANK_ELITE": 4, "RANK_HERO": 5, "RANK_LEGEND": 6}.get(rank_id, 0)


def stage_order(stage_id: str) -> int:
    return {f"KINGDOM_{value}": value for value in range(1, 6)}.get(stage_id, 0)


def raid_progress_rows(source: dict[str, Any]) -> list[dict[str, Any]]:
    regions = source["payload"]["regions"]
    existing = {(row["raidId"], row["difficulty"]): row for row in regions.get("raids", [])}
    progress = {row["regionId"]: row for row in regions["progress"]}
    flags = set(source["payload"]["kingdom"]["progressionFlagIds"])
    stage = stage_order(source["payload"]["kingdom"]["kingdomStageId"])
    parts = {
        "RAID_HYDRA": ["HYDRA_HEAD", "HYDRA_BODY", "HYDRA_HEART"],
        "RAID_DRAGON": ["DRAGON_HORN", "DRAGON_WING", "DRAGON_BODY", "DRAGON_HEART"],
    }
    result: list[dict[str, Any]] = []
    for raid_id, region_id, required_stage in (("RAID_HYDRA", "REGION_R04", 4), ("RAID_DRAGON", "REGION_R05", 5)):
        previous_clear = 0
        for difficulty in ("NORMAL", "HARD", "CORRUPTED"):
            old = copy.deepcopy(existing.get((raid_id, difficulty), {}))
            unlocked = stage >= required_stage and progress[region_id]["progressPercent"] >= 100
            if difficulty == "HARD":
                unlocked = unlocked and previous_clear >= 1
            elif difficulty == "CORRUPTED":
                unlocked = unlocked and previous_clear >= 3 and "FLAG_RAID_DRAGON_VARIANTS" in flags
            part_map = {value["partId"]: value for value in old.get("partStates", [])}
            row = {
                "raidId": raid_id,
                "difficulty": difficulty,
                "unlocked": bool(old.get("unlocked", unlocked) or unlocked),
                "attemptCount": int(old.get("attemptCount", old.get("clearCount", 0))),
                "clearCount": int(old.get("clearCount", 0)),
                "bestClearTimeMs": old.get("bestClearTimeMs"),
                "firstClearedAtUtc": old.get("firstClearedAtUtc"),
                "lastClearedAtUtc": old.get("lastClearedAtUtc"),
                "firstClearRewardOperationId": old.get("firstClearRewardOperationId"),
                "lastResultCode": old.get("lastResultCode"),
                "partStates": [],
            }
            for part_id in parts[raid_id]:
                part = copy.deepcopy(part_map.get(part_id, {}))
                row["partStates"].append({
                    "partId": part_id,
                    "breakCount": int(part.get("breakCount", 0)),
                    "exposedCount": int(part.get("exposedCount", 0)),
                    "bestBreakTimeMs": part.get("bestBreakTimeMs"),
                    "lastRewardOperationId": part.get("lastRewardOperationId"),
                })
            result.append(row)
            previous_clear = row["clearCount"]
    return result


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    regions = result["payload"]["regions"]
    regions["regionVersion"] = 2
    regions["raidVersion"] = 1
    regions["nextRaidHistorySequence"] = int(regions.get("nextRaidHistorySequence", 1))
    regions["raidHistory"] = copy.deepcopy(regions.get("raidHistory", []))
    regions["raids"] = raid_progress_rows(result)
    result["gameVersion"] = "1.0.0-p14"
    result["contentVersion"] = "1.0.0-content.12"
    P12.P11.seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P14 template input drifted from P13")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001401", "profileId": "018f0000-0000-7000-8000-000000001402", "revision": 0})
    P12.P11.seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.12" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.12"], "schemaFile": "save.content.12.schema.json", "sha256": P12.sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {
        TEMPLATE_OUTPUT: P12.pretty(new_game),
        GOLDEN_ROOT / "p14-new-game.golden.json": P12.pretty(new_game),
        GOLDEN_ROOT / "p14-migration-before.golden.json": P12.pretty(before),
        GOLDEN_ROOT / "p14-migration-after.golden.json": P12.pretty(after),
    }
    return P12.pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if P12.sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P14 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY = YES" not in text or "| 미해결 항목 | 없음 |" not in text:
        raise ContractError("P14 contract is not implementation-ready")
    package = build_package()
    schema_bytes = build_schema()
    registry_bytes, extras = build_extras(schema_bytes)
    return package, schema_bytes, registry_bytes, extras


def generate(package: Package, schema_bytes: bytes, registry_bytes: bytes, extras: dict[Path, bytes], check: bool) -> None:
    files: dict[Path, bytes] = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": P12.pretty(package.schema),
        SCHEMA_OUTPUT: schema_bytes,
        REGISTRY_OUTPUT: registry_bytes,
    }
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    files.update(extras)
    failures: list[str] = []
    for path, contents in files.items():
        if check:
            if not path.is_file():
                failures.append(f"missing: {path.relative_to(ROOT)}")
            elif path.read_bytes() != contents:
                failures.append(f"drift: {path.relative_to(ROOT)}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)
    if failures:
        raise ContractError("P14 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p14-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p14-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {P12.sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {P12.sha(schema_bytes)}")
    print(f"package-fingerprint: {P12.sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
