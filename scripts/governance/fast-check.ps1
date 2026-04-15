param(
  [switch]$AsJson,
  [switch]$RunFullGate,
  [switch]$DisableAutoEscalation
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$commonPath = Join-Path $repoRoot "scripts\lib\common.ps1"
if (-not (Test-Path -LiteralPath $commonPath -PathType Leaf)) {
  throw "Missing common helper: $commonPath"
}
. $commonPath
$psExe = Get-CurrentPowerShellPath

function Parse-JsonFromText([string]$RawText) {
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

function Get-PendingFiles([string]$RootPath) {
  $result = [ordered]@{
    git_available = $false
    pending_files = @()
  }
  $gitCmd = Get-Command -Name git -ErrorAction SilentlyContinue
  if ($null -eq $gitCmd) { return [pscustomobject]$result }

  $statusOut = & git -C $RootPath status --porcelain 2>$null
  if ($LASTEXITCODE -ne 0) { return [pscustomobject]$result }

  $result.git_available = $true
  $files = New-Object System.Collections.Generic.List[string]
  foreach ($lineRaw in @($statusOut)) {
    $line = [string]$lineRaw
    if ($line.Length -lt 4) { continue }
    $pathText = $line.Substring(3).Trim()
    if ([string]::IsNullOrWhiteSpace($pathText)) { continue }
    if ($pathText.Contains(" -> ")) {
      $parts = $pathText -split " -> "
      $pathText = $parts[$parts.Count - 1]
    }
    $pathNorm = ($pathText -replace '\\', '/').Trim()
    if (-not [string]::IsNullOrWhiteSpace($pathNorm)) {
      [void]$files.Add($pathNorm)
    }
  }
  $result.pending_files = @($files)
  return [pscustomobject]$result
}

function Get-EscalationDecision([string[]]$PendingFiles, [switch]$ForceFull, [switch]$AutoEscalationDisabled) {
  $reasonCodes = New-Object System.Collections.Generic.List[string]
  if ($ForceFull) {
    [void]$reasonCodes.Add("manual_full_gate")
    return [pscustomobject]@{
      should_run_full = $true
      reason_codes = @($reasonCodes)
      high_risk_files = @()
    }
  }

  if ($AutoEscalationDisabled) {
    return [pscustomobject]@{
      should_run_full = $false
      reason_codes = @("auto_escalation_disabled")
      high_risk_files = @()
    }
  }

  if ($null -eq $PendingFiles -or $PendingFiles.Count -eq 0) {
    return [pscustomobject]@{
      should_run_full = $false
      reason_codes = @("no_pending_files")
      high_risk_files = @()
    }
  }

  $highRiskPatterns = @(
    '^config/',
    '^scripts/',
    '^tests/',
    '^source/',
    '^hooks/',
    '^ci/',
    '^templates/',
    '^\.governance/',
    '^AGENTS\.md$',
    '^CLAUDE\.md$',
    '^GEMINI\.md$'
  )

  $highRiskFiles = New-Object System.Collections.Generic.List[string]
  foreach ($f in $PendingFiles) {
    foreach ($pattern in $highRiskPatterns) {
      if ([regex]::IsMatch([string]$f, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        [void]$highRiskFiles.Add([string]$f)
        break
      }
    }
  }

  if ($highRiskFiles.Count -gt 0) {
    [void]$reasonCodes.Add("high_risk_pending_changes")
    return [pscustomobject]@{
      should_run_full = $true
      reason_codes = @($reasonCodes)
      high_risk_files = @($highRiskFiles)
    }
  }

  return [pscustomobject]@{
    should_run_full = $false
    reason_codes = @("pending_changes_low_risk")
    high_risk_files = @()
  }
}

function Run-DoctorStage([string]$StageName, [string[]]$StageArgs) {
  $doctorScript = Join-Path $repoRoot "scripts\doctor.ps1"
  if (-not (Test-Path -LiteralPath $doctorScript -PathType Leaf)) {
    throw "Missing doctor script: $doctorScript"
  }

  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  $raw = ""
  $parsed = $null
  $exitCode = 1
  $status = "FAIL"
  $errorText = $null
  try {
    $captured = & $psExe -NoProfile -ExecutionPolicy Bypass -File $doctorScript @StageArgs 2>&1
    $raw = ($captured | Out-String).TrimEnd()
    $exitCode = [int]$LASTEXITCODE
    $parsed = Parse-JsonFromText -RawText $raw
    if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["health"] -and -not [string]::IsNullOrWhiteSpace([string]$parsed.health)) {
      $status = if ([string]$parsed.health -eq "GREEN") { "PASS" } else { "FAIL" }
      if ($status -eq "PASS") { $exitCode = 0 }
    } else {
      $status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
    }
  } catch {
    $errorText = $_.Exception.Message
    $status = "FAIL"
    $exitCode = 1
  } finally {
    $sw.Stop()
  }

  return [pscustomobject]@{
    stage = $StageName
    status = $status
    exit_code = $exitCode
    elapsed_ms = [int]$sw.ElapsedMilliseconds
    health = if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["health"]) { [string]$parsed.health } else { "UNKNOWN" }
    failed_steps = if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["failed_steps"]) { @($parsed.failed_steps) } else { @() }
    slow_steps_top3 = if ($null -ne $parsed -and $null -ne $parsed.PSObject.Properties["slow_steps_top3"]) { @($parsed.slow_steps_top3) } else { @() }
    error = $errorText
    raw = $raw
  }
}

$overallSw = [System.Diagnostics.Stopwatch]::StartNew()
$pending = Get-PendingFiles -RootPath $repoRoot
$escalation = Get-EscalationDecision -PendingFiles @($pending.pending_files) -ForceFull:$RunFullGate -AutoEscalationDisabled:$DisableAutoEscalation

$fastStage = Run-DoctorStage -StageName "fast_precheck" -StageArgs @("-SkipVerifyTargets", "-AsJson")
$fullStage = $null
if ($fastStage.exit_code -eq 0 -and $escalation.should_run_full) {
  $fullStage = Run-DoctorStage -StageName "full_gate" -StageArgs @("-AsJson")
}
$overallSw.Stop()

$ok = ($fastStage.exit_code -eq 0)
if ($null -ne $fullStage) {
  $ok = ($ok -and $fullStage.exit_code -eq 0)
}

$summary = [pscustomobject]@{
  schema_version = "1.0"
  generated_at = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
  ok = $ok
  mode = if ($null -ne $fullStage) { "fast_plus_full" } else { "fast_only" }
  elapsed_ms = [int]$overallSw.ElapsedMilliseconds
  pending = [pscustomobject]@{
    git_available = [bool]$pending.git_available
    pending_count = @($pending.pending_files).Count
    sample = @($pending.pending_files | Select-Object -First 20)
  }
  auto_escalation = [pscustomobject]@{
    enabled = (-not $DisableAutoEscalation)
    triggered = ($null -ne $fullStage)
    reason_codes = @($escalation.reason_codes)
    high_risk_files = @($escalation.high_risk_files | Select-Object -First 20)
  }
  fast_precheck = $fastStage
  full_gate = $fullStage
}

if ($AsJson) {
  $summary | ConvertTo-Json -Depth 10 | Write-Output
} else {
  Write-Host "=== FAST_CHECK ==="
  Write-Host ("mode={0} ok={1} elapsed_ms={2}" -f $summary.mode, $summary.ok, $summary.elapsed_ms)
  Write-Host ("fast_precheck.status={0} elapsed_ms={1}" -f $fastStage.status, $fastStage.elapsed_ms)
  if ($null -ne $fullStage) {
    Write-Host ("full_gate.status={0} elapsed_ms={1}" -f $fullStage.status, $fullStage.elapsed_ms)
  } else {
    Write-Host "full_gate.status=SKIP"
  }
  Write-Host ("auto_escalation.triggered={0} reason_codes={1}" -f $summary.auto_escalation.triggered, (@($summary.auto_escalation.reason_codes) -join ","))
  if (@($summary.auto_escalation.high_risk_files).Count -gt 0) {
    Write-Host ("auto_escalation.high_risk_files=" + (@($summary.auto_escalation.high_risk_files) -join ","))
  }
}

if ($summary.ok) { exit 0 }
exit 1
