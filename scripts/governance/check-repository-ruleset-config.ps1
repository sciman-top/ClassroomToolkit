param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath ".governance\repository-ruleset-policy.json"
$configPath = Join-Path $repoPath ".governance\repository-ruleset-config.json"
$rulesetPath = Join-Path $repoPath ".github\rulesets\default.json"

function New-Result {
  return [ordered]@{
    schema_version = "1.0"
    generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    repo_root = ($repoPath -replace "\\", "/")
    status = "ok"
    should_fail_gate = $false
    policy_path = ($policyPath -replace "\\", "/")
    checks = @()
  }
}

function Add-Check {
  param(
    [hashtable]$Result,
    [string]$Name,
    [string]$Status,
    [string]$Reason
  )
  $checks = [System.Collections.Generic.List[object]]::new()
  foreach ($c in @($Result.checks)) { [void]$checks.Add($c) }
  [void]$checks.Add([pscustomobject]@{
    name = $Name
    status = $Status
    reason = $Reason
  })
  $Result.checks = @($checks.ToArray())
}

function Set-Fail {
  param(
    [hashtable]$Result,
    [string]$Status
  )
  if ($Status -eq "warn" -and $Result.status -eq "ok") {
    $Result.status = "warn"
  } elseif ($Status -eq "error") {
    $Result.status = "error"
    $Result.should_fail_gate = $true
  }
}

function Read-JsonSafe {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    return $null
  }
  try {
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
  } catch {
    return $null
  }
}

$result = New-Result
$policy = Read-JsonSafe -Path $policyPath
$config = Read-JsonSafe -Path $configPath

$requireConfigArtifact = $true
if ($null -ne $policy -and $null -ne $policy.PSObject.Properties["require_ruleset_config_artifact"]) {
  $requireConfigArtifact = [bool]$policy.require_ruleset_config_artifact
}

$configExists = Test-Path -LiteralPath $configPath -PathType Leaf
$rulesetExists = Test-Path -LiteralPath $rulesetPath -PathType Leaf

if ($requireConfigArtifact -and -not ($configExists -or $rulesetExists)) {
  Add-Check -Result $result -Name "artifact_presence" -Status "error" -Reason "missing .governance/repository-ruleset-config.json and .github/rulesets/default.json"
  Set-Fail -Result $result -Status "error"
} else {
  Add-Check -Result $result -Name "artifact_presence" -Status "ok" -Reason "ruleset artifact present"
}

if (-not $configExists) {
  Add-Check -Result $result -Name "config_presence" -Status "warn" -Reason ".governance/repository-ruleset-config.json not found; fallback to .github/rulesets/default.json only"
  Set-Fail -Result $result -Status "warn"
} elseif ($null -eq $config) {
  Add-Check -Result $result -Name "config_json" -Status "error" -Reason "invalid JSON in .governance/repository-ruleset-config.json"
  Set-Fail -Result $result -Status "error"
} else {
  $requiredBoolFields = @(
    "require_pull_request",
    "require_status_checks",
    "require_review",
    "require_resolve_conversations"
  )
  $requiredMissing = 0
  foreach ($field in $requiredBoolFields) {
    if ($null -eq $config.PSObject.Properties[$field] -or -not ($config.$field -is [bool])) {
      Add-Check -Result $result -Name ("config_field_" + $field) -Status "error" -Reason "field missing or not boolean"
      Set-Fail -Result $result -Status "error"
      $requiredMissing++
    } elseif (-not [bool]$config.$field) {
      Add-Check -Result $result -Name ("config_field_" + $field) -Status "error" -Reason "field must be true by minimum control"
      Set-Fail -Result $result -Status "error"
      $requiredMissing++
    }
  }

  if ($null -eq $config.PSObject.Properties["required_approving_review_count"] -or -not ($config.required_approving_review_count -is [int]) -or [int]$config.required_approving_review_count -lt 1) {
    Add-Check -Result $result -Name "required_approving_review_count" -Status "error" -Reason "required_approving_review_count must be integer >= 1"
    Set-Fail -Result $result -Status "error"
    $requiredMissing++
  } else {
    Add-Check -Result $result -Name "required_approving_review_count" -Status "ok" -Reason "valid"
  }

  $checksList = @($config.required_status_checks)
  if ($checksList.Count -eq 0) {
    Add-Check -Result $result -Name "required_status_checks" -Status "error" -Reason "required_status_checks must include at least one check name"
    Set-Fail -Result $result -Status "error"
    $requiredMissing++
  } else {
    Add-Check -Result $result -Name "required_status_checks" -Status "ok" -Reason ("count=" + $checksList.Count)
  }

  if ($requiredMissing -eq 0) {
    Add-Check -Result $result -Name "minimum_controls" -Status "ok" -Reason "all minimum controls satisfied"
  }
}

$resultObj = [pscustomobject]$result
if ($AsJson) {
  $resultObj | ConvertTo-Json -Depth 8 | Write-Output
  if ($resultObj.should_fail_gate) { exit 1 } else { exit 0 }
}

Write-Host ("ruleset_config.status=" + $resultObj.status)
Write-Host ("ruleset_config.should_fail_gate=" + $resultObj.should_fail_gate)
foreach ($check in @($resultObj.checks)) {
  Write-Host ("[{0}] {1} - {2}" -f $check.status.ToUpperInvariant(), $check.name, $check.reason)
}
if ($resultObj.should_fail_gate) { exit 1 }
exit 0

