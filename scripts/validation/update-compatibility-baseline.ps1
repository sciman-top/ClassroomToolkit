[CmdletBinding()]
param(
    [string]$BaselinePath = "docs/compatibility/matrix-baseline-2026Q2.md",
    [string]$ReportsIndexPath = "docs/compatibility/reports/index.md",
    [string]$ReportsDir = "docs/compatibility/reports"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $BaselinePath)) {
    throw "Baseline file not found: $BaselinePath"
}

if (-not (Test-Path $ReportsDir)) {
    throw "Reports directory not found: $ReportsDir"
}

$reportFiles = Get-ChildItem -Path $ReportsDir -File -Filter "*.md" |
    Where-Object { $_.Name -ne "index.md" }

function Parse-Report([System.IO.FileInfo]$file) {
    $text = Get-Content -Path $file.FullName -Raw

    $matrixId = ""
    $status = "Pending"

    $matrixMatch = [regex]::Match($text, "(?m)^Matrix ID:\s*(.+)$")
    if ($matrixMatch.Success) {
        $matrixId = $matrixMatch.Groups[1].Value.Trim()
    }

    $statusMatch = [regex]::Match($text, "(?m)^- Status:\s*(.+)$")
    if ($statusMatch.Success) {
        $status = $statusMatch.Groups[1].Value.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($matrixId)) {
        return $null
    }

    [pscustomobject]@{
        MatrixId = $matrixId
        Status = $status
        FileName = $file.Name
        LastWriteTime = $file.LastWriteTime
    }
}

$latestByMatrix = @{}
foreach ($file in $reportFiles) {
    $entry = Parse-Report $file
    if ($null -eq $entry) {
        continue
    }

    if (-not $latestByMatrix.ContainsKey($entry.MatrixId) -or $entry.LastWriteTime -gt $latestByMatrix[$entry.MatrixId].LastWriteTime) {
        $latestByMatrix[$entry.MatrixId] = $entry
    }
}

function Map-Status([string]$status) {
    switch -Regex ($status) {
        "^Pass$" { return "Verified" }
        "^Fail$" { return "Failed" }
        default { return "Pending Verify" }
    }
}

$lines = Get-Content -Path $BaselinePath
$updated = New-Object System.Collections.Generic.List[string]

foreach ($line in $lines) {
    if ($line -match '^\|\s*BL-') {
        $parts = $line.Split('|')
        if ($parts.Length -ge 11) {
            $id = $parts[1].Trim()
            if ($latestByMatrix.ContainsKey($id)) {
                $entry = $latestByMatrix[$id]
                $mapped = Map-Status $entry.Status
                $parts[9] = " $mapped "
                $parts[10] = " report:$($entry.FileName) "
                $line = ($parts -join '|')
            }
        }
    }

    $updated.Add($line)
}

Set-Content -Path $BaselinePath -Value ($updated -join "`n") -Encoding UTF8

if (Test-Path $ReportsIndexPath) {
    $indexContent = Get-Content -Path $ReportsIndexPath -Raw
    if ($indexContent -notmatch [regex]::Escape("$BaselinePath")) {
        $append = @"

## Baseline Link

- Baseline: [$BaselinePath]($BaselinePath)
"@
        Set-Content -Path $ReportsIndexPath -Value ($indexContent + $append) -Encoding UTF8
    }
}

Write-Host "[compat-baseline] updated: $BaselinePath"
