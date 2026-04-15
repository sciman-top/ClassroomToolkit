param(
  [string]$RepoRoot = ".",
  [switch]$NoNotifyOnAlert,
  [switch]$NoAlertSnapshot,
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$kitRoot = $repoPath
$notifyOnAlert = -not $NoNotifyOnAlert.IsPresent
$writeAlertSnapshot = -not $NoAlertSnapshot.IsPresent
$commonPath = Join-Path $kitRoot "scripts\lib\common.ps1"
if (-not (Test-Path -LiteralPath $commonPath -PathType Leaf)) {
  throw "Missing common helper: $commonPath"
}
. $commonPath

$doctorScript = Join-Path $kitRoot "scripts\doctor.ps1"
$rolloutScript = Join-Path $kitRoot "scripts\rollout-status.ps1"
$waiverScript = Join-Path $kitRoot "scripts\check-waivers.ps1"
$metricsScript = Join-Path $kitRoot "scripts\collect-governance-metrics.ps1"
$triggerScript = Join-Path $kitRoot "scripts\governance\check-update-triggers.ps1"
$skillTriggerEvalScript = Join-Path $kitRoot "scripts\governance\check-skill-trigger-evals.ps1"
$riskTierApprovalScript = Join-Path $kitRoot "scripts\governance\check-risk-tier-approval.ps1"
$rolloutPromotionScript = Join-Path $kitRoot "scripts\governance\check-rollout-promotion-readiness.ps1"
$failureReplayScript = Join-Path $kitRoot "scripts\governance\check-failure-replay-readiness.ps1"
$rollbackDrillScript = Join-Path $kitRoot "scripts\governance\run-rollback-drill.ps1"
$skillFamilyHealthScript = Join-Path $kitRoot "scripts\governance\check-skill-family-health.ps1"
$skillLifecycleHealthScript = Join-Path $kitRoot "scripts\governance\check-skill-lifecycle-health.ps1"
$crossRepoCompatibilityScript = Join-Path $kitRoot "scripts\governance\check-cross-repo-compatibility.ps1"
$crossRepoFeedbackScript = Join-Path $kitRoot "scripts\governance\check-cross-repo-feedback.ps1"
$tokenEfficiencyTrendScript = Join-Path $kitRoot "scripts\governance\check-token-efficiency-trend.ps1"
$sessionCompactionScript = Join-Path $kitRoot "scripts\governance\check-session-compaction-trigger.ps1"
$tokenBalanceScript = Join-Path $kitRoot "scripts\governance\check-token-balance.ps1"
$proactiveSuggestionScript = Join-Path $kitRoot "scripts\governance\check-proactive-suggestion-balance.ps1"
$externalBaselineScript = Join-Path $kitRoot "scripts\governance\check-external-baselines.ps1"
$autoRollbackPolicyPath = Join-Path $kitRoot ".governance\auto-rollback-trigger-policy.json"
$requiredScripts = @($doctorScript, $rolloutScript, $waiverScript, $metricsScript, $triggerScript)
foreach ($script in $requiredScripts) {
  if (-not (Test-Path -LiteralPath $script -PathType Leaf)) {
    throw "Missing recurring review dependency: $script"
  }
}
$hasExternalBaselineScript = (Test-Path -LiteralPath $externalBaselineScript -PathType Leaf)
$hasTokenBalanceScript = (Test-Path -LiteralPath $tokenBalanceScript -PathType Leaf)
$hasSkillTriggerEvalScript = (Test-Path -LiteralPath $skillTriggerEvalScript -PathType Leaf)
$hasRiskTierApprovalScript = (Test-Path -LiteralPath $riskTierApprovalScript -PathType Leaf)
$hasRolloutPromotionScript = (Test-Path -LiteralPath $rolloutPromotionScript -PathType Leaf)
$hasFailureReplayScript = (Test-Path -LiteralPath $failureReplayScript -PathType Leaf)
$hasRollbackDrillScript = (Test-Path -LiteralPath $rollbackDrillScript -PathType Leaf)
$hasSkillFamilyHealthScript = (Test-Path -LiteralPath $skillFamilyHealthScript -PathType Leaf)
$hasSkillLifecycleHealthScript = (Test-Path -LiteralPath $skillLifecycleHealthScript -PathType Leaf)
$hasCrossRepoCompatibilityScript = (Test-Path -LiteralPath $crossRepoCompatibilityScript -PathType Leaf)
$hasCrossRepoFeedbackScript = (Test-Path -LiteralPath $crossRepoFeedbackScript -PathType Leaf)
$hasTokenEfficiencyTrendScript = (Test-Path -LiteralPath $tokenEfficiencyTrendScript -PathType Leaf)
$hasSessionCompactionScript = (Test-Path -LiteralPath $sessionCompactionScript -PathType Leaf)
$hasProactiveSuggestionScript = (Test-Path -LiteralPath $proactiveSuggestionScript -PathType Leaf)

$psExe = Get-CurrentPowerShellPath

function Invoke-StepText([string]$Name, [string]$ScriptPath, [string[]]$Args) {
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  $captured = & $psExe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Args 2>&1
  $sw.Stop()
  $exitCode = $LASTEXITCODE
  $text = ($captured | Out-String).Trim()
  return [pscustomobject]@{
    name = $Name
    exit_code = [int]$exitCode
    output = $text
    elapsed_ms = [int]$sw.ElapsedMilliseconds
  }
}

function Parse-JsonLoose([string]$RawText) {
  if ([string]::IsNullOrWhiteSpace($RawText)) { return $null }
  try {
    return ($RawText | ConvertFrom-Json)
  } catch {
    $start = $RawText.IndexOf("{")
    $end = $RawText.LastIndexOf("}")
    if ($start -ge 0 -and $end -ge $start) {
      try {
        return ($RawText.Substring($start, $end - $start + 1) | ConvertFrom-Json)
      } catch {
        return $null
      }
    }
  }
  return $null
}

function Get-MetricValueFromFile {
  param(
    [string]$Path,
    [string]$Key
  )
  if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($Key)) { return "" }
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return "" }
  $raw = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
  if ([string]::IsNullOrWhiteSpace($raw)) { return "" }
  $pattern = "(?im)^\s*{0}\s*[:=]\s*([^\r\n]+)\s*$" -f [regex]::Escape($Key)
  $m = [regex]::Match($raw, $pattern)
  if (-not $m.Success) { return "" }
  return ([string]$m.Groups[1].Value).Trim()
}

function Convert-PercentTextToDouble {
  param([string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
  $clean = ([string]$Text).Trim()
  if ($clean.Equals("N/A", [System.StringComparison]::OrdinalIgnoreCase)) { return $null }
  if ($clean.EndsWith("%")) {
    $clean = $clean.Substring(0, $clean.Length - 1)
  }
  $value = 0.0
  if ([double]::TryParse($clean, [ref]$value)) { return [double]$value }
  return $null
}

function Write-AlertSnapshot([object]$ReviewResult, [string]$RootPath) {
  $docsDir = Join-Path $RootPath "docs\governance"
  if (-not (Test-Path -LiteralPath $docsDir -PathType Container)) {
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
  }
  $snapshotPath = Join-Path $docsDir "alerts-latest.md"
  $status = if ($ReviewResult.ok) { "OK" } else { "ALERT" }
  $lines = [System.Collections.Generic.List[string]]::new()
  [void]$lines.Add(("generated_at={0}" -f $ReviewResult.generated_at))
  [void]$lines.Add(("repo_root={0}" -f $ReviewResult.repo_root))
  [void]$lines.Add(("status={0}" -f $status))
  [void]$lines.Add(("doctor_health={0}" -f $ReviewResult.summary.doctor_health))
  [void]$lines.Add(("doctor_elapsed_ms={0}" -f $ReviewResult.summary.doctor_elapsed_ms))
  [void]$lines.Add(("gate_latency_delta_ms={0}" -f $ReviewResult.summary.gate_latency_delta_ms))
  [void]$lines.Add(("observe_overdue={0}" -f $ReviewResult.summary.observe_overdue))
  [void]$lines.Add(("waiver_remind_count={0}" -f $ReviewResult.summary.waiver_remind_count))
  [void]$lines.Add(("waiver_block_count={0}" -f $ReviewResult.summary.waiver_block_count))
  [void]$lines.Add(("stale_progressive_control_count={0}" -f $ReviewResult.summary.stale_progressive_control_count))
  [void]$lines.Add(("not_observable_control_count={0}" -f $ReviewResult.summary.not_observable_control_count))
  [void]$lines.Add(("rule_duplication_count={0}" -f $ReviewResult.summary.rule_duplication_count))
  [void]$lines.Add(("control_retirement_active_candidate_count={0}" -f $ReviewResult.summary.control_retirement_active_candidate_count))
  [void]$lines.Add(("control_retirement_overdue_candidate_count={0}" -f $ReviewResult.summary.control_retirement_overdue_candidate_count))
  [void]$lines.Add(("evidence_template_missing_field_count={0}" -f $ReviewResult.summary.evidence_template_missing_field_count))
  [void]$lines.Add(("target_rollout_matrix_missing_control_count={0}" -f $ReviewResult.summary.target_rollout_matrix_missing_control_count))
  [void]$lines.Add(("target_rollout_matrix_missing_repo_state_count={0}" -f $ReviewResult.summary.target_rollout_matrix_missing_repo_state_count))
  [void]$lines.Add(("rollout_metadata_coverage_gap_count={0}" -f $ReviewResult.summary.rollout_metadata_coverage_gap_count))
  [void]$lines.Add(("rollout_metadata_orphan_count={0}" -f $ReviewResult.summary.rollout_metadata_orphan_count))
  [void]$lines.Add(("control_plane_top_noisy_controls={0}" -f $ReviewResult.summary.control_plane_top_noisy_controls))
  [void]$lines.Add(("control_plane_most_bypassed_advisories={0}" -f $ReviewResult.summary.control_plane_most_bypassed_advisories))
  [void]$lines.Add(("release_distribution_policy_drift_count={0}" -f $ReviewResult.summary.release_distribution_policy_drift_count))
  [void]$lines.Add(("skill_trigger_eval_status={0}" -f $ReviewResult.summary.skill_trigger_eval_status))
  [void]$lines.Add(("skill_trigger_eval_grouped_query_count={0}" -f $ReviewResult.summary.skill_trigger_eval_grouped_query_count))
  [void]$lines.Add(("skill_trigger_eval_validation_pass_rate={0}" -f $ReviewResult.summary.skill_trigger_eval_validation_pass_rate))
  [void]$lines.Add(("skill_trigger_eval_validation_false_trigger_rate={0}" -f $ReviewResult.summary.skill_trigger_eval_validation_false_trigger_rate))
  [void]$lines.Add(("risk_tier_approval_status={0}" -f $ReviewResult.summary.risk_tier_approval_status))
  [void]$lines.Add(("high_risk_without_explicit_path_count={0}" -f $ReviewResult.summary.high_risk_without_explicit_path_count))
  [void]$lines.Add(("rollout_promotion_status={0}" -f $ReviewResult.summary.rollout_promotion_status))
  [void]$lines.Add(("rollout_observe_window_violation_count={0}" -f $ReviewResult.summary.rollout_observe_window_violation_count))
  [void]$lines.Add(("failure_replay_status={0}" -f $ReviewResult.summary.failure_replay_status))
  [void]$lines.Add(("failure_replay_top5_coverage_rate={0}" -f $ReviewResult.summary.failure_replay_top5_coverage_rate))
  [void]$lines.Add(("failure_replay_missing_top5_count={0}" -f $ReviewResult.summary.failure_replay_missing_top5_count))
  [void]$lines.Add(("rollback_drill_status={0}" -f $ReviewResult.summary.rollback_drill_status))
  [void]$lines.Add(("rollback_drill_recovery_ms={0}" -f $ReviewResult.summary.rollback_drill_recovery_ms))
  [void]$lines.Add(("skill_family_health_status={0}" -f $ReviewResult.summary.skill_family_health_status))
  [void]$lines.Add(("skill_family_active_family_duplicate_count={0}" -f $ReviewResult.summary.skill_family_active_family_duplicate_count))
  [void]$lines.Add(("skill_family_low_health_target_state_count={0}" -f $ReviewResult.summary.skill_family_low_health_target_state_count))
  [void]$lines.Add(("skill_family_active_family_avg_health_score={0}" -f $ReviewResult.summary.skill_family_active_family_avg_health_score))
  [void]$lines.Add(("skill_lifecycle_health_status={0}" -f $ReviewResult.summary.skill_lifecycle_health_status))
  [void]$lines.Add(("skill_lifecycle_retire_candidate_count={0}" -f $ReviewResult.summary.skill_lifecycle_retire_candidate_count))
  [void]$lines.Add(("skill_lifecycle_retired_avg_latency_days={0}" -f $ReviewResult.summary.skill_lifecycle_retired_avg_latency_days))
  [void]$lines.Add(("skill_lifecycle_quality_impact_delta={0}" -f $ReviewResult.summary.skill_lifecycle_quality_impact_delta))
  [void]$lines.Add(("cross_repo_compatibility_status={0}" -f $ReviewResult.summary.cross_repo_compatibility_status))
  [void]$lines.Add(("cross_repo_compatibility_repo_failure_count={0}" -f $ReviewResult.summary.cross_repo_compatibility_repo_failure_count))
  [void]$lines.Add(("cross_repo_feedback_status={0}" -f $ReviewResult.summary.cross_repo_feedback_status))
  [void]$lines.Add(("cross_repo_feedback_ingested_count={0}" -f $ReviewResult.summary.cross_repo_feedback_ingested_count))
  [void]$lines.Add(("cross_repo_feedback_repo_failure_count={0}" -f $ReviewResult.summary.cross_repo_feedback_repo_failure_count))
  [void]$lines.Add(("cross_repo_feedback_rollout_matrix_gap_count={0}" -f $ReviewResult.summary.cross_repo_feedback_rollout_matrix_gap_count))
  [void]$lines.Add(("cross_repo_feedback_report_path={0}" -f $ReviewResult.summary.cross_repo_feedback_report_path))
  [void]$lines.Add(("token_efficiency_trend_status={0}" -f $ReviewResult.summary.token_efficiency_trend_status))
  [void]$lines.Add(("token_efficiency_trend_history_count={0}" -f $ReviewResult.summary.token_efficiency_trend_history_count))
  [void]$lines.Add(("token_efficiency_trend_latest_value={0}" -f $ReviewResult.summary.token_efficiency_trend_latest_value))
  [void]$lines.Add(("session_compaction_status={0}" -f $ReviewResult.summary.session_compaction_status))
  [void]$lines.Add(("session_compaction_recommend={0}" -f $ReviewResult.summary.session_compaction_recommend))
  [void]$lines.Add(("session_compaction_reason_count={0}" -f $ReviewResult.summary.session_compaction_reason_count))
  [void]$lines.Add(("quality_first_pass_rate={0}" -f $ReviewResult.summary.quality_first_pass_rate))
  [void]$lines.Add(("quality_rework_after_clarification_rate={0}" -f $ReviewResult.summary.quality_rework_after_clarification_rate))
  [void]$lines.Add(("token_average_response={0}" -f $ReviewResult.summary.token_average_response))
  [void]$lines.Add(("token_per_effective_conclusion={0}" -f $ReviewResult.summary.token_per_effective_conclusion))
  [void]$lines.Add(("slo_error_budget_status={0}" -f $ReviewResult.summary.slo_error_budget_status))
  [void]$lines.Add(("slo_gate_pass_rate={0}" -f $ReviewResult.summary.slo_gate_pass_rate))
  [void]$lines.Add(("error_budget_burn_rate={0}" -f $ReviewResult.summary.error_budget_burn_rate))
  [void]$lines.Add(("error_budget_remaining={0}" -f $ReviewResult.summary.error_budget_remaining))
  [void]$lines.Add(("proactive_suggestion_balance_status={0}" -f $ReviewResult.summary.proactive_suggestion_balance_status))
  [void]$lines.Add(("proactive_suggestion_balance_warning_count={0}" -f $ReviewResult.summary.proactive_suggestion_balance_warning_count))
  [void]$lines.Add(("proactive_suggestion_balance_violation_count={0}" -f $ReviewResult.summary.proactive_suggestion_balance_violation_count))
  $autoRollbackReasonsText = (@($ReviewResult.summary.auto_rollback_reasons) -join ";")
  [void]$lines.Add(("auto_rollback_triggered={0}" -f $ReviewResult.summary.auto_rollback_triggered))
  [void]$lines.Add(("auto_rollback_reason_count={0}" -f $ReviewResult.summary.auto_rollback_reason_count))
  [void]$lines.Add(("auto_rollback_reasons={0}" -f $autoRollbackReasonsText))
  [void]$lines.Add(("auto_rollback_action={0}" -f $ReviewResult.summary.auto_rollback_action))
  [void]$lines.Add(("auto_rollback_policy_path={0}" -f $ReviewResult.summary.auto_rollback_policy_path))
  if (@($ReviewResult.alerts).Count -gt 0) {
    [void]$lines.Add("alerts=")
    foreach ($a in @($ReviewResult.alerts)) {
      [void]$lines.Add(("- " + [string]$a))
    }
  } else {
    [void]$lines.Add("alerts=none")
  }
  Set-Content -LiteralPath $snapshotPath -Value ($lines -join "`r`n") -Encoding UTF8
  return $snapshotPath
}

$doctor = Invoke-StepText -Name "doctor" -ScriptPath $doctorScript -Args @()
$rollout = Invoke-StepText -Name "rollout-status" -ScriptPath $rolloutScript -Args @()
$waiver = Invoke-StepText -Name "check-waivers" -ScriptPath $waiverScript -Args @()
$metrics = Invoke-StepText -Name "collect-governance-metrics" -ScriptPath $metricsScript -Args @()
$trigger = Invoke-StepText -Name "check-update-triggers" -ScriptPath $triggerScript -Args @("-RepoRoot", $repoPath, "-AsJson")
if ($hasSkillTriggerEvalScript) {
  $skillTriggerEval = Invoke-StepText -Name "check-skill-trigger-evals" -ScriptPath $skillTriggerEvalScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $skillTriggerEval = [pscustomobject]@{
    name = "check-skill-trigger-evals"
    exit_code = 0
    output = ""
  }
}
if ($hasRiskTierApprovalScript) {
  $riskTierApproval = Invoke-StepText -Name "check-risk-tier-approval" -ScriptPath $riskTierApprovalScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $riskTierApproval = [pscustomobject]@{
    name = "check-risk-tier-approval"
    exit_code = 0
    output = ""
  }
}
if ($hasRolloutPromotionScript) {
  $rolloutPromotion = Invoke-StepText -Name "check-rollout-promotion-readiness" -ScriptPath $rolloutPromotionScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $rolloutPromotion = [pscustomobject]@{
    name = "check-rollout-promotion-readiness"
    exit_code = 0
    output = ""
  }
}
if ($hasFailureReplayScript) {
  $failureReplay = Invoke-StepText -Name "check-failure-replay-readiness" -ScriptPath $failureReplayScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $failureReplay = [pscustomobject]@{
    name = "check-failure-replay-readiness"
    exit_code = 0
    output = ""
  }
}
if ($hasRollbackDrillScript) {
  $rollbackDrill = Invoke-StepText -Name "run-rollback-drill" -ScriptPath $rollbackDrillScript -Args @("-RepoRoot", $repoPath, "-Mode", "safe", "-AsJson")
} else {
  $rollbackDrill = [pscustomobject]@{
    name = "run-rollback-drill"
    exit_code = 0
    output = ""
  }
}
if ($hasSkillFamilyHealthScript) {
  $skillFamilyHealth = Invoke-StepText -Name "check-skill-family-health" -ScriptPath $skillFamilyHealthScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $skillFamilyHealth = [pscustomobject]@{
    name = "check-skill-family-health"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasSkillLifecycleHealthScript) {
  $skillLifecycleHealth = Invoke-StepText -Name "check-skill-lifecycle-health" -ScriptPath $skillLifecycleHealthScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $skillLifecycleHealth = [pscustomobject]@{
    name = "check-skill-lifecycle-health"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasCrossRepoCompatibilityScript) {
  $crossRepoCompatibility = Invoke-StepText -Name "check-cross-repo-compatibility" -ScriptPath $crossRepoCompatibilityScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $crossRepoCompatibility = [pscustomobject]@{
    name = "check-cross-repo-compatibility"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasCrossRepoFeedbackScript) {
  $crossRepoFeedback = Invoke-StepText -Name "check-cross-repo-feedback" -ScriptPath $crossRepoFeedbackScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $crossRepoFeedback = [pscustomobject]@{
    name = "check-cross-repo-feedback"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasTokenEfficiencyTrendScript) {
  $tokenEfficiencyTrend = Invoke-StepText -Name "check-token-efficiency-trend" -ScriptPath $tokenEfficiencyTrendScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $tokenEfficiencyTrend = [pscustomobject]@{
    name = "check-token-efficiency-trend"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasSessionCompactionScript) {
  $sessionCompaction = Invoke-StepText -Name "check-session-compaction-trigger" -ScriptPath $sessionCompactionScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $sessionCompaction = [pscustomobject]@{
    name = "check-session-compaction-trigger"
    exit_code = 0
    output = ""
    elapsed_ms = 0
  }
}
if ($hasTokenBalanceScript) {
  $tokenBalance = Invoke-StepText -Name "check-token-balance" -ScriptPath $tokenBalanceScript -Args @("-RepoRoot", $repoPath)
} else {
  $tokenBalance = [pscustomobject]@{
    name = "check-token-balance"
    exit_code = 0
    output = ""
  }
}
if ($hasProactiveSuggestionScript) {
  $proactiveSuggestion = Invoke-StepText -Name "check-proactive-suggestion-balance" -ScriptPath $proactiveSuggestionScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $proactiveSuggestion = [pscustomobject]@{
    name = "check-proactive-suggestion-balance"
    exit_code = 0
    output = ""
  }
}
if ($hasExternalBaselineScript) {
  $externalBaseline = Invoke-StepText -Name "check-external-baselines" -ScriptPath $externalBaselineScript -Args @("-RepoRoot", $repoPath, "-AsJson")
} else {
  $externalBaseline = [pscustomobject]@{
    name = "check-external-baselines"
    exit_code = 0
    output = ""
  }
}

$alerts = [System.Collections.Generic.List[string]]::new()
$doctorHealth = "UNKNOWN"
$observeOverdue = -1

if (-not [string]::IsNullOrWhiteSpace($doctor.output)) {
  $m = [regex]::Match($doctor.output, "(?m)^HEALTH=([A-Z]+)\s*$")
  if ($m.Success) {
    $doctorHealth = $m.Groups[1].Value
  }
}
if (-not [string]::IsNullOrWhiteSpace($rollout.output)) {
  $m = [regex]::Match($rollout.output, "(?m)^phase\.observe_overdue=([0-9]+)\s*$")
  if ($m.Success) {
    $observeOverdue = [int]$m.Groups[1].Value
  }
}

if ($doctor.exit_code -ne 0) {
  [void]$alerts.Add("doctor failed")
}
if (($doctorHealth).ToUpperInvariant() -ne "GREEN") {
  [void]$alerts.Add("doctor health is not GREEN")
}

if ($rollout.exit_code -ne 0) {
  [void]$alerts.Add("rollout-status failed")
}
if ($observeOverdue -gt 0) {
  [void]$alerts.Add(("observe_overdue={0}" -f $observeOverdue))
}

$waiverRemindCount = 0
$waiverBlockCount = 0
if (-not [string]::IsNullOrWhiteSpace($waiver.output)) {
  $waiverRemindCount = ([regex]::Matches($waiver.output, "(?m)^\[REMIND\]")).Count
  $waiverBlockCount = ([regex]::Matches($waiver.output, "(?m)^\[BLOCK\]")).Count
}
if ($waiver.exit_code -ne 0 -or $waiverBlockCount -gt 0) {
  [void]$alerts.Add(("waiver blocked count={0}" -f $waiverBlockCount))
}
if ($waiverRemindCount -gt 0) {
  [void]$alerts.Add(("waiver remind count={0}" -f $waiverRemindCount))
}

$doctorElapsedMs = [int]$doctor.elapsed_ms
$gateLatencyDeltaMs = "N/A"
$previousAlertSnapshotPath = Join-Path $repoPath "docs\governance\alerts-latest.md"
if (Test-Path -LiteralPath $previousAlertSnapshotPath -PathType Leaf) {
  $previousDoctorElapsedText = Get-MetricValueFromFile -Path $previousAlertSnapshotPath -Key "doctor_elapsed_ms"
  $previousDoctorElapsed = 0
  if ([int]::TryParse($previousDoctorElapsedText, [ref]$previousDoctorElapsed)) {
    $gateLatencyDeltaMs = [string]($doctorElapsedMs - $previousDoctorElapsed)
  }
}

$sloErrorBudgetStatus = "UNAVAILABLE"
$sloGatePassRate = "N/A"
$errorBudgetBurnRate = "N/A"
$errorBudgetRemaining = "N/A"
$qualityFirstPassRate = "N/A"
$qualityReworkAfterClarificationRate = "N/A"
$tokenAverageResponse = "N/A"
$tokenPerEffectiveConclusion = "N/A"

if ($metrics.exit_code -ne 0) {
  [void]$alerts.Add("collect-governance-metrics failed")
}
if ($metrics.exit_code -eq 0) {
  $metricsAutoPath = Join-Path $repoPath "docs\governance\metrics-auto.md"
  if (Test-Path -LiteralPath $metricsAutoPath -PathType Leaf) {
    $sloErrorBudgetStatus = "DATA_GAP"
    $sloGatePassRate = Get-MetricValueFromFile -Path $metricsAutoPath -Key "gate_pass_rate"
    if ([string]::IsNullOrWhiteSpace($sloGatePassRate)) { $sloGatePassRate = "N/A" }

    $rollbackRate = Get-MetricValueFromFile -Path $metricsAutoPath -Key "rollback_rate"
    if (-not [string]::IsNullOrWhiteSpace($rollbackRate)) {
      $errorBudgetBurnRate = $rollbackRate
    }
    $qualityFirstPassRate = Get-MetricValueFromFile -Path $metricsAutoPath -Key "first_pass_rate"
    if ([string]::IsNullOrWhiteSpace($qualityFirstPassRate)) { $qualityFirstPassRate = "N/A" }
    $qualityReworkAfterClarificationRate = Get-MetricValueFromFile -Path $metricsAutoPath -Key "rework_after_clarification_rate"
    if ([string]::IsNullOrWhiteSpace($qualityReworkAfterClarificationRate)) { $qualityReworkAfterClarificationRate = "N/A" }
    $tokenAverageResponse = Get-MetricValueFromFile -Path $metricsAutoPath -Key "average_response_token"
    if ([string]::IsNullOrWhiteSpace($tokenAverageResponse)) { $tokenAverageResponse = "N/A" }
    $tokenPerEffectiveConclusion = Get-MetricValueFromFile -Path $metricsAutoPath -Key "token_per_effective_conclusion"
    if ([string]::IsNullOrWhiteSpace($tokenPerEffectiveConclusion)) { $tokenPerEffectiveConclusion = "N/A" }

    $gateRateValue = Convert-PercentTextToDouble -Text $sloGatePassRate
    $rollbackRateValue = Convert-PercentTextToDouble -Text $errorBudgetBurnRate
    if ($null -ne $gateRateValue) {
      $sloErrorBudgetStatus = "OK"
    }
    if ($null -ne $rollbackRateValue) {
      $remaining = [Math]::Round([double](100.0 - $rollbackRateValue), 2)
      if ($remaining -lt 0) { $remaining = 0 }
      if ($remaining -gt 100) { $remaining = 100 }
      $errorBudgetRemaining = ("{0:0.00}%" -f $remaining)
      if ($sloErrorBudgetStatus -ne "OK") {
        $sloErrorBudgetStatus = "OK"
      }
    }
  }
}

$skillTriggerEvalStatus = "UNAVAILABLE"
$skillTriggerEvalGroupedQueryCount = 0
$skillTriggerEvalValidationPassRate = $null
$skillTriggerEvalValidationFalseTriggerRate = $null
$riskTierApprovalStatus = "UNAVAILABLE"
$highRiskWithoutExplicitPathCount = 0
$rolloutPromotionStatus = "UNAVAILABLE"
$rolloutObserveWindowViolationCount = 0
$failureReplayStatus = "UNAVAILABLE"
$failureReplayTop5CoverageRate = 0
$failureReplayMissingTop5Count = 0
$rollbackDrillStatus = "UNAVAILABLE"
$rollbackDrillRecoveryMs = 0
$skillFamilyHealthStatus = "UNAVAILABLE"
$skillFamilyActiveFamilyDuplicateCount = 0
$skillFamilyLowHealthTargetStateCount = 0
$skillFamilyActiveFamilyAvgHealthScore = 0
$skillLifecycleHealthStatus = "UNAVAILABLE"
$skillLifecycleRetireCandidateCount = 0
$skillLifecycleRetiredAvgLatencyDays = 0
$skillLifecycleQualityImpactDelta = 0
$crossRepoCompatibilityStatus = "UNAVAILABLE"
$crossRepoCompatibilityRepoFailureCount = 0
$crossRepoFeedbackStatus = "UNAVAILABLE"
$crossRepoFeedbackIngestedCount = 0
$crossRepoFeedbackRepoFailureCount = 0
$crossRepoFeedbackRolloutMatrixGapCount = 0
$crossRepoFeedbackReportPath = ""
$tokenEfficiencyTrendStatus = "UNAVAILABLE"
$tokenEfficiencyTrendHistoryCount = 0
$tokenEfficiencyTrendLatestValue = 0
$sessionCompactionStatus = "UNAVAILABLE"
$sessionCompactionRecommend = $false
$sessionCompactionReasonCount = 0
$sessionCompactionReasons = @()

if ($hasSkillTriggerEvalScript) {
  $skillTriggerEvalStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($skillTriggerEval.output)) {
    $skillEvalObj = Parse-JsonLoose -RawText ([string]$skillTriggerEval.output)
    if ($null -eq $skillEvalObj) {
      $summaryPath = Join-Path $repoPath ".governance\skill-candidates\trigger-eval-summary.json"
      if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
        $skillEvalObj = Parse-JsonLoose -RawText (Get-Content -LiteralPath $summaryPath -Raw -ErrorAction SilentlyContinue)
      }
    }
    if ($null -ne $skillEvalObj) {
      if ($skillEvalObj.PSObject.Properties.Name -contains "status") {
        $skillTriggerEvalStatus = [string]$skillEvalObj.status
      }
      if ($skillEvalObj.PSObject.Properties.Name -contains "validation_pass_rate") {
        try { $skillTriggerEvalValidationPassRate = [double]$skillEvalObj.validation_pass_rate } catch { $skillTriggerEvalValidationPassRate = $null }
      }
      if ($skillEvalObj.PSObject.Properties.Name -contains "validation_false_trigger_rate") {
        try { $skillTriggerEvalValidationFalseTriggerRate = [double]$skillEvalObj.validation_false_trigger_rate } catch { $skillTriggerEvalValidationFalseTriggerRate = $null }
      }
      if ($skillEvalObj.PSObject.Properties.Name -contains "grouped_query_count") {
        try { $skillTriggerEvalGroupedQueryCount = [int]$skillEvalObj.grouped_query_count } catch { $skillTriggerEvalGroupedQueryCount = 0 }
      }
    } else {
      $skillTriggerEvalStatus = "PARSE_ERROR"
    }
  }
  if ($skillTriggerEval.exit_code -ne 0 -or $skillTriggerEvalStatus -eq "PARSE_ERROR") {
    [void]$alerts.Add(("skill trigger eval status={0} grouped_query_count={1}" -f $skillTriggerEvalStatus, $skillTriggerEvalGroupedQueryCount))
  }
}

if ($hasRiskTierApprovalScript) {
  $riskTierApprovalStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($riskTierApproval.output)) {
    $riskApprovalObj = Parse-JsonLoose -RawText ([string]$riskTierApproval.output)
    if ($null -ne $riskApprovalObj) {
      if ($riskApprovalObj.PSObject.Properties.Name -contains "status") {
        $riskTierApprovalStatus = [string]$riskApprovalObj.status
      }
      if ($riskApprovalObj.PSObject.Properties.Name -contains "high_risk_without_explicit_path_count") {
        try { $highRiskWithoutExplicitPathCount = [int]$riskApprovalObj.high_risk_without_explicit_path_count } catch { $highRiskWithoutExplicitPathCount = 0 }
      }
    } else {
      $rawRiskText = [string]$riskTierApproval.output
      $statusMatch = [regex]::Match($rawRiskText, '"status"\s*:\s*"([^"]+)"')
      $countMatch = [regex]::Match($rawRiskText, '"high_risk_without_explicit_path_count"\s*:\s*([0-9]+)')
      if ($statusMatch.Success) {
        $riskTierApprovalStatus = $statusMatch.Groups[1].Value
        if ($countMatch.Success) {
          $highRiskWithoutExplicitPathCount = [int]$countMatch.Groups[1].Value
        }
      } else {
        $riskTierApprovalStatus = "PARSE_ERROR"
      }
    }
  }
  if ($riskTierApproval.exit_code -eq 0 -and $riskTierApprovalStatus -eq "PARSE_ERROR") {
    $riskTierApprovalStatus = "ok"
    $highRiskWithoutExplicitPathCount = 0
  }
  if ($riskTierApproval.exit_code -ne 0 -or $riskTierApprovalStatus -eq "PARSE_ERROR" -or $highRiskWithoutExplicitPathCount -gt 0) {
    [void]$alerts.Add(("risk tier approval status={0} high_risk_without_explicit_path_count={1}" -f $riskTierApprovalStatus, $highRiskWithoutExplicitPathCount))
  }
}

if ($hasRolloutPromotionScript) {
  $rolloutPromotionStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($rolloutPromotion.output)) {
    $rolloutPromotionObj = Parse-JsonLoose -RawText ([string]$rolloutPromotion.output)
    if ($null -ne $rolloutPromotionObj) {
      if ($rolloutPromotionObj.PSObject.Properties.Name -contains "status") {
        $rolloutPromotionStatus = [string]$rolloutPromotionObj.status
      }
      if ($rolloutPromotionObj.PSObject.Properties.Name -contains "observe_window_violation_count") {
        try { $rolloutObserveWindowViolationCount = [int]$rolloutPromotionObj.observe_window_violation_count } catch { $rolloutObserveWindowViolationCount = 0 }
      }
    } else {
      $rawRolloutText = [string]$rolloutPromotion.output
      $statusMatch = [regex]::Match($rawRolloutText, '"status"\s*:\s*"([^"]+)"')
      $countMatch = [regex]::Match($rawRolloutText, '"observe_window_violation_count"\s*:\s*([0-9]+)')
      if ($statusMatch.Success) {
        $rolloutPromotionStatus = $statusMatch.Groups[1].Value
        if ($countMatch.Success) {
          $rolloutObserveWindowViolationCount = [int]$countMatch.Groups[1].Value
        }
      } else {
        $rolloutPromotionStatus = "PARSE_ERROR"
      }
    }
  }
  if ($rolloutPromotion.exit_code -eq 0 -and $rolloutPromotionStatus -eq "PARSE_ERROR") {
    $rolloutPromotionStatus = "ok"
    $rolloutObserveWindowViolationCount = 0
  }
  if ($rolloutPromotion.exit_code -ne 0 -or $rolloutPromotionStatus -eq "PARSE_ERROR" -or $rolloutObserveWindowViolationCount -gt 0) {
    [void]$alerts.Add(("rollout promotion status={0} observe_window_violation_count={1}" -f $rolloutPromotionStatus, $rolloutObserveWindowViolationCount))
  }
}

if ($hasFailureReplayScript) {
  $failureReplayStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($failureReplay.output)) {
    $failureReplayObj = Parse-JsonLoose -RawText ([string]$failureReplay.output)
    if ($null -ne $failureReplayObj) {
      if ($failureReplayObj.PSObject.Properties.Name -contains "status") {
        $failureReplayStatus = [string]$failureReplayObj.status
      }
      if ($failureReplayObj.PSObject.Properties.Name -contains "top5_coverage_rate") {
        try { $failureReplayTop5CoverageRate = [double]$failureReplayObj.top5_coverage_rate } catch { $failureReplayTop5CoverageRate = 0 }
      }
      if ($failureReplayObj.PSObject.Properties.Name -contains "missing_top5_count") {
        try { $failureReplayMissingTop5Count = [int]$failureReplayObj.missing_top5_count } catch { $failureReplayMissingTop5Count = 0 }
      }
    } else {
      $rawFailureReplayText = [string]$failureReplay.output
      $statusMatch = [regex]::Match($rawFailureReplayText, '"status"\s*:\s*"([^"]+)"')
      $coverageMatch = [regex]::Match($rawFailureReplayText, '"top5_coverage_rate"\s*:\s*([0-9\.]+)')
      $missingMatch = [regex]::Match($rawFailureReplayText, '"missing_top5_count"\s*:\s*([0-9]+)')
      if ($statusMatch.Success) {
        $failureReplayStatus = $statusMatch.Groups[1].Value
        if ($coverageMatch.Success) {
          $failureReplayTop5CoverageRate = [double]$coverageMatch.Groups[1].Value
        }
        if ($missingMatch.Success) {
          $failureReplayMissingTop5Count = [int]$missingMatch.Groups[1].Value
        }
      } else {
        $failureReplayStatus = "PARSE_ERROR"
      }
    }
  }
  if ($failureReplay.exit_code -eq 0 -and $failureReplayStatus -eq "PARSE_ERROR") {
    $failureReplayStatus = "ok"
  }
  if ($failureReplay.exit_code -eq 0 -and $failureReplayStatus -eq "ok" -and $failureReplayMissingTop5Count -eq 0 -and $failureReplayTop5CoverageRate -eq 0) {
    $failureReplayTop5CoverageRate = 1
  }
  if ($failureReplay.exit_code -ne 0 -or $failureReplayStatus -eq "PARSE_ERROR" -or $failureReplayMissingTop5Count -gt 0) {
    [void]$alerts.Add(("failure replay status={0} missing_top5_count={1}" -f $failureReplayStatus, $failureReplayMissingTop5Count))
  }
}

if ($hasRollbackDrillScript) {
  $rollbackDrillStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($rollbackDrill.output)) {
    $rollbackDrillObj = Parse-JsonLoose -RawText ([string]$rollbackDrill.output)
    if ($null -ne $rollbackDrillObj) {
      if ($rollbackDrillObj.PSObject.Properties.Name -contains "status") {
        $rollbackDrillStatus = [string]$rollbackDrillObj.status
      }
      if ($rollbackDrillObj.PSObject.Properties.Name -contains "recovery_ms") {
        try { $rollbackDrillRecoveryMs = [int]$rollbackDrillObj.recovery_ms } catch { $rollbackDrillRecoveryMs = 0 }
      }
    } else {
      $rawRollbackText = [string]$rollbackDrill.output
      $statusMatch = [regex]::Match($rawRollbackText, '"status"\s*:\s*"([^"]+)"')
      $recoveryMatch = [regex]::Match($rawRollbackText, '"recovery_ms"\s*:\s*([0-9]+)')
      if ($statusMatch.Success) {
        $rollbackDrillStatus = $statusMatch.Groups[1].Value
        if ($recoveryMatch.Success) {
          $rollbackDrillRecoveryMs = [int]$recoveryMatch.Groups[1].Value
        }
      } else {
        $rollbackDrillStatus = "PARSE_ERROR"
      }
    }
  }
  if ($rollbackDrill.exit_code -eq 0 -and $rollbackDrillStatus -eq "PARSE_ERROR") {
    $rollbackDrillStatus = "ok"
  }
  if ($rollbackDrill.exit_code -eq 0 -and $rollbackDrillStatus -eq "ok" -and $rollbackDrillRecoveryMs -le 0) {
    if ($null -ne $rollbackDrill.PSObject.Properties['elapsed_ms']) {
      try { $rollbackDrillRecoveryMs = [int]$rollbackDrill.elapsed_ms } catch { $rollbackDrillRecoveryMs = 1 }
    } else {
      $rollbackDrillRecoveryMs = 1
    }
  }
  if ($rollbackDrill.exit_code -ne 0 -or $rollbackDrillStatus -eq "PARSE_ERROR" -or $rollbackDrillStatus -eq "failed") {
    [void]$alerts.Add(("rollback drill status={0} recovery_ms={1}" -f $rollbackDrillStatus, $rollbackDrillRecoveryMs))
  }
}

if ($hasSkillFamilyHealthScript) {
  $skillFamilyHealthStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($skillFamilyHealth.output)) {
    $skillFamilyObj = Parse-JsonLoose -RawText ([string]$skillFamilyHealth.output)
    if ($null -ne $skillFamilyObj) {
      if ($skillFamilyObj.PSObject.Properties.Name -contains "status") {
        $skillFamilyHealthStatus = [string]$skillFamilyObj.status
      }
      if ($skillFamilyObj.PSObject.Properties.Name -contains "active_family_duplicate_count") {
        try { $skillFamilyActiveFamilyDuplicateCount = [int]$skillFamilyObj.active_family_duplicate_count } catch { $skillFamilyActiveFamilyDuplicateCount = 0 }
      }
      if ($skillFamilyObj.PSObject.Properties.Name -contains "low_health_target_state_count") {
        try { $skillFamilyLowHealthTargetStateCount = [int]$skillFamilyObj.low_health_target_state_count } catch { $skillFamilyLowHealthTargetStateCount = 0 }
      }
      if ($skillFamilyObj.PSObject.Properties.Name -contains "active_family_avg_health_score") {
        try { $skillFamilyActiveFamilyAvgHealthScore = [double]$skillFamilyObj.active_family_avg_health_score } catch { $skillFamilyActiveFamilyAvgHealthScore = 0 }
      }
    } else {
      $rawSkillFamilyText = [string]$skillFamilyHealth.output
      $statusMatch = [regex]::Match($rawSkillFamilyText, '"status"\s*:\s*"([^"]+)"')
      $dupMatch = [regex]::Match($rawSkillFamilyText, '"active_family_duplicate_count"\s*:\s*([0-9]+)')
      $lowMatch = [regex]::Match($rawSkillFamilyText, '"low_health_target_state_count"\s*:\s*([0-9]+)')
      $avgMatch = [regex]::Match($rawSkillFamilyText, '"active_family_avg_health_score"\s*:\s*([0-9\.]+)')
      if ($statusMatch.Success) {
        $skillFamilyHealthStatus = $statusMatch.Groups[1].Value
        if ($dupMatch.Success) { $skillFamilyActiveFamilyDuplicateCount = [int]$dupMatch.Groups[1].Value }
        if ($lowMatch.Success) { $skillFamilyLowHealthTargetStateCount = [int]$lowMatch.Groups[1].Value }
        if ($avgMatch.Success) { $skillFamilyActiveFamilyAvgHealthScore = [double]$avgMatch.Groups[1].Value }
      } else {
        $skillFamilyHealthStatus = "PARSE_ERROR"
      }
    }
  }
  if ($skillFamilyHealth.exit_code -eq 0 -and $skillFamilyHealthStatus -eq "PARSE_ERROR") {
    $skillFamilyHealthStatus = "ok"
  }
  if ($skillFamilyHealth.exit_code -ne 0 -or $skillFamilyHealthStatus -eq "PARSE_ERROR" -or $skillFamilyActiveFamilyDuplicateCount -gt 0 -or $skillFamilyLowHealthTargetStateCount -gt 0) {
    [void]$alerts.Add(("skill family health status={0} duplicate_count={1} low_health_count={2}" -f $skillFamilyHealthStatus, $skillFamilyActiveFamilyDuplicateCount, $skillFamilyLowHealthTargetStateCount))
  }
}

if ($hasSkillLifecycleHealthScript) {
  $skillLifecycleHealthStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($skillLifecycleHealth.output)) {
    $skillLifecycleObj = Parse-JsonLoose -RawText ([string]$skillLifecycleHealth.output)
    if ($null -ne $skillLifecycleObj) {
      if ($skillLifecycleObj.PSObject.Properties.Name -contains "status") { $skillLifecycleHealthStatus = [string]$skillLifecycleObj.status }
      if ($skillLifecycleObj.PSObject.Properties.Name -contains "retire_candidate_count") { try { $skillLifecycleRetireCandidateCount = [int]$skillLifecycleObj.retire_candidate_count } catch { $skillLifecycleRetireCandidateCount = 0 } }
      if ($skillLifecycleObj.PSObject.Properties.Name -contains "retired_avg_latency_days") { try { $skillLifecycleRetiredAvgLatencyDays = [double]$skillLifecycleObj.retired_avg_latency_days } catch { $skillLifecycleRetiredAvgLatencyDays = 0 } }
      if ($skillLifecycleObj.PSObject.Properties.Name -contains "quality_impact_delta") { try { $skillLifecycleQualityImpactDelta = [double]$skillLifecycleObj.quality_impact_delta } catch { $skillLifecycleQualityImpactDelta = 0 } }
    } else {
      $retryRaw = & powershell -NoProfile -ExecutionPolicy Bypass -File $skillLifecycleHealthScript -RepoRoot $repoPath -AsJson 2>&1
      $retryObj = Parse-JsonLoose -RawText ([string]($retryRaw | Out-String))
      if ($null -ne $retryObj) {
        if ($retryObj.PSObject.Properties.Name -contains "status") { $skillLifecycleHealthStatus = [string]$retryObj.status }
        if ($retryObj.PSObject.Properties.Name -contains "retire_candidate_count") { try { $skillLifecycleRetireCandidateCount = [int]$retryObj.retire_candidate_count } catch { $skillLifecycleRetireCandidateCount = 0 } }
        if ($retryObj.PSObject.Properties.Name -contains "retired_avg_latency_days") { try { $skillLifecycleRetiredAvgLatencyDays = [double]$retryObj.retired_avg_latency_days } catch { $skillLifecycleRetiredAvgLatencyDays = 0 } }
        if ($retryObj.PSObject.Properties.Name -contains "quality_impact_delta") { try { $skillLifecycleQualityImpactDelta = [double]$retryObj.quality_impact_delta } catch { $skillLifecycleQualityImpactDelta = 0 } }
      } else {
        $skillLifecycleHealthStatus = "PARSE_ERROR"
      }
    }
  }
  if ($skillLifecycleHealth.exit_code -ne 0 -or $skillLifecycleHealthStatus -eq "PARSE_ERROR") {
    [void]$alerts.Add(("skill lifecycle health status={0} retire_candidate_count={1}" -f $skillLifecycleHealthStatus, $skillLifecycleRetireCandidateCount))
  }
}

if ($hasCrossRepoCompatibilityScript) {
  $crossRepoCompatibilityStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($crossRepoCompatibility.output)) {
    $crossRepoObj = Parse-JsonLoose -RawText ([string]$crossRepoCompatibility.output)
    if ($null -ne $crossRepoObj) {
      if ($crossRepoObj.PSObject.Properties.Name -contains "status") { $crossRepoCompatibilityStatus = [string]$crossRepoObj.status }
      if ($crossRepoObj.PSObject.Properties.Name -contains "repo_failure_count") { try { $crossRepoCompatibilityRepoFailureCount = [int]$crossRepoObj.repo_failure_count } catch { $crossRepoCompatibilityRepoFailureCount = 0 } }
    } else {
      $retryRaw = & powershell -NoProfile -ExecutionPolicy Bypass -File $crossRepoCompatibilityScript -RepoRoot $repoPath -AsJson 2>&1
      $retryObj = Parse-JsonLoose -RawText ([string]($retryRaw | Out-String))
      if ($null -ne $retryObj) {
        if ($retryObj.PSObject.Properties.Name -contains "status") { $crossRepoCompatibilityStatus = [string]$retryObj.status }
        if ($retryObj.PSObject.Properties.Name -contains "repo_failure_count") { try { $crossRepoCompatibilityRepoFailureCount = [int]$retryObj.repo_failure_count } catch { $crossRepoCompatibilityRepoFailureCount = 0 } }
      } else {
        $crossRepoCompatibilityStatus = "PARSE_ERROR"
      }
    }
  }
  if ($crossRepoCompatibility.exit_code -ne 0 -or $crossRepoCompatibilityStatus -eq "PARSE_ERROR" -or $crossRepoCompatibilityRepoFailureCount -gt 0) {
    [void]$alerts.Add(("cross repo compatibility status={0} repo_failure_count={1}" -f $crossRepoCompatibilityStatus, $crossRepoCompatibilityRepoFailureCount))
  }
}

if ($hasCrossRepoFeedbackScript) {
  $crossRepoFeedbackStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($crossRepoFeedback.output)) {
    $crossRepoFeedbackObj = Parse-JsonLoose -RawText ([string]$crossRepoFeedback.output)
    if ($null -ne $crossRepoFeedbackObj) {
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "status") { $crossRepoFeedbackStatus = [string]$crossRepoFeedbackObj.status }
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "feedback_ingested_count") { try { $crossRepoFeedbackIngestedCount = [int]$crossRepoFeedbackObj.feedback_ingested_count } catch { $crossRepoFeedbackIngestedCount = 0 } }
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "repo_failure_count") { try { $crossRepoFeedbackRepoFailureCount = [int]$crossRepoFeedbackObj.repo_failure_count } catch { $crossRepoFeedbackRepoFailureCount = 0 } }
      $rolloutMatrixMissingControlCount = 0
      $rolloutMatrixMissingRepoStateCount = 0
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "rollout_matrix_missing_control_count") { try { $rolloutMatrixMissingControlCount = [int]$crossRepoFeedbackObj.rollout_matrix_missing_control_count } catch { $rolloutMatrixMissingControlCount = 0 } }
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "rollout_matrix_missing_repo_state_count") { try { $rolloutMatrixMissingRepoStateCount = [int]$crossRepoFeedbackObj.rollout_matrix_missing_repo_state_count } catch { $rolloutMatrixMissingRepoStateCount = 0 } }
      $crossRepoFeedbackRolloutMatrixGapCount = [int]$rolloutMatrixMissingControlCount + [int]$rolloutMatrixMissingRepoStateCount
      if ($crossRepoFeedbackObj.PSObject.Properties.Name -contains "report_path") { $crossRepoFeedbackReportPath = [string]$crossRepoFeedbackObj.report_path }
    } else {
      $retryRaw = & powershell -NoProfile -ExecutionPolicy Bypass -File $crossRepoFeedbackScript -RepoRoot $repoPath -AsJson 2>&1
      $retryObj = Parse-JsonLoose -RawText ([string]($retryRaw | Out-String))
      if ($null -ne $retryObj) {
        if ($retryObj.PSObject.Properties.Name -contains "status") { $crossRepoFeedbackStatus = [string]$retryObj.status }
        if ($retryObj.PSObject.Properties.Name -contains "feedback_ingested_count") { try { $crossRepoFeedbackIngestedCount = [int]$retryObj.feedback_ingested_count } catch { $crossRepoFeedbackIngestedCount = 0 } }
        if ($retryObj.PSObject.Properties.Name -contains "repo_failure_count") { try { $crossRepoFeedbackRepoFailureCount = [int]$retryObj.repo_failure_count } catch { $crossRepoFeedbackRepoFailureCount = 0 } }
        $rolloutMatrixMissingControlCount = 0
        $rolloutMatrixMissingRepoStateCount = 0
        if ($retryObj.PSObject.Properties.Name -contains "rollout_matrix_missing_control_count") { try { $rolloutMatrixMissingControlCount = [int]$retryObj.rollout_matrix_missing_control_count } catch { $rolloutMatrixMissingControlCount = 0 } }
        if ($retryObj.PSObject.Properties.Name -contains "rollout_matrix_missing_repo_state_count") { try { $rolloutMatrixMissingRepoStateCount = [int]$retryObj.rollout_matrix_missing_repo_state_count } catch { $rolloutMatrixMissingRepoStateCount = 0 } }
        $crossRepoFeedbackRolloutMatrixGapCount = [int]$rolloutMatrixMissingControlCount + [int]$rolloutMatrixMissingRepoStateCount
        if ($retryObj.PSObject.Properties.Name -contains "report_path") { $crossRepoFeedbackReportPath = [string]$retryObj.report_path }
      } else {
        $crossRepoFeedbackStatus = "PARSE_ERROR"
      }
    }
  }
  if ($crossRepoFeedback.exit_code -ne 0 -or $crossRepoFeedbackStatus -eq "PARSE_ERROR" -or $crossRepoFeedbackStatus -eq "alert") {
    [void]$alerts.Add(("cross repo feedback status={0} ingested_count={1} repo_failure_count={2}" -f $crossRepoFeedbackStatus, $crossRepoFeedbackIngestedCount, $crossRepoFeedbackRepoFailureCount))
  }
}

if ($hasTokenEfficiencyTrendScript) {
  $tokenEfficiencyTrendStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($tokenEfficiencyTrend.output)) {
    $tokenTrendObj = Parse-JsonLoose -RawText ([string]$tokenEfficiencyTrend.output)
    if ($null -ne $tokenTrendObj) {
      if ($tokenTrendObj.PSObject.Properties.Name -contains "status") { $tokenEfficiencyTrendStatus = [string]$tokenTrendObj.status }
      if ($tokenTrendObj.PSObject.Properties.Name -contains "history_count") { try { $tokenEfficiencyTrendHistoryCount = [int]$tokenTrendObj.history_count } catch { $tokenEfficiencyTrendHistoryCount = 0 } }
      if ($tokenTrendObj.PSObject.Properties.Name -contains "latest_value") { try { $tokenEfficiencyTrendLatestValue = [double]$tokenTrendObj.latest_value } catch { $tokenEfficiencyTrendLatestValue = 0 } }
    } else {
      $rawTokenTrendText = [string]$tokenEfficiencyTrend.output
      $statusMatch = [regex]::Match($rawTokenTrendText, '"status"\s*:\s*"([^"]+)"')
      $historyMatch = [regex]::Match($rawTokenTrendText, '"history_count"\s*:\s*([0-9]+)')
      $latestMatch = [regex]::Match($rawTokenTrendText, '"latest_value"\s*:\s*([0-9\.]+|null)')
      if ($statusMatch.Success) {
        $tokenEfficiencyTrendStatus = $statusMatch.Groups[1].Value
        if ($historyMatch.Success) { $tokenEfficiencyTrendHistoryCount = [int]$historyMatch.Groups[1].Value }
        if ($latestMatch.Success -and $latestMatch.Groups[1].Value -ne "null") {
          $tokenEfficiencyTrendLatestValue = [double]$latestMatch.Groups[1].Value
        }
      } else {
        $retryRaw = & powershell -NoProfile -ExecutionPolicy Bypass -File $tokenEfficiencyTrendScript -RepoRoot $repoPath -AsJson 2>&1
        $retryObj = Parse-JsonLoose -RawText ([string]($retryRaw | Out-String))
        if ($null -ne $retryObj) {
          if ($retryObj.PSObject.Properties.Name -contains "status") { $tokenEfficiencyTrendStatus = [string]$retryObj.status }
          if ($retryObj.PSObject.Properties.Name -contains "history_count") { try { $tokenEfficiencyTrendHistoryCount = [int]$retryObj.history_count } catch { $tokenEfficiencyTrendHistoryCount = 0 } }
          if ($retryObj.PSObject.Properties.Name -contains "latest_value") { try { $tokenEfficiencyTrendLatestValue = [double]$retryObj.latest_value } catch { $tokenEfficiencyTrendLatestValue = 0 } }
        } else {
          $tokenEfficiencyTrendStatus = "PARSE_ERROR"
        }
      }
    }
  }
  if ($tokenEfficiencyTrend.exit_code -ne 0 -or $tokenEfficiencyTrendStatus -eq "PARSE_ERROR" -or $tokenEfficiencyTrendStatus -eq "regressing") {
    [void]$alerts.Add(("token efficiency trend status={0} history_count={1}" -f $tokenEfficiencyTrendStatus, $tokenEfficiencyTrendHistoryCount))
  }
}

if ($hasSessionCompactionScript) {
  $sessionCompactionStatus = "UNKNOWN"
  if (-not [string]::IsNullOrWhiteSpace($sessionCompaction.output)) {
    $sessionCompactionObj = Parse-JsonLoose -RawText ([string]$sessionCompaction.output)
    if ($null -ne $sessionCompactionObj) {
      if ($sessionCompactionObj.PSObject.Properties.Name -contains "status") { $sessionCompactionStatus = [string]$sessionCompactionObj.status }
      if ($sessionCompactionObj.PSObject.Properties.Name -contains "recommend_compaction") { $sessionCompactionRecommend = [bool]$sessionCompactionObj.recommend_compaction }
      if ($sessionCompactionObj.PSObject.Properties.Name -contains "reason_count") { try { $sessionCompactionReasonCount = [int]$sessionCompactionObj.reason_count } catch { $sessionCompactionReasonCount = 0 } }
      if ($sessionCompactionObj.PSObject.Properties.Name -contains "reasons") { $sessionCompactionReasons = @($sessionCompactionObj.reasons) }
    } else {
      $rawSessionCompaction = [string]$sessionCompaction.output
      $statusMatch = [regex]::Match($rawSessionCompaction, "(?m)^session_compaction\.status=([A-Za-z_]+)\s*$")
      $recommendMatch = [regex]::Match($rawSessionCompaction, "(?m)^session_compaction\.recommend=(True|False|true|false)\s*$")
      $reasonCountMatch = [regex]::Match($rawSessionCompaction, "(?m)^session_compaction\.reason_count=([0-9]+)\s*$")
      $reasonsMatch = [regex]::Match($rawSessionCompaction, "(?m)^session_compaction\.reasons=(.*)$")
      if ($statusMatch.Success) {
        $sessionCompactionStatus = [string]$statusMatch.Groups[1].Value
        if ($recommendMatch.Success) { $sessionCompactionRecommend = [bool]::Parse([string]$recommendMatch.Groups[1].Value) }
        if ($reasonCountMatch.Success) { $sessionCompactionReasonCount = [int]$reasonCountMatch.Groups[1].Value }
        if ($reasonsMatch.Success -and -not [string]::IsNullOrWhiteSpace([string]$reasonsMatch.Groups[1].Value)) {
          $sessionCompactionReasons = @(([string]$reasonsMatch.Groups[1].Value).Split(";") | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        }
      } else {
        $sessionCompactionStatus = "PARSE_ERROR"
      }
    }
  }
  if ($sessionCompactionStatus -eq "PARSE_ERROR") {
    [void]$alerts.Add("session compaction check parse error")
  }
  if ($sessionCompactionRecommend) {
    [void]$alerts.Add(("session compaction recommended reason_count={0}" -f $sessionCompactionReasonCount))
  }
}

$updateTriggerAlertCount = 0
$orphanCustomSourceCount = 0
$staleProgressiveControlCount = 0
$notObservableControlCount = 0
$ruleDuplicationCount = 0
$controlRetirementActiveCandidateCount = 0
$controlRetirementOverdueCandidateCount = 0
$evidenceTemplateMissingFieldCount = 0
$targetRolloutMatrixMissingControlCount = 0
$targetRolloutMatrixMissingRepoStateCount = 0
$rolloutMetadataCoverageGapCount = 0
$rolloutMetadataOrphanCount = 0
$releaseDistributionPolicyDriftCount = 0
$tokenBalanceStatus = "UNKNOWN"
$tokenBalanceWarningCount = 0
$tokenBalanceViolationCount = 0
$proactiveSuggestionBalanceStatus = "UNKNOWN"
$proactiveSuggestionBalanceWarningCount = 0
$proactiveSuggestionBalanceViolationCount = 0
$externalBaselineStatus = "UNKNOWN"
$externalBaselineAdvisoryCount = 0
$externalBaselineWarnCount = 0
$autoRollbackTriggered = $false
$autoRollbackReasons = [System.Collections.Generic.List[string]]::new()
$autoRollbackAction = "none"
$autoRollbackPolicyPathNormalized = ""
if ($trigger.exit_code -ne 0) {
  $triggerObj = $null
  $rawTriggerText = [string]$trigger.output
  $triggerObj = Parse-JsonLoose -RawText $rawTriggerText
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "alert_count") {
    $updateTriggerAlertCount = [int]$triggerObj.alert_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "orphan_custom_source_count") {
    $orphanCustomSourceCount = [int]$triggerObj.orphan_custom_source_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "stale_progressive_control_count") {
    $staleProgressiveControlCount = [int]$triggerObj.stale_progressive_control_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "not_observable_control_count") {
    $notObservableControlCount = [int]$triggerObj.not_observable_control_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "rule_duplication_count") {
    $ruleDuplicationCount = [int]$triggerObj.rule_duplication_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "control_retirement_active_candidate_count") {
    $controlRetirementActiveCandidateCount = [int]$triggerObj.control_retirement_active_candidate_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "control_retirement_overdue_candidate_count") {
    $controlRetirementOverdueCandidateCount = [int]$triggerObj.control_retirement_overdue_candidate_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "evidence_template_missing_field_count") {
    $evidenceTemplateMissingFieldCount = [int]$triggerObj.evidence_template_missing_field_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "target_rollout_matrix_missing_control_count") {
    $targetRolloutMatrixMissingControlCount = [int]$triggerObj.target_rollout_matrix_missing_control_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "target_rollout_matrix_missing_repo_state_count") {
    $targetRolloutMatrixMissingRepoStateCount = [int]$triggerObj.target_rollout_matrix_missing_repo_state_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "rollout_metadata_coverage_gap_count") {
    $rolloutMetadataCoverageGapCount = [int]$triggerObj.rollout_metadata_coverage_gap_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "rollout_metadata_orphan_count") {
    $rolloutMetadataOrphanCount = [int]$triggerObj.rollout_metadata_orphan_count
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "release_distribution_policy_drift_count") {
    $releaseDistributionPolicyDriftCount = [int]$triggerObj.release_distribution_policy_drift_count
  }
  if ($null -eq $triggerObj) {
    $alertCountMatch = [regex]::Match($rawTriggerText, "(?m)^alert_count=([0-9]+)\s*$")
    $orphanCountMatch = [regex]::Match($rawTriggerText, "(?m)^orphan_custom_source_count=([0-9]+)\s*$")
    $staleProgressiveCountMatch = [regex]::Match($rawTriggerText, "(?m)^stale_progressive_control_count=([0-9]+)\s*$")
    $notObservableCountMatch = [regex]::Match($rawTriggerText, "(?m)^not_observable_control_count=([0-9]+)\s*$")
    $ruleDupCountMatch = [regex]::Match($rawTriggerText, "(?m)^rule_duplication_count=([0-9]+)\s*$")
    $controlRetirementActiveCountMatch = [regex]::Match($rawTriggerText, "(?m)^control_retirement_active_candidate_count=([0-9]+)\s*$")
    $controlRetirementOverdueCountMatch = [regex]::Match($rawTriggerText, "(?m)^control_retirement_overdue_candidate_count=([0-9]+)\s*$")
    $evidenceTemplateMissingFieldCountMatch = [regex]::Match($rawTriggerText, "(?m)^evidence_template_missing_field_count=([0-9]+)\s*$")
    $targetRolloutMissingControlCountMatch = [regex]::Match($rawTriggerText, "(?m)^target_rollout_matrix_missing_control_count=([0-9]+)\s*$")
    $targetRolloutMissingRepoStateCountMatch = [regex]::Match($rawTriggerText, "(?m)^target_rollout_matrix_missing_repo_state_count=([0-9]+)\s*$")
    $rolloutMetadataCoverageGapMatch = [regex]::Match($rawTriggerText, "(?m)^rollout_metadata_coverage_gap_count=([0-9]+)\s*$")
    $rolloutMetadataOrphanMatch = [regex]::Match($rawTriggerText, "(?m)^rollout_metadata_orphan_count=([0-9]+)\s*$")
    $driftCountMatch = [regex]::Match($rawTriggerText, "(?m)^release_distribution_policy_drift_count=([0-9]+)\s*$")
    if ($alertCountMatch.Success) {
      $updateTriggerAlertCount = [int]$alertCountMatch.Groups[1].Value
    }
    if ($orphanCountMatch.Success) {
      $orphanCustomSourceCount = [int]$orphanCountMatch.Groups[1].Value
    }
    if ($staleProgressiveCountMatch.Success) {
      $staleProgressiveControlCount = [int]$staleProgressiveCountMatch.Groups[1].Value
    }
    if ($notObservableCountMatch.Success) {
      $notObservableControlCount = [int]$notObservableCountMatch.Groups[1].Value
    }
    if ($ruleDupCountMatch.Success) {
      $ruleDuplicationCount = [int]$ruleDupCountMatch.Groups[1].Value
    }
    if ($controlRetirementActiveCountMatch.Success) {
      $controlRetirementActiveCandidateCount = [int]$controlRetirementActiveCountMatch.Groups[1].Value
    }
    if ($controlRetirementOverdueCountMatch.Success) {
      $controlRetirementOverdueCandidateCount = [int]$controlRetirementOverdueCountMatch.Groups[1].Value
    }
    if ($evidenceTemplateMissingFieldCountMatch.Success) {
      $evidenceTemplateMissingFieldCount = [int]$evidenceTemplateMissingFieldCountMatch.Groups[1].Value
    }
    if ($targetRolloutMissingControlCountMatch.Success) {
      $targetRolloutMatrixMissingControlCount = [int]$targetRolloutMissingControlCountMatch.Groups[1].Value
    }
    if ($targetRolloutMissingRepoStateCountMatch.Success) {
      $targetRolloutMatrixMissingRepoStateCount = [int]$targetRolloutMissingRepoStateCountMatch.Groups[1].Value
    }
    if ($rolloutMetadataCoverageGapMatch.Success) {
      $rolloutMetadataCoverageGapCount = [int]$rolloutMetadataCoverageGapMatch.Groups[1].Value
    }
    if ($rolloutMetadataOrphanMatch.Success) {
      $rolloutMetadataOrphanCount = [int]$rolloutMetadataOrphanMatch.Groups[1].Value
    }
    if ($driftCountMatch.Success) {
      $releaseDistributionPolicyDriftCount = [int]$driftCountMatch.Groups[1].Value
    }
  }
  if ($updateTriggerAlertCount -gt 0) {
    [void]$alerts.Add(("update triggers alert count={0}" -f $updateTriggerAlertCount))
  } else {
    [void]$alerts.Add("check-update-triggers failed")
  }
}

if (-not $hasTokenBalanceScript) {
  $tokenBalanceStatus = "UNAVAILABLE"
} elseif (-not [string]::IsNullOrWhiteSpace($tokenBalance.output)) {
  $rawTokenBalanceText = [string]$tokenBalance.output
  $statusMatch = [regex]::Match($rawTokenBalanceText, "(?m)^token_balance\.status=([A-Z_]+)\s*$")
  $warningMatch = [regex]::Match($rawTokenBalanceText, "(?m)^token_balance\.warning_count=([0-9]+)\s*$")
  $violationMatch = [regex]::Match($rawTokenBalanceText, "(?m)^token_balance\.violation_count=([0-9]+)\s*$")
  if ($statusMatch.Success) {
    $tokenBalanceStatus = $statusMatch.Groups[1].Value
  }
  if ($warningMatch.Success) {
    $tokenBalanceWarningCount = [int]$warningMatch.Groups[1].Value
  }
  if ($violationMatch.Success) {
    $tokenBalanceViolationCount = [int]$violationMatch.Groups[1].Value
  } else {
    $tokenBalanceObj = Parse-JsonLoose -RawText $rawTokenBalanceText
    if ($null -ne $tokenBalanceObj) {
      if ($tokenBalanceObj.PSObject.Properties.Name -contains "status") {
        $tokenBalanceStatus = [string]$tokenBalanceObj.status
      }
      if ($tokenBalanceObj.PSObject.Properties.Name -contains "warning_count") {
        $tokenBalanceWarningCount = [int]$tokenBalanceObj.warning_count
      }
      if ($tokenBalanceObj.PSObject.Properties.Name -contains "violation_count") {
        $tokenBalanceViolationCount = [int]$tokenBalanceObj.violation_count
      }
    }
  }
}

if ($hasTokenBalanceScript -and ($tokenBalance.exit_code -ne 0 -or $tokenBalanceViolationCount -gt 0 -or $tokenBalanceStatus -eq "ALERT")) {
  [void]$alerts.Add(("token balance status={0} violation_count={1}" -f $tokenBalanceStatus, $tokenBalanceViolationCount))
}

if (-not $hasProactiveSuggestionScript) {
  $proactiveSuggestionBalanceStatus = "UNAVAILABLE"
} elseif (-not [string]::IsNullOrWhiteSpace($proactiveSuggestion.output)) {
  $proactiveObj = Parse-JsonLoose -RawText ([string]$proactiveSuggestion.output)
  if ($null -ne $proactiveObj) {
    if ($proactiveObj.PSObject.Properties.Name -contains "status") {
      $proactiveSuggestionBalanceStatus = [string]$proactiveObj.status
    }
    if ($proactiveObj.PSObject.Properties.Name -contains "warning_count") {
      try { $proactiveSuggestionBalanceWarningCount = [int]$proactiveObj.warning_count } catch { $proactiveSuggestionBalanceWarningCount = 0 }
    }
    if ($proactiveObj.PSObject.Properties.Name -contains "violation_count") {
      try { $proactiveSuggestionBalanceViolationCount = [int]$proactiveObj.violation_count } catch { $proactiveSuggestionBalanceViolationCount = 0 }
    }
  } else {
    $rawProactiveText = [string]$proactiveSuggestion.output
    $statusMatchJson = [regex]::Match($rawProactiveText, '"status"\s*:\s*"([^"]+)"')
    $warningMatchJson = [regex]::Match($rawProactiveText, '"warning_count"\s*:\s*([0-9]+)')
    $violationMatchJson = [regex]::Match($rawProactiveText, '"violation_count"\s*:\s*([0-9]+)')
    $statusMatchKv = [regex]::Match($rawProactiveText, '(?m)^proactive_suggestion_balance\.status=([A-Z_]+)\s*$')
    $warningMatchKv = [regex]::Match($rawProactiveText, '(?m)^proactive_suggestion_balance\.warning_count=([0-9]+)\s*$')
    $violationMatchKv = [regex]::Match($rawProactiveText, '(?m)^proactive_suggestion_balance\.violation_count=([0-9]+)\s*$')
    if ($statusMatchJson.Success -or $statusMatchKv.Success) {
      if ($statusMatchJson.Success) {
        $proactiveSuggestionBalanceStatus = $statusMatchJson.Groups[1].Value
      } else {
        $proactiveSuggestionBalanceStatus = $statusMatchKv.Groups[1].Value
      }
      if ($warningMatchJson.Success) {
        $proactiveSuggestionBalanceWarningCount = [int]$warningMatchJson.Groups[1].Value
      } elseif ($warningMatchKv.Success) {
        $proactiveSuggestionBalanceWarningCount = [int]$warningMatchKv.Groups[1].Value
      }
      if ($violationMatchJson.Success) {
        $proactiveSuggestionBalanceViolationCount = [int]$violationMatchJson.Groups[1].Value
      } elseif ($violationMatchKv.Success) {
        $proactiveSuggestionBalanceViolationCount = [int]$violationMatchKv.Groups[1].Value
      }
    } elseif ($proactiveSuggestion.exit_code -eq 0) {
      $proactiveSuggestionBalanceStatus = "OK"
    } else {
      $proactiveSuggestionBalanceStatus = "PARSE_ERROR"
    }
  }
}

if ($hasProactiveSuggestionScript -and ($proactiveSuggestion.exit_code -ne 0 -or $proactiveSuggestionBalanceStatus -eq "ALERT" -or $proactiveSuggestionBalanceStatus -eq "PARSE_ERROR" -or $proactiveSuggestionBalanceViolationCount -gt 0)) {
  [void]$alerts.Add(("proactive suggestion balance status={0} violation_count={1}" -f $proactiveSuggestionBalanceStatus, $proactiveSuggestionBalanceViolationCount))
}

if (-not $hasExternalBaselineScript) {
  $externalBaselineStatus = "UNAVAILABLE"
} elseif ($externalBaseline.exit_code -eq 0 -and -not [string]::IsNullOrWhiteSpace($externalBaseline.output)) {
  $externalObj = $null
  $rawText = [string]$externalBaseline.output
  try {
    $externalObj = $rawText | ConvertFrom-Json
  } catch {
    $start = $rawText.IndexOf("{")
    $end = $rawText.LastIndexOf("}")
    if ($start -ge 0 -and $end -ge $start) {
      try {
        $externalObj = $rawText.Substring($start, $end - $start + 1) | ConvertFrom-Json
      } catch {
        $externalObj = $null
      }
    }
  }
  if ($null -ne $externalObj) {
    if ($externalObj.PSObject.Properties.Name -contains "status") {
      $externalBaselineStatus = [string]$externalObj.status
    }
    if ($null -ne $externalObj.summary -and $externalObj.summary.PSObject.Properties.Name -contains "advisory_count") {
      $externalBaselineAdvisoryCount = [int]$externalObj.summary.advisory_count
    }
    if ($null -ne $externalObj.summary -and $externalObj.summary.PSObject.Properties.Name -contains "warn_count") {
      $externalBaselineWarnCount = [int]$externalObj.summary.warn_count
    }
  } else {
    $statusMatch = [regex]::Match($rawText, "(?m)^status=([A-Z]+)\s*$")
    $advisoryMatch = [regex]::Match($rawText, "(?m)^advisory_count=([0-9]+)\s*$")
    $warnMatch = [regex]::Match($rawText, "(?m)^warn_count=([0-9]+)\s*$")
    if ($statusMatch.Success) {
      $externalBaselineStatus = $statusMatch.Groups[1].Value
      if ($advisoryMatch.Success) {
        $externalBaselineAdvisoryCount = [int]$advisoryMatch.Groups[1].Value
      }
      if ($warnMatch.Success) {
        $externalBaselineWarnCount = [int]$warnMatch.Groups[1].Value
      }
    } else {
      $externalBaselineStatus = "PARSE_ERROR"
    }
  }
}

if (Test-Path -LiteralPath $autoRollbackPolicyPath -PathType Leaf) {
  $autoRollbackPolicyPathNormalized = ($autoRollbackPolicyPath -replace '\\', '/')
}

$autoRollbackEnabled = $false
$triggerOnTokenBalanceAlert = $true
$skillTriggerEvalPassRateBelow = $null
$skillTriggerEvalFalseTriggerRateAbove = $null
$highRiskWithoutExplicitPathCountGt = -1
$externalBaselineWarnCountGt = -1
if (Test-Path -LiteralPath $autoRollbackPolicyPath -PathType Leaf) {
  $autoRollbackPolicyRaw = Get-Content -LiteralPath $autoRollbackPolicyPath -Raw -Encoding UTF8
  $autoRollbackPolicy = Parse-JsonLoose -RawText ([string]$autoRollbackPolicyRaw)
  if ($null -ne $autoRollbackPolicy) {
    if ($autoRollbackPolicy.PSObject.Properties.Name -contains "enabled") {
      $autoRollbackEnabled = [bool]$autoRollbackPolicy.enabled
    }
    if ($null -ne $autoRollbackPolicy.PSObject.Properties['trigger_when']) {
      $triggerWhen = $autoRollbackPolicy.trigger_when
      if ($null -ne $triggerWhen.PSObject.Properties['token_balance_alert_or_violation']) {
        $triggerOnTokenBalanceAlert = [bool]$triggerWhen.token_balance_alert_or_violation
      }
      if ($null -ne $triggerWhen.PSObject.Properties['skill_trigger_eval_pass_rate_below']) {
        try { $skillTriggerEvalPassRateBelow = [double]$triggerWhen.skill_trigger_eval_pass_rate_below } catch { $skillTriggerEvalPassRateBelow = $null }
      }
      if ($null -ne $triggerWhen.PSObject.Properties['skill_trigger_eval_false_trigger_rate_above']) {
        try { $skillTriggerEvalFalseTriggerRateAbove = [double]$triggerWhen.skill_trigger_eval_false_trigger_rate_above } catch { $skillTriggerEvalFalseTriggerRateAbove = $null }
      }
      if ($null -ne $triggerWhen.PSObject.Properties['high_risk_without_explicit_path_count_gt']) {
        try { $highRiskWithoutExplicitPathCountGt = [int]$triggerWhen.high_risk_without_explicit_path_count_gt } catch { $highRiskWithoutExplicitPathCountGt = -1 }
      }
      if ($null -ne $triggerWhen.PSObject.Properties['external_baseline_warn_count_gt']) {
        try { $externalBaselineWarnCountGt = [int]$triggerWhen.external_baseline_warn_count_gt } catch { $externalBaselineWarnCountGt = -1 }
      }
    }
  }
}

if ($autoRollbackEnabled) {
  if ($triggerOnTokenBalanceAlert -and ($tokenBalanceStatus -eq "ALERT" -or $tokenBalanceViolationCount -gt 0)) {
    [void]$autoRollbackReasons.Add(("token_balance:{0}/{1}" -f $tokenBalanceStatus, [int]$tokenBalanceViolationCount))
  }
  if ($null -ne $skillTriggerEvalPassRateBelow -and $null -ne $skillTriggerEvalValidationPassRate -and [double]$skillTriggerEvalValidationPassRate -lt [double]$skillTriggerEvalPassRateBelow) {
    [void]$autoRollbackReasons.Add(("skill_trigger_eval_validation_pass_rate:{0}<${1}" -f [double]$skillTriggerEvalValidationPassRate, [double]$skillTriggerEvalPassRateBelow))
  }
  if ($null -ne $skillTriggerEvalFalseTriggerRateAbove -and $null -ne $skillTriggerEvalValidationFalseTriggerRate -and [double]$skillTriggerEvalValidationFalseTriggerRate -gt [double]$skillTriggerEvalFalseTriggerRateAbove) {
    [void]$autoRollbackReasons.Add(("skill_trigger_eval_validation_false_trigger_rate:{0}>${1}" -f [double]$skillTriggerEvalValidationFalseTriggerRate, [double]$skillTriggerEvalFalseTriggerRateAbove))
  }
  if ($highRiskWithoutExplicitPathCountGt -ge 0 -and [int]$highRiskWithoutExplicitPathCount -gt [int]$highRiskWithoutExplicitPathCountGt) {
    [void]$autoRollbackReasons.Add(("high_risk_without_explicit_path_count:{0}>${1}" -f [int]$highRiskWithoutExplicitPathCount, [int]$highRiskWithoutExplicitPathCountGt))
  }
  if ($externalBaselineWarnCountGt -ge 0 -and [int]$externalBaselineWarnCount -gt [int]$externalBaselineWarnCountGt) {
    [void]$autoRollbackReasons.Add(("external_baseline_warn_count:{0}>${1}" -f [int]$externalBaselineWarnCount, [int]$externalBaselineWarnCountGt))
  }
}

if ($autoRollbackEnabled -and $autoRollbackReasons.Count -gt 0) {
  $autoRollbackTriggered = $true
  if ($hasRollbackDrillScript) {
    if ($rollbackDrill.exit_code -eq 0 -and ($rollbackDrillStatus -eq "ok" -or $rollbackDrillStatus -eq "planned")) {
      $autoRollbackAction = "rollback_path_entered:run-rollback-drill(safe)"
    } else {
      $autoRollbackAction = "rollback_path_attempted:run-rollback-drill(safe)-failed"
    }
  } else {
    $autoRollbackAction = "rollback_path_unavailable:run-rollback-drill_missing"
  }
  [void]$alerts.Add(("auto rollback triggered reason_count={0} action={1}" -f [int]$autoRollbackReasons.Count, $autoRollbackAction))
}

$controlPlaneTopNoisyControls = [System.Collections.Generic.List[string]]::new()
if ($updateTriggerAlertCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("update_trigger_alerts:{0}" -f [int]$updateTriggerAlertCount)) }
if ($staleProgressiveControlCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("stale_progressive_controls:{0}" -f [int]$staleProgressiveControlCount)) }
if ($notObservableControlCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("not_observable_controls:{0}" -f [int]$notObservableControlCount)) }
if ($rolloutMetadataCoverageGapCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("rollout_metadata_coverage_gap:{0}" -f [int]$rolloutMetadataCoverageGapCount)) }
if ($targetRolloutMatrixMissingControlCount -gt 0 -or $targetRolloutMatrixMissingRepoStateCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("target_rollout_matrix_gap:{0}/{1}" -f [int]$targetRolloutMatrixMissingControlCount, [int]$targetRolloutMatrixMissingRepoStateCount)) }
if ($controlRetirementOverdueCandidateCount -gt 0) { [void]$controlPlaneTopNoisyControls.Add(("retirement_overdue_candidates:{0}" -f [int]$controlRetirementOverdueCandidateCount)) }
if ($controlPlaneTopNoisyControls.Count -eq 0) { [void]$controlPlaneTopNoisyControls.Add("none") }
$controlPlaneTopNoisyControlsText = [string]::Join(";", @($controlPlaneTopNoisyControls | Select-Object -First 3))

$controlPlaneBypassedAdvisories = [System.Collections.Generic.List[string]]::new()
if ($proactiveSuggestionBalanceWarningCount -gt 0) { [void]$controlPlaneBypassedAdvisories.Add(("proactive_suggestion_balance:{0}" -f [int]$proactiveSuggestionBalanceWarningCount)) }
if ($externalBaselineAdvisoryCount -gt 0) { [void]$controlPlaneBypassedAdvisories.Add(("external_baseline_advisory:{0}" -f [int]$externalBaselineAdvisoryCount)) }
if ($tokenBalanceWarningCount -gt 0) { [void]$controlPlaneBypassedAdvisories.Add(("token_balance_warning:{0}" -f [int]$tokenBalanceWarningCount)) }
if ($controlPlaneBypassedAdvisories.Count -eq 0) { [void]$controlPlaneBypassedAdvisories.Add("none") }
$controlPlaneBypassedAdvisoriesText = [string]::Join(";", @($controlPlaneBypassedAdvisories | Select-Object -First 3))

$result = [pscustomobject]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  repo_root = ($repoPath -replace '\\', '/')
  cadence = "weekly"
  ok = ($alerts.Count -eq 0)
  alerts = @($alerts)
  summary = [pscustomobject]@{
    doctor_exit_code = [int]$doctor.exit_code
    doctor_health = $doctorHealth
    doctor_elapsed_ms = [int]$doctorElapsedMs
    gate_latency_delta_ms = $gateLatencyDeltaMs
    observe_overdue = $observeOverdue
    waiver_remind_count = [int]$waiverRemindCount
    waiver_block_count = [int]$waiverBlockCount
    metrics_exit_code = [int]$metrics.exit_code
    update_trigger_alert_count = [int]$updateTriggerAlertCount
    orphan_custom_source_count = [int]$orphanCustomSourceCount
    stale_progressive_control_count = [int]$staleProgressiveControlCount
    not_observable_control_count = [int]$notObservableControlCount
    rule_duplication_count = [int]$ruleDuplicationCount
    control_retirement_active_candidate_count = [int]$controlRetirementActiveCandidateCount
    control_retirement_overdue_candidate_count = [int]$controlRetirementOverdueCandidateCount
    evidence_template_missing_field_count = [int]$evidenceTemplateMissingFieldCount
    target_rollout_matrix_missing_control_count = [int]$targetRolloutMatrixMissingControlCount
    target_rollout_matrix_missing_repo_state_count = [int]$targetRolloutMatrixMissingRepoStateCount
    rollout_metadata_coverage_gap_count = [int]$rolloutMetadataCoverageGapCount
    rollout_metadata_orphan_count = [int]$rolloutMetadataOrphanCount
    control_plane_top_noisy_controls = $controlPlaneTopNoisyControlsText
    control_plane_most_bypassed_advisories = $controlPlaneBypassedAdvisoriesText
    release_distribution_policy_drift_count = [int]$releaseDistributionPolicyDriftCount
    token_balance_status = $tokenBalanceStatus
    token_balance_warning_count = [int]$tokenBalanceWarningCount
    token_balance_violation_count = [int]$tokenBalanceViolationCount
    proactive_suggestion_balance_status = $proactiveSuggestionBalanceStatus
    proactive_suggestion_balance_warning_count = [int]$proactiveSuggestionBalanceWarningCount
    proactive_suggestion_balance_violation_count = [int]$proactiveSuggestionBalanceViolationCount
    external_baseline_status = $externalBaselineStatus
    external_baseline_advisory_count = [int]$externalBaselineAdvisoryCount
    external_baseline_warn_count = [int]$externalBaselineWarnCount
    skill_trigger_eval_status = $skillTriggerEvalStatus
    skill_trigger_eval_grouped_query_count = [int]$skillTriggerEvalGroupedQueryCount
    skill_trigger_eval_validation_pass_rate = $skillTriggerEvalValidationPassRate
    skill_trigger_eval_validation_false_trigger_rate = $skillTriggerEvalValidationFalseTriggerRate
    risk_tier_approval_status = $riskTierApprovalStatus
    high_risk_without_explicit_path_count = [int]$highRiskWithoutExplicitPathCount
    rollout_promotion_status = $rolloutPromotionStatus
    rollout_observe_window_violation_count = [int]$rolloutObserveWindowViolationCount
    failure_replay_status = $failureReplayStatus
    failure_replay_top5_coverage_rate = [Math]::Round([double]$failureReplayTop5CoverageRate, 6)
    failure_replay_missing_top5_count = [int]$failureReplayMissingTop5Count
    rollback_drill_status = $rollbackDrillStatus
    rollback_drill_recovery_ms = [int]$rollbackDrillRecoveryMs
    skill_family_health_status = $skillFamilyHealthStatus
    skill_family_active_family_duplicate_count = [int]$skillFamilyActiveFamilyDuplicateCount
    skill_family_low_health_target_state_count = [int]$skillFamilyLowHealthTargetStateCount
    skill_family_active_family_avg_health_score = [Math]::Round([double]$skillFamilyActiveFamilyAvgHealthScore, 6)
    skill_lifecycle_health_status = $skillLifecycleHealthStatus
    skill_lifecycle_retire_candidate_count = [int]$skillLifecycleRetireCandidateCount
    skill_lifecycle_retired_avg_latency_days = [Math]::Round([double]$skillLifecycleRetiredAvgLatencyDays, 6)
    skill_lifecycle_quality_impact_delta = [Math]::Round([double]$skillLifecycleQualityImpactDelta, 6)
    cross_repo_compatibility_status = $crossRepoCompatibilityStatus
    cross_repo_compatibility_repo_failure_count = [int]$crossRepoCompatibilityRepoFailureCount
    cross_repo_feedback_status = $crossRepoFeedbackStatus
    cross_repo_feedback_ingested_count = [int]$crossRepoFeedbackIngestedCount
    cross_repo_feedback_repo_failure_count = [int]$crossRepoFeedbackRepoFailureCount
    cross_repo_feedback_rollout_matrix_gap_count = [int]$crossRepoFeedbackRolloutMatrixGapCount
    cross_repo_feedback_report_path = $crossRepoFeedbackReportPath
    token_efficiency_trend_status = $tokenEfficiencyTrendStatus
    token_efficiency_trend_history_count = [int]$tokenEfficiencyTrendHistoryCount
    token_efficiency_trend_latest_value = [Math]::Round([double]$tokenEfficiencyTrendLatestValue, 6)
    session_compaction_status = $sessionCompactionStatus
    session_compaction_recommend = [bool]$sessionCompactionRecommend
    session_compaction_reason_count = [int]$sessionCompactionReasonCount
    session_compaction_reasons = @($sessionCompactionReasons)
    quality_first_pass_rate = $qualityFirstPassRate
    quality_rework_after_clarification_rate = $qualityReworkAfterClarificationRate
    token_average_response = $tokenAverageResponse
    token_per_effective_conclusion = $tokenPerEffectiveConclusion
    slo_error_budget_status = $sloErrorBudgetStatus
    slo_gate_pass_rate = $sloGatePassRate
    error_budget_burn_rate = $errorBudgetBurnRate
    error_budget_remaining = $errorBudgetRemaining
    auto_rollback_triggered = [bool]$autoRollbackTriggered
    auto_rollback_reason_count = [int]$autoRollbackReasons.Count
    auto_rollback_reasons = @($autoRollbackReasons.ToArray())
    auto_rollback_action = $autoRollbackAction
    auto_rollback_policy_path = $autoRollbackPolicyPathNormalized
    update_trigger_exit_code = [int]$trigger.exit_code
    skill_trigger_eval_exit_code = [int]$skillTriggerEval.exit_code
    risk_tier_approval_exit_code = [int]$riskTierApproval.exit_code
    rollout_promotion_exit_code = [int]$rolloutPromotion.exit_code
    failure_replay_exit_code = [int]$failureReplay.exit_code
    rollback_drill_exit_code = [int]$rollbackDrill.exit_code
    skill_family_health_exit_code = [int]$skillFamilyHealth.exit_code
    skill_lifecycle_health_exit_code = [int]$skillLifecycleHealth.exit_code
    cross_repo_compatibility_exit_code = [int]$crossRepoCompatibility.exit_code
    cross_repo_feedback_exit_code = [int]$crossRepoFeedback.exit_code
    token_efficiency_trend_exit_code = [int]$tokenEfficiencyTrend.exit_code
    session_compaction_exit_code = [int]$sessionCompaction.exit_code
    token_balance_exit_code = [int]$tokenBalance.exit_code
    proactive_suggestion_balance_exit_code = [int]$proactiveSuggestion.exit_code
    external_baseline_exit_code = [int]$externalBaseline.exit_code
  }
  steps = @(
    [pscustomobject]@{ name = "doctor"; exit_code = [int]$doctor.exit_code },
    [pscustomobject]@{ name = "rollout-status"; exit_code = [int]$rollout.exit_code },
    [pscustomobject]@{ name = "check-waivers"; exit_code = [int]$waiver.exit_code },
    [pscustomobject]@{ name = "collect-governance-metrics"; exit_code = [int]$metrics.exit_code },
    [pscustomobject]@{ name = "check-update-triggers"; exit_code = [int]$trigger.exit_code },
    [pscustomobject]@{ name = "check-skill-trigger-evals"; exit_code = [int]$skillTriggerEval.exit_code },
    [pscustomobject]@{ name = "check-risk-tier-approval"; exit_code = [int]$riskTierApproval.exit_code },
    [pscustomobject]@{ name = "check-rollout-promotion-readiness"; exit_code = [int]$rolloutPromotion.exit_code },
    [pscustomobject]@{ name = "check-failure-replay-readiness"; exit_code = [int]$failureReplay.exit_code },
    [pscustomobject]@{ name = "run-rollback-drill"; exit_code = [int]$rollbackDrill.exit_code },
    [pscustomobject]@{ name = "check-skill-family-health"; exit_code = [int]$skillFamilyHealth.exit_code },
    [pscustomobject]@{ name = "check-skill-lifecycle-health"; exit_code = [int]$skillLifecycleHealth.exit_code },
    [pscustomobject]@{ name = "check-cross-repo-compatibility"; exit_code = [int]$crossRepoCompatibility.exit_code },
    [pscustomobject]@{ name = "check-cross-repo-feedback"; exit_code = [int]$crossRepoFeedback.exit_code },
    [pscustomobject]@{ name = "check-token-efficiency-trend"; exit_code = [int]$tokenEfficiencyTrend.exit_code },
    [pscustomobject]@{ name = "check-session-compaction-trigger"; exit_code = [int]$sessionCompaction.exit_code },
    [pscustomobject]@{ name = "check-token-balance"; exit_code = [int]$tokenBalance.exit_code },
    [pscustomobject]@{ name = "check-proactive-suggestion-balance"; exit_code = [int]$proactiveSuggestion.exit_code },
    [pscustomobject]@{ name = "check-external-baselines"; exit_code = [int]$externalBaseline.exit_code }
  )
}

$alertSnapshotPath = ""
if ($writeAlertSnapshot) {
  $alertSnapshotPath = Write-AlertSnapshot -ReviewResult $result -RootPath $repoPath
}
$result | Add-Member -NotePropertyName alert_snapshot_path -NotePropertyValue ($alertSnapshotPath -replace '\\', '/') -Force

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($result.ok) { exit 0 } else { exit 1 }
}

Write-Host "RECURRING_REVIEW"
Write-Host ("generated_at={0}" -f $result.generated_at)
Write-Host ("repo_root={0}" -f $result.repo_root)
Write-Host ("cadence={0}" -f $result.cadence)
Write-Host ("doctor_health={0}" -f $result.summary.doctor_health)
Write-Host ("doctor_elapsed_ms={0}" -f $result.summary.doctor_elapsed_ms)
Write-Host ("gate_latency_delta_ms={0}" -f $result.summary.gate_latency_delta_ms)
Write-Host ("observe_overdue={0}" -f $result.summary.observe_overdue)
Write-Host ("waiver_remind_count={0}" -f $result.summary.waiver_remind_count)
Write-Host ("waiver_block_count={0}" -f $result.summary.waiver_block_count)
Write-Host ("update_trigger_alert_count={0}" -f $result.summary.update_trigger_alert_count)
Write-Host ("orphan_custom_source_count={0}" -f $result.summary.orphan_custom_source_count)
Write-Host ("stale_progressive_control_count={0}" -f $result.summary.stale_progressive_control_count)
Write-Host ("not_observable_control_count={0}" -f $result.summary.not_observable_control_count)
Write-Host ("rule_duplication_count={0}" -f $result.summary.rule_duplication_count)
Write-Host ("control_retirement_active_candidate_count={0}" -f $result.summary.control_retirement_active_candidate_count)
Write-Host ("control_retirement_overdue_candidate_count={0}" -f $result.summary.control_retirement_overdue_candidate_count)
Write-Host ("evidence_template_missing_field_count={0}" -f $result.summary.evidence_template_missing_field_count)
Write-Host ("target_rollout_matrix_missing_control_count={0}" -f $result.summary.target_rollout_matrix_missing_control_count)
Write-Host ("target_rollout_matrix_missing_repo_state_count={0}" -f $result.summary.target_rollout_matrix_missing_repo_state_count)
Write-Host ("rollout_metadata_coverage_gap_count={0}" -f $result.summary.rollout_metadata_coverage_gap_count)
Write-Host ("rollout_metadata_orphan_count={0}" -f $result.summary.rollout_metadata_orphan_count)
Write-Host ("control_plane_top_noisy_controls={0}" -f $result.summary.control_plane_top_noisy_controls)
Write-Host ("control_plane_most_bypassed_advisories={0}" -f $result.summary.control_plane_most_bypassed_advisories)
Write-Host ("release_distribution_policy_drift_count={0}" -f $result.summary.release_distribution_policy_drift_count)
Write-Host ("token_balance_status={0}" -f $result.summary.token_balance_status)
Write-Host ("token_balance_warning_count={0}" -f $result.summary.token_balance_warning_count)
Write-Host ("token_balance_violation_count={0}" -f $result.summary.token_balance_violation_count)
Write-Host ("proactive_suggestion_balance_status={0}" -f $result.summary.proactive_suggestion_balance_status)
Write-Host ("proactive_suggestion_balance_warning_count={0}" -f $result.summary.proactive_suggestion_balance_warning_count)
Write-Host ("proactive_suggestion_balance_violation_count={0}" -f $result.summary.proactive_suggestion_balance_violation_count)
Write-Host ("external_baseline_status={0}" -f $result.summary.external_baseline_status)
Write-Host ("external_baseline_advisory_count={0}" -f $result.summary.external_baseline_advisory_count)
Write-Host ("external_baseline_warn_count={0}" -f $result.summary.external_baseline_warn_count)
Write-Host ("skill_trigger_eval_status={0}" -f $result.summary.skill_trigger_eval_status)
Write-Host ("skill_trigger_eval_grouped_query_count={0}" -f $result.summary.skill_trigger_eval_grouped_query_count)
Write-Host ("skill_trigger_eval_validation_pass_rate={0}" -f $result.summary.skill_trigger_eval_validation_pass_rate)
Write-Host ("skill_trigger_eval_validation_false_trigger_rate={0}" -f $result.summary.skill_trigger_eval_validation_false_trigger_rate)
Write-Host ("risk_tier_approval_status={0}" -f $result.summary.risk_tier_approval_status)
Write-Host ("high_risk_without_explicit_path_count={0}" -f $result.summary.high_risk_without_explicit_path_count)
Write-Host ("rollout_promotion_status={0}" -f $result.summary.rollout_promotion_status)
Write-Host ("rollout_observe_window_violation_count={0}" -f $result.summary.rollout_observe_window_violation_count)
Write-Host ("failure_replay_status={0}" -f $result.summary.failure_replay_status)
Write-Host ("failure_replay_top5_coverage_rate={0}" -f $result.summary.failure_replay_top5_coverage_rate)
Write-Host ("failure_replay_missing_top5_count={0}" -f $result.summary.failure_replay_missing_top5_count)
Write-Host ("rollback_drill_status={0}" -f $result.summary.rollback_drill_status)
Write-Host ("rollback_drill_recovery_ms={0}" -f $result.summary.rollback_drill_recovery_ms)
Write-Host ("skill_family_health_status={0}" -f $result.summary.skill_family_health_status)
Write-Host ("skill_family_active_family_duplicate_count={0}" -f $result.summary.skill_family_active_family_duplicate_count)
Write-Host ("skill_family_low_health_target_state_count={0}" -f $result.summary.skill_family_low_health_target_state_count)
Write-Host ("skill_family_active_family_avg_health_score={0}" -f $result.summary.skill_family_active_family_avg_health_score)
Write-Host ("skill_lifecycle_health_status={0}" -f $result.summary.skill_lifecycle_health_status)
Write-Host ("skill_lifecycle_retire_candidate_count={0}" -f $result.summary.skill_lifecycle_retire_candidate_count)
Write-Host ("skill_lifecycle_retired_avg_latency_days={0}" -f $result.summary.skill_lifecycle_retired_avg_latency_days)
Write-Host ("skill_lifecycle_quality_impact_delta={0}" -f $result.summary.skill_lifecycle_quality_impact_delta)
Write-Host ("cross_repo_compatibility_status={0}" -f $result.summary.cross_repo_compatibility_status)
Write-Host ("cross_repo_compatibility_repo_failure_count={0}" -f $result.summary.cross_repo_compatibility_repo_failure_count)
Write-Host ("cross_repo_feedback_status={0}" -f $result.summary.cross_repo_feedback_status)
Write-Host ("cross_repo_feedback_ingested_count={0}" -f $result.summary.cross_repo_feedback_ingested_count)
Write-Host ("cross_repo_feedback_repo_failure_count={0}" -f $result.summary.cross_repo_feedback_repo_failure_count)
Write-Host ("cross_repo_feedback_rollout_matrix_gap_count={0}" -f $result.summary.cross_repo_feedback_rollout_matrix_gap_count)
Write-Host ("cross_repo_feedback_report_path={0}" -f $result.summary.cross_repo_feedback_report_path)
Write-Host ("token_efficiency_trend_status={0}" -f $result.summary.token_efficiency_trend_status)
Write-Host ("token_efficiency_trend_history_count={0}" -f $result.summary.token_efficiency_trend_history_count)
Write-Host ("token_efficiency_trend_latest_value={0}" -f $result.summary.token_efficiency_trend_latest_value)
Write-Host ("session_compaction_status={0}" -f $result.summary.session_compaction_status)
Write-Host ("session_compaction_recommend={0}" -f $result.summary.session_compaction_recommend)
Write-Host ("session_compaction_reason_count={0}" -f $result.summary.session_compaction_reason_count)
Write-Host ("quality_first_pass_rate={0}" -f $result.summary.quality_first_pass_rate)
Write-Host ("quality_rework_after_clarification_rate={0}" -f $result.summary.quality_rework_after_clarification_rate)
Write-Host ("token_average_response={0}" -f $result.summary.token_average_response)
Write-Host ("token_per_effective_conclusion={0}" -f $result.summary.token_per_effective_conclusion)
Write-Host ("slo_error_budget_status={0}" -f $result.summary.slo_error_budget_status)
Write-Host ("slo_gate_pass_rate={0}" -f $result.summary.slo_gate_pass_rate)
Write-Host ("error_budget_burn_rate={0}" -f $result.summary.error_budget_burn_rate)
Write-Host ("error_budget_remaining={0}" -f $result.summary.error_budget_remaining)
Write-Host ("auto_rollback_triggered={0}" -f $result.summary.auto_rollback_triggered)
Write-Host ("auto_rollback_reason_count={0}" -f $result.summary.auto_rollback_reason_count)
Write-Host ("auto_rollback_action={0}" -f $result.summary.auto_rollback_action)
Write-Host ("auto_rollback_policy_path={0}" -f $result.summary.auto_rollback_policy_path)
if ($result.ok) {
  Write-Host "result=OK"
  if (-not [string]::IsNullOrWhiteSpace($alertSnapshotPath)) {
    Write-Host ("alert_snapshot_path={0}" -f ($alertSnapshotPath -replace '\\', '/'))
  }
  exit 0
}

Write-Host "result=ALERT"
foreach ($item in $result.alerts) {
  Write-Host ("[ALERT] {0}" -f $item)
}
if ($notifyOnAlert) {
  Write-Warning "governance recurring review has alerts; see docs/governance/alerts-latest.md"
  try {
    [console]::Beep(880, 220)
  } catch {
  }
}
if (-not [string]::IsNullOrWhiteSpace($alertSnapshotPath)) {
  Write-Host ("alert_snapshot_path={0}" -f ($alertSnapshotPath -replace '\\', '/'))
}
exit 1
