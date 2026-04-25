param(
    [string]$Configuration = "Debug",
    [string]$TestProject = ".\tests\ClassroomToolkit.Tests\ClassroomToolkit.Tests.csproj",
    [string]$OutputRoot = ".\logs\brush-telemetry-report",
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Invoke-DotnetWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int]$MaxAttempts = 3,
        [int]$RetryDelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        & dotnet @Arguments
        if ($LASTEXITCODE -eq 0) {
            return
        }

        if ($attempt -ge $MaxAttempts) {
            throw "dotnet $($Arguments -join ' ') failed after $MaxAttempts attempts (exit=$LASTEXITCODE)."
        }

        Write-Host "dotnet $($Arguments -join ' ') 失败，$RetryDelaySeconds 秒后重试 ($attempt/$MaxAttempts)..." -ForegroundColor Yellow
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runDirectory = Join-Path $OutputRoot $timestamp
New-Item -Path $runDirectory -ItemType Directory -Force | Out-Null
$runDirectory = [System.IO.Path]::GetFullPath($runDirectory)

$telemetryJsonl = Join-Path $runDirectory "telemetry-snapshots.jsonl"
$logPath = Join-Path $runDirectory "telemetry-test.log"
$reportPath = Join-Path $runDirectory "telemetry-report.md"

Write-Host "==> Brush telemetry report collection started" -ForegroundColor Green
Write-Host "    Output directory: $runDirectory" -ForegroundColor DarkGray

if (-not $SkipRestore) {
    Write-Host "==> Restoring packages" -ForegroundColor Cyan
    Invoke-DotnetWithRetry -Arguments @("restore")
}

if (-not $SkipBuild) {
    Write-Host "==> Building test project" -ForegroundColor Cyan
    Invoke-DotnetWithRetry -Arguments @("build", $TestProject, "-c", $Configuration, "-m:1")
}

Write-Host "==> Running telemetry snapshot tests" -ForegroundColor Cyan
$env:CTOOLKIT_TELEMETRY_OUTPUT = $telemetryJsonl
try {
    $null = & dotnet test $TestProject -c $Configuration --no-build -m:1 --filter "FullyQualifiedName~BrushTelemetrySnapshotTests" *>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item Env:CTOOLKIT_TELEMETRY_OUTPUT -ErrorAction SilentlyContinue
}

if (-not (Test-Path $telemetryJsonl)) {
    throw "Telemetry output not found: $telemetryJsonl"
}

$rows = @()
foreach ($line in Get-Content -Path $telemetryJsonl) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }
    $rows += ($line | ConvertFrom-Json)
}

if ($rows.Count -eq 0) {
    throw "Telemetry output is empty: $telemetryJsonl"
}

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Brush Telemetry Report")
$report.Add("")
$report.Add("- Time: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")")
$report.Add("- Configuration: $Configuration")
$report.Add("- Test Project: $TestProject")
$report.Add("")
$report.Add("## Snapshot Table")
$report.Add("")
$report.Add("| Preset | Mode | dt p95(ms) | alloc p95(bytes) | raw p95 | resampled p95 | taper avg/p95/min/max (DIP) |")
$report.Add("| --- | --- | ---: | ---: | ---: | ---: | --- |")

foreach ($row in $rows | Sort-Object mode, preset) {
    $taperCell = ("{0:N2}/{1:N2}/{2:N2}/{3:N2}" -f $row.taper_base_avg_dip, $row.taper_base_p95_dip, $row.taper_base_min_dip, $row.taper_base_max_dip)
    $dtP95 = ([double]$row.dt_p95_ms).ToString("N3")
    $allocP95 = ([double]$row.alloc_p95_bytes).ToString("N0")
    $rawP95 = ([double]$row.raw_p95_points).ToString("N1")
    $resampledP95 = ([double]$row.resampled_p95_points).ToString("N1")
    $report.Add("| $($row.preset) | $($row.mode) | $dtP95 | $allocP95 | $rawP95 | $resampledP95 | $taperCell |")
}

$report.Add("")
$report.Add("## Artifacts")
$report.Add("")
$report.Add("- Telemetry JSONL: $(Split-Path -Leaf $telemetryJsonl)")
$report.Add("- Test Log: $(Split-Path -Leaf $logPath)")

Set-Content -Path $reportPath -Value $report -Encoding utf8
Write-Host "==> Telemetry report generated: $reportPath" -ForegroundColor Green
Write-Host "==> Brush telemetry report collection completed" -ForegroundColor Green
