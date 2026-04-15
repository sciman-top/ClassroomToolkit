param(
  [string]$RepoRoot = ".",
  [ValidateSet("lite", "standard", "deep")]
  [string]$TokenBudgetMode,
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-KeyValueMap {
  param([string]$Path)
  $map = @{}
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $map }
  foreach ($line in @(Get-Content -LiteralPath $Path)) {
    $s = [string]$line
    if ([string]::IsNullOrWhiteSpace($s)) { continue }
    if ($s.TrimStart().StartsWith("#")) { continue }
    $idx = $s.IndexOf("=")
    if ($idx -lt 1) { continue }
    $k = $s.Substring(0, $idx).Trim()
    $v = $s.Substring($idx + 1).Trim()
    if (-not [string]::IsNullOrWhiteSpace($k)) {
      $map[$k] = $v
    }
  }
  return $map
}

function Parse-RateOrNull {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text) -or $Text -eq "N/A") { return $null }
  $t = $Text.Trim()
  if ($t.EndsWith("%")) {
    $raw = 0.0
    if ([double]::TryParse($t.TrimEnd('%'), [ref]$raw)) {
      return ($raw / 100.0)
    }
  }
  $v = 0.0
  if ([double]::TryParse($t, [ref]$v)) {
    if ($v -gt 1.0) { return ($v / 100.0) }
    return $v
  }
  return $null
}

function Parse-IntOrNull {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text) -or $Text -eq "N/A") { return $null }
  $v = 0
  if ([int]::TryParse($Text.Trim(), [ref]$v)) { return $v }
  return $null
}

function Test-TokenBudgetModeValue {
  param([string]$Value)
  return @("lite", "standard", "deep") -contains ([string]$Value).Trim().ToLowerInvariant()
}

function Resolve-TokenBudgetMode {
  param(
    [string]$RepoRootResolved,
    [string]$ExplicitMode
  )

  if (Test-TokenBudgetModeValue -Value $ExplicitMode) {
    return ([string]$ExplicitMode).Trim().ToLowerInvariant()
  }

  $envMode = [Environment]::GetEnvironmentVariable("GOVERNANCE_TOKEN_BUDGET_MODE")
  if (-not (Test-TokenBudgetModeValue -Value $envMode)) {
    $envMode = [Environment]::GetEnvironmentVariable("TOKEN_BUDGET_MODE")
  }
  if (Test-TokenBudgetModeValue -Value $envMode) {
    return ([string]$envMode).Trim().ToLowerInvariant()
  }

  $projectRulePolicyPath = Join-Path $RepoRootResolved "config\project-rule-policy.json"
  if (Test-Path -LiteralPath $projectRulePolicyPath -PathType Leaf) {
    try {
      $projectRulePolicy = Get-Content -LiteralPath $projectRulePolicyPath -Raw | ConvertFrom-Json
      $modeFromProjectRule = $null
      if ($null -ne $projectRulePolicy.defaults -and $null -ne $projectRulePolicy.defaults.PSObject.Properties['token_budget_mode']) {
        $modeFromProjectRule = ([string]$projectRulePolicy.defaults.token_budget_mode).Trim().ToLowerInvariant()
      }

      $repoNorm = ($RepoRootResolved -replace '\\', '/').TrimEnd('/')
      $repoName = Split-Path -Leaf $RepoRootResolved
      foreach ($entry in @($projectRulePolicy.repos)) {
        if ($null -eq $entry) { continue }
        $match = $false
        if ($null -ne $entry.PSObject.Properties['repo'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repo)) {
          $entryRepo = ([string]$entry.repo -replace '\\', '/').TrimEnd('/')
          if ($entryRepo.Equals($repoNorm, [System.StringComparison]::OrdinalIgnoreCase)) {
            $match = $true
          }
        }
        if (-not $match -and $null -ne $entry.PSObject.Properties['repoName'] -and -not [string]::IsNullOrWhiteSpace([string]$entry.repoName)) {
          if (([string]$entry.repoName).Equals($repoName, [System.StringComparison]::OrdinalIgnoreCase)) {
            $match = $true
          }
        }
        if (-not $match) { continue }
        if ($null -ne $entry.PSObject.Properties['token_budget_mode']) {
          $candidate = ([string]$entry.token_budget_mode).Trim().ToLowerInvariant()
          if (Test-TokenBudgetModeValue -Value $candidate) {
            return $candidate
          }
        }
      }

      if (Test-TokenBudgetModeValue -Value $modeFromProjectRule) {
        return $modeFromProjectRule
      }
    } catch {
      # fallback chain continues
    }
  }

  # Gate strictness should not inherit response style defaults (e.g. lite output mode).
  return "standard"
}

function Get-ThresholdValue {
  param(
    [object]$PolicyObject,
    [string]$Mode,
    [string]$Key
  )

  if ($null -ne $PolicyObject.PSObject.Properties['thresholds_by_mode'] -and
      $null -ne $PolicyObject.thresholds_by_mode -and
      $null -ne $PolicyObject.thresholds_by_mode.PSObject.Properties[$Mode]) {
    $modeThresholds = $PolicyObject.thresholds_by_mode.PSObject.Properties[$Mode].Value
    if ($null -ne $modeThresholds -and $null -ne $modeThresholds.PSObject.Properties[$Key]) {
      return $modeThresholds.PSObject.Properties[$Key].Value
    }
  }

  if ($null -ne $PolicyObject.PSObject.Properties['thresholds'] -and
      $null -ne $PolicyObject.thresholds -and
      $null -ne $PolicyObject.thresholds.PSObject.Properties[$Key]) {
    return $PolicyObject.thresholds.PSObject.Properties[$Key].Value
  }

  return $null
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath ".governance\token-balance-policy.json"
$metricsPath = Join-Path $repoPath "docs\governance\metrics-auto.md"
$tokenBudgetModeResolved = Resolve-TokenBudgetMode -RepoRootResolved $repoPath -ExplicitMode $TokenBudgetMode

$policy = $null
if (Test-Path -LiteralPath $policyPath -PathType Leaf) {
  try {
    $policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
  } catch {
    $policy = $null
  }
}
if ($null -eq $policy) {
  $policy = [pscustomobject]@{
    enabled = $true
    thresholds = [pscustomobject]@{
      min_first_pass_rate = 0.6
      max_rework_after_clarification_rate = 0.35
      max_average_response_token = 1800
      max_single_task_token = 12000
    }
    actions = [pscustomobject]@{
      suggested_rollback_profile = [pscustomobject]@{
        token_budget_mode = "standard"
        max_autonomous_iterations = 4
        clarification_max_questions = 3
      }
    }
  }
}

$warnings = [System.Collections.Generic.List[string]]::new()
$violations = [System.Collections.Generic.List[object]]::new()

$minFirstPassRate = [double](Get-ThresholdValue -PolicyObject $policy -Mode $tokenBudgetModeResolved -Key "min_first_pass_rate")
$maxReworkAfterClarificationRate = [double](Get-ThresholdValue -PolicyObject $policy -Mode $tokenBudgetModeResolved -Key "max_rework_after_clarification_rate")
$maxAverageResponseToken = [int](Get-ThresholdValue -PolicyObject $policy -Mode $tokenBudgetModeResolved -Key "max_average_response_token")
$maxSingleTaskToken = [int](Get-ThresholdValue -PolicyObject $policy -Mode $tokenBudgetModeResolved -Key "max_single_task_token")

if (-not [bool]$policy.enabled) {
  $result = [pscustomobject]@{
    schema_version = "1.0"
    status = "DISABLED"
    token_budget_mode = $tokenBudgetModeResolved
    repo_root = ($repoPath -replace '\\', '/')
    policy_path = ($policyPath -replace '\\', '/')
    metrics_path = ($metricsPath -replace '\\', '/')
    warning_count = 0
    violation_count = 0
    effective_thresholds = [pscustomobject]@{
      min_first_pass_rate = $minFirstPassRate
      max_rework_after_clarification_rate = $maxReworkAfterClarificationRate
      max_average_response_token = $maxAverageResponseToken
      max_single_task_token = $maxSingleTaskToken
    }
    warnings = @()
    violations = @()
    suggested_rollback_profile = $policy.actions.suggested_rollback_profile
  }
  if ($AsJson) { $result | ConvertTo-Json -Depth 8 | Write-Output; exit 0 }
  Write-Host "token_balance.status=DISABLED"
  exit 0
}

if (-not (Test-Path -LiteralPath $metricsPath -PathType Leaf)) {
  [void]$warnings.Add("metrics file not found: $metricsPath")
} else {
  $kv = Parse-KeyValueMap -Path $metricsPath

  $firstPass = Parse-RateOrNull -Text ([string]$kv["first_pass_rate"])
  $reworkRate = Parse-RateOrNull -Text ([string]$kv["rework_after_clarification_rate"])
  $avgRespToken = Parse-IntOrNull -Text ([string]$kv["average_response_token"])
  $singleTaskToken = Parse-IntOrNull -Text ([string]$kv["single_task_token"])

  if ($null -eq $firstPass) {
    [void]$warnings.Add("first_pass_rate is missing or N/A")
  } elseif ($firstPass -lt $minFirstPassRate) {
    [void]$violations.Add([pscustomobject]@{
      metric = "first_pass_rate"
      actual = $firstPass
      threshold = $minFirstPassRate
      comparator = ">="
      recommendation = "Quality pressure: rollback to balanced profile and observe one week."
    })
  }

  if ($null -eq $reworkRate) {
    [void]$warnings.Add("rework_after_clarification_rate is missing or N/A")
  } elseif ($reworkRate -gt $maxReworkAfterClarificationRate) {
    [void]$violations.Add([pscustomobject]@{
      metric = "rework_after_clarification_rate"
      actual = $reworkRate
      threshold = $maxReworkAfterClarificationRate
      comparator = "<="
      recommendation = "Rework is high: raise clarification and budget profile; avoid further tightening."
    })
  }

  if ($null -ne $avgRespToken -and $avgRespToken -gt $maxAverageResponseToken) {
    [void]$violations.Add([pscustomobject]@{
      metric = "average_response_token"
      actual = $avgRespToken
      threshold = $maxAverageResponseToken
      comparator = "<="
      recommendation = "Responses are long: compress explanation layer first, keep execution and verification."
    })
  } elseif ($null -eq $avgRespToken) {
    [void]$warnings.Add("average_response_token is missing or N/A")
  }

  if ($null -ne $singleTaskToken -and $singleTaskToken -gt $maxSingleTaskToken) {
    [void]$violations.Add([pscustomobject]@{
      metric = "single_task_token"
      actual = $singleTaskToken
      threshold = $maxSingleTaskToken
      comparator = "<="
      recommendation = "Task token cost is high: split tasks and improve template reuse."
    })
  } elseif ($null -eq $singleTaskToken) {
    [void]$warnings.Add("single_task_token is missing or N/A")
  }
}

$status = if ($violations.Count -gt 0) { "ALERT" } elseif ($warnings.Count -gt 0) { "ADVISORY" } else { "OK" }
$warningsArray = New-Object string[] $warnings.Count
if ($warnings.Count -gt 0) {
  $warnings.CopyTo($warningsArray, 0)
}
$violationsArray = New-Object object[] $violations.Count
if ($violations.Count -gt 0) {
  $violations.CopyTo($violationsArray, 0)
}
$result = [pscustomobject]@{
  schema_version = "1.0"
  status = $status
  token_budget_mode = $tokenBudgetModeResolved
  repo_root = ($repoPath -replace '\\', '/')
  policy_path = ($policyPath -replace '\\', '/')
  metrics_path = ($metricsPath -replace '\\', '/')
  warning_count = $warnings.Count
  violation_count = $violations.Count
  effective_thresholds = [pscustomobject]@{
    min_first_pass_rate = $minFirstPassRate
    max_rework_after_clarification_rate = $maxReworkAfterClarificationRate
    max_average_response_token = $maxAverageResponseToken
    max_single_task_token = $maxSingleTaskToken
  }
  warnings = $warningsArray
  violations = $violationsArray
  suggested_rollback_profile = $policy.actions.suggested_rollback_profile
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($status -eq "ALERT") { exit 1 } else { exit 0 }
}

Write-Host ("token_balance.status={0}" -f $status)
Write-Host ("token_balance.token_budget_mode={0}" -f $tokenBudgetModeResolved)
Write-Host ("token_balance.warning_count={0}" -f $warnings.Count)
Write-Host ("token_balance.violation_count={0}" -f $violations.Count)
foreach ($w in @($warnings)) { Write-Host ("[WARN] " + [string]$w) }
foreach ($v in @($violations)) {
  Write-Host ("[VIOLATION] metric={0} actual={1} threshold={2} comparator={3}" -f $v.metric, $v.actual, $v.threshold, $v.comparator)
}
if ($status -eq "ALERT") { exit 1 } else { exit 0 }
