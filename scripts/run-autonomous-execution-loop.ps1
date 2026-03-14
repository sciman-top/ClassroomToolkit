param(
    [string]$RepoRoot = ".",
    [string]$Mode = "architecture-refactor",
    [string]$TaskFile = "docs/refactor/tasks.json",
    [string]$StateFile = ".codex/refactor-state.json",
    [string]$ConfigFile = "",
    [string]$CodexCommand = "codex",
    [string]$SkillPath = "",
    [int]$MaxIterations = 10,
    [int]$MaxNoProgress = 3,
    [int]$IterationTimeoutSeconds = 900,
    [int]$IdleTimeoutSeconds = 120,
    [int]$LockStaleAfterMinutes = 30,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$wrapperPath = Join-Path (Resolve-Path -LiteralPath $PSScriptRoot).Path "run-refactor-loop.ps1"
$forwardArgs = @(
    "-RepoRoot", $RepoRoot,
    "-Mode", $Mode,
    "-CodexCommand", $CodexCommand,
    "-MaxIterations", $MaxIterations,
    "-MaxNoProgress", $MaxNoProgress,
    "-IterationTimeoutSeconds", $IterationTimeoutSeconds,
    "-IdleTimeoutSeconds", $IdleTimeoutSeconds,
    "-LockStaleAfterMinutes", $LockStaleAfterMinutes
)

if (-not [string]::IsNullOrWhiteSpace($SkillPath)) {
    $forwardArgs += @("-SkillPath", $SkillPath)
}

if ($PSBoundParameters.ContainsKey("TaskFile")) {
    $forwardArgs += @("-TaskFile", $TaskFile)
}

if ($PSBoundParameters.ContainsKey("StateFile")) {
    $forwardArgs += @("-StateFile", $StateFile)
}

if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    $forwardArgs += @("-ConfigFile", $ConfigFile)
}

if ($DryRun.IsPresent) {
    $forwardArgs += "-DryRun"
}

& powershell -File $wrapperPath @forwardArgs
