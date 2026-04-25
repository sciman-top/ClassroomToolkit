[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$MatrixId = "BL-UNKNOWN",
    [string]$PresentationVendor = "Unknown",
    [string]$PresentationEdition = "Unknown",
    [string]$PresentationVersion = "Unknown",
    [string]$PresentationProcessName = "Unknown",
    [string]$PresentationClassSignature = "Unknown",
    [string]$PresentationArch = "Unknown",
    [string]$PrivilegeMatch = "Unknown",
    [string]$OutputPath = "",
    [string]$OutputJsonPath = "",
    [switch]$EmitJson,
    [switch]$FailOnPreflightFailure,
    [switch]$RunPreflight
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Get-VcppRuntimeVersion {
    try {
        $key = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" -ErrorAction Stop
        if ($null -ne $key -and $key.Installed -eq 1) {
            return "{0}.{1}.{2}" -f $key.Major, $key.Minor, $key.Bld
        }
        return "NotInstalled"
    }
    catch {
        return "Unknown"
    }
}

function Get-Elevation {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            return "Admin"
        }
        return "Standard"
    }
    catch {
        return "Unknown"
    }
}

$machine = $env:COMPUTERNAME
$os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
$procArch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
$dotnetVersion = (& dotnet --version)
$vcpp = Get-VcppRuntimeVersion
$appElevation = Get-Elevation

$preflightResult = "NotRun"
$preflightDetail = "Skipped"
if ($RunPreflight) {
    try {
        pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/validation/run-compatibility-preflight.ps1 -Configuration $Configuration | Out-Host
        $preflightResult = "Pass"
        $preflightDetail = "scripts/validation/run-compatibility-preflight.ps1"
    }
    catch {
        $preflightResult = "Fail"
        $preflightDetail = $_.Exception.Message
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $safeMachine = ($machine -replace "[^a-zA-Z0-9_-]", "-")
    $OutputPath = "docs/compatibility/reports/$stamp-$safeMachine-$MatrixId.md"
}

$resolvedStatus = "Pending"
if ($preflightResult -eq "Pass") {
    $resolvedStatus = "Pass"
}
elseif ($preflightResult -eq "Fail") {
    $resolvedStatus = "Fail"
}

if (($EmitJson -or -not [string]::IsNullOrWhiteSpace($OutputJsonPath)) -and [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
    $OutputJsonPath = [System.IO.Path]::ChangeExtension($OutputPath, ".json")
}

$outputDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$now = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$content = @"
# Compatibility Matrix Execution Report

Date: $now
Matrix ID: $MatrixId
Machine: $machine

## Environment
- OS: $os
- OS architecture: $osArch
- App process architecture: $procArch
- .NET runtime: $dotnetVersion
- VC++ runtime (x64): $vcpp
- App elevation: $appElevation

## Presentation Software
- Vendor: $PresentationVendor
- Edition/channel: $PresentationEdition
- Version: $PresentationVersion
- Process name: $PresentationProcessName
- Class/window signature: $PresentationClassSignature
- Presentation architecture: $PresentationArch
- Privilege consistency: $PrivilegeMatch

## Gate Evidence
- Preflight result: $preflightResult
- Preflight detail: $preflightDetail

## Verdict
- Status: $resolvedStatus
- Notes: Fill compatibility observations and remediation actions here.
"@

Set-Content -Path $OutputPath -Value $content -Encoding UTF8
Write-Host "[compat-matrix] report generated: $OutputPath"

if ($EmitJson -or -not [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
    $jsonOutputDir = Split-Path -Parent $OutputJsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonOutputDir)) {
        New-Item -ItemType Directory -Force -Path $jsonOutputDir | Out-Null
    }

    $jsonPayload = [ordered]@{
        generatedAt = $now
        matrixId = $MatrixId
        machine = $machine
        environment = [ordered]@{
            os = $os
            osArchitecture = $osArch
            appProcessArchitecture = $procArch
            dotnetRuntime = $dotnetVersion
            vcppRuntimeX64 = $vcpp
            appElevation = $appElevation
        }
        presentation = [ordered]@{
            vendor = $PresentationVendor
            edition = $PresentationEdition
            version = $PresentationVersion
            processName = $PresentationProcessName
            classSignature = $PresentationClassSignature
            architecture = $PresentationArch
            privilegeConsistency = $PrivilegeMatch
        }
        gate = [ordered]@{
            preflightResult = $preflightResult
            preflightDetail = $preflightDetail
        }
        verdict = [ordered]@{
            status = $resolvedStatus
            markdownReport = $OutputPath
        }
    }

    $jsonText = $jsonPayload | ConvertTo-Json -Depth 8
    Set-Content -Path $OutputJsonPath -Value $jsonText -Encoding UTF8
    Write-Host "[compat-matrix] json generated: $OutputJsonPath"
}

if ($FailOnPreflightFailure -and $RunPreflight -and $preflightResult -eq "Fail") {
    Write-Error "[compat-matrix] preflight failed and FailOnPreflightFailure is enabled."
    exit 2
}
