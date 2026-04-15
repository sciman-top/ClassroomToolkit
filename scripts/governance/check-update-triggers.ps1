param(
  [string]$RepoRoot = ".",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-KeyValueMap {
  param([string]$Path)
  $map = @{}
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    return $map
  }
  foreach ($line in @(Get-Content -LiteralPath $Path)) {
    $s = [string]$line
    if ([string]::IsNullOrWhiteSpace($s)) { continue }
    if ($s.TrimStart().StartsWith("#")) { continue }
    $i = $s.IndexOf("=")
    if ($i -lt 1) { continue }
    $k = $s.Substring(0, $i).Trim()
    $v = $s.Substring($i + 1).Trim()
    if (-not [string]::IsNullOrWhiteSpace($k)) {
      $map[$k] = $v
    }
  }
  return $map
}

function Parse-IsoDateOrNull {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
  $d = [datetime]::MinValue
  if ([datetime]::TryParseExact($Text, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$d)) {
    return $d.Date
  }
  return $null
}

function Add-Alert {
  param(
    [System.Collections.Generic.List[object]]$List,
    [string]$Id,
    [string]$Severity,
    [string]$Reason,
    [string]$RecommendedAction,
    [string]$Evidence
  )
  $List.Add([pscustomobject]@{
    id = $Id
    severity = $Severity
    reason = $Reason
    recommended_action = $RecommendedAction
    evidence = $Evidence
  }) | Out-Null
}

function Normalize-StringArray {
  param([object]$Value)
  if ($null -eq $Value) {
    return ,([object[]]@())
  }
  if ($Value -is [System.Array]) {
    return ,([object[]]@($Value | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique))
  }
  return ,([object[]]@([string]$Value))
}

function Test-StringArraySetEqual {
  param([object]$A, [object]$B)
  $left = Normalize-StringArray -Value $A
  $right = Normalize-StringArray -Value $B
  if ($left.Count -ne $right.Count) { return $false }
  for ($i = 0; $i -lt $left.Count; $i++) {
    if (-not $left[$i].Equals($right[$i], [System.StringComparison]::OrdinalIgnoreCase)) {
      return $false
    }
  }
  return $true
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$kitRoot = $repoPath
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (Test-Path -LiteralPath $commonPath -PathType Leaf) {
  . $commonPath
  Assert-Command -Name powershell
  $psExe = Get-CurrentPowerShellPath
} else {
  $psExe = (Get-Process -Id $PID -ErrorAction SilentlyContinue).Path
  if ([string]::IsNullOrWhiteSpace($psExe)) {
    $psExe = "powershell"
  }
}

$policyPath = Join-Path $kitRoot "config\update-trigger-policy.json"
$policy = $null
if (Test-Path -LiteralPath $policyPath -PathType Leaf) {
  try {
    $policy = Read-JsonFile -Path $policyPath -DisplayName $policyPath
  } catch {
    $policy = $null
  }
}
if ($null -eq $policy) {
  $policy = [pscustomobject]@{
    cadence = [pscustomobject]@{
      recurring_review_days = 7
    }
    triggers = [pscustomobject]@{
      cli_version_drift = [pscustomobject]@{ enabled = $true; severity = "high" }
      rollout_observe_overdue = [pscustomobject]@{ enabled = $true; severity = "medium" }
      rollout_metadata_coverage_gap = [pscustomobject]@{
        enabled = $true
        severity = "medium"
        max_allowed_gap_count = 0
        max_allowed_orphan_count = 0
      }
      metrics_snapshot_stale = [pscustomobject]@{ enabled = $true; severity = "medium"; max_age_days = 8 }
      waiver_expired_unrecovered = [pscustomobject]@{ enabled = $true; severity = "high" }
      platform_na_expired = [pscustomobject]@{ enabled = $true; severity = "medium" }
      release_distribution_policy_drift = [pscustomobject]@{ enabled = $true; severity = "high" }
      skill_trigger_eval_summary_stale = [pscustomobject]@{ enabled = $false; severity = "medium"; max_age_days = 8 }
      low_value_orphan_custom_sources = [pscustomobject]@{ enabled = $false; severity = "medium" }
      gate_noise_budget_breach = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_false_positive_rate = 0.05
        max_gate_latency_delta_ms = 5000
        alert_on_data_gap = $false
      }
      stale_progressive_controls_present = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_allowed_count = 0
      }
      not_observable_controls_present = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_allowed_count = 0
      }
      rule_duplication_detected = [pscustomobject]@{
        enabled = $false
        severity = "medium"
      }
      control_retirement_backlog = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_active_candidate_count = 8
        max_overdue_candidate_count = 0
      }
      evidence_template_fields_missing = [pscustomobject]@{
        enabled = $false
        severity = "medium"
      }
      target_rollout_matrix_gap = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_allowed_missing_control_count = 0
        max_allowed_missing_repo_state_count = 0
      }
      cross_repo_feedback_snapshot_stale = [pscustomobject]@{
        enabled = $false
        severity = "medium"
        max_age_days = 8
        min_feedback_ingested_count = 1
      }
      dependency_review_policy_drift = [pscustomobject]@{
        enabled = $false
        severity = "high"
        required_fail_on_severity = "high"
        required_action_major = "v4"
      }
      slsa_provenance_placeholder = [pscustomobject]@{
        enabled = $false
        severity = "high"
      }
    }
  }
}

$alerts = [System.Collections.Generic.List[object]]::new()
$steps = [System.Collections.Generic.List[object]]::new()
$today = (Get-Date).Date

# 1) CLI drift trigger
$driftScript = Join-Path $kitRoot "scripts\check-cli-version-drift.ps1"
if ([bool]$policy.triggers.cli_version_drift.enabled -and (Test-Path -LiteralPath $driftScript -PathType Leaf)) {
  $driftOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $driftScript -AsJson 2>&1
  $driftExit = $LASTEXITCODE
  $driftText = [string]::Join([Environment]::NewLine, @($driftOut))
  $drift = $null
  if (-not [string]::IsNullOrWhiteSpace($driftText)) {
    try { $drift = $driftText | ConvertFrom-Json } catch { $drift = $null }
  }
  $steps.Add([pscustomobject]@{ name = "cli-version-drift"; exit_code = [int]$driftExit }) | Out-Null
  if ($null -ne $drift -and [int]$drift.drift_count -gt 0) {
    Add-Alert -List $alerts `
      -Id "cli_version_drift" `
      -Severity ([string]$policy.triggers.cli_version_drift.severity) `
      -Reason ("detected cli drift count={0}" -f [int]$drift.drift_count) `
      -RecommendedAction "Upgrade drifted CLIs to stable and rerun build->test->contract/invariant->hotspot." `
      -Evidence "scripts/check-cli-version-drift.ps1"
  }
}

# 2) rollout observe overdue trigger
$rolloutScript = Join-Path $kitRoot "scripts\rollout-status.ps1"
if ([bool]$policy.triggers.rollout_observe_overdue.enabled -and (Test-Path -LiteralPath $rolloutScript -PathType Leaf)) {
  $rolloutOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $rolloutScript 2>&1
  $rolloutExit = $LASTEXITCODE
  $rolloutText = ($rolloutOut | Out-String)
  $steps.Add([pscustomobject]@{ name = "rollout-observe-overdue"; exit_code = [int]$rolloutExit }) | Out-Null
  $m = [regex]::Match($rolloutText, "(?m)^phase\.observe_overdue=([0-9]+)\s*$")
  if ($m.Success -and [int]$m.Groups[1].Value -gt 0) {
    $overdue = [int]$m.Groups[1].Value
    Add-Alert -List $alerts `
      -Id "rollout_observe_overdue" `
      -Severity ([string]$policy.triggers.rollout_observe_overdue.severity) `
      -Reason ("observe_overdue={0}" -f $overdue) `
      -RecommendedAction "Review rollout policy and move overdue observe repos to enforce/waiver decision." `
      -Evidence "scripts/rollout-status.ps1"
  }
}

# 2.1) rollout metadata coverage gap
$rolloutCoverageGapCount = 0
$rolloutCoverageOrphanCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['rollout_metadata_coverage_gap'] -and [bool]$policy.triggers.rollout_metadata_coverage_gap.enabled) {
  $rolloutCoverageScript = Join-Path $kitRoot "scripts\governance\check-rollout-coverage.ps1"
  if (Test-Path -LiteralPath $rolloutCoverageScript -PathType Leaf) {
    $coverageOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $rolloutCoverageScript -RepoRoot $repoPath -AsJson 2>&1
    $coverageExit = $LASTEXITCODE
    $coverageText = [string]::Join([Environment]::NewLine, @($coverageOut))
    $coverageObj = $null
    if (-not [string]::IsNullOrWhiteSpace($coverageText)) {
      try { $coverageObj = $coverageText | ConvertFrom-Json } catch { $coverageObj = $null }
    }
    $steps.Add([pscustomobject]@{ name = "rollout-coverage-gap"; exit_code = [int]$coverageExit }) | Out-Null
    if ($null -ne $coverageObj) {
      if ($coverageObj.PSObject.Properties.Name -contains "coverage_gap_count") {
        $rolloutCoverageGapCount = [int]$coverageObj.coverage_gap_count
      }
      if ($coverageObj.PSObject.Properties.Name -contains "orphan_rollout_count") {
        $rolloutCoverageOrphanCount = [int]$coverageObj.orphan_rollout_count
      }
    }
    $maxAllowedGapCount = 0
    $maxAllowedOrphanCount = 0
    if ($null -ne $policy.triggers.rollout_metadata_coverage_gap.PSObject.Properties['max_allowed_gap_count']) {
      $maxAllowedGapCount = [int]$policy.triggers.rollout_metadata_coverage_gap.max_allowed_gap_count
    }
    if ($null -ne $policy.triggers.rollout_metadata_coverage_gap.PSObject.Properties['max_allowed_orphan_count']) {
      $maxAllowedOrphanCount = [int]$policy.triggers.rollout_metadata_coverage_gap.max_allowed_orphan_count
    }
    if ($rolloutCoverageGapCount -gt $maxAllowedGapCount -or $rolloutCoverageOrphanCount -gt $maxAllowedOrphanCount) {
      Add-Alert -List $alerts `
        -Id "rollout_metadata_coverage_gap" `
        -Severity ([string]$policy.triggers.rollout_metadata_coverage_gap.severity) `
        -Reason ("coverage_gap_count={0} > {1} or orphan_rollout_count={2} > {3}" -f $rolloutCoverageGapCount, $maxAllowedGapCount, $rolloutCoverageOrphanCount, $maxAllowedOrphanCount) `
        -RecommendedAction "Align config/rule-rollout.json repos with config/repositories.json and keep observe/enforce metadata complete." `
        -Evidence "scripts/governance/check-rollout-coverage.ps1"
    }
  }
}

# 3) metrics snapshot staleness + waiver expired unrecovered
$metricsPath = Join-Path $kitRoot "docs\governance\metrics-auto.md"
$kv = Parse-KeyValueMap -Path $metricsPath
if ([bool]$policy.triggers.metrics_snapshot_stale.enabled) {
  $maxAge = 8
  if ($null -ne $policy.triggers.metrics_snapshot_stale.max_age_days) {
    $maxAge = [int]$policy.triggers.metrics_snapshot_stale.max_age_days
  }
  $periodDate = Parse-IsoDateOrNull -Text ([string]$kv["period"])
  if ($null -eq $periodDate) {
    Add-Alert -List $alerts `
      -Id "metrics_snapshot_stale" `
      -Severity ([string]$policy.triggers.metrics_snapshot_stale.severity) `
      -Reason "metrics-auto period missing or invalid" `
      -RecommendedAction "Run scripts/collect-governance-metrics.ps1 to refresh metrics snapshot." `
      -Evidence "docs/governance/metrics-auto.md"
  } else {
    $age = [int](New-TimeSpan -Start $periodDate -End $today).TotalDays
    if ($age -gt $maxAge) {
      Add-Alert -List $alerts `
        -Id "metrics_snapshot_stale" `
        -Severity ([string]$policy.triggers.metrics_snapshot_stale.severity) `
        -Reason ("metrics snapshot age={0} days > {1}" -f $age, $maxAge) `
        -RecommendedAction "Run scripts/collect-governance-metrics.ps1 and commit governance metrics template changes if needed." `
        -Evidence "docs/governance/metrics-auto.md"
    }
  }
}

if ([bool]$policy.triggers.waiver_expired_unrecovered.enabled) {
  $expired = 0
  if ($kv.ContainsKey("waiver_expired_unrecovered_count")) {
    [void][int]::TryParse([string]$kv["waiver_expired_unrecovered_count"], [ref]$expired)
  }
  if ($expired -gt 0) {
    Add-Alert -List $alerts `
      -Id "waiver_expired_unrecovered" `
      -Severity ([string]$policy.triggers.waiver_expired_unrecovered.severity) `
      -Reason ("waiver_expired_unrecovered_count={0}" -f $expired) `
      -RecommendedAction "Recover or close expired waivers and rerun scripts/check-waivers.ps1." `
      -Evidence "docs/governance/metrics-auto.md"
  }
}

# 3.5) gate noise budget breach
$gateNoiseBudgetAlertCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['gate_noise_budget_breach'] -and [bool]$policy.triggers.gate_noise_budget_breach.enabled) {
  $alertsSnapshotPath = Join-Path $kitRoot "docs\governance\alerts-latest.md"
  $steps.Add([pscustomobject]@{ name = "gate-noise-budget"; exit_code = 0 }) | Out-Null
  $cfg = $policy.triggers.gate_noise_budget_breach
  $maxFalsePositiveRate = 0.05
  $maxGateLatencyDeltaMs = 5000
  $alertOnDataGap = $false
  if ($null -ne $cfg.PSObject.Properties['max_false_positive_rate']) { $maxFalsePositiveRate = [double]$cfg.max_false_positive_rate }
  if ($null -ne $cfg.PSObject.Properties['max_gate_latency_delta_ms']) { $maxGateLatencyDeltaMs = [int]$cfg.max_gate_latency_delta_ms }
  if ($null -ne $cfg.PSObject.Properties['alert_on_data_gap']) { $alertOnDataGap = [bool]$cfg.alert_on_data_gap }

  $breachReasons = [System.Collections.Generic.List[string]]::new()
  if (-not (Test-Path -LiteralPath $alertsSnapshotPath -PathType Leaf)) {
    if ($alertOnDataGap) {
      $breachReasons.Add("alerts snapshot missing") | Out-Null
    }
  } else {
    $snapshotKv = Parse-KeyValueMap -Path $alertsSnapshotPath
    $falsePositiveRaw = ""
    if ($snapshotKv.ContainsKey("skill_trigger_eval_validation_false_trigger_rate")) {
      $falsePositiveRaw = [string]$snapshotKv["skill_trigger_eval_validation_false_trigger_rate"]
    } elseif ($snapshotKv.ContainsKey("validation_false_trigger_rate")) {
      $falsePositiveRaw = [string]$snapshotKv["validation_false_trigger_rate"]
    }
    $gateLatencyRaw = ""
    if ($snapshotKv.ContainsKey("gate_latency_delta_ms")) {
      $gateLatencyRaw = [string]$snapshotKv["gate_latency_delta_ms"]
    }

    $falsePositiveParsed = $false
    $falsePositiveValue = 0.0
    if (-not [string]::IsNullOrWhiteSpace($falsePositiveRaw) -and -not $falsePositiveRaw.Equals("N/A", [System.StringComparison]::OrdinalIgnoreCase)) {
      $falsePositiveParsed = [double]::TryParse(
        $falsePositiveRaw,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$falsePositiveValue
      )
      if (-not $falsePositiveParsed) {
        $falsePositiveParsed = [double]::TryParse($falsePositiveRaw, [ref]$falsePositiveValue)
      }
      if ($falsePositiveParsed -and $falsePositiveValue -gt $maxFalsePositiveRate) {
        $breachReasons.Add(("false_trigger_rate={0} > {1}" -f $falsePositiveValue, $maxFalsePositiveRate)) | Out-Null
      }
    } elseif ($alertOnDataGap) {
      $breachReasons.Add("false_trigger_rate missing_or_na") | Out-Null
    }

    $gateLatencyParsed = $false
    $gateLatencyValue = 0
    if (-not [string]::IsNullOrWhiteSpace($gateLatencyRaw) -and -not $gateLatencyRaw.Equals("N/A", [System.StringComparison]::OrdinalIgnoreCase)) {
      $gateLatencyParsed = [int]::TryParse($gateLatencyRaw, [ref]$gateLatencyValue)
      if ($gateLatencyParsed -and $gateLatencyValue -gt $maxGateLatencyDeltaMs) {
        $breachReasons.Add(("gate_latency_delta_ms={0} > {1}" -f $gateLatencyValue, $maxGateLatencyDeltaMs)) | Out-Null
      }
    } elseif ($alertOnDataGap) {
      $breachReasons.Add("gate_latency_delta_ms missing_or_na") | Out-Null
    }
  }

  if ($breachReasons.Count -gt 0) {
    $gateNoiseBudgetAlertCount = 1
    Add-Alert -List $alerts `
      -Id "gate_noise_budget_breach" `
      -Severity ([string]$policy.triggers.gate_noise_budget_breach.severity) `
      -Reason ([string]::Join("; ", @($breachReasons))) `
      -RecommendedAction "Tune noisy checks to advisory, reduce false positives, and keep gate latency delta within policy threshold." `
      -Evidence "docs/governance/alerts-latest.md"
  }
}

# 3.6) control registry signals
$staleProgressiveControlCount = 0
$notObservableControlCount = 0
$controlRegistryPath = Join-Path $kitRoot "config\governance-control-registry.json"
$controlRegistry = $null
if (Test-Path -LiteralPath $controlRegistryPath -PathType Leaf) {
  try {
    $controlRegistry = Read-JsonFile -Path $controlRegistryPath -DisplayName "governance-control-registry.json"
  } catch {
    $controlRegistry = $null
  }
}
if ($null -ne $controlRegistry -and $null -ne $controlRegistry.controls) {
  foreach ($control in @($controlRegistry.controls)) {
    $inventoryStatus = ""
    if ($null -ne $control.PSObject.Properties['inventory_status']) {
      $inventoryStatus = ([string]$control.inventory_status).Trim()
    }
    if ($inventoryStatus -eq "not_observable_candidate") {
      $notObservableControlCount++
      continue
    }
    if ($inventoryStatus -eq "stale_candidate") {
      $staleProgressiveControlCount++
      continue
    }
    if ($inventoryStatus -eq "too_loose_candidate") {
      $controlClass = ""
      if ($null -ne $control.PSObject.Properties['class']) {
        $controlClass = ([string]$control.class).Trim()
      }
      if ($controlClass -eq "progressive") {
        $staleProgressiveControlCount++
      }
    }
  }
}

if ($null -ne $policy.triggers.PSObject.Properties['stale_progressive_controls_present'] -and [bool]$policy.triggers.stale_progressive_controls_present.enabled) {
  $maxAllowedCount = 0
  if ($null -ne $policy.triggers.stale_progressive_controls_present.PSObject.Properties['max_allowed_count']) {
    $maxAllowedCount = [int]$policy.triggers.stale_progressive_controls_present.max_allowed_count
  }
  $steps.Add([pscustomobject]@{ name = "stale-progressive-controls"; exit_code = 0 }) | Out-Null
  if ($staleProgressiveControlCount -gt $maxAllowedCount) {
    Add-Alert -List $alerts `
      -Id "stale_progressive_controls_present" `
      -Severity ([string]$policy.triggers.stale_progressive_controls_present.severity) `
      -Reason ("stale progressive control count={0} > {1}" -f $staleProgressiveControlCount, $maxAllowedCount) `
      -RecommendedAction "Review stale or too-loose progressive controls and either mature, downgrade, or retire them." `
      -Evidence "config/governance-control-registry.json"
  }
}

if ($null -ne $policy.triggers.PSObject.Properties['not_observable_controls_present'] -and [bool]$policy.triggers.not_observable_controls_present.enabled) {
  $maxAllowedCount = 0
  if ($null -ne $policy.triggers.not_observable_controls_present.PSObject.Properties['max_allowed_count']) {
    $maxAllowedCount = [int]$policy.triggers.not_observable_controls_present.max_allowed_count
  }
  $steps.Add([pscustomobject]@{ name = "not-observable-controls"; exit_code = 0 }) | Out-Null
  if ($notObservableControlCount -gt $maxAllowedCount) {
    Add-Alert -List $alerts `
      -Id "not_observable_controls_present" `
      -Severity ([string]$policy.triggers.not_observable_controls_present.severity) `
      -Reason ("not observable control count={0} > {1}" -f $notObservableControlCount, $maxAllowedCount) `
      -RecommendedAction "Add direct metrics or recurring review fields before promoting these controls." `
      -Evidence "config/governance-control-registry.json"
  }
}

# 3.7) rule duplication trigger
$ruleDuplicationCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['rule_duplication_detected'] -and [bool]$policy.triggers.rule_duplication_detected.enabled) {
  $ruleDupScript = Join-Path $kitRoot "scripts\governance\check-rule-duplication.ps1"
  if (Test-Path -LiteralPath $ruleDupScript -PathType Leaf) {
    $ruleDupOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $ruleDupScript -RepoRoot $repoPath -AsJson 2>&1
    $ruleDupExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name = "rule-duplication-scan"; exit_code = [int]$ruleDupExit }) | Out-Null
    $ruleDupObj = $null
    $ruleDupText = [string]::Join([Environment]::NewLine, @($ruleDupOut))
    if (-not [string]::IsNullOrWhiteSpace($ruleDupText)) {
      try { $ruleDupObj = $ruleDupText | ConvertFrom-Json } catch { $ruleDupObj = $null }
    }
    if ($null -ne $ruleDupObj -and $ruleDupObj.PSObject.Properties.Name -contains "issue_count") {
      $ruleDuplicationCount = [int]$ruleDupObj.issue_count
    }
    if ($ruleDuplicationCount -gt 0) {
      Add-Alert -List $alerts `
        -Id "rule_duplication_detected" `
        -Severity ([string]$policy.triggers.rule_duplication_detected.severity) `
        -Reason ("rule_duplication_issue_count={0}" -f $ruleDuplicationCount) `
        -RecommendedAction "Run scripts/governance/check-rule-duplication.ps1, deduplicate repeated blocks, and keep long details in docs instead of top rule files." `
        -Evidence "scripts/governance/check-rule-duplication.ps1"
    }
  }
}

# 3.8) control retirement backlog
$controlRetirementActiveCandidateCount = 0
$controlRetirementOverdueCandidateCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['control_retirement_backlog'] -and [bool]$policy.triggers.control_retirement_backlog.enabled) {
  $retirementScript = Join-Path $kitRoot "scripts\governance\check-control-retirement-candidates.ps1"
  if (Test-Path -LiteralPath $retirementScript -PathType Leaf) {
    $retirementOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $retirementScript -RepoRoot $repoPath -AsJson 2>&1
    $retirementExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name = "control-retirement-backlog"; exit_code = [int]$retirementExit }) | Out-Null
    $retirementText = [string]::Join([Environment]::NewLine, @($retirementOut))
    $retirementObj = $null
    if (-not [string]::IsNullOrWhiteSpace($retirementText)) {
      try { $retirementObj = $retirementText | ConvertFrom-Json } catch { $retirementObj = $null }
    }
    if ($null -ne $retirementObj) {
      if ($retirementObj.PSObject.Properties.Name -contains "active_candidate_count") {
        $controlRetirementActiveCandidateCount = [int]$retirementObj.active_candidate_count
      }
      if ($retirementObj.PSObject.Properties.Name -contains "overdue_candidate_count") {
        $controlRetirementOverdueCandidateCount = [int]$retirementObj.overdue_candidate_count
      }
    }

    $maxActiveCandidateCount = 8
    $maxOverdueCandidateCount = 0
    if ($null -ne $policy.triggers.control_retirement_backlog.PSObject.Properties['max_active_candidate_count']) {
      $maxActiveCandidateCount = [int]$policy.triggers.control_retirement_backlog.max_active_candidate_count
    }
    if ($null -ne $policy.triggers.control_retirement_backlog.PSObject.Properties['max_overdue_candidate_count']) {
      $maxOverdueCandidateCount = [int]$policy.triggers.control_retirement_backlog.max_overdue_candidate_count
    }
    if ($controlRetirementActiveCandidateCount -gt $maxActiveCandidateCount -or $controlRetirementOverdueCandidateCount -gt $maxOverdueCandidateCount) {
      Add-Alert -List $alerts `
        -Id "control_retirement_backlog" `
        -Severity ([string]$policy.triggers.control_retirement_backlog.severity) `
        -Reason ("active_candidate_count={0} > {1} or overdue_candidate_count={2} > {3}" -f $controlRetirementActiveCandidateCount, $maxActiveCandidateCount, $controlRetirementOverdueCandidateCount, $maxOverdueCandidateCount) `
        -RecommendedAction "Close or downgrade retirement candidates and keep decision_due_date within cadence." `
        -Evidence "config/control-retirement-candidates.json"
    }
  }
}

# 3.9) evidence template fields
$evidenceTemplateMissingFieldCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['evidence_template_fields_missing'] -and [bool]$policy.triggers.evidence_template_fields_missing.enabled) {
  $evidenceTemplateScript = Join-Path $kitRoot "scripts\governance\check-evidence-template-fields.ps1"
  if (Test-Path -LiteralPath $evidenceTemplateScript -PathType Leaf) {
    $evidenceOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $evidenceTemplateScript -RepoRoot $repoPath -AsJson 2>&1
    $evidenceExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name = "evidence-template-fields"; exit_code = [int]$evidenceExit }) | Out-Null
    $evidenceText = [string]::Join([Environment]::NewLine, @($evidenceOut))
    $evidenceObj = $null
    if (-not [string]::IsNullOrWhiteSpace($evidenceText)) {
      try { $evidenceObj = $evidenceText | ConvertFrom-Json } catch { $evidenceObj = $null }
    }
    if ($null -ne $evidenceObj -and $evidenceObj.PSObject.Properties.Name -contains "missing_field_count") {
      $evidenceTemplateMissingFieldCount = [int]$evidenceObj.missing_field_count
    }
    if ($evidenceTemplateMissingFieldCount -gt 0) {
      Add-Alert -List $alerts `
        -Id "evidence_template_fields_missing" `
        -Severity ([string]$policy.triggers.evidence_template_fields_missing.severity) `
        -Reason ("missing_field_count={0}" -f $evidenceTemplateMissingFieldCount) `
        -RecommendedAction "Backfill required Phase2 evidence fields in docs/change-evidence/template.md and regenerate dependent evidence." `
        -Evidence "scripts/governance/check-evidence-template-fields.ps1"
    }
  }
}

# 3.10) target rollout matrix gap
$targetRolloutMatrixMissingControlCount = 0
$targetRolloutMatrixMissingRepoStateCount = 0
$crossRepoFeedbackSnapshotStaleCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['target_rollout_matrix_gap'] -and [bool]$policy.triggers.target_rollout_matrix_gap.enabled) {
  $targetRolloutScript = Join-Path $kitRoot "scripts\governance\check-target-rollout-matrix.ps1"
  if (Test-Path -LiteralPath $targetRolloutScript -PathType Leaf) {
    $targetRolloutOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $targetRolloutScript -RepoRoot $repoPath -AsJson 2>&1
    $targetRolloutExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name = "target-rollout-matrix-gap"; exit_code = [int]$targetRolloutExit }) | Out-Null
    $targetRolloutText = [string]::Join([Environment]::NewLine, @($targetRolloutOut))
    $targetRolloutObj = $null
    if (-not [string]::IsNullOrWhiteSpace($targetRolloutText)) {
      try { $targetRolloutObj = $targetRolloutText | ConvertFrom-Json } catch { $targetRolloutObj = $null }
    }
    if ($null -ne $targetRolloutObj) {
      if ($targetRolloutObj.PSObject.Properties.Name -contains "missing_control_count") {
        $targetRolloutMatrixMissingControlCount = [int]$targetRolloutObj.missing_control_count
      }
      if ($targetRolloutObj.PSObject.Properties.Name -contains "missing_repo_state_count") {
        $targetRolloutMatrixMissingRepoStateCount = [int]$targetRolloutObj.missing_repo_state_count
      }
    }

    $maxMissingControlCount = 0
    $maxMissingRepoStateCount = 0
    if ($null -ne $policy.triggers.target_rollout_matrix_gap.PSObject.Properties['max_allowed_missing_control_count']) {
      $maxMissingControlCount = [int]$policy.triggers.target_rollout_matrix_gap.max_allowed_missing_control_count
    }
    if ($null -ne $policy.triggers.target_rollout_matrix_gap.PSObject.Properties['max_allowed_missing_repo_state_count']) {
      $maxMissingRepoStateCount = [int]$policy.triggers.target_rollout_matrix_gap.max_allowed_missing_repo_state_count
    }
    if ($targetRolloutMatrixMissingControlCount -gt $maxMissingControlCount -or $targetRolloutMatrixMissingRepoStateCount -gt $maxMissingRepoStateCount) {
      Add-Alert -List $alerts `
        -Id "target_rollout_matrix_gap" `
        -Severity ([string]$policy.triggers.target_rollout_matrix_gap.severity) `
        -Reason ("missing_control_count={0} > {1} or missing_repo_state_count={2} > {3}" -f $targetRolloutMatrixMissingControlCount, $maxMissingControlCount, $targetRolloutMatrixMissingRepoStateCount, $maxMissingRepoStateCount) `
        -RecommendedAction "Backfill config/target-control-rollout-matrix.json for all distributable progressive controls and target repos." `
        -Evidence "scripts/governance/check-target-rollout-matrix.ps1"
    }
  }
}

# 3.11) cross-repo feedback snapshot stale
if ($null -ne $policy.triggers.PSObject.Properties['cross_repo_feedback_snapshot_stale'] -and [bool]$policy.triggers.cross_repo_feedback_snapshot_stale.enabled) {
  $steps.Add([pscustomobject]@{ name = "cross-repo-feedback-snapshot-stale"; exit_code = 0 }) | Out-Null
  $cfg = $policy.triggers.cross_repo_feedback_snapshot_stale
  $maxAgeDays = 8
  $minFeedbackIngestedCount = 1
  if ($null -ne $cfg.PSObject.Properties['max_age_days']) { $maxAgeDays = [int]$cfg.max_age_days }
  if ($null -ne $cfg.PSObject.Properties['min_feedback_ingested_count']) { $minFeedbackIngestedCount = [int]$cfg.min_feedback_ingested_count }

  $feedbackReportPath = Join-Path $kitRoot "docs\governance\cross-repo-feedback-report-latest.md"
  $feedbackReasons = [System.Collections.Generic.List[string]]::new()
  if (-not (Test-Path -LiteralPath $feedbackReportPath -PathType Leaf)) {
    $feedbackReasons.Add("feedback report missing") | Out-Null
  } else {
    $feedbackKv = Parse-KeyValueMap -Path $feedbackReportPath
    $generatedAtText = ""
    if ($feedbackKv.ContainsKey("generated_at")) { $generatedAtText = [string]$feedbackKv["generated_at"] }
    $generatedAt = [datetime]::MinValue
    $hasGeneratedAt = [datetime]::TryParse($generatedAtText, [ref]$generatedAt)
    if (-not $hasGeneratedAt) {
      $feedbackReasons.Add("generated_at missing_or_invalid") | Out-Null
    } else {
      $ageDays = [int](New-TimeSpan -Start $generatedAt -End (Get-Date)).TotalDays
      if ($ageDays -gt $maxAgeDays) {
        $feedbackReasons.Add(("snapshot age={0} days > {1}" -f $ageDays, $maxAgeDays)) | Out-Null
      }
    }
    $ingestedCount = 0
    $hasIngested = $false
    if ($feedbackKv.ContainsKey("feedback_ingested_count")) {
      $hasIngested = [int]::TryParse([string]$feedbackKv["feedback_ingested_count"], [ref]$ingestedCount)
    }
    if (-not $hasIngested) {
      $feedbackReasons.Add("feedback_ingested_count missing_or_invalid") | Out-Null
    } elseif ($ingestedCount -lt $minFeedbackIngestedCount) {
      $feedbackReasons.Add(("feedback_ingested_count={0} < {1}" -f $ingestedCount, $minFeedbackIngestedCount)) | Out-Null
    }
  }

  if ($feedbackReasons.Count -gt 0) {
    $crossRepoFeedbackSnapshotStaleCount = 1
    Add-Alert -List $alerts `
      -Id "cross_repo_feedback_snapshot_stale" `
      -Severity ([string]$policy.triggers.cross_repo_feedback_snapshot_stale.severity) `
      -Reason ([string]::Join("; ", @($feedbackReasons))) `
      -RecommendedAction "Regenerate cross-repo feedback report and verify cross-repo compatibility inputs." `
      -Evidence "docs/governance/cross-repo-feedback-report-latest.md"
  }
}

# 4) platform_na expired
if ([bool]$policy.triggers.platform_na_expired.enabled) {
  $evidenceDir = Join-Path $kitRoot "docs\change-evidence"
  $expiredCount = 0
  if (Test-Path -LiteralPath $evidenceDir -PathType Container) {
    $files = @(Get-ChildItem -LiteralPath $evidenceDir -File -Filter *.md | Sort-Object LastWriteTime -Descending | Select-Object -First 60)
    foreach ($f in $files) {
      $lines = @(Get-Content -LiteralPath $f.FullName)
      foreach ($line in $lines) {
        $m = [regex]::Match([string]$line, "platform_na\.expires_at\s*=\s*([0-9]{4}-[0-9]{2}-[0-9]{2})")
        if ($m.Success) {
          $d = Parse-IsoDateOrNull -Text $m.Groups[1].Value
          if ($null -ne $d -and $d -lt $today) {
            $expiredCount++
          }
        }
      }
    }
  }
  $steps.Add([pscustomobject]@{ name = "platform-na-expiry-scan"; exit_code = 0 }) | Out-Null
  if ($expiredCount -gt 0) {
    Add-Alert -List $alerts `
      -Id "platform_na_expired" `
      -Severity ([string]$policy.triggers.platform_na_expired.severity) `
      -Reason ("expired platform_na entries={0}" -f $expiredCount) `
      -RecommendedAction "Re-verify platform capabilities and refresh platform_na evidence with new expires_at." `
      -Evidence "docs/change-evidence/*.md"
  }
}

# 5) release-distribution-policy drift
$releasePolicyDriftCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['release_distribution_policy_drift'] -and [bool]$policy.triggers.release_distribution_policy_drift.enabled) {
  $releasePolicyPath = Join-Path $kitRoot "config\release-distribution-policy.json"
  $repositoriesPath = Join-Path $kitRoot "config\repositories.json"
  $releaseProfileRoot = Join-Path $kitRoot "source\project"
  $steps.Add([pscustomobject]@{ name = "release-distribution-policy-drift"; exit_code = 0 }) | Out-Null

  if ((Test-Path -LiteralPath $releasePolicyPath -PathType Leaf) -and (Test-Path -LiteralPath $repositoriesPath -PathType Leaf) -and (Test-Path -LiteralPath $releaseProfileRoot -PathType Container)) {
    $rdPolicy = $null
    $reposList = @()
    try {
      $rdPolicy = Read-JsonFile -Path $releasePolicyPath -DisplayName $releasePolicyPath
      $reposList = @(Read-JsonArray $repositoriesPath)
    } catch {
      $rdPolicy = $null
      $reposList = @()
    }

    if ($null -ne $rdPolicy -and $null -ne $rdPolicy.default -and $rdPolicy.repos -is [System.Array]) {
      foreach ($rp in @($rdPolicy.repos)) {
        if ($null -eq $rp -or [string]::IsNullOrWhiteSpace([string]$rp.repoName)) { continue }
        $rpName = [string]$rp.repoName
        $repoPath = $null
        foreach ($repoEntry in $reposList) {
          $repoText = [string]$repoEntry
          if ((Split-Path -Leaf $repoText).Equals($rpName, [System.StringComparison]::OrdinalIgnoreCase)) {
            $repoPath = $repoText
            break
          }
        }
        if ([string]::IsNullOrWhiteSpace($repoPath) -or -not (Test-Path -LiteralPath $repoPath)) { continue }

        $profilePath = Join-Path $releaseProfileRoot "$rpName\custom\.governance\release-profile.json"
        if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) { continue }
        $profile = $null
        try {
          $profile = Read-JsonFile -Path $profilePath -DisplayName $profilePath
        } catch {
          $releasePolicyDriftCount++
          continue
        }
        if ($null -eq $profile -or $null -eq $profile.policies -or $null -eq $profile.policies.signing -or $null -eq $profile.policies.packaging) {
          $releasePolicyDriftCount++
          continue
        }

        $signingDefault = $rdPolicy.default.signing
        $packagingDefault = $rdPolicy.default.packaging
        $signingRepo = if ($null -ne $rp.PSObject.Properties['signing']) { $rp.signing } else { $null }
        $packagingRepo = if ($null -ne $rp.PSObject.Properties['packaging']) { $rp.packaging } else { $null }

        $expectedSigningRequired = if ($null -ne $signingRepo -and $signingRepo.PSObject.Properties['required']) { [bool]$signingRepo.required } else { [bool]$signingDefault.required }
        $expectedSigningMode = if ($null -ne $signingRepo -and $signingRepo.PSObject.Properties['mode']) { [string]$signingRepo.mode } else { [string]$signingDefault.mode }
        $expectedAllowPaid = if ($null -ne $signingRepo -and $signingRepo.PSObject.Properties['allow_paid_signing']) { [bool]$signingRepo.allow_paid_signing } else { [bool]$signingDefault.allow_paid_signing }
        $expectedDefaultChannel = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['default_channel']) { [string]$packagingRepo.default_channel } else { [string]$packagingDefault.default_channel }
        $expectedRequireFramework = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['require_framework_dependent']) { [bool]$packagingRepo.require_framework_dependent } else { [bool]$packagingDefault.require_framework_dependent }
        $expectedRequireSelfContained = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['require_self_contained']) { [bool]$packagingRepo.require_self_contained } else { [bool]$packagingDefault.require_self_contained }
        $expectedChannels = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['channels']) { @($packagingRepo.channels) } else { @($packagingDefault.channels) }
        $expectedForms = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['distribution_forms']) { @($packagingRepo.distribution_forms) } else { @($packagingDefault.distribution_forms) }
        $expectedModes = if ($null -ne $packagingRepo -and $packagingRepo.PSObject.Properties['network_modes']) { @($packagingRepo.network_modes) } else { @($packagingDefault.network_modes) }

        $profileSigning = $profile.policies.signing
        $profilePackaging = $profile.policies.packaging
        $drifted = $false
        if ([bool]$profileSigning.required -ne $expectedSigningRequired) { $drifted = $true }
        if ([string]$profileSigning.mode -ne $expectedSigningMode) { $drifted = $true }
        if ([bool]$profileSigning.allow_paid_signing -ne $expectedAllowPaid) { $drifted = $true }
        if ([string]$profilePackaging.default_channel -ne $expectedDefaultChannel) { $drifted = $true }
        if ([bool]$profilePackaging.require_framework_dependent -ne $expectedRequireFramework) { $drifted = $true }
        if ([bool]$profilePackaging.require_self_contained -ne $expectedRequireSelfContained) { $drifted = $true }
        if (-not (Test-StringArraySetEqual -A $profilePackaging.channels -B $expectedChannels)) { $drifted = $true }
        if (-not (Test-StringArraySetEqual -A $profilePackaging.distribution_forms -B $expectedForms)) { $drifted = $true }
        if (-not (Test-StringArraySetEqual -A $profilePackaging.network_modes -B $expectedModes)) { $drifted = $true }

        if ($drifted) {
          $releasePolicyDriftCount++
        }
      }
    }
  }

  if ($releasePolicyDriftCount -gt 0) {
    Add-Alert -List $alerts `
      -Id "release_distribution_policy_drift" `
      -Severity ([string]$policy.triggers.release_distribution_policy_drift.severity) `
      -Reason ("release_distribution_policy_drift_count={0}" -f $releasePolicyDriftCount) `
      -RecommendedAction "Run scripts/suggest-release-profile.ps1 for drifted repos and write back to source/project/<Repo>/custom/.governance/release-profile.json, then rerun gates." `
      -Evidence "config/release-distribution-policy.json + source/project/*/custom/.governance/release-profile.json"
  }
}

# 6) skill trigger eval summary staleness
$skillTriggerEvalAlertCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['skill_trigger_eval_summary_stale'] -and [bool]$policy.triggers.skill_trigger_eval_summary_stale.enabled) {
  $promotionPolicyPath = Join-Path $kitRoot ".governance\skill-promotion-policy.json"
  $summaryRel = ".governance/skill-candidates/trigger-eval-summary.json"
  $requireEval = $false
  if (Test-Path -LiteralPath $promotionPolicyPath -PathType Leaf) {
    try {
      $promotionPolicy = Read-JsonFile -Path $promotionPolicyPath -DisplayName $promotionPolicyPath
      if ($null -ne $promotionPolicy.PSObject.Properties['require_trigger_eval_for_create']) {
        $requireEval = [bool]$promotionPolicy.require_trigger_eval_for_create
      }
      if ($null -ne $promotionPolicy.PSObject.Properties['trigger_eval_summary_relative_path'] -and -not [string]::IsNullOrWhiteSpace([string]$promotionPolicy.trigger_eval_summary_relative_path)) {
        $summaryRel = [string]$promotionPolicy.trigger_eval_summary_relative_path
      }
    } catch {
      $requireEval = $false
    }
  }

  if ($requireEval) {
    $maxAge = 8
    if ($null -ne $policy.triggers.skill_trigger_eval_summary_stale.PSObject.Properties['max_age_days']) {
      $maxAge = [int]$policy.triggers.skill_trigger_eval_summary_stale.max_age_days
    }
    $summaryPath = Join-Path $kitRoot ($summaryRel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
      $skillTriggerEvalAlertCount++
      Add-Alert -List $alerts `
        -Id "skill_trigger_eval_summary_stale" `
        -Severity ([string]$policy.triggers.skill_trigger_eval_summary_stale.severity) `
        -Reason "trigger eval summary missing while create requires trigger eval" `
        -RecommendedAction "Run scripts/governance/check-skill-trigger-evals.ps1 to generate summary before promote-skill-candidates create." `
        -Evidence ($summaryPath -replace '\\', '/')
    } else {
      $summaryObj = $null
      try { $summaryObj = Read-JsonFile -Path $summaryPath -DisplayName $summaryPath } catch { $summaryObj = $null }
      if ($null -eq $summaryObj) {
        $skillTriggerEvalAlertCount++
        Add-Alert -List $alerts `
          -Id "skill_trigger_eval_summary_stale" `
          -Severity ([string]$policy.triggers.skill_trigger_eval_summary_stale.severity) `
          -Reason "trigger eval summary parse error" `
          -RecommendedAction "Regenerate trigger eval summary using check-skill-trigger-evals and re-run update triggers." `
          -Evidence ($summaryPath -replace '\\', '/')
      } else {
        $generatedAt = $null
        if ($null -ne $summaryObj.PSObject.Properties['generated_at']) {
          try { $generatedAt = [datetime]$summaryObj.generated_at } catch { $generatedAt = $null }
        }
        $status = ""
        if ($null -ne $summaryObj.PSObject.Properties['status']) { $status = [string]$summaryObj.status }
        $validationCount = 0
        if ($null -ne $summaryObj.PSObject.Properties['validation_query_count']) {
          try { $validationCount = [int]$summaryObj.validation_query_count } catch { $validationCount = 0 }
        }

        $isStale = $false
        if ($null -eq $generatedAt) {
          $isStale = $true
        } else {
          $ageDays = [int](New-TimeSpan -Start $generatedAt -End (Get-Date)).TotalDays
          if ($ageDays -gt $maxAge) { $isStale = $true }
        }
        if ($status -in @("no_data", "no_validation_split")) { $isStale = $true }
        if ($validationCount -le 0) { $isStale = $true }

        if ($isStale) {
          $skillTriggerEvalAlertCount++
          Add-Alert -List $alerts `
            -Id "skill_trigger_eval_summary_stale" `
            -Severity ([string]$policy.triggers.skill_trigger_eval_summary_stale.severity) `
            -Reason ("trigger eval summary stale_or_insufficient status={0} validation_query_count={1}" -f $status, $validationCount) `
            -RecommendedAction "Collect trigger eval runs and regenerate summary before create promotion." `
            -Evidence ($summaryPath -replace '\\', '/')
        }
      }
    }
  }
}

# 7) low-value orphan custom source trigger
$orphanCustomCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['low_value_orphan_custom_sources'] -and [bool]$policy.triggers.low_value_orphan_custom_sources.enabled) {
  $orphanScript = Join-Path $kitRoot "scripts\check-orphan-custom-sources.ps1"
  if (Test-Path -LiteralPath $orphanScript -PathType Leaf) {
    $orphanOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $orphanScript -AsJson 2>&1
    $orphanExit = $LASTEXITCODE
    $steps.Add([pscustomobject]@{ name = "orphan-custom-source-scan"; exit_code = [int]$orphanExit }) | Out-Null
    $orphanObj = $null
    $orphanText = [string]::Join([Environment]::NewLine, @($orphanOut))
    if (-not [string]::IsNullOrWhiteSpace($orphanText)) {
      try { $orphanObj = $orphanText | ConvertFrom-Json } catch { $orphanObj = $null }
    }
    if ($null -ne $orphanObj -and $orphanObj.PSObject.Properties.Name -contains "orphan_count") {
      $orphanCustomCount = [int]$orphanObj.orphan_count
    }
    if ($orphanCustomCount -gt 0) {
      Add-Alert -List $alerts `
        -Id "low_value_orphan_custom_sources" `
        -Severity ([string]$policy.triggers.low_value_orphan_custom_sources.severity) `
        -Reason ("orphan_custom_source_count={0}" -f $orphanCustomCount) `
        -RecommendedAction "Run scripts/check-orphan-custom-sources.ps1 and prune or map orphan custom files." `
        -Evidence "scripts/check-orphan-custom-sources.ps1"
    }
  }
}

# 8) dependency-review enforce drift
$dependencyReviewPolicyDriftCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['dependency_review_policy_drift'] -and [bool]$policy.triggers.dependency_review_policy_drift.enabled) {
  $workflowPath = Join-Path $kitRoot ".github\workflows\dependency-review.yml"
  $steps.Add([pscustomobject]@{ name = "dependency-review-policy-drift"; exit_code = 0 }) | Out-Null
  $requiredSeverity = "high"
  $requiredActionMajor = "v4"
  if ($null -ne $policy.triggers.dependency_review_policy_drift.PSObject.Properties['required_fail_on_severity']) {
    $requiredSeverity = ([string]$policy.triggers.dependency_review_policy_drift.required_fail_on_severity).Trim().ToLowerInvariant()
  }
  if ($null -ne $policy.triggers.dependency_review_policy_drift.PSObject.Properties['required_action_major']) {
    $requiredActionMajor = ([string]$policy.triggers.dependency_review_policy_drift.required_action_major).Trim().ToLowerInvariant()
  }

  if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
    $dependencyReviewPolicyDriftCount = 1
    Add-Alert -List $alerts `
      -Id "dependency_review_policy_drift" `
      -Severity ([string]$policy.triggers.dependency_review_policy_drift.severity) `
      -Reason "dependency-review workflow missing" `
      -RecommendedAction "Restore .github/workflows/dependency-review.yml from governance baseline and rerun verify." `
      -Evidence ".github/workflows/dependency-review.yml"
  } else {
    $workflowText = Get-Content -LiteralPath $workflowPath -Raw
    $missingReasons = [System.Collections.Generic.List[string]]::new()
    $severityPattern = "(?im)^\s*fail-on-severity\s*:\s*" + [regex]::Escape($requiredSeverity) + "\s*$"
    if (-not [regex]::IsMatch($workflowText, $severityPattern)) {
      $missingReasons.Add(("fail-on-severity != {0}" -f $requiredSeverity)) | Out-Null
    }
    $actionPattern = "(?im)^\s*uses\s*:\s*actions/dependency-review-action@" + [regex]::Escape($requiredActionMajor) + "\s*$"
    if (-not [regex]::IsMatch($workflowText, $actionPattern)) {
      $missingReasons.Add(("dependency-review action major != {0}" -f $requiredActionMajor)) | Out-Null
    }
    if ($missingReasons.Count -gt 0) {
      $dependencyReviewPolicyDriftCount = 1
      Add-Alert -List $alerts `
        -Id "dependency_review_policy_drift" `
        -Severity ([string]$policy.triggers.dependency_review_policy_drift.severity) `
        -Reason ([string]::Join("; ", @($missingReasons))) `
        -RecommendedAction "Align dependency-review workflow to enforce policy (action major + fail-on-severity) and rerun verify." `
        -Evidence ".github/workflows/dependency-review.yml"
    }
  }
}

# 9) slsa provenance placeholder detection
$slsaProvenancePlaceholderCount = 0
if ($null -ne $policy.triggers.PSObject.Properties['slsa_provenance_placeholder'] -and [bool]$policy.triggers.slsa_provenance_placeholder.enabled) {
  $slsaWorkflowPath = Join-Path $kitRoot ".github\workflows\slsa.yml"
  $steps.Add([pscustomobject]@{ name = "slsa-provenance-placeholder"; exit_code = 0 }) | Out-Null
  if (-not (Test-Path -LiteralPath $slsaWorkflowPath -PathType Leaf)) {
    $slsaProvenancePlaceholderCount = 1
    Add-Alert -List $alerts `
      -Id "slsa_provenance_placeholder" `
      -Severity ([string]$policy.triggers.slsa_provenance_placeholder.severity) `
      -Reason "slsa workflow missing" `
      -RecommendedAction "Restore .github/workflows/slsa.yml from governance baseline." `
      -Evidence ".github/workflows/slsa.yml"
  } else {
    $slsaText = Get-Content -LiteralPath $slsaWorkflowPath -Raw
    $placeholderHit = [regex]::IsMatch($slsaText, "(?im)placeholder|provenance-note")
    $generatorHit = [regex]::IsMatch($slsaText, "(?im)slsa-framework/slsa-github-generator")
    $idTokenHit = [regex]::IsMatch($slsaText, "(?im)id-token\s*:\s*write")
    if ($placeholderHit -or -not $generatorHit -or -not $idTokenHit) {
      $slsaProvenancePlaceholderCount = 1
      $reasonParts = [System.Collections.Generic.List[string]]::new()
      if ($placeholderHit) { $reasonParts.Add("placeholder_signature_detected") | Out-Null }
      if (-not $generatorHit) { $reasonParts.Add("generator_workflow_missing") | Out-Null }
      if (-not $idTokenHit) { $reasonParts.Add("id-token:write missing") | Out-Null }
      Add-Alert -List $alerts `
        -Id "slsa_provenance_placeholder" `
        -Severity ([string]$policy.triggers.slsa_provenance_placeholder.severity) `
        -Reason ([string]::Join("; ", @($reasonParts))) `
        -RecommendedAction "Switch slsa workflow to generator-based attestation pipeline and keep id-token: write permission." `
        -Evidence ".github/workflows/slsa.yml"
    }
  }
}

$result = [pscustomobject]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  repo_root = ($repoPath -replace '\\', '/')
  status = if ($alerts.Count -eq 0) { "OK" } else { "ALERT" }
  alert_count = $alerts.Count
  skill_trigger_eval_alert_count = [int]$skillTriggerEvalAlertCount
  gate_noise_budget_alert_count = [int]$gateNoiseBudgetAlertCount
  stale_progressive_control_count = [int]$staleProgressiveControlCount
  not_observable_control_count = [int]$notObservableControlCount
  rule_duplication_count = [int]$ruleDuplicationCount
  control_retirement_active_candidate_count = [int]$controlRetirementActiveCandidateCount
  control_retirement_overdue_candidate_count = [int]$controlRetirementOverdueCandidateCount
  evidence_template_missing_field_count = [int]$evidenceTemplateMissingFieldCount
  target_rollout_matrix_missing_control_count = [int]$targetRolloutMatrixMissingControlCount
  target_rollout_matrix_missing_repo_state_count = [int]$targetRolloutMatrixMissingRepoStateCount
  cross_repo_feedback_snapshot_stale_count = [int]$crossRepoFeedbackSnapshotStaleCount
  rollout_metadata_coverage_gap_count = [int]$rolloutCoverageGapCount
  rollout_metadata_orphan_count = [int]$rolloutCoverageOrphanCount
  orphan_custom_source_count = [int]$orphanCustomCount
  release_distribution_policy_drift_count = [int]$releasePolicyDriftCount
  dependency_review_policy_drift_count = [int]$dependencyReviewPolicyDriftCount
  slsa_provenance_placeholder_count = [int]$slsaProvenancePlaceholderCount
  alerts = @($alerts)
  steps = @($steps)
  policy_path = ($policyPath -replace '\\', '/')
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($alerts.Count -eq 0) { exit 0 } else { exit 1 }
}

Write-Host "UPDATE_TRIGGER_CHECK"
Write-Host ("status={0}" -f $result.status)
Write-Host ("alert_count={0}" -f $result.alert_count)
Write-Host ("policy_path={0}" -f $result.policy_path)
if ($alerts.Count -eq 0) {
  Write-Host "alerts=none"
  exit 0
}
foreach ($a in $alerts) {
  Write-Host ("[ALERT] id={0} severity={1} reason={2}" -f $a.id, $a.severity, $a.reason)
}
exit 1
