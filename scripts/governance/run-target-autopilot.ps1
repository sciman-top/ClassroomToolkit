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

function Resolve-KitRoot {
  param([string]$ProvidedPath, [string]$RepoPath)

  if (-not [string]::IsNullOrWhiteSpace($ProvidedPath)) {
    $resolved = Resolve-Path -LiteralPath $ProvidedPath -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
    throw "Governance kit path not found: $ProvidedPath"
  }

  $gitValue = ""
  try {
    $gitValue = (& git -C $RepoPath config --local --get governance.kitRoot 2>$null)
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

  throw "Cannot resolve repo-governance-hub root. Set git config governance.kitRoot or pass -GovernanceKitRoot."
}

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$kitRoot = Resolve-KitRoot -ProvidedPath $GovernanceKitRoot -RepoPath $repoPath
$sharedScript = Join-Path $kitRoot "scripts\governance\run-target-autopilot.ps1"

if (-not (Test-Path -LiteralPath $sharedScript -PathType Leaf)) {
  throw "Shared autopilot script not found: $sharedScript"
}

$currentScript = [System.IO.Path]::GetFullPath($PSCommandPath)
$targetScript = [System.IO.Path]::GetFullPath($sharedScript)
if ($currentScript -eq $targetScript) {
  throw "Delegation loop detected: shared script resolves to current script."
}

$psExe = "powershell"
$pwshCmd = Get-Command -Name "pwsh" -ErrorAction SilentlyContinue
if ($null -ne $pwshCmd) { $psExe = $pwshCmd.Source }

$invokeArgs = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", $sharedScript,
  "-RepoRoot", $RepoRoot,
  "-GovernanceKitRoot", $kitRoot,
  "-IssueId", $IssueId,
  "-ClarificationScenario", $ClarificationScenario,
  "-CodexCommand", $CodexCommand,
  "-MaxCycles", [string]$MaxCycles,
  "-MaxFixAttemptsPerGate", [string]$MaxFixAttemptsPerGate,
  "-MaxWorkIterationsPerCycle", [string]$MaxWorkIterationsPerCycle
)

if (-not [string]::IsNullOrWhiteSpace($ClarificationContextFile)) {
  $invokeArgs += @("-ClarificationContextFile", $ClarificationContextFile)
}
if ($SkipWorkIteration.IsPresent) { $invokeArgs += "-SkipWorkIteration" }
if ($DryRun.IsPresent) { $invokeArgs += "-DryRun" }

& $psExe @invokeArgs
exit $LASTEXITCODE

