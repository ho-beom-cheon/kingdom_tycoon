#!/usr/bin/env python3
"""Build and verify P11 progression/promotion content and Save contracts."""

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
SOURCE = ROOT / "docs" / "design" / "TYCOON_P11_PROGRESSION_PROMOTION_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.9"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.8.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.9.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p10-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p11-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P11"

EXPECTED_DOCUMENT_SHA256 = "400d3209881376daf324d303def8cf99dc0c3624146b5550f79a773c3c25f2a4"
EXPECTED_SCHEMA_SHA256 = "e781077738def1c473a976d75b7cf87717212b0cfcefd141aef21d629f0584dc"
EXPECTED_TEMPLATE_SHA256 = "ef9d76a865001042ad458e81920d53f7687cdba940e99e9fbeb97ba8bb427846"
GENERATED_AT = "2026-07-19T00:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p10_content as P10  # noqa: E402

ContractError = P10.ContractError
Package = P10.Package
P03 = P10.P03

RANKS = [
    ("RANK_APPRENTICE", 1, 20),
    ("RANK_REGULAR", 2, 30),
    ("RANK_SKILLED", 3, 40),
    ("RANK_ELITE", 4, 50),
    ("RANK_HERO", 5, 60),
    ("RANK_LEGEND", 6, 70),
]


def level_rows() -> list[list[str]]:
    rows: list[list[str]] = []
    for rank_id, order, maximum in RANKS:
        cumulative = 0
        for level in range(1, maximum + 1):
            n = level - 1
            to_next = 0 if level == maximum else order * 20 + order * 4 * n + (order * n * n) // 2
            rows.append([rank_id, str(level), str(to_next), str(cumulative), "TUNABLE", "TRUE"])
            cumulative += to_next
    return rows


TABLES: dict[str, tuple[list[str], list[list[str]], list[str]]] = {
    "mercenary_level_curves.csv": (
        ["rank_id", "level", "xp_to_next", "cumulative_xp", "status", "enabled"],
        level_rows(), ["rank_id", "level"]),
    "promotion_review_rules.csv": (
        ["from_rank_id", "to_rank_id", "guild_level_required", "base_kingdom_gold", "review_seconds", "recommended_equipment_score", "status", "enabled"],
        [
            ["RANK_APPRENTICE", "RANK_REGULAR", "1", "200", "60", "400", "TUNABLE", "TRUE"],
            ["RANK_REGULAR", "RANK_SKILLED", "2", "800", "300", "900", "TUNABLE", "TRUE"],
            ["RANK_SKILLED", "RANK_ELITE", "2", "3000", "900", "1800", "TUNABLE", "TRUE"],
            ["RANK_ELITE", "RANK_HERO", "3", "12000", "1800", "3500", "TUNABLE", "TRUE"],
            ["RANK_HERO", "RANK_LEGEND", "4", "40000", "3600", "6000", "TUNABLE", "TRUE"],
        ], ["from_rank_id"]),
    "promotion_record_requirements.csv": (
        ["from_rank_id", "requirement_no", "record_type", "subject_id", "required_count", "status", "enabled"],
        [
            ["RANK_APPRENTICE", "1", "HUNT_COUNT", "", "3", "TUNABLE", "TRUE"],
            ["RANK_REGULAR", "1", "REGION_BATTLE_COUNT", "REGION_R02", "20", "TUNABLE", "TRUE"],
            ["RANK_SKILLED", "1", "ELITE_KILL_COUNT", "", "10", "TUNABLE", "TRUE"],
            ["RANK_ELITE", "1", "BOSS_CONTRIBUTION_COUNT", "", "3", "TUNABLE", "TRUE"],
            ["RANK_HERO", "1", "RAID_CLEAR_COUNT", "", "3", "TUNABLE", "TRUE"],
        ], ["from_rank_id", "requirement_no"]),
    "promotion_supply_rules.csv": (
        ["supply_rule_id", "from_rank_id", "item_id", "quantity", "status", "enabled"],
        [
            ["SUPPLY_PROMO_APPRENTICE", "RANK_APPRENTICE", "MAT_PROMO_BRONZE_EMBLEM", "1", "TUNABLE", "TRUE"],
            ["SUPPLY_PROMO_REGULAR", "RANK_REGULAR", "MAT_PROMO_SILVER_BADGE", "1", "TUNABLE", "TRUE"],
            ["SUPPLY_PROMO_SKILLED", "RANK_SKILLED", "MAT_PROMO_ELITE_SIGIL", "1", "TUNABLE", "TRUE"],
            ["SUPPLY_PROMO_ELITE", "RANK_ELITE", "MAT_PROMO_HERO_CREST", "1", "TUNABLE", "TRUE"],
            ["SUPPLY_PROMO_HERO", "RANK_HERO", "MAT_PROMO_LEGEND_CREST", "1", "TUNABLE", "TRUE"],
        ], ["supply_rule_id"]),
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
    integers = {"level", "xp_to_next", "cumulative_xp", "guild_level_required", "base_kingdom_gold",
                "review_seconds", "recommended_equipment_score", "requirement_no", "required_count", "quantity"}
    enums = {"record_type": ["HUNT_COUNT", "REGION_BATTLE_COUNT", "ELITE_KILL_COUNT", "BOSS_CONTRIBUTION_COUNT", "RAID_CLEAR_COUNT"]}
    nullable = name == "subject_id"
    domain = "SAFE_INT" if name in {"xp_to_next", "cumulative_xp", "base_kingdom_gold", "review_seconds", "recommended_equipment_score", "required_count", "quantity"} else "INT32" if name in integers else "BOOL" if name == "enabled" else "STATUS" if name == "status" else "ENUM" if name in enums else "STABLE_ID"
    return {"domain": domain, "enumValues": enums.get(name, []), "name": name, "nullable": nullable}


def table_entry(name: str, raw: bytes, header: list[str], rows: list[list[str]], primary_key: list[str]) -> dict[str, Any]:
    foreign_keys: list[dict[str, Any]] = []
    for source in ("rank_id", "from_rank_id", "to_rank_id"):
        if source in header:
            foreign_keys.append({"mode": "HARD", "sourceFields": [source], "targetFields": ["rank_id"], "targetFile": "mercenary_ranks.csv"})
    if "item_id" in header:
        foreign_keys.append({"mode": "HARD", "sourceFields": ["item_id"], "targetFields": ["item_id"], "targetFile": "items.csv"})
    return {"fields": [field(value) for value in header], "file": name, "foreignKeys": foreign_keys,
            "primaryKey": primary_key, "required": True, "rowCount": len(rows), "schemaVersion": 1, "sha256": sha(raw)}


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.8"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({"contentVersion": "1.0.0-content.9", "csvSchemaSetVersion": 7,
                     "minimumGameVersion": "1.0.0-p11", "generatedAtUtc": GENERATED_AT})
    table_map = {value["file"]: value for value in manifest["tables"]}
    for name, (header, rows, primary_key) in TABLES.items():
        raw = csv_bytes(header, rows)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, rows, primary_key)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 84 or len(TABLES["mercenary_level_curves.csv"][1]) != 270:
        raise ContractError("P11 package must contain 84 tables and 270 level rows")
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P11 schema input drifted from content.8")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.9"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.9"
    defs = schema["$defs"]
    records = defs["MercenaryRecords"]
    for name in ("region2BattleCount", "eliteKillCount", "bossContributionCount"):
        records["required"].append(name)
        records["properties"][name] = {"minimum": 0, "maximum": 9007199254740991, "type": "integer"}
    payload = defs["Payload"]
    payload["required"].insert(payload["required"].index("extensions"), "progression")
    payload["properties"]["progression"] = {"$ref": "#/$defs/Progression"}
    operation_enum = defs["OperationJournalEntry"]["properties"]["operationType"]["enum"]
    if "PROMOTION" not in operation_enum:
        operation_enum.append("PROMOTION")
    ledger = defs["EconomyLedgerEntry"]["properties"]
    if "PROMOTION_START" not in ledger["transactionType"]["enum"]:
        ledger["transactionType"]["enum"].append("PROMOTION_START")
    ledger_line = defs["EconomyLedgerLine"]["properties"]
    if "SERVICE" not in ledger_line["productKind"]["enum"]:
        ledger_line["productKind"]["enum"].append("SERVICE")
    stable = {"minLength": 1, "maxLength": 128, "type": "string"}
    nullable_stable = {"oneOf": [stable, {"type": "null"}]}
    nullable_int = {"oneOf": [{"minimum": 0, "maximum": 9007199254740991, "type": "integer"}, {"type": "null"}]}
    positive = {"minimum": 1, "maximum": 9007199254740991, "type": "integer"}
    event = object_schema(
        ["sequence", "eventType", "mercenaryInstanceId", "fromRankId", "toRankId", "levelBefore", "levelAfter", "experienceDelta", "resultCode", "createdAtUtc"],
        {"sequence": positive,
         "eventType": {"enum": ["ExperienceAwarded", "PromotionReady", "PromotionSupportIssued", "PromotionReviewStarted", "PromotionReviewCompleted", "PromotionApplied"], "type": "string"},
         "mercenaryInstanceId": stable, "fromRankId": nullable_stable, "toRankId": nullable_stable,
         "levelBefore": nullable_int, "levelAfter": nullable_int, "experienceDelta": nullable_int,
         "resultCode": stable, "createdAtUtc": {"format": "utc-instant", "type": "string"}},
    )
    defs["Progression"] = object_schema(
        ["progressionVersion", "nextEventSequence", "issuedSupplyKeys", "events"],
        {"progressionVersion": {"const": 1, "type": "integer"}, "nextEventSequence": positive,
         "issuedSupplyKeys": {"items": stable, "maxItems": 120, "type": "array", "uniqueItems": True},
         "events": {"items": event, "maxItems": 200, "type": "array"}},
    )
    return pretty(schema)


def progression() -> dict[str, Any]:
    return {"progressionVersion": 1, "nextEventSequence": 1, "issuedSupplyKeys": [], "events": []}


def seal(document: dict[str, Any]) -> None:
    document["integrity"]["payloadSha256"] = sha(P03._jcs_bytes(document["payload"]))
    integrity = document["integrity"]
    envelope = {key: document[key] for key in ("schemaId", "saveVersion", "gameVersion", "contentVersion", "saveId", "profileId", "revision", "createdAtUtc", "savedAtUtc")}
    envelope.update({key: integrity[key] for key in ("integrityVersion", "algorithm", "canonicalization", "payloadSha256")})
    document["integrity"]["fileSha256"] = sha(P03._jcs_bytes(envelope))


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    for mercenary in result["payload"]["mercenaries"]:
        records = mercenary["records"]
        records.update({"region2BattleCount": 0, "eliteKillCount": 0, "bossContributionCount": 0})
    result["payload"]["progression"] = progression()
    result["gameVersion"] = "1.0.0-p11"
    result["contentVersion"] = "1.0.0-content.9"
    seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P11 template input drifted from P10")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001101",
                     "profileId": "018f0000-0000-7000-8000-000000001102", "revision": 0})
    seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.9" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.9"],
                                "schemaFile": "save.content.9.schema.json", "sha256": sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {TEMPLATE_OUTPUT: pretty(new_game), GOLDEN_ROOT / "p11-new-game.golden.json": pretty(new_game),
             GOLDEN_ROOT / "p11-migration-before.golden.json": pretty(before),
             GOLDEN_ROOT / "p11-migration-after.golden.json": pretty(after)}
    return pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P11 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY: YES" not in text or "UNRESOLVED: NONE" not in text:
        raise ContractError("P11 contract is not implementation-ready")
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
        raise ContractError("P11 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p11-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p11-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {sha(schema_bytes)}")
    print(f"package-fingerprint: {sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
