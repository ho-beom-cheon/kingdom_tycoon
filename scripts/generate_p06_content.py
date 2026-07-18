#!/usr/bin/env python3
"""Build and verify the immutable P06 combat content package and Save goldens."""

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
P06_SOURCE = ROOT / "docs" / "design" / "TYCOON_P06_MOVEMENT_COMBAT_AI_COMPLETE_DESIGN_v1.1.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.4"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
NEW_GAME_OUTPUT = CONTRACT_ROOT / "p06-new-game.template.json"
MIGRATION_BEFORE_OUTPUT = CONTRACT_ROOT / "p06-migration-before.golden.json"
MIGRATION_AFTER_OUTPUT = CONTRACT_ROOT / "p06-migration-after.golden.json"
EXPECTED_SOURCE_MANIFEST_LENGTH = 72_812
EXPECTED_SOURCE_MANIFEST_SHA256 = "062f09739be767ea8ea469087ae0a4c4af6630a2cb898be590fb6790888dd5ef"
EXPECTED_MANIFEST_LENGTH = 72_741
EXPECTED_MANIFEST_SHA256 = "d541f924b230eb9b21e1e2a46fe9c5f30d72d75eddacef8cb2c05bd620151fb5"
EXPECTED_SCHEMA_LENGTH = 2_724
EXPECTED_SCHEMA_SHA256 = "451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41"
EXPECTED_SAVE_VECTORS = (
    (1, "1.0.0-content.4", 7_195, "3261e57e670ab82d5dec52557af69b3f174456057410a2c149ea944acaa7d9e9", "95a9509be823ac6b9f1bd3f093493eec727bed5ae6043c2da50149006c79c759"),
    (1, "1.0.0-content.3", 7_195, "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b", "a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a"),
    (2, "1.0.0-content.4", 7_195, "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b", "42fa5de8ab52657b1d721a948ba41607f61b552b1cc892f485ca5b7bec100aa3"),
)
EXPECTED_START_HASH = "97f7c3d1c799948c48cbd00ffe54188ed4b7084c3544a13886620deb2a94faa1"
EXPECTED_RECALL_HASH = "027b3f13dd4dc316455ae6b668d5f8cbcf6d4805f472b16d3303eb7bebb4e920"
EXPECTED_TERMINAL_RESULT_DIGEST = "a4503d54e3c8082d7ab49a019f92bf8eb5673e869b494c822c8434c79fe692e8"


def _load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load module: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


P05 = _load_module("kingdom_tycoon_p05_content", ROOT / "scripts" / "generate_p05_content.py")
P04 = P05.P04
P03 = P05.P03
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
    if len(blocks) != 10 or blocks[0] != blocks[2] or blocks[1] != blocks[9]:
        raise ContractError("P06 repeated autonomy and asset CSV contracts must be byte-identical")
    selected = (blocks[1], *blocks[2:9])
    expected_files = (
        "asset_register.csv",
        "autonomy_rules.csv",
        "combat_ai_profiles.csv",
        "combat_job_profiles.csv",
        "localizations.csv",
        "region_encounter_profiles.csv",
        "runtime_config.csv",
        "skill_runtime_rules.csv",
    )
    result: dict[str, bytes] = {}
    for file_name, body in zip(expected_files, selected, strict=True):
        result[file_name] = P03._canonical_csv_bytes(body)
    return result


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
        raise ContractError("P06 Save vector metadata/length mismatch")
    if actual_payload_sha256 != payload_sha256 or document["integrity"]["payloadSha256"] != payload_sha256:
        raise ContractError("P06 Save payload golden mismatch")
    if actual_envelope_sha256 != envelope_sha256 or document["integrity"]["fileSha256"] != envelope_sha256:
        raise ContractError("P06 Save envelope golden mismatch")


def _request_hash(command: dict[str, Any]) -> str:
    digest_input = {key: value for key, value in command.items() if key != "requestHash"}
    return hashlib.sha256(P03._jcs_bytes(digest_input)).hexdigest()


def _read_rows(raw: bytes) -> list[dict[str, str]]:
    return list(csv.DictReader(io.StringIO(raw.decode("utf-8"), newline=""), strict=True))


def _apply_manifest_correction(source_manifest: dict[str, Any]) -> dict[str, Any]:
    """Remove the single duplicated descriptor typo while preserving CSV bytes."""
    manifest = json.loads(json.dumps(source_manifest, ensure_ascii=False))
    table = next(item for item in manifest["tables"] if item["file"] == "combat_ai_profiles.csv")
    duplicate = [field for field in table["fields"] if field["name"] == "status"]
    if [(field["domain"], field["nullable"]) for field in duplicate] != [("SAFE_INT", False), ("STATUS", False)]:
        raise ContractError("P06 combat_ai_profiles manifest correction precondition failed")
    table["fields"] = [
        field
        for field in table["fields"]
        if not (field["name"] == "status" and field["domain"] == "SAFE_INT")
    ]
    return manifest


def _validate_p06_semantics(csv_bytes: dict[str, bytes]) -> None:
    autonomy = _read_rows(csv_bytes["autonomy_rules.csv"])
    if len(autonomy) != 37 or len({row["state"] for row in autonomy}) != 17:
        raise ContractError("P06 autonomy matrix must contain 37 rules for all 17 states")
    if any(row["enabled"] not in {"TRUE", "FALSE"} for row in autonomy):
        raise ContractError("P06 autonomy enabled flag is invalid")

    ai_profiles = _read_rows(csv_bytes["combat_ai_profiles.csv"])
    job_profiles = _read_rows(csv_bytes["combat_job_profiles.csv"])
    if {row["job_id"] for row in ai_profiles} != {row["job_id"] for row in job_profiles} or len(ai_profiles) != 5:
        raise ContractError("P06 combat profiles must cover the same five jobs")

    encounters = _read_rows(csv_bytes["region_encounter_profiles.csv"])
    if len(encounters) != 5 or {row["region_id"] for row in encounters} != {"REGION_R01"}:
        raise ContractError("P06 production encounter coverage must be R01-only")
    if sum(int(row["weight"]) for row in encounters) != 100:
        raise ContractError("P06 R01 encounter weights must sum to 100")

    skills = _read_rows(csv_bytes["skill_runtime_rules.csv"])
    if len(skills) != 15 or len({row["skill_id"] for row in skills}) != 15:
        raise ContractError("P06 skill runtime rules must cover all 15 skills")

    runtime = {row["config_key"]: row for row in _read_rows(csv_bytes["runtime_config.csv"])}
    required = {
        "P06_SIMULATION_TICK_HZ": "10",
        "P06_MAX_RUNTIME_ENTITIES": "60",
        "P06_PATH_EXPANSIONS_PER_TICK": "240",
        "P06_PATH_CACHE_CAPACITY": "128",
    }
    if any(key not in runtime or runtime[key]["value"] != value for key, value in required.items()):
        raise ContractError("P06 runtime configuration contract mismatch")


def load_contract() -> tuple[Package, list[dict[str, Any]]]:
    text = P06_SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    if "**상태: CONFIRMED**" not in text or "UNRESOLVED: NONE" not in text or "IMPLEMENTATION_READY: YES" not in text:
        raise ContractError("P06 contract is not implementation-ready")

    blocks = _json_blocks(text)
    if len(blocks) != 10:
        raise ContractError(f"P06 must contain ten executable JSON blocks, found {len(blocks)}")
    source_manifest = blocks[0]
    saves = blocks[1:4]
    start_command, start_result, start_journal = blocks[4:7]
    recall_command, recall_result, recall_journal = blocks[7:10]

    source_manifest_bytes = P03._jcs_bytes(source_manifest)
    if len(source_manifest_bytes) != EXPECTED_SOURCE_MANIFEST_LENGTH or hashlib.sha256(source_manifest_bytes).hexdigest() != EXPECTED_SOURCE_MANIFEST_SHA256:
        raise ContractError("P06 source manifest RFC8785 golden mismatch")
    manifest = _apply_manifest_correction(source_manifest)
    manifest_bytes = P03._jcs_bytes(manifest)
    if len(manifest_bytes) != EXPECTED_MANIFEST_LENGTH or hashlib.sha256(manifest_bytes).hexdigest() != EXPECTED_MANIFEST_SHA256:
        raise ContractError("P06 manifest RFC8785 golden mismatch")
    if manifest["contentVersion"] != "1.0.0-content.4" or manifest["csvSchemaSetVersion"] != 3 or len(manifest["tables"]) != 66:
        raise ContractError("P06 release metadata mismatch")
    for document, expected in zip(saves, EXPECTED_SAVE_VECTORS, strict=True):
        _validate_save_vector(document, expected)

    if _request_hash(start_command) != EXPECTED_START_HASH or start_command["requestHash"] != EXPECTED_START_HASH:
        raise ContractError("P06 START_HUNT requestHash golden mismatch")
    if _request_hash(recall_command) != EXPECTED_RECALL_HASH or recall_command["requestHash"] != EXPECTED_RECALL_HASH:
        raise ContractError("P06 RECALL_HUNT requestHash golden mismatch")
    if recall_result["resultDigest"] != EXPECTED_TERMINAL_RESULT_DIGEST or recall_journal["resultDigest"] != EXPECTED_TERMINAL_RESULT_DIGEST:
        raise ContractError("P06 terminal result digest mismatch")
    if start_journal["requestHash"] != EXPECTED_START_HASH or recall_journal["requestHash"] != EXPECTED_RECALL_HASH:
        raise ContractError("P06 journal request hash mismatch")
    if start_result["canonicalPartyMercenaryInstanceIds"] != start_command["partyMercenaryInstanceIds"]:
        raise ContractError("P06 START_HUNT canonical party order mismatch")

    base, _, _ = P05.load_contract()
    schema_bytes = P03._jcs_bytes(base.schema)
    if len(schema_bytes) != EXPECTED_SCHEMA_LENGTH or hashlib.sha256(schema_bytes).hexdigest() != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P06 inherited schema golden mismatch")

    csv_bytes = dict(base.csv_bytes)
    csv_bytes.update(_csv_blocks(text))
    P03._validate_csv_and_references(manifest, csv_bytes)
    P04._validate_domains(manifest, csv_bytes)
    P04._validate_p04_semantics(csv_bytes)
    _validate_p06_semantics(csv_bytes)
    return Package(base.schema, manifest, csv_bytes, manifest_bytes), blocks


def _pretty_json(value: dict[str, Any]) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _expected_files(package: Package, blocks: list[dict[str, Any]]) -> dict[Path, bytes]:
    files = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": _pretty_json(package.schema),
        NEW_GAME_OUTPUT: _pretty_json(blocks[1]),
        MIGRATION_BEFORE_OUTPUT: _pretty_json(blocks[2]),
        MIGRATION_AFTER_OUTPUT: _pretty_json(blocks[3]),
    }
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    return files


def generate(package: Package, blocks: list[dict[str, Any]], check: bool) -> None:
    failures: list[str] = []
    expected = _expected_files(package, blocks)
    for path, contents in expected.items():
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
    expected_versions = {f"1.0.0-content.{version}" for version in range(1, 5)}
    if check and not expected_versions.issubset(versions):
        failures.append(f"versioned package tree invalid: versions={versions}")
    if failures:
        raise ContractError("P06 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, blocks = load_contract()
        generate(package, blocks, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, json.JSONDecodeError, csv.Error) as error:
        print(f"p06-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p06-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {hashlib.sha256(package.manifest_bytes).hexdigest()}")
    print(f"localizations-sha256: {hashlib.sha256(package.csv_bytes['localizations.csv']).hexdigest()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
