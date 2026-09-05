[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$SourceRef = "HEAD",
    [string]$ResolvedSourceCommit = "",
    [string]$OutputRoot = "",
    [switch]$AllowOverwriteVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$artifactLayoutModule = Join-Path $PSScriptRoot "..\artifacts\ArtifactLayout.psm1"
Import-Module -Name $artifactLayoutModule -Force
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Get-ClassroomToolkitArtifactPath -Name ReleaseRoot
}

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path (Get-Location) $Path
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
$trackedChanges = & git status --porcelain --untracked-files=no
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the Git worktree."
}
if (-not [string]::IsNullOrWhiteSpace(($trackedChanges -join "`n"))) {
    throw "Source package requires a clean tracked worktree so the installer and source archive resolve to the same commit."
}

$commitRef = if ([string]::IsNullOrWhiteSpace($ResolvedSourceCommit)) {
    "$SourceRef^{commit}"
}
else {
    "$ResolvedSourceCommit^{commit}"
}
$commit = (& git rev-parse --verify --end-of-options $commitRef).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw "Source reference does not resolve to a commit: $commitRef"
}

$releaseRoot = Join-Path (Resolve-AbsolutePath -Path $OutputRoot) $Version
$sourceDeliveryRoot = Join-Path $releaseRoot "source"
New-Item -ItemType Directory -Path $sourceDeliveryRoot -Force | Out-Null
$zipPath = Join-Path $sourceDeliveryRoot ("ClassroomToolkit-Source-{0}.zip" -f $Version)
if (Test-Path -LiteralPath $zipPath) {
    if (-not $AllowOverwriteVersion) {
        throw "Source package already exists: $zipPath"
    }

    Remove-Item -LiteralPath $zipPath -Force
}

& git archive --format=zip ("--prefix=ClassroomToolkit-{0}/" -f $Version) --output=$zipPath $commit
if ($LASTEXITCODE -ne 0) {
    throw "git archive failed (exit=$LASTEXITCODE)"
}

$manifest = [ordered]@{
    version = $Version
    source_ref = $SourceRef
    source_commit = $commit
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
    artifact = Split-Path -Leaf $zipPath
    sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    excludes_local_classroom_data = $true
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $sourceDeliveryRoot "source-package-manifest.json") -Encoding UTF8
Write-Host "[source-package] DONE $zipPath"
