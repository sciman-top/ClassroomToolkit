param(
  [ValidateSet("quick", "full")]
  [string]$Profile = "quick",
  [string]$Configuration = "Debug",
  [switch]$SkipBuildServerShutdown,
  [switch]$EnableGovernanceChecks = $true,
  [switch]$EmitGovernanceReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
  return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Resolve-MsBuildDllPath {
  $repoRoot = Get-RepoRoot
  $globalJsonPath = Join-Path $repoRoot "global.json"
  if (!(Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    throw "global.json not found: $globalJsonPath"
  }

  $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
  $sdkVersion = [string]$globalJson.sdk.version
  if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "global.json sdk.version is missing."
  }

  $msbuildDll = Join-Path "C:\Program Files\dotnet\sdk" (Join-Path $sdkVersion "MSBuild.dll")
  if (!(Test-Path -LiteralPath $msbuildDll -PathType Leaf)) {
    throw "MSBuild.dll not found for sdk.version=$sdkVersion at $msbuildDll"
  }

  return $msbuildDll
}

function Invoke-BuildServerShutdown {
  if ($SkipBuildServerShutdown) {
    return
  }

  Write-Host "=== build-server-shutdown ==="
  dotnet build-server shutdown | Out-Host
}

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

Invoke-BuildServerShutdown

$msbuildDllPath = Resolve-MsBuildDllPath

Invoke-Step -Name "build" -Action {
  dotnet $msbuildDllPath ClassroomToolkit.sln -restore -property:Configuration=$Configuration -verbosity:minimal
}

if ($Profile -eq "full") {
  Invoke-Step -Name "test" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration --no-restore --no-build --disable-build-servers
  }
}

Invoke-Step -Name "contract-invariant" -Action {
  dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration --no-restore --no-build --disable-build-servers --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
}

Invoke-Step -Name "hotspot" -Action {
  & "$PSScriptRoot\check-hotspot-line-budgets.ps1"
}

if ($EnableGovernanceChecks) {
  Invoke-Step -Name "waiver-health" -Action {
    & "$PSScriptRoot\..\governance\check-waiver-health.ps1"
  }

  Invoke-Step -Name "evidence-completeness" -Action {
    & "$PSScriptRoot\..\governance\check-evidence-completeness.ps1" -Mode all -Threshold 98
  }

  if ($EmitGovernanceReport) {
    Invoke-Step -Name "endstate-doctor-report" -Action {
      & "$PSScriptRoot\..\governance\run-doctor-endstate.ps1" -EvidenceThreshold 98
    }
  }
}

Invoke-Step -Name "stable-tests" -Action {
  & "$PSScriptRoot\..\validation\run-stable-tests.ps1" -Configuration $Configuration -SkipBuild -Profile quick
}

Invoke-BuildServerShutdown

Write-Host "run-local-quality-gates done. profile=$Profile configuration=$Configuration"
