[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("quick", "standard", "full")]
    [string]$Profile = "standard",
    [switch]$SkipTests,
    [switch]$SkipCompatibilityReport,
    [switch]$SkipUiPerformanceSampling,
    [switch]$SkipSettingsLoadPerformanceSampling,
    [string]$OutputRoot = "artifacts/release"
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
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$Arguments = @()
    )

    Write-Host "[release-preflight] START $Name"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "[release-preflight] FAIL  $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[release-preflight] PASS  $Name"
}

$powerShellExe = Resolve-PowerShellExecutable
$outputRootPath = Resolve-Path "." | ForEach-Object { Join-Path $_.Path $OutputRoot }
New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

if ($SkipTests) {
    Invoke-Step -Name "build" -FilePath "dotnet" -Arguments @(
        "build",
        "ClassroomToolkit.sln",
        "-c",
        $Configuration,
        "-m:1"
    )
}
else {
    Invoke-Step -Name "quality-gates" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/quality/run-local-quality-gates.ps1",
        "-Profile",
        $Profile,
        "-Configuration",
        $Configuration
    )
}

if (-not $SkipCompatibilityReport) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportMd = Join-Path $outputRootPath ("preflight-compatibility-report-{0}.md" -f $stamp)
    $reportJson = Join-Path $outputRootPath ("preflight-compatibility-report-{0}.json" -f $stamp)

    Invoke-Step -Name "compatibility-report" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/validation/run-compatibility-matrix-report.ps1",
        "-Configuration",
        $Configuration,
        "-MatrixId",
        ("PRE-{0}" -f $stamp),
        "-EmitJson",
        "-OutputPath",
        $reportMd,
        "-OutputJsonPath",
        $reportJson
    )
}

if (-not $SkipUiPerformanceSampling) {
    Invoke-Step -Name "ui-performance-sampling" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/validation/collect-ui-performance-samples.ps1",
        "-OutputRoot",
        (Join-Path $outputRootPath "validation")
    )
}

if (-not $SkipSettingsLoadPerformanceSampling) {
    Invoke-Step -Name "settings-load-performance-sampling" -FilePath $powerShellExe -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "scripts/validation/collect-settings-load-performance-samples.ps1",
        "-Configuration",
        $Configuration,
        "-OutputRoot",
        (Join-Path $outputRootPath "validation")
    )
}

Write-Host "[release-preflight] ALL PASS"
