[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [ValidateSet("all", "standard", "offline")]
    [string]$PackageMode = "all",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string]$SourceRef = "HEAD",
    [switch]$EnsureLatestRuntime,
    [switch]$AllowOverwriteVersion,
    [switch]$CreatePrivateMigration,
    [string]$MigrationSourceRoot = "",
    [string]$MigrationId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Get-ClassroomToolkitArtifactPath -Name ReleaseRoot
}

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
    "-OutputRoot", (Join-Path $OutputRoot ".staging")
)
if ($EnsureLatestRuntime) {
    $installerArguments += "-EnsureLatestRuntime"
}
if ($AllowOverwriteVersion) {
    $installerArguments += "-AllowOverwriteVersion"
}
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot } else { Join-Path (Get-Location) $OutputRoot }
$stagingOutputRoot = Join-Path $outputRootPath ".staging"
$stagingReleaseRoot = Join-Path $stagingOutputRoot $Version
$sourceCommit = (& git rev-parse --verify --end-of-options "$SourceRef^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw "SourceRef does not resolve to a commit: $SourceRef"
}

if (Test-Path -LiteralPath (Join-Path $outputRootPath $Version)) {
    if (-not $AllowOverwriteVersion) {
        throw "Release directory already exists: $(Join-Path $outputRootPath $Version). Pass -AllowOverwriteVersion to overwrite."
    }
    Remove-Item -LiteralPath (Join-Path $outputRootPath $Version) -Recurse -Force
}
if (Test-Path -LiteralPath $stagingReleaseRoot) {
    if (-not $AllowOverwriteVersion) {
        throw "Staging release directory already exists: $stagingReleaseRoot. Pass -AllowOverwriteVersion to overwrite."
    }
    Remove-Item -LiteralPath $stagingReleaseRoot -Recurse -Force
}
$completed = $false
try {
    Invoke-ReleaseScript -Name "user-installers" -ScriptPath (Join-Path $PSScriptRoot "prepare-user-installers.ps1") -Arguments $installerArguments

    if ($PackageMode -in @("all", "offline")) {
        Invoke-ReleaseScript -Name "portable-package" -ScriptPath (Join-Path $PSScriptRoot "prepare-portable-package.ps1") -Arguments @(
            "-Version", $Version,
            "-OutputRoot", (Join-Path $OutputRoot ".staging"),
            "-RepositoryUrl", "https://github.com/sciman-top/ClassroomToolkit",
            "-SourceRef", $SourceRef,
            "-ResolvedSourceCommit", $sourceCommit
        )
    }

    $sourceArguments = @(
        "-Version", $Version,
        "-SourceRef", $SourceRef,
        "-ResolvedSourceCommit", $sourceCommit,
        "-OutputRoot", (Join-Path $OutputRoot ".staging")
    )
    if ($AllowOverwriteVersion) {
        $sourceArguments += "-AllowOverwriteVersion"
    }
    Invoke-ReleaseScript -Name "public-source" -ScriptPath (Join-Path $PSScriptRoot "prepare-source-package.ps1") -Arguments $sourceArguments

    $stagingItems = @(
        "installer",
        (Join-Path "installer" "user-installers-manifest.json"),
        (Join-Path "source" ("ClassroomToolkit-Source-{0}.zip" -f $Version)),
        (Join-Path "source" "source-package-manifest.json")
    )
    if ($PackageMode -in @("all", "offline")) {
        $stagingItems += @(
            (Join-Path "portable" ("ClassroomToolkit-{0}-portable.zip" -f $Version)),
            (Join-Path "portable" "portable-package-manifest.json")
        )
    }
    $missingItems = @($stagingItems | Where-Object { -not (Test-Path -LiteralPath (Join-Path $stagingReleaseRoot $_)) })
    if ($missingItems.Count -gt 0) {
        throw "Release staging is missing required delivery items: $($missingItems -join ', ')"
    }

    New-Item -ItemType Directory -Path (Join-Path $outputRootPath $Version) -Force | Out-Null
    $releaseRoot = Join-Path $outputRootPath $Version
    $deliveryDirectories = @("installer", "source")
    if ($PackageMode -in @("all", "offline")) {
        $deliveryDirectories += "portable"
    }
    foreach ($relativePath in $deliveryDirectories) {
        $sourcePath = Join-Path $stagingReleaseRoot $relativePath
        $destinationPath = Join-Path $releaseRoot $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Move-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
    }

    $manifest = [ordered]@{
        version = $Version
        generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
        package_mode = $PackageMode
        configuration = $Configuration
        source_ref = $SourceRef
        source_commit = $sourceCommit
        staging_cleaned = $true
        outputs = [ordered]@{
            standard_installer = if ($PackageMode -in @("all", "standard")) { "installer/standard" } else { $null }
            offline_installer = if ($PackageMode -in @("all", "offline")) { "installer/offline" } else { $null }
            portable = if ($PackageMode -in @("all", "offline")) { "portable/ClassroomToolkit-{0}-portable.zip" -f $Version } else { $null }
            source = "source/ClassroomToolkit-Source-{0}.zip" -f $Version
        }
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseRoot "release-manifest.json") -Encoding UTF8
    $completed = $true
}
finally {
    if ($completed -and (Test-Path -LiteralPath $stagingReleaseRoot)) {
        Remove-Item -LiteralPath $stagingReleaseRoot -Recurse -Force
    }
    if ($completed -and (Test-Path -LiteralPath $stagingOutputRoot)) {
        $remaining = Get-ChildItem -LiteralPath $stagingOutputRoot -Force -ErrorAction SilentlyContinue
        if ($null -eq $remaining -or $remaining.Count -eq 0) {
            Remove-Item -LiteralPath $stagingOutputRoot -Force
        }
    }
}

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
