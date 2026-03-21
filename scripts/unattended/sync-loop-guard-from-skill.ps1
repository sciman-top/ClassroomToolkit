param(
    [string]$RepoRoot = ".",
    [string]$SkillRoot = "",
    [string]$SkillConfigTemplatePath = "",
    [string]$OutputFile = ".codex/unattended-loop.guard.json",
    [switch]$PreferOverride,
    [string]$OverrideSkillRoot = "E:\\CODE\\skills-manager\\overrides\\autonomous-execution-loop"
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

    $combined = Join-Path $BasePath $Path
    if (Test-Path -LiteralPath $combined) {
        return (Resolve-Path -LiteralPath $combined).Path
    }
    return [System.IO.Path]::GetFullPath($combined)
}

function Resolve-SkillConfigTemplatePath {
    param(
        [string]$ExplicitTemplatePath,
        [string]$ExplicitSkillRoot,
        [string]$RepoPath,
        [bool]$UseOverrideFirst,
        [string]$OverrideRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitTemplatePath)) {
        $resolved = Resolve-AbsolutePath -Path $ExplicitTemplatePath -BasePath $RepoPath
        if (-not (Test-Path -LiteralPath $resolved)) {
            throw "Skill config template not found: $resolved"
        }
        return $resolved
    }

    $candidateRoots = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitSkillRoot)) {
        $candidateRoots.Add((Resolve-AbsolutePath -Path $ExplicitSkillRoot -BasePath $RepoPath))
    }

    $runtimeRoot = $null
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        $runtimeRoot = Join-Path $env:CODEX_HOME "skills/autonomous-execution-loop"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($HOME)) {
        $runtimeRoot = Join-Path $HOME ".codex/skills/autonomous-execution-loop"
    }

    if (-not [string]::IsNullOrWhiteSpace($runtimeRoot)) {
        $candidateRoots.Add($runtimeRoot)
    }

    if (-not [string]::IsNullOrWhiteSpace($OverrideRoot)) {
        $candidateRoots.Add($OverrideRoot)
    }

    $orderedRoots = if ($UseOverrideFirst) {
        @($candidateRoots | Sort-Object { if ($_ -eq $OverrideRoot) { 0 } else { 1 } })
    }
    else {
        @($candidateRoots)
    }

    foreach ($root in $orderedRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }
        $templatePath = Join-Path $root "templates/autonomous-execution.config.template.json"
        if (Test-Path -LiteralPath $templatePath) {
            return (Resolve-Path -LiteralPath $templatePath).Path
        }
    }

    throw "Unable to locate autonomous-execution config template from runtime/override skill roots."
}

$repoPath = Resolve-AbsolutePath -Path $RepoRoot
$templatePath = Resolve-SkillConfigTemplatePath `
    -ExplicitTemplatePath $SkillConfigTemplatePath `
    -ExplicitSkillRoot $SkillRoot `
    -RepoPath $repoPath `
    -UseOverrideFirst ([bool]$PreferOverride) `
    -OverrideRoot $OverrideSkillRoot

$template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
$guard = $null
if ($null -ne $template.PSObject.Properties["execution_guard"]) {
    $guard = $template.execution_guard
}
if ($null -eq $guard) {
    throw "execution_guard section is missing in template: $templatePath"
}

$maxRetries = 1
if ($null -ne $guard.PSObject.Properties["max_retries_per_task"]) {
    $maxRetries = [int]$guard.max_retries_per_task
}
if ($maxRetries -lt 1) {
    $maxRetries = 1
}

$stopOnMissingStatusLine = $true
if ($null -ne $guard.PSObject.Properties["stop_on_missing_status_line"]) {
    $stopOnMissingStatusLine = [bool]$guard.stop_on_missing_status_line
}

$stopOnRepeatedFailureSignature = $true
if ($null -ne $guard.PSObject.Properties["stop_on_repeated_failure_signature"]) {
    $stopOnRepeatedFailureSignature = [bool]$guard.stop_on_repeated_failure_signature
}

$profiles = [ordered]@{
    aggressive = [ordered]@{
        max_retries_per_task = 1
        stop_on_missing_status_line = $stopOnMissingStatusLine
        stop_on_repeated_failure_signature = $stopOnRepeatedFailureSignature
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
        max_retries_per_task = $maxRetries
        stop_on_missing_status_line = $stopOnMissingStatusLine
        stop_on_repeated_failure_signature = $stopOnRepeatedFailureSignature
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
        max_retries_per_task = [Math]::Max(2, $maxRetries)
        stop_on_missing_status_line = $stopOnMissingStatusLine
        stop_on_repeated_failure_signature = $stopOnRepeatedFailureSignature
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

$activeProfile = "balanced"
$activePreset = $profiles[$activeProfile]

$outputPath = Resolve-AbsolutePath -Path $OutputFile -BasePath $repoPath
$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$profile = [ordered]@{
    version = 1
    synced_at = [DateTime]::UtcNow.ToString("o")
    source_skill_config_template = $templatePath
    active_profile = $activeProfile
    profile_updated_at = [DateTime]::UtcNow.ToString("o")
    profiles = $profiles
    execution_guard = $activePreset
    checklist = [ordered]@{
        max_attempts_per_task = [int]$activePreset.max_retries_per_task
        max_codex_runs = [int]$activePreset.max_codex_runs
        max_wall_clock_minutes = [int]$activePreset.max_wall_clock_minutes
        codex_timeout_seconds = [int]$activePreset.codex_timeout_seconds
        codex_idle_timeout_seconds = [int]$activePreset.codex_idle_timeout_seconds
        gate_timeout_seconds = [int]$activePreset.gate_timeout_seconds
        gate_idle_timeout_seconds = [int]$activePreset.gate_idle_timeout_seconds
    }
    refactor = [ordered]@{
        max_task_retries = [int]$activePreset.max_retries_per_task
        max_exec_failures_per_task = [int]$activePreset.max_retries_per_task
        max_no_progress_per_task = [int]$activePreset.max_retries_per_task
        max_iterations = [int]$activePreset.max_iterations
        max_no_progress = [int]$activePreset.max_no_progress
        max_non_code_progress = [int]$activePreset.max_non_code_progress
        iteration_timeout_seconds = [int]$activePreset.iteration_timeout_seconds
        idle_timeout_seconds = [int]$activePreset.idle_timeout_seconds
        max_wall_clock_minutes = [int]$activePreset.max_wall_clock_minutes
        stop_on_missing_status_line = [bool]$activePreset.stop_on_missing_status_line
        stop_on_repeated_failure_signature = [bool]$activePreset.stop_on_repeated_failure_signature
    }
}

$profile | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $outputPath -Encoding UTF8
$profile
