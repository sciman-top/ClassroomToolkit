[CmdletBinding()]
param(
    [string]$LogRoot = "logs",
    [int]$WindowHours = 24,
    [string]$OutputRoot = "artifacts/validation"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-Percentile {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][double[]]$Values,
        [Parameter(Mandatory = $true)][double]$Percentile
    )

    if ($Values.Count -eq 0) {
        return $null
    }

    $sorted = $Values | Sort-Object
    $position = [Math]::Ceiling($Percentile * $sorted.Count) - 1
    if ($position -lt 0) {
        $position = 0
    }
    if ($position -ge $sorted.Count) {
        $position = $sorted.Count - 1
    }

    return [double]$sorted[$position]
}

function Resolve-StatSummary {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][double[]]$Values)

    if ($Values.Count -eq 0) {
        return [pscustomobject]@{
            count = 0
            p50_ms = $null
            p95_ms = $null
            max_ms = $null
            avg_ms = $null
        }
    }

    $avg = ($Values | Measure-Object -Average).Average
    $max = ($Values | Measure-Object -Maximum).Maximum
    return [pscustomobject]@{
        count = $Values.Count
        p50_ms = [Math]::Round((Resolve-Percentile -Values $Values -Percentile 0.50), 2)
        p95_ms = [Math]::Round((Resolve-Percentile -Values $Values -Percentile 0.95), 2)
        max_ms = [Math]::Round([double]$max, 2)
        avg_ms = [Math]::Round([double]$avg, 2)
    }
}

$now = Get-Date
$windowStart = $now.AddHours(-$WindowHours)
New-Item -Path $OutputRoot -ItemType Directory -Force | Out-Null

$photoDecodeSamples = New-Object System.Collections.Generic.List[double]
$photoDispatchSamples = New-Object System.Collections.Generic.List[double]
$inkRedrawP95Samples = New-Object System.Collections.Generic.List[double]
$droppedLogSamples = New-Object System.Collections.Generic.List[int]

$photoLogPath = Join-Path $LogRoot "photo-overlay-latest.log"
if (Test-Path -LiteralPath $photoLogPath) {
    $photoLines = Get-Content -LiteralPath $photoLogPath -ErrorAction SilentlyContinue
    foreach ($line in $photoLines) {
        if ($line -match '\[(?<event>[^\]]+)\]\s+(?<clock>\d{2}:\d{2}:\d{2}\.\d{3})') {
            $timeStamp = [DateTime]::MinValue
            try {
                $timeStamp = [DateTime]::ParseExact(
                    $matches.clock,
                    "HH:mm:ss.fff",
                    [System.Globalization.CultureInfo]::InvariantCulture)
            }
            catch {
                $timeStamp = [DateTime]::MinValue
            }

            if ($timeStamp -ne [DateTime]::MinValue) {
                $dayMatched = Get-Date -Hour $timeStamp.Hour -Minute $timeStamp.Minute -Second $timeStamp.Second -Millisecond $timeStamp.Millisecond
                if ($dayMatched -lt $windowStart) {
                    continue
                }
            }
        }

        if ($line -match 'load-decoded.*elapsedMs=(?<elapsed>\d+(\.\d+)?)') {
            $photoDecodeSamples.Add([double]$matches.elapsed) | Out-Null
        }

        if ($line -match 'apply-dispatch.*queueMs=(?<queue>\d+(\.\d+)?)') {
            $photoDispatchSamples.Add([double]$matches.queue) | Out-Null
        }
    }
}

$appLogs = Get-ChildItem -Path $LogRoot -File -Filter "app_*.log" -ErrorAction SilentlyContinue
foreach ($log in $appLogs) {
    $lines = Get-Content -LiteralPath $log.FullName -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        if ($line -match '\[InkRedrawTelemetry\].*all\(p50/p95\)=(?<p50>\d+(\.\d+)?)/(?<p95>\d+(\.\d+)?)ms') {
            $inkRedrawP95Samples.Add([double]$matches.p95) | Out-Null
        }

        if ($line -match 'dropped-log-messages=(?<dropped>\d+)') {
            $droppedLogSamples.Add([int]$matches.dropped) | Out-Null
        }
    }
}

$decodeStats = Resolve-StatSummary -Values $photoDecodeSamples.ToArray()
$dispatchStats = Resolve-StatSummary -Values $photoDispatchSamples.ToArray()
$inkStats = Resolve-StatSummary -Values $inkRedrawP95Samples.ToArray()
$droppedStats = [pscustomobject]@{
    count = $droppedLogSamples.Count
    max = if ($droppedLogSamples.Count -eq 0) { $null } else { ($droppedLogSamples | Measure-Object -Maximum).Maximum }
}

$summary = [ordered]@{
    generated_at = $now.ToString("o")
    window_hours = $WindowHours
    log_root = $LogRoot
    photo_overlay_decode = $decodeStats
    photo_overlay_dispatch_queue = $dispatchStats
    ink_redraw_p95 = $inkStats
    dropped_log_alert = $droppedStats
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path $OutputRoot "ui-performance-samples-$stamp.json"
$mdPath = Join-Path $OutputRoot "ui-performance-samples-$stamp.md"

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8

$lines = @(
    "# UI Performance Sampling",
    "",
    "- GeneratedAt: $($now.ToString('yyyy-MM-dd HH:mm:ss'))",
    "- WindowHours: $WindowHours",
    "- LogRoot: $LogRoot",
    "",
    "## PhotoOverlay Decode",
    "- Count: $($decodeStats.count)",
    "- P50(ms): $($decodeStats.p50_ms)",
    "- P95(ms): $($decodeStats.p95_ms)",
    "- Max(ms): $($decodeStats.max_ms)",
    "- Avg(ms): $($decodeStats.avg_ms)",
    "",
    "## PhotoOverlay Dispatch Queue",
    "- Count: $($dispatchStats.count)",
    "- P50(ms): $($dispatchStats.p50_ms)",
    "- P95(ms): $($dispatchStats.p95_ms)",
    "- Max(ms): $($dispatchStats.max_ms)",
    "- Avg(ms): $($dispatchStats.avg_ms)",
    "",
    "## Ink Redraw P95 Samples",
    "- Count: $($inkStats.count)",
    "- P50(ms): $($inkStats.p50_ms)",
    "- P95(ms): $($inkStats.p95_ms)",
    "- Max(ms): $($inkStats.max_ms)",
    "- Avg(ms): $($inkStats.avg_ms)",
    "",
    "## Dropped Log Alert",
    "- Count: $($droppedStats.count)",
    "- MaxDropped: $($droppedStats.max)",
    "",
    "## Artifacts",
    "- JSON: $jsonPath"
)

$lines | Set-Content -Path $mdPath -Encoding UTF8

Write-Host ("[ui-performance-sampling] JSON: {0}" -f $jsonPath)
Write-Host ("[ui-performance-sampling] Markdown: {0}" -f $mdPath)
