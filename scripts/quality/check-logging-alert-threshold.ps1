[CmdletBinding()]
param(
    [string]$LogRoot = "logs",
    [int]$DroppedLogThreshold = 0,
    [int]$WindowHours = 24
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($DroppedLogThreshold -lt 0) {
    throw "[logging-alert-threshold] DroppedLogThreshold must be >= 0."
}

if ($WindowHours -le 0) {
    throw "[logging-alert-threshold] WindowHours must be > 0."
}

if (-not (Test-Path -LiteralPath $LogRoot)) {
    Write-Host ("[logging-alert-threshold] PASS log root not found, skip scan: {0}" -f $LogRoot)
    exit 0
}

$windowStart = [DateTime]::Now.AddHours(-$WindowHours)
$pattern = [regex]'^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\].*dropped-log-messages=(?<count>\d+)'
$records = New-Object System.Collections.Generic.List[object]

$files = Get-ChildItem -Path $LogRoot -File -Filter "app_*.log" -ErrorAction SilentlyContinue
foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        $match = $pattern.Match($line)
        if (-not $match.Success) {
            continue
        }

        try {
            $timestamp = [DateTime]::ParseExact(
                $match.Groups["ts"].Value,
                "yyyy-MM-dd HH:mm:ss.fff",
                [System.Globalization.CultureInfo]::InvariantCulture)
        }
        catch {
            continue
        }

        if ($timestamp -lt $windowStart) {
            continue
        }

        $count = [int]$match.Groups["count"].Value
        $records.Add([pscustomobject]@{
                timestamp = $timestamp
                dropped = $count
                file = $file.Name
                line = $line
            }) | Out-Null
    }
}

if ($records.Count -eq 0) {
    Write-Host "[logging-alert-threshold] PASS no dropped-log alert entries in the scan window."
    exit 0
}

$maxDropped = ($records | Measure-Object -Property dropped -Maximum).Maximum
if ($maxDropped -le $DroppedLogThreshold) {
    Write-Host ("[logging-alert-threshold] PASS max dropped-log count {0} <= threshold {1}." -f $maxDropped, $DroppedLogThreshold)
    exit 0
}

Write-Host ("[logging-alert-threshold] FAIL dropped-log threshold exceeded: max={0}, threshold={1}" -f $maxDropped, $DroppedLogThreshold)
foreach ($record in ($records | Sort-Object dropped -Descending | Select-Object -First 10)) {
    Write-Host ("  - ts={0:yyyy-MM-dd HH:mm:ss.fff} dropped={1} file={2}" -f $record.timestamp, $record.dropped, $record.file)
}

exit 3
