#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_HUNT_GAME_FEEL_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_HUNT_GAME_FEEL_COMPLETENESS_REVIEW_v1.0.md"
rules="client-unity/Assets/KingdomTycoon/Resources/Contracts/world-hunt.rules.json"
catalog="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Combat/WorldHuntCatalog.cs"
feedback="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldHuntFeedback.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/WorldHuntFeedbackTests.cs"
service_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/ContinuousHuntTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"

for required in "$design" "$review" "$rules" "$catalog" "$feedback" "$screen" "$edit_tests" "$service_tests" "$play_tests"; do
  [ -f "$required" ] || { printf '[hunt-game-feel] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[hunt-game-feel 1/4] Validating finalized presentation contract...\n'
grep -q 'FINAL / IMPLEMENTATION READY' "$design"
grep -q 'UNRESOLVED=0' "$review"
python -m json.tool "$rules" >/dev/null
grep -q '"maximumRewardFeedEntries"' "$rules"
grep -q '모션 감소 모드' "$design"

printf '[hunt-game-feel 2/4] Validating combat, reward and regional feedback...\n'
grep -q 'class WorldHuntFeedbackTracker' "$feedback"
grep -q 'class WorldHuntFeedbackAudio' "$feedback"
grep -q 'RiskLabel' "$catalog"
grep -q 'DropPreview' "$catalog"
grep -q '피해표시' "$screen"
grep -q '전투 · 보상 소식' "$screen"
grep -q '스킬 총 레벨' "$feedback"
grep -q '장비 최고' "$feedback"

printf '[hunt-game-feel 3/4] Validating behavior and accessibility coverage...\n'
grep -q 'FirstObservationAndSameRevisionNeverCreateDuplicateFeedback' "$edit_tests"
grep -q 'OverviewDerivesKoreanRiskDropPreviewAndValidatedFeedbackPolicy' "$service_tests"
grep -q 'GameFeelShowsRegionalDifferenceGrowthAndAccessiblePreferences' "$play_tests"
grep -q 'CombatAdvanceCreatesDamageFeedbackAndKeepsFeedBounded' "$play_tests"
grep -q '모션 감소' "$screen"
grep -q '효과음 끔' "$screen"

printf '[hunt-game-feel 4/4] Unity execution policy...\n'
if [ "${HUNT_GAME_FEEL_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[hunt-game-feel] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.WorldHuntFeedbackTests -testResults "$repo_root/artifacts/hunt-game-feel-editmode.xml" -logFile "$repo_root/artifacts/hunt-game-feel-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/hunt-game-feel-playmode.xml" -logFile "$repo_root/artifacts/hunt-game-feel-playmode.log"
else
  printf '[hunt-game-feel] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[hunt-game-feel] Gate passed.\n'
