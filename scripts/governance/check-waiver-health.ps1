param(
  [string]$WaiverDir = "docs/governance/waivers",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
  param([Parameter(Mandatory = $true)][string]$RelativePath)
  $normalized = $RelativePath -replace '/', '\'
  return Join-Path (Get-Location).Path $normalized
}

function Parse-KeyValueFile {
  param([Parameter(Mandatory = $true)][string]$Path)
  $map = @{}
  foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
    if ($line -match '^\s*([^#=\s][^=]*)\s*=\s*(.*)$') {
      $key = $Matches[1].Trim()
      $value = $Matches[2].Trim()
      $map[$key] = $value
    }
  }
  return $map
}

function Normalize-Status {
  param([string]$Status)
  return ([string]$Status).Trim().ToLowerInvariant()
}

$requiredFields = @("owner", "expires_at", "status", "recovery_plan", "evidence_link")
$recoveredStatuses = @("recovered", "closed", "resolved", "done", "completed")

$waiverRoot = Resolve-RepoPath -RelativePath $WaiverDir
if (!(Test-Path -LiteralPath $waiverRoot -PathType Container)) {
  throw "Waiver directory not found: $waiverRoot"
}

$files = @(Get-ChildItem -LiteralPath $waiverRoot -Filter *.md -File |
  Where-Object { $_.Name -ne "_template.md" } |
  Sort-Object -Property FullName)

$results = @()
$violations = @()

foreach ($file in $files) {
  $data = Parse-KeyValueFile -Path $file.FullName
  $missing = @()

  foreach ($field in $requiredFields) {
    if (-not $data.ContainsKey($field) -or [string]::IsNullOrWhiteSpace([string]$data[$field])) {
      $missing += $field
    }
  }

  $status = Normalize-Status -Status ([string]$data["status"])
  $expiresRaw = [string]$data["expires_at"]
  $expiresAt = $null
  $expiresParseOk = [datetime]::TryParse($expiresRaw, [ref]$expiresAt)
  $isRecovered = $recoveredStatuses -contains $status
  $isExpiredUnrecovered = $false
  if ($expiresParseOk -and -not $isRecovered) {
    $isExpiredUnrecovered = $expiresAt.Date -lt (Get-Date).Date
  }

  $issues = @()
  if ($missing.Count -gt 0) {
    $issues += ("missing_fields=" + ($missing -join ","))
  }
  if (-not [string]::IsNullOrWhiteSpace($expiresRaw) -and -not $expiresParseOk) {
    $issues += "invalid_expires_at"
  }
  if ($isExpiredUnrecovered) {
    $issues += "expired_unrecovered"
  }

  $row = [pscustomobject]@{
    file = $file.FullName
    status = $status
    expires_at = $expiresRaw
    recovered = $isRecovered
    expired_unrecovered = $isExpiredUnrecovered
    issues = $issues
    ok = $issues.Count -eq 0
  }
  $results += $row

  if (-not $row.ok) {
    $violations += $row
  }
}

$activeCount = @($results | Where-Object { -not $_.recovered }).Count
$expiredUnrecoveredCount = @($results | Where-Object { $_.expired_unrecovered }).Count
$status = if ($violations.Count -eq 0) { "PASS" } else { "FAIL" }

Write-Host "[waiver] dir=$WaiverDir files=$($files.Count) active=$activeCount expired_unrecovered=$expiredUnrecoveredCount status=$status"

if ($results.Count -gt 0) {
  $results | Select-Object file, status, expires_at, recovered, expired_unrecovered, ok | Format-Table -AutoSize | Out-Host
}

if ($violations.Count -gt 0) {
  Write-Host "[waiver][FAIL] violations=$($violations.Count)"
  foreach ($v in $violations) {
    $issueText = if ($v.issues.Count -gt 0) { $v.issues -join ";" } else { "unknown" }
    Write-Host ("  - {0}: {1}" -f $v.file, $issueText)
  }
}

if ($AsJson) {
  [pscustomobject]@{
    status = $status
    waiverDir = $WaiverDir
    totalFiles = $files.Count
    activeCount = $activeCount
    expiredUnrecoveredCount = $expiredUnrecoveredCount
    checked = $results
    violations = $violations
  } | ConvertTo-Json -Depth 6
}

if ($status -ne "PASS") {
  exit 1
}

exit 0
