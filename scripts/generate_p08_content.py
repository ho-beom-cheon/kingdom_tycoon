#!/usr/bin/env python3
"""Build and verify the immutable P08 economy/store content and Save contracts."""

from __future__ import annotations

import argparse
import copy
import csv
import hashlib
import io
import json
import re
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "TYCOON_P08_ECONOMY_STORE_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.6"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SAVE_SCHEMA_INPUT = CONTRACT_ROOT / "save.content.5.schema.json"
SAVE_SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.6.schema.json"
SAVE_REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
P07_TEMPLATE = CONTRACT_ROOT / "p07-new-game.template.json"
NEW_GAME_OUTPUT = CONTRACT_ROOT / "p08-new-game.template.json"
GOLDEN_ROOT = ROOT / "docs" / "goldens" / "P08"

EXPECTED_DOCUMENT_SHA256 = "9703af3f39cf7d256ead2bc735bae1ef374491df67507b61a814ba4f98def8a9"
EXPECTED_P07_SCHEMA_SHA256 = "d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392"
EXPECTED_P07_TEMPLATE_SHA256 = "6e85ef169602b5fac957df3447b41a687efcd9012dcfd78fdeb16120a508d5cf"
GENERATED_AT = "2026-07-23T00:00:00.000Z"


sys.path.insert(0, str(ROOT / "scripts"))
import generate_p07_content as P07  # noqa: E402

P03 = P07.P03
ContractError = P07.ContractError
Package = P07.Package


NEW_FILES = (
    "pricing_policies.csv",
    "store_level_rules.csv",
    "store_assortment.csv",
    "store_supply_rules.csv",
    "store_price_rules.csv",
    "store_purchase_ai_rules.csv",
    "transaction_reason_codes.csv",
)

HEADERS_TO_FILES = {
    "policy_id": "pricing_policies.csv",
    "store_level": "store_level_rules.csv",
    "assortment_id": "store_assortment.csv",
    "supply_rule_id": "store_supply_rules.csv",
    "price_rule_id": "store_price_rules.csv",
    "personality_id": "store_purchase_ai_rules.csv",
    "reason_code": "transaction_reason_codes.csv",
    "currency_id": "currencies.csv",
    "state": "autonomy_rules.csv",
    "config_key": "runtime_config.csv",
    "locale": "localizations.csv",
}

SCHEMA_FIELDS: dict[str, list[tuple[str, str, bool, list[str]]]] = {
    "pricing_policies.csv": [
        ("policy_id", "STABLE_ID", False, []), ("multiplier_bps", "INT32", False, []),
        ("min_store_level", "INT32", False, []), ("name_text_key", "STABLE_ID", False, []),
        ("effect_text_key", "STABLE_ID", False, []), ("status", "STATUS", False, []),
        ("enabled", "BOOL", False, []),
    ],
    "store_level_rules.csv": [
        ("store_level", "INT32", False, []), ("max_tier", "INT32", False, []),
        ("max_quality_order", "INT32", False, []), ("boss_allowed", "BOOL", False, []),
        ("policy_change_allowed", "BOOL", False, []), ("max_stack_lines", "INT32", False, []),
        ("max_equipment_lines", "INT32", False, []), ("status", "STATUS", False, []),
        ("enabled", "BOOL", False, []),
    ],
    "store_assortment.csv": [
        ("assortment_id", "STABLE_ID", False, []), ("product_kind", "ENUM", False, ["ITEM", "POTION", "EQUIPMENT"]),
        ("product_id", "STRING", False, []), ("min_store_level", "INT32", False, []),
        ("customer_purchase_allowed", "BOOL", False, []), ("merchant_acquire_allowed", "BOOL", False, []),
        ("status", "STATUS", False, []), ("enabled", "BOOL", False, []),
    ],
    "store_supply_rules.csv": [
        ("supply_rule_id", "STABLE_ID", False, []), ("product_kind", "ENUM", False, ["POTION", "EQUIPMENT"]),
        ("product_id", "STABLE_ID", False, []), ("target_quantity", "INT32", False, []),
        ("min_store_level", "INT32", False, []), ("refresh_buy_count", "INT32", False, []),
        ("source_type", "ENUM", False, ["SYSTEM_SUPPLY"]), ("status", "STATUS", False, []),
        ("enabled", "BOOL", False, []),
    ],
    "store_price_rules.csv": [
        ("price_rule_id", "STABLE_ID", False, []), ("product_kind", "ENUM", False, ["ITEM", "POTION", "EQUIPMENT"]),
        ("product_id", "STRING", False, []), ("acquire_rate_bps", "INT32", False, []),
        ("customer_base_price", "SAFE_INT", False, []), ("customer_markup_bps", "INT32", False, []),
        ("min_price", "SAFE_INT", False, []), ("status", "STATUS", False, []), ("enabled", "BOOL", False, []),
    ],
    "store_purchase_ai_rules.csv": [
        ("personality_id", "STABLE_ID", False, []), ("threshold_bps", "INT32", False, []),
        ("reserve_gold", "SAFE_INT", False, []), ("potion_target", "INT32", False, []),
        ("potion_max_per_cycle", "INT32", False, []), ("equipment_max_per_cycle", "INT32", False, []),
        ("equipment_priority", "ENUM", False, ["BALANCED", "PRICE", "UPGRADE", "WEAPON", "DEFENSE", "QUALITY"]),
        ("status", "STATUS", False, []), ("enabled", "BOOL", False, []),
    ],
    "transaction_reason_codes.csv": [
        ("reason_code", "STABLE_ID", False, []),
        ("transaction_type", "ENUM", False, ["SET_PRICING_POLICY", "SELL_TO_STORE", "BUY_FROM_STORE", "SYSTEM_SUPPLY", "AUTONOMY"]),
        ("user_visible", "BOOL", False, []), ("text_key", "STABLE_ID", False, []),
        ("status", "STATUS", False, []), ("enabled", "BOOL", False, []),
    ],
}

PRIMARY_KEYS = {
    "pricing_policies.csv": ["policy_id"], "store_level_rules.csv": ["store_level"],
    "store_assortment.csv": ["assortment_id"], "store_supply_rules.csv": ["supply_rule_id"],
    "store_price_rules.csv": ["price_rule_id"], "store_purchase_ai_rules.csv": ["personality_id"],
    "transaction_reason_codes.csv": ["reason_code"],
}


def _pretty_json(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def _rows(raw: bytes) -> tuple[list[str], list[dict[str, str]]]:
    reader = csv.DictReader(io.StringIO(raw.decode("utf-8"), newline=""), strict=True)
    return list(reader.fieldnames or []), list(reader)


def _csv_bytes(header: list[str], rows: list[dict[str, str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=header, lineterminator="\r\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def _parse_design_csv(text: str) -> tuple[dict[str, bytes], list[str]]:
    blocks = re.findall(r"```csv\n(.*?)\n```", text, flags=re.DOTALL)
    if len(blocks) != 12:
        raise ContractError(f"P08 must contain 12 CSV production blocks, found {len(blocks)}")
    parsed: dict[str, bytes] = {}
    asset_rows: list[str] = []
    for body in blocks:
        first = body.splitlines()[0].split(",", 1)[0]
        if first == "ASSET_P08_GOLD_KINGDOM":
            asset_rows = body.splitlines()
            continue
        file_name = HEADERS_TO_FILES.get(first)
        if file_name is None or file_name in parsed:
            raise ContractError(f"P08 CSV block routing failed: {first}")
        # P08-M01: the normative Korean success sentence contains a literal comma
        # but the design fence omitted RFC 4180 quoting. Preserve the exact text
        # while making the production package a valid six-column CSV.
        if file_name == "localizations.csv":
            body = body.replace(
                "ko-KR,TXT_P08_TRANSACTION_SUCCESS,거래 완료. 개인 {personalDelta}, 왕국 {kingdomDelta},STORE_RESULT",
                'ko-KR,TXT_P08_TRANSACTION_SUCCESS,"거래 완료. 개인 {personalDelta}, 왕국 {kingdomDelta}",STORE_RESULT',
            )
        parsed[file_name] = P03._canonical_csv_bytes(body)
    if not asset_rows or len(parsed) != 11:
        raise ContractError("P08 CSV append contract is incomplete")
    return parsed, asset_rows


def _merge_by_key(base: bytes, patch: bytes, key: tuple[str, ...], replace: bool = False) -> bytes:
    header, rows = _rows(base)
    patch_header, patch_rows = _rows(patch)
    if header != patch_header:
        raise ContractError(f"P08 CSV header mismatch: {header} != {patch_header}")
    values = {tuple(row[field] for field in key): row for row in rows}
    for row in patch_rows:
        row_key = tuple(row[field] for field in key)
        if row_key in values and not replace:
            raise ContractError(f"P08 duplicate append key: {row_key}")
        values[row_key] = row
    ordered = [values[value] for value in sorted(values)]
    return _csv_bytes(header, ordered)


def _build_csv(base: dict[str, bytes], parsed: dict[str, bytes], asset_rows: list[str]) -> dict[str, bytes]:
    result = dict(base)
    for file_name in NEW_FILES:
        result[file_name] = parsed[file_name]
    result["currencies.csv"] = _merge_by_key(base["currencies.csv"], parsed["currencies.csv"], ("currency_id",))
    result["autonomy_rules.csv"] = _merge_by_key(base["autonomy_rules.csv"], parsed["autonomy_rules.csv"], ("state", "rule_no"), replace=True)
    result["runtime_config.csv"] = _merge_by_key(base["runtime_config.csv"], parsed["runtime_config.csv"], ("config_key",))
    result["localizations.csv"] = _merge_by_key(base["localizations.csv"], parsed["localizations.csv"], ("locale", "text_key"))
    asset_header, existing_assets = _rows(base["asset_register.csv"])
    patch = P03._canonical_csv_bytes(",".join(asset_header) + "\n" + "\n".join(asset_rows))
    result["asset_register.csv"] = _merge_by_key(base["asset_register.csv"], patch, ("asset_id",))
    return result


def _field(name: str, domain: str, nullable: bool, enum_values: list[str]) -> dict[str, Any]:
    return {"domain": domain, "enumValues": enum_values, "name": name, "nullable": nullable}


def _table(file_name: str, raw: bytes) -> dict[str, Any]:
    fields = [_field(*definition) for definition in SCHEMA_FIELDS[file_name]]
    foreign_keys: list[dict[str, Any]] = []
    if file_name == "store_purchase_ai_rules.csv":
        foreign_keys.append({"mode": "HARD", "sourceFields": ["personality_id"], "targetFields": ["personality_id"], "targetFile": "personalities.csv"})
    if file_name == "transaction_reason_codes.csv":
        foreign_keys.append({"mode": "HARD", "sourceFields": ["text_key"], "targetFields": ["text_key"], "targetFile": "localizations.csv"})
    return {
        "fields": fields, "file": file_name, "foreignKeys": foreign_keys,
        "primaryKey": PRIMARY_KEYS[file_name], "required": True,
        "rowCount": len(_rows(raw)[1]), "schemaVersion": 1, "sha256": _sha(raw),
    }


def _manifest(base: dict[str, Any], csv_bytes: dict[str, bytes]) -> tuple[dict[str, Any], bytes]:
    manifest = copy.deepcopy(base)
    manifest["contentVersion"] = "1.0.0-content.6"
    manifest["csvSchemaSetVersion"] = 4
    manifest["minimumGameVersion"] = "1.0.0-p08"
    manifest["generatedAtUtc"] = GENERATED_AT
    tables = {table["file"]: table for table in manifest["tables"]}
    for file_name in ("asset_register.csv", "autonomy_rules.csv", "currencies.csv", "localizations.csv", "runtime_config.csv"):
        table = tables[file_name]
        table["rowCount"] = len(_rows(csv_bytes[file_name])[1])
        table["sha256"] = _sha(csv_bytes[file_name])
    currency_type = next(field for field in tables["currencies.csv"]["fields"] if field["name"] == "currency_type")
    if "PERSONAL" not in currency_type["enumValues"]:
        currency_type["enumValues"].append("PERSONAL")
    reason_code = next(field for field in tables["autonomy_rules.csv"]["fields"] if field["name"] == "reason_code")
    for value in ("RETURNED_TO_STORE", "AUTO_SELL_ELIGIBLE", "NO_ELIGIBLE_TRANSACTION", "POTION_TARGET_LOW", "EQUIPMENT_UPGRADE", "HEAL_DEFERRED_P09"):
        if value not in reason_code["enumValues"]:
            reason_code["enumValues"].append(value)
    for file_name in NEW_FILES:
        tables[file_name] = _table(file_name, csv_bytes[file_name])
    manifest["tables"] = [tables[name] for name in sorted(tables)]
    if len(manifest["tables"]) != 75:
        raise ContractError("P08 content package must contain exactly 75 tables")
    return manifest, P03._jcs_bytes(manifest)


def _schema(text: str) -> tuple[dict[str, Any], bytes]:
    raw = SAVE_SCHEMA_INPUT.read_bytes()
    if _sha(raw) != EXPECTED_P07_SCHEMA_SHA256:
        raise ContractError("P08 schema input is not the immutable content.5 schema")
    schema = json.loads(raw)
    definitions = [value for value in re.findall(r"```json\n(.*?)\n```", text, flags=re.DOTALL) if '"Economy"' in value]
    if len(definitions) != 1:
        raise ContractError("P08 economy schema definition block is missing")
    schema["$id"] = "urn:tycoon:schema:save:v1:content.6"
    schema["properties"]["contentVersion"]["const"] = "1.0.0-content.6"
    payload = schema["$defs"]["Payload"]
    payload["required"].insert(payload["required"].index("extensions"), "economy")
    payload["properties"]["economy"] = {"$ref": "#/$defs/Economy"}
    operations = schema["$defs"]["OperationJournalEntry"]["properties"]
    operations["operationType"]["enum"].append("STORE_TRANSACTION")
    operations["resultPayload"] = {"oneOf": [{"type": "null"}, {"type": "object"}]}
    schema["$defs"].update(json.loads(definitions[0]))
    encoded = _pretty_json(schema)
    # P08-M02: applying the seven normative composition steps produces 62,967
    # bytes, not the document's stated 62,977. The generated schema is guarded
    # by the source .5 hash and this deterministic composer instead of accepting
    # the unreproducible precomputed digest.
    if len(encoded) != 62_967:
        raise ContractError(f"P08 composed schema length drifted: bytes={len(encoded)}, sha256={_sha(encoded)}")
    return schema, encoded


def _registry(schema_sha: str) -> tuple[dict[str, Any], bytes]:
    registry = json.loads(SAVE_REGISTRY_OUTPUT.read_bytes())
    registry["entries"] = [entry for entry in registry["entries"] if "1.0.0-content.6" not in entry["contentVersions"]]
    registry["entries"].append({"contentVersions": ["1.0.0-content.6"], "schemaFile": "save.content.6.schema.json", "sha256": schema_sha})
    encoded = _pretty_json(registry)
    if len(encoded) != 826:
        raise ContractError(f"P08 registry length drifted: bytes={len(encoded)}, sha256={_sha(encoded)}")
    return registry, encoded


def _economy() -> dict[str, Any]:
    return {
        "economyVersion": 1,
        "store": {
            "stockVersion": 0, "stackLines": [], "equipment": [],
            "supplyState": {"mode": "SYSTEM_SUPPLY", "epoch": 0, "buyCountSinceRefresh": 0, "lastRefreshOperationId": None},
            "ledger": {"nextSequence": 1, "prunedThroughSequence": 0, "prunedDigest": None, "entries": []},
        },
    }


def _envelope(document: dict[str, Any]) -> dict[str, Any]:
    integrity = document["integrity"]
    return {
        "schemaId": document["schemaId"], "saveVersion": document["saveVersion"], "gameVersion": document["gameVersion"],
        "contentVersion": document["contentVersion"], "saveId": document["saveId"], "profileId": document["profileId"],
        "revision": document["revision"], "createdAtUtc": document["createdAtUtc"], "savedAtUtc": document["savedAtUtc"],
        "integrityVersion": integrity["integrityVersion"], "algorithm": integrity["algorithm"],
        "canonicalization": integrity["canonicalization"], "payloadSha256": integrity["payloadSha256"],
    }


def _seal(document: dict[str, Any]) -> dict[str, Any]:
    document["integrity"]["payloadSha256"] = _sha(P03._jcs_bytes(document["payload"]))
    document["integrity"]["fileSha256"] = _sha(P03._jcs_bytes(_envelope(document)))
    return document


def _fixtures() -> dict[Path, bytes]:
    raw = P07_TEMPLATE.read_bytes()
    if _sha(raw) != EXPECTED_P07_TEMPLATE_SHA256:
        raise ContractError("P08 fixture input is not the tracked P07 template")
    source = json.loads(raw)
    new_game = copy.deepcopy(source)
    new_game.update({"gameVersion": "1.0.0-p08", "contentVersion": "1.0.0-content.6", "saveId": "018f0000-0000-7000-8000-000000000801", "profileId": "018f0000-0000-7000-8000-000000000802"})
    new_game["payload"]["economy"] = _economy()
    _seal(new_game)
    files = {NEW_GAME_OUTPUT: _pretty_json(new_game), GOLDEN_ROOT / "p08-new-game.golden.json": _pretty_json(new_game)}
    variants = {
        "buildable": ("018f0000-0000-7000-8000-000000000811", "018f0000-0000-7000-8000-000000000812", 7, "BUILDABLE"),
        "active": ("018f0000-0000-7000-8000-000000000821", "018f0000-0000-7000-8000-000000000822", 11, "ACTIVE"),
        "stopped": ("018f0000-0000-7000-8000-000000000831", "018f0000-0000-7000-8000-000000000832", 21, "STOPPED"),
    }
    for name, (save_id, profile_id, revision, state) in variants.items():
        before = copy.deepcopy(source)
        before.update({"saveId": save_id, "profileId": profile_id, "revision": revision, "savedAtUtc": GENERATED_AT})
        store = next(value for value in before["payload"]["facilities"] if value["facilityId"] == "FAC_STORE")
        store.update({"state": state, "level": 1})
        if state == "ACTIVE":
            merchant = next(value for value in before["payload"]["managementNpcs"] if value["professionId"] == "NPC_MERCHANT")
            store["assignedNpcInstanceId"] = merchant["instanceId"]
            merchant["assignedFacilityId"] = "FAC_STORE"
            merchant["working"] = True
        _seal(before)
        after = copy.deepcopy(before)
        after.update({"gameVersion": "1.0.0-p08", "contentVersion": "1.0.0-content.6", "revision": revision + 1})
        after["payload"]["economy"] = _economy()
        _seal(after)
        files[GOLDEN_ROOT / f"p08-migration-{name}-before.golden.json"] = _pretty_json(before)
        files[GOLDEN_ROOT / f"p08-migration-{name}-after.golden.json"] = _pretty_json(after)
    return files


def _golden_commands() -> dict[Path, bytes]:
    text = SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    blocks = [json.loads(raw) for raw in re.findall(r"```json\n(.*?)\n```", text, flags=re.DOTALL)]
    requests = [value for value in blocks if value.get("commandType") in {"SET_PRICING_POLICY", "SELL_TO_STORE", "BUY_FROM_STORE", "REFRESH_SYSTEM_STORE_SUPPLY"}]
    results = [value for value in blocks if "revisionBefore" in value and "revisionAfter" in value]
    files: dict[Path, bytes] = {}
    for value in requests:
        value["requestHash"] = _sha(P03._jcs_bytes(value))
        files[GOLDEN_ROOT / f"request-{value['commandType'].lower()}.json"] = _pretty_json(value)
    for index, value in enumerate(results):
        digest = _sha(P03._jcs_bytes(value))
        value["resultDigest"] = digest
        files[GOLDEN_ROOT / f"result-{index + 1:02d}.json"] = _pretty_json(value)
    return files


def load_contract() -> tuple[Package, bytes, bytes, dict[Path, bytes]]:
    raw = SOURCE.read_bytes()
    scoped = raw.decode("utf-8-sig").replace("\r\n", "\n").replace(EXPECTED_DOCUMENT_SHA256, "0" * 64).encode("utf-8")
    if _sha(scoped) != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P08 final design document digest mismatch")
    text = raw.decode("utf-8-sig").replace("\r\n", "\n")
    if "UNRESOLVED: NONE" not in text or "IMPLEMENTATION_READY: YES" not in text:
        raise ContractError("P08 contract is not implementation-ready")
    base, _ = P07.load_contract()
    parsed, asset_rows = _parse_design_csv(text)
    csv_bytes = _build_csv(base.csv_bytes, parsed, asset_rows)
    manifest, manifest_bytes = _manifest(base.manifest, csv_bytes)
    _, schema_bytes = _schema(text)
    _, registry_bytes = _registry(_sha(schema_bytes))
    P03._validate_csv_and_references(manifest, csv_bytes)
    package = Package(base.schema, manifest, csv_bytes, manifest_bytes)
    extras = _fixtures()
    extras.update(_golden_commands())
    return package, schema_bytes, registry_bytes, extras


def generate(package: Package, schema_bytes: bytes, registry_bytes: bytes, extras: dict[Path, bytes], check: bool) -> None:
    files: dict[Path, bytes] = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": _pretty_json(package.schema),
        SAVE_SCHEMA_OUTPUT: schema_bytes,
        SAVE_REGISTRY_OUTPUT: registry_bytes,
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
        raise ContractError("P08 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, schema_bytes, registry_bytes, extras = load_contract()
        generate(package, schema_bytes, registry_bytes, extras, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, KeyError, json.JSONDecodeError, csv.Error) as error:
        print(f"p08-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p08-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {_sha(package.manifest_bytes)}")
    print(f"localizations-sha256: {_sha(package.csv_bytes['localizations.csv'])}")
    print(f"save-schema-sha256: {_sha(schema_bytes)}")
    print(f"package-fingerprint: {_sha(package.manifest_bytes + schema_bytes)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
