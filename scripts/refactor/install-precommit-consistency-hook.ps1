param(
    [string]$HookPath = ".git/hooks/pre-commit",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\\..")).Path
$checkScriptPath = Join-Path $repoRoot "scripts/refactor/check-doc-consistency.ps1"
$targetHookPath = Join-Path $repoRoot $HookPath

if (-not (Test-Path -LiteralPath $checkScriptPath)) {
    throw "Consistency check script not found: $checkScriptPath"
}

$marker = "# classroomtoolkit-refactor-doc-consistency-hook"
$hookBody = @(
    "#!/usr/bin/env sh"
    $marker
    "set -e"
    ""
    "REPO_ROOT=""`$(git rev-parse --show-toplevel 2>/dev/null || true)"""
    "if [ -z ""`$REPO_ROOT"" ]; then"
    "  exit 0"
    "fi"
    ""
    "PS_BIN=""pwsh"""
    "if command -v pwsh >/dev/null 2>&1; then"
    "  PS_BIN=""pwsh"""
    "fi"
    ""
    """`$PS_BIN"" -NoProfile -ExecutionPolicy Bypass -File ""`$REPO_ROOT/scripts/refactor/check-doc-consistency.ps1"" -AsJson"
    "status=`$?"
    "if [ `$status -ne 0 ]; then"
    "  echo ""[pre-commit] Refactor doc consistency check failed.""" 
    "  echo ""[pre-commit] Run: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/refactor/check-doc-consistency.ps1 -Fix"""
    "  exit `$status"
    "fi"
    ""
    "exit 0"
) -join "`n"

$hookDir = Split-Path -Parent $targetHookPath
if (-not (Test-Path -LiteralPath $hookDir)) {
    New-Item -ItemType Directory -Path $hookDir -Force | Out-Null
}

if (Test-Path -LiteralPath $targetHookPath) {
    $currentContent = Get-Content -LiteralPath $targetHookPath -Raw
    $managedByThisScript = $currentContent -match [regex]::Escape($marker)

    if (-not $managedByThisScript -and -not $Force) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupPath = "$targetHookPath.backup-$timestamp"
        Copy-Item -LiteralPath $targetHookPath -Destination $backupPath -Force
        Write-Host "Existing pre-commit hook backed up to: $backupPath"
    }
}

Set-Content -LiteralPath $targetHookPath -Value $hookBody -Encoding ASCII -NoNewline
Write-Host "Installed pre-commit hook: $targetHookPath"
