param(
  [string]$RepoRoot = ".",
  [string]$GovernanceRoot = "",
  [string]$IssueId = "project-governance-cycle-default",
  [ValidateSet("auto", "plan", "requirement", "bugfix", "acceptance")]
  [string]$ClarificationScenario = "auto",
  [string]$ClarificationContextFile = "",
  [ValidateSet("plan", "safe")]
  [string]$Mode = "safe",
  [switch]$ShowScope
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path -LiteralPath $RepoRoot).Path

function Resolve-GovernanceRoot {
  param([string]$ProvidedPath)

  if (-not [string]::IsNullOrWhiteSpace($ProvidedPath)) {
    $resolved = Resolve-Path -LiteralPath $ProvidedPath -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
    throw "Governance kit path not found: $ProvidedPath"
  }

  $gitValue = ""
  try {
    $gitValue = (& git -C $repoPath config --local --get governance.root 2>$null)
  }
  catch {
    $gitValue = ""
  }

  if (-not [string]::IsNullOrWhiteSpace($gitValue)) {
    $resolved = Resolve-Path -LiteralPath ($gitValue -replace '/', '\') -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
  }

  if (-not [string]::IsNullOrWhiteSpace($env:GOVERNANCE_ROOT)) {
    $resolved = Resolve-Path -LiteralPath $env:GOVERNANCE_ROOT -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Path }
  }

  throw "Cannot resolve repo-governance-hub root. Set git config governance.root or pass -GovernanceRoot."
}

$kitRoot = Resolve-GovernanceRoot -ProvidedPath $GovernanceRoot
$runner = Join-Path $kitRoot "scripts/run-project-governance-cycle.ps1"
if (-not (Test-Path -LiteralPath $runner)) {
  throw "Missing runner script: $runner"
}

$args = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", $runner,
  "-RepoPath", $repoPath,
  "-RepoName", (Split-Path -Leaf $repoPath),
  "-IssueId", $IssueId,
  "-ClarificationScenario", $ClarificationScenario,
  "-ClarificationContextFile", $ClarificationContextFile,
  "-Mode", $Mode
)
if ($ShowScope.IsPresent) { $args += "-ShowScope" }

& powershell @args
if ($LASTEXITCODE -ne 0) {
  throw "run-project-governance-cycle failed with exit code $LASTEXITCODE"
}

