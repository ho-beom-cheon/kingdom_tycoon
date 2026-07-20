#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_AUTOMATIC_GROWTH_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_AUTOMATIC_GROWTH_COMPLETENESS_REVIEW_v1.0.md"
rules="client-unity/Assets/KingdomTycoon/Resources/Contracts/automatic-growth.rules.json"
service="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Combat/ContinuousHuntGameService.cs"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.13.schema.json"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/ContinuousHuntTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"

for required in "$design" "$review" "$rules" "$service" "$schema" "$edit_tests" "$play_tests"; do
  [ -f "$required" ] || { printf '[automatic-growth] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[automatic-growth 1/3] Validating contracts and generated Save artifacts...\n'
grep -q '구현 가능' "$review"
grep -q '"skillGrowth"' "$schema"
grep -q '"TRAIN_SKILLS"' "$service"
grep -q '"ENHANCE_EQUIPMENT"' "$service"
python scripts/generate_p15_content.py --check

printf '[automatic-growth 2/3] Validating policy and behavior coverage...\n'
python -m json.tool "$rules" >/dev/null
grep -q 'TownReturnTrainsUnlockedSkillAndPersistsItsHuntBonus' "$edit_tests"
grep -q 'TownReturnEnhancesOwnedEquippedItemWithoutBreakingResume' "$edit_tests"
grep -q '스킬 Lv' "$play_tests"
grep -q '최고 장비' "$play_tests"

printf '[automatic-growth 3/3] Unity execution policy...\n'
if [ "${AUTOMATIC_GROWTH_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[automatic-growth] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.ContinuousHuntTests -testResults "$repo_root/artifacts/automatic-growth-editmode.xml" -logFile "$repo_root/artifacts/automatic-growth-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/automatic-growth-playmode.xml" -logFile "$repo_root/artifacts/automatic-growth-playmode.log"
else
  printf '[automatic-growth] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[automatic-growth] Gate passed.\n'
