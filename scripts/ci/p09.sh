#!/usr/bin/env bash
set -Eeuo pipefail

# P09's portable gate verifies generated content/save contracts and rendered
# evidence everywhere. Licensed Unity tests and Android IL2CPP remain opt-in.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p09_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P09ProductionSetup.cs"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Production/ProductionInfrastructure.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.7.schema.json"
golden_root="docs/goldens/P09"
capture_root="docs/reports/captures/P09"

for required in "$generator" "$editor_source" "$runtime_source" "$schema"; do
  [ -f "$required" ] || { printf '[p09] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p09] Python is required.\n' >&2; exit 1; }

printf '[p09 1/4] Validating content .7, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p09] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p09 2/4] Validating production, UI, capture, and Android entrypoints...\n'
grep -q 'class ProductionGameService' "$runtime_source"
grep -q 'class P09GeneratedAssetVerifier' "$editor_source"
grep -q 'class P09CaptureGenerator' "$editor_source"
grep -q 'class P09AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P09-Development.apk' "$editor_source"

printf '[p09 3/4] Validating six rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p09] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p09_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 6 ] || { printf '[p09] Expected 6 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p09] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p09_*.png' -print | sort)

printf '[p09 4/4] Unity execution policy...\n'
if [ "${P09_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] \
    || { printf '[p09] P09_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P09ProductionTests \
    -testResults "$repo_root/artifacts/p09-editmode.xml" -logFile "$repo_root/artifacts/p09-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P09ProductionScreenTests \
    -testResults "$repo_root/artifacts/p09-playmode.xml" -logFile "$repo_root/artifacts/p09-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P09CaptureGenerator.Run -logFile "$repo_root/artifacts/p09-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P09AndroidBuilder.Build -logFile "$repo_root/artifacts/p09-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P09-Development.apk" ] \
    || { printf '[p09] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p09] Unity tests, captures, and Android build completed.\n'
else
  printf '[p09] Unity execution skipped; set P09_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p09] Gate passed.\n'
