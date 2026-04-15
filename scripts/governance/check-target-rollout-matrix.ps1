param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepoPath {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
  return ([string]$Text).Trim().Replace('\', '/').TrimEnd('/')
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$reposPath = Join-Path $repoPath "config\repositories.json"
$registryPath = Join-Path $repoPath "config\governance-control-registry.json"
$matrixPath = Join-Path $repoPath "config\target-control-rollout-matrix.json"

if (-not (Test-Path -LiteralPath $reposPath -PathType Leaf)) { throw "repositories.json not found: $reposPath" }
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw "governance-control-registry.json not found: $registryPath" }
if (-not (Test-Path -LiteralPath $matrixPath -PathType Leaf)) { throw "target-control-rollout-matrix.json not found: $matrixPath" }

$repos = @((Get-Content -LiteralPath $reposPath -Raw | ConvertFrom-Json))
$registry = (Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json)
$matrix = (Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json)

$allRepos = [System.Collections.Generic.List[string]]::new()
foreach ($r in @($repos)) {
  $n = Normalize-RepoPath ([string]$r)
  if (-not [string]::IsNullOrWhiteSpace($n)) { [void]$allRepos.Add($n) }
}

$distributableProgressive = [System.Collections.Generic.List[object]]::new()
foreach ($c in @($registry.controls)) {
  $isProgressive = ($null -ne $c.PSObject.Properties['class'] -and [string]$c.class -eq "progressive")
  $isDistributable = ($null -ne $c.PSObject.Properties['distributable'] -and [bool]$c.distributable)
  if ($isProgressive -and $isDistributable) {
    [void]$distributableProgressive.Add($c)
  }
}

$matrixControlsById = @{}
foreach ($m in @($matrix.controls)) {
  if ($null -ne $m.PSObject.Properties['control_id']) {
    $matrixControlsById[[string]$m.control_id] = $m
  }
}

$missingControlIds = [System.Collections.Generic.List[string]]::new()
$missingRepoStates = [System.Collections.Generic.List[string]]::new()
$stateRows = [System.Collections.Generic.List[object]]::new()

foreach ($control in @($distributableProgressive)) {
  $controlId = [string]$control.control_id
  if (-not $matrixControlsById.ContainsKey($controlId)) {
    [void]$missingControlIds.Add($controlId)
    continue
  }

  $entry = $matrixControlsById[$controlId]
  $expectedRepos = [System.Collections.Generic.List[string]]::new()
  $repoScope = ""
  if ($null -ne $control.PSObject.Properties['repo_scope']) {
    $repoScope = [string]$control.repo_scope
  }
  if ($repoScope -eq "repo_specific_distributable" -and $null -ne $entry.PSObject.Properties['applies_to_repos']) {
    foreach ($r in @($entry.applies_to_repos)) {
      $n = Normalize-RepoPath ([string]$r)
      if (-not [string]::IsNullOrWhiteSpace($n)) { [void]$expectedRepos.Add($n) }
    }
  } else {
    foreach ($r in @($allRepos)) { [void]$expectedRepos.Add($r) }
  }

  $stateByRepo = @{}
  if ($null -ne $entry.PSObject.Properties['repo_states']) {
    foreach ($s in @($entry.repo_states)) {
      $repo = Normalize-RepoPath ([string]$s.repo)
      if (-not [string]::IsNullOrWhiteSpace($repo)) { $stateByRepo[$repo] = $s }
    }
  }

  foreach ($repo in @($expectedRepos)) {
    if (-not $stateByRepo.ContainsKey($repo)) {
      [void]$missingRepoStates.Add(("{0}::{1}" -f $controlId, $repo))
      continue
    }
    $s = $stateByRepo[$repo]
    $phase = ""
    if ($null -ne $s.PSObject.Properties['phase']) { $phase = [string]$s.phase }
    [void]$stateRows.Add([pscustomobject]@{
      control_id = $controlId
      repo = $repo
      phase = $phase
    })
  }
}

$result = [pscustomobject]@{
  schema_version = "1.0"
  repo_root = ($repoPath -replace '\\', '/')
  matrix_path = ($matrixPath -replace '\\', '/')
  distributable_progressive_control_count = [int]$distributableProgressive.Count
  matrix_control_count = [int]@($matrix.controls).Count
  missing_control_count = [int]$missingControlIds.Count
  missing_repo_state_count = [int]$missingRepoStates.Count
  missing_control_ids = @($missingControlIds)
  missing_repo_states = @($missingRepoStates)
  sampled_states = @($stateRows | Select-Object -First 50)
}

if ($AsJson.IsPresent) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($result.missing_control_count -gt 0 -or $result.missing_repo_state_count -gt 0) { exit 1 }
  exit 0
}

Write-Host ("target_rollout_matrix.distributable_progressive_control_count={0}" -f $result.distributable_progressive_control_count)
Write-Host ("target_rollout_matrix.matrix_control_count={0}" -f $result.matrix_control_count)
Write-Host ("target_rollout_matrix.missing_control_count={0}" -f $result.missing_control_count)
Write-Host ("target_rollout_matrix.missing_repo_state_count={0}" -f $result.missing_repo_state_count)
if ($result.missing_control_count -gt 0) {
  Write-Host ("target_rollout_matrix.missing_control_ids={0}" -f ([string]::Join(";", @($result.missing_control_ids))))
}
if ($result.missing_repo_state_count -gt 0) {
  Write-Host ("target_rollout_matrix.missing_repo_states={0}" -f ([string]::Join(";", @($result.missing_repo_states))))
}
if ($result.missing_control_count -gt 0 -or $result.missing_repo_state_count -gt 0) {
  exit 1
}
Write-Host "[PASS] target rollout matrix check passed"
exit 0
