#!/usr/bin/env python3
"""Build and verify P12 region-unlock content and Save contracts."""

from __future__ import annotations

import argparse
import copy
import csv
import hashlib
import io
import json
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "TYCOON_P12_REGIONS_UNLOCKS_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.10"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.9.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.10.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p11-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p12-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P12"

EXPECTED_DOCUMENT_SHA256 = "30ac32798e6533c4a8632a0c2a7e2b944752cbe6eecff338c322d5baf663d72d"
EXPECTED_SCHEMA_SHA256 = "7eb2ee415a4500ecfb6985ea388405050110cac06431e748e45b661730ccd1e2"
EXPECTED_TEMPLATE_SHA256 = "3d06b5d98581917318c1be349295b8256be262a00d6f3914e6e80cf6a63e7a0c"
GENERATED_AT = "2026-07-19T00:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p11_content as P11  # noqa: E402

ContractError = P11.ContractError
Package = P11.Package
P03 = P11.P03


UNLOCK_ROWS = [
    ["REGION_R01", "", "0", "KINGDOM_1", "", "0", "", "0", "", "0", "CONFIRMED", "TRUE"],
    ["REGION_R02", "REGION_R01", "100", "KINGDOM_2", "", "0", "", "0", "", "0", "CONFIRMED", "TRUE"],
    ["REGION_R03", "REGION_R02", "100", "KINGDOM_3", "REGION_R02", "1", "", "0", "", "0", "CONFIRMED", "TRUE"],
    ["REGION_R04", "REGION_R03", "100", "KINGDOM_3", "REGION_R03", "1", "FAC_ALCHEMY", "2", "", "0", "CONFIRMED", "TRUE"],
    ["REGION_R05", "REGION_R04", "100", "KINGDOM_4", "", "0", "", "0", "RAID_HYDRA", "1", "CONFIRMED", "TRUE"],
]

ACCESS_ROWS = [[f"REGION_R0{order}", "1", "4", "TRUE", "TRUE", "TRUE", "TUNABLE", "TRUE"] for order in range(1, 6)]

PROGRESS_ROWS = [
    ["REGION_R01", "20", "25", "100", "100", "TUNABLE", "TRUE"],
    ["REGION_R02", "12", "20", "100", "100", "TUNABLE", "TRUE"],
    ["REGION_R03", "10", "20", "100", "100", "TUNABLE", "TRUE"],
    ["REGION_R04", "8", "18", "100", "100", "TUNABLE", "TRUE"],
    ["REGION_R05", "6", "15", "100", "100", "TUNABLE", "TRUE"],
]

HUNT_ROWS = [
    ["REGION_R01", "2", "2", "8000", "4", "TUNABLE", "TRUE"],
    ["REGION_R02", "3", "3", "8000", "5", "TUNABLE", "TRUE"],
    ["REGION_R03", "4", "4", "8000", "6", "TUNABLE", "TRUE"],
    ["REGION_R04", "4", "5", "8000", "7", "TUNABLE", "TRUE"],
    ["REGION_R05", "4", "6", "8000", "8", "TUNABLE", "TRUE"],
]

ENCOUNTERS = {
    "REGION_R01": ["MON_R01_SLIME", "MON_R01_WOLF", "MON_R01_BOAR", "MON_R01_GOBLIN", "MON_R01_ELITE_DIRE_WOLF"],
    "REGION_R02": ["MON_R02_SPIDER", "MON_R02_TREANT", "MON_R02_SHAMAN", "MON_R02_LIZARD", "MON_R02_ELITE_OGRE"],
    "REGION_R03": ["MON_R03_BAT", "MON_R03_CRAWLER", "MON_R03_GOLEM", "MON_R03_MINER", "MON_R03_ELITE_GUARDIAN"],
    "REGION_R04": ["MON_R04_FROG", "MON_R04_WITCH", "MON_R04_PLAGUE_BEAST", "MON_R04_TROLL", "MON_R04_ELITE_HYDRA_SPAWN"],
    "REGION_R05": ["MON_R05_ICE_WOLF", "MON_R05_WRAITH", "MON_R05_YETI", "MON_R05_RUNE_SENTINEL", "MON_R05_ELITE_FROST_GIANT"],
}

TABLES: dict[str, tuple[list[str], list[list[str]], list[str]]] = {
    "region_unlock_rules.csv": (
        ["region_id", "previous_region_id", "previous_progress_required", "kingdom_stage_id", "elite_source_region_id", "elite_kill_required", "facility_id", "facility_level_required", "raid_id", "raid_clear_required", "status", "enabled"],
        UNLOCK_ROWS, ["region_id"]),
    "region_access_policy_rules.csv": (
        ["region_id", "party_min", "party_max", "default_allowed_on_unlock", "manual_toggle_allowed", "require_town_safe", "status", "enabled"],
        ACCESS_ROWS, ["region_id"]),
    "region_progress_rules.csv": (
        ["region_id", "victory_progress", "elite_bonus_progress", "progress_cap", "unlock_next_at_percent", "status", "enabled"],
        PROGRESS_ROWS, ["region_id"]),
    "region_hunt_policy_rules.csv": (
        ["region_id", "recommended_party_size", "recommended_potion_quantity", "danger_power_ratio_bps", "inventory_reserve_slots", "status", "enabled"],
        HUNT_ROWS, ["region_id"]),
}


def pretty(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def csv_bytes(header: list[str], rows: list[list[str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.writer(stream, lineterminator="\r\n")
    writer.writerow(header)
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def field(name: str) -> dict[str, Any]:
    bools = {"default_allowed_on_unlock", "manual_toggle_allowed", "require_town_safe", "enabled"}
    safe = {"elite_kill_required", "raid_clear_required"}
    integers = {"previous_progress_required", "facility_level_required", "party_min", "party_max",
                "victory_progress", "elite_bonus_progress", "progress_cap", "unlock_next_at_percent",
                "recommended_party_size", "recommended_potion_quantity", "danger_power_ratio_bps", "inventory_reserve_slots"}
    nullable = name in {"previous_region_id", "elite_source_region_id", "facility_id", "raid_id"}
    domain = "BOOL" if name in bools else "SAFE_INT" if name in safe else "INT32" if name in integers else "STATUS" if name == "status" else "STABLE_ID"
    return {"domain": domain, "enumValues": [], "name": name, "nullable": nullable}


def table_entry(name: str, raw: bytes, header: list[str], rows: list[list[str]], primary_key: list[str]) -> dict[str, Any]:
    targets = {
        "region_id": ("regions.csv", "region_id"),
        "previous_region_id": ("regions.csv", "region_id"),
        "elite_source_region_id": ("regions.csv", "region_id"),
        "kingdom_stage_id": ("kingdom_stages.csv", "stage_id"),
        "facility_id": ("facilities.csv", "facility_id"),
        "raid_id": ("raids.csv", "raid_id"),
    }
    foreign_keys = [{"mode": "HARD", "sourceFields": [source], "targetFields": [target], "targetFile": file}
                    for source, (file, target) in targets.items() if source in header]
    return {"fields": [field(value) for value in header], "file": name, "foreignKeys": foreign_keys,
            "primaryKey": primary_key, "required": True, "rowCount": len(rows), "schemaVersion": 1, "sha256": sha(raw)}


def encounter_bytes() -> bytes:
    rows: list[list[str]] = []
    for region, monsters in ENCOUNTERS.items():
        for index, monster in enumerate(monsters):
            elite = index == 4
            rows.append([region, monster, "4" if elite else "24", "1" if elite else "2",
                         "1" if elite else "5", region.removeprefix("REGION_") + ("_ELITE" if elite else "_NORMAL"),
                         "TUNABLE", "TRUE"])
    return csv_bytes(["region_id", "monster_id", "weight", "wave_min", "wave_max", "spawn_group", "status", "enabled"], rows)


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.9"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({"contentVersion": "1.0.0-content.10", "csvSchemaSetVersion": 8,
                     "minimumGameVersion": "1.0.0-p12", "generatedAtUtc": GENERATED_AT})
    table_map = {value["file"]: value for value in manifest["tables"]}
    encounters = encounter_bytes()
    data["region_encounter_profiles.csv"] = encounters
    encounter_entry = copy.deepcopy(table_map["region_encounter_profiles.csv"])
    encounter_entry.update({"rowCount": 25, "sha256": sha(encounters)})
    table_map["region_encounter_profiles.csv"] = encounter_entry
    for name, (header, rows, primary_key) in TABLES.items():
        raw = csv_bytes(header, rows)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, rows, primary_key)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 88:
        raise ContractError("P12 package must contain 88 tables")
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P12 schema input drifted from content.9")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.10"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.10"
    defs = schema["$defs"]
    positive = {"minimum": 1, "maximum": 9007199254740991, "type": "integer"}
    nullable_progress = {"oneOf": [{"minimum": 0, "maximum": 100, "type": "integer"}, {"type": "null"}]}
    nullable_bool = {"oneOf": [{"type": "boolean"}, {"type": "null"}]}
    stable = {"minLength": 1, "maxLength": 128, "type": "string"}
    event = object_schema(
        ["sequence", "eventType", "regionId", "mercenaryInstanceIds", "progressBefore", "progressAfter", "allowedBefore", "allowedAfter", "resultCode", "createdAtUtc"],
        {"sequence": positive, "eventType": {"enum": ["RegionUnlocked", "AccessPolicyChanged", "HuntSettled"], "type": "string"},
         "regionId": stable, "mercenaryInstanceIds": {"items": {"format": "uuid", "type": "string"}, "maxItems": 8, "type": "array", "uniqueItems": True},
         "progressBefore": nullable_progress, "progressAfter": nullable_progress, "allowedBefore": nullable_bool, "allowedAfter": nullable_bool,
         "resultCode": stable, "createdAtUtc": {"format": "utc-instant", "type": "string"}},
    )
    defs["RegionEvent"] = event
    regions = defs["Regions"]
    regions["required"] = ["regionVersion", "nextEventSequence", "progress", "raids", "events"]
    regions["properties"] = {
        "regionVersion": {"const": 1, "type": "integer"},
        "nextEventSequence": positive,
        "progress": regions["properties"]["progress"],
        "raids": regions["properties"]["raids"],
        "events": {"items": {"$ref": "#/$defs/RegionEvent"}, "maxItems": 200, "type": "array"},
    }
    operation_enum = defs["OperationJournalEntry"]["properties"]["operationType"]["enum"]
    if "REGION_POLICY" not in operation_enum:
        operation_enum.append("REGION_POLICY")
    return pretty(schema)


def region_state(source: dict[str, Any]) -> dict[str, Any]:
    old = source["payload"]["regions"]
    return {"regionVersion": 1, "nextEventSequence": 1, "progress": old["progress"], "raids": old["raids"], "events": []}


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["payload"]["regions"] = region_state(result)
    existing = {value["regionId"]: bool(value["allowed"]) for value in result["payload"]["kingdom"]["regionAccessPolicies"]}
    result["payload"]["kingdom"]["regionAccessPolicies"] = [
        {"regionId": f"REGION_R0{order}", "allowed": existing.get(f"REGION_R0{order}", order == 1)} for order in range(1, 6)
    ]
    result["gameVersion"] = "1.0.0-p12"
    result["contentVersion"] = "1.0.0-content.10"
    P11.seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P12 template input drifted from P11")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001201",
                     "profileId": "018f0000-0000-7000-8000-000000001202", "revision": 0})
    P11.seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.10" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.10"],
                                "schemaFile": "save.content.10.schema.json", "sha256": sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {TEMPLATE_OUTPUT: pretty(new_game), GOLDEN_ROOT / "p12-new-game.golden.json": pretty(new_game),
             GOLDEN_ROOT / "p12-migration-before.golden.json": pretty(before),
             GOLDEN_ROOT / "p12-migration-after.golden.json": pretty(after)}
    return pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P12 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY: YES" not in text or "UNRESOLVED: NONE" not in text:
        raise ContractError("P12 contract is not implementation-ready")
    package = build_package()
    schema_bytes = build_schema()
    registry_bytes, extras = build_extras(schema_bytes)
    return package, schema_bytes, registry_bytes, extras


def generate(package: Package, schema_bytes: bytes, registry_bytes: bytes, extras: dict[Path, bytes], check: bool) -> None:
    files: dict[Path, bytes] = {OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": pretty(package.schema), SCHEMA_OUTPUT: schema_bytes,
        REGISTRY_OUTPUT: registry_bytes}
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    files.update(extras)
    failures: list[str] = []
    for path, contents in files.items():
        if check:
            if not path.is_file(): failures.append(f"missing: {path.relative_to(ROOT)}")
            elif path.read_bytes() != contents: failures.append(f"drift: {path.relative_to(ROOT)}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)
    if failures:
        raise ContractError("P12 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p12-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p12-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {sha(schema_bytes)}")
    print(f"package-fingerprint: {sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
