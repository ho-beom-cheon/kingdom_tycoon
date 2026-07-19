#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

design="docs/design/TYCOON_P16_SERVER_SKELETON_COMPLETE_DESIGN_v1.0.md"
review="docs/reports/P16_DESIGN_COMPLETENESS_REVIEW.md"
session="server-api/src/main/java/com/kingdomtycoon/server/session/DevelopmentSessionService.java"
controller="server-api/src/main/java/com/kingdomtycoon/server/api/SpecialRecruitmentController.java"
gateway="client-unity/Assets/KingdomTycoon/Runtime/Infrastructure/Recruitment/P16ServerRecruitmentGateway.cs"
server_test="server-api/src/test/java/com/kingdomtycoon/server/P16ServerApiIntegrationTest.java"
unity_test="client-unity/Assets/KingdomTycoon/Tests/EditMode/P16ServerAdapterTests.cs"

printf '[p16 1/3] Validating final design and completeness review...\n'
for required in "$design" "$review" "$session" "$controller" "$gateway" "$server_test" "$unity_test"; do
  [ -f "$required" ] || { printf '[p16] Missing required file: %s\n' "$required" >&2; exit 1; }
done
grep -q '구현 가능 (`PASS`)' "$review"
grep -q '구현을 막는 `UNRESOLVED`는 없다' "$review"

printf '[p16 2/3] Validating server authority and HTTP adapter boundaries...\n'
grep -q 'Idempotency-Key' "$controller"
grep -q 'SummonApplicationService' "$controller"
grep -q 'class HttpRecruitmentGateway' "$gateway"
grep -q 'P16_SERVER_UNAVAILABLE' "$gateway"
grep -q 'class P16ServerApiIntegrationTest' "$server_test"
grep -q 'class P16ServerAdapterTests' "$unity_test"

printf '[p16 3/3] Validating migration and secret policy...\n'
[ ! -f server-api/src/main/resources/db/migration/V018__p16_server_skeleton.sql ] \
  || { printf '[p16] P16 must reuse V001-V017 unless an approved forward migration is documented.\n' >&2; exit 1; }
! grep -R -nE '(TYCOON_APP_PASSWORD|TYCOON_MIGRATOR_PASSWORD):[[:space:]]+[^$]' server-api/src/main/resources \
  || { printf '[p16] A database secret appears to be hardcoded.\n' >&2; exit 1; }

printf '[p16] Gate passed. Licensed Unity and PostgreSQL execution are reported separately.\n'
