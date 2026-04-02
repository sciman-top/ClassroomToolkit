param(
  [string]$RepoRoot = ".",
  [string]$GovernanceKitRoot = "",
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
    $resolved = Resolve-Path -LiteralPath ($gitValue -replace '/', '\') -ErrorAction SilentlyContinue
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
    & powershell -NoProfile -ExecutionPolicy Bypass -Command $CommandText
  }
}

function Invoke-CodexFix {
  param(
    [Parameter(Mandatory = $true)][string]$Issue,
    [Parameter(Mandatory = $true)][string]$FailureLogPath,
    [Parameter(Mandatory = $true)][string]$LogRoot
  )

  $promptPath = Join-Path $LogRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-fix.prompt.txt")
  $logPath = Join-Path $LogRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-fix.codex.log")

  $prompt = @"
You are in autonomous remediation mode.

Goal:
- Fix the failed gate command with the smallest safe change.
- Do not weaken or bypass governance quality gates.

Context:
- repo_root: $repoPath
- issue: $Issue
- failure_log: $FailureLogPath

Required:
1) Find root cause from the failure log.
2) Implement minimal safe fix.
3) Re-run relevant verification commands.
4) Output concise evidence.
"@

  Set-Content -LiteralPath $promptPath -Value $prompt -Encoding UTF8

  Push-Location $repoPath
  try {
    $args = @(
      "-a", "never",
      "-s", "workspace-write",
      "exec",
      "--cd", $repoPath,
      "-"
    )

    Get-Content -LiteralPath $promptPath -Raw | & $CodexCommand @args *>&1 | Tee-Object -LiteralPath $logPath | Out-Host
    $exitCode = $LASTEXITCODE
  }
  finally {
    Pop-Location
  }

  if ($null -eq $exitCode) { $exitCode = 0 }

  return [pscustomobject]@{
    exit_code = [int]$exitCode
    log_path = $logPath
    prompt_path = $promptPath
  }
}

function Invoke-CodexWorkIteration {
  param(
    [Parameter(Mandatory = $true)][string]$QuickGateCommand,
    [Parameter(Mandatory = $true)][string]$LogRoot
  )

  $promptPath = Join-Path $LogRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-work.prompt.txt")
  $logPath = Join-Path $LogRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-work.codex.log")

  $quickGateHint = if ([string]::IsNullOrWhiteSpace($QuickGateCommand) -or $QuickGateCommand -like "N/A*") {
    "No quick gate command available. Use the required build/test/contract/hotspot commands for verification."
  } else {
    "After implementing, run this quick gate command before finishing: $QuickGateCommand"
  }

  $prompt = @"
You are running in autonomous continuous execution mode.

Task:
- Complete one highest-impact automatable task in this repository.
- Prefer concrete bug fixes, reliability improvements, or test debt reduction.
- Avoid broad speculative refactors.

Verification requirements:
- $quickGateHint
- If quick gate fails, fix and re-run before finishing.

Output requirements:
- Print STATUS: ITERATION_COMPLETE_CONTINUE when iteration can continue.
- Print STATUS: BLOCKED_NEEDS_HUMAN only for hard blockers that cannot be resolved safely.
"@

  Set-Content -LiteralPath $promptPath -Value $prompt -Encoding UTF8

  Push-Location $repoPath
  try {
    $args = @(
      "-a", "never",
      "-s", "workspace-write",
      "exec",
      "--cd", $repoPath,
      "-"
    )

    Get-Content -LiteralPath $promptPath -Raw | & $CodexCommand @args *>&1 | Tee-Object -LiteralPath $logPath | Out-Host
    $exitCode = $LASTEXITCODE
  }
  finally {
    Pop-Location
  }

  if ($null -eq $exitCode) { $exitCode = 0 }

  return [pscustomobject]@{
    exit_code = [int]$exitCode
    log_path = $logPath
    prompt_path = $promptPath
  }
}

$kitRoot = Resolve-KitRoot -ProvidedPath $GovernanceKitRoot
$analyzeScript = Join-Path $kitRoot "scripts/analyze-repo-governance.ps1"
if (-not (Test-Path -LiteralPath $analyzeScript)) {
  throw "Missing analyzer script: $analyzeScript"
}

Assert-Command -Name powershell
Assert-Command -Name $CodexCommand

$runId = [guid]::NewGuid().ToString("n")
$logRoot = Join-Path $repoPath (".codex/logs/target-autopilot/" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + $runId)
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

$analysisJson = & powershell -NoProfile -ExecutionPolicy Bypass -File $analyzeScript -RepoPath $repoPath -AsJson
$analysis = [string]::Join([Environment]::NewLine, @($analysisJson)) | ConvertFrom-Json

$buildCmd = [string]$analysis.recommended.build
$testCmd = [string]$analysis.recommended.test
$contractCmd = [string]$analysis.recommended.contract_invariant
$hotspotCmd = [string]$analysis.recommended.hotspot
$quickGateCmd = [string]$analysis.recommended.quick_gate

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

if ($DryRun) {
  Write-Host "dry_run=true"
  foreach ($step in $gateSteps) {
    Write-Host ("planned_gate." + $step.name + "=" + $step.command)
  }
  Write-Host "planned.quick_gate=$quickGateCmd"
  exit 0
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

    $fixed = $false
    for ($attempt = 1; $attempt -le $MaxFixAttemptsPerGate; $attempt++) {
      Write-Host "AUTO_FIX step=$($step.name) attempt=$attempt/$MaxFixAttemptsPerGate"
      $fix = Invoke-CodexFix -Issue ("gate-failed:" + $step.name) -FailureLogPath $result.log_path -LogRoot $logRoot
      if ($fix.exit_code -ne 0) {
        Write-Host "fix failed: $($fix.log_path)"
        continue
      }

      $result = Invoke-ShellCommand -Name ("gate.retry." + $step.name) -CommandText $step.command -WorkDir $repoPath -LogRoot $logRoot
      if ($result.exit_code -eq 0) {
        $fixed = $true
        break
      }
    }

    if (-not $fixed -and $result.exit_code -ne 0) {
      throw "Gate step '$($step.name)' failed after auto-fix attempts. log=$($result.log_path)"
    }
  }

  if ($SkipWorkIteration.IsPresent) {
    continue
  }

  for ($iter = 1; $iter -le $MaxWorkIterationsPerCycle; $iter++) {
    Write-Host "WORK_ITERATION $iter/$MaxWorkIterationsPerCycle"
    $work = Invoke-CodexWorkIteration -QuickGateCommand $quickGateCmd -LogRoot $logRoot
    if ($work.exit_code -ne 0) {
      throw "work iteration failed: $($work.log_path)"
    }

    if (-not [string]::IsNullOrWhiteSpace($quickGateCmd) -and $quickGateCmd -notlike "N/A*") {
      $quickResult = Invoke-ShellCommand -Name "quick-gate" -CommandText $quickGateCmd -WorkDir $repoPath -LogRoot $logRoot
      if ($quickResult.exit_code -ne 0) {
        throw "quick gate failed after work iteration. log=$($quickResult.log_path)"
      }
    }
  }
}

Write-Host "STATUS: ITERATION_COMPLETE_CONTINUE"
Write-Host "target safe autopilot completed"
