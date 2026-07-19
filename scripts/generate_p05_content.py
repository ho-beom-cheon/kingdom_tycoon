#!/usr/bin/env python3
"""Build and verify the immutable P05 content package and Save goldens."""

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
P05_SOURCE = ROOT / "docs" / "design" / "TYCOON_P05_MERCENARY_ROSTER_COMPLETE_DESIGN_v1.0.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.3"
CONTRACT_ROOT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts"
NEW_GAME_OUTPUT = CONTRACT_ROOT / "p05-new-game.template.json"
MIGRATION_OUTPUT = CONTRACT_ROOT / "p05-migration-after.golden.json"
EXPECTED_MANIFEST_LENGTH = 67_270
EXPECTED_MANIFEST_SHA256 = "352a947980b09b0dc5d5699a9367bd6e35cfb1296241784d4c52bc80df0a98e1"
EXPECTED_SCHEMA_LENGTH = 2_724
EXPECTED_SCHEMA_SHA256 = "451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41"
EXPECTED_LOCALIZATION_ROWS = 806
EXPECTED_LOCALIZATION_SHA256 = "60feb5d4c780aaa52fc7808d206d690950883432e2608f95034050c88f5f1e18"
EXPECTED_REQUEST_HASH = "8b19c3a58c3803be9fa801b09a281cb975e76b2fa900e48df1c6314475d9a37c"
EXPECTED_SAVE_VECTORS = (
    (1, 7_195, "46a293a784f888a771def107eef93d6c15cc9816279dcf215a6e36574a27d34b", "a1020838ab600acf9924b827ef168677a2784a6055977d462a474efdf238fe3a"),
    (2, 7_195, "441d02adf6d58189ea0b15c8f5ea3919211c5aa0a0c1ca31f706fe0a254c887e", "ef10df65febf0f049ebb330cc3adfb2c2f87985d90e3f015c079f204e85cbec1"),
)


def _load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load module: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


P04 = _load_module("kingdom_tycoon_p04_content", ROOT / "scripts" / "generate_p04_content.py")
P03 = P04.P03
ContractError = P03.ContractError
Package = P03.Package


def _json_blocks(text: str) -> list[dict[str, Any]]:
    return [
        value
        for raw in re.findall(r"```json\n(.*?)\n```", text, flags=re.DOTALL)
        if isinstance((value := json.loads(raw)), dict)
    ]


def _csv_block(text: str) -> str:
    blocks = re.findall(r"```csv\n(.*?)\n```", text, flags=re.DOTALL)
    if len(blocks) != 2 or blocks[0] != blocks[1]:
        raise ContractError("P05 section 10 and section 15 localization bundles must be identical")
    return blocks[0]


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


def _validate_save_vector(document: dict[str, Any], expected: tuple[int, int, str, str]) -> None:
    revision, payload_length, payload_sha256, envelope_sha256 = expected
    payload = P03._jcs_bytes(document["payload"])
    actual_payload_sha256 = hashlib.sha256(payload).hexdigest()
    actual_envelope_sha256 = hashlib.sha256(P03._jcs_bytes(_envelope_digest_input(document))).hexdigest()
    if document["revision"] != revision or len(payload) != payload_length:
        raise ContractError(f"P05 Save vector revision/length mismatch: revision={document['revision']}, bytes={len(payload)}")
    if actual_payload_sha256 != payload_sha256 or document["integrity"]["payloadSha256"] != payload_sha256:
        raise ContractError("P05 Save payload golden mismatch")
    if actual_envelope_sha256 != envelope_sha256 or document["integrity"]["fileSha256"] != envelope_sha256:
        raise ContractError("P05 Save envelope golden mismatch")


def load_contract() -> tuple[Package, dict[str, Any], dict[str, Any]]:
    text = P05_SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    if "**상태: CONFIRMED**" not in text or "`NONE`" not in text.split("## 25. UNRESOLVED", 1)[1]:
        raise ContractError("P05 contract is not final")

    blocks = _json_blocks(text)
    manifest = next(item for item in blocks if item.get("schemaId") == P03.EXPECTED_SCHEMA_ID)
    saves = [item for item in blocks if item.get("schemaId") == "urn:tycoon:save:v1"]
    command = next(item for item in blocks if item.get("commandType") == "SET_MERCENARY_ACTIVE")
    if len(saves) != 2:
        raise ContractError(f"P05 must contain two Save vectors, found {len(saves)}")

    manifest_bytes = P03._jcs_bytes(manifest)
    if len(manifest_bytes) != EXPECTED_MANIFEST_LENGTH or hashlib.sha256(manifest_bytes).hexdigest() != EXPECTED_MANIFEST_SHA256:
        raise ContractError("P05 manifest RFC8785 golden mismatch")
    if manifest["contentVersion"] != "1.0.0-content.3" or manifest["csvSchemaSetVersion"] != 3 or len(manifest["tables"]) != 62:
        raise ContractError("P05 release metadata mismatch")
    if hashlib.sha256(P03._jcs_bytes(command)).hexdigest() != EXPECTED_REQUEST_HASH:
        raise ContractError("P05 SetMercenaryActive requestHash golden mismatch")
    for document, expected in zip(saves, EXPECTED_SAVE_VECTORS, strict=True):
        _validate_save_vector(document, expected)

    base, _ = P04.load_contract()
    schema_bytes = P03._jcs_bytes(base.schema)
    if len(schema_bytes) != EXPECTED_SCHEMA_LENGTH or hashlib.sha256(schema_bytes).hexdigest() != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P05 inherited schema golden mismatch")

    csv_bytes = dict(base.csv_bytes)
    csv_bytes["localizations.csv"] = P04._merge_localizations(base.csv_bytes["localizations.csv"], _csv_block(text))
    localization_rows = list(csv.reader(io.StringIO(csv_bytes["localizations.csv"].decode("utf-8"), newline=""), strict=True))
    if len(localization_rows) - 1 != EXPECTED_LOCALIZATION_ROWS:
        raise ContractError("P05 localization row count mismatch")
    if hashlib.sha256(csv_bytes["localizations.csv"]).hexdigest() != EXPECTED_LOCALIZATION_SHA256:
        raise ContractError("P05 localization SHA-256 mismatch")

    P03._validate_csv_and_references(manifest, csv_bytes)
    P04._validate_domains(manifest, csv_bytes)
    P04._validate_p04_semantics(csv_bytes)
    return Package(base.schema, manifest, csv_bytes, manifest_bytes), saves[0], saves[1]


def _pretty_json(value: dict[str, Any]) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _expected_files(package: Package, new_game: dict[str, Any], migration: dict[str, Any]) -> dict[Path, bytes]:
    files = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": _pretty_json(package.schema),
        NEW_GAME_OUTPUT: _pretty_json(new_game),
        MIGRATION_OUTPUT: _pretty_json(migration),
    }
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    return files


def generate(package: Package, new_game: dict[str, Any], migration: dict[str, Any], check: bool) -> None:
    failures: list[str] = []
    expected = _expected_files(package, new_game, migration)
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
    if check and versions != ["1.0.0-content.1", "1.0.0-content.2", "1.0.0-content.3"]:
        failures.append(f"versioned package tree invalid: versions={versions}")
    if failures:
        raise ContractError("P05 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, new_game, migration = load_contract()
        generate(package, new_game, migration, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, json.JSONDecodeError, csv.Error) as error:
        print(f"p05-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p05-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {hashlib.sha256(package.manifest_bytes).hexdigest()}")
    print(f"localizations-sha256: {hashlib.sha256(package.csv_bytes['localizations.csv']).hexdigest()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
