#!/usr/bin/env python3
"""Build and verify P15 offline/tutorial content and Save contracts."""

from __future__ import annotations

import argparse
import copy
import csv
import json
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "TYCOON_P15_OFFLINE_TUTORIAL_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.13"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SCHEMA_INPUT = CONTRACT_ROOT / "save.content.12.schema.json"
SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.13.schema.json"
REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
TEMPLATE_INPUT = CONTRACT_ROOT / "p14-new-game.template.json"
TEMPLATE_OUTPUT = CONTRACT_ROOT / "p15-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P15"

EXPECTED_DOCUMENT_SHA256 = "3834bd9f0fe2bd5f5eecd69fcf9b92daea87182ede6a4eb988143b10993723a9"
EXPECTED_SCHEMA_SHA256 = "fda61c2a1f3810a6cd3c0d69882614fe90c42cbe0d141974afa8de2e940b9642"
EXPECTED_TEMPLATE_SHA256 = "195241cb1c67c2dc894458947d584485de5aaccfdf827112bacf2fd4e245ebfe"
GENERATED_AT = "2026-07-20T00:00:00.000Z"

sys.path.insert(0, str(ROOT / "scripts"))
import generate_p14_content as P14  # noqa: E402

ContractError = P14.ContractError
Package = P14.Package
P12 = P14.P12
P03 = P14.P03


RUNTIME_ROWS = [
    ["P15_OFFLINE_MIN_SECONDS", "INTEGER", "60", "SECONDS", "0", "3600", "TXT_P15_OFFLINE_MIN_SECONDS", "TUNABLE", "TRUE"],
    ["P15_OFFLINE_MAX_SECONDS", "INTEGER", "28800", "SECONDS", "0", "86400", "TXT_P15_OFFLINE_MAX_SECONDS", "TUNABLE", "TRUE"],
    ["P15_CLOCK_ROLLBACK_TOLERANCE_SECONDS", "INTEGER", "2", "SECONDS", "0", "60", "TXT_P15_CLOCK_ROLLBACK_TOLERANCE", "TUNABLE", "TRUE"],
    ["P15_OFFLINE_HISTORY_MAX_ENTRIES", "INTEGER", "20", "COUNT", "1", "100", "TXT_P15_OFFLINE_HISTORY_MAX_ENTRIES", "TUNABLE", "TRUE"],
    ["P15_TUTORIAL_TARGET_MINUTES", "INTEGER", "25", "MINUTES", "20", "30", "TXT_P15_TUTORIAL_TARGET_MINUTES", "TUNABLE", "TRUE"],
]

LOCALIZATIONS = {
    "TXT_P15_OFFLINE_MIN_SECONDS": ("Minimum offline settlement time", "오프라인 정산 최소 시간"),
    "TXT_P15_OFFLINE_MAX_SECONDS": ("Maximum offline settlement time", "오프라인 정산 최대 시간"),
    "TXT_P15_CLOCK_ROLLBACK_TOLERANCE": ("Clock rollback tolerance", "시계 역행 허용 범위"),
    "TXT_P15_OFFLINE_HISTORY_MAX_ENTRIES": ("Offline history limit", "오프라인 이력 상한"),
    "TXT_P15_TUTORIAL_TARGET_MINUTES": ("Tutorial target duration", "튜토리얼 목표 시간"),
    "TXT_P15_HUB_TITLE": ("Kingdom journey", "왕국 재건 여정"),
    "TXT_P15_OFFLINE_TITLE": ("Return report", "복귀 보고서"),
    "TXT_P15_TUTORIAL_TITLE": ("Onboarding journey", "온보딩 여정"),
    "TXT_P15_RELEASE_TITLE": ("Version 1.0 readiness", "1.0 준비 상태"),
    "TXT_P15_CONTINUE": ("Continue", "계속 진행"),
    "TXT_P15_SKIP": ("Skip step", "단계 건너뛰기"),
    "TXT_P15_SKIP_ALL": ("Skip tutorial", "튜토리얼 전체 건너뛰기"),
    "TXT_P15_CLOCK_ROLLBACK": ("Rewards paused because the device clock moved backwards.", "기기 시간이 뒤로 이동해 보상 정산을 보류했습니다."),
    "TXT_P15_NO_OFFLINE": ("No offline rewards yet.", "아직 정산할 오프라인 보상이 없습니다."),
    "TXT_P15_LOCAL_AUTHORITY": ("Offline - local progress is available", "오프라인 · 로컬 진행 사용 가능"),
    "TXT_P15_HUNT_LINE": ("Hunt progress", "일반 사냥 성과"),
    "TXT_P15_FACILITY_LINE": ("Facility production", "시설 생산 진행"),
    "TXT_P15_NPC_LINE": ("NPC proficiency", "NPC 숙련 진행"),
    "TXT_P15_POTION_LINE": ("Potion consumption", "포션 사용"),
    "TXT_P15_INJURY_LINE": ("Injury recovery", "부상 회복"),
    "TXT_P15_PROMOTION_LINE": ("Promotion review", "승급 심사"),
}


def update_table(name: str, raw: bytes, table_map: dict[str, dict[str, Any]], data: dict[str, bytes]) -> None:
    _, rows = P14.parse_csv(raw)
    entry = copy.deepcopy(table_map[name])
    entry.update({"rowCount": len(rows), "sha256": P12.sha(raw)})
    data[name] = raw
    table_map[name] = entry


def add_runtime_config(data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> None:
    header, rows = P14.parse_csv(data["runtime_config.csv"])
    rows.extend(RUNTIME_ROWS)
    rows.sort(key=lambda value: value[0])
    update_table("runtime_config.csv", P14.csv_bytes(header, rows), table_map, data)


def add_localizations(data: dict[str, bytes], table_map: dict[str, dict[str, Any]]) -> None:
    header, rows = P14.parse_csv(data["localizations.csv"])
    existing = {(row[0], row[1]) for row in rows}
    for key, (english, korean) in LOCALIZATIONS.items():
        if ("en-US", key) not in existing:
            rows.append(["en-US", key, english, "P15_OFFLINE_TUTORIAL", "CONFIRMED", "TRUE"])
        if ("ko-KR", key) not in existing:
            rows.append(["ko-KR", key, korean, "P15_OFFLINE_TUTORIAL", "CONFIRMED", "TRUE"])
    rows.sort(key=lambda value: (value[0], value[1]))
    update_table("localizations.csv", P14.csv_bytes(header, rows), table_map, data)


def validate_contract_rows(data: dict[str, bytes]) -> None:
    offline_header, offline_rows = P14.parse_csv(data["offline_reward_rules.csv"])
    offline = [dict(zip(offline_header, row, strict=True)) for row in offline_rows]
    expected_types = ["HUNT", "FACILITY", "NPC_PROFICIENCY", "POTION_CONSUMPTION", "INJURY_RECOVERY", "PROMOTION_REVIEW"]
    if [row["settlement_type"] for row in offline] != expected_types:
        raise ContractError("P15 offline settlement type order invalid")
    if any(int(row["max_seconds"]) != 28800 for row in offline):
        raise ContractError("P15 offline rule cap must be 28800 seconds")
    tutorial_header, tutorial_rows = P14.parse_csv(data["tutorial_steps.csv"])
    tutorial = [dict(zip(tutorial_header, row, strict=True)) for row in tutorial_rows]
    if [int(row["order"]) for row in tutorial] != list(range(1, 11)):
        raise ContractError("P15 tutorial must contain ordered steps 1..10")
    if any(row["skippable"] != "TRUE" for row in tutorial):
        raise ContractError("P15 tutorial steps must remain skippable")


def build_package() -> Package:
    base_root = CONTENT_ROOT / "1.0.0-content.12"
    manifest = json.loads((base_root / "content_manifest.json").read_bytes())
    schema = json.loads((base_root / "content_manifest.schema.json").read_bytes())
    data = {table["file"]: (base_root / table["file"]).read_bytes() for table in manifest["tables"]}
    manifest.update({
        "contentVersion": "1.0.0-content.13",
        "csvSchemaSetVersion": 11,
        "minimumGameVersion": "1.0.0-p15",
        "generatedAtUtc": GENERATED_AT,
    })
    table_map = {value["file"]: value for value in manifest["tables"]}
    add_runtime_config(data, table_map)
    add_localizations(data, table_map)
    manifest["tables"] = [table_map[name] for name in sorted(table_map)]
    if len(manifest["tables"]) != 95:
        raise ContractError("P15 package must contain 95 tables")
    validate_contract_rows(data)
    manifest_bytes = P03._jcs_bytes(manifest)
    P03._validate_csv_and_references(manifest, data)
    return Package(schema, manifest, data, manifest_bytes)


def object_schema(required: list[str], properties: dict[str, Any]) -> dict[str, Any]:
    return {"additionalProperties": False, "properties": properties, "required": required, "type": "object"}


def build_schema() -> bytes:
    raw = SCHEMA_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P15 schema input drifted from content.12")
    schema = json.loads(raw)
    schema["$id"] = "urn:tycoon:schema:save:v1:content.13"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.13"
    defs = schema["$defs"]
    stable_id = {"maxLength": 64, "pattern": "^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", "type": "string"}
    uuid = {"format": "uuid", "type": "string"}
    uuid_v7 = {"format": "uuid-v7", "type": "string"}
    utc = {"format": "utc-instant", "type": "string"}
    nullable_utc = {"oneOf": [utc, {"type": "null"}]}
    nullable_uuid = {"oneOf": [uuid, {"type": "null"}]}
    nullable_stable = {"oneOf": [stable_id, {"type": "null"}]}
    safe = {"maximum": 9007199254740991, "minimum": 0, "type": "integer"}
    digest = {"pattern": "^[0-9a-f]{64}$", "type": "string"}

    # Issue #49 keeps content.13/saveVersion 1 wire-compatible. These fields are
    # optional at schema load and are filled atomically by the runtime migration.
    autonomy = defs["MercenaryAutonomy"]["properties"]
    autonomy.update({
        "assignedRegionId": nullable_stable,
        "autoResume": {"type": "boolean"},
        "currentHpBps": {"maximum": 10000, "minimum": 0, "type": "integer"},
        "bagFill": {"maximum": 100, "minimum": 0, "type": "integer"},
        "bagCapacity": {"maximum": 100, "minimum": 1, "type": "integer"},
        "pendingSaleGold": safe,
        "cyclesCompleted": safe,
        "earnedGold": safe,
    })

    defs["TutorialActionReceipt"] = object_schema(
        ["operationId", "stepId", "actionType", "targetId", "requestHash", "appliedAtUtc", "result"],
        {"operationId": uuid_v7, "stepId": stable_id, "actionType": stable_id, "targetId": stable_id,
         "requestHash": digest, "appliedAtUtc": utc,
         "result": {"enum": ["COMPLETED", "SKIPPED", "REPLAYED"], "type": "string"}},
    )
    defs["Tutorial"] = object_schema(
        ["tutorialVersion", "currentStepId", "completedStepIds", "grantedRewardIds", "actionReceipts", "skipped",
         "startedAtUtc", "lastAdvancedAtUtc", "completedAtUtc", "lastOperationId"],
        {"tutorialVersion": {"const": 1, "type": "integer"}, "currentStepId": nullable_stable,
         "completedStepIds": {"items": stable_id, "maxItems": 10, "type": "array", "uniqueItems": True},
         "grantedRewardIds": {"items": stable_id, "maxItems": 32, "type": "array", "uniqueItems": True},
         "actionReceipts": {"items": {"$ref": "#/$defs/TutorialActionReceipt"}, "maxItems": 32, "type": "array"},
         "skipped": {"type": "boolean"}, "startedAtUtc": nullable_utc, "lastAdvancedAtUtc": nullable_utc,
         "completedAtUtc": nullable_utc, "lastOperationId": nullable_uuid},
    )
    defs["OfflineSettlementLine"] = object_schema(
        ["type", "quantity", "labelTextKey"],
        {"type": {"enum": ["HUNT", "POTION_CONSUMPTION", "FACILITY", "NPC_PROFICIENCY", "INJURY_RECOVERY", "PROMOTION_REVIEW"], "type": "string"},
         "quantity": safe, "labelTextKey": stable_id},
    )
    defs["OfflineSettlementHistory"] = object_schema(
        ["settlementId", "startUtc", "endUtc", "elapsedSeconds", "eligibleSeconds", "status", "lines", "digest"],
        {"settlementId": digest, "startUtc": utc, "endUtc": utc, "elapsedSeconds": safe, "eligibleSeconds": safe,
         "status": {"enum": ["APPLIED", "CAPPED", "BELOW_MINIMUM", "CLOCK_ROLLBACK", "REPLAYED"], "type": "string"},
         "lines": {"items": {"$ref": "#/$defs/OfflineSettlementLine"}, "maxItems": 6, "type": "array"}, "digest": digest},
    )
    defs["Offline"] = object_schema(
        ["offlineVersion", "accrualCursorUtc", "lastTrustedUtc", "lastSettlementId", "lastStatus", "lastElapsedSeconds",
         "lastEligibleSeconds", "pendingSettlement", "history"],
        {"offlineVersion": {"const": 1, "type": "integer"}, "accrualCursorUtc": utc, "lastTrustedUtc": utc,
         "lastSettlementId": {"oneOf": [digest, {"type": "null"}]},
         "lastStatus": {"enum": ["NONE", "APPLIED", "CAPPED", "BELOW_MINIMUM", "CLOCK_ROLLBACK", "REPLAYED"], "type": "string"},
         "lastElapsedSeconds": safe, "lastEligibleSeconds": safe,
         "pendingSettlement": {"oneOf": [{"$ref": "#/$defs/OfflinePendingSettlement"}, {"type": "null"}]},
         "history": {"items": {"$ref": "#/$defs/OfflineSettlementHistory"}, "maxItems": 20, "type": "array"}},
    )
    operation_enum = defs["OperationJournalEntry"]["properties"]["operationType"]["enum"]
    if "TUTORIAL_COMMAND" not in operation_enum:
        operation_enum.append("TUTORIAL_COMMAND")
    return P12.pretty(schema)


def normalize_tutorial(value: dict[str, Any]) -> dict[str, Any]:
    completed = sorted(set(value.get("completedStepIds", [])))
    valid_ids = [f"TUT_{index:02d}_" for index in range(1, 11)]
    current = value.get("currentStepId")
    if current and not any(current.startswith(prefix) for prefix in valid_ids):
        current = "TUT_01_KINGDOM_OVERVIEW"
    return {
        "tutorialVersion": 1,
        "currentStepId": current or (None if value.get("completedAtUtc") else "TUT_01_KINGDOM_OVERVIEW"),
        "completedStepIds": completed,
        "grantedRewardIds": sorted(set(value.get("grantedRewardIds", []))),
        "actionReceipts": copy.deepcopy(value.get("actionReceipts", []))[-32:],
        "skipped": bool(value.get("skipped", False)),
        "startedAtUtc": value.get("startedAtUtc"),
        "lastAdvancedAtUtc": value.get("lastAdvancedAtUtc"),
        "completedAtUtc": value.get("completedAtUtc"),
        "lastOperationId": value.get("lastOperationId"),
    }


def normalize_offline(value: dict[str, Any]) -> dict[str, Any]:
    return {
        "offlineVersion": 1,
        "accrualCursorUtc": value["accrualCursorUtc"],
        "lastTrustedUtc": value["lastTrustedUtc"],
        "lastSettlementId": value.get("lastSettlementId"),
        "lastStatus": value.get("lastStatus", "NONE"),
        "lastElapsedSeconds": int(value.get("lastElapsedSeconds", 0)),
        "lastEligibleSeconds": int(value.get("lastEligibleSeconds", 0)),
        "pendingSettlement": copy.deepcopy(value.get("pendingSettlement")),
        "history": copy.deepcopy(value.get("history", []))[-20:],
    }


def migrate(source: dict[str, Any]) -> dict[str, Any]:
    result = copy.deepcopy(source)
    payload = result["payload"]
    payload["tutorial"] = normalize_tutorial(payload["tutorial"])
    payload["offline"] = normalize_offline(payload["offline"])
    for mercenary in payload["mercenaries"]:
        autonomy = mercenary["autonomy"]
        autonomy.update({
            "assignedRegionId": None,
            "autoResume": True,
            "currentHpBps": 10000,
            "bagFill": 0,
            "bagCapacity": 6,
            "pendingSaleGold": 0,
            "cyclesCompleted": 0,
            "earnedGold": 0,
        })
    result["gameVersion"] = "1.0.0-p15"
    result["contentVersion"] = "1.0.0-content.13"
    P12.P11.seal(result)
    return result


def build_extras(schema_bytes: bytes) -> tuple[bytes, dict[Path, bytes]]:
    raw = TEMPLATE_INPUT.read_bytes()
    if P12.sha(raw) != EXPECTED_TEMPLATE_SHA256:
        raise ContractError("P15 template input drifted from P14")
    source = json.loads(raw)
    new_game = migrate(source)
    new_game.update({"saveId": "018f0000-0000-7000-8000-000000001501", "profileId": "018f0000-0000-7000-8000-000000001502", "revision": 0})
    P12.P11.seal(new_game)
    before = copy.deepcopy(source)
    after = migrate(before)
    registry = json.loads(REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.13" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.13"], "schemaFile": "save.content.13.schema.json", "sha256": P12.sha(schema_bytes)})
    registry["entries"].sort(key=lambda entry: int(entry["contentVersions"][0].rsplit(".", 1)[-1]))
    files = {
        TEMPLATE_OUTPUT: P12.pretty(new_game),
        GOLDEN_ROOT / "p15-new-game.golden.json": P12.pretty(new_game),
        GOLDEN_ROOT / "p15-migration-before.golden.json": P12.pretty(before),
        GOLDEN_ROOT / "p15-migration-after.golden.json": P12.pretty(after),
    }
    return P12.pretty(registry), files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    if P12.sha(raw) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P15 final design document digest mismatch")
    text = raw.decode("utf-8-sig")
    if "FINAL / IMPLEMENTATION READY" not in text or "UNRESOLVED=0" not in text:
        raise ContractError("P15 contract is not implementation-ready")
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
        raise ContractError("P15 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p15-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p15-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {P12.sha(package.manifest_bytes)}")
    print(f"save-schema-sha256: {P12.sha(schema_bytes)}")
    print(f"package-fingerprint: {P12.sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
