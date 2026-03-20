param(
    [string]$RepoRoot = ".",
    [string]$TaskFile = "docs/superpowers/plans/2026-03-20-terminal-architecture-closure.tasks.json",
    [string]$CodexCommand = "codex",
    [string]$StartFromTaskId = "",
    [int]$MaxAttemptsPerTask = 1,
    [switch]$DisableGatePreflight,
    [switch]$SkipManualValidation,
    [switch]$ForceReleaseWithoutManual,
    [switch]$SkipReleaseValidation,
    [switch]$SkipAutoCommit,
    [switch]$NoRollback,
    [switch]$AllowDirtyWorkingTree,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$runnerPath = Join-Path (Resolve-Path -LiteralPath $PSScriptRoot).Path "run-unattended-loop.ps1"
$forwardArgs = @(
    "-Mode", "checklist",
    "-RepoRoot", $RepoRoot,
    "-TaskFile", $TaskFile,
    "-CodexCommand", $CodexCommand,
    "-MaxAttemptsPerTask", $MaxAttemptsPerTask
)

if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) {
    $forwardArgs += @("-StartFromTaskId", $StartFromTaskId)
}
if ($DisableGatePreflight.IsPresent) {
    $forwardArgs += "-DisableGatePreflight"
}
if ($SkipManualValidation.IsPresent) {
    $forwardArgs += "-SkipManualValidation"
}
if ($ForceReleaseWithoutManual.IsPresent) {
    $forwardArgs += "-ForceReleaseWithoutManual"
}
if ($SkipReleaseValidation.IsPresent) {
    $forwardArgs += "-SkipReleaseValidation"
}
if ($SkipAutoCommit.IsPresent) {
    $forwardArgs += "-SkipAutoCommit"
}
if ($NoRollback.IsPresent) {
    $forwardArgs += "-NoRollback"
}
if ($AllowDirtyWorkingTree.IsPresent) {
    $forwardArgs += "-AllowDirtyWorkingTree"
}
if ($DryRun.IsPresent) {
    $forwardArgs += "-DryRun"
}

& powershell -File $runnerPath @forwardArgs
exit $LASTEXITCODE
