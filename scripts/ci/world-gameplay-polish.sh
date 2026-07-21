#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_WORLD_GAMEPLAY_POLISH_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_WORLD_GAMEPLAY_POLISH_COMPLETENESS_REVIEW_v1.0.md"
layout="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/MobileLivingWorldLayout.cs"
catalog="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldFacilityInteractionCatalog.cs"
pixel_art="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldHuntPixelArtLibrary.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/MobileLivingWorldTests.cs"
art_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/WorldHuntPixelArtTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"

for required in "$design" "$review" "$layout" "$catalog" "$pixel_art" "$screen" "$edit_tests" "$art_tests" "$play_tests"; do
  [ -f "$required" ] || { printf '[world-gameplay-polish] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[world-gameplay-polish 1/4] Validating final design and review...\n'
grep -q 'FINAL / IMPLEMENTATION READY' "$design"
grep -q 'UNRESOLVED=0' "$design"
grep -q 'PASS / IMPLEMENTATION MAY START' "$review"
grep -q 'UNRESOLVED=0' "$review"

printf '[world-gameplay-polish 2/4] Validating oblique world and connected hunting grounds...\n'
grep -q 'ProjectionShear = .16f' "$layout"
grep -q 'ProjectionVerticalScale = .74f' "$layout"
grep -q 'BuildRoadConnection' "$screen"
grep -q '사냥터지면_5' "$screen"

printf '[world-gameplay-polish 3/4] Validating buildings, mercenary detail, and visual assets...\n'
grep -q 'class WorldFacilityInteractionCatalog' "$catalog"
grep -q 'ShowFacility' "$screen"
grep -q 'ShowCharacterDetail' "$screen"
grep -q 'GetDetail(selectedMemberInstanceId)' "$screen"
grep -q 'FacilitySprite' "$pixel_art"
grep -q 'KingdomBuildingsOpenContextualKoreanFeaturePanels' "$play_tests"
grep -q 'MercenaryTapOpensRealStatusEquipmentAndActivityDetail' "$play_tests"
grep -q 'EveryVisibleFacilityHasTwoKoreanFeatureRoutes' "$edit_tests"
grep -q 'LibraryCreatesTwentySixPointFilteredCachedSpritesWithinBudget' "$art_tests"

printf '[world-gameplay-polish 4/4] Unity execution policy...\n'
if [ "${WORLD_GAMEPLAY_POLISH_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[world-gameplay-polish] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter 'KingdomTycoon.Tests.EditMode.MobileLivingWorldTests|KingdomTycoon.Tests.EditMode.WorldHuntPixelArtTests' -testResults "$repo_root/artifacts/world-gameplay-polish-editmode.xml" -logFile "$repo_root/artifacts/world-gameplay-polish-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/world-gameplay-polish-playmode.xml" -logFile "$repo_root/artifacts/world-gameplay-polish-playmode.log"
else
  printf '[world-gameplay-polish] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[world-gameplay-polish] Gate passed.\n'
