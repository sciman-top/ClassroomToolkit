param(
  [string]$RepoRoot = ".",
  [string]$GovernanceKitRoot = "",
  [switch]$NoNotifyOnAlert,
  [switch]$NoAlertSnapshot,
  [switch]$AsJson
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
  } catch {
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

$kitRoot = Resolve-KitRoot -ProvidedPath $GovernanceKitRoot
$runner = Join-Path $kitRoot "scripts/governance/run-recurring-review.ps1"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
  throw "Missing runner script: $runner"
}

$args = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", $runner,
  "-RepoRoot", $repoPath
)
if ($NoNotifyOnAlert.IsPresent) { $args += "-NoNotifyOnAlert" }
if ($NoAlertSnapshot.IsPresent) { $args += "-NoAlertSnapshot" }
if ($AsJson.IsPresent) { $args += "-AsJson" }

& powershell @args
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

