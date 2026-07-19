#!/usr/bin/env python3
"""Build and verify P09 production/NPC content and Save contracts."""

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
SOURCE = ROOT / "docs" / "design" / "TYCOON_P09_PRODUCTION_NPC_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.7"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.6.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.7.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p08-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p09-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P09"

EXPECTED_DOCUMENT_SHA256 = "6fdd74f95f03eb8fd481f7f26a2aaa92cb31b0c91866ba05bb5ec6b363238614"
EXPECTED_SCHEMA_SHA256 = "6a776d04768d6d92a23177efe61c9a2359760840d57caefcc57e1b08b1f31980"
EXPECTED_TEMPLATE_SHA256 = "e273ddd95c10db0f2c0aa9ebc8eb3a776994a90f76e38978619e2f00100bddd9"
GENERATED_AT = "2026-07-24T00:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p08_content as P08  # noqa: E402

ContractError = P08.ContractError
Package = P08.Package
P03 = P08.P03


TABLES: dict[str, tuple[list[str], list[list[str]], list[str], dict[str, list[str]]]] = {
    "production_facility_rules.csv": (
        ["facility_level", "queue_capacity", "speed_bps", "status", "enabled"],
        [[str(level), str(capacity), str(speed), "TUNABLE", "TRUE"] for level, capacity, speed in
         ((1, 4, 10000), (2, 6, 11250), (3, 8, 12750), (4, 10, 14500))],
        ["facility_level"],
        {},
    ),
    "production_stock_targets.csv": (
        ["target_id", "priority", "product_kind", "product_id", "recipe_id", "target_quantity",
         "min_target", "max_target", "emergency_floor", "status", "enabled"],
        [
            ["TARGET_SMALL_HEAL", "10", "POTION", "POT_HEAL_SMALL", "REC_POT_HEAL_SMALL", "8", "2", "99", "2", "TUNABLE", "TRUE"],
            ["TARGET_WARRIOR", "20", "EQUIPMENT", "EQ_T1_WARRIOR_WEAPON", "REC_EQ_T1_WARRIOR_WEAPON", "1", "0", "10", "0", "TUNABLE", "TRUE"],
            ["TARGET_GUARDIAN", "30", "EQUIPMENT", "EQ_T1_GUARDIAN_WEAPON", "REC_EQ_T1_GUARDIAN_WEAPON", "1", "0", "10", "0", "TUNABLE", "TRUE"],
            ["TARGET_ARCHER", "40", "EQUIPMENT", "EQ_T1_ARCHER_WEAPON", "REC_EQ_T1_ARCHER_WEAPON", "1", "0", "10", "0", "TUNABLE", "TRUE"],
            ["TARGET_MAGE", "50", "EQUIPMENT", "EQ_T1_MAGE_WEAPON", "REC_EQ_T1_MAGE_WEAPON", "1", "0", "10", "0", "TUNABLE", "TRUE"],
            ["TARGET_CLERIC", "60", "EQUIPMENT", "EQ_T1_CLERIC_WEAPON", "REC_EQ_T1_CLERIC_WEAPON", "1", "0", "10", "0", "TUNABLE", "TRUE"],
        ],
        ["target_id"],
        {"product_kind": ["POTION", "EQUIPMENT"]},
    ),
    "treatment_rules.csv": (
        ["treatment_id", "facility_id", "required_profession_id", "base_ticks", "xp_reward",
         "from_state", "to_state", "status", "enabled"],
        [["TREAT_INJURY_BASIC", "FAC_INFIRMARY", "NPC_HEALER", "30", "30", "INJURED", "IDLE_TOWN", "TUNABLE", "TRUE"]],
        ["treatment_id"],
        {},
    ),
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


def field(name: str, enums: dict[str, list[str]]) -> dict[str, Any]:
    numeric = {"facility_level", "queue_capacity", "speed_bps", "priority", "target_quantity",
               "min_target", "max_target", "emergency_floor", "base_ticks", "xp_reward"}
    domain = "INT32" if name in numeric else "BOOL" if name == "enabled" else "STATUS" if name == "status" else "ENUM" if name in enums else "STABLE_ID"
    return {"domain": domain, "enumValues": enums.get(name, []), "name": name, "nullable": False}


def table_entry(name: str, raw: bytes, header: list[str], primary_key: list[str], enums: dict[str, list[str]]) -> dict[str, Any]:
    fks: list[dict[str, Any]] = []
    if name == "production_stock_targets.csv":
        fks.extend([
            {"mode": "HARD", "sourceFields": ["recipe_id"], "targetFields": ["recipe_id"], "targetFile": "recipes.csv"},
        ])
    if name == "treatment_rules.csv":
        fks.extend([
            {"mode": "HARD", "sourceFields": ["facility_id"], "targetFields": ["facility_id"], "targetFile": "facilities.csv"},
            {"mode": "HARD", "sourceFields": ["required_profession_id"], "targetFields": ["profession_id"], "targetFile": "npc_professions.csv"},
        ])
    return {
        "fields": [field(name_, enums) for name_ in header],
        "file": name,
        "foreignKeys": fks,
        "primaryKey": primary_key,
        "required": True,
        "rowCount": len(TABLES[name][1]),
        "schemaVersion": 1,
        "sha256": sha(raw),
    }


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.6"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({
        "contentVersion": "1.0.0-content.7",
        "csvSchemaSetVersion": 5,
        "minimumGameVersion": "1.0.0-p09",
        "generatedAtUtc": GENERATED_AT,
    })
    table_map = {value["file"]: value for value in manifest["tables"]}
    for name, (header, rows, primary_key, enums) in TABLES.items():
        raw = csv_bytes(header, rows)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, primary_key, enums)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 78:
        raise ContractError("P09 content package must contain exactly 78 tables")
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P09 schema input drifted from content.6")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.7"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.7"
    payload = schema["$defs"]["Payload"]
    payload["required"].insert(payload["required"].index("extensions"), "production")
    payload["properties"]["production"] = {"$ref": "#/$defs/Production"}
    operation_types = schema["$defs"]["OperationJournalEntry"]["properties"]["operationType"]["enum"]
    if "PRODUCTION_COMMAND" not in operation_types:
        operation_types.append("PRODUCTION_COMMAND")

    stable = {"minLength": 1, "maxLength": 128, "type": "string"}
    nullable_stable = {"oneOf": [stable, {"type": "null"}]}
    non_negative = {"minimum": 0, "maximum": 9007199254740991, "type": "integer"}
    positive = {"minimum": 1, "maximum": 9007199254740991, "type": "integer"}
    material = object_schema(["itemId", "quantity"], {"itemId": stable, "quantity": positive})
    job = object_schema(
        ["jobId", "queueNo", "jobKind", "recipeId", "targetMercenaryInstanceId", "quantity",
         "ticksTotal", "ticksRemaining", "status", "reservedMaterials", "outputKind", "outputId",
         "outputQuantity", "enqueuedOperationId", "startedAtTick"],
        {
            "jobId": stable, "queueNo": positive, "jobKind": {"enum": ["CRAFT", "TREATMENT"], "type": "string"},
            "recipeId": nullable_stable, "targetMercenaryInstanceId": nullable_stable, "quantity": positive,
            "ticksTotal": positive, "ticksRemaining": non_negative,
            "status": {"enum": ["QUEUED", "RUNNING"], "type": "string"},
            "reservedMaterials": {"items": material, "maxItems": 8, "type": "array"},
            "outputKind": {"oneOf": [{"enum": ["POTION", "EQUIPMENT", "TREATMENT"], "type": "string"}, {"type": "null"}]},
            "outputId": nullable_stable, "outputQuantity": non_negative, "enqueuedOperationId": stable,
            "startedAtTick": {"oneOf": [non_negative, {"type": "null"}]},
        },
    )
    completion = object_schema(
        ["jobId", "jobKind", "outputKind", "outputId", "quantity", "completedAtTick"],
        {"jobId": stable, "jobKind": {"enum": ["CRAFT", "TREATMENT"], "type": "string"},
         "outputKind": {"enum": ["POTION", "EQUIPMENT", "TREATMENT"], "type": "string"},
         "outputId": stable, "quantity": positive, "completedAtTick": non_negative},
    )
    queue = object_schema(
        ["facilityId", "stoppedReason", "jobs", "lastCompletion"],
        {"facilityId": {"enum": ["FAC_BLACKSMITH", "FAC_ALCHEMY", "FAC_INFIRMARY"], "type": "string"},
         "stoppedReason": stable, "jobs": {"items": job, "maxItems": 10, "type": "array"},
         "lastCompletion": {"oneOf": [completion, {"type": "null"}]}},
    )
    target = object_schema(
        ["targetId", "priority", "productKind", "productId", "recipeId", "targetQuantity", "minTarget",
         "maxTarget", "emergencyFloor", "enabled", "lastStopReason"],
        {"targetId": stable, "priority": {"minimum": 1, "maximum": 9999, "type": "integer"},
         "productKind": {"enum": ["POTION", "EQUIPMENT"], "type": "string"}, "productId": stable,
         "recipeId": stable, "targetQuantity": non_negative, "minTarget": non_negative, "maxTarget": positive,
         "emergencyFloor": non_negative, "enabled": {"type": "boolean"}, "lastStopReason": stable},
    )
    event = object_schema(
        ["sequence", "eventType", "facilityId", "jobId", "reasonCode", "tick"],
        {"sequence": positive, "eventType": stable, "facilityId": stable, "jobId": nullable_stable,
         "reasonCode": stable, "tick": non_negative},
    )
    schema["$defs"]["Production"] = object_schema(
        ["productionVersion", "currentTick", "nextQueueNo", "nextEventSequence", "stockTargets", "facilityQueues", "events"],
        {"productionVersion": {"const": 1, "type": "integer"}, "currentTick": non_negative,
         "nextQueueNo": positive, "nextEventSequence": positive,
         "stockTargets": {"items": target, "maxItems": 64, "type": "array"},
         "facilityQueues": {"items": queue, "minItems": 3, "maxItems": 3, "type": "array"},
         "events": {"items": event, "maxItems": 100, "type": "array"}},
    )
    return pretty(schema)


def default_targets() -> list[dict[str, Any]]:
    rows = TABLES["production_stock_targets.csv"][1]
    return [{
        "targetId": row[0], "priority": int(row[1]), "productKind": row[2], "productId": row[3],
        "recipeId": row[4], "targetQuantity": int(row[5]), "minTarget": int(row[6]),
        "maxTarget": int(row[7]), "emergencyFloor": int(row[8]), "enabled": True,
        "lastStopReason": "NONE",
    } for row in rows]


def production() -> dict[str, Any]:
    return {
        "productionVersion": 1, "currentTick": 0, "nextQueueNo": 1, "nextEventSequence": 1,
        "stockTargets": default_targets(),
        "facilityQueues": [
            {"facilityId": facility_id, "stoppedReason": "P09_FACILITY_LOCKED", "jobs": [], "lastCompletion": None}
            for facility_id in ("FAC_BLACKSMITH", "FAC_ALCHEMY", "FAC_INFIRMARY")
        ],
        "events": [],
    }


def seal(document: dict[str, Any]) -> None:
    document["integrity"]["payloadSha256"] = sha(P03._jcs_bytes(document["payload"]))
    integrity = document["integrity"]
    envelope = {key: document[key] for key in ("schemaId", "saveVersion", "gameVersion", "contentVersion", "saveId", "profileId", "revision", "createdAtUtc", "savedAtUtc")}
    envelope.update({key: integrity[key] for key in ("integrityVersion", "algorithm", "canonicalization", "payloadSha256")})
    document["integrity"]["fileSha256"] = sha(P03._jcs_bytes(envelope))


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["gameVersion"] = "1.0.0-p09"
    result["contentVersion"] = "1.0.0-content.7"
    result["payload"]["production"] = production()
    result["payload"]["economy"]["store"]["supplyState"]["mode"] = "PRODUCTION_OWNED"
    seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P09 template input drifted from P08")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000000901", "profileId": "018f0000-0000-7000-8000-000000000902", "revision": 0})
    seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.7" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.7"], "schemaFile": "save.content.7.schema.json", "sha256": sha(schema_bytes)})
    files = {
        TEMPLATE_OUTPUT: pretty(new_game),
        GOLDEN_ROOT / "p09-new-game.golden.json": pretty(new_game),
        GOLDEN_ROOT / "p09-migration-before.golden.json": pretty(before),
        GOLDEN_ROOT / "p09-migration-after.golden.json": pretty(after),
    }
    return pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P09 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY: YES" not in text or "UNRESOLVED: NONE" not in text:
        raise ContractError("P09 contract is not implementation-ready")
    package = build_package()
    schema_bytes = build_schema()
    registry_bytes, extras = build_extras(schema_bytes)
    return package, schema_bytes, registry_bytes, extras


def generate(package: Package, schema_bytes: bytes, registry_bytes: bytes, extras: dict[Path, bytes], check: bool) -> None:
    files: dict[Path, bytes] = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": pretty(package.schema),
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
        raise ContractError("P09 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p09-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p09-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {sha(schema_bytes)}")
    print(f"package-fingerprint: {sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
