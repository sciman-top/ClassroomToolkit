param(
    [string]$SummaryPath = "",
    [string]$OutputDirectory = "",
    [switch]$IncludeClassifierSuggestions,
    [switch]$IncludeOverridesFragment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        if (Test-Path -LiteralPath $Path) {
            return (Resolve-Path -LiteralPath $Path).Path
        }
        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Resolve-LatestChecklistSummaryPath {
    param([string]$RepoRoot)

    $summaryDir = Join-Path $RepoRoot ".codex/logs/checklist-loop"
    if (-not (Test-Path -LiteralPath $summaryDir)) {
        throw "Summary directory not found: $summaryDir"
    }

    $latest = Get-ChildItem -LiteralPath $summaryDir -File -Filter "run-*.summary.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace([string]$latest)) {
        throw "No checklist summary found in: $summaryDir"
    }

    return $latest
}

function Add-UniqueLine {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Line
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return
    }

    if ($List.Contains($Line)) {
        return
    }

    $List.Add($Line)
}

function Get-CompatibilitySignalLines {
    param(
        [object]$Summary,
        [string]$LogDirectory
    )

    $signals = New-Object 'System.Collections.Generic.List[string]'
    $pattern = "(?i)兼容|compat|presentation|ppt|wps|office|startup|classifier|override|rpc_e_call_rejected"

    foreach ($task in @($Summary.tasks)) {
        foreach ($gate in @($task.gates)) {
            $candidates = @()
            if ($null -ne $gate.PSObject.Properties["stdout_path"] -and -not [string]::IsNullOrWhiteSpace([string]$gate.stdout_path)) {
                $candidates += [string]$gate.stdout_path
            }
            if ($null -ne $gate.PSObject.Properties["stderr_path"] -and -not [string]::IsNullOrWhiteSpace([string]$gate.stderr_path)) {
                $candidates += [string]$gate.stderr_path
            }

            foreach ($path in $candidates) {
                $resolved = Resolve-AbsolutePath -Path $path -BasePath $LogDirectory
                if (-not (Test-Path -LiteralPath $resolved)) {
                    continue
                }

                $lineNumber = 0
                foreach ($line in (Get-Content -LiteralPath $resolved)) {
                    $lineNumber++
                    if ($line -match $pattern) {
                        Add-UniqueLine -List $signals -Line ("{0}:{1}: {2}" -f (Split-Path -Leaf $resolved), $lineNumber, $line.Trim())
                    }

                    if ($signals.Count -ge 50) {
                        break
                    }
                }
            }
        }
    }

    return $signals
}

$repoRoot = (Resolve-Path -LiteralPath ".").Path
$effectiveSummaryPath = if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    Resolve-LatestChecklistSummaryPath -RepoRoot $repoRoot
}
else {
    Resolve-AbsolutePath -Path $SummaryPath -BasePath $repoRoot
}

if (-not (Test-Path -LiteralPath $effectiveSummaryPath)) {
    throw "Summary file not found: $effectiveSummaryPath"
}

$summary = Get-Content -LiteralPath $effectiveSummaryPath -Raw | ConvertFrom-Json
$logDirectory = if ($null -ne $summary.PSObject.Properties["log_directory"] -and -not [string]::IsNullOrWhiteSpace([string]$summary.log_directory)) {
    [string]$summary.log_directory
}
else {
    Split-Path -Parent $effectiveSummaryPath
}

$effectiveOutputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $logDirectory
}
else {
    Resolve-AbsolutePath -Path $OutputDirectory -BasePath $repoRoot
}
if (-not (Test-Path -LiteralPath $effectiveOutputDirectory)) {
    New-Item -ItemType Directory -Path $effectiveOutputDirectory -Force | Out-Null
}

$signalLines = @(Get-CompatibilitySignalLines -Summary $summary -LogDirectory $logDirectory)

$tasks = @($summary.tasks)
$totalTasks = $tasks.Count
$completedTasks = @($tasks | Where-Object { [string]$_.status -eq "completed" }).Count
$failedTasks = @($tasks | Where-Object { [string]$_.status -eq "failed" }).Count
$skippedTasks = @($tasks | Where-Object { [string]$_.status -like "skipped*" }).Count

$gateTotal = 0
$gatePassed = 0
$gateFailed = 0
foreach ($task in $tasks) {
    $gates = @($task.gates)
    $gateTotal += $gates.Count
    $gatePassed += @($gates | Where-Object { [string]$_.status -eq "passed" }).Count
    $gateFailed += @($gates | Where-Object { [string]$_.status -eq "failed" }).Count
}

$suggestionsPath = $null
$suggestions = $null
$overridesFragmentPath = $null
if ($IncludeOverridesFragment.IsPresent) {
    $IncludeClassifierSuggestions = $true
}

if ($IncludeClassifierSuggestions.IsPresent) {
    $suggestionScript = Join-Path $PSScriptRoot "suggest-presentation-classifier-overrides.ps1"
    if (Test-Path -LiteralPath $suggestionScript) {
        $suggestionsPath = Join-Path $effectiveOutputDirectory ("run-{0}.classifier-suggestions.json" -f [string]$summary.run_id)
        & powershell -ExecutionPolicy Bypass -File $suggestionScript `
            -SummaryPath $effectiveSummaryPath `
            -LogDirectory $logDirectory `
            -OutputPath $suggestionsPath | Out-Null
        if (Test-Path -LiteralPath $suggestionsPath) {
            $suggestions = Get-Content -LiteralPath $suggestionsPath -Raw | ConvertFrom-Json
        }
    }
}

if ($IncludeOverridesFragment.IsPresent -and -not [string]::IsNullOrWhiteSpace($suggestionsPath)) {
    $fragmentScript = Join-Path $PSScriptRoot "build-classifier-overrides-fragment.ps1"
    if (Test-Path -LiteralPath $fragmentScript) {
        $overridesFragmentPath = Join-Path $effectiveOutputDirectory ("run-{0}.classifier-overrides.fragment.json" -f [string]$summary.run_id)
        & powershell -ExecutionPolicy Bypass -File $fragmentScript `
            -SuggestionsPath $suggestionsPath `
            -OutputPath $overridesFragmentPath | Out-Null
    }
}

$report = [ordered]@{
    generated_at_utc = [DateTime]::UtcNow.ToString("o")
    run_id = [string]$summary.run_id
    status = [string]$summary.status
    error_class = [string]$summary.error_class
    error_message = [string]$summary.error_message
    failed_task_id = [string]$summary.failed_task_id
    failed_gate_command = [string]$summary.failed_gate_command
    failed_timeout_reason = [string]$summary.failed_timeout_reason
    started_at = [string]$summary.started_at
    finished_at = [string]$summary.finished_at
    elapsed_seconds = [double]$summary.elapsed_seconds
    codex_runs_used = [int]$summary.codex_runs_used
    summary_path = $effectiveSummaryPath
    log_directory = $logDirectory
    task_counts = [ordered]@{
        total = $totalTasks
        completed = $completedTasks
        failed = $failedTasks
        skipped = $skippedTasks
    }
    gate_counts = [ordered]@{
        total = $gateTotal
        passed = $gatePassed
        failed = $gateFailed
    }
    compatibility_signals = @($signalLines)
    classifier_suggestions_path = $suggestionsPath
    classifier_overrides_fragment_path = $overridesFragmentPath
}

if ($null -ne $suggestions) {
    $report.classifier_token_suggestions = [ordered]@{
        additionalOfficeProcessTokens = @($suggestions.token_suggestions.additionalOfficeProcessTokens)
        additionalWpsProcessTokens = @($suggestions.token_suggestions.additionalWpsProcessTokens)
        additionalOfficeClassTokens = @($suggestions.token_suggestions.additionalOfficeClassTokens)
        additionalWpsClassTokens = @($suggestions.token_suggestions.additionalWpsClassTokens)
        additionalSlideshowClassTokens = @($suggestions.token_suggestions.additionalSlideshowClassTokens)
        unknownProcessTokens = @($suggestions.token_suggestions.unknownProcessTokens)
    }
}

$jsonPath = Join-Path $effectiveOutputDirectory ("run-{0}.compatibility-summary.json" -f [string]$summary.run_id)
$mdPath = Join-Path $effectiveOutputDirectory ("run-{0}.compatibility-summary.md" -f [string]$summary.run_id)

$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Compatibility Summary")
$lines.Add("")
$lines.Add("- RunId: $([string]$summary.run_id)")
$lines.Add("- Status: $([string]$summary.status)")
$lines.Add("- ErrorClass: $([string]$summary.error_class)")
$lines.Add("- FailedTask: $([string]$summary.failed_task_id)")
$lines.Add("- FailedGate: $([string]$summary.failed_gate_command)")
$lines.Add("- ElapsedSeconds: $([string]$summary.elapsed_seconds)")
$lines.Add("- CodexRunsUsed: $([string]$summary.codex_runs_used)")
$lines.Add("- SummaryPath: $effectiveSummaryPath")
$lines.Add("- LogDirectory: $logDirectory")
$lines.Add("")
$lines.Add("## Task/Gate Counts")
$lines.Add("")
$lines.Add("| Metric | Value |")
$lines.Add("| --- | ---: |")
$lines.Add("| tasks.total | $totalTasks |")
$lines.Add("| tasks.completed | $completedTasks |")
$lines.Add("| tasks.failed | $failedTasks |")
$lines.Add("| tasks.skipped | $skippedTasks |")
$lines.Add("| gates.total | $gateTotal |")
$lines.Add("| gates.passed | $gatePassed |")
$lines.Add("| gates.failed | $gateFailed |")
$lines.Add("")
$lines.Add("## Compatibility Signals")
$lines.Add("")
if (@($signalLines).Count -eq 0) {
    $lines.Add("- none")
}
else {
    foreach ($signal in $signalLines) {
        $lines.Add("- $signal")
    }
}

if ($null -ne $suggestions) {
    $lines.Add("")
    $lines.Add("## Suggested Classifier Overrides")
    $lines.Add("")
    $lines.Add("- additionalOfficeProcessTokens: $((@($suggestions.token_suggestions.additionalOfficeProcessTokens) -join ", "))")
    $lines.Add("- additionalWpsProcessTokens: $((@($suggestions.token_suggestions.additionalWpsProcessTokens) -join ", "))")
    $lines.Add("- additionalOfficeClassTokens: $((@($suggestions.token_suggestions.additionalOfficeClassTokens) -join ", "))")
    $lines.Add("- additionalWpsClassTokens: $((@($suggestions.token_suggestions.additionalWpsClassTokens) -join ", "))")
    $lines.Add("- additionalSlideshowClassTokens: $((@($suggestions.token_suggestions.additionalSlideshowClassTokens) -join ", "))")
    $lines.Add("- unknownProcessTokens: $((@($suggestions.token_suggestions.unknownProcessTokens) -join ", "))")
    $lines.Add("- suggestionsPath: $suggestionsPath")
    if (-not [string]::IsNullOrWhiteSpace($overridesFragmentPath)) {
        $lines.Add("- overridesFragmentPath: $overridesFragmentPath")
    }
}

$lines | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "COMPATIBILITY_SUMMARY_JSON: $jsonPath"
Write-Host "COMPATIBILITY_SUMMARY_MD: $mdPath"
if ($null -ne $suggestionsPath) {
    Write-Host "COMPATIBILITY_SUGGESTIONS: $suggestionsPath"
}
if (-not [string]::IsNullOrWhiteSpace($overridesFragmentPath)) {
    Write-Host "COMPATIBILITY_OVERRIDES_FRAGMENT: $overridesFragmentPath"
}
