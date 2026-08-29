[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$MigrationId,
    [string]$OutputRoot = "",
    [switch]$AllowOverwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Get-ClassroomToolkitArtifactPath -Name PrivateMigrationRoot
}

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Assert-SafeSegment {
    param([Parameter(Mandatory = $true)][string]$Value, [Parameter(Mandatory = $true)][string]$Label)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Trim() -ne $Value -or $Value -eq "." -or $Value -eq ".." -or
        $Value.Contains([System.IO.Path]::DirectorySeparatorChar) -or $Value.Contains([System.IO.Path]::AltDirectorySeparatorChar) -or
        $Value.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Invalid $Label '$Value'."
    }
}

function Copy-MigrationItem {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $parent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    if ((Get-Item -LiteralPath $Source).PSIsContainer) {
        Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
    }
    else {
        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
}

Assert-SafeSegment -Value $Version -Label "release version"
Assert-SafeSegment -Value $MigrationId -Label "migration id"
$resolvedSourceRoot = Resolve-AbsolutePath -Path $SourceRoot
if (-not (Test-Path -LiteralPath $resolvedSourceRoot -PathType Container)) {
    throw "Migration source root does not exist: $resolvedSourceRoot"
}

$packageRoot = Join-Path (Resolve-AbsolutePath -Path $OutputRoot) ("ClassroomToolkit-Migration-{0}-{1}" -f $Version, $MigrationId)
if (Test-Path -LiteralPath $packageRoot) {
    if (-not $AllowOverwrite) {
        throw "Migration package already exists: $packageRoot"
    }

    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

$payloadRoot = Join-Path $packageRoot "payload"
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
$candidates = @("data", "settings.ini", "settings.json")
foreach ($relativePath in $candidates) {
    $sourcePath = Join-Path $resolvedSourceRoot $relativePath
    if (Test-Path -LiteralPath $sourcePath) {
        Copy-MigrationItem -Source $sourcePath -Destination (Join-Path $payloadRoot $relativePath)
    }
}

$payloadFiles = @(Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Sort-Object FullName)
if ($payloadFiles.Count -eq 0) {
    throw "No supported classroom data was found under: $resolvedSourceRoot"
}

$files = @($payloadFiles | ForEach-Object {
    [ordered]@{
        relative_path = $_.FullName.Substring($payloadRoot.Length).TrimStart('\\').Replace('\\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        length = $_.Length
    }
})
$sourceCommit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve current source commit."
}

$manifest = [ordered]@{
    version = $Version
    migration_id = $MigrationId
    source_commit = $sourceCommit
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
    source_root = $resolvedSourceRoot
    files = $files
    restore_requires_backup_for_nonempty_target = $true
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $packageRoot "migration-manifest.json") -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "restore-private-migration.ps1") -Destination (Join-Path $packageRoot "Restore-PrivateMigration.ps1") -Force
Write-Host "[private-migration] DONE $packageRoot"
