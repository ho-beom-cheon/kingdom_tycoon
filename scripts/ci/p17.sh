#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_P17_BALANCE_OPTIMIZATION_RC_COMPLETE_DESIGN_v1.0.md"
review="docs/reports/P17_DESIGN_COMPLETENESS_REVIEW.md"
setup="client-unity/Assets/KingdomTycoon/Editor/P17ReleaseReadinessSetup.cs"
edit_test="client-unity/Assets/KingdomTycoon/Tests/EditMode/P17ReleaseReadinessTests.cs"
play_test="client-unity/Assets/KingdomTycoon/Tests/PlayMode/P17ReleaseReadinessScreenTests.cs"
register="client-unity/Assets/StreamingAssets/Content/1.0.0-content.13/asset_register.csv"

printf '[p17 1/4] Validating final design and completeness review...\n'
for required in "$design" "$review" "$setup" "$edit_test" "$play_test" "$register"; do
  [ -f "$required" ] || { printf '[p17] Missing required file: %s\n' "$required" >&2; exit 1; }
done
grep -q '구현 가능 (`PASS`)' "$review"
grep -q '구현을 막는 `UNRESOLVED`는 없다' "$review"
grep -q '실제 집행액 | `0원`' "$design"

printf '[p17 2/4] Validating Korean UI and RC contracts...\n'
grep -q 'KingdomTycoon-1.0.0-rc.1.apk' "$setup"
grep -q 'com.kingdomtycoon.game' "$setup"
grep -q 'EditorUserBuildSettings.development = false' "$setup"
grep -q 'AndroidArchitecture.ARM64' "$setup"
grep -q 'ManagedStrippingLevel.High' "$setup"
grep -q '왕국 외곽 초원' client-unity/Assets/KingdomTycoon/Editor/P15OfflineTutorialSetup.cs
! grep -nE '\+216,000 tick|대상: REGION_R01|KINGDOM OPERATIONS|RELEASE 1\.0 READINESS' client-unity/Assets/KingdomTycoon/Editor/P15OfflineTutorialSetup.cs

printf '[p17 3/4] Validating active asset license fields and zero spend...\n'
python - "$register" <<'PY'
import csv, sys
with open(sys.argv[1], encoding="utf-8", newline="") as stream:
    rows = [row for row in csv.DictReader(stream) if row["enabled"] == "TRUE"]
assert rows, "asset register has no enabled rows"
required = ("creator", "license", "commercial_use", "modification_allowed", "status")
for row in rows:
    assert all(row[field].strip() for field in required), f"missing license field: {row['asset_id']}"
    assert row["commercial_use"] == "TRUE", f"commercial use denied: {row['asset_id']}"
    assert row["modification_allowed"] == "TRUE", f"modification denied: {row['asset_id']}"
assert sum(int(row["price_krw"]) for row in rows) == 0, "P17 actual asset spend must be zero"
print(f"[p17] asset register passed: enabled={len(rows)}, spend=0 KRW")
PY

printf '[p17 4/4] Validating six rendered release captures...\n'
capture_root="docs/reports/captures/P17"
for capture in \
  p17_01_report_16x9.png \
  p17_02_process_target_16x9.png \
  p17_03_process_complete_16x9.png \
  p17_04_report_20x9.png \
  p17_05_kingdom_release_art_16x9.png \
  p17_06_kingdom_release_art_20x9.png; do
  path="$capture_root/$capture"
  [ -s "$path" ] || { printf '[p17] Missing release capture: %s\n' "$path" >&2; exit 1; }
done

printf '[p17] Gate passed. Licensed Unity and physical Android device execution are reported separately.\n'
