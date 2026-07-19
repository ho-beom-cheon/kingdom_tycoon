#!/usr/bin/env bash
set -Eeuo pipefail

# Portable P12 gate: deterministic content/Save contracts, runtime/UI entrypoints,
# and GPU-rendered acceptance evidence. Licensed Unity and Android remain opt-in.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p12_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P12RegionMapSetup.cs"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Regions/RegionInfrastructure.cs"
migration_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Regions/P11ToP12ContentMigration.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.10.schema.json"
golden_root="docs/goldens/P12"
capture_root="docs/reports/captures/P12"

for required in "$generator" "$editor_source" "$runtime_source" "$migration_source" "$schema"; do
  [ -f "$required" ] || { printf '[p12] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p12] Python is required.\n' >&2; exit 1; }

printf '[p12 1/4] Validating content .10, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p12] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p12 2/4] Validating region policy, combat settlement, UI, capture, and Android entrypoints...\n'
grep -q 'class RegionGameService' "$runtime_source"
grep -q 'ValidateDeployment' "$runtime_source"
grep -q 'ApplyHuntSettlement' "$runtime_source"
grep -q 'class P11ToP12ContentMigration' "$migration_source"
grep -q 'class P12GeneratedAssetVerifier' "$editor_source"
grep -q 'class P12CaptureGenerator' "$editor_source"
grep -q 'class P12AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P12-Development.apk' "$editor_source"

printf '[p12 3/4] Validating five rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p12] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p12_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 5 ] || { printf '[p12] Expected 5 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p12] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p12_*.png' -print | sort)

printf '[p12 4/4] Unity execution policy...\n'
if [ "${P12_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p12] P12_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P12RegionTests \
    -testResults "$repo_root/artifacts/p12-editmode.xml" -logFile "$repo_root/artifacts/p12-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P12RegionMapScreenTests \
    -testResults "$repo_root/artifacts/p12-playmode.xml" -logFile "$repo_root/artifacts/p12-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P12CaptureGenerator.Run -logFile "$repo_root/artifacts/p12-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P12AndroidBuilder.Build -logFile "$repo_root/artifacts/p12-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P12-Development.apk" ] || { printf '[p12] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p12] Unity tests, captures, and Android build completed.\n'
else
  printf '[p12] Unity execution skipped; set P12_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p12] Gate passed.\n'
