param(
  [ValidateSet("quick", "full")]
  [string]$Profile = "quick",
  [string]$Configuration = "Debug",
  [ValidateSet("changed", "all")]
  [string]$EvidenceMode = "changed",
  [double]$EvidenceThreshold = 98.0,
  [switch]$RunAllEvidenceObserve,
  [switch]$SkipBuildServerShutdown
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )

  Write-Host "=== $Name ==="
  $global:LASTEXITCODE = 0
  & $Action
  if ($LASTEXITCODE -ne 0) {
    throw "Step failed: $Name (exit=$LASTEXITCODE)"
  }
}

Invoke-Step -Name "precheck-dotnet" -Action {
  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet not found."
  }
}

Invoke-Step -Name "precheck-powershell" -Action {
  if (-not (Get-Command powershell -ErrorAction SilentlyContinue)) {
    throw "powershell not found."
  }
}

Invoke-Step -Name "precheck-tests-project" -Action {
  if (-not (Test-Path -LiteralPath "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj" -PathType Leaf)) {
    throw "tests project not found."
  }
}

$qualityArgs = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", "scripts/quality/run-local-quality-gates.ps1",
  "-Profile", $Profile,
  "-Configuration", $Configuration,
  "-EmitGovernanceReport"
)
if ($SkipBuildServerShutdown) {
  $qualityArgs += "-SkipBuildServerShutdown"
}

Invoke-Step -Name "quality-gates-with-governance" -Action {
  & powershell @qualityArgs
}

Invoke-Step -Name "endstate-doctor-main" -Action {
  & powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-doctor-endstate.ps1 `
    -EvidenceMode $EvidenceMode `
    -EvidenceThreshold $EvidenceThreshold
}

if ($RunAllEvidenceObserve -and $EvidenceMode -ne "all") {
  Write-Host "=== endstate-doctor-observe-all (non-blocking) ==="
  $global:LASTEXITCODE = 0
  & powershell -NoProfile -ExecutionPolicy Bypass -File scripts/governance/run-doctor-endstate.ps1 `
    -EvidenceMode all `
    -EvidenceThreshold $EvidenceThreshold
  if ($LASTEXITCODE -ne 0) {
    Write-Host "[observe] EvidenceMode=all reported gaps (expected during observe phase)."
    $global:LASTEXITCODE = 0
  }
}

Write-Host "run-endstate-loop done. profile=$Profile configuration=$Configuration evidence_mode=$EvidenceMode threshold=$EvidenceThreshold"

