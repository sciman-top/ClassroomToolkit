param(
  [ValidateSet("quick", "full")]
  [string]$Profile = "quick",
  [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )

  Write-Host "=== $Name ==="
  & $Action
  if ($LASTEXITCODE -ne 0) {
    throw "Step failed: $Name (exit=$LASTEXITCODE)"
  }
}

Invoke-Step -Name "build" -Action {
  dotnet build ClassroomToolkit.sln -c $Configuration
}

if ($Profile -eq "full") {
  Invoke-Step -Name "test" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration
  }
}

Invoke-Step -Name "contract-invariant" -Action {
  dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
}

Invoke-Step -Name "hotspot" -Action {
  & "$PSScriptRoot\check-hotspot-line-budgets.ps1"
}

Invoke-Step -Name "stable-tests" -Action {
  & "$PSScriptRoot\..\validation\run-stable-tests.ps1" -Configuration $Configuration -SkipBuild -Profile quick
}

Write-Host "run-local-quality-gates done. profile=$Profile configuration=$Configuration"
