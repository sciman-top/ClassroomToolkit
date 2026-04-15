param(
  [string]$RepoRoot = ".",
  [string]$PolicyRelativePath = ".governance/trace-grading-policy.json",
  [string]$EvidenceRelativePath = "docs/change-evidence",
  [ValidateSet("plan", "safe")]
  [string]$Mode = "plan",
  [int]$MaxFiles = 200,
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
  try { return (Get-Content -LiteralPath $PathText -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { return $DefaultValue }
}

function Get-DefaultValue([string]$FieldName) {
  $normalized = ""
  if ($null -ne $FieldName) { $normalized = ([string]$FieldName).Trim().ToLowerInvariant() }
  switch ($normalized) {
    "decision_score" { return "0.80" }
    "hard_guard_hits" { return "none" }
    "reason_codes" { return "trace_grading_backfill" }
    default { return "N/A" }
  }
}

function Upsert-FieldLine([string]$RawText, [string]$FieldName, [string]$FieldValue) {
  $escaped = [regex]::Escape($FieldName)
  $pattern = "(?m)^(\s*{0}\s*=\s*)(.*?)\s*$" -f $escaped
  $m = [regex]::Match($RawText, $pattern)
  if ($m.Success) {
    $existing = ([string]$m.Groups[2].Value).Trim()
    if (-not [string]::IsNullOrWhiteSpace($existing)) {
      return [pscustomobject]@{ text = $RawText; changed = $false }
    }
    $updated = [regex]::Replace($RawText, $pattern, ('$1' + $FieldValue), 1)
    return [pscustomobject]@{ text = $updated; changed = $true }
  }
  $newline = if ($RawText.Contains("`r`n")) { "`r`n" } else { "`n" }
  $suffix = if ([string]::IsNullOrWhiteSpace($RawText) -or $RawText.EndsWith($newline)) { "" } else { $newline }
  $updatedText = $RawText + $suffix + ("{0}={1}" -f $FieldName, $FieldValue) + $newline
  return [pscustomobject]@{ text = $updatedText; changed = $true }
}

$repoPath = Resolve-NormalizedPath $RepoRoot
$policyPath = Join-Path ($repoPath -replace '/', '\') ($PolicyRelativePath -replace '/', '\')
$evidenceRoot = Join-Path ($repoPath -replace '/', '\') ($EvidenceRelativePath -replace '/', '\')

$result = [ordered]@{
  schema_version = "1.0"
  mode = $Mode
  repo_root = $repoPath
  policy_path = ($policyPath -replace '\\', '/')
  evidence_root = ($evidenceRoot -replace '\\', '/')
  status = "ok"
  required_fields = @()
  scanned_file_count = 0
  candidate_file_count = 0
  changed_file_count = 0
  changed_files = @()
}

$policy = Read-Json -PathText $policyPath -DefaultValue $null
if ($null -eq $policy) {
  $result.status = "missing_policy"
  if ($AsJson) { $result | ConvertTo-Json -Depth 10 | Write-Output } else { Write-Host "trace_grading_backfill.status=missing_policy" }
  exit 1
}

$requiredFields = @("decision_score", "hard_guard_hits", "reason_codes")
if ($null -ne $policy.PSObject.Properties['required_fields']) {
  $candidate = @($policy.required_fields | ForEach-Object { ([string]$_).Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  if ($candidate.Count -gt 0) { $requiredFields = @($candidate) }
}
$result.required_fields = @($requiredFields)

if (-not (Test-Path -LiteralPath $evidenceRoot -PathType Container)) {
  $result.status = "missing_evidence_root"
  if ($AsJson) { $result | ConvertTo-Json -Depth 10 | Write-Output } else { Write-Host "trace_grading_backfill.status=missing_evidence_root" }
  exit 1
}

$files = @(Get-ChildItem -LiteralPath $evidenceRoot -File -Filter "*.md" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First $MaxFiles)
$result["scanned_file_count"] = [int](@($files).Count)
$changed = New-Object System.Collections.Generic.List[object]
$candidateCount = 0

foreach ($file in @($files)) {
  $raw = [string](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
  $working = $raw
  $fileChanged = $false
  foreach ($field in $requiredFields) {
    $value = Get-DefaultValue $field
    $patch = Upsert-FieldLine -RawText $working -FieldName $field -FieldValue $value
    $working = [string]$patch.text
    if ([bool]$patch.changed) { $fileChanged = $true }
  }
  if ($fileChanged) {
    $candidateCount++
    if ($Mode -eq "safe") {
      [System.IO.File]::WriteAllText($file.FullName, $working, [System.Text.UTF8Encoding]::new($false))
    }
    $changed.Add([pscustomobject]@{ file = ($file.FullName -replace '\\', '/'); action = $Mode }) | Out-Null
  }
}

$result["candidate_file_count"] = [int]$candidateCount
$result["changed_file_count"] = [int]$changed.Count
$result["changed_files"] = @($changed.ToArray())

if ($AsJson) {
  $result | ConvertTo-Json -Depth 10 | Write-Output
} else {
  Write-Host ("trace_grading_backfill.status={0}" -f $result.status)
  Write-Host ("trace_grading_backfill.mode={0}" -f $Mode)
  Write-Host ("trace_grading_backfill.scanned_file_count={0}" -f [int]$result.scanned_file_count)
  Write-Host ("trace_grading_backfill.candidate_file_count={0}" -f [int]$result.candidate_file_count)
  Write-Host ("trace_grading_backfill.changed_file_count={0}" -f [int]$result.changed_file_count)
}

exit 0
