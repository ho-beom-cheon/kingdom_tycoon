#!/usr/bin/env python3
"""Build and verify the P03 canonical content package from the final contract.

The checked-in Markdown contract is the immutable authoring input.  This tool
extracts its manifest schema, full v2 manifest fixture, and all 60 CSV blocks,
then enforces the byte-level package contract before writing StreamingAssets.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = ROOT / "docs" / "design" / "TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md"
DEFAULT_OUTPUT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
SCHEMA_OUTPUT = ROOT / "data" / "schemas" / "content_manifest.schema.json"
EXPECTED_SCHEMA_ID = "urn:tycoon:content-manifest:v2"
EXPECTED_SCHEMA_DOCUMENT_ID = "urn:tycoon:content-manifest-schema:v2"
EXPECTED_CONTENT_VERSION = "1.0.0-content.1"
EXPECTED_TABLE_COUNT = 60
DISABLED_STATUSES = {
    "DEFERRED",
    "OPS_LATER",
    "REFERENCE_ONLY",
    "REJECTED",
    "UNRESOLVED",
    "TEMPLATE",
}


class ContractError(RuntimeError):
    """Raised when the final design or generated package violates its contract."""


@dataclass(frozen=True)
class Package:
    schema: dict[str, Any]
    manifest: dict[str, Any]
    csv_bytes: dict[str, bytes]
    manifest_bytes: bytes


def _json_blocks(markdown: str) -> list[dict[str, Any]]:
    blocks: list[dict[str, Any]] = []
    for raw in re.findall(r"```json\n(.*?)\n```", markdown, flags=re.DOTALL):
        value = json.loads(raw)
        if isinstance(value, dict):
            blocks.append(value)
    return blocks


def _csv_blocks(markdown: str) -> dict[str, str]:
    pattern = re.compile(
        r"### 8\.\d+ `([^`]+)` \((\d+) rows\)\n\n```csv\n(.*?)\n```",
        flags=re.DOTALL,
    )
    blocks: dict[str, str] = {}
    for file_name, declared_count, body in pattern.findall(markdown):
        if file_name in blocks:
            raise ContractError(f"Duplicate CSV block: {file_name}")
        rows = list(csv.reader(io.StringIO(body, newline="")))
        actual_count = max(0, len(rows) - 1)
        if actual_count != int(declared_count):
            raise ContractError(
                f"{file_name}: heading declares {declared_count} rows, found {actual_count}"
            )
        blocks[file_name] = body
    return blocks


def _canonical_csv_bytes(body: str) -> bytes:
    normalized = body.replace("\r\n", "\n").replace("\r", "\n")
    return (normalized.rstrip("\n") + "\n").replace("\n", "\r\n").encode("utf-8")


def _jcs_bytes(value: dict[str, Any]) -> bytes:
    # The manifest contains strings, booleans, null, arrays, and integral JSON
    # numbers only. For that subset this is RFC 8785 canonical JSON.
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def _require_exact_properties(value: dict[str, Any], expected: set[str], location: str) -> None:
    actual = set(value)
    if actual != expected:
        missing = sorted(expected - actual)
        unknown = sorted(actual - expected)
        raise ContractError(f"{location}: missing={missing}, unknown={unknown}")


def _validate_manifest_shape(manifest: dict[str, Any]) -> None:
    _require_exact_properties(
        manifest,
        {
            "schemaId",
            "contractVersion",
            "contentVersion",
            "csvSchemaSetVersion",
            "packageKind",
            "baseContentVersion",
            "minimumGameVersion",
            "channel",
            "generatedAtUtc",
            "tables",
        },
        "manifest",
    )
    expected_scalars = {
        "schemaId": EXPECTED_SCHEMA_ID,
        "contractVersion": 2,
        "contentVersion": EXPECTED_CONTENT_VERSION,
        "csvSchemaSetVersion": 2,
        "packageKind": "BASE",
        "baseContentVersion": None,
        "minimumGameVersion": "1.0.0",
        "channel": "DEV",
        "generatedAtUtc": "2026-07-18T00:00:00.000Z",
    }
    for key, expected in expected_scalars.items():
        if manifest[key] != expected:
            raise ContractError(f"manifest.{key}: expected {expected!r}, found {manifest[key]!r}")

    tables = manifest["tables"]
    if not isinstance(tables, list) or len(tables) != EXPECTED_TABLE_COUNT:
        raise ContractError(f"manifest.tables: expected {EXPECTED_TABLE_COUNT} descriptors")
    names = [table.get("file") for table in tables]
    if names != sorted(names, key=lambda item: item.encode("utf-8")):
        raise ContractError("CONTENT_MANIFEST_TABLE_ORDER_INVALID")
    if len(names) != len(set(names)):
        raise ContractError("CONTENT_MANIFEST_TABLE_ORDER_INVALID: duplicate file")

    for index, table in enumerate(tables):
        location = f"manifest.tables[{index}]"
        _require_exact_properties(
            table,
            {
                "file",
                "schemaVersion",
                "required",
                "sha256",
                "rowCount",
                "primaryKey",
                "fields",
                "foreignKeys",
            },
            location,
        )
        if table["schemaVersion"] != 1 or table["required"] is not True:
            raise ContractError(f"{location}: P03 requires schemaVersion=1 and required=true")
        if not re.fullmatch(r"[0-9a-f]{64}", table["sha256"]):
            raise ContractError(f"{location}: CONTENT_MANIFEST_HASH_FORMAT_INVALID")
        fields = table["fields"]
        field_names = [field.get("name") for field in fields]
        if len(field_names) != len(set(field_names)):
            raise ContractError(f"{location}: duplicate field")
        if field_names[-2:] != ["status", "enabled"]:
            raise ContractError(f"{location}: lifecycle fields must be last")
        for field_index, field in enumerate(fields):
            _require_exact_properties(
                field,
                {"name", "domain", "nullable", "enumValues"},
                f"{location}.fields[{field_index}]",
            )
        for foreign_key_index, foreign_key in enumerate(table["foreignKeys"]):
            _require_exact_properties(
                foreign_key,
                {"sourceFields", "targetFile", "targetFields", "mode"},
                f"{location}.foreignKeys[{foreign_key_index}]",
            )
            if foreign_key["mode"] not in {"HARD", "SOFT"}:
                raise ContractError(f"{location}: invalid FK strength")


def _row_key(row: dict[str, str], fields: list[str]) -> tuple[str, ...]:
    return tuple(row[field] for field in fields)


def _validate_csv_and_references(
    manifest: dict[str, Any], csv_bytes: dict[str, bytes]
) -> None:
    descriptors = {table["file"]: table for table in manifest["tables"]}
    parsed_rows: dict[str, list[dict[str, str]]] = {}

    if set(descriptors) != set(csv_bytes):
        raise ContractError(
            f"Manifest/CSV set mismatch: manifest-only={sorted(set(descriptors)-set(csv_bytes))}, "
            f"csv-only={sorted(set(csv_bytes)-set(descriptors))}"
        )

    for file_name, descriptor in descriptors.items():
        raw = csv_bytes[file_name]
        if raw.startswith(b"\xef\xbb\xbf") or not raw.endswith(b"\r\n") or b"\n" in raw.replace(b"\r\n", b""):
            raise ContractError(f"{file_name}: canonical CRLF/BOM contract failed")
        actual_hash = hashlib.sha256(raw).hexdigest()
        if actual_hash != descriptor["sha256"]:
            raise ContractError(
                f"{file_name}: CONTENT_MANIFEST_HASH_MISMATCH "
                f"expected={descriptor['sha256']} actual={actual_hash}"
            )
        text = raw.decode("utf-8")
        records = list(csv.reader(io.StringIO(text, newline="")))
        headers = [field["name"] for field in descriptor["fields"]]
        if not records or records[0] != headers:
            raise ContractError(f"{file_name}: CONTENT_MANIFEST_HEADER_MISMATCH")
        if len(records) - 1 != descriptor["rowCount"]:
            raise ContractError(f"{file_name}: CONTENT_MANIFEST_ROW_COUNT_MISMATCH")
        rows: list[dict[str, str]] = []
        primary_keys: set[tuple[str, ...]] = set()
        for row_number, values in enumerate(records[1:], start=2):
            if len(values) != len(headers):
                raise ContractError(f"{file_name}:{row_number}: CSV column count mismatch")
            row = dict(zip(headers, values, strict=True))
            key = _row_key(row, descriptor["primaryKey"])
            if key in primary_keys:
                raise ContractError(f"{file_name}:{row_number}: CSV_PRIMARY_KEY_DUPLICATE {key}")
            primary_keys.add(key)
            if row["status"] in DISABLED_STATUSES and row["enabled"] != "FALSE":
                raise ContractError(f"{file_name}:{row_number}: CSV_STATUS_ENABLED_INVALID")
            if "|" in "".join(values):
                raise ContractError(f"{file_name}:{row_number}: CSV_PIPE_LIST_FORBIDDEN")
            rows.append(row)
        parsed_rows[file_name] = rows

    for file_name, descriptor in descriptors.items():
        for foreign_key in descriptor["foreignKeys"]:
            target_file = foreign_key["targetFile"]
            target_rows = parsed_rows[target_file]
            target_keys = {
                _row_key(row, foreign_key["targetFields"]): row for row in target_rows
            }
            for row_number, row in enumerate(parsed_rows[file_name], start=2):
                source_key = _row_key(row, foreign_key["sourceFields"])
                if any(not item for item in source_key):
                    continue
                target = target_keys.get(source_key)
                if foreign_key["mode"] == "HARD" and target is None:
                    raise ContractError(
                        f"{file_name}:{row_number}: CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET "
                        f"{target_file}{source_key}"
                    )
                if row["enabled"] == "TRUE" and target is not None and target["enabled"] != "TRUE":
                    raise ContractError(
                        f"{file_name}:{row_number}: enabled row references disabled {target_file}{source_key}"
                    )


def load_contract(source: Path) -> Package:
    markdown = source.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    if "UNRESOLVED`는 `NONE`" not in markdown or "추가 상세 설계 요청 없이" not in markdown:
        raise ContractError("The source document is not the final P03 implementation contract")

    json_blocks = _json_blocks(markdown)
    schema = next(
        (item for item in json_blocks if item.get("$id") == EXPECTED_SCHEMA_DOCUMENT_ID),
        None,
    )
    manifest = next(
        (item for item in json_blocks if item.get("schemaId") == EXPECTED_SCHEMA_ID),
        None,
    )
    if schema is None or manifest is None:
        raise ContractError("The final contract must contain the v2 schema and manifest fixtures")

    _validate_manifest_shape(manifest)
    blocks = _csv_blocks(markdown)
    if len(blocks) != EXPECTED_TABLE_COUNT:
        raise ContractError(f"Expected {EXPECTED_TABLE_COUNT} CSV blocks, found {len(blocks)}")
    csv_bytes = {name: _canonical_csv_bytes(body) for name, body in blocks.items()}
    _validate_csv_and_references(manifest, csv_bytes)
    return Package(schema, manifest, csv_bytes, _jcs_bytes(manifest))


def _package_fingerprint(package: Package) -> str:
    digest = hashlib.sha256()
    digest.update(b"KTCPKG1")
    digest.update(struct.pack(">Q", len(package.manifest_bytes)))
    digest.update(package.manifest_bytes)
    for file_name in sorted(package.csv_bytes, key=lambda item: item.encode("utf-8")):
        name_bytes = file_name.encode("utf-8")
        contents = package.csv_bytes[file_name]
        digest.update(struct.pack(">Q", len(name_bytes)))
        digest.update(name_bytes)
        digest.update(struct.pack(">Q", len(contents)))
        digest.update(contents)
    return digest.hexdigest()


def _expected_files(package: Package) -> dict[Path, bytes]:
    schema_bytes = (
        json.dumps(package.schema, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")
    files = {
        SCHEMA_OUTPUT: schema_bytes,
        DEFAULT_OUTPUT / "content_manifest.json": package.manifest_bytes,
    }
    files.update({DEFAULT_OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    return files


def _check_legacy_inventory() -> None:
    legacy_root = ROOT / "data" / "csv"
    csv_files = sorted(legacy_root.glob("*.csv"))
    if len(csv_files) != 29 or not (legacy_root / "README.md").is_file():
        raise ContractError(
            "Legacy authoring inventory drifted: expected 29 CSV files and README.md"
        )


def generate(package: Package, check: bool) -> None:
    expected = _expected_files(package)
    failures: list[str] = []
    for path, contents in expected.items():
        if check:
            if not path.is_file():
                failures.append(f"missing: {path.relative_to(ROOT)}")
            elif path.read_bytes() != contents:
                failures.append(f"drift: {path.relative_to(ROOT)}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)

    expected_package_names = {path.name for path in expected if path.parent == DEFAULT_OUTPUT}
    if DEFAULT_OUTPUT.exists():
        actual_managed_names = {
            path.name
            for path in DEFAULT_OUTPUT.iterdir()
            if path.is_file() and (path.suffix == ".csv" or path.name == "content_manifest.json")
        }
        stale = sorted(actual_managed_names - expected_package_names)
        if check:
            failures.extend(f"stale: {DEFAULT_OUTPUT.relative_to(ROOT) / name}" for name in stale)
        else:
            for name in stale:
                (DEFAULT_OUTPUT / name).unlink()

    if failures:
        raise ContractError("Canonical package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    try:
        _check_legacy_inventory()
        package = load_contract(args.source.resolve())
        generate(package, args.check)
    except (ContractError, OSError, UnicodeError, json.JSONDecodeError, csv.Error) as error:
        print(f"canonical-content: ERROR: {error}", file=sys.stderr)
        return 1

    mode = "verified" if args.check else "generated"
    print(f"canonical-content: {mode} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {hashlib.sha256(package.manifest_bytes).hexdigest()}")
    print(f"package-fingerprint: {_package_fingerprint(package)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
