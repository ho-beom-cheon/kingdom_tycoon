#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_CORE_HUNT_VISUAL_POLISH_COMPLETE_DESIGN_v1.0.md"
review="docs/reviews/CORE_HUNT_VISUAL_POLISH_COMPLETENESS_REVIEW_v1.0.md"
library="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/WorldHuntPixelArtLibrary.cs"
screen="client-unity/Assets/KingdomTycoon/Runtime/Presentation/Combat/ContinuousHuntScreenPresenter.cs"
edit_tests="client-unity/Assets/KingdomTycoon/Tests/EditMode/WorldHuntPixelArtTests.cs"
play_tests="client-unity/Assets/KingdomTycoon/Tests/PlayMode/ContinuousHuntScreenTests.cs"

for required in "$design" "$review" "$library" "$screen" "$edit_tests" "$play_tests"; do
  [ -f "$required" ] || { printf '[hunt-visual-polish] Missing required file: %s\n' "$required" >&2; exit 1; }
done

printf '[hunt-visual-polish 1/4] Validating finalized zero-cost visual contract...\n'
grep -q 'FINAL / IMPLEMENTATION READY' "$design"
grep -q 'UNRESOLVED=0' "$review"
grep -q '외부 자산 0건' "$review"
grep -q '실제 집행 0원' "$review"

printf '[hunt-visual-polish 2/4] Validating deterministic pixel library and presentation wiring...\n'
grep -q 'class WorldHuntPixelArtLibrary' "$library"
grep -q 'FilterMode.Point' "$library"
grep -q 'TextureWrapMode.Clamp' "$library"
grep -q 'SpriteCount' "$library"
grep -q '왕국픽셀랜드마크' "$screen"
grep -q '환경장식_' "$screen"
grep -q 'visuals.MonsterSprite' "$screen"
grep -q 'visuals.JobSprite' "$screen"

printf '[hunt-visual-polish 3/4] Validating asset, layout and lifecycle coverage...\n'
grep -q 'LibraryCreatesTwentySixPointFilteredCachedSpritesWithinBudget' "$edit_tests"
grep -q 'JobsThemesAndEliteShapesHaveDistinctPixelFingerprints' "$edit_tests"
grep -q 'DisposeDestroysOwnedAssetsAndRejectsFurtherAccess' "$edit_tests"
grep -q 'PixelArtReplacesFlatActorsMonstersEnvironmentAndKingdomBlocks' "$play_tests"
grep -q 'SupportedAspectsKeepAllTouchTargetsInsideSafeAreaWithoutOverlap' "$play_tests"

printf '[hunt-visual-polish 4/4] Unity execution policy...\n'
if [ "${HUNT_VISUAL_POLISH_RUN_UNITY:-0}" = "1" ]; then
  [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ] || { printf '[hunt-visual-polish] UNITY_EDITOR is required.\n' >&2; exit 1; }
  mkdir -p artifacts
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_root/client-unity" -runTests -testPlatform editmode -testFilter KingdomTycoon.Tests.EditMode.WorldHuntPixelArtTests -testResults "$repo_root/artifacts/hunt-visual-polish-editmode.xml" -logFile "$repo_root/artifacts/hunt-visual-polish-editmode.log"
  "$UNITY_EDITOR" -batchmode -projectPath "$repo_root/client-unity" -runTests -testPlatform playmode -testFilter KingdomTycoon.Tests.PlayMode.ContinuousHuntScreenTests -testResults "$repo_root/artifacts/hunt-visual-polish-playmode.xml" -logFile "$repo_root/artifacts/hunt-visual-polish-playmode.log"
else
  printf '[hunt-visual-polish] Unity execution skipped; enable it on a licensed runner.\n'
fi

printf '[hunt-visual-polish] Gate passed.\n'
