param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$repoName = Split-Path -Leaf $repoPath

function Test-CommandExists([string]$Name) {
  return $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

$hasOsvScanner = Test-CommandExists -Name "osv-scanner"
$hasSyft = Test-CommandExists -Name "syft"

$summary = [ordered]@{
  repo = $repoName
  repo_root = ($repoPath -replace "\\", "/")
  osv_scanner_available = $hasOsvScanner
  syft_available = $hasSyft
  sbom_generated = $false
  vulnerability_scan_executed = $false
  status = "ADVISORY"
  notes = @()
}

$sbomPath = Join-Path $repoPath "sbom.spdx.json"

if ($hasSyft) {
  try {
    & syft dir:$repoPath -o spdx-json=$sbomPath | Out-Null
    if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $sbomPath -PathType Leaf)) {
      $summary.sbom_generated = $true
    } else {
      $summary.notes += "syft executed but sbom output missing"
    }
  } catch {
    $summary.notes += ("syft execution failed: " + $_.Exception.Message)
  }
} else {
  $summary.notes += "syft not found; skip local sbom generation"
}

if ($hasOsvScanner) {
  try {
    & osv-scanner scan source --recursive $repoPath | Out-Null
    if ($LASTEXITCODE -eq 0) {
      $summary.vulnerability_scan_executed = $true
    } else {
      $summary.notes += ("osv-scanner returned non-zero exit code: " + [string]$LASTEXITCODE)
    }
  } catch {
    $summary.notes += ("osv-scanner execution failed: " + $_.Exception.Message)
  }
} else {
  $summary.notes += "osv-scanner not found; skip local vulnerability scan"
}

if ($summary.sbom_generated -or $summary.vulnerability_scan_executed) {
  $summary.status = "PASS"
}

if ($AsJson) {
  ([pscustomobject]$summary) | ConvertTo-Json -Depth 6 | Write-Output
  exit 0
}

Write-Host ("supply_chain.repo=" + $summary.repo)
Write-Host ("supply_chain.status=" + $summary.status)
Write-Host ("supply_chain.osv_scanner_available=" + $summary.osv_scanner_available)
Write-Host ("supply_chain.syft_available=" + $summary.syft_available)
Write-Host ("supply_chain.sbom_generated=" + $summary.sbom_generated)
Write-Host ("supply_chain.vulnerability_scan_executed=" + $summary.vulnerability_scan_executed)
foreach ($note in @($summary.notes)) {
  Write-Host ("supply_chain.note=" + [string]$note)
}
exit 0
