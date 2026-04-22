[CmdletBinding()]
param(
    [ValidateSet("quick", "standard", "full")]
    [string]$Profile = "standard",
    [string]$Configuration = "Debug"
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

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$Arguments = @()
    )

    Write-Host "[quality] START $Name"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "[quality] FAIL  $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[quality] PASS  $Name"
}

Invoke-NativeStep -Name "build" -FilePath "dotnet" -Arguments @(
    "build",
    "ClassroomToolkit.sln",
    "-c",
    $Configuration,
    "-m:1"
)

$stableTestsScript = Join-Path $PSScriptRoot "..\validation\run-stable-tests.ps1"
$stableConfigValidator = Join-Path $PSScriptRoot "..\validation\validate-stable-test-config.ps1"
$powerShellExe = Resolve-PowerShellExecutable
# Contract guard: keep literal "-Profile $Profile" in source for profile propagation verification.
if ((Test-Path -LiteralPath $stableTestsScript) -and (Test-Path -LiteralPath $stableConfigValidator)) {
    Invoke-NativeStep -Name "stable-tests-config" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $stableConfigValidator
    )
    Invoke-NativeStep -Name "stable-tests" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $stableTestsScript,
        "-Configuration",
        $Configuration,
        "-Profile",
        $Profile
    )
}
else {
    Invoke-NativeStep -Name "test(full)" -FilePath "dotnet" -Arguments @(
        "test",
        "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
        "-c",
        $Configuration,
        "-m:1"
    )
}

Invoke-NativeStep -Name "test(contract)" -FilePath "dotnet" -Arguments @(
    "test",
    "tests/ClassroomToolkit.Tests/ClassroomToolkit.Tests.csproj",
    "-c",
    $Configuration,
    "-m:1",
    "--filter",
    "FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~InteropHookLifecycleContractTests|FullyQualifiedName~InteropHookEventDispatchContractTests|FullyQualifiedName~GlobalHookServiceLifecycleContractTests|FullyQualifiedName~CrossPageDisplayLifecycleContractTests"
)

Invoke-NativeStep -Name "hotspot" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-hotspot-line-budgets.ps1"
)

Invoke-NativeStep -Name "governance-truth-source" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-governance-truth-source.ps1"
)

Invoke-NativeStep -Name "dependency-governance" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-dependency-upgrade-feasibility.ps1"
)

Invoke-NativeStep -Name "dependency-vulnerability" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-dependency-vulnerabilities.ps1"
)

Invoke-NativeStep -Name "logging-alert-threshold" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-logging-alert-threshold.ps1"
)

Invoke-NativeStep -Name "analyzer-backlog-baseline" -FilePath $powerShellExe -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "scripts/quality/check-analyzer-backlog-baseline.ps1",
    "-Configuration",
    $Configuration
)

Write-Host "[quality] ALL PASS"
