param(
  [string]$BudgetFile = "scripts/quality/hotspot-line-budgets.json",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
  param([string]$RelativePath)
  $normalized = $RelativePath -replace '/', '\'
  return Join-Path (Get-Location).Path $normalized
}

$budgetPath = Resolve-RepoPath -RelativePath $BudgetFile
if (!(Test-Path -LiteralPath $budgetPath -PathType Leaf)) {
  throw "Budget file not found: $budgetPath"
}

$entries = Get-Content -LiteralPath $budgetPath -Raw | ConvertFrom-Json
if ($null -eq $entries -or $entries.Count -eq 0) {
  throw "Budget file has no entries: $budgetPath"
}

$results = @()
$missing = @()
$violations = @()

foreach ($entry in $entries) {
  $relPath = [string]$entry.path
  $maxLines = [int]$entry.maxLines
  if ([string]::IsNullOrWhiteSpace($relPath) -or $maxLines -le 0) {
    throw "Invalid budget entry detected. path='$relPath' maxLines='$maxLines'"
  }

  $fullPath = Resolve-RepoPath -RelativePath $relPath
  if (!(Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    $missing += $relPath
    continue
  }

  $lineCount = (Get-Content -LiteralPath $fullPath | Measure-Object -Line).Lines
  $delta = $lineCount - $maxLines
  $ok = $lineCount -le $maxLines
  $row = [pscustomobject]@{
    path = $relPath
    maxLines = $maxLines
    actualLines = $lineCount
    delta = $delta
    ok = $ok
  }
  $results += $row
  if (-not $ok) {
    $violations += $row
  }
}

Write-Host "[hotspot] budgetFile=$BudgetFile entries=$($entries.Count)"

if ($results.Count -gt 0) {
  $results |
    Sort-Object -Property path |
    Format-Table -AutoSize path, maxLines, actualLines, delta, ok
}

if ($missing.Count -gt 0) {
  Write-Host "[hotspot][FAIL] missing_files=$($missing.Count)"
  foreach ($m in $missing) {
    Write-Host "  - $m"
  }
}

if ($violations.Count -gt 0) {
  Write-Host "[hotspot][FAIL] over_budget=$($violations.Count)"
  foreach ($v in $violations) {
    Write-Host ("  - {0}: actual={1} max={2} delta={3}" -f $v.path, $v.actualLines, $v.maxLines, $v.delta)
  }
}

$status = if ($missing.Count -eq 0 -and $violations.Count -eq 0) { "PASS" } else { "FAIL" }
Write-Host "[hotspot] status=$status"

if ($AsJson) {
  [pscustomobject]@{
    status = $status
    budgetFile = $BudgetFile
    checked = $results
    missingFiles = $missing
    overBudget = $violations
  } | ConvertTo-Json -Depth 5
}

if ($status -ne "PASS") {
  exit 1
}

