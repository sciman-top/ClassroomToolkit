param(
  [string]$RepoRoot = ".",
  [string]$Period = "",
  [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$reviewScript = Join-Path $repoPath "scripts\governance\run-recurring-review.ps1"
$commonPath = Join-Path $repoPath "scripts\lib\common.ps1"
if (-not (Test-Path -LiteralPath $reviewScript -PathType Leaf)) {
  throw "Missing recurring review script: $reviewScript"
}

$periodValue = $Period
if ([string]::IsNullOrWhiteSpace($periodValue)) {
  $periodValue = (Get-Date).ToString("yyyy-MM")
}
if (-not [regex]::IsMatch($periodValue, "^[0-9]{4}-[0-9]{2}$")) {
  throw "Invalid -Period value: $periodValue (expected yyyy-MM)"
}

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

$reviewOut = & $psExe -NoProfile -ExecutionPolicy Bypass -File $reviewScript -RepoRoot $repoPath -NoNotifyOnAlert -AsJson 2>&1
$reviewExit = $LASTEXITCODE
$reviewText = ($reviewOut | Out-String).Trim()
$review = $null
if (-not [string]::IsNullOrWhiteSpace($reviewText)) {
  try {
    $review = $reviewText | ConvertFrom-Json
  } catch {
    throw "run-recurring-review returned non-JSON output"
  }
}
if ($null -eq $review) {
  throw "run-recurring-review produced empty review result"
}

$reviewsDir = Join-Path $repoPath "docs\governance\reviews"
if (-not (Test-Path -LiteralPath $reviewsDir -PathType Container)) {
  New-Item -ItemType Directory -Path $reviewsDir -Force | Out-Null
}

$reviewPath = Join-Path $reviewsDir ($periodValue + "-monthly-review.md")
$status = if ([bool]$review.ok) { "OK" } else { "ALERT" }
$lines = [System.Collections.Generic.List[string]]::new()
[void]$lines.Add("# Governance Monthly Review")
[void]$lines.Add("")
[void]$lines.Add(("period={0}" -f $periodValue))
[void]$lines.Add(("generated_at={0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")))
[void]$lines.Add(("repo_root={0}" -f (($repoPath -replace '\\', '/'))))
[void]$lines.Add(("status={0}" -f $status))
[void]$lines.Add(("doctor_health={0}" -f [string]$review.summary.doctor_health))
[void]$lines.Add(("observe_overdue={0}" -f [int]$review.summary.observe_overdue))
[void]$lines.Add(("waiver_remind_count={0}" -f [int]$review.summary.waiver_remind_count))
[void]$lines.Add(("waiver_block_count={0}" -f [int]$review.summary.waiver_block_count))
[void]$lines.Add(("update_trigger_alert_count={0}" -f [int]$review.summary.update_trigger_alert_count))
[void]$lines.Add(("alert_snapshot_path={0}" -f [string]$review.alert_snapshot_path))
[void]$lines.Add("")
[void]$lines.Add("## Alerts")
if (@($review.alerts).Count -eq 0) {
  [void]$lines.Add("- none")
} else {
  foreach ($a in @($review.alerts)) {
    [void]$lines.Add(("- " + [string]$a))
  }
}
[void]$lines.Add("")
[void]$lines.Add("## Next Actions")
if ([bool]$review.ok) {
  [void]$lines.Add("- Keep weekly recurring review enabled.")
  [void]$lines.Add("- Re-check rollout observe/enforce plan before next month.")
} else {
  [void]$lines.Add("- Resolve alerts in priority order: doctor -> rollout -> waiver -> metrics.")
  [void]$lines.Add("- Resolve periodic update trigger alerts (CLI drift, stale metrics, expired platform_na, overdue rollout).")
  [void]$lines.Add("- Re-run scripts/governance/run-recurring-review.ps1 until status=OK.")
}

Set-Content -LiteralPath $reviewPath -Value ($lines -join "`r`n") -Encoding UTF8

$result = [pscustomobject]@{
  schema_version = "1.0"
  period = $periodValue
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  repo_root = ($repoPath -replace '\\', '/')
  status = $status
  recurring_review_exit_code = [int]$reviewExit
  output_path = ($reviewPath -replace '\\', '/')
  alert_snapshot_path = [string]$review.alert_snapshot_path
  alerts = @($review.alerts)
}

if ($AsJson) {
  $result | ConvertTo-Json -Depth 8 | Write-Output
  if ($status -eq "OK") { exit 0 } else { exit 1 }
}

Write-Host "MONTHLY_POLICY_REVIEW"
Write-Host ("period={0}" -f $periodValue)
Write-Host ("status={0}" -f $status)
Write-Host ("output_path={0}" -f ($reviewPath -replace '\\', '/'))
Write-Host ("alert_snapshot_path={0}" -f [string]$review.alert_snapshot_path)
if ($status -eq "OK") { exit 0 } else { exit 1 }
