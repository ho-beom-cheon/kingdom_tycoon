#!/usr/bin/env bash
set -Eeuo pipefail

# Portable P14 gate: deterministic raid content/Save contracts, runtime/UI
# entrypoints, and rendered acceptance evidence. Licensed Unity and Android
# execution remain opt-in for local or licensed runners.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p14_content.py"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Raids/RaidInfrastructure.cs"
simulation_source="client-unity/Assets/KingdomTycoon/Runtime/Domain/Raids/RaidDomain.cs"
migration_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Raids/P13ToP14ContentMigration.cs"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P14RaidSetup.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.12.schema.json"
golden_root="docs/goldens/P14"
capture_root="docs/reports/captures/P14"

for required in "$generator" "$runtime_source" "$simulation_source" "$migration_source" "$editor_source" "$schema"; do
  [ -f "$required" ] || { printf '[p14] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p14] Python is required.\n' >&2; exit 1; }

printf '[p14 1/4] Validating content .12, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p14] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p14 2/4] Validating simulation, replay, UI, capture, and Android entrypoints...\n'
grep -q 'class RaidGameService' "$runtime_source"
grep -q 'P14_OPERATION_HASH_MISMATCH' "$runtime_source"
grep -q 'class DeterministicRaidSimulation' "$simulation_source"
grep -q 'class P13ToP14ContentMigration' "$migration_source"
grep -q 'class P14GeneratedAssetVerifier' "$editor_source"
grep -q 'class P14CaptureGenerator' "$editor_source"
grep -q 'class P14AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P14-Development.apk' "$editor_source"

printf '[p14 3/4] Validating five rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p14] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p14_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 5 ] || { printf '[p14] Expected 5 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p14] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p14_*.png' -print | sort)

printf '[p14 4/4] Unity execution policy...\n'
if [ "${P14_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p14] P14_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P14RaidTests -testResults "$repo_root/artifacts/p14-editmode.xml" -logFile "$repo_root/artifacts/p14-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P14RaidScreenTests -testResults "$repo_root/artifacts/p14-playmode.xml" -logFile "$repo_root/artifacts/p14-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" -executeMethod KingdomTycoon.Editor.P14CaptureGenerator.Run -logFile "$repo_root/artifacts/p14-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" -executeMethod KingdomTycoon.Editor.P14AndroidBuilder.Build -logFile "$repo_root/artifacts/p14-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P14-Development.apk" ] || { printf '[p14] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p14] Unity tests, captures, and Android build completed.\n'
else
  printf '[p14] Unity execution skipped; set P14_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p14] Gate passed.\n'
