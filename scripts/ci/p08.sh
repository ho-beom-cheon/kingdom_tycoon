#!/usr/bin/env bash
set -Eeuo pipefail

# P08's portable gate verifies the generated content/save contract and the
# checked-in visual evidence on every runner. Licensed Unity execution is
# opt-in because IL2CPP and rendered captures require a configured editor.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p08_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P08StoreSetup.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.6.schema.json"
golden_root="docs/goldens/P08"
capture_root="docs/reports/captures/P08"

[ -f "$generator" ] || { printf '[p08] Missing generator: %s\n' "$generator" >&2; exit 1; }
[ -f "$editor_source" ] || { printf '[p08] Missing editor source: %s\n' "$editor_source" >&2; exit 1; }
[ -f "$schema" ] || { printf '[p08] Missing Save schema: %s\n' "$schema" >&2; exit 1; }
command -v python >/dev/null 2>&1 || { printf '[p08] Python is required.\n' >&2; exit 1; }

printf '[p08 1/4] Validating content .6, Save schema, migrations, and hashes...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check
golden_count="$(find "$golden_root" -maxdepth 1 -type f -name '*.json' | wc -l | tr -d ' ')"
[ "$golden_count" -ge 9 ] || { printf '[p08] Expected at least 9 JSON goldens, found %s.\n' "$golden_count" >&2; exit 1; }

printf '[p08 2/4] Validating generated-asset, capture, and Android entrypoints...\n'
grep -q 'class P08GeneratedAssetVerifier' "$editor_source"
grep -q 'class P08CaptureGenerator' "$editor_source"
grep -q 'class P08AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P08-Development.apk' "$editor_source"

printf '[p08 3/4] Validating twelve rendered acceptance captures...\n'
[ -d "$capture_root" ] || { printf '[p08] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p08_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 12 ] || { printf '[p08] Expected 12 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 65536 ] || { printf '[p08] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p08_*.png' -print | sort)

printf '[p08 4/4] Unity execution policy...\n'
if [ "${P08_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] \
    || { printf '[p08] P08_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.P08EconomyTests \
    -testResults "$repo_root/artifacts/p08-editmode.xml" -logFile "$repo_root/artifacts/p08-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.P08StoreScreenTests \
    -testResults "$repo_root/artifacts/p08-playmode.xml" -logFile "$repo_root/artifacts/p08-playmode.log"
  # Do not pass -nographics here: camera.Render needs a graphics device to
  # produce non-blank PNG evidence in batch mode.
  "$UNITY_EDITOR" -batchmode -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P08CaptureGenerator.Run \
    -logFile "$repo_root/artifacts/p08-captures.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P08AndroidBuilder.Build \
    -logFile "$repo_root/artifacts/p08-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P08-Development.apk" ] \
    || { printf '[p08] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p08] Unity tests, captures, and Android build completed.\n'
else
  printf '[p08] Unity execution skipped; set P08_RUN_UNITY=1 on a licensed runner.\n'
fi

printf '[p08] Gate passed.\n'
