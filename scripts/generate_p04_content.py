#!/usr/bin/env python3
"""Build and verify the immutable P04 content package from its three contracts."""

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
P03_SOURCE = ROOT / "docs" / "design" / "TYCOON_P03_CANONICAL_CONTENT_COMPLETE_DESIGN_v1.1.md"
P04_SOURCE = ROOT / "docs" / "design" / "TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.md"
P04_CORRECTION_1 = ROOT / "docs" / "design" / "TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.1_CORRECTION_APPENDIX.md"
P04_CORRECTION_2 = ROOT / "docs" / "design" / "TYCOON_P04_KINGDOM_FACILITIES_COMPLETE_DESIGN_v1.0.2_CORRECTION_APPENDIX.md"
CONTENT_ROOT = ROOT / "client-unity" / "Assets" / "StreamingAssets" / "Content"
OUTPUT = CONTENT_ROOT / "1.0.0-content.2"
TEMPLATE_OUTPUT = ROOT / "client-unity" / "Assets" / "KingdomTycoon" / "Resources" / "Contracts" / "p04-new-game.template.json"
EXPECTED_MANIFEST_LENGTH = 67_270
EXPECTED_MANIFEST_SHA256 = "668a4d4084d903e0a1dfb6394ed62287f3dc3a3f3c1fcc1d2d81d4f2af537c63"
EXPECTED_SCHEMA_SHA256 = "451611cb6a44c6e4254f5ef51b356609b22ddf7ea9a22e2fc95de45299c47f41"


def _load_p03_module():
    spec = importlib.util.spec_from_file_location(
        "kingdom_tycoon_p03_content", ROOT / "scripts" / "generate_canonical_content.py"
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("P03 canonical generator could not be loaded")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


P03 = _load_p03_module()
ContractError = P03.ContractError
Package = P03.Package


def _json_blocks(text: str) -> list[dict[str, Any]]:
    return [
        value
        for raw in re.findall(r"```json\n(.*?)\n```", text, flags=re.DOTALL)
        if isinstance((value := json.loads(raw)), dict)
    ]


def _csv_after(text: str, heading: str) -> str:
    heading_at = text.index(heading)
    start = text.index("```csv\n", heading_at) + len("```csv\n")
    end = text.index("\n```", start)
    return text[start:end]


def _canonical_csv(body: str) -> bytes:
    return P03._canonical_csv_bytes(body)


def _read_rows(raw: bytes) -> list[list[str]]:
    return list(csv.reader(io.StringIO(raw.decode("utf-8"), newline=""), strict=True))


def _merge_localizations(base: bytes, *addition_bodies: str) -> bytes:
    rows = _read_rows(base)
    header = rows[0]
    merged = rows[1:]
    for body in addition_bodies:
        addition = list(csv.reader(io.StringIO(body, newline=""), strict=True))
        if addition[0] != header:
            raise ContractError("P04 localization header does not match P03")
        merged.extend(addition[1:])
    keys = [(row[0], row[1]) for row in merged]
    if len(keys) != len(set(keys)):
        raise ContractError("P04 localization merge contains duplicate locale/text_key")
    output = io.StringIO(newline="")
    writer = csv.writer(output, lineterminator="\r\n")
    writer.writerow(header)
    writer.writerows(sorted(merged, key=lambda row: (row[0].encode("utf-8"), row[1].encode("utf-8"))))
    return output.getvalue().encode("utf-8")


def _validate_domains(manifest: dict[str, Any], csv_bytes: dict[str, bytes]) -> None:
    integer = re.compile(r"^(0|-?[1-9][0-9]*)$")
    decimal = re.compile(r"^-?(0|[1-9][0-9]*)(?:\.[0-9]+)?$")
    stable_id = re.compile(r"^[A-Z][A-Z0-9_]{0,63}$")
    locale = re.compile(r"^[a-z]{2}-[A-Z]{2}$")
    statuses = {"CONFIRMED", "TUNABLE", "DEFERRED", "OPS_LATER", "REFERENCE_ONLY", "REJECTED", "UNRESOLVED", "TEMPLATE"}
    descriptors = {table["file"]: table for table in manifest["tables"]}
    for file_name, descriptor in descriptors.items():
        records = _read_rows(csv_bytes[file_name])
        for row_number, values in enumerate(records[1:], start=2):
            row = dict(zip(records[0], values, strict=True))
            for field in descriptor["fields"]:
                value = row[field["name"]]
                if not value and field["nullable"]:
                    continue
                domain = field["domain"]
                valid = True
                if domain == "BOOL":
                    valid = value in {"TRUE", "FALSE"}
                elif domain in {"INT32", "INT64", "SAFE_INT", "SEED64"}:
                    valid = bool(integer.fullmatch(value))
                elif domain == "POSITIVE_INT":
                    valid = bool(integer.fullmatch(value)) and 0 < int(value) <= 9_007_199_254_740_991
                elif domain == "DECIMAL":
                    valid = bool(decimal.fullmatch(value))
                elif domain == "ENUM":
                    valid = value in field["enumValues"]
                elif domain == "STABLE_ID":
                    valid = bool(stable_id.fullmatch(value))
                elif domain == "STATUS":
                    valid = value in statuses
                elif domain == "LOCALE":
                    valid = bool(locale.fullmatch(value))
                elif domain == "STRING":
                    valid = bool(value) and len(value.encode("utf-8")) <= 4096
                if not valid:
                    raise ContractError(
                        f"{file_name}:{row_number}/{field['name']}: value {value!r} does not match {domain}"
                    )


def _validate_p04_semantics(csv_bytes: dict[str, bytes]) -> None:
    runtime = list(csv.DictReader(io.StringIO(csv_bytes["runtime_config.csv"].decode("utf-8"))))
    for row_number, row in enumerate(runtime, start=2):
        value_type = row["value_type"]
        value = row["value"]
        minimum = row["min_value"]
        maximum = row["max_value"]
        if value_type == "BOOLEAN":
            valid = value in {"TRUE", "FALSE"} and not minimum and not maximum and row["unit"] == "BOOL"
            code = "CSV_RUNTIME_BOOLEAN_LEXICAL_INVALID"
        elif value_type == "INTEGER":
            valid = re.fullmatch(r"^(0|-?[1-9][0-9]*)$", value) is not None
            valid &= all(not item or re.fullmatch(r"^(0|-?[1-9][0-9]*)$", item) is not None for item in (minimum, maximum))
            valid &= (not minimum or int(minimum) <= int(value)) and (not maximum or int(value) <= int(maximum))
            code = "CSV_RUNTIME_VALUE_TYPE_MISMATCH"
        elif value_type == "DECIMAL":
            pattern = r"^-?(0|[1-9][0-9]*)(?:\.[0-9]+)?$"
            valid = re.fullmatch(pattern, value) is not None
            valid &= all(not item or re.fullmatch(pattern, item) is not None for item in (minimum, maximum))
            valid &= (not minimum or float(minimum) <= float(value)) and (not maximum or float(value) <= float(maximum))
            code = "CSV_RUNTIME_VALUE_TYPE_MISMATCH"
        else:
            valid = value_type == "STRING" and bool(value) and not minimum and not maximum
            code = "CSV_RUNTIME_VALUE_TYPE_MISMATCH"
        if not valid:
            raise ContractError(f"runtime_config.csv:{row_number}: {code}")

    construction = list(csv.DictReader(io.StringIO(csv_bytes["facility_construction_rules.csv"].decode("utf-8"))))
    coverage = {(row["facility_id"], int(row["level"])) for row in construction if row["enabled"] == "TRUE"}
    facility_ids = {row["facility_id"] for row in construction}
    if len(construction) != 32 or len(facility_ids) != 8 or coverage != {(facility, level) for facility in facility_ids for level in range(1, 5)}:
        raise ContractError("CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID")
    if any(not 1 <= int(row["build_or_upgrade_duration_seconds"]) <= 86_400 or row["cancel_refund_ratio"] != "0" for row in construction):
        raise ContractError("CSV_FACILITY_DURATION_INVALID")

    assets = list(csv.DictReader(io.StringIO(csv_bytes["facility_world_assets.csv"].decode("utf-8"))))
    addresses = [row["address"] for row in assets]
    if len(assets) != 13 or len(addresses) != len(set(addresses)):
        raise ContractError("CSV_FACILITY_WORLD_ASSET_DUPLICATE")
    base_ids = {row["facility_id"] for row in assets if row["state_variant"] == "BASE"}
    roles = {row["state_variant"] for row in assets if not row["facility_id"]}
    if len(base_ids) != 8:
        raise ContractError("CSV_FACILITY_WORLD_ASSET_COVERAGE_INVALID")
    if roles != {"BACKGROUND", "PLOT", "LOCKED", "CONSTRUCTION", "STOPPED"}:
        raise ContractError("CSV_FACILITY_WORLD_ROLE_COVERAGE_INVALID")


def load_contract() -> tuple[Package, dict[str, Any]]:
    v1 = P04_SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    correction_1 = P04_CORRECTION_1.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    correction_2 = P04_CORRECTION_2.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    if "`NONE`" not in correction_2 or "P04 구현을 재개한다" not in correction_2:
        raise ContractError("P04 v1.0.2 is not a final implementation contract")

    manifest = next(item for item in _json_blocks(correction_2) if item.get("schemaId") == P03.EXPECTED_SCHEMA_ID)
    schema = next(item for item in _json_blocks(correction_1) if item.get("$id") == P03.EXPECTED_SCHEMA_DOCUMENT_ID)
    manifest_bytes = P03._jcs_bytes(manifest)
    if len(manifest_bytes) != EXPECTED_MANIFEST_LENGTH or hashlib.sha256(manifest_bytes).hexdigest() != EXPECTED_MANIFEST_SHA256:
        raise ContractError("P04 manifest RFC8785 golden mismatch")
    if hashlib.sha256(P03._jcs_bytes(schema)).hexdigest() != EXPECTED_SCHEMA_SHA256:
        raise ContractError("P04 schema RFC8785 golden mismatch")
    if manifest["contentVersion"] != "1.0.0-content.2" or manifest["csvSchemaSetVersion"] != 3 or len(manifest["tables"]) != 62:
        raise ContractError("P04 release metadata mismatch")

    p03 = P03.load_contract(P03_SOURCE)
    csv_bytes = dict(p03.csv_bytes)
    csv_bytes["facility_construction_rules.csv"] = _canonical_csv(_csv_after(v1, "### 11.1 `facility_construction_rules.csv`"))
    csv_bytes["facility_world_assets.csv"] = _canonical_csv(_csv_after(v1, "### 11.2 `facility_world_assets.csv`"))
    csv_bytes["asset_register.csv"] = _canonical_csv(_csv_after(v1, "### 11.3 `asset_register.csv`"))
    correction_csv = re.findall(r"```csv\n(.*?)\n```", correction_1, flags=re.DOTALL)
    csv_bytes["runtime_config.csv"] = _canonical_csv(correction_csv[0])
    csv_bytes["localizations.csv"] = _merge_localizations(
        csv_bytes["localizations.csv"],
        _csv_after(v1, "### 11.5 `localizations.csv`"),
        correction_csv[1],
    )
    P03._validate_csv_and_references(manifest, csv_bytes)
    _validate_domains(manifest, csv_bytes)
    _validate_p04_semantics(csv_bytes)

    golden_raw = re.search(r"### P04_NEW_GAME_GOLDEN\n\n```json\n(.*?)\n```", v1, flags=re.DOTALL)
    if golden_raw is None:
        raise ContractError("P04 new-game golden is missing")
    golden = json.loads(golden_raw.group(1))
    return Package(schema, manifest, csv_bytes, manifest_bytes), golden


def _expected_files(package: Package, golden: dict[str, Any]) -> dict[Path, bytes]:
    schema_bytes = (json.dumps(package.schema, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")
    golden_bytes = (json.dumps(golden, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")
    files = {
        OUTPUT / "content_manifest.json": package.manifest_bytes,
        OUTPUT / "content_manifest.schema.json": schema_bytes,
        TEMPLATE_OUTPUT: golden_bytes,
    }
    files.update({OUTPUT / name: contents for name, contents in package.csv_bytes.items()})
    return files


def generate(package: Package, golden: dict[str, Any], check: bool) -> None:
    failures: list[str] = []
    expected = _expected_files(package, golden)
    for path, contents in expected.items():
        if check:
            if not path.is_file():
                failures.append(f"missing: {path.relative_to(ROOT)}")
            elif path.read_bytes() != contents:
                failures.append(f"drift: {path.relative_to(ROOT)}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)

    expected_names = {path.name for path in expected if path.parent == OUTPUT}
    if OUTPUT.exists():
        actual_names = {path.name for path in OUTPUT.iterdir() if path.is_file() and not path.name.endswith(".meta")}
        for stale in sorted(actual_names - expected_names):
            if check:
                failures.append(f"stale: {(OUTPUT / stale).relative_to(ROOT)}")
            else:
                (OUTPUT / stale).unlink()

    if check:
        versions = sorted(path.name for path in CONTENT_ROOT.iterdir() if path.is_dir()) if CONTENT_ROOT.exists() else []
        flat = sorted(path.name for path in CONTENT_ROOT.iterdir() if path.is_file() and path.suffix in {".csv", ".json"}) if CONTENT_ROOT.exists() else []
        required_versions = {"1.0.0-content.1", "1.0.0-content.2"}
        if not required_versions.issubset(versions) or flat:
            failures.append(f"versioned package tree invalid: versions={versions}, flat={flat}")
    if failures:
        raise ContractError("P04 package check failed:\n- " + "\n- ".join(failures))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        package, golden = load_contract()
        generate(package, golden, args.check)
    except (ContractError, OSError, UnicodeError, ValueError, json.JSONDecodeError, csv.Error) as error:
        print(f"p04-content: ERROR: {error}", file=sys.stderr)
        return 1
    print(f"p04-content: {'verified' if args.check else 'generated'} {len(package.csv_bytes)} tables")
    print(f"manifest-sha256: {hashlib.sha256(package.manifest_bytes).hexdigest()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
