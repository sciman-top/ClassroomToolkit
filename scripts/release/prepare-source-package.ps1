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

$scriptHelpersModule = Join-Path $PSScriptRoot "..\artifacts\ScriptHelpers.psm1"
Import-Module -Name $scriptHelpersModule -Force

Assert-SafeReleaseVersionSegment -Value $Version
$commit = Resolve-ReleaseSourceCommit -SourceRef $SourceRef -ResolvedSourceCommit $ResolvedSourceCommit -ScenarioLabel 'source'

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
