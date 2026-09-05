using System.IO;
using System.Text.Json;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ReleaseArtifactContractTests
{
    [Fact]
    public void ReleaseConfig_ShouldPinTheVelopackToolAndPublicUpdateRepository()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "release-config.json")));

        var velopack = config.RootElement.GetProperty("release").GetProperty("velopack");
        velopack.GetProperty("toolVersion").GetString().Should().Be("1.2.0");
        velopack.GetProperty("packageId").GetString().Should().Be("ClassroomToolkit");
        velopack.GetProperty("repositoryUrl").GetString().Should().Be("https://github.com/sciman-top/ClassroomToolkit");
    }

    [Fact]
    public void UserInstallerScript_ShouldKeepStandardAndOfflineUpdateChannelsSeparate()
    {
        var source = ReadScript("prepare-user-installers.ps1");

        source.Should().Contain("\"--channel\", $Channel");
        source.Should().Contain("-Kind \"standard\"");
        source.Should().Contain("-Channel \"standard\"");
        source.Should().Contain("-Kind \"offline\"");
        source.Should().Contain("-Channel \"offline\"");
        source.Should().Contain("-Framework $standardFramework");
        source.Should().Contain("dotnet @vpkArguments");
    }

    [Fact]
    public void SourcePackageScript_ShouldArchiveCommittedSourceWithoutClassroomData()
    {
        var source = ReadScript("prepare-source-package.ps1");

        source.Should().Contain("git archive --format=zip");
        source.Should().Contain("git rev-parse --verify --end-of-options");
        source.Should().Contain("ResolvedSourceCommit");
        source.Should().Contain("git status --porcelain --untracked-files=no");
        source.Should().Contain("clean tracked worktree");
        source.Should().Contain("excludes_local_classroom_data = $true");
    }

    [Fact]
    public void PrivateMigrationScripts_ShouldRequireIntegrityCheckAndRecoverableBackup()
    {
        var prepare = ReadScript("prepare-private-migration.ps1");
        var restore = ReadScript("restore-private-migration.ps1");

        prepare.Should().Contain("migration-manifest.json");
        prepare.Should().Contain("Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256");
        prepare.Should().Contain("Restore-PrivateMigration.ps1");
        restore.Should().Contain("Assert-PackageIntegrity");
        restore.Should().Contain("-BackupExisting");
        restore.Should().Contain("Move-Item -LiteralPath $resolvedTargetRoot -Destination $backupRoot");
    }

    [Fact]
    public void PortablePackageScript_ShouldCreateADataSeparatedNotifyOnlyArtifact()
    {
        var source = ReadScript("prepare-portable-package.ps1");

        source.Should().Contain("portable.mode");
        source.Should().Contain("portable-release.json");
        source.Should().Contain("api.github.com/repos");
        source.Should().Contain("notify-and-open-download-page");
        source.Should().Contain("ClassroomToolkit-{0}-portable.zip");
        source.Should().Contain("ResolvedSourceCommit");
        source.Should().Contain("portable/{0}");
        source.Should().Contain("data/");
    }

    [Fact]
    public void AggregateReleaseScript_ShouldCleanStagingAndLeaveFinalDeliveryPaths()
    {
        var source = ReadScript("prepare-release-artifacts.ps1");

        source.Should().Contain(".staging");
        source.Should().Contain("staging_cleaned = $true");
        source.Should().Contain("Remove-Item -LiteralPath $stagingReleaseRoot -Recurse -Force");
        source.Should().Contain("standard_installer");
        source.Should().Contain("offline_installer");
        source.Should().Contain("ClassroomToolkit-{0}-portable.zip");
        source.Should().Contain("ResolvedSourceCommit");
        source.Should().Contain("$sourceCommit = (& git rev-parse");
    }

    [Fact]
    public void ArtifactLayout_ShouldKeepDeliveryEvidenceHistoryAndPrivateRootsSeparate()
    {
        var layout = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "artifacts",
            "ArtifactLayout.psm1"));

        layout.Should().Contain("artifacts/release");
        layout.Should().Contain("artifacts/private-migration");
        layout.Should().Contain("artifacts/evidence/quality/current");
        layout.Should().Contain("artifacts/evidence/tests/current");
        layout.Should().Contain("artifacts/evidence/validation/current");
        layout.Should().Contain("artifacts/evidence/release-preflight/current");
        layout.Should().Contain("artifacts/archive/legacy-outputs");
        layout.Should().Contain("Export-ModuleMember -Function Get-ClassroomToolkitArtifactPath");
    }

    [Fact]
    public void EvidenceScripts_ShouldUseStableCurrentFilenames()
    {
        ReadRepoFile("scripts", "validation", "run-stable-tests.ps1").Should().Contain("EvidenceTestsCurrent");
        ReadRepoFile("scripts", "validation", "collect-ui-performance-samples.ps1").Should().Contain("ui-performance-samples.json");
        ReadRepoFile("scripts", "validation", "collect-settings-load-performance-samples.ps1").Should().Contain("settings-load-performance-summary.json");
        ReadRepoFile("scripts", "quality", "check-analyzer-backlog-baseline.ps1").Should().Contain("EvidenceQualityCurrent");
        ReadRepoFile("scripts", "release", "preflight-check.ps1").Should().Contain("EvidenceReleasePreflightCurrent");
    }

    private static string ReadScript(string name)
    {
        return ReadRepoFile("scripts", "release", name);
    }

    private static string ReadRepoFile(params string[] pathParts)
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath(pathParts));
    }
}
