param(
    [string]$RepoRoot = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([string]$Root)
    return (Resolve-Path -LiteralPath $Root).Path
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$repoPath = Resolve-RepoPath -Root $RepoRoot
$summaryScript = Join-Path $repoPath "scripts/unattended/export-compatibility-summary.ps1"
$suggestScript = Join-Path $repoPath "scripts/unattended/suggest-presentation-classifier-overrides.ps1"
Assert-Condition -Condition (Test-Path -LiteralPath $summaryScript) -Message "Missing export script: $summaryScript"
Assert-Condition -Condition (Test-Path -LiteralPath $suggestScript) -Message "Missing suggestion script: $suggestScript"

$tempRoot = Join-Path $repoPath ".codex/tmp/compatibility-reporting-tests"
if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
}
$runDir = Join-Path $tempRoot (Get-Date -Format "yyyyMMdd-HHmmss-fff")
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$stdoutLog = Join-Path $runDir "gate-stdout.log"
$stderrLog = Join-Path $runDir "gate-stderr.log"
@'
启动探针[提示/presentation-privilege-unknown] process=powerpoint_gov class=GovSlideFrameClass
启动探针[提示/presentation-privilege-unknown] process=campus_wpp_edu_service class=EduWpsShowFrame
startup compatibility evidence override-token:pptgov_custom
'@ | Set-Content -LiteralPath $stdoutLog -Encoding UTF8
@'
Compatibility warning: classifier override-token:campus_wpp_edu_service
Presentation className=GovPptScreenClass processName=pptgov_custom
'@ | Set-Content -LiteralPath $stderrLog -Encoding UTF8

$summaryPath = Join-Path $runDir "run-test.summary.json"
$summary = [ordered]@{
    run_id = "test-compat-report"
    started_at = [DateTime]::UtcNow.AddMinutes(-1).ToString("o")
    finished_at = [DateTime]::UtcNow.ToString("o")
    status = "failed"
    error_class = "gate_failure"
    error_message = "mock failure"
    failed_task_id = "compat-task"
    failed_gate_command = "dotnet test"
    failed_timeout_reason = ""
    elapsed_seconds = 42.5
    codex_runs_used = 1
    log_directory = $runDir
    tasks = @(
        [ordered]@{
            id = "compat-task"
            status = "failed"
            gates = @(
                [ordered]@{
                    status = "failed"
                    stdout_path = $stdoutLog
                    stderr_path = $stderrLog
                }
            )
        }
    )
}
$summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

$suggestPath = Join-Path $runDir "classifier-suggestions.json"
& powershell -ExecutionPolicy Bypass -File $suggestScript `
    -SummaryPath $summaryPath `
    -LogDirectory $runDir `
    -OutputPath $suggestPath | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "suggest script failed with exit code $LASTEXITCODE"
}

Assert-Condition -Condition (Test-Path -LiteralPath $suggestPath) -Message "Suggestion output not found."
$suggestion = Get-Content -LiteralPath $suggestPath -Raw | ConvertFrom-Json
Assert-Condition -Condition (@($suggestion.token_suggestions.additionalOfficeProcessTokens) -contains "pptgov_custom") -Message "Expected office process token suggestion missing."
Assert-Condition -Condition (@($suggestion.token_suggestions.additionalWpsProcessTokens) -contains "campus_wpp_edu_service") -Message "Expected WPS process token suggestion missing."

$fragmentScript = Join-Path $repoPath "scripts/unattended/build-classifier-overrides-fragment.ps1"
Assert-Condition -Condition (Test-Path -LiteralPath $fragmentScript) -Message "Missing fragment script: $fragmentScript"
$settingsPath = Join-Path $runDir "settings.json"
@'
{
  "Paint": {
    "control_ms_ppt": "True"
  }
}
'@ | Set-Content -LiteralPath $settingsPath -Encoding UTF8
$directFragmentPath = Join-Path $runDir "direct.classifier-overrides.fragment.json"
& powershell -ExecutionPolicy Bypass -File $fragmentScript `
    -SuggestionsPath $suggestPath `
    -OutputPath $directFragmentPath `
    -SettingsPath $settingsPath `
    -ApplyToSettings | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "fragment build/apply script failed with exit code $LASTEXITCODE"
}
Assert-Condition -Condition (Test-Path -LiteralPath $directFragmentPath) -Message "Direct fragment output not found."
Assert-Condition -Condition (Test-Path -LiteralPath "$settingsPath.bak") -Message "Settings backup file not generated."
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
Assert-Condition -Condition ([string]$settings.Paint.presentation_classifier_overrides_json -like "*campus_wpp_edu_service*") -Message "settings.json did not receive overrides patch."

& powershell -ExecutionPolicy Bypass -File $summaryScript `
    -SummaryPath $summaryPath `
    -OutputDirectory $runDir `
    -IncludeClassifierSuggestions `
    -IncludeOverridesFragment | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "summary export script failed with exit code $LASTEXITCODE"
}

$reportJson = Join-Path $runDir "run-test-compat-report.compatibility-summary.json"
$reportMd = Join-Path $runDir "run-test-compat-report.compatibility-summary.md"
$fragmentPath = Join-Path $runDir "run-test-compat-report.classifier-overrides.fragment.json"
Assert-Condition -Condition (Test-Path -LiteralPath $reportJson) -Message "Compatibility summary json not found."
Assert-Condition -Condition (Test-Path -LiteralPath $reportMd) -Message "Compatibility summary markdown not found."
Assert-Condition -Condition (Test-Path -LiteralPath $fragmentPath) -Message "Overrides fragment file not found."

$report = Get-Content -LiteralPath $reportJson -Raw | ConvertFrom-Json
Assert-Condition -Condition ([string]$report.status -eq "failed") -Message "Unexpected report status."
Assert-Condition -Condition ([int]$report.task_counts.total -eq 1) -Message "Unexpected task count."
Assert-Condition -Condition (@($report.compatibility_signals).Count -gt 0) -Message "Expected compatibility signals not found."
Assert-Condition -Condition (@($report.classifier_token_suggestions.additionalOfficeProcessTokens) -contains "pptgov_custom") -Message "Expected embedded suggestion missing."
Assert-Condition -Condition ([string]$report.classifier_overrides_fragment_path -eq $fragmentPath) -Message "Fragment path not linked in summary report."

$fragment = Get-Content -LiteralPath $fragmentPath -Raw | ConvertFrom-Json
Assert-Condition -Condition ([string]$fragment.settings_patch.key -eq "presentation_classifier_overrides_json") -Message "Fragment settings key mismatch."
Assert-Condition -Condition ([string]$fragment.settings_patch.section -eq "Paint") -Message "Fragment settings section mismatch."
Assert-Condition -Condition ([string]$fragment.settings_patch.value -like "*pptgov_custom*") -Message "Fragment value missing expected token."

Write-Host "COMPATIBILITY_REPORTING: PASS" -ForegroundColor Green
Write-Host "report_json: $reportJson"
Write-Host "report_md: $reportMd"
