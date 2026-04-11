param(
  [Parameter(Mandatory = $true)][string]$Query,
  [Parameter(Mandatory = $true)][string]$ShouldTrigger,
  [string]$RepoRoot = ".",
  [ValidateSet("validation", "train")]
  [string]$Split = "validation",
  [string]$SkillName = "",
  [string]$Triggered = "",
  [string]$EvidencePath = "",
  [string]$IssueId = "",
  [string]$Evaluator = "manual",
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

function Parse-NullableBool([string]$ValueText) {
  if ([string]::IsNullOrWhiteSpace($ValueText)) { return $null }
  $v = $ValueText.Trim().ToLowerInvariant()
  if (@("true", "1", "yes", "y") -contains $v) { return $true }
  if (@("false", "0", "no", "n") -contains $v) { return $false }
  throw "Triggered must be one of: true/false/1/0/yes/no."
}

function Parse-RequiredBool([string]$ValueText, [string]$FieldName) {
  $resolved = Parse-NullableBool $ValueText
  if ($null -eq $resolved) {
    throw "$FieldName must be one of: true/false/1/0/yes/no."
  }
  return [bool]$resolved
}

function Get-AutoTriggeredFromEvidence([string]$EvidencePathText, [string]$SkillNameText) {
  if ([string]::IsNullOrWhiteSpace($EvidencePathText)) { return $null }
  if (-not (Test-Path -LiteralPath $EvidencePathText -PathType Leaf)) { return $null }
  $raw = Get-Content -LiteralPath $EvidencePathText -Raw -ErrorAction SilentlyContinue
  if ([string]::IsNullOrWhiteSpace($raw)) { return $null }

  # Try JSON first.
  $obj = $null
  try { $obj = $raw | ConvertFrom-Json -ErrorAction Stop } catch { $obj = $null }
  if ($null -ne $obj) {
    $needle = if ([string]::IsNullOrWhiteSpace($SkillNameText)) { "" } else { $SkillNameText.Trim().ToLowerInvariant() }
    $json = $raw.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($needle)) {
      if ($json.Contains('"name":"skill"') -or $json.Contains('"type":"tool_use"') -and $json.Contains('"skill"')) {
        return $true
      }
    } else {
      if ($json.Contains($needle)) { return $true }
    }
    return $false
  }

  # Fallback text match.
  $text = $raw.ToLowerInvariant()
  if ([string]::IsNullOrWhiteSpace($SkillNameText)) {
    if ($text.Contains("skill") -and $text.Contains("tool_use")) { return $true }
  } else {
    if ($text.Contains($SkillNameText.Trim().ToLowerInvariant())) { return $true }
  }
  return $false
}

$queryText = $Query.Trim()
if ([string]::IsNullOrWhiteSpace($queryText)) {
  throw "Query cannot be empty."
}
$shouldTriggerValue = Parse-RequiredBool -ValueText $ShouldTrigger -FieldName "ShouldTrigger"

$repoPath = Normalize-RepoPath $RepoRoot
$eventFile = Join-Path ($repoPath -replace '/', '\') ".governance\skill-candidates\trigger-eval-runs.jsonl"
Ensure-ParentDirectory $eventFile

$triggeredValue = Parse-NullableBool $Triggered
if ($null -eq $triggeredValue) {
  $triggeredValue = Get-AutoTriggeredFromEvidence -EvidencePathText $EvidencePath -SkillNameText $SkillName
}
if ($null -eq $triggeredValue) {
  throw "Cannot resolve triggered result. Provide -Triggered or a valid -EvidencePath."
}

$record = [ordered]@{
  schema_version = "1.0"
  timestamp = (Get-Date).ToString("o")
  repo = $repoPath
  split = $Split
  query = $queryText
  should_trigger = [bool]$shouldTriggerValue
  triggered = [bool]$triggeredValue
  skill_name = $SkillName
  issue_id = $IssueId
  evaluator = $Evaluator
  evidence_path = $EvidencePath
}

($record | ConvertTo-Json -Depth 6 -Compress) | Out-File -LiteralPath $eventFile -Append -Encoding utf8

$result = [pscustomobject]@{
  ok = $true
  repo = $repoPath
  event_file = ($eventFile -replace '\\', '/')
  split = $Split
  should_trigger = [bool]$shouldTriggerValue
  triggered = [bool]$triggeredValue
  recorded_at = [string]$record.timestamp
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 6 | Write-Output
} else {
  Write-Host ("skill_trigger_eval.recorded split={0} should_trigger={1} triggered={2} event_file={3}" -f $Split, [bool]$ShouldTrigger, [bool]$triggeredValue, ($eventFile -replace '\\', '/'))
}
