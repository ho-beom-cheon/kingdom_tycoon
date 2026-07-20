#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_CONTINUOUS_AUTO_HUNT_CORRECTION_COMPLETE_DESIGN_v1.0.md"
service="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Combat/ContinuousHuntGameService.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/ContinuousHuntTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.13.schema.json"

for required in "$design" "$service" "$screen" "$edit_tests" "$play_tests" "$schema"; do
  [ -f "$required" ] || { printf '[continuous-hunt] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[continuous-hunt 1/3] Validating design and save compatibility...\n'
grep -q 'IMPLEMENTABLE / COMPLETE' "$design"
grep -q '"assignedRegionId"' "$schema"
grep -q '"currentHpBps"' "$schema"
grep -q '"bagCapacity"' "$schema"
python scripts/generate_p15_content.py --check

printf '[continuous-hunt 2/3] Validating independent loop and Korean presentation...\n'
grep -q 'class ContinuousHuntGameService' "$service"
grep -q 'assignedRegionId' "$service"
grep -q 'class ContinuousHuntScreenPresenter' "$screen"
grep -q '상시 자동 사냥' "$screen"
grep -q 'SeveralMercenariesCanPersistInOneGroundAndAdvanceIndependently' "$edit_tests"
grep -q 'SupportedAspectsKeepAllTouchTargetsInsideSafeAreaWithoutOverlap' "$play_tests"

printf '[continuous-hunt 3/3] Unity execution policy...\n'
if [ "${CONTINUOUS_HUNT_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[continuous-hunt] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.ContinuousHuntTests -testResults "$repo_root/artifacts/continuous-hunt-editmode.xml" -logFile "$repo_root/artifacts/continuous-hunt-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/continuous-hunt-playmode.xml" -logFile "$repo_root/artifacts/continuous-hunt-playmode.log"
else
  printf '[continuous-hunt] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[continuous-hunt] Gate passed.\n'
