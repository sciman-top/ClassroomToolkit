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
    [int]$MaxNonCodeProgress = 2,
    [int]$MaxExecFailuresPerTask = 1,
    [int]$MaxNoProgressPerTask = 1,
    [int]$IterationTimeoutSeconds = 900,
    [int]$IdleTimeoutSeconds = 120,
    [int]$LockStaleAfterMinutes = 30,
    [ValidateSet("compact", "full")]
    [string]$PromptProfile = "compact",
    [switch]$SkipManualGates,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$wrapperPath = Join-Path (Resolve-Path -LiteralPath $PSScriptRoot).Path "run-unattended-loop.ps1"
$forwardArgs = @(
    "-Mode", "refactor",
    "-RepoRoot", $RepoRoot,
    "-RefactorModeId", $Mode,
    "-CodexCommand", $CodexCommand,
    "-MaxIterations", $MaxIterations,
    "-MaxNoProgress", $MaxNoProgress,
    "-MaxNonCodeProgress", $MaxNonCodeProgress,
    "-MaxExecFailuresPerTask", $MaxExecFailuresPerTask,
    "-MaxNoProgressPerTask", $MaxNoProgressPerTask,
    "-IterationTimeoutSeconds", $IterationTimeoutSeconds,
    "-IdleTimeoutSeconds", $IdleTimeoutSeconds,
    "-LockStaleAfterMinutes", $LockStaleAfterMinutes,
    "-PromptProfile", $PromptProfile
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

if ($SkipManualGates.IsPresent) {
    $forwardArgs += "-SkipManualGates"
}

if ($DryRun.IsPresent) {
    $forwardArgs += "-DryRun"
}

& powershell -File $wrapperPath @forwardArgs
