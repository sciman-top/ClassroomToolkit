param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-IsoDateOrNull {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
  $formats = @("yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK")
  foreach ($f in $formats) {
    try {
      return [DateTime]::ParseExact(
        $Text.Trim(),
        $f,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeLocal
      )
    } catch { }
  }
  return $null
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath "config\control-retirement-candidates.json"
if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
  throw "control-retirement-candidates file not found: $policyPath"
}

$doc = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
$today = (Get-Date).Date
$activeStatuses = @("proposed", "approved", "in_progress")
$activeCount = 0
$overdueCount = 0

foreach ($candidate in @($doc.candidates)) {
  $status = ""
  if ($null -ne $candidate.PSObject.Properties['candidate_status']) {
    $status = ([string]$candidate.candidate_status).Trim().ToLowerInvariant()
  }
  if ($activeStatuses -contains $status) {
    $activeCount++
    $due = $null
    if ($null -ne $candidate.PSObject.Properties['decision_due_date']) {
      $due = Parse-IsoDateOrNull -Text ([string]$candidate.decision_due_date)
    }
    if ($null -ne $due -and $due.Date -lt $today) {
      $overdueCount++
    }
  }
}

$result = [pscustomobject]@{
  schema_version = "1.0"
  repo_root = ($repoPath -replace '\\', '/')
  policy_path = ($policyPath -replace '\\', '/')
  active_candidate_count = [int]$activeCount
  overdue_candidate_count = [int]$overdueCount
}

if ($AsJson.IsPresent) {
  $result | ConvertTo-Json -Depth 6 | Write-Output
  if ($result.overdue_candidate_count -gt 0) { exit 1 }
  exit 0
}

Write-Host ("control_retirement.active_candidate_count={0}" -f $result.active_candidate_count)
Write-Host ("control_retirement.overdue_candidate_count={0}" -f $result.overdue_candidate_count)
if ($result.overdue_candidate_count -gt 0) {
  exit 1
}
Write-Host "[PASS] control retirement candidate check passed"
exit 0
