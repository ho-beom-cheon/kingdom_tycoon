#!/usr/bin/env bash
set -Eeuo pipefail

# Portable P13 gate: deterministic recruitment content/Save contracts,
# runtime/UI entrypoints, and GPU-rendered acceptance evidence. Licensed Unity
# and Android execution remain opt-in for local or licensed runners.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p13_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P13RecruitmentSetup.cs"
runtime_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Recruitment/RecruitmentInfrastructure.cs"
migration_source="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Recruitment/P12ToP13ContentMigration.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.11.schema.json"
golden_root="docs/goldens/P13"
capture_root="docs/reports/captures/P13"

for required in "$generator" "$editor_source" "$runtime_source" "$migration_source" "$schema"; do
  [ -f "$required" ] || { printf '[p13] Missing required file: %s\n' "$required" >&2; exit 1; }
done
command -v python >/dev/null 2>&1 || { printf '[p13] Python is required.\n' >&2; exit 1; }

printf '[p13 1/4] Validating content .11, Save schema, migration, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -eq 3 ] || { printf '[p13] Expected 3 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p13 2/4] Validating gateway, pity, replay, UI, capture, and Android entrypoints...\n'
grep -q 'class RecruitmentGameService' "$runtime_source"
grep -q 'class DevelopmentRecruitmentGateway' "$runtime_source"
grep -q 'P13_OPERATION_HASH_MISMATCH' "$runtime_source"
grep -q 'class P12ToP13ContentMigration' "$migration_source"
grep -q 'class P13GeneratedAssetVerifier' "$editor_source"
grep -q 'class P13CaptureGenerator' "$editor_source"
grep -q 'class P13AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P13-Development.apk' "$editor_source"

printf '[p13 3/4] Validating five rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p13] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p13_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 5 ] || { printf '[p13] Expected 5 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 100000 ] || { printf '[p13] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p13_*.png' -print | sort)

printf '[p13 4/4] Unity execution policy...\n'
if [ "${P13_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[p13] P13_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P13RecruitmentTests \
    -testResults "$repo_root/artifacts/p13-editmode.xml" -logFile "$repo_root/artifacts/p13-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P13RecruitmentScreenTests \
    -testResults "$repo_root/artifacts/p13-playmode.xml" -logFile "$repo_root/artifacts/p13-playmode.log"
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P13CaptureGenerator.Run -logFile "$repo_root/artifacts/p13-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P13AndroidBuilder.Build -logFile "$repo_root/artifacts/p13-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P13-Development.apk" ] || { printf '[p13] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p13] Unity tests, captures, and Android build completed.\n'
else
  printf '[p13] Unity execution skipped; set P13_RUN_UNITY=1 on a licensed runner with Android modules.\n'
fi

printf '[p13] Gate passed.\n'
