#!/usr/bin/env bash
set -Eeuo pipefail

# P10's portable gate validates the complete equipment-growth contract and
# rendered evidence. Licensed Unity tests and Android IL2CPP remain opt-in.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p10_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P10EquipmentGrowthSetup.cs"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/EquipmentGrowth/EquipmentGrowthInfrastructure.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.8.schema.json"
golden_root="docs/goldens/P10"
capture_root="docs/reports/captures/P10"

for required in "$generator" "$editor_source" "$runtime_source" "$schema"; do
  [ -f "$required" ] || { printf '[p10] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p10] Python is required.\n' >&2; exit 1; }

printf '[p10 1/4] Validating content .8, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p10] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p10 2/4] Validating runtime, UI, capture, and Android entrypoints...\n'
grep -q 'class EquipmentGrowthGameService' "$runtime_source"
grep -q 'class P10GeneratedAssetVerifier' "$editor_source"
grep -q 'class P10CaptureGenerator' "$editor_source"
grep -q 'class P10AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P10-Development.apk' "$editor_source"

printf '[p10 3/4] Validating four rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p10] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p10_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 4 ] || { printf '[p10] Expected 4 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p10] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p10_*.png' -print | sort)

printf '[p10 4/4] Unity execution policy...\n'
if [ "${P10_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p10] P10_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P10EquipmentGrowthTests \
    -testResults "$repo_root/artifacts/p10-editmode.xml" -logFile "$repo_root/artifacts/p10-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P10EquipmentGrowthScreenTests \
    -testResults "$repo_root/artifacts/p10-playmode.xml" -logFile "$repo_root/artifacts/p10-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P10CaptureGenerator.Run -logFile "$repo_root/artifacts/p10-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P10AndroidBuilder.Build -logFile "$repo_root/artifacts/p10-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P10-Development.apk" ] || { printf '[p10] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p10] Unity tests, captures, and Android build completed.\n'
else
  printf '[p10] Unity execution skipped; set P10_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p10] Gate passed.\n'
