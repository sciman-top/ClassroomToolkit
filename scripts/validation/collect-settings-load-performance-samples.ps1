[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [int]$ColdIterations = 20,
    [int]$HotIterations = 200,
    [string]$OutputRoot = "artifacts/validation"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Invoke-Probe {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$SettingsPath,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    & dotnet run `
        --project "scripts/validation/SettingsLoadPerfProbe/SettingsLoadPerfProbe.csproj" `
        -c $Configuration `
        -- `
        --settings-path $SettingsPath `
        --output-json $OutputPath `
        --label $Label `
        --cold-iterations $ColdIterations `
        --hot-iterations $HotIterations

    if ($LASTEXITCODE -ne 0) {
        throw "[settings-load-performance-sampling] probe failed label=$Label exit=$LASTEXITCODE"
    }
}

function New-SampleSettingsFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$PayloadBytes
    )

    $payload = "a" * $PayloadBytes
    $json = @"
{
  "Paint": {
    "brush_base_size": "12",
    "brush_color": "#FF000000",
    "payload": "$payload"
  },
  "Launcher": {
    "x": "100",
    "y": "200"
  }
}
"@
    $json | Set-Content -Path $Path -Encoding UTF8
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Resolve-Path "." | ForEach-Object { Join-Path $_.Path $OutputRoot }
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$tempDir = Join-Path $env:TEMP ("ctk-settings-perf-{0}" -f ([guid]::NewGuid().ToString("N")))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $smallPath = Join-Path $tempDir "settings-small.json"
    $mediumPath = Join-Path $tempDir "settings-medium.json"
    New-SampleSettingsFile -Path $smallPath -PayloadBytes 4096
    New-SampleSettingsFile -Path $mediumPath -PayloadBytes 524288

    $smallOutput = Join-Path $outputDir ("settings-load-performance-small-{0}.json" -f $stamp)
    $mediumOutput = Join-Path $outputDir ("settings-load-performance-medium-{0}.json" -f $stamp)

    Invoke-Probe -Label "small" -SettingsPath $smallPath -OutputPath $smallOutput
    Invoke-Probe -Label "medium" -SettingsPath $mediumPath -OutputPath $mediumOutput

    $small = Get-Content -Path $smallOutput -Raw | ConvertFrom-Json
    $medium = Get-Content -Path $mediumOutput -Raw | ConvertFrom-Json

    $summary = [ordered]@{
        generated_at = (Get-Date).ToString("o")
        configuration = $Configuration
        cold_iterations = $ColdIterations
        hot_iterations = $HotIterations
        samples = @(
            $small,
            $medium
        )
    }

    $summaryJsonPath = Join-Path $outputDir ("settings-load-performance-summary-{0}.json" -f $stamp)
    $summaryMdPath = Join-Path $outputDir ("settings-load-performance-summary-{0}.md" -f $stamp)
    $summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryJsonPath -Encoding UTF8

    $mdLines = @(
        "# Settings Load Performance Sampling",
        "",
        "- GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "- Configuration: $Configuration",
        "- ColdIterations: $ColdIterations",
        "- HotIterations: $HotIterations",
        "",
        "## Small Sample",
        "- FileBytes: $($small.file_size_bytes)",
        "- Cold Avg(ms): $($small.cold.avg_ms)",
        "- Cold P95(ms): $($small.cold.p95_ms)",
        "- Hot Avg(ms): $($small.hot.avg_ms)",
        "- Hot P95(ms): $($small.hot.p95_ms)",
        "",
        "## Medium Sample",
        "- FileBytes: $($medium.file_size_bytes)",
        "- Cold Avg(ms): $($medium.cold.avg_ms)",
        "- Cold P95(ms): $($medium.cold.p95_ms)",
        "- Hot Avg(ms): $($medium.hot.avg_ms)",
        "- Hot P95(ms): $($medium.hot.p95_ms)",
        "",
        "## Artifacts",
        "- Small JSON: $smallOutput",
        "- Medium JSON: $mediumOutput",
        "- Summary JSON: $summaryJsonPath"
    )
    $mdLines | Set-Content -Path $summaryMdPath -Encoding UTF8

    Write-Host ("[settings-load-performance-sampling] Small: {0}" -f $smallOutput)
    Write-Host ("[settings-load-performance-sampling] Medium: {0}" -f $mediumOutput)
    Write-Host ("[settings-load-performance-sampling] SummaryJson: {0}" -f $summaryJsonPath)
    Write-Host ("[settings-load-performance-sampling] SummaryMarkdown: {0}" -f $summaryMdPath)
}
finally {
    if (Test-Path -LiteralPath $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
