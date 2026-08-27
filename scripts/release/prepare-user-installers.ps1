[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [ValidateSet("all", "standard", "offline")]
    [string]$PackageMode = "all",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string]$ConfigPath = "scripts/release/release-config.json",
    [switch]$EnsureLatestRuntime,
    [switch]$AllowOverwriteVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Get-ClassroomToolkitArtifactPath -Name ReleaseRoot
}

$environmentBootstrap = Join-Path $PSScriptRoot "..\env\Initialize-WindowsProcessEnvironment.ps1"
if (Test-Path -LiteralPath $environmentBootstrap) {
    . $environmentBootstrap
}

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path (Get-Location) $Path
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[user-installer] START $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "[user-installer] FAIL  $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[user-installer] PASS  $Name"
}

function Assert-SafeReleaseVersionSegment {
    param([Parameter(Mandatory = $true)][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Trim() -ne $Value -or $Value -eq "." -or $Value -eq "..") {
        throw "Invalid release version '$Value'."
    }

    if ($Value.Contains([System.IO.Path]::DirectorySeparatorChar) -or
        $Value.Contains([System.IO.Path]::AltDirectorySeparatorChar) -or
        $Value.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Invalid release version '$Value': version must be a single safe directory name."
    }
}

Assert-SafeReleaseVersionSegment -Value $Version
$resolvedConfigPath = Resolve-AbsolutePath -Path $ConfigPath
$config = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
$releaseConfig = $config.release
if ($null -eq $releaseConfig -or $null -eq $releaseConfig.velopack) {
    throw "Invalid release config: release.velopack is required."
}

$outputRootPath = Resolve-AbsolutePath -Path $OutputRoot
$releaseRoot = Join-Path $outputRootPath $Version
$distributionScript = Join-Path $PSScriptRoot "prepare-distribution.ps1"
$distributionArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $distributionScript,
    "-Version", $Version,
    "-PackageMode", $PackageMode,
    "-Configuration", $Configuration,
    "-OutputRoot", $OutputRoot,
    "-ConfigPath", $ConfigPath,
    "-SkipZip"
)
if ($EnsureLatestRuntime) {
    $distributionArguments += "-EnsureLatestRuntime"
}
if ($AllowOverwriteVersion) {
    $distributionArguments += "-AllowOverwriteVersion"
}

Invoke-Step -Name "prepare-publish-output" -Action {
    pwsh @distributionArguments
}

$appExecutable = [string]$releaseConfig.appExecutableName
$runtimeIdentifier = [string]$releaseConfig.runtimeIdentifier
$packageId = [string]$releaseConfig.velopack.packageId
$title = [string]$releaseConfig.appExecutableName
$iconPath = Join-Path (Resolve-AbsolutePath -Path ".") "icon.ico"
$standardFramework = [string]$releaseConfig.velopack.standardFramework

function Invoke-VelopackPack {
    param(
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$PackageDirectory,
        [Parameter(Mandatory = $true)][string]$Channel,
        [string]$Framework = ""
    )

    if (-not (Test-Path -LiteralPath $PackageDirectory)) {
        throw "Publish output is missing: $PackageDirectory"
    }

    $installerRoot = Join-Path $releaseRoot (Join-Path "installer" $Kind)
    New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null
    $vpkArguments = @(
        "vpk", "pack",
        "--outputDir", $installerRoot,
        "--channel", $Channel,
        "--runtime", $runtimeIdentifier,
        "--packId", $packageId,
        "--packVersion", $Version,
        "--packDir", $PackageDirectory,
        "--packAuthors", "sciman",
        "--packTitle", $title,
        "--icon", $iconPath,
        "--mainExe", $appExecutable
    )
    if (-not [string]::IsNullOrWhiteSpace($Framework)) {
        $vpkArguments += @("--framework", $Framework)
    }

    Invoke-Step -Name ("velopack-{0}" -f $Kind) -Action {
        dotnet @vpkArguments
    }
}

if ($PackageMode -in @("all", "standard")) {
    Invoke-VelopackPack -Kind "standard" -PackageDirectory (Join-Path $releaseRoot "standard\app") -Channel "standard" -Framework $standardFramework
}

if ($PackageMode -in @("all", "offline")) {
    Invoke-VelopackPack -Kind "offline" -PackageDirectory (Join-Path $releaseRoot "offline\app") -Channel "offline"
}

$installerManifest = [ordered]@{
    version = $Version
    package_id = $packageId
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
    package_mode = $PackageMode
    update_repository = [string]$releaseConfig.velopack.repositoryUrl
    update_channels = [ordered]@{
        standard = if ($PackageMode -in @("all", "standard")) { "standard" } else { $null }
        offline = if ($PackageMode -in @("all", "offline")) { "offline" } else { $null }
    }
}
$installerManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $releaseRoot "user-installers-manifest.json") -Encoding UTF8
Write-Host "[user-installer] DONE"
