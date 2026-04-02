[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[compat-preflight] START $Name"
    & $Action
    Write-Host "[compat-preflight] PASS  $Name"
}

if (-not $SkipBuild) {
    Invoke-Step -Name "build" -Action {
        dotnet build ClassroomToolkit.sln -c $Configuration
    }
}

Invoke-Step -Name "test(full)" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration
}

Invoke-Step -Name "test(contract)" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
}

Invoke-Step -Name "hotspot" -Action {
    powershell -File scripts/quality/check-hotspot-line-budgets.ps1
}

Write-Host "[compat-preflight] ALL PASS"
