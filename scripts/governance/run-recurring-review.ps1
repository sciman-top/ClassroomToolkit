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
$requiredScripts = @($doctorScript, $rolloutScript, $waiverScript, $metricsScript, $triggerScript)
foreach ($script in $requiredScripts) {
  if (-not (Test-Path -LiteralPath $script -PathType Leaf)) {
    throw "Missing recurring review dependency: $script"
  }
}

$psExe = Get-CurrentPowerShellPath

function Invoke-StepText([string]$Name, [string]$ScriptPath, [string[]]$Args) {
  $captured = & $psExe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Args 2>&1
  $exitCode = $LASTEXITCODE
  $text = ($captured | Out-String).Trim()
  return [pscustomobject]@{
    name = $Name
    exit_code = [int]$exitCode
    output = $text
  }
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
  [void]$lines.Add(("observe_overdue={0}" -f $ReviewResult.summary.observe_overdue))
  [void]$lines.Add(("waiver_remind_count={0}" -f $ReviewResult.summary.waiver_remind_count))
  [void]$lines.Add(("waiver_block_count={0}" -f $ReviewResult.summary.waiver_block_count))
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

if ($metrics.exit_code -ne 0) {
  [void]$alerts.Add("collect-governance-metrics failed")
}

$updateTriggerAlertCount = 0
if ($trigger.exit_code -ne 0) {
  $triggerObj = $null
  if (-not [string]::IsNullOrWhiteSpace($trigger.output)) {
    try {
      $triggerObj = $trigger.output | ConvertFrom-Json
    } catch {
      $triggerObj = $null
    }
  }
  if ($null -ne $triggerObj -and $triggerObj.PSObject.Properties.Name -contains "alert_count") {
    $updateTriggerAlertCount = [int]$triggerObj.alert_count
  }
  if ($updateTriggerAlertCount -gt 0) {
    [void]$alerts.Add(("update triggers alert count={0}" -f $updateTriggerAlertCount))
  } else {
    [void]$alerts.Add("check-update-triggers failed")
  }
}

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
    observe_overdue = $observeOverdue
    waiver_remind_count = [int]$waiverRemindCount
    waiver_block_count = [int]$waiverBlockCount
    metrics_exit_code = [int]$metrics.exit_code
    update_trigger_alert_count = [int]$updateTriggerAlertCount
    update_trigger_exit_code = [int]$trigger.exit_code
  }
  steps = @(
    [pscustomobject]@{ name = "doctor"; exit_code = [int]$doctor.exit_code },
    [pscustomobject]@{ name = "rollout-status"; exit_code = [int]$rollout.exit_code },
    [pscustomobject]@{ name = "check-waivers"; exit_code = [int]$waiver.exit_code },
    [pscustomobject]@{ name = "collect-governance-metrics"; exit_code = [int]$metrics.exit_code },
    [pscustomobject]@{ name = "check-update-triggers"; exit_code = [int]$trigger.exit_code }
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
Write-Host ("observe_overdue={0}" -f $result.summary.observe_overdue)
Write-Host ("waiver_remind_count={0}" -f $result.summary.waiver_remind_count)
Write-Host ("waiver_block_count={0}" -f $result.summary.waiver_block_count)
Write-Host ("update_trigger_alert_count={0}" -f $result.summary.update_trigger_alert_count)
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
