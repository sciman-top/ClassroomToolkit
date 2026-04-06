param(
  [string]$RepoRoot = ".",
  [string]$GovernanceKitRoot = "",
  [string]$IssueId = "target-autopilot-default",
  [ValidateSet("auto", "plan", "requirement", "bugfix", "acceptance")]
  [string]$ClarificationScenario = "auto",
  [string]$ClarificationContextFile = "",
  [string]$CodexCommand = "codex",
  [int]$MaxCycles = 20,
  [int]$MaxFixAttemptsPerGate = 2,
  [int]$MaxWorkIterationsPerCycle = 1,
  [switch]$SkipWorkIteration,
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path

function Resolve-KitRoot {
  param([string]$ProvidedPath)

  if (-not [string]::IsNullOrWhiteSpace($ProvidedPath)) {
    $resolved = Resolve-Path -LiteralPath $ProvidedPath -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
    throw "Governance kit path not found: $ProvidedPath"
  }

  $gitValue = ""
  try {
    $gitValue = (& git -C $repoPath config --local --get governance.kitRoot 2>$null)
  }
  catch {
    $gitValue = ""
  }

  if (-not [string]::IsNullOrWhiteSpace($gitValue)) {
    $resolved = Resolve-Path -LiteralPath ($gitValue -replace '/', '\\') -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
  }

  if (-not [string]::IsNullOrWhiteSpace($env:GOVERNANCE_KIT_ROOT)) {
    $resolved = Resolve-Path -LiteralPath $env:GOVERNANCE_KIT_ROOT -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
  }

  throw "Cannot resolve governance-kit root. Set git config governance.kitRoot or pass -GovernanceKitRoot."
}

function Assert-Command {
  param([string]$Name)

  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "Required command not found: $Name"
  }
}

function Invoke-LoggedCommand {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][scriptblock]$Action,
    [Parameter(Mandatory = $true)][string]$WorkDir,
    [Parameter(Mandatory = $true)][string]$LogRoot
  )

  $safeName = ($Name -replace '[^a-zA-Z0-9._-]', '_')
  $logPath = Join-Path $LogRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-" + $safeName + ".log")

  Push-Location $WorkDir
  try {
    & $Action *>&1 | Tee-Object -LiteralPath $logPath | Out-Host
    $exitCode = $LASTEXITCODE
  }
  finally {
    Pop-Location
  }

  if ($null -eq $exitCode) { $exitCode = 0 }

  return [pscustomobject]@{
    name = $Name
    exit_code = [int]$exitCode
    log_path = $logPath
  }
}

function Invoke-ShellCommand {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$CommandText,
    [Parameter(Mandatory = $true)][string]$WorkDir,
    [Parameter(Mandatory = $true)][string]$LogRoot
  )

  return Invoke-LoggedCommand -Name $Name -WorkDir $WorkDir -LogRoot $LogRoot -Action {
    $scriptBlock = [ScriptBlock]::Create($CommandText)
    & $scriptBlock
  }
}

function Emit-BrowserSessionHint {
  param([string]$RepoPath)
  $helperPath = Join-Path $RepoPath "tools\browser-session\start-browser-session.ps1"
  if (-not (Test-Path -LiteralPath $helperPath)) {
    return
  }

  Write-Host "browser_session.helper=$helperPath"
  Write-Host "browser_session.start=powershell -ExecutionPolicy Bypass -File tools/browser-session/start-browser-session.ps1 -Action start -Name automation -Port 9222 -Url about:blank"
  Write-Host "browser_session.attach=agent-browser --cdp 9222 open about:blank"
}

function Invoke-ClarificationTracker {
  param(
    [Parameter(Mandatory = $true)][string]$Mode,
    [string]$Outcome = "",
    [string]$Reason = ""
  )

  $args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $trackerScript,
    "-RepoPath", $repoPath,
    "-IssueId", $IssueId,
    "-Scenario", $effectiveClarificationScenario,
    "-Mode", $Mode
  )
  if (-not [string]::IsNullOrWhiteSpace($Outcome)) {
    $args += @("-Outcome", $Outcome)
  }
  if (-not [string]::IsNullOrWhiteSpace($Reason)) {
    $args += @("-Reason", $Reason)
  }

  $json = & powershell @args
  if ($LASTEXITCODE -ne 0) {
    throw "clarification tracker failed with exit code $LASTEXITCODE"
  }

  return [string]::Join([Environment]::NewLine, @($json)) | ConvertFrom-Json
}

function Resolve-EffectiveClarificationScenario {
  param(
    [string]$RequestedScenario,
    [string]$ContextFile
  )

  if ($RequestedScenario -ne "auto" -and -not [string]::IsNullOrWhiteSpace($RequestedScenario)) {
    return [pscustomobject]@{
      scenario = $RequestedScenario
      source = "param"
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($ContextFile) -and (Test-Path -LiteralPath $ContextFile)) {
    try {
      $ctx = Get-Content -LiteralPath $ContextFile -Raw | ConvertFrom-Json
      $ctxScenario = ""
      if ($null -ne $ctx.PSObject.Properties['clarification_scenario']) {
        $ctxScenario = [string]$ctx.clarification_scenario
      } elseif ($null -ne $ctx.PSObject.Properties['scenario']) {
        $ctxScenario = [string]$ctx.scenario
      }
      $valid = @("plan", "requirement", "bugfix", "acceptance")
      if ($valid -contains $ctxScenario) {
        return [pscustomobject]@{
          scenario = $ctxScenario
          source = "context_file"
        }
      }
    } catch {
      Write-Host ("[WARN] clarification context parse failed: {0}" -f $ContextFile)
    }
  }

  return [pscustomobject]@{
    scenario = "bugfix"
    source = "fallback"
  }
}

$kitRoot = Resolve-KitRoot -ProvidedPath $GovernanceKitRoot
$analyzeScript = Join-Path $kitRoot "scripts/analyze-repo-governance.ps1"
$trackerScript = Join-Path $kitRoot "scripts/governance/track-issue-state.ps1"
if (-not (Test-Path -LiteralPath $analyzeScript)) {
  throw "Missing analyzer script: $analyzeScript"
}
if (-not (Test-Path -LiteralPath $trackerScript)) {
  throw "Missing clarification tracker script: $trackerScript"
}

Assert-Command -Name powershell

$runId = [guid]::NewGuid().ToString("n")
$scenarioResolution = Resolve-EffectiveClarificationScenario -RequestedScenario $ClarificationScenario -ContextFile $ClarificationContextFile
$effectiveClarificationScenario = [string]$scenarioResolution.scenario
$clarificationScenarioSource = [string]$scenarioResolution.source
$logRoot = Join-Path $repoPath (".codex/logs/target-autopilot/" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + $runId)
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

$analysisJson = & powershell -NoProfile -ExecutionPolicy Bypass -File $analyzeScript -RepoPath $repoPath -AsJson
$analysis = [string]::Join([Environment]::NewLine, @($analysisJson)) | ConvertFrom-Json

$buildCmd = [string]$analysis.recommended.build
$testCmd = [string]$analysis.recommended.test
$contractCmd = [string]$analysis.recommended.contract_invariant
$hotspotCmd = [string]$analysis.recommended.hotspot

$gateSteps = @(
  [pscustomobject]@{ name = "build"; command = $buildCmd },
  [pscustomobject]@{ name = "test"; command = $testCmd },
  [pscustomobject]@{ name = "contract-invariant"; command = $contractCmd },
  [pscustomobject]@{ name = "hotspot"; command = $hotspotCmd }
)

Write-Host "TARGET_SAFE_AUTOPILOT"
Write-Host "run_id=$runId"
Write-Host "repo_root=$repoPath"
Write-Host "governance_kit_root=$kitRoot"
Write-Host "logs=$logRoot"
Write-Host "mode=gate-orchestrator"
Write-Host "issue_id=$IssueId"
Write-Host "clarification_scenario=$effectiveClarificationScenario"
Write-Host "clarification_scenario_source=$clarificationScenarioSource"
Emit-BrowserSessionHint -RepoPath $repoPath

if ($DryRun) {
  Write-Host "dry_run=true"
  foreach ($step in $gateSteps) {
    Write-Host ("planned_gate." + $step.name + "=" + $step.command)
  }
  if (-not $SkipWorkIteration.IsPresent -and $MaxWorkIterationsPerCycle -gt 0) {
    Write-Host "planned_work_iteration=no-op (handled by outer AI session)"
  }
  exit 0
}

$clarificationState = Invoke-ClarificationTracker -Mode "evaluate"
if ($clarificationState.clarification_required -eq $true) {
  Write-Host ("CLARIFICATION_REQUIRED issue_id={0} attempt_count={1} scenario={2}" -f $IssueId, $clarificationState.attempt_count, $clarificationState.scenario)
}

for ($cycle = 1; $cycle -le $MaxCycles; $cycle++) {
  Write-Host ""
  Write-Host "=== cycle $cycle / $MaxCycles ==="

  foreach ($step in $gateSteps) {
    if ([string]::IsNullOrWhiteSpace($step.command) -or $step.command -like "N/A*") {
      throw "Required gate step '$($step.name)' is unavailable: $($step.command)"
    }

    $result = Invoke-ShellCommand -Name ("gate." + $step.name) -CommandText $step.command -WorkDir $repoPath -LogRoot $logRoot
    if ($result.exit_code -eq 0) {
      continue
    }

    $recovered = $false
    for ($attempt = 1; $attempt -le $MaxFixAttemptsPerGate; $attempt++) {
      Write-Host "RETRY step=$($step.name) attempt=$attempt/$MaxFixAttemptsPerGate"
      $result = Invoke-ShellCommand -Name ("gate.retry." + $step.name) -CommandText $step.command -WorkDir $repoPath -LogRoot $logRoot
      if ($result.exit_code -eq 0) {
        $recovered = $true
        break
      }
    }

    if (-not $recovered) {
      $failureReason = "gate:$($step.name) failed; log=$($result.log_path)"
      $clarificationState = Invoke-ClarificationTracker -Mode "record" -Outcome "failure" -Reason $failureReason
      if ($clarificationState.clarification_required -eq $true) {
        Write-Host ("CLARIFICATION_REQUIRED issue_id={0} attempt_count={1} scenario={2}" -f $IssueId, $clarificationState.attempt_count, $clarificationState.scenario)
        Write-Host ("[CLARIFICATION_STATE_JSON] " + ($clarificationState | ConvertTo-Json -Depth 8 -Compress))
      }
      throw "Gate step '$($step.name)' failed. log=$($result.log_path)"
    }
  }

  if (-not $SkipWorkIteration.IsPresent -and $MaxWorkIterationsPerCycle -gt 0) {
    Write-Host "WORK_ITERATION delegated_to_outer_ai_session (no-op)"
  }
}

Write-Host "STATUS: ITERATION_COMPLETE_CONTINUE"
Write-Host "target safe autopilot completed"
Invoke-ClarificationTracker -Mode "record" -Outcome "success" | Out-Null
