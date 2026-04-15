param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$commonPath = Join-Path $repoPath "scripts\lib\common.ps1"
if (-not (Test-Path -LiteralPath $commonPath -PathType Leaf)) {
  throw "Missing common helper: $commonPath"
}
. $commonPath

$policyPath = Join-Path $repoPath "config\practice-stack-policy.json"
$reposPath = Join-Path $repoPath "config\repositories.json"
if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
  throw "practice-stack-policy.json not found: $policyPath"
}
if (-not (Test-Path -LiteralPath $reposPath -PathType Leaf)) {
  throw "repositories.json not found: $reposPath"
}

$policy = Read-JsonFile -Path $policyPath -DisplayName $policyPath
$repos = @(Read-JsonArray $reposPath)

$practiceOrder = @(
  "sdd",
  "tdd",
  "atdd_bdd",
  "contract_testing",
  "harness_engineering",
  "policy_as_code",
  "observability",
  "progressive_delivery",
  "hooks_ci_gates",
  "ssdf",
  "slsa",
  "sbom",
  "scorecard",
  "repository_rulesets"
)

function Test-PropertyExists {
  param(
    [object]$Obj,
    [string]$Name
  )

  if ($null -eq $Obj -or [string]::IsNullOrWhiteSpace($Name)) {
    return $false
  }
  if ($Obj -is [System.Collections.IDictionary]) {
    return $Obj.Contains($Name)
  }
  if ($null -ne $Obj.PSObject -and $null -ne $Obj.PSObject.Properties[$Name]) {
    return $true
  }
  return $false
}

function Get-PropertyValue {
  param(
    [object]$Obj,
    [string]$Name
  )

  if (-not (Test-PropertyExists -Obj $Obj -Name $Name)) {
    return $null
  }
  if ($Obj -is [System.Collections.IDictionary]) {
    return $Obj[$Name]
  }
  return $Obj.$Name
}

function Get-LevelWeight {
  param([string]$Level)
  switch ($Level) {
    "required" { return 2 }
    "recommended" { return 1 }
    default { return 0 }
  }
}

function Get-RepoPracticeEntry {
  param(
    [object[]]$RepoEntries,
    [string]$RepoName
  )

  foreach ($entry in @($RepoEntries)) {
    if ($null -eq $entry) { continue }
    $entryName = [string]$entry.repoName
    if ($entryName.Equals($RepoName, [System.StringComparison]::OrdinalIgnoreCase)) {
      return $entry
    }
  }
  return $null
}

$alerts = [System.Collections.Generic.List[object]]::new()
$items = [System.Collections.Generic.List[object]]::new()
$repoEntries = if ($null -eq $policy.repos) { @() } else { @($policy.repos) }

foreach ($repoItem in @($repos)) {
  $repoText = [string]$repoItem
  if ([string]::IsNullOrWhiteSpace($repoText)) { continue }
  $repoName = Split-Path -Leaf $repoText
  $repoPolicy = Get-RepoPracticeEntry -RepoEntries $repoEntries -RepoName $repoName
  $repoPractices = Get-PropertyValue -Obj $repoPolicy -Name "practices"

  $totalWeight = 0
  $gotWeight = 0
  $missingRequired = [System.Collections.Generic.List[string]]::new()
  $missingRecommended = [System.Collections.Generic.List[string]]::new()

  foreach ($practice in $practiceOrder) {
    $defaultLevel = "optional"
    if (Test-PropertyExists -Obj $policy.default -Name $practice) {
      $defaultLevel = [string](Get-PropertyValue -Obj $policy.default -Name $practice)
    }

    $weight = Get-LevelWeight -Level $defaultLevel
    $totalWeight += $weight

    $enabled = $true
    if (Test-PropertyExists -Obj $repoPractices -Name $practice) {
      $raw = Get-PropertyValue -Obj $repoPractices -Name $practice
      if ($raw -is [bool]) {
        $enabled = [bool]$raw
      } else {
        $enabled = $false
      }
    }

    if ($enabled) {
      $gotWeight += $weight
      continue
    }

    if ($defaultLevel -eq "required") {
      [void]$missingRequired.Add($practice)
      $alerts.Add([pscustomobject]@{
        repo = $repoName
        practice = $practice
        level = "required"
        message = "required practice disabled"
      }) | Out-Null
      continue
    }

    if ($defaultLevel -eq "recommended") {
      [void]$missingRecommended.Add($practice)
      $alerts.Add([pscustomobject]@{
        repo = $repoName
        practice = $practice
        level = "recommended"
        message = "recommended practice disabled"
      }) | Out-Null
    }
  }

  $score = if ($totalWeight -eq 0) { 100 } else { [int][math]::Round((100.0 * $gotWeight) / $totalWeight) }
  $items.Add([pscustomobject]@{
    repo = $repoName
    score = $score
    missing_required = @($missingRequired)
    missing_recommended = @($missingRecommended)
  }) | Out-Null
}

$averageScore = 100
if ($items.Count -gt 0) {
  $averageScore = [int][math]::Round((($items | Measure-Object -Property score -Average).Average))
}

$hasRequiredAlerts = @($alerts | Where-Object { $_.level -eq "required" }).Count -gt 0
$status = if ($alerts.Count -eq 0) { "PASS" } elseif ($hasRequiredAlerts) { "WARN" } else { "ADVISORY" }

$result = [pscustomobject]@{
  schema_version = "1.0"
  status = $status
  summary = [pscustomobject]@{
    repo_count = $items.Count
    alert_count = $alerts.Count
    average_score = $averageScore
  }
  repositories = @($items)
  alerts = @($alerts)
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  exit 0
}

Write-Host ("practice_stack.status=" + $result.status)
Write-Host ("practice_stack.repo_count=" + $result.summary.repo_count)
Write-Host ("practice_stack.alert_count=" + $result.summary.alert_count)
Write-Host ("practice_stack.average_score=" + $result.summary.average_score)
exit 0
