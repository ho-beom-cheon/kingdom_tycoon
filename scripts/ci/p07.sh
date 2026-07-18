#!/usr/bin/env bash
set -Eeuo pipefail

# P07's portable gate always validates generated contracts and checked-in
# acceptance evidence. A licensed runner can opt into Unity tests and the
# IL2CPP ARM64 build by setting P07_RUN_UNITY=1 and UNITY_EDITOR.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generator="scripts/generate_p07_content.py"
editor_source="client-unity/Assets/KingdomTycoon/Editor/P07InventorySetup.cs"
capture_root="docs/reports/captures/P07"

[ -f "$generator" ] || { printf '[p07] Missing generator: %s\n' "$generator" >&2; exit 1; }
[ -f "$editor_source" ] || { printf '[p07] Missing editor source: %s\n' "$editor_source" >&2; exit 1; }
command -v python >/dev/null 2>&1 || { printf '[p07] Python is required.\n' >&2; exit 1; }

printf '[p07 1/3] Validating content .5, Save schema, migration, and hash goldens...\n'
PYTHONUNBUFFERED=1 python -u "$generator" --check

printf '[p07 2/3] Validating Android entrypoint and acceptance captures...\n'
grep -q 'class P07AndroidBuilder' "$editor_source"
grep -q 'KingdomTycoon-P07-Development.apk' "$editor_source"
[ -d "$capture_root" ] || { printf '[p07] Missing capture directory: %s\n' "$capture_root" >&2; exit 1; }
capture_count="$(find "$capture_root" -maxdepth 1 -type f -name 'p07_*.png' | wc -l | tr -d ' ')"
[ "$capture_count" -eq 10 ] || { printf '[p07] Expected 10 captures, found %s.\n' "$capture_count" >&2; exit 1; }
while IFS= read -r capture; do
  [ "$(wc -c < "$capture")" -gt 65536 ] || { printf '[p07] Capture is too small to be rendered evidence: %s\n' "$capture" >&2; exit 1; }
done < <(find "$capture_root" -maxdepth 1 -type f -name 'p07_*.png' -print | sort)

printf '[p07 3/3] Unity execution policy...\n'
if [ "${P07_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] \
    || { printf '[p07] P07_RUN_UNITY=1 requires executable UNITY_EDITOR.\n' >&2; exit 1; }
  mkdir -p artifacts client-unity/Builds/Android
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform editmode -testResults "$repo_root/artifacts/p07-editmode.xml" \
    -logFile "$repo_root/artifacts/p07-editmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" \
    -runTests -testPlatform playmode -testResults "$repo_root/artifacts/p07-playmode.xml" \
    -logFile "$repo_root/artifacts/p07-playmode.log"
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$repo_root/client-unity" \
    -executeMethod KingdomTycoon.Editor.P07AndroidBuilder.Build \
    -logFile "$repo_root/artifacts/p07-android-build.log"
  [ -s "client-unity/Builds/Android/KingdomTycoon-P07-Development.apk" ] \
    || { printf '[p07] Android APK was not produced.\n' >&2; exit 1; }
  printf '[p07] Unity EditMode, PlayMode, and Android build completed.\n'
else
  printf '[p07] Unity execution skipped; set P07_RUN_UNITY=1 on a licensed runner.\n'
fi

printf '[p07] Gate passed.\n'
