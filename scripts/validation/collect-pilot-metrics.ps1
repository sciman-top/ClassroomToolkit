param(
    [string]$LogRoot = "logs",
    [int]$WindowMinutes = 30
)

$ErrorActionPreference = "Stop"

$now = Get-Date
$windowStart = $now.AddMinutes(-$WindowMinutes)

# 1) Error count from app/error logs
$errorFiles = Get-ChildItem -Path $LogRoot -Recurse -File -Include "error_*.log","app_*.log" -ErrorAction SilentlyContinue
$errorEvents = @()
foreach ($f in $errorFiles) {
    $lines = Get-Content -Path $f.FullName -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        if ($line -match "^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]") {
            try {
                $ts = [datetime]::ParseExact($matches.ts, "yyyy-MM-dd HH:mm:ss.fff", $null)
                if ($ts -ge $windowStart) {
                    if ($line -match "Exception|error|fatal|崩溃|失败") {
                        $errorEvents += $line
                    }
                }
            } catch {}
        }
    }
}

# 2) GC counters (snapshot)
$dotnetProc = Get-Process | Where-Object { $_.ProcessName -like "ClassroomToolkit*" } | Select-Object -First 1
$gcInfo = $null
if ($dotnetProc) {
    try {
        $gcInfo = Get-Counter "\ .NET CLR Memory($($dotnetProc.ProcessName))\% Time in GC" -ErrorAction SilentlyContinue
    } catch {}
}

# 3) Input latency placeholder from telemetry logs
$latencySamples = @()
$telemetryFiles = Get-ChildItem -Path $LogRoot -Recurse -File -Include "*.log" -ErrorAction SilentlyContinue
foreach ($f in $telemetryFiles) {
    $lines = Get-Content -Path $f.FullName -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        if ($line -match "PhotoInputTelemetry|BrushMoveTelemetry") {
            $latencySamples += $line
        }
    }
}

$reportDir = Join-Path $LogRoot "validation"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$report = Join-Path $reportDir "pilot-metrics-$stamp.md"
$errorSampleText = if ($errorEvents.Count -gt 0) { [string]::Join("`n", ($errorEvents | Select-Object -First 20)) } else { "(none)" }
$telemetrySampleText = if ($latencySamples.Count -gt 0) { [string]::Join("`n", ($latencySamples | Select-Object -First 20)) } else { "(none)" }
$gcValueText = if ($gcInfo -and $gcInfo.CounterSamples) { [string]::Join(', ', ($gcInfo.CounterSamples.CookedValue | ForEach-Object { '{0:N2}' -f $_ })) } else { "n/a" }

@"
# Pilot Metrics Snapshot

- GeneratedAt: $($now.ToString("yyyy-MM-dd HH:mm:ss"))
- WindowMinutes: $WindowMinutes
- ErrorEvents: $($errorEvents.Count)
- TelemetrySamples: $($latencySamples.Count)
- GCPercentTimeInGc: $gcValueText

## Error Samples (top 20)
$errorSampleText

## Telemetry Samples (top 20)
$telemetrySampleText
"@ | Set-Content -Path $report -Encoding UTF8

Write-Host "Metrics report generated: $report"
