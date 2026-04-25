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

function Resolve-PowerShellExecutable {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_ALLOW_WINDOWS_POWERSHELL)) {
        $legacy = Get-Command powershell -ErrorAction SilentlyContinue
        if ($legacy) { return [string]$legacy.Source }
    }

    $programFilesPwsh = if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        Join-Path $env:ProgramFiles "PowerShell\7\pwsh.exe"
    } else {
        $null
    }
    if (-not [string]::IsNullOrWhiteSpace($programFilesPwsh) -and (Test-Path -LiteralPath $programFilesPwsh)) {
        return $programFilesPwsh
    }

    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return [string]$pwsh.Source }

    $legacyFallback = Get-Command powershell -ErrorAction SilentlyContinue
    if ($legacyFallback) { return [string]$legacyFallback.Source }

    throw "PowerShell 7 (pwsh) is required unless CODEX_ALLOW_WINDOWS_POWERSHELL=1 is set."
}

$wrapperPath = Join-Path (Resolve-Path -LiteralPath $PSScriptRoot).Path "run-refactor-loop.ps1"
$forwardArgs = @(
    "-RepoRoot", $RepoRoot,
    "-Mode", $Mode,
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

& (Resolve-PowerShellExecutable) -NoProfile -ExecutionPolicy Bypass -File $wrapperPath @forwardArgs
