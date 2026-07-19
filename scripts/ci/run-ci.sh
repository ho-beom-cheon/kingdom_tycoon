#!/usr/bin/env bash
set -Eeuo pipefail

# This script is the shared CI contract. GitHub Actions calls it, and developers
# can run the same checks locally without having to reproduce workflow internals.
pipeline_name="repository-ci"
pipeline_started=$SECONDS
step_started=$SECONDS
current_step="initialization"

append_summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    printf '%s\n' "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
}

record_result() {
  local component="$1"
  local result="$2"
  local detail="$3"
  printf '[%s] %s: %s — %s\n' "$pipeline_name" "$result" "$component" "$detail"
  append_summary "- **${component}**: ${result} — ${detail}"
}

fail() {
  local message="$1"
  record_result "$current_step" "FAILED" "$message"
  printf '[%s] Pipeline failed after %ss.\n' \
    "$pipeline_name" "$((SECONDS - pipeline_started))" >&2
  exit 1
}

handle_error() {
  local exit_code=$?
  record_result "$current_step" "FAILED" "command exited with code ${exit_code}"
  printf '[%s] Pipeline failed after %ss.\n' \
    "$pipeline_name" "$((SECONDS - pipeline_started))" >&2
  exit "$exit_code"
}

start_step() {
  local number="$1"
  local total="$2"
  local description="$3"
  current_step="$description"
  step_started=$SECONDS
  printf '\n[%s %s/%s] %s...\n' "$pipeline_name" "$number" "$total" "$description"
}

finish_step() {
  local detail="$1"
  record_result "$current_step" "PASSED" "${detail}; $((SECONDS - step_started))s"
}

skip_step() {
  local reason="$1"
  record_result "$current_step" "SKIPPED" "$reason"
}

trap handle_error ERR

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

branch_name="$(git branch --show-current)"
if [ -z "$branch_name" ]; then
  branch_name="${GITHUB_REF_NAME:-detached HEAD}"
fi

printf '[%s] Pipeline started (branch: %s, commit: %s).\n' \
  "$pipeline_name" "$branch_name" "$(git rev-parse --short HEAD)"
append_summary "## Repository CI summary"
append_summary ""
append_summary "Commit: \`$(git rev-parse --short HEAD)\`"
append_summary ""

start_step 1 4 "Git Hook syntax"
for hook in .githooks/pre-commit .githooks/pre-push; do
  [ -f "$hook" ] || fail "required hook is missing: ${hook}"
  bash -n "$hook"
done
finish_step "repository-managed hooks parsed successfully"

start_step 2 4 "Content data validation"
validator="scripts/validate_content.py"
canonical_generator="scripts/generate_canonical_content.py"
p04_generator="scripts/generate_p04_content.py"
p05_generator="scripts/generate_p05_content.py"
p06_generator="scripts/generate_p06_content.py"
p07_gate="scripts/ci/p07.sh"
p08_gate="scripts/ci/p08.sh"
p09_gate="scripts/ci/p09.sh"
p10_gate="scripts/ci/p10.sh"
p11_gate="scripts/ci/p11.sh"
p12_gate="scripts/ci/p12.sh"
p13_gate="scripts/ci/p13.sh"
p14_gate="scripts/ci/p14.sh"

if [ -f "$validator" ]; then
  command -v python >/dev/null 2>&1 || fail "Python is required for ${validator}"
  [ -f "$canonical_generator" ] || fail "canonical content generator is missing: ${canonical_generator}"
  [ -f "$p04_generator" ] || fail "P04 content generator is missing: ${p04_generator}"
  [ -f "$p05_generator" ] || fail "P05 content generator is missing: ${p05_generator}"
  [ -f "$p06_generator" ] || fail "P06 content generator is missing: ${p06_generator}"
  [ -f "$p07_gate" ] || fail "P07 CI gate is missing: ${p07_gate}"
  [ -f "$p08_gate" ] || fail "P08 CI gate is missing: ${p08_gate}"
  [ -f "$p09_gate" ] || fail "P09 CI gate is missing: ${p09_gate}"
  [ -f "$p10_gate" ] || fail "P10 CI gate is missing: ${p10_gate}"
  [ -f "$p11_gate" ] || fail "P11 CI gate is missing: ${p11_gate}"
  [ -f "$p12_gate" ] || fail "P12 CI gate is missing: ${p12_gate}"
  [ -f "$p13_gate" ] || fail "P13 CI gate is missing: ${p13_gate}"
  [ -f "$p14_gate" ] || fail "P14 CI gate is missing: ${p14_gate}"
  printf '[%s] Canonical package: %s --check\n' "$pipeline_name" "$canonical_generator"
  PYTHONUNBUFFERED=1 python -u "$canonical_generator" --check
  printf '[%s] P04 package: %s --check\n' "$pipeline_name" "$p04_generator"
  PYTHONUNBUFFERED=1 python -u "$p04_generator" --check
  printf '[%s] P05 package: %s --check\n' "$pipeline_name" "$p05_generator"
  PYTHONUNBUFFERED=1 python -u "$p05_generator" --check
  printf '[%s] P06 package: %s --check\n' "$pipeline_name" "$p06_generator"
  PYTHONUNBUFFERED=1 python -u "$p06_generator" --check
  printf '[%s] P07 gate: %s\n' "$pipeline_name" "$p07_gate"
  bash "$p07_gate"
  printf '[%s] P08 gate: %s\n' "$pipeline_name" "$p08_gate"
  bash "$p08_gate"
  printf '[%s] P09 gate: %s\n' "$pipeline_name" "$p09_gate"
  bash "$p09_gate"
  printf '[%s] P10 gate: %s\n' "$pipeline_name" "$p10_gate"
  bash "$p10_gate"
  printf '[%s] P11 gate: %s\n' "$pipeline_name" "$p11_gate"
  bash "$p11_gate"
  printf '[%s] P12 gate: %s\n' "$pipeline_name" "$p12_gate"
  bash "$p12_gate"
  printf '[%s] P13 gate: %s\n' "$pipeline_name" "$p13_gate"
  bash "$p13_gate"
  printf '[%s] P14 gate: %s\n' "$pipeline_name" "$p14_gate"
  bash "$p14_gate"
  printf '[%s] Validator: %s\n' "$pipeline_name" "$validator"
  PYTHONUNBUFFERED=1 python -u "$validator"
  finish_step "${validator} completed"
else
  skip_step "content validator is not present on this branch"
fi

start_step 3 4 "Server clean test"
gradle_dir=""
if [ -f "gradlew" ]; then
  gradle_dir="."
elif [ -f "server-api/gradlew" ]; then
  gradle_dir="server-api"
fi

if [ -n "$gradle_dir" ]; then
  command -v java >/dev/null 2>&1 || fail "Java is required when a Gradle Wrapper is present"
  read -r -a server_tasks <<< "${SERVER_TASK:-clean test}"
  (
    cd "$gradle_dir"
    chmod +x gradlew
    ./gradlew "${server_tasks[@]}"
  )
  finish_step "Gradle task '${SERVER_TASK:-clean test}' completed in ${gradle_dir}"
else
  skip_step "Gradle Wrapper is not present; server build files are not treated as runnable"
fi

start_step 4 4 "Unity project policy"
unity_root="client-unity"
project_version="${unity_root}/ProjectSettings/ProjectVersion.txt"

if [ -f "$project_version" ]; then
  [ -d "${unity_root}/Assets" ] || fail "${unity_root}/Assets is missing"
  [ -f "${unity_root}/Packages/manifest.json" ] || fail "Unity package manifest is missing"

  grep -q '^m_EditorVersion: 6000\.3\.20f1$' "$project_version" \
    || fail "Unity Editor must be pinned to 6000.3.20f1"
  grep -q '^m_EditorVersionWithRevision: 6000\.3\.20f1 (c9ba695d4f07)$' "$project_version" \
    || fail "Unity Editor revision must be pinned to c9ba695d4f07"

  editor_settings="${unity_root}/ProjectSettings/EditorSettings.asset"
  [ -f "$editor_settings" ] || fail "Unity EditorSettings.asset is missing"

  version_control_settings="${unity_root}/ProjectSettings/VersionControlSettings.asset"
  if grep -q 'm_ExternalVersionControlSupport: Visible Meta Files' "$editor_settings"; then
    :
  elif [ -f "$version_control_settings" ] \
    && grep -q 'm_Mode: Visible Meta Files' "$version_control_settings"; then
    :
  else
    fail "Unity Version Control must be Visible Meta Files"
  fi

  grep -Eq 'm_(Asset)?SerializationMode: 2' "$editor_settings" \
    || fail "Unity Asset Serialization must be Force Text"

  missing_meta=0
  while IFS= read -r -d '' asset; do
    if [ ! -f "${asset}.meta" ]; then
      printf '[%s] Missing Unity meta: %s.meta\n' "$pipeline_name" "$asset" >&2
      missing_meta=$((missing_meta + 1))
    fi
  done < <(find "${unity_root}/Assets" -mindepth 1 ! -name '*.meta' -print0)

  [ "$missing_meta" -eq 0 ] || fail "${missing_meta} Unity Asset paths are missing .meta files"
  finish_step "Unity structure, serialization policy, and Asset metadata are valid"
else
  skip_step "Unity project is not present; licensed EditMode/PlayMode execution remains deferred"
fi

append_summary ""
append_summary "Total duration: $((SECONDS - pipeline_started))s"
printf '\n[%s] Pipeline passed in %ss.\n' \
  "$pipeline_name" "$((SECONDS - pipeline_started))"
