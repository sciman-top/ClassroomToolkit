param(
    [string]$RepoRoot = ".",
    [string]$GuardProfileFile = ".codex/unattended-loop.guard.json",
    [ValidateSet("aggressive", "balanced", "conservative")]
    [string]$Profile = "balanced"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

$profilePresets = @{
    aggressive = [ordered]@{
        max_retries_per_task = 1
        stop_on_missing_status_line = $true
        stop_on_repeated_failure_signature = $true
        max_wall_clock_minutes = 60
        max_codex_runs = 12
        codex_timeout_seconds = 600
        codex_idle_timeout_seconds = 90
        gate_timeout_seconds = 300
        gate_idle_timeout_seconds = 60
        iteration_timeout_seconds = 540
        idle_timeout_seconds = 60
        max_iterations = 5
        max_no_progress = 1
        max_non_code_progress = 1
    }
    balanced = [ordered]@{
        max_retries_per_task = 1
        stop_on_missing_status_line = $true
        stop_on_repeated_failure_signature = $true
        max_wall_clock_minutes = 90
        max_codex_runs = 24
        codex_timeout_seconds = 900
        codex_idle_timeout_seconds = 120
        gate_timeout_seconds = 600
        gate_idle_timeout_seconds = 90
        iteration_timeout_seconds = 780
        idle_timeout_seconds = 90
        max_iterations = 8
        max_no_progress = 2
        max_non_code_progress = 1
    }
    conservative = [ordered]@{
        max_retries_per_task = 2
        stop_on_missing_status_line = $true
        stop_on_repeated_failure_signature = $true
        max_wall_clock_minutes = 150
        max_codex_runs = 40
        codex_timeout_seconds = 1500
        codex_idle_timeout_seconds = 240
        gate_timeout_seconds = 900
        gate_idle_timeout_seconds = 180
        iteration_timeout_seconds = 1320
        idle_timeout_seconds = 180
        max_iterations = 12
        max_no_progress = 3
        max_non_code_progress = 2
    }
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$guardPath = Resolve-InputPath -RootPath $repoPath -InputPath $GuardProfileFile
if (-not (Test-Path -LiteralPath $guardPath)) {
    throw "Guard profile not found: $guardPath"
}

$doc = Get-Content -LiteralPath $guardPath -Raw | ConvertFrom-Json
$preset = $profilePresets[$Profile]
if ($null -eq $preset) {
    throw "Unsupported profile: $Profile"
}

if ($null -eq $doc.PSObject.Properties["execution_guard"]) {
    $doc | Add-Member -NotePropertyName "execution_guard" -NotePropertyValue ([pscustomobject]@{})
}
if ($null -eq $doc.PSObject.Properties["checklist"]) {
    $doc | Add-Member -NotePropertyName "checklist" -NotePropertyValue ([pscustomobject]@{})
}
if ($null -eq $doc.PSObject.Properties["refactor"]) {
    $doc | Add-Member -NotePropertyName "refactor" -NotePropertyValue ([pscustomobject]@{})
}

$executionGuard = [ordered]@{}
foreach ($entry in $preset.GetEnumerator()) {
    $executionGuard[$entry.Key] = $entry.Value
}
$doc.execution_guard = [pscustomobject]$executionGuard

$doc.checklist.max_attempts_per_task = [int]$preset.max_retries_per_task
$doc.checklist.max_codex_runs = [int]$preset.max_codex_runs
$doc.checklist.max_wall_clock_minutes = [int]$preset.max_wall_clock_minutes
$doc.checklist.codex_timeout_seconds = [int]$preset.codex_timeout_seconds
$doc.checklist.codex_idle_timeout_seconds = [int]$preset.codex_idle_timeout_seconds
$doc.checklist.gate_timeout_seconds = [int]$preset.gate_timeout_seconds
$doc.checklist.gate_idle_timeout_seconds = [int]$preset.gate_idle_timeout_seconds

$doc.refactor.max_task_retries = [int]$preset.max_retries_per_task
$doc.refactor.max_exec_failures_per_task = [int]$preset.max_retries_per_task
$doc.refactor.max_no_progress_per_task = [int]$preset.max_retries_per_task
$doc.refactor.max_iterations = [int]$preset.max_iterations
$doc.refactor.max_no_progress = [int]$preset.max_no_progress
$doc.refactor.max_non_code_progress = [int]$preset.max_non_code_progress
$doc.refactor.iteration_timeout_seconds = [int]$preset.iteration_timeout_seconds
$doc.refactor.idle_timeout_seconds = [int]$preset.idle_timeout_seconds
$doc.refactor.max_wall_clock_minutes = [int]$preset.max_wall_clock_minutes
$doc.refactor.stop_on_missing_status_line = [bool]$preset.stop_on_missing_status_line
$doc.refactor.stop_on_repeated_failure_signature = [bool]$preset.stop_on_repeated_failure_signature

$doc | Add-Member -NotePropertyName "active_profile" -NotePropertyValue $Profile -Force
$doc | Add-Member -NotePropertyName "profile_updated_at" -NotePropertyValue ([DateTime]::UtcNow.ToString("o")) -Force
$doc | Add-Member -NotePropertyName "profiles" -NotePropertyValue ([pscustomobject]$profilePresets) -Force

$doc | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $guardPath -Encoding UTF8

[ordered]@{
    status = "ok"
    guard_profile = $guardPath
    active_profile = $Profile
    execution_guard = $doc.execution_guard
} | ConvertTo-Json -Depth 20
