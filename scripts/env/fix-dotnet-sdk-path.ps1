[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SdkChannel = "10",
    [string]$RequiredSdkVersion = "10.0.201",
    [string]$UserDotnetRoot = "$env:USERPROFILE\.dotnet",
    [string]$MachineDotnetRoot = "C:\Program Files\dotnet",
    [switch]$SkipInstall,
    [switch]$SkipPathCleanup,
    [switch]$SkipRebootHint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Remove-PathEntry {
    param(
        [string]$PathValue,
        [string[]]$EntriesToRemove
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    $normalizedRemovals = $EntriesToRemove | ForEach-Object {
        ([System.IO.Path]::GetFullPath($_)).TrimEnd('\')
    }

    $parts = $PathValue -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $kept = foreach ($part in $parts) {
        $trimmed = $part.Trim()
        $full = ""
        try {
            $full = ([System.IO.Path]::GetFullPath($trimmed)).TrimEnd('\')
        }
        catch {
            $full = $trimmed.TrimEnd('\')
        }

        if ($normalizedRemovals -contains $full) {
            continue
        }

        $trimmed
    }

    return ($kept -join ';')
}

function Ensure-PathEntryFront {
    param(
        [string]$PathValue,
        [string]$Entry
    )

    $target = $Entry.TrimEnd('\')
    $parts = @()
    if (-not [string]::IsNullOrWhiteSpace($PathValue)) {
        $parts = $PathValue -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $filtered = foreach ($part in $parts) {
        if ($part.TrimEnd('\') -ieq $target) { continue }
        $part
    }

    $all = @($target)
    if ($filtered) {
        $all += @($filtered)
    }

    return ($all -join ';')
}

if (-not (Test-IsAdmin)) {
    throw "Please run this script as Administrator."
}

Write-Step "Step 1 - Install machine-wide .NET SDK channel $SdkChannel"
if (-not $SkipInstall) {
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget was not found. Install App Installer first, or install .NET SDK system-wide manually."
    }

    $installArgs = @(
        "install",
        "Microsoft.DotNet.SDK.$SdkChannel",
        "--scope", "machine",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )

    if ($PSCmdlet.ShouldProcess("winget", ($installArgs -join ' '))) {
        & winget @installArgs | Out-Host
    }
}
else {
    Write-Host "Skipped SDK installation by -SkipInstall." -ForegroundColor Yellow
}

Write-Step "Step 2 - Remove user-level DOTNET_ROOT overrides"
if ($PSCmdlet.ShouldProcess("User Environment", "Clear DOTNET_ROOT and DOTNET_ROOT_X64")) {
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $null, "User")
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT_X64", $null, "User")
}

if (-not $SkipPathCleanup) {
    Write-Step "Step 3 - Cleanup user PATH entries for $UserDotnetRoot"
    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    $newUserPath = Remove-PathEntry -PathValue $userPath -EntriesToRemove @(
        $UserDotnetRoot,
        (Join-Path $UserDotnetRoot "tools")
    )

    if ($PSCmdlet.ShouldProcess("User PATH", "Remove $UserDotnetRoot and $UserDotnetRoot\tools")) {
        [Environment]::SetEnvironmentVariable("PATH", $newUserPath, "User")
    }
}
else {
    Write-Host "Skipped user PATH cleanup by -SkipPathCleanup." -ForegroundColor Yellow
}

Write-Step "Step 4 - Set machine-level DOTNET_ROOT"
if ($PSCmdlet.ShouldProcess("Machine Environment", "Set DOTNET_ROOT and DOTNET_ROOT_X64")) {
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $MachineDotnetRoot, "Machine")
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT_X64", $MachineDotnetRoot, "Machine")
}

Write-Step "Step 5 - Ensure machine PATH starts with $MachineDotnetRoot"
$machinePath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
$newMachinePath = Ensure-PathEntryFront -PathValue $machinePath -Entry $MachineDotnetRoot
if ($PSCmdlet.ShouldProcess("Machine PATH", "Ensure $MachineDotnetRoot is first")) {
    [Environment]::SetEnvironmentVariable("PATH", $newMachinePath, "Machine")
}

Write-Step "Step 6 - Apply current-session overrides"
$env:DOTNET_ROOT = $MachineDotnetRoot
$env:DOTNET_ROOT_X64 = $MachineDotnetRoot
$env:PATH = Ensure-PathEntryFront -PathValue $env:PATH -Entry $MachineDotnetRoot

Write-Step "Step 7 - Validation"
Write-Host "`nwhere dotnet:" -ForegroundColor Yellow
& where.exe dotnet | Out-Host

Write-Host "`ndotnet --list-sdks:" -ForegroundColor Yellow
& dotnet --list-sdks | Out-Host

Write-Host "`ndotnet --info (filtered):" -ForegroundColor Yellow
& dotnet --info | Select-String "Base Path|\.NET SDKs installed|$RequiredSdkVersion|DOTNET_ROOT" | Out-Host

$installedSdks = & dotnet --list-sdks
$requiredFound = $installedSdks | Where-Object { $_ -match "^$([regex]::Escape($RequiredSdkVersion))\s" }
if (-not $requiredFound) {
    Write-Warning "Required SDK $RequiredSdkVersion was not detected. Verify machine-wide installation."
}
else {
    Write-Host "Detected required SDK $RequiredSdkVersion." -ForegroundColor Green
}

if (-not $SkipRebootHint) {
    Write-Host "`nReboot is recommended now. Then reopen Visual Studio / VSCode and validate again." -ForegroundColor Green
}

