param(
  [Parameter(Mandatory = $true)][string]$IssueSignature,
  [string]$RepoRoot = ".",
  [string]$IssueId = "",
  [string]$StepName = "",
  [string]$CommandText = "",
  [string]$FailureReason = "",
  [string]$EvidenceLink = "",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepoPath([string]$PathText) {
  $resolved = Resolve-Path -LiteralPath $PathText -ErrorAction Stop
  return ([System.IO.Path]::GetFullPath($resolved.Path) -replace '\\', '/').TrimEnd('/')
}

function Ensure-ParentDirectory([string]$PathText) {
  $parent = Split-Path -Parent $PathText
  if ([string]::IsNullOrWhiteSpace($parent)) { return }
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
  }
}

$repoPath = Normalize-RepoPath $RepoRoot
$eventFile = Join-Path ($repoPath -replace '/', '\') ".governance\skill-candidates\events.jsonl"
Ensure-ParentDirectory $eventFile

$signature = $IssueSignature.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($signature)) {
  throw "IssueSignature cannot be empty."
}

$record = [ordered]@{
  schema_version = "1.0"
  timestamp = (Get-Date).ToString("o")
  repo = $repoPath
  issue_signature = $signature
  issue_id = $IssueId
  step_name = $StepName
  command_text = $CommandText
  failure_reason = $FailureReason
  evidence_link = $EvidenceLink
}

($record | ConvertTo-Json -Depth 6 -Compress) | Out-File -LiteralPath $eventFile -Append -Encoding utf8

$result = [pscustomobject]@{
  ok = $true
  repo = $repoPath
  event_file = ($eventFile -replace '\\', '/')
  issue_signature = $signature
  recorded_at = [string]$record.timestamp
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 6 | Write-Output
} else {
  Write-Host ("skill_candidate.recorded signature={0} event_file={1}" -f $signature, ($eventFile -replace '\\', '/'))
}
