param(
    [ValidateSet("checklist", "refactor")]
    [string]$Mode = "checklist",
    [string]$RepoRoot = ".",
    [string]$CodexCommand = "codex",
    [string]$RefactorModeId = "architecture-refactor",

    [string]$TaskFile = "",
    [string]$StateFile = "",
    [string]$ConfigFile = "",
    [string]$GuardProfileFile = ".codex/unattended-loop.guard.json",
    [string]$StartFromTaskId = "",
    [int]$MaxAttemptsPerTask = 1,
    [int]$MaxTaskRetries = 1,
    [int]$MaxIterations = 10,
    [int]$MaxNoProgress = 3,
    [int]$MaxNonCodeProgress = 2,
    [int]$MaxExecFailuresPerTask = 1,
    [int]$MaxNoProgressPerTask = 1,
    [bool]$StopOnMissingStatusLine = $true,
    [bool]$StopOnRepeatedFailureSignature = $true,
    [int]$LockStaleAfterMinutes = 30,
    [int]$IterationTimeoutSeconds = 900,
    [int]$IdleTimeoutSeconds = 120,
    [int]$CodexTimeoutSeconds = 1200,
    [int]$CodexIdleTimeoutSeconds = 180,
    [int]$GateTimeoutSeconds = 900,
    [int]$GateIdleTimeoutSeconds = 120,
    [int]$MaxWallClockMinutes = 120,
    [int]$MaxCodexRuns = 50,
    [string]$SkillPath = "",
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
    [switch]$EnableCompatibilityArtifacts,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:UserBoundParameters = @{}
foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $script:UserBoundParameters[[string]$entry.Key] = $true
}

$scriptsRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path

function Resolve-InputPath {
    param(
        [string]$RootPath,
        [string]$InputPath
    )

    if ([System.IO.Path]::IsPathRooted($InputPath)) {
        return $InputPath
    }

    return Join-Path $RootPath $InputPath
}

function Read-GuardProfile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Test-UserBoundParameter {
    param([string]$Name)
    return $script:UserBoundParameters.ContainsKey($Name)
}

function Try-ApplyGuardInt {
    param(
        [object]$Section,
        [string]$PropertyName,
        [string]$BoundParamName,
        [int]$MinValue,
        [ref]$Target
    )

    if ($null -eq $Section -or $null -eq $Section.PSObject.Properties[$PropertyName]) {
        return
    }

    if (Test-UserBoundParameter -Name $BoundParamName) {
        return
    }

    $value = [int]$Section.$PropertyName
    if ($value -ge $MinValue) {
        $Target.Value = $value
    }
}

function Try-ApplyGuardBool {
    param(
        [object]$Section,
        [string]$PropertyName,
        [string]$BoundParamName,
        [ref]$Target
    )

    if ($null -eq $Section -or $null -eq $Section.PSObject.Properties[$PropertyName]) {
        return
    }

    if (Test-UserBoundParameter -Name $BoundParamName) {
        return
    }

    $Target.Value = [bool]$Section.$PropertyName
}

$guardProfilePath = Resolve-InputPath -RootPath $repoPath -InputPath $GuardProfileFile
$guardProfile = Read-GuardProfile -Path $guardProfilePath
if ($null -ne $guardProfile) {
    $guardSection = $null
    if ($null -ne $guardProfile.PSObject.Properties["execution_guard"]) {
        $guardSection = $guardProfile.execution_guard
    }
    if ($null -ne $guardSection) {
        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_retries_per_task" -BoundParamName "MaxTaskRetries" -MinValue 1 -Target ([ref]$MaxTaskRetries)
        if (-not (Test-UserBoundParameter -Name "MaxAttemptsPerTask")) { $MaxAttemptsPerTask = $MaxTaskRetries }
        if (-not (Test-UserBoundParameter -Name "MaxExecFailuresPerTask")) { $MaxExecFailuresPerTask = $MaxTaskRetries }
        if (-not (Test-UserBoundParameter -Name "MaxNoProgressPerTask")) { $MaxNoProgressPerTask = $MaxTaskRetries }

        Try-ApplyGuardBool -Section $guardSection -PropertyName "stop_on_missing_status_line" -BoundParamName "StopOnMissingStatusLine" -Target ([ref]$StopOnMissingStatusLine)
        Try-ApplyGuardBool -Section $guardSection -PropertyName "stop_on_repeated_failure_signature" -BoundParamName "StopOnRepeatedFailureSignature" -Target ([ref]$StopOnRepeatedFailureSignature)

        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_wall_clock_minutes" -BoundParamName "MaxWallClockMinutes" -MinValue 1 -Target ([ref]$MaxWallClockMinutes)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_codex_runs" -BoundParamName "MaxCodexRuns" -MinValue 1 -Target ([ref]$MaxCodexRuns)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "codex_timeout_seconds" -BoundParamName "CodexTimeoutSeconds" -MinValue 1 -Target ([ref]$CodexTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "codex_idle_timeout_seconds" -BoundParamName "CodexIdleTimeoutSeconds" -MinValue 1 -Target ([ref]$CodexIdleTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "gate_timeout_seconds" -BoundParamName "GateTimeoutSeconds" -MinValue 1 -Target ([ref]$GateTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "gate_idle_timeout_seconds" -BoundParamName "GateIdleTimeoutSeconds" -MinValue 1 -Target ([ref]$GateIdleTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "iteration_timeout_seconds" -BoundParamName "IterationTimeoutSeconds" -MinValue 1 -Target ([ref]$IterationTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "idle_timeout_seconds" -BoundParamName "IdleTimeoutSeconds" -MinValue 1 -Target ([ref]$IdleTimeoutSeconds)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_iterations" -BoundParamName "MaxIterations" -MinValue 1 -Target ([ref]$MaxIterations)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_no_progress" -BoundParamName "MaxNoProgress" -MinValue 1 -Target ([ref]$MaxNoProgress)
        Try-ApplyGuardInt -Section $guardSection -PropertyName "max_non_code_progress" -BoundParamName "MaxNonCodeProgress" -MinValue 1 -Target ([ref]$MaxNonCodeProgress)
    }

    if ($Mode -eq "checklist" -and $null -ne $guardProfile.PSObject.Properties["checklist"]) {
        $checklistGuard = $guardProfile.checklist
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "max_attempts_per_task" -BoundParamName "MaxAttemptsPerTask" -MinValue 1 -Target ([ref]$MaxAttemptsPerTask)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "max_codex_runs" -BoundParamName "MaxCodexRuns" -MinValue 1 -Target ([ref]$MaxCodexRuns)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "max_wall_clock_minutes" -BoundParamName "MaxWallClockMinutes" -MinValue 1 -Target ([ref]$MaxWallClockMinutes)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "codex_timeout_seconds" -BoundParamName "CodexTimeoutSeconds" -MinValue 1 -Target ([ref]$CodexTimeoutSeconds)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "codex_idle_timeout_seconds" -BoundParamName "CodexIdleTimeoutSeconds" -MinValue 1 -Target ([ref]$CodexIdleTimeoutSeconds)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "gate_timeout_seconds" -BoundParamName "GateTimeoutSeconds" -MinValue 1 -Target ([ref]$GateTimeoutSeconds)
        Try-ApplyGuardInt -Section $checklistGuard -PropertyName "gate_idle_timeout_seconds" -BoundParamName "GateIdleTimeoutSeconds" -MinValue 1 -Target ([ref]$GateIdleTimeoutSeconds)
    }

    if ($Mode -eq "refactor" -and $null -ne $guardProfile.PSObject.Properties["refactor"]) {
        $refactorGuard = $guardProfile.refactor
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_task_retries" -BoundParamName "MaxTaskRetries" -MinValue 1 -Target ([ref]$MaxTaskRetries)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_exec_failures_per_task" -BoundParamName "MaxExecFailuresPerTask" -MinValue 1 -Target ([ref]$MaxExecFailuresPerTask)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_no_progress_per_task" -BoundParamName "MaxNoProgressPerTask" -MinValue 1 -Target ([ref]$MaxNoProgressPerTask)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_iterations" -BoundParamName "MaxIterations" -MinValue 1 -Target ([ref]$MaxIterations)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_no_progress" -BoundParamName "MaxNoProgress" -MinValue 1 -Target ([ref]$MaxNoProgress)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_non_code_progress" -BoundParamName "MaxNonCodeProgress" -MinValue 1 -Target ([ref]$MaxNonCodeProgress)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "iteration_timeout_seconds" -BoundParamName "IterationTimeoutSeconds" -MinValue 1 -Target ([ref]$IterationTimeoutSeconds)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "idle_timeout_seconds" -BoundParamName "IdleTimeoutSeconds" -MinValue 1 -Target ([ref]$IdleTimeoutSeconds)
        Try-ApplyGuardInt -Section $refactorGuard -PropertyName "max_wall_clock_minutes" -BoundParamName "MaxWallClockMinutes" -MinValue 1 -Target ([ref]$MaxWallClockMinutes)

        Try-ApplyGuardBool -Section $refactorGuard -PropertyName "stop_on_missing_status_line" -BoundParamName "StopOnMissingStatusLine" -Target ([ref]$StopOnMissingStatusLine)
        Try-ApplyGuardBool -Section $refactorGuard -PropertyName "stop_on_repeated_failure_signature" -BoundParamName "StopOnRepeatedFailureSignature" -Target ([ref]$StopOnRepeatedFailureSignature)
    }
}

if ($Mode -eq "checklist") {
    $runner = Join-Path $scriptsRoot "run-checklist-loop.ps1"
    $forward = @(
        "-RepoRoot", $repoPath,
        "-CodexCommand", $CodexCommand,
        "-MaxAttemptsPerTask", $MaxAttemptsPerTask,
        "-LockStaleAfterMinutes", $LockStaleAfterMinutes,
        "-CodexTimeoutSeconds", $CodexTimeoutSeconds,
        "-CodexIdleTimeoutSeconds", $CodexIdleTimeoutSeconds,
        "-GateTimeoutSeconds", $GateTimeoutSeconds,
        "-GateIdleTimeoutSeconds", $GateIdleTimeoutSeconds,
        "-MaxWallClockMinutes", $MaxWallClockMinutes,
        "-MaxCodexRuns", $MaxCodexRuns
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
    if ($EnableCompatibilityArtifacts.IsPresent) { $forward += "-EnableCompatibilityArtifacts" }
    if ($DryRun.IsPresent) { $forward += "-DryRun" }

    & powershell -File $runner @forward
    exit $LASTEXITCODE
}

$runner = Join-Path $scriptsRoot "run-refactor-loop.ps1"
$forward = @(
    "-RepoRoot", $repoPath,
    "-Mode", $RefactorModeId,
    "-CodexCommand", $CodexCommand,
    "-GuardProfileFile", $guardProfilePath,
    "-MaxTaskRetries", $MaxTaskRetries,
    "-MaxIterations", $MaxIterations,
    "-MaxNoProgress", $MaxNoProgress,
    "-MaxNonCodeProgress", $MaxNonCodeProgress,
    "-MaxExecFailuresPerTask", $MaxExecFailuresPerTask,
    "-MaxNoProgressPerTask", $MaxNoProgressPerTask,
    "-IterationTimeoutSeconds", $IterationTimeoutSeconds,
    "-IdleTimeoutSeconds", $IdleTimeoutSeconds,
    "-MaxWallClockMinutes", $MaxWallClockMinutes,
    "-LockStaleAfterMinutes", $LockStaleAfterMinutes,
    "-PromptProfile", $PromptProfile
)
if (-not [string]::IsNullOrWhiteSpace($TaskFile)) { $forward += @("-TaskFile", $TaskFile) }
if (-not [string]::IsNullOrWhiteSpace($StateFile)) { $forward += @("-StateFile", $StateFile) }
if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $forward += @("-ConfigFile", $ConfigFile) }
if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) { $forward += @("-StartFromTaskId", $StartFromTaskId) }
if (-not [string]::IsNullOrWhiteSpace($SkillPath)) { $forward += @("-SkillPath", $SkillPath) }
if ($SkipManualGates.IsPresent) { $forward += "-SkipManualGates" }
if ($DryRun.IsPresent) { $forward += "-DryRun" }

& powershell -File $runner @forward
exit $LASTEXITCODE
