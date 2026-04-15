param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$reposPath = Join-Path $repoPath "config\repositories.json"
$rolloutPath = Join-Path $repoPath "config\rule-rollout.json"

function Normalize-RepoPath {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
  return ([string]$Text).Trim().Replace('\', '/').TrimEnd('/')
}

if (-not (Test-Path -LiteralPath $reposPath -PathType Leaf)) {
  throw "repositories.json not found: $reposPath"
}
if (-not (Test-Path -LiteralPath $rolloutPath -PathType Leaf)) {
  throw "rule-rollout.json not found: $rolloutPath"
}

$repos = @()
try {
  $repos = @((Get-Content -LiteralPath $reposPath -Raw | ConvertFrom-Json))
} catch {
  throw "failed to parse repositories.json: $($_.Exception.Message)"
}
$rollout = $null
try {
  $rollout = (Get-Content -LiteralPath $rolloutPath -Raw | ConvertFrom-Json)
} catch {
  throw "failed to parse rule-rollout.json: $($_.Exception.Message)"
}

$repoSet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($repo in @($repos)) {
  $normalized = Normalize-RepoPath ([string]$repo)
  if (-not [string]::IsNullOrWhiteSpace($normalized)) {
    [void]$repoSet.Add($normalized)
  }
}

$rolloutRepoSet = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$rolloutRepos = @()
if ($null -ne $rollout -and $null -ne $rollout.PSObject.Properties['repos']) {
  $rolloutRepos = @($rollout.repos)
}
foreach ($item in $rolloutRepos) {
  $normalized = Normalize-RepoPath ([string]$item.repo)
  if (-not [string]::IsNullOrWhiteSpace($normalized)) {
    [void]$rolloutRepoSet.Add($normalized)
  }
}

$missing = New-Object System.Collections.Generic.List[string]
foreach ($repo in $repoSet) {
  if (-not $rolloutRepoSet.Contains($repo)) {
    $missing.Add($repo) | Out-Null
  }
}

$orphan = New-Object System.Collections.Generic.List[string]
foreach ($repo in $rolloutRepoSet) {
  if (-not $repoSet.Contains($repo)) {
    $orphan.Add($repo) | Out-Null
  }
}

$result = [pscustomobject]@{
  schema_version = "1.0"
  repo_root = ($repoPath -replace '\\', '/')
  repository_count = [int]$repoSet.Count
  rollout_repo_count = [int]$rolloutRepoSet.Count
  coverage_gap_count = [int]$missing.Count
  orphan_rollout_count = [int]$orphan.Count
  missing_rollout_repos = @($missing)
  orphan_rollout_repos = @($orphan)
}

if ($AsJson.IsPresent) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($result.coverage_gap_count -gt 0 -or $result.orphan_rollout_count -gt 0) { exit 1 } else { exit 0 }
}

Write-Host ("rollout_coverage.repository_count={0}" -f $result.repository_count)
Write-Host ("rollout_coverage.rollout_repo_count={0}" -f $result.rollout_repo_count)
Write-Host ("rollout_coverage.coverage_gap_count={0}" -f $result.coverage_gap_count)
Write-Host ("rollout_coverage.orphan_rollout_count={0}" -f $result.orphan_rollout_count)
if ($result.coverage_gap_count -gt 0) {
  Write-Host ("rollout_coverage.missing={0}" -f ([string]::Join(";", @($result.missing_rollout_repos))))
}
if ($result.orphan_rollout_count -gt 0) {
  Write-Host ("rollout_coverage.orphan={0}" -f ([string]::Join(";", @($result.orphan_rollout_repos))))
}
if ($result.coverage_gap_count -gt 0 -or $result.orphan_rollout_count -gt 0) {
  exit 1
}
Write-Host "[PASS] rollout coverage check passed"
exit 0

