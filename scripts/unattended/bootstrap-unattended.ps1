param(
    [ValidateSet("checklist", "refactor")]
    [string]$Mode = "refactor",
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
    [int]$IterationTimeoutSeconds = 900,
    [int]$IdleTimeoutSeconds = 120,
    [int]$CodexTimeoutSeconds = 1200,
    [int]$CodexIdleTimeoutSeconds = 180,
    [int]$GateTimeoutSeconds = 900,
    [int]$GateIdleTimeoutSeconds = 120,
    [int]$MaxWallClockMinutes = 120,
    [int]$MaxCodexRuns = 50,
    [ValidateSet("compact", "full")]
    [string]$PromptProfile = "compact",

    [string]$SyncScript = "",
    [string]$RuntimeSkillPath = "",
    [string]$OverrideSkillPath = "E:\CODE\skills-manager\overrides\autonomous-execution-loop\SKILL.md",
    [switch]$SkipSkillSync,
    [switch]$PreferOverrideSkill,

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

function Resolve-AbsolutePath {
    param(
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }
        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Resolve-DefaultRuntimeSkillPath {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return (Join-Path $env:CODEX_HOME "skills/autonomous-execution-loop/SKILL.md")
    }

    if (-not [string]::IsNullOrWhiteSpace($HOME)) {
        return (Join-Path $HOME ".codex/skills/autonomous-execution-loop/SKILL.md")
    }

    throw "Unable to resolve default runtime skill path: CODEX_HOME and HOME are both empty."
}

function Resolve-EffectiveSkillPath {
    param(
        [string]$RuntimePath,
        [string]$OverridePath,
        [bool]$PreferOverride
    )

    $ordered = @()
    if ($PreferOverride) {
        $ordered += $OverridePath
        $ordered += $RuntimePath
    }
    else {
        $ordered += $RuntimePath
        $ordered += $OverridePath
    }

    foreach ($candidate in $ordered) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "No usable skill path found. Checked runtime='$RuntimePath' and override='$OverridePath'."
}

$repoPath = Resolve-AbsolutePath -Path $RepoRoot
$entryScript = Join-Path $repoPath "scripts/run-unattended-loop.ps1"
if (-not (Test-Path -LiteralPath $entryScript)) {
    throw "Unified unattended entrypoint not found: $entryScript"
}

$resolvedRuntimeSkillPath = if ([string]::IsNullOrWhiteSpace($RuntimeSkillPath)) {
    Resolve-DefaultRuntimeSkillPath
}
else {
    Resolve-AbsolutePath -Path $RuntimeSkillPath -BasePath $repoPath
}

$resolvedOverrideSkillPath = Resolve-AbsolutePath -Path $OverrideSkillPath -BasePath $repoPath

if ($Mode -eq "refactor" -and -not $SkipSkillSync.IsPresent -and -not [string]::IsNullOrWhiteSpace($SyncScript)) {
    $syncScriptPath = Resolve-AbsolutePath -Path $SyncScript -BasePath $repoPath
    if (-not (Test-Path -LiteralPath $syncScriptPath)) {
        throw "Sync script not found: $syncScriptPath"
    }

    Write-Host "SKILL_SYNC: running $syncScriptPath"
    & powershell -ExecutionPolicy Bypass -File $syncScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Skill sync failed with exit code $($LASTEXITCODE): $syncScriptPath"
    }
    Write-Host "SKILL_SYNC: done"
}

$forward = @(
    "-Mode", $Mode,
    "-RepoRoot", $repoPath,
    "-CodexCommand", $CodexCommand,
    "-RefactorModeId", $RefactorModeId,
    "-MaxAttemptsPerTask", $MaxAttemptsPerTask,
    "-MaxIterations", $MaxIterations,
    "-MaxNoProgress", $MaxNoProgress,
    "-MaxNonCodeProgress", $MaxNonCodeProgress,
    "-MaxExecFailuresPerTask", $MaxExecFailuresPerTask,
    "-MaxNoProgressPerTask", $MaxNoProgressPerTask,
    "-LockStaleAfterMinutes", $LockStaleAfterMinutes,
    "-IterationTimeoutSeconds", $IterationTimeoutSeconds,
    "-IdleTimeoutSeconds", $IdleTimeoutSeconds,
    "-CodexTimeoutSeconds", $CodexTimeoutSeconds,
    "-CodexIdleTimeoutSeconds", $CodexIdleTimeoutSeconds,
    "-GateTimeoutSeconds", $GateTimeoutSeconds,
    "-GateIdleTimeoutSeconds", $GateIdleTimeoutSeconds,
    "-MaxWallClockMinutes", $MaxWallClockMinutes,
    "-MaxCodexRuns", $MaxCodexRuns,
    "-PromptProfile", $PromptProfile
)

if (-not [string]::IsNullOrWhiteSpace($TaskFile)) { $forward += @("-TaskFile", $TaskFile) }
if (-not [string]::IsNullOrWhiteSpace($StateFile)) { $forward += @("-StateFile", $StateFile) }
if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $forward += @("-ConfigFile", $ConfigFile) }
if (-not [string]::IsNullOrWhiteSpace($StartFromTaskId)) { $forward += @("-StartFromTaskId", $StartFromTaskId) }

if ($Mode -eq "refactor") {
    $effectiveSkillPath = Resolve-EffectiveSkillPath `
        -RuntimePath $resolvedRuntimeSkillPath `
        -OverridePath $resolvedOverrideSkillPath `
        -PreferOverride ([bool]$PreferOverrideSkill)
    Write-Host "SKILL_PATH: $effectiveSkillPath"
    $forward += @("-SkillPath", $effectiveSkillPath)
}

if ($DisableGatePreflight.IsPresent) { $forward += "-DisableGatePreflight" }
if ($SkipManualValidation.IsPresent) { $forward += "-SkipManualValidation" }
if ($ForceReleaseWithoutManual.IsPresent) { $forward += "-ForceReleaseWithoutManual" }
if ($SkipReleaseValidation.IsPresent) { $forward += "-SkipReleaseValidation" }
if ($SkipManualGates.IsPresent) { $forward += "-SkipManualGates" }
if ($SkipAutoCommit.IsPresent) { $forward += "-SkipAutoCommit" }
if ($NoRollback.IsPresent) { $forward += "-NoRollback" }
if ($AllowDirtyWorkingTree.IsPresent) { $forward += "-AllowDirtyWorkingTree" }
if ($DryRun.IsPresent) { $forward += "-DryRun" }

& powershell -ExecutionPolicy Bypass -File $entryScript @forward
exit $LASTEXITCODE
