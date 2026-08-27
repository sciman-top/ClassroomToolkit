[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [ValidateSet("all", "standard", "offline")]
    [string]$PackageMode = "all",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts/release",
    [string]$SourceRef = "HEAD",
    [switch]$EnsureLatestRuntime,
    [switch]$AllowOverwriteVersion,
    [switch]$CreatePrivateMigration,
    [string]$MigrationSourceRoot = "",
    [string]$MigrationId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-ReleaseScript {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "[release-artifacts] START $Name"
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "[release-artifacts] FAIL $Name (exit=$LASTEXITCODE)"
    }
    Write-Host "[release-artifacts] PASS  $Name"
}

$installerArguments = @(
    "-Version", $Version,
    "-PackageMode", $PackageMode,
    "-Configuration", $Configuration,
    "-OutputRoot", $OutputRoot
)
if ($EnsureLatestRuntime) {
    $installerArguments += "-EnsureLatestRuntime"
}
if ($AllowOverwriteVersion) {
    $installerArguments += "-AllowOverwriteVersion"
}
Invoke-ReleaseScript -Name "user-installers" -ScriptPath (Join-Path $PSScriptRoot "prepare-user-installers.ps1") -Arguments $installerArguments

$sourceArguments = @(
    "-Version", $Version,
    "-SourceRef", $SourceRef,
    "-OutputRoot", $OutputRoot
)
if ($AllowOverwriteVersion) {
    $sourceArguments += "-AllowOverwriteVersion"
}
Invoke-ReleaseScript -Name "public-source" -ScriptPath (Join-Path $PSScriptRoot "prepare-source-package.ps1") -Arguments $sourceArguments

if ($CreatePrivateMigration) {
    if ([string]::IsNullOrWhiteSpace($MigrationSourceRoot) -or [string]::IsNullOrWhiteSpace($MigrationId)) {
        throw "-CreatePrivateMigration requires -MigrationSourceRoot and -MigrationId."
    }

    Invoke-ReleaseScript -Name "private-migration" -ScriptPath (Join-Path $PSScriptRoot "prepare-private-migration.ps1") -Arguments @(
        "-Version", $Version,
        "-SourceRoot", $MigrationSourceRoot,
        "-MigrationId", $MigrationId,
        "-AllowOverwrite"
    )
}

Write-Host "[release-artifacts] DONE"
