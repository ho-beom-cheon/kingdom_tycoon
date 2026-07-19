#!/usr/bin/env python3
"""Build and verify P10 equipment-growth content and Save contracts."""

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
SOURCE = ROOT / "docs" / "design" / "TYCOON_P10_EQUIPMENT_GROWTH_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.8"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.7.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.8.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p09-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p10-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P10"

EXPECTED_DOCUMENT_SHA256 = "7861dd64c03614cf922f1082ee6520135ea8fb43af6c4a4a9c736c75ebcbc2be"
EXPECTED_SCHEMA_SHA256 = "14b1b96b87105a94e96de77023679129421e57010cbbc7fc801684477c1e73ba"
EXPECTED_TEMPLATE_SHA256 = "f6247b4666b8471fe41bf514306defbbe158b174cb0913d8ace1993cff490907"
GENERATED_AT = "2026-07-26T00:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p09_content as P09  # noqa: E402

ContractError = P09.ContractError
Package = P09.Package
P03 = P09.P03


TABLES: dict[str, tuple[list[str], list[list[str]], list[str], dict[str, list[str]]]] = {
    "equipment_growth_facility_rules.csv": (
        ["facility_level", "max_enhancement_level", "refine_enabled", "dismantle_enabled",
         "batch_dismantle_limit", "status", "enabled"],
        [
            ["1", "0", "FALSE", "TRUE", "10", "TUNABLE", "TRUE"],
            ["2", "5", "FALSE", "TRUE", "10", "TUNABLE", "TRUE"],
            ["3", "8", "TRUE", "TRUE", "20", "TUNABLE", "TRUE"],
            ["4", "10", "TRUE", "TRUE", "20", "TUNABLE", "TRUE"],
        ],
        ["facility_level"],
        {},
    ),
    "dismantle_rules.csv": (
        ["equipment_tier", "stone_item_id", "base_stone_quantity", "quality_multiplier_enabled",
         "enhancement_refund_bps", "status", "enabled"],
        [
            ["1", "MAT_ENHANCE_1", "1", "TRUE", "7000", "TUNABLE", "TRUE"],
            ["2", "MAT_ENHANCE_1", "2", "TRUE", "7000", "TUNABLE", "TRUE"],
            ["3", "MAT_ENHANCE_2", "2", "TRUE", "7000", "TUNABLE", "TRUE"],
            ["4", "MAT_ENHANCE_3", "2", "TRUE", "7000", "TUNABLE", "TRUE"],
            ["5", "MAT_ENHANCE_4", "2", "TRUE", "7000", "TUNABLE", "TRUE"],
        ],
        ["equipment_tier"],
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


def field(name: str) -> dict[str, Any]:
    integer = {"facility_level", "max_enhancement_level", "batch_dismantle_limit", "equipment_tier",
               "base_stone_quantity", "enhancement_refund_bps"}
    boolean = {"refine_enabled", "dismantle_enabled", "quality_multiplier_enabled", "enabled"}
    domain = "INT32" if name in integer else "BOOL" if name in boolean else "STATUS" if name == "status" else "STABLE_ID"
    return {"domain": domain, "enumValues": [], "name": name, "nullable": False}


def table_entry(name: str, raw: bytes, header: list[str], primary_key: list[str]) -> dict[str, Any]:
    foreign_keys: list[dict[str, Any]] = []
    if name == "dismantle_rules.csv":
        foreign_keys.append({"mode": "HARD", "sourceFields": ["stone_item_id"], "targetFields": ["item_id"], "targetFile": "items.csv"})
    return {
        "fields": [field(value) for value in header],
        "file": name,
        "foreignKeys": foreign_keys,
        "primaryKey": primary_key,
        "required": True,
        "rowCount": len(TABLES[name][1]),
        "schemaVersion": 1,
        "sha256": sha(raw),
    }


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.7"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({"contentVersion": "1.0.0-content.8", "csvSchemaSetVersion": 6,
                     "minimumGameVersion": "1.0.0-p10", "generatedAtUtc": GENERATED_AT})
    table_map = {value["file"]: value for value in manifest["tables"]}
    for name, (header, rows, primary_key, _) in TABLES.items():
        raw = csv_bytes(header, rows)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, primary_key)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 80:
        raise ContractError("P10 content package must contain exactly 80 tables")
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def add_equipment_fields(definition: dict[str, Any]) -> None:
    required = definition["required"]
    anchor = required.index("locked")
    for offset, name in enumerate(("enhancementPityBps", "enhancementAttemptCount", "enhancementMaterialInvested", "pendingRefineOption", "refineRollCount")):
        required.insert(anchor + offset, name)
    properties = definition["properties"]
    properties["enhancementPityBps"] = {"minimum": 0, "maximum": 10000, "type": "integer"}
    properties["enhancementAttemptCount"] = {"minimum": 0, "maximum": 9007199254740991, "type": "integer"}
    properties["enhancementMaterialInvested"] = {"items": {"$ref": "#/$defs/EnhancementMaterialInvestment"}, "maxItems": 4, "type": "array"}
    properties["pendingRefineOption"] = {"oneOf": [{"$ref": "#/$defs/RefineOption"}, {"type": "null"}]}
    properties["refineRollCount"] = {"minimum": 0, "maximum": 9007199254740991, "type": "integer"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P10 schema input drifted from content.7")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.8"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.8"
    payload = schema["$defs"]["Payload"]
    payload["required"].insert(payload["required"].index("extensions"), "equipmentGrowth")
    payload["properties"]["equipmentGrowth"] = {"$ref": "#/$defs/EquipmentGrowth"}
    schema["$defs"]["OperationJournalEntry"]["properties"]["operationType"]["enum"].append("EQUIPMENT_GROWTH_COMMAND")
    ledger_types = schema["$defs"]["EconomyLedgerEntry"]["properties"]["transactionType"]["enum"]
    ledger_types.extend(["ENHANCE_EQUIPMENT", "REFINE_EQUIPMENT"])
    schema["$defs"]["RefineOption"]["properties"]["value"] = {"minimum": 0, "maximum": 10000, "type": "integer"}
    stable = {"minLength": 1, "maxLength": 128, "type": "string"}
    nullable_stable = {"oneOf": [stable, {"type": "null"}]}
    nullable_int = {"oneOf": [{"minimum": 0, "maximum": 10, "type": "integer"}, {"type": "null"}]}
    positive = {"minimum": 1, "maximum": 9007199254740991, "type": "integer"}
    schema["$defs"]["EnhancementMaterialInvestment"] = object_schema(
        ["itemId", "quantity"], {"itemId": stable, "quantity": positive})
    add_equipment_fields(schema["$defs"]["EquipmentInstance"])
    add_equipment_fields(schema["$defs"]["StoreEquipment"])
    event = object_schema(
        ["sequence", "eventType", "equipmentInstanceId", "actorMercenaryInstanceId", "resultCode",
         "levelBefore", "levelAfter", "optionId", "createdAtUtc"],
        {
            "sequence": positive,
            "eventType": {"enum": ["EquipmentEnhancementAttempted", "EquipmentRefineRolled",
                                      "EquipmentRefineResolved", "EquipmentDismantled"], "type": "string"},
            "equipmentInstanceId": stable,
            "actorMercenaryInstanceId": stable,
            "resultCode": stable,
            "levelBefore": nullable_int,
            "levelAfter": nullable_int,
            "optionId": nullable_stable,
            "createdAtUtc": {"format": "utc-instant", "type": "string"},
        },
    )
    schema["$defs"]["EquipmentGrowth"] = object_schema(
        ["growthVersion", "nextEventSequence", "events"],
        {"growthVersion": {"const": 1, "type": "integer"}, "nextEventSequence": positive,
         "events": {"items": event, "maxItems": 100, "type": "array"}},
    )
    return pretty(schema)


def growth() -> dict[str, Any]:
    return {"growthVersion": 1, "nextEventSequence": 1, "events": []}


def initialize_equipment(value: dict[str, Any]) -> None:
    value["enhancementPityBps"] = 0
    value["enhancementAttemptCount"] = 0
    value["enhancementMaterialInvested"] = []
    value["pendingRefineOption"] = None
    value["refineRollCount"] = 0


def seal(document: dict[str, Any]) -> None:
    document["integrity"]["payloadSha256"] = sha(P03._jcs_bytes(document["payload"]))
    integrity = document["integrity"]
    envelope = {key: document[key] for key in ("schemaId", "saveVersion", "gameVersion", "contentVersion", "saveId", "profileId", "revision", "createdAtUtc", "savedAtUtc")}
    envelope.update({key: integrity[key] for key in ("integrityVersion", "algorithm", "canonicalization", "payloadSha256")})
    document["integrity"]["fileSha256"] = sha(P03._jcs_bytes(envelope))


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    for equipment in result["payload"]["inventory"]["equipment"]:
        initialize_equipment(equipment)
    for equipment in result["payload"]["economy"]["store"]["equipment"]:
        initialize_equipment(equipment)
    result["payload"]["equipmentGrowth"] = growth()
    result["gameVersion"] = "1.0.0-p10"
    result["contentVersion"] = "1.0.0-content.8"
    seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P10 template input drifted from P09")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001001",
                     "profileId": "018f0000-0000-7000-8000-000000001002", "revision": 0})
    seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.8" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.8"],
                                "schemaFile": "save.content.8.schema.json", "sha256": sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {
        TEMPLATE_OUTPUT: pretty(new_game),
        GOLDEN_ROOT / "p10-new-game.golden.json": pretty(new_game),
        GOLDEN_ROOT / "p10-migration-before.golden.json": pretty(before),
        GOLDEN_ROOT / "p10-migration-after.golden.json": pretty(after),
    }
    return pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P10 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY: YES" not in text or "UNRESOLVED: NONE" not in text:
        raise ContractError("P10 contract is not implementation-ready")
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
        raise ContractError("P10 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p10-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p10-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {sha(schema_bytes)}")
    print(f"package-fingerprint: {sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
