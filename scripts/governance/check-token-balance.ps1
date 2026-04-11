param(
  [string]$RepoRoot = ".",
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

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$policyPath = Join-Path $repoPath ".governance\token-balance-policy.json"
$metricsPath = Join-Path $repoPath "docs\governance\metrics-auto.md"

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

if (-not [bool]$policy.enabled) {
  $result = [pscustomobject]@{
    schema_version = "1.0"
    status = "DISABLED"
    repo_root = ($repoPath -replace '\\', '/')
    policy_path = ($policyPath -replace '\\', '/')
    metrics_path = ($metricsPath -replace '\\', '/')
    warning_count = 0
    violation_count = 0
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
  } elseif ($firstPass -lt [double]$policy.thresholds.min_first_pass_rate) {
    [void]$violations.Add([pscustomobject]@{
      metric = "first_pass_rate"
      actual = $firstPass
      threshold = [double]$policy.thresholds.min_first_pass_rate
      comparator = ">="
      recommendation = "Quality pressure: rollback to balanced profile and observe one week."
    })
  }

  if ($null -eq $reworkRate) {
    [void]$warnings.Add("rework_after_clarification_rate is missing or N/A")
  } elseif ($reworkRate -gt [double]$policy.thresholds.max_rework_after_clarification_rate) {
    [void]$violations.Add([pscustomobject]@{
      metric = "rework_after_clarification_rate"
      actual = $reworkRate
      threshold = [double]$policy.thresholds.max_rework_after_clarification_rate
      comparator = "<="
      recommendation = "Rework is high: raise clarification and budget profile; avoid further tightening."
    })
  }

  if ($null -ne $avgRespToken -and $avgRespToken -gt [int]$policy.thresholds.max_average_response_token) {
    [void]$violations.Add([pscustomobject]@{
      metric = "average_response_token"
      actual = $avgRespToken
      threshold = [int]$policy.thresholds.max_average_response_token
      comparator = "<="
      recommendation = "Responses are long: compress explanation layer first, keep execution and verification."
    })
  } elseif ($null -eq $avgRespToken) {
    [void]$warnings.Add("average_response_token is missing or N/A")
  }

  if ($null -ne $singleTaskToken -and $singleTaskToken -gt [int]$policy.thresholds.max_single_task_token) {
    [void]$violations.Add([pscustomobject]@{
      metric = "single_task_token"
      actual = $singleTaskToken
      threshold = [int]$policy.thresholds.max_single_task_token
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
  repo_root = ($repoPath -replace '\\', '/')
  policy_path = ($policyPath -replace '\\', '/')
  metrics_path = ($metricsPath -replace '\\', '/')
  warning_count = $warnings.Count
  violation_count = $violations.Count
  warnings = $warningsArray
  violations = $violationsArray
  suggested_rollback_profile = $policy.actions.suggested_rollback_profile
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($status -eq "ALERT") { exit 1 } else { exit 0 }
}

Write-Host ("token_balance.status={0}" -f $status)
Write-Host ("token_balance.warning_count={0}" -f $warnings.Count)
Write-Host ("token_balance.violation_count={0}" -f $violations.Count)
foreach ($w in @($warnings)) { Write-Host ("[WARN] " + [string]$w) }
foreach ($v in @($violations)) {
  Write-Host ("[VIOLATION] metric={0} actual={1} threshold={2} comparator={3}" -f $v.metric, $v.actual, $v.threshold, $v.comparator)
}
if ($status -eq "ALERT") { exit 1 } else { exit 0 }
