#!/usr/bin/env bash
set -Eeuo pipefail

# P11's portable gate validates progression/promotion contracts and rendered
# evidence. Licensed Unity tests and Android IL2CPP remain explicit opt-ins.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p11_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P11ProgressionSetup.cs"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Progression/ProgressionInfrastructure.cs"
migration_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Progression/P10ToP11ContentMigration.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.9.schema.json"
golden_root="docs/goldens/P11"
capture_root="docs/reports/captures/P11"

for required in "$generator" "$editor_source" "$runtime_source" "$migration_source" "$schema"; do
  [ -f "$required" ] || { printf '[p11] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p11] Python is required.\n' >&2; exit 1; }

printf '[p11 1/4] Validating content .9, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p11] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p11 2/4] Validating XP, promotion, UI, capture, and Android entrypoints...\n'
grep -q 'class ProgressionGameService' "$runtime_source"
grep -q 'ApplyCombatSettlement' "$runtime_source"
grep -q 'class P10ToP11ContentMigration' "$migration_source"
grep -q 'class P11GeneratedAssetVerifier' "$editor_source"
grep -q 'class P11CaptureGenerator' "$editor_source"
grep -q 'class P11AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P11-Development.apk' "$editor_source"

printf '[p11 3/4] Validating four rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p11] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p11_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 4 ] || { printf '[p11] Expected 4 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p11] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p11_*.png' -print | sort)

printf '[p11 4/4] Unity execution policy...\n'
if [ "${P11_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p11] P11_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P11ProgressionTests \
    -testResults "$repo_root/artifacts/p11-editmode.xml" -logFile "$repo_root/artifacts/p11-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P11ProgressionScreenTests \
    -testResults "$repo_root/artifacts/p11-playmode.xml" -logFile "$repo_root/artifacts/p11-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P11CaptureGenerator.Run -logFile "$repo_root/artifacts/p11-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P11AndroidBuilder.Build -logFile "$repo_root/artifacts/p11-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P11-Development.apk" ] || { printf '[p11] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p11] Unity tests, captures, and Android build completed.\n'
else
  printf '[p11] Unity execution skipped; set P11_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p11] Gate passed.\n'
