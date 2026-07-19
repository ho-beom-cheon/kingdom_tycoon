#!/usr/bin/env python3
"""Build and verify P13 recruitment content and Save contracts."""

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
SOURCE = ROOT / "docs" / "design" / "TYCOON_P13_RECRUITMENT_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.11"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.10.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.11.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p12-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p13-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P13"

EXPECTED_DOCUMENT_SHA256 = "416775320aadaeaf8c97bb4bb9561abc8e9777a4d803d3014748b1b697ee3a52"
EXPECTED_SCHEMA_SHA256 = "96a6cf679f2223deaa1613ba3694147081388cb7ecf578cb4c4fd5fa087304c1"
EXPECTED_TEMPLATE_SHA256 = "2349868589093357593262062321bbdc88f0ed62eb8d117ab4a14a0d9c3279c2"
GENERATED_AT = "2026-07-19T12:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p12_content as P12  # noqa: E402

ContractError = P12.ContractError
Package = P12.Package
P03 = P12.P03


TAVERN_ROWS = [
    ["1", "TAVERN_L1", "3", "1", "14400", "250", "TUNABLE", "TRUE"],
    ["2", "TAVERN_L1", "4", "1", "10800", "300", "TUNABLE", "TRUE"],
    ["3", "TAVERN_L1", "4", "2", "7200", "400", "TUNABLE", "TRUE"],
    ["4", "TAVERN_L4", "5", "2", "3600", "500", "TUNABLE", "TRUE"],
]

GRADE_COST_ROWS = [
    ["GRADE_C", "10000", "TUNABLE", "TRUE"],
    ["GRADE_B", "15000", "TUNABLE", "TRUE"],
    ["GRADE_A", "25000", "TUNABLE", "TRUE"],
]

HISTORY_ROWS = [["RECRUITMENT_HISTORY_V1", "100", "200", "50", "1", "TUNABLE", "TRUE"]]

TABLES: dict[str, tuple[list[str], list[list[str]], list[str]]] = {
    "recruitment_tavern_rules.csv": (
        ["tavern_level", "pool_id", "candidate_count", "max_locked", "free_refresh_seconds", "paid_refresh_cost", "status", "enabled"],
        TAVERN_ROWS,
        ["tavern_level"],
    ),
    "recruitment_grade_cost_rules.csv": (
        ["grade_id", "hire_cost_multiplier_bps", "status", "enabled"],
        GRADE_COST_ROWS,
        ["grade_id"],
    ),
    "recruitment_history_rules.csv": (
        ["history_rule_id", "history_max_entries", "event_max_entries", "pending_request_max_entries", "result_reveal_seconds", "status", "enabled"],
        HISTORY_ROWS,
        ["history_rule_id"],
    ),
}


def csv_bytes(header: list[str], rows: list[list[str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.writer(stream, lineterminator="\r\n")
    writer.writerow(header)
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def parse_csv(raw: bytes) -> tuple[list[str], list[list[str]]]:
    reader = csv.reader(io.StringIO(raw.decode("utf-8-sig"), newline=""))
    rows = list(reader)
    if not rows:
        raise ContractError("P13 source CSV is empty")
    return rows[0], rows[1:]


def field(name: str) -> dict[str, Any]:
    integers = {
        "tavern_level", "candidate_count", "max_locked", "free_refresh_seconds",
        "paid_refresh_cost", "hire_cost_multiplier_bps", "history_max_entries",
        "event_max_entries", "pending_request_max_entries", "result_reveal_seconds",
    }
    domain = "BOOL" if name == "enabled" else "STATUS" if name == "status" else "INT32" if name in integers else "STABLE_ID"
    return {"domain": domain, "enumValues": [], "name": name, "nullable": False}


def table_entry(name: str, raw: bytes, header: list[str], rows: list[list[str]], primary_key: list[str]) -> dict[str, Any]:
    targets = {
        "pool_id": ("recruitment_pools.csv", "pool_id"),
        "grade_id": ("mercenary_grades.csv", "grade_id"),
    }
    foreign_keys = [
        {"mode": "HARD", "sourceFields": [source], "targetFields": [target], "targetFile": file}
        for source, (file, target) in targets.items() if source in header
    ]
    return {
        "fields": [field(value) for value in header],
        "file": name,
        "foreignKeys": foreign_keys,
        "primaryKey": primary_key,
        "required": True,
        "rowCount": len(rows),
        "schemaVersion": 1,
        "sha256": P12.sha(raw),
    }


def update_existing_table(
    name: str,
    raw: bytes,
    table_map: dict[str, dict[str, Any]],
    data: dict[str, bytes],
) -> None:
    header, rows = parse_csv(raw)
    entry = copy.deepcopy(table_map[name])
    entry.update({"rowCount": len(rows), "sha256": P12.sha(raw)})
    data[name] = raw
    table_map[name] = entry


def build_recruitment_tables(base_data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> dict[str, bytes]:
    data = dict(base_data)

    pool_header, pool_rows = parse_csv(data["recruitment_pools.csv"])
    pool_rows.extend([
        ["SPECIAL_WARRIOR_TICKET", "SPECIAL", "PITY_GROUP_SPECIAL_STANDARD", "RATE_UP_WARRIOR", "TICKET", "1", "", "", "TUNABLE", "TRUE"],
        ["SPECIAL_WARRIOR_FREE_PREMIUM", "SPECIAL", "PITY_GROUP_SPECIAL_STANDARD", "RATE_UP_WARRIOR", "FREE_PREMIUM", "300", "", "", "TUNABLE", "TRUE"],
        ["SPECIAL_WARRIOR_PAID_PREMIUM", "SPECIAL", "PITY_GROUP_SPECIAL_STANDARD", "RATE_UP_WARRIOR", "PAID_PREMIUM", "300", "", "", "TUNABLE", "TRUE"],
    ])
    update_existing_table("recruitment_pools.csv", csv_bytes(pool_header, pool_rows), table_map, data)

    entry_header, entry_rows = parse_csv(data["recruitment_pool_entries.csv"])
    for row in entry_rows:
        if row[0] == "TAVERN_TUTORIAL_FIRST" and row[4] == "GRADE_B":
            row[7] = "25"
    for pool_id in ("SPECIAL_WARRIOR_TICKET", "SPECIAL_WARRIOR_FREE_PREMIUM", "SPECIAL_WARRIOR_PAID_PREMIUM"):
        entry_rows.extend([
            [pool_id, "1", "GENERATED_MERCENARY", "GEN_MERC_STANDARD_V1", "GRADE_A", "ALL", "", "82", "TUNABLE", "TRUE"],
            [pool_id, "2", "GENERATED_MERCENARY", "GEN_MERC_STANDARD_V1", "GRADE_S", "ALL", "", "16", "TUNABLE", "TRUE"],
            [pool_id, "3", "GENERATED_MERCENARY", "GEN_MERC_STANDARD_V1", "GRADE_SS", "ALL", "", "2", "TUNABLE", "TRUE"],
        ])
    update_existing_table("recruitment_pool_entries.csv", csv_bytes(entry_header, entry_rows), table_map, data)

    rate_header, _ = parse_csv(data["recruitment_rate_up_groups.csv"])
    update_existing_table(
        "recruitment_rate_up_groups.csv",
        csv_bytes(rate_header, [["RATE_UP_WARRIOR", "5000", "NEXT_S_OR_SS_FEATURED", "TUNABLE", "TRUE"]]),
        table_map,
        data,
    )
    rate_entry_header, _ = parse_csv(data["recruitment_rate_up_entries.csv"])
    update_existing_table(
        "recruitment_rate_up_entries.csv",
        csv_bytes(rate_entry_header, [["RATE_UP_WARRIOR", "JOB_WARRIOR", "100", "TUNABLE", "TRUE"]]),
        table_map,
        data,
    )
    return data


def validate_recruitment(data: dict[str, bytes]) -> None:
    headers: dict[str, list[str]] = {}
    rows: dict[str, list[dict[str, str]]] = {}
    for name in (
        "recruitment_pools.csv", "recruitment_pool_entries.csv", "recruitment_pity_groups.csv",
        "recruitment_pity_rules.csv", "recruitment_tavern_rules.csv", "recruitment_grade_cost_rules.csv",
    ):
        header, values = parse_csv(data[name])
        headers[name] = header
        rows[name] = [dict(zip(header, value, strict=True)) for value in values]

    pools = {row["pool_id"]: row for row in rows["recruitment_pools.csv"] if row["enabled"] == "TRUE"}
    grouped: dict[str, list[dict[str, str]]] = {}
    for row in rows["recruitment_pool_entries.csv"]:
        if row["enabled"] == "TRUE":
            grouped.setdefault(row["pool_id"], []).append(row)
    for pool_id, pool in pools.items():
        entries = grouped.get(pool_id, [])
        if sum(int(row["weight"]) for row in entries) != 100:
            raise ContractError(f"P13 pool weight must equal 100: {pool_id}")
        grades = {row["grade_id"] for row in entries}
        allowed = {"GRADE_C", "GRADE_B", "GRADE_A"} if pool["pool_type"] == "TAVERN" else {"GRADE_A", "GRADE_S", "GRADE_SS"}
        if not grades or not grades.issubset(allowed):
            raise ContractError(f"P13 pool grade boundary invalid: {pool_id}")
    for row in rows["recruitment_tavern_rules.csv"]:
        if row["pool_id"] not in pools or pools[row["pool_id"]]["pool_type"] != "TAVERN":
            raise ContractError("P13 tavern rule must reference a TAVERN pool")
        count, locked = int(row["candidate_count"]), int(row["max_locked"])
        if count < 3 or count > 5 or locked < 1 or locked >= count:
            raise ContractError("P13 tavern candidate/lock bounds invalid")
    if {row["grade_id"] for row in rows["recruitment_grade_cost_rules.csv"]} != {"GRADE_C", "GRADE_B", "GRADE_A"}:
        raise ContractError("P13 tavern grade cost coverage invalid")


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.10"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({
        "contentVersion": "1.0.0-content.11",
        "csvSchemaSetVersion": 9,
        "minimumGameVersion": "1.0.0-p13",
        "generatedAtUtc": GENERATED_AT,
    })
    table_map = {value["file"]: value for value in manifest["tables"]}
    data = build_recruitment_tables(data, table_map)
    for name, (header, values, primary_key) in TABLES.items():
        raw = csv_bytes(header, values)
        data[name] = raw
        table_map[name] = table_entry(name, raw, header, values, primary_key)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 91:
        raise ContractError("P13 package must contain 91 tables")
    validate_recruitment(data)
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P13 schema input drifted from content.10")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.11"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.11"
    defs = schema["$defs"]
    stable = {"maxLength": 128, "minLength": 1, "type": "string"}
    stable_id = {"maxLength": 64, "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", "type": "string"}
    positive = {"maximum": 9007199254740991, "minimum": 1, "type": "integer"}
    safe = {"maximum": 9007199254740991, "minimum": 0, "type": "integer"}
    seed = {"pattern": "^[0-9]+$", "type": "string"}
    nullable_utc = {"oneOf": [{"format": "utc-instant", "type": "string"}, {"type": "null"}]}
    nullable_id = {"oneOf": [stable, {"type": "null"}]}
    candidate = object_schema(
        ["candidateId", "poolId", "generationProfileId", "nameSeed", "appearanceSeed", "growthSeed", "displayName", "jobId", "gradeId", "personalityId", "traitIds", "hireCost", "locked", "generatedAtUtc"],
        {
            "candidateId": {"format": "uuid-v7", "type": "string"}, "poolId": stable_id,
            "generationProfileId": stable_id, "nameSeed": seed, "appearanceSeed": seed, "growthSeed": seed,
            "displayName": {"maxLength": 40, "minLength": 1, "type": "string"}, "jobId": stable_id,
            "gradeId": stable_id, "personalityId": stable_id,
            "traitIds": {"items": stable_id, "maxItems": 8, "type": "array", "uniqueItems": True},
            "hireCost": safe, "locked": {"type": "boolean"}, "generatedAtUtc": {"format": "utc-instant", "type": "string"},
        },
    )
    tavern = object_schema(
        ["refreshSequence", "lastRefreshAtUtc", "nextFreeRefreshAtUtc", "candidates"],
        {"refreshSequence": safe, "lastRefreshAtUtc": nullable_utc, "nextFreeRefreshAtUtc": nullable_utc,
         "candidates": {"items": {"$ref": "#/$defs/RecruitmentCandidate"}, "maxItems": 5, "type": "array"}},
    )
    history = object_schema(
        ["sequence", "operationId", "poolId", "poolType", "paymentType", "count", "costAmount", "resultInstanceIds", "resultGradeIds", "guaranteeReasonIds", "serverReceiptId", "createdAtUtc"],
        {"sequence": positive, "operationId": {"format": "uuid-v7", "type": "string"}, "poolId": stable_id,
         "poolType": {"enum": ["TAVERN", "SPECIAL"], "type": "string"},
         "paymentType": {"enum": ["KINGDOM_GOLD", "TICKET", "FREE_PREMIUM", "PAID_PREMIUM"], "type": "string"},
         "count": {"const": 1, "type": "integer"}, "costAmount": safe,
         "resultInstanceIds": {"items": {"format": "uuid-v7", "type": "string"}, "maxItems": 1, "type": "array", "uniqueItems": True},
         "resultGradeIds": {"items": stable_id, "maxItems": 1, "type": "array"},
         "guaranteeReasonIds": {"items": stable_id, "maxItems": 4, "type": "array", "uniqueItems": True},
         "serverReceiptId": nullable_id, "createdAtUtc": {"format": "utc-instant", "type": "string"}},
    )
    event = object_schema(
        ["sequence", "eventType", "operationId", "subjectId", "resultCode", "createdAtUtc"],
        {"sequence": positive, "eventType": {"enum": ["TavernRefreshed", "CandidateLockChanged", "CandidateHired", "SpecialRecruitmentCompleted"], "type": "string"},
         "operationId": {"format": "uuid-v7", "type": "string"}, "subjectId": stable,
         "resultCode": stable_id, "createdAtUtc": {"format": "utc-instant", "type": "string"}},
    )
    defs["RecruitmentCandidate"] = candidate
    defs["RecruitmentTavernState"] = tavern
    defs["RecruitmentHistoryEntry"] = history
    defs["RecruitmentEvent"] = event
    state = defs["RecruitmentMockState"]
    state["required"] = [
        "stateVersion", "authority", "serverRevision", "nextHistorySequence", "nextEventSequence",
        "premiumWalletCache", "tavern", "pityCounters", "featuredGuarantees", "pendingRequests", "history", "events",
    ]
    state["properties"].update({
        "stateVersion": {"const": 1, "type": "integer"},
        "nextHistorySequence": positive,
        "nextEventSequence": positive,
        "tavern": {"$ref": "#/$defs/RecruitmentTavernState"},
        "history": {"items": {"$ref": "#/$defs/RecruitmentHistoryEntry"}, "maxItems": 100, "type": "array"},
        "events": {"items": {"$ref": "#/$defs/RecruitmentEvent"}, "maxItems": 200, "type": "array"},
    })
    state["properties"]["pendingRequests"]["maxItems"] = 50
    for value in ("TAVERN_REFRESH", "TAVERN_LOCK", "TAVERN_HIRE", "SPECIAL_RECRUIT"):
        enum = defs["OperationJournalEntry"]["properties"]["operationType"]["enum"]
        if value not in enum:
            enum.append(value)
    tx_enum = defs["EconomyLedgerEntry"]["properties"]["transactionType"]["enum"]
    for value in ("TAVERN_REFRESH", "TAVERN_HIRE"):
        if value not in tx_enum:
            tx_enum.append(value)
    return P12.pretty(schema)


def recruitment_state(source: dict[str, Any]) -> dict[str, Any]:
    old = source["payload"]["recruitmentMockState"]
    wallet = copy.deepcopy(old["premiumWalletCache"])
    if old["authority"] == "MOCK_ONLY" and sum(int(value) for value in wallet.values()) == 0:
        wallet.update({"freePremium": 600, "paidPremium": 0, "specialRecruitTickets": 3})
    return {
        "stateVersion": 1,
        "authority": old["authority"],
        "serverRevision": old["serverRevision"],
        "nextHistorySequence": 1,
        "nextEventSequence": 1,
        "premiumWalletCache": wallet,
        "tavern": {"refreshSequence": 0, "lastRefreshAtUtc": None, "nextFreeRefreshAtUtc": None, "candidates": []},
        "pityCounters": copy.deepcopy(old["pityCounters"]),
        "featuredGuarantees": copy.deepcopy(old["featuredGuarantees"]),
        "pendingRequests": copy.deepcopy(old["pendingRequests"]),
        "history": [],
        "events": [],
    }


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    result["payload"]["recruitmentMockState"] = recruitment_state(result)
    result["gameVersion"] = "1.0.0-p13"
    result["contentVersion"] = "1.0.0-content.11"
    P12.P11.seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P13 template input drifted from P12")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001301", "profileId": "018f0000-0000-7000-8000-000000001302", "revision": 0})
    P12.P11.seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.11" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.11"], "schemaFile": "save.content.11.schema.json", "sha256": P12.sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {
        TEMPLATE_OUTPUT: P12.pretty(new_game),
        GOLDEN_ROOT / "p13-new-game.golden.json": P12.pretty(new_game),
        GOLDEN_ROOT / "p13-migration-before.golden.json": P12.pretty(before),
        GOLDEN_ROOT / "p13-migration-after.golden.json": P12.pretty(after),
    }
    return P12.pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if P12.sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P13 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "IMPLEMENTATION_READY: YES" not in text or "UNRESOLVED: NONE" not in text:
        raise ContractError("P13 contract is not implementation-ready")
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
        raise ContractError("P13 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p13-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p13-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {P12.sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {P12.sha(schema_bytes)}")
    print(f"package-fingerprint: {P12.sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
