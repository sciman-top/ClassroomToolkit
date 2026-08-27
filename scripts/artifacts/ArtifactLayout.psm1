Set-StrictMode -Version Latest

$script:ArtifactPaths = @{
    ReleaseRoot = "artifacts/release"
    PrivateMigrationRoot = "artifacts/private-migration"
    EvidenceQualityCurrent = "artifacts/evidence/quality/current"
    EvidenceTestsCurrent = "artifacts/evidence/tests/current"
    EvidenceValidationCurrent = "artifacts/evidence/validation/current"
    EvidenceReleasePreflightCurrent = "artifacts/evidence/release-preflight/current"
    LegacyArchiveRoot = "artifacts/archive/legacy-outputs"
}

function Get-ClassroomToolkitArtifactPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet(
            "ReleaseRoot",
            "PrivateMigrationRoot",
            "EvidenceQualityCurrent",
            "EvidenceTestsCurrent",
            "EvidenceValidationCurrent",
            "EvidenceReleasePreflightCurrent",
            "LegacyArchiveRoot")]
        [string]$Name
    )

    return $script:ArtifactPaths[$Name]
}

Export-ModuleMember -Function Get-ClassroomToolkitArtifactPath
