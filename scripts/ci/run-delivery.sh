#!/usr/bin/env bash
set -Eeuo pipefail

# Continuous Delivery stops at a downloadable, checksummed artifact bundle.
# Publishing to a production server or an app store is intentionally excluded.
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

release_name="${RELEASE_NAME:-local-delivery}"
safe_release_name="$(printf '%s' "$release_name" | tr -cs 'A-Za-z0-9._-' '-')"
safe_release_name="${safe_release_name#-}"
safe_release_name="${safe_release_name%-}"
[ -n "$safe_release_name" ] || safe_release_name="local-delivery"

delivery_dir="${DELIVERY_DIR:-${RUNNER_TEMP:-$repo_root/.delivery}/${safe_release_name}}"
if [ -e "$delivery_dir" ]; then
  printf '[delivery] Output directory already exists: %s\n' "$delivery_dir" >&2
  exit 1
fi

printf '[delivery] Preparing %s at %s.\n' "$safe_release_name" "$delivery_dir"

# Reuse the CI contract with a build task so delivery never packages a result
# that skipped the repository's normal validation gates.
SERVER_TASK="clean build" scripts/ci/run-ci.sh

artifact_dir="${delivery_dir}/artifacts"
mkdir -p "$artifact_dir"
artifact_count=0

if [ -d "server-api/build/libs" ]; then
  mkdir -p "${artifact_dir}/server"
  while IFS= read -r -d '' artifact; do
    cp "$artifact" "${artifact_dir}/server/"
    artifact_count=$((artifact_count + 1))
    printf '[delivery] Collected server artifact: %s\n' "$artifact"
  done < <(find server-api/build/libs -maxdepth 1 -type f -name '*.jar' -print0)
fi

# Unity builds require a licensed runner and are produced by a later build
# phase. If such outputs already exist, delivery can package them without
# embedding Unity credentials in this repository.
if [ -d "client-unity/Builds" ]; then
  while IFS= read -r -d '' artifact; do
    relative_path="${artifact#client-unity/Builds/}"
    destination="${artifact_dir}/unity/${relative_path}"
    mkdir -p "$(dirname "$destination")"
    cp "$artifact" "$destination"
    artifact_count=$((artifact_count + 1))
    printf '[delivery] Collected Unity artifact: %s\n' "$artifact"
  done < <(find client-unity/Builds -type f -print0)
fi

if [ "$artifact_count" -eq 0 ]; then
  cat > "${artifact_dir}/NO_BUILD_ARTIFACTS.txt" <<'EOF'
No server JAR or Unity build output was available.

This delivery run still records validation evidence and checksums. Server
artifacts will appear after a Gradle Wrapper and buildable server module are
committed. Unity artifacts require an approved licensed runner or a validated
local build copied under client-unity/Builds before packaging.
EOF
  artifact_count=1
  printf '[delivery] No build outputs found; added an explicit placeholder.\n'
fi

created_at="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
commit_sha="$(git rev-parse HEAD)"
cat > "${delivery_dir}/DELIVERY_MANIFEST.md" <<EOF
# Kingdom Tycoon delivery manifest

- Release: \`${safe_release_name}\`
- Commit: \`${commit_sha}\`
- Created at: \`${created_at}\`
- Artifact files: \`${artifact_count}\`
- Deployment: not performed (\`OPS_LATER\`)

This bundle is a Continuous Delivery output. It is not evidence of production
deployment, app-store publication, or infrastructure readiness.
EOF

(
  cd "$delivery_dir"
  find artifacts -type f -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS.txt
)

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    printf '\n## Delivery artifact\n\n'
    printf -- '- Release: `%s`\n' "$safe_release_name"
    printf -- '- Commit: `%s`\n' "${commit_sha:0:12}"
    printf -- '- Artifact files: %s\n' "$artifact_count"
    printf -- '- Production deployment: not performed (`OPS_LATER`)\n'
  } >> "$GITHUB_STEP_SUMMARY"
fi

printf '[delivery] Bundle ready with %s artifact file(s).\n' "$artifact_count"
printf '[delivery] Checksums: %s\n' "${delivery_dir}/SHA256SUMS.txt"
