param(
    [string]$RepoRoot = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }
        return [System.IO.Path]::GetFullPath($Path)
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$root = Resolve-AbsolutePath -Path $RepoRoot
$runner = Join-Path $root "scripts/run-unattended-loop.ps1"

if (-not (Test-Path -LiteralPath $runner)) {
    throw "Missing unified runner: $runner"
}

# Contract 1: refactor dry-run normal path should be successful.
& powershell -ExecutionPolicy Bypass -File $runner `
    -Mode refactor `
    -RepoRoot $root `
    -DryRun `
    -MaxIterations 1 `
    -MaxWallClockMinutes 1 | Out-Null

$successExit = $LASTEXITCODE
if ($successExit -ne 0) {
    throw "Expected success exit code 0 for refactor dry-run, actual: $successExit"
}

# Contract 2: explicit invalid skill path must fail fast with blocker exit code.
& powershell -ExecutionPolicy Bypass -File $runner `
    -Mode refactor `
    -RepoRoot $root `
    -DryRun `
    -SkillPath ".codex/skills/__definitely_missing__/SKILL.md" `
    -MaxIterations 1 `
    -MaxWallClockMinutes 1 | Out-Null

$blockedExit = $LASTEXITCODE
if ($blockedExit -ne 2) {
    throw "Expected blocker exit code 2 for invalid skill path, actual: $blockedExit"
}

Write-Host "REFACTOR_EXIT_CONTRACT: PASS"
