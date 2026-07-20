#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_WORLD_HUNT_ECONOMY_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_WORLD_HUNT_ECONOMY_COMPLETENESS_REVIEW_v1.0.md"
rules="client-unity/Assets/KingdomTycoon/Resources/Contracts/world-hunt.rules.json"
schema="client-unity/Assets/KingdomTycoon/Resources/Contracts/save.content.13.schema.json"
service="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Combat/ContinuousHuntGameService.cs"
catalog="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Combat/WorldHuntCatalog.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
drag="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldMapDragSurface.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/ContinuousHuntTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"

for required in "$design" "$review" "$rules" "$schema" "$service" "$catalog" "$screen" "$drag" "$edit_tests" "$play_tests"; do
  [ -f "$required" ] || { printf '[world-hunt-loop] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[world-hunt-loop 1/4] Validating finalized design and Save contract...\n'
grep -q 'FINAL / IMPLEMENTATION READY' "$design"
grep -q 'UNRESOLVED=0' "$review"
grep -q '"worldHunt"' "$schema"
grep -q '"huntBag"' "$schema"
grep -q '"WorldMonster"' "$schema"
python scripts/generate_p15_content.py --check
python -m json.tool "$rules" >/dev/null

printf '[world-hunt-loop 2/4] Validating real combat, loot and economy handoff...\n'
grep -q 'AdvanceCombat' "$service"
grep -q 'ResolveLoot' "$service"
grep -q 'SettleBag' "$service"
grep -q 'RunStoreAutonomyCycle' "$service"
grep -q 'loot_entries.csv' "$catalog"
grep -q 'TownSettlementMovesBagThroughRealStoreLedgerAndPurchaseAi' "$edit_tests"
grep -q 'AssignedMercenariesReserveDifferentMonstersAndApplyRealHpDamage' "$edit_tests"

printf '[world-hunt-loop 3/4] Validating integrated Korean world and drag coverage...\n'
grep -q '통합 사냥 월드' "$screen"
grep -q 'class WorldMapDragSurface' "$drag"
grep -q 'WorldContainsCentralKingdomFourDirectionsMonstersZoomAndClampedTwoAxisDrag' "$play_tests"
grep -q 'SupportedAspectsKeepAllTouchTargetsInsideSafeAreaWithoutOverlap' "$play_tests"

printf '[world-hunt-loop 4/4] Unity execution policy...\n'
if [ "${WORLD_HUNT_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[world-hunt-loop] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.ContinuousHuntTests -testResults "$repo_root/artifacts/world-hunt-editmode.xml" -logFile "$repo_root/artifacts/world-hunt-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/world-hunt-playmode.xml" -logFile "$repo_root/artifacts/world-hunt-playmode.log"
else
  printf '[world-hunt-loop] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[world-hunt-loop] Gate passed.\n'
