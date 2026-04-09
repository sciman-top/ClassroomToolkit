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
  $policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
}
if ($null -eq $policy) {
  $policy = [pscustomobject]@{
    cadence = [pscustomobject]@{
      recurring_review_days = 7
    }
    triggers = [pscustomobject]@{
      cli_version_drift = [pscustomobject]@{ enabled = $true; severity = "high" }
      rollout_observe_overdue = [pscustomobject]@{ enabled = $true; severity = "medium" }
      metrics_snapshot_stale = [pscustomobject]@{ enabled = $true; severity = "medium"; max_age_days = 8 }
      waiver_expired_unrecovered = [pscustomobject]@{ enabled = $true; severity = "high" }
      platform_na_expired = [pscustomobject]@{ enabled = $true; severity = "medium" }
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

$result = [pscustomobject]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  repo_root = ($repoPath -replace '\\', '/')
  status = if ($alerts.Count -eq 0) { "OK" } else { "ALERT" }
  alert_count = $alerts.Count
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
