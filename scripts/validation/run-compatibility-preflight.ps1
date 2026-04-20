[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-PowerShellExecutable {
    if (Get-Command "pwsh" -ErrorAction SilentlyContinue) {
        return "pwsh"
    }

    if (Get-Command "powershell" -ErrorAction SilentlyContinue) {
        return "powershell"
    }

    throw "No PowerShell executable found. Expected 'pwsh' or 'powershell'."
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[compat-preflight] START $Name"
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "[compat-preflight] FAIL  $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[compat-preflight] PASS  $Name"
}

if (-not $SkipBuild) {
    Invoke-Step -Name "build" -Action {
        dotnet build ClassroomToolkit.sln -c $Configuration -m:1
    }
}

Invoke-Step -Name "test(full)" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration -m:1
}

Invoke-Step -Name "test(contract)" -Action {
    dotnet test tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj -c $Configuration -m:1 --filter "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
}

Invoke-Step -Name "hotspot" -Action {
    $powerShellExe = Resolve-PowerShellExecutable
    & $powerShellExe -NoProfile -ExecutionPolicy Bypass -File scripts/quality/check-hotspot-line-budgets.ps1
}

Write-Host "[compat-preflight] ALL PASS"
