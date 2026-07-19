#!/usr/bin/env bash
set -Eeuo pipefail

# Portable P15 gate: offline settlement/tutorial contracts, runtime/UI
# entrypoints, and GPU-rendered acceptance evidence. Licensed Unity and
# Android execution remain opt-in for local or licensed runners.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p15_content.py"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/OfflineTutorial/OfflineTutorialInfrastructure.cs"
domain_source="client-unity/Assets/KingdomTycoon/Runtime/Domain/OfflineTutorial/OfflineTutorialDomain.cs"
migration_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/OfflineTutorial/P14ToP15ContentMigration.cs"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P15OfflineTutorialSetup.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.13.schema.json"
golden_root="docs/goldens/P15"
capture_root="docs/reports/captures/P15"

for required in "$generator" "$runtime_source" "$domain_source" "$migration_source" "$editor_source" "$schema"; do
  [ -f "$required" ] || { printf '[p15] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p15] Python is required.\n' >&2; exit 1; }

printf '[p15 1/4] Validating content .13, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p15] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p15 2/4] Validating settlement, replay, tutorial, UI, capture, and Android entrypoints...\n'
grep -q 'class OfflineTutorialGameService' "$runtime_source"
grep -q 'P15_OPERATION_HASH_MISMATCH' "$runtime_source"
grep -q 'class OfflineWindowPolicy' "$domain_source"
grep -q 'class P14ToP15ContentMigration' "$migration_source"
grep -q 'class P15GeneratedAssetVerifier' "$editor_source"
grep -q 'class P15CaptureGenerator' "$editor_source"
grep -q 'class P15AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P15-Development.apk' "$editor_source"

printf '[p15 3/4] Validating seven rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p15] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p15_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 7 ] || { printf '[p15] Expected 7 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p15] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p15_*.png' -print | sort)

printf '[p15 4/4] Unity execution policy...\n'
if [ "${P15_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p15] P15_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P15OfflineTutorialTests -testResults "$repo_root/artifacts/p15-editmode.xml" -logFile "$repo_root/artifacts/p15-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P15OfflineTutorialScreenTests -testResults "$repo_root/artifacts/p15-playmode.xml" -logFile "$repo_root/artifacts/p15-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" -executeMethod KingdomTycoon.Editor.P15CaptureGenerator.Run -logFile "$repo_root/artifacts/p15-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" -executeMethod KingdomTycoon.Editor.P15AndroidBuilder.Build -logFile "$repo_root/artifacts/p15-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P15-Development.apk" ] || { printf '[p15] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p15] Unity tests, captures, and Android build completed.\n'
else
  printf '[p15] Unity execution skipped; set P15_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p15] Gate passed.\n'
