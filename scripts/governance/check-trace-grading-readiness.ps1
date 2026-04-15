param(
  [string]$RepoRoot = ".",
  [string]$PolicyRelativePath = ".governance/trace-grading-policy.json",
  [string]$EvidenceRelativePath = "docs/change-evidence",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath([string]$PathText) {
  $resolved = Resolve-Path -LiteralPath $PathText -ErrorAction Stop
  return ([System.IO.Path]::GetFullPath($resolved.Path) -replace '\\', '/').TrimEnd('/')
}

function Read-Json([string]$PathText, [object]$DefaultValue) {
  if (-not (Test-Path -LiteralPath $PathText -PathType Leaf)) { return $DefaultValue }
  try {
    return (Get-Content -LiteralPath $PathText -Raw -Encoding UTF8 | ConvertFrom-Json)
  } catch {
    return $DefaultValue
  }
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$policyPath = Join-Path ($repoPath -replace '/', '\') ($PolicyRelativePath -replace '/', '\')
$evidenceRoot = Join-Path ($repoPath -replace '/', '\') ($EvidenceRelativePath -replace '/', '\')

$result = [ordered]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("o")
  repo_root = $repoPath
  policy_path = ($policyPath -replace '\\', '/')
  evidence_root = ($evidenceRoot -replace '\\', '/')
  status = "unknown"
  window_days = 30
  minimum_sample_size = 20
  minimum_coverage_rate = 0.80
  fail_on_breach = $true
  scanned_file_count = 0
  sampled_file_count = 0
  required_fields = @()
  field_coverage = @{}
  overall_coverage_rate = 0
  missing_fields_by_file = @()
}

$policy = Read-Json -PathText $policyPath -DefaultValue $null
if ($null -eq $policy) {
  $result.status = "missing_policy"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "trace_grading.status=missing_policy" }
  exit 1
}

$enabled = $true
if ($null -ne $policy.PSObject.Properties['enabled']) { $enabled = [bool]$policy.enabled }
if (-not $enabled) {
  $result.status = "disabled"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "trace_grading.status=disabled" }
  exit 0
}

$windowDays = 30
if ($null -ne $policy.PSObject.Properties['window_days']) {
  try { $windowDays = [Math]::Max(1, [int]$policy.window_days) } catch { $windowDays = 30 }
}
$minSample = 20
if ($null -ne $policy.PSObject.Properties['minimum_sample_size']) {
  try { $minSample = [Math]::Max(1, [int]$policy.minimum_sample_size) } catch { $minSample = 20 }
}
$minCoverage = 0.80
if ($null -ne $policy.PSObject.Properties['minimum_coverage_rate']) {
  try { $minCoverage = [double]$policy.minimum_coverage_rate } catch { $minCoverage = 0.80 }
}
$failOnBreach = $true
if ($null -ne $policy.PSObject.Properties['fail_on_breach']) {
  $failOnBreach = [bool]$policy.fail_on_breach
}
$requiredFields = @("decision_score", "hard_guard_hits", "reason_codes")
if ($null -ne $policy.PSObject.Properties['required_fields']) {
  $candidate = @($policy.required_fields | ForEach-Object { ([string]$_).Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  if ($candidate.Count -gt 0) { $requiredFields = @($candidate) }
}

$result.window_days = [int]$windowDays
$result.minimum_sample_size = [int]$minSample
$result.minimum_coverage_rate = [Math]::Round([double]$minCoverage, 6)
$result.fail_on_breach = [bool]$failOnBreach
$result.required_fields = @($requiredFields)

if (-not (Test-Path -LiteralPath $evidenceRoot -PathType Container)) {
  $result.status = "missing_evidence_root"
  if ($AsJson) { $result | ConvertTo-Json -Depth 12 | Write-Output } else { Write-Host "trace_grading.status=missing_evidence_root" }
  if ($failOnBreach) { exit 1 } else { exit 0 }
}

$cutoff = (Get-Date).AddDays(-1 * $windowDays)
$files = @(Get-ChildItem -LiteralPath $evidenceRoot -File -Filter "*.md" -ErrorAction SilentlyContinue)
$result.scanned_file_count = @($files).Count
$sampleFiles = @($files | Where-Object { $_.LastWriteTime -ge $cutoff })
$result.sampled_file_count = @($sampleFiles).Count

$fieldHit = @{}
foreach ($f in $requiredFields) { $fieldHit[$f] = 0 }
$missingByFile = New-Object System.Collections.Generic.List[object]

foreach ($file in @($sampleFiles)) {
  $raw = ""
  try { $raw = [string](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8) } catch { $raw = "" }
  $missing = New-Object System.Collections.Generic.List[string]
  foreach ($field in $requiredFields) {
    $pattern = "(?m)^\s*{0}\s*=\s*(.+?)\s*$" -f [regex]::Escape($field)
    $m = [regex]::Match($raw, $pattern)
    if ($m.Success -and -not [string]::IsNullOrWhiteSpace(([string]$m.Groups[1].Value).Trim())) {
      $fieldHit[$field] = [int]$fieldHit[$field] + 1
    } else {
      $missing.Add([string]$field) | Out-Null
    }
  }
  if ($missing.Count -gt 0) {
    $missingByFile.Add([pscustomobject]@{
      file = ($file.FullName -replace '\\', '/')
      missing_fields = @($missing)
    }) | Out-Null
  }
}

$coverageMap = [ordered]@{}
$sampleCount = [Math]::Max(1, [int]$result.sampled_file_count)
$coverageSum = 0.0
foreach ($field in $requiredFields) {
  $rate = [Math]::Round(([double]$fieldHit[$field] / [double]$sampleCount), 6)
  $coverageMap[$field] = $rate
  $coverageSum += $rate
}
$overallRate = 0.0
if ($requiredFields.Count -gt 0) {
  $overallRate = [Math]::Round(($coverageSum / [double]$requiredFields.Count), 6)
}

$result.field_coverage = $coverageMap
$result.overall_coverage_rate = $overallRate
$result.missing_fields_by_file = @($missingByFile.ToArray())

if ($result.sampled_file_count -lt $minSample) {
  $result.status = "insufficient_sample"
} elseif ($overallRate -lt $minCoverage) {
  $result.status = "coverage_below_threshold"
} else {
  $result.status = "ok"
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 12 | Write-Output
} else {
  Write-Host ("trace_grading.status={0}" -f $result.status)
  Write-Host ("trace_grading.sampled_file_count={0}" -f [int]$result.sampled_file_count)
  Write-Host ("trace_grading.overall_coverage_rate={0}" -f $result.overall_coverage_rate)
}

if ($failOnBreach -and [string]$result.status -ne "ok") { exit 1 }
exit 0
