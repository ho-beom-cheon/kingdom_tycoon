$ErrorActionPreference = "Stop"

$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) {
    throw "Git repository root could not be determined."
}

$hooksDirectory = Join-Path $repoRoot ".githooks"
$requiredHooks = @("pre-commit", "pre-push")

foreach ($hook in $requiredHooks) {
    $hookPath = Join-Path $hooksDirectory $hook
    if (-not (Test-Path -LiteralPath $hookPath)) {
        throw "Required Git hook is missing: $hookPath"
    }
}

git -C $repoRoot config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw "Failed to configure core.hooksPath."
}

$configuredPath = (git -C $repoRoot config --get core.hooksPath).Trim()
if ($configuredPath -ne ".githooks") {
    throw "Unexpected core.hooksPath: $configuredPath"
}

Write-Host "Git hooks configured: $configuredPath"
