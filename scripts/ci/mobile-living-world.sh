#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_MOBILE_LIVING_WORLD_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_MOBILE_LIVING_WORLD_COMPLETENESS_REVIEW_v1.0.md"
layout="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/MobileLivingWorldLayout.cs"
camera="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldMapDragSurface.cs"
assets="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/MobileWorldAssetLibrary.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
navigation="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Navigation/UnifiedNavigationMenu.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/MobileLivingWorldTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"
asset_root="client-unity/Assets/KingdomTycoon/Resources/ThirdParty/NinjaAdventure"

for required in "$design" "$review" "$layout" "$camera" "$assets" "$screen" "$navigation" "$edit_tests" "$play_tests" \
  "THIRD_PARTY_NOTICES.md" "data/csv/asset_register.csv" \
  "$asset_root/tileset_floor.png" "$asset_root/tileset_village_abandoned.png" "$asset_root/grass.png" "$asset_root/crate.png" "$asset_root/pot.png"; do
  [ -f "$required" ] || { printf '[mobile-living-world] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[mobile-living-world 1/4] Validating finalized implementation contract...\n'
grep -q 'FINAL / IMPLEMENTATION READY' "$design"
grep -q 'UNRESOLVED=0' "$design"
grep -q 'PASS / IMPLEMENTATION MAY START' "$review"
grep -q 'UNRESOLVED=0' "$review"

printf '[mobile-living-world 2/4] Validating world, camera, assignment, and navigation wiring...\n'
grep -q 'WorldSize = new(3000f, 3600f)' "$layout"
grep -q 'REGION_R01' "$layout"
grep -q 'REGION_R05' "$layout"
grep -q 'PanBy(Vector2 delta)' "$camera"
grep -q 'ZoomBy(float delta)' "$camera"
grep -q 'ConsumeTapSuppression' "$camera"
grep -q '사냥터배치시트' "$screen"
grep -q '통합메뉴버튼' "$screen"
grep -q 'OpenMercenaries' "$navigation"

printf '[mobile-living-world 3/4] Validating CC0 asset record, selective import, and test coverage...\n'
grep -q 'ASSET_CC0_NINJA_ADVENTURE_ENVIRONMENT' data/csv/asset_register.csv
grep -q 'CC0-1.0' data/csv/asset_register.csv
grep -q '6ac78232d5aedcc85ce5f27d060ea92366f7c24a' THIRD_PARTY_NOTICES.md
grep -q 'class MobileWorldAssetLibrary' "$assets"
grep -q 'MobileLivingWorldTests' "$edit_tests"
grep -q 'HuntingGroundTapOpensAssignmentSheetAndSupportsSeveralMercenaries' "$play_tests"
grep -q 'MobileWorldOpensAsKingdomHomeAndReplacesBottomNavigationWithTopMenu' "$play_tests"
[ "$(find "$asset_root" -maxdepth 1 -type f -name '*.png' | wc -l)" -eq 5 ]

printf '[mobile-living-world 4/4] Unity execution policy...\n'
if [ "${MOBILE_LIVING_WORLD_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[mobile-living-world] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.MobileLivingWorldTests -testResults "$repo_root/artifacts/mobile-living-world-editmode.xml" -logFile "$repo_root/artifacts/mobile-living-world-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/mobile-living-world-playmode.xml" -logFile "$repo_root/artifacts/mobile-living-world-playmode.log"
else
  printf '[mobile-living-world] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[mobile-living-world] Gate passed.\n'
