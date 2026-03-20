param(
    [ValidateSet("checklist", "refactor")]
    [string]$Mode = "checklist",
    [string]$RepoRoot = ".",
    [string]$CodexCommand = "codex",
    [string]$RefactorModeId = "architecture-refactor",

    [string]$TaskFile = "",
    [string]$StateFile = "",
    [string]$ConfigFile = "",
    [string]$StartFromTaskId = "",
    [int]$MaxAttemptsPerTask = 1,
    [int]$MaxIterations = 10,
    [int]$MaxNoProgress = 3,
    [int]$MaxNonCodeProgress = 2,
    [int]$MaxExecFailuresPerTask = 1,
    [int]$MaxNoProgressPerTask = 1,
    [int]$LockStaleAfterMinutes = 30,
    [ValidateSet("compact", "full")]
    [string]$PromptProfile = "compact",

    [switch]$DisableGatePreflight,
    [switch]$SkipManualValidation,
    [switch]$ForceReleaseWithoutManual,
    [switch]$SkipReleaseValidation,
    [switch]$SkipManualGates,
    [switch]$SkipAutoCommit,
    [switch]$NoRollback,
    [switch]$AllowDirtyWorkingTree,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptsRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path

if ($Mode -eq "checklist") {
    $runner = Join-Path $scriptsRoot "run-checklist-loop.ps1"
    $forward = @(
        "-RepoRoot", $RepoRoot,
        "-CodexCommand", $CodexCommand,
        "-MaxAttemptsPerTask", $MaxAttemptsPerTask,
        "-LockStaleAfterMinutes", $LockStaleAfterMinutes
    )
    if (-not [string]::IsNullOrWhiteSpace($TaskFile)) { $forward += @("-TaskFile", $TaskFile) }
    if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) { $forward += @("-StartFromTaskId", $StartFromTaskId) }
    if ($DisableGatePreflight.IsPresent) { $forward += "-DisableGatePreflight" }
    if ($SkipManualValidation.IsPresent) { $forward += "-SkipManualValidation" }
    if ($ForceReleaseWithoutManual.IsPresent) { $forward += "-ForceReleaseWithoutManual" }
    if ($SkipReleaseValidation.IsPresent) { $forward += "-SkipReleaseValidation" }
    if ($SkipAutoCommit.IsPresent) { $forward += "-SkipAutoCommit" }
    if ($NoRollback.IsPresent) { $forward += "-NoRollback" }
    if ($AllowDirtyWorkingTree.IsPresent) { $forward += "-AllowDirtyWorkingTree" }
    if ($DryRun.IsPresent) { $forward += "-DryRun" }

    & powershell -File $runner @forward
    exit $LASTEXITCODE
}

$runner = Join-Path $scriptsRoot "run-refactor-loop.ps1"
$forward = @(
    "-RepoRoot", $RepoRoot,
    "-Mode", $RefactorModeId,
    "-CodexCommand", $CodexCommand,
    "-MaxIterations", $MaxIterations,
    "-MaxNoProgress", $MaxNoProgress,
    "-MaxNonCodeProgress", $MaxNonCodeProgress,
    "-MaxExecFailuresPerTask", $MaxExecFailuresPerTask,
    "-MaxNoProgressPerTask", $MaxNoProgressPerTask,
    "-LockStaleAfterMinutes", $LockStaleAfterMinutes,
    "-PromptProfile", $PromptProfile
)
if (-not [string]::IsNullOrWhiteSpace($TaskFile)) { $forward += @("-TaskFile", $TaskFile) }
if (-not [string]::IsNullOrWhiteSpace($StateFile)) { $forward += @("-StateFile", $StateFile) }
if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $forward += @("-ConfigFile", $ConfigFile) }
if ($SkipManualGates.IsPresent) { $forward += "-SkipManualGates" }
if ($DryRun.IsPresent) { $forward += "-DryRun" }

& powershell -File $runner @forward
exit $LASTEXITCODE
