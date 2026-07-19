#!/usr/bin/env python3
"""Build and verify the immutable P07 inventory content and Save contracts."""

from __future__ import annotations

import argparse
import csv
import hashlib
import importlib.util
import io
import json
import re
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "TYCOON_P07_LOOT_INVENTORY_EQUIPMENT_COMPLETE_DESIGN_v1.1.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.5"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
SAVE_SCHEMA_OUTPUT = CONTRACT_ROOT / "save.content.5.schema.json"
SAVE_REGISTRY_OUTPUT = CONTRACT_ROOT / "save.schema.registry.json"
NEW_GAME_OUTPUT = CONTRACT_ROOT / "p07-new-game.template.json"
MIGRATION_BEFORE_OUTPUT = CONTRACT_ROOT / "p07-migration-before.golden.json"
MIGRATION_AFTER_OUTPUT = CONTRACT_ROOT / "p07-migration-after.golden.json"

EXPECTED_DOCUMENT_SHA256 = "52f4717e5b9a11204c0389d21adc32d99046dc7a25c2cba440c518791f000a3c"
EXPECTED_SOURCE_MANIFEST_LENGTH = 74_609
EXPECTED_SOURCE_MANIFEST_SHA256 = "69f79179b2dac1f57d2e59bc079426434d0ae975d24cd2a86efebdd7963ee866"
EXPECTED_MANIFEST_LENGTH = 74_538
EXPECTED_MANIFEST_SHA256 = "692922c62883a7da9520b7d9bc586b451f5b94b7461e3909dbca23e1cfcecef4"
EXPECTED_SAVE_SCHEMA_LENGTH = 53_081
EXPECTED_SAVE_SCHEMA_SHA256 = "d306ae8c05d11f9225bfb988db3d3388f56177807b176ed7e44caa9ee6bc8392"
EXPECTED_SAVE_VECTORS = (
    (1, "1.0.0-content.5", 7_529, "549da561612263e7c5312dab6f1178d76fa40b81c76680a755630664972ffa1f", "290e7c6984115f73aacd271ffc687fd4d7622b7ad3b21a891401b9495c8a76f4"),
    (2, "1.0.0-content.4", 7_195, "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b", "42fa5de8ab52657b1d721a948ba41607f61b552b1cc892f485ca5b7bec100aa3"),
    (3, "1.0.0-content.5", 7_529, "805f4d1fb388e0a1fbf4259e94d3f5c6e4d5f294b76c7e70d21b2a892c21afef", "43c64e452f62c88fbb6c626d29f1167ee91230477b7f7cf5bd1630f585727334"),
)


def _load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load module: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


P06 = _load_module("kingdom_tycoon_p06_content", ROOT / "scripts" / "generate_p06_content.py")
P04 = P06.P04
P03 = P06.P03
ContractError = P03.ContractError
Package = P03.Package


def _json_blocks(text: str) -> list[dict[str, Any]]:
    return [
        value
        for raw in re.findall(r"```json\n(.*?)\n```", text, flags=re.DOTALL)
        if isinstance((value := json.loads(raw)), dict)
    ]


def _csv_blocks(text: str) -> dict[str, bytes]:
    blocks = re.findall(r"```csv\n(.*?)\n```", text, flags=re.DOTALL)
    if len(blocks) != 6 or blocks[0] != blocks[5]:
        raise ContractError("P07 repeated asset register contracts must be byte-identical")
    expected_files = (
        "asset_register.csv",
        "equipment_score_weights.csv",
        "inventory_capacity_rules.csv",
        "localizations.csv",
        "runtime_config.csv",
    )
    return {
        file_name: P03._canonical_csv_bytes(body)
        for file_name, body in zip(expected_files, blocks[:5], strict=True)
    }


def _pretty_json(value: dict[str, Any]) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _envelope_digest_input(document: dict[str, Any]) -> dict[str, Any]:
    integrity = document["integrity"]
    return {
        "schemaId": document["schemaId"],
        "saveVersion": document["saveVersion"],
        "gameVersion": document["gameVersion"],
        "contentVersion": document["contentVersion"],
        "saveId": document["saveId"],
        "profileId": document["profileId"],
        "revision": document["revision"],
        "createdAtUtc": document["createdAtUtc"],
        "savedAtUtc": document["savedAtUtc"],
        "integrityVersion": integrity["integrityVersion"],
        "algorithm": integrity["algorithm"],
        "canonicalization": integrity["canonicalization"],
        "payloadSha256": integrity["payloadSha256"],
    }


def _validate_save_vector(document: dict[str, Any], expected: tuple[int, str, int, str, str]) -> None:
    revision, content_version, payload_length, payload_sha256, envelope_sha256 = expected
    payload = P03._jcs_bytes(document["payload"])
    actual_payload_sha256 = hashlib.sha256(payload).hexdigest()
    actual_envelope_sha256 = hashlib.sha256(P03._jcs_bytes(_envelope_digest_input(document))).hexdigest()
    if document["revision"] != revision or document["contentVersion"] != content_version or len(payload) != payload_length:
        raise ContractError("P07 Save vector metadata/length mismatch")
    if actual_payload_sha256 != payload_sha256 or document["integrity"]["payloadSha256"] != payload_sha256:
        raise ContractError("P07 Save payload golden mismatch")
    if actual_envelope_sha256 != envelope_sha256 or document["integrity"]["fileSha256"] != envelope_sha256:
        raise ContractError("P07 Save envelope golden mismatch")


def _digest_without(value: dict[str, Any], field: str) -> str:
    return hashlib.sha256(P03._jcs_bytes({key: item for key, item in value.items() if key != field})).hexdigest()


def _read_rows(raw: bytes) -> list[dict[str, str]]:
    return list(csv.DictReader(io.StringIO(raw.decode("utf-8"), newline=""), strict=True))


def _validate_semantics(csv_bytes: dict[str, bytes]) -> None:
    weights = _read_rows(csv_bytes["equipment_score_weights.csv"])
    if len(weights) != 5 or {row["job_id"] for row in weights} != {
        "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC"
    }:
        raise ContractError("P07 equipment weights must cover all five jobs")
    if any(
        not 0 <= int(row[key]) <= 20_000
        for row in weights
        for key in row
        if key.endswith("_weight_bps") or key == "price_penalty_bps"
    ):
        raise ContractError("P07 equipment score weights exceed the fixed-point domain")

    capacities = _read_rows(csv_bytes["inventory_capacity_rules.csv"])
    if [int(row["warehouse_level"]) for row in capacities] != [1, 2, 3, 4]:
        raise ContractError("P07 inventory capacity rows must cover warehouse levels 1..4")
    for field in ("item_stack_slots", "equipment_slots", "potion_stack_slots", "hunt_buffer_slots"):
        values = [int(row[field]) for row in capacities]
        if values != sorted(values):
            raise ContractError(f"P07 capacity must be monotonic: {field}")

    runtime = {row["config_key"]: row["value"] for row in _read_rows(csv_bytes["runtime_config.csv"])}
    required = {"P07_MAX_LIVE_ITEM_VIEWS": "30", "P07_MAX_PAGE_SIZE": "100"}
    if any(runtime.get(key) != value for key, value in required.items()):
        raise ContractError("P07 runtime configuration contract mismatch")


def load_contract() -> tuple[Package, list[dict[str, Any]]]:
    raw = SOURCE.read_bytes()
    marker = b"DOCUMENT_CONTENT_SHA256_SCOPE:"
    marker_start = raw.rfind(b"\n", 0, raw.index(marker)) + 1
    if hashlib.sha256(raw[:marker_start]).hexdigest() != EXPECTED_DOCUMENT_SHA256:
        raise ContractError("P07 standalone design document digest mismatch")

    text = raw.decode("utf-8-sig").replace("\r\n", "\n")
    if "**상태: CONFIRMED**" not in text or "UNRESOLVED: NONE" not in text or "IMPLEMENTATION_READY: YES" not in text:
        raise ContractError("P07 contract is not implementation-ready")
    blocks = _json_blocks(text)
    if len(blocks) != 21:
        raise ContractError(f"P07 must contain 21 executable JSON blocks, found {len(blocks)}")

    for index in range(0, 15, 3):
        request, result, journal = blocks[index:index + 3]
        if _digest_without(request, "requestHash") != request["requestHash"]:
            raise ContractError(f"P07 {request.get('commandType')} request hash mismatch")
        if _digest_without(result, "resultDigest") != result["resultDigest"] or journal["resultDigest"] != result["resultDigest"]:
            raise ContractError(f"P07 {request.get('commandType')} result digest mismatch")
        if journal["requestHash"] != request["requestHash"] or journal["operationId"] != request["operationId"]:
            raise ContractError(f"P07 {request.get('commandType')} journal mismatch")

    source_manifest, registry, save_schema = blocks[15:18]
    save_vectors = blocks[18:21]
    source_manifest_bytes = P03._jcs_bytes(source_manifest)
    if len(source_manifest_bytes) != EXPECTED_SOURCE_MANIFEST_LENGTH or hashlib.sha256(source_manifest_bytes).hexdigest() != EXPECTED_SOURCE_MANIFEST_SHA256:
        raise ContractError("P07 source manifest RFC8785 golden mismatch")
    manifest = P06._apply_manifest_correction(source_manifest)
    manifest_bytes = P03._jcs_bytes(manifest)
    if len(manifest_bytes) != EXPECTED_MANIFEST_LENGTH or hashlib.sha256(manifest_bytes).hexdigest() != EXPECTED_MANIFEST_SHA256:
        raise ContractError("P07 corrected manifest RFC8785 golden mismatch")
    if manifest["contentVersion"] != "1.0.0-content.5" or manifest["csvSchemaSetVersion"] != 3 or len(manifest["tables"]) != 68:
        raise ContractError("P07 release metadata mismatch")
    save_schema_bytes = _pretty_json(save_schema)
    if len(save_schema_bytes) != EXPECTED_SAVE_SCHEMA_LENGTH or hashlib.sha256(save_schema_bytes).hexdigest() != EXPECTED_SAVE_SCHEMA_SHA256:
        raise ContractError("P07 full Save schema golden mismatch")
    if registry["entries"][1]["sha256"] != EXPECTED_SAVE_SCHEMA_SHA256:
        raise ContractError("P07 Save registry does not select the .5 schema")
    for document, expected in zip(save_vectors, EXPECTED_SAVE_VECTORS, strict=True):
        _validate_save_vector(document, expected)

    base, _ = P06.load_contract()
    csv_bytes = dict(base.csv_bytes)
    csv_bytes.update(_csv_blocks(text))
    P03._validate_csv_and_references(manifest, csv_bytes)
    P04._validate_domains(manifest, csv_bytes)
    P04._validate_p04_semantics(csv_bytes)
    P06._validate_p06_semantics(csv_bytes)
    _validate_semantics(csv_bytes)
    return Package(base.schema, manifest, csv_bytes, manifest_bytes), blocks


def _expected_files(package: Package, blocks: list[dict[str, Any]]) -> dict[Path, bytes]:
    files = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": _pretty_json(package.schema),
        SAVE_REGISTRY_OUTPUT: _pretty_json(blocks[16]),
        SAVE_SCHEMA_OUTPUT: _pretty_json(blocks[17]),
        NEW_GAME_OUTPUT: _pretty_json(blocks[18]),
        MIGRATION_BEFORE_OUTPUT: _pretty_json(blocks[19]),
        MIGRATION_AFTER_OUTPUT: _pretty_json(blocks[20]),
    }
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    return files


def _registry_preserves_p07(actual_bytes: bytes, expected_bytes: bytes) -> bool:
    try:
        actual = json.loads(actual_bytes)
        expected = json.loads(expected_bytes)
    except (UnicodeDecodeError, json.JSONDecodeError):
        return False
    return (
        actual.get("registryVersion") == expected.get("registryVersion")
        and actual.get("preparseRequired") == expected.get("preparseRequired")
        and all(entry in actual.get("entries", []) for entry in expected.get("entries", []))
    )


def generate(package: Package, blocks: list[dict[str, Any]], check: bool) -> None:
    failures: list[str] = []
    expected = _expected_files(package, blocks)
    for path, contents in expected.items():
        if path == SAVE_REGISTRY_OUTPUT and path.is_file() and _registry_preserves_p07(path.read_bytes(), contents):
            continue
        if path.is_file() and path.read_bytes() != contents:
            failures.append(f"CONTENT_RELEASE_SPLIT_BRAIN: {path.relative_to(ROOT)}")
        elif check and not path.is_file():
            failures.append(f"missing: {path.relative_to(ROOT)}")
        elif not check and not path.is_file():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)

    expected_names = {path.name for path in expected if path.parent == OUTPUT}
    if OUTPUT.exists():
        actual_names = {path.name for path in OUTPUT.iterdir() if path.is_file() and not path.name.endswith(".meta")}
        for stale in sorted(actual_names - expected_names):
            failures.append(f"stale: {(OUTPUT / stale).relative_to(ROOT)}")
    versions = sorted(path.name for path in CONTENT_ROOT.iterdir() if path.is_dir()) if CONTENT_ROOT.exists() else []
    expected_versions = {f"1.0.0-content.{version}" for version in range(1, 6)}
    if check and not expected_versions.issubset(versions):
        failures.append(f"versioned package tree invalid: versions={versions}")
    if failures:
        raise ContractError("P07 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, blocks = load_contract()
        generate(package, blocks, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, json.JSONDecodeError, csv.Error) as error:
        print(f"p07-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p07-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {hashlib.sha256(package.manifest_bytes).hexdigest()}")
    print(f"localizations-sha256: {hashlib.sha256(package.csv_bytes['localizations.csv']).hexdigest()}")
    print(f"save-schema-sha256: {EXPECTED_SAVE_SCHEMA_SHA256}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
