param(
    [string]$Configuration = "Debug",
    [string]$TestProject = ".\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj",
    [string]$OutputRoot = ".\logs\brush-quality-baseline",
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-TrxCounters {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TrxPath
    )

    if (-not (Test-Path $TrxPath)) {
        return [pscustomobject]@{
            Total = 0
            Passed = 0
            Failed = 0
            Skipped = 0
        }
    }

    [xml]$trx = Get-Content -Path $TrxPath
    $counters = $trx.TestRun.ResultSummary.Counters

    return [pscustomobject]@{
        Total = [int]$counters.total
        Passed = [int]$counters.passed
        Failed = [int]$counters.failed
        Skipped = [int]$counters.notExecuted
    }
}

function Invoke-TestBatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Filter,
        [Parameter(Mandatory = $true)]
        [string]$RunDirectory,
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$BuildConfig
    )

    $trxName = "$Name.trx"
    $trxPath = Join-Path $RunDirectory $trxName
    $logPath = Join-Path $RunDirectory "$Name.log"

    Write-Host "==> Running test batch: $Name" -ForegroundColor Cyan
    Write-Host "    Filter: $Filter" -ForegroundColor DarkGray

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $null = & dotnet test $ProjectPath -c $BuildConfig --no-build --filter $Filter --logger "trx;LogFileName=$trxName" --results-directory $RunDirectory *>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
    $sw.Stop()

    $counters = Read-TrxCounters -TrxPath $trxPath
    return [pscustomobject]@{
        Name = $Name
        Filter = $Filter
        DurationMs = [int]$sw.ElapsedMilliseconds
        ExitCode = $exitCode
        Total = $counters.Total
        Passed = $counters.Passed
        Failed = $counters.Failed
        Skipped = $counters.Skipped
        TrxPath = $trxPath
        LogPath = $logPath
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runDirectory = Join-Path $OutputRoot $timestamp
New-Item -Path $runDirectory -ItemType Directory -Force | Out-Null

Write-Host "==> Brush quality baseline collection started" -ForegroundColor Green
Write-Host "    Output directory: $runDirectory" -ForegroundColor DarkGray

if (-not $SkipRestore) {
    Write-Host "==> Restoring packages" -ForegroundColor Cyan
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }
}

if (-not $SkipBuild) {
    Write-Host "==> Building test project" -ForegroundColor Cyan
    dotnet build $TestProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

$batches = @(
    @{
        Name = "quality-regression"
        Filter = "FullyQualifiedName~BrushQualityRegressionTests|FullyQualifiedName~InkStrokeQualityMetricsTests"
    },
    @{
        Name = "stylus-replay"
        Filter = "FullyQualifiedName~ClassroomWritingModeStylusReplayTests|FullyQualifiedName~ClassroomWritingModeRendererIntegrationTests|FullyQualifiedName~ClassroomWritingModeTunerTests"
    },
    @{
        Name = "performance-guard"
        Filter = "FullyQualifiedName~BrushPerformanceGuardTests|FullyQualifiedName~VariableWidthBrushCornerPreserveTests|FullyQualifiedName~VariableWidthBrushRendererRibbonTests"
    }
)

$results = @()
$hasFailures = $false

foreach ($batch in $batches) {
    $result = Invoke-TestBatch -Name $batch.Name -Filter $batch.Filter -RunDirectory $runDirectory -ProjectPath $TestProject -BuildConfig $Configuration
    $results += $result
    if ($result.ExitCode -ne 0 -or $result.Failed -gt 0) {
        $hasFailures = $true
    }
}

$totalTests = ($results | Measure-Object -Property Total -Sum).Sum
$totalPassed = ($results | Measure-Object -Property Passed -Sum).Sum
$totalFailed = ($results | Measure-Object -Property Failed -Sum).Sum
$totalSkipped = ($results | Measure-Object -Property Skipped -Sum).Sum
$totalDurationMs = ($results | Measure-Object -Property DurationMs -Sum).Sum

$reportPath = Join-Path $runDirectory "baseline-report.md"
$resultLabel = if ($hasFailures) { "FAILED" } else { "PASSED" }
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("# Brush Quality Baseline Report")
$reportLines.Add("")
$reportLines.Add(("- Time: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")))
$reportLines.Add("- Configuration: $Configuration")
$reportLines.Add("- Test Project: $TestProject")
$reportLines.Add("- Result: $resultLabel")
$reportLines.Add("")
$reportLines.Add("## Batch Results")
$reportLines.Add("")
$reportLines.Add("| Batch | Duration(ms) | Total | Passed | Failed | Skipped | Trx | Log |")
$reportLines.Add("| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |")

foreach ($result in $results) {
    $trxName = [System.IO.Path]::GetFileName($result.TrxPath)
    $logName = [System.IO.Path]::GetFileName($result.LogPath)
    $reportLines.Add("| $($result.Name) | $($result.DurationMs) | $($result.Total) | $($result.Passed) | $($result.Failed) | $($result.Skipped) | $trxName | $logName |")
}

$reportLines.Add("")
$reportLines.Add("## Summary")
$reportLines.Add("")
$reportLines.Add("- Total: $totalTests")
$reportLines.Add("- Passed: $totalPassed")
$reportLines.Add("- Failed: $totalFailed")
$reportLines.Add("- Skipped: $totalSkipped")
$reportLines.Add("- Duration(ms): $totalDurationMs")

Set-Content -Path $reportPath -Value $reportLines -Encoding utf8

Write-Host "==> Baseline report generated: $reportPath" -ForegroundColor Green

if ($hasFailures) {
    throw "Baseline collection has failed tests. See $reportPath."
}

Write-Host "==> Brush quality baseline collection completed" -ForegroundColor Green
