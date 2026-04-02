[CmdletBinding()]
param(
    [string]$ReportsDir = "docs/compatibility/reports",
    [string]$OutputPath = "docs/compatibility/reports/index.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $ReportsDir)) {
    throw "Reports directory not found: $ReportsDir"
}

$reportFiles = Get-ChildItem -Path $ReportsDir -File -Filter "*.md" |
    Where-Object { $_.Name -ne "index.md" } |
    Sort-Object LastWriteTime -Descending

function Parse-Report([System.IO.FileInfo]$file) {
    $text = Get-Content -Path $file.FullName -Raw

    $matrixId = "Unknown"
    $status = "Pending"
    $date = "Unknown"

    $matrixMatch = [regex]::Match($text, "(?m)^Matrix ID:\s*(.+)$")
    if ($matrixMatch.Success) {
        $matrixId = $matrixMatch.Groups[1].Value.Trim()
    }

    $statusMatch = [regex]::Match($text, "(?m)^- Status:\s*(.+)$")
    if ($statusMatch.Success) {
        $status = $statusMatch.Groups[1].Value.Trim()
    }

    $dateMatch = [regex]::Match($text, "(?m)^Date:\s*(.+)$")
    if ($dateMatch.Success) {
        $date = $dateMatch.Groups[1].Value.Trim()
    }

    [pscustomobject]@{
        FileName = $file.Name
        RelativePath = "docs/compatibility/reports/$($file.Name)"
        MatrixId = $matrixId
        Status = $status
        Date = $date
        LastWriteTime = $file.LastWriteTime
    }
}

$entries = @($reportFiles | ForEach-Object { Parse-Report $_ })

$summaryByMatrix = $entries |
    Group-Object MatrixId |
    ForEach-Object {
        $_.Group | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    } |
    Sort-Object MatrixId

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$content = @()
$content += "# Compatibility Reports Index"
$content += ""
$content += "Generated at: $generatedAt"
$content += ""
$content += "## Latest By Matrix ID"
$content += ""
$content += "| Matrix ID | Latest Status | Report | Generated Date |"
$content += "|---|---|---|---|"

foreach ($item in $summaryByMatrix) {
    $reportLink = "[$($item.FileName)]($($item.RelativePath))"
    $content += "| $($item.MatrixId) | $($item.Status) | $reportLink | $($item.Date) |"
}

$content += ""
$content += "## All Reports"
$content += ""
$content += "| File | Matrix ID | Status | Generated Date |"
$content += "|---|---|---|---|"

foreach ($item in $entries) {
    $reportLink = "[$($item.FileName)]($($item.RelativePath))"
    $content += "| $reportLink | $($item.MatrixId) | $($item.Status) | $($item.Date) |"
}

$outputDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

Set-Content -Path $OutputPath -Value ($content -join "`n") -Encoding UTF8
Write-Host "[compat-index] generated: $OutputPath"
