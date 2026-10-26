using System.IO;
using System.Text.Json;
using AwesomeAssertions;

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

        source.Should().Contain("-Kind \"standard\"");
        source.Should().Contain("-Channel \"standard\"");
        source.Should().Contain("-Kind \"offline\"");
        source.Should().Contain("-Channel \"offline\"");
    }

    [Fact]
    public void SourcePackageScript_ShouldArchiveCommittedSourceWithoutClassroomData()
    {
        var source = ReadScript("prepare-source-package.ps1");

        source.Should().Contain("git archive --format=zip");
        source.Should().Contain("excludes_local_classroom_data = $true");
    }

    [Fact]
    public void ScriptHelpers_ShouldPinSharedSourceCommitResolutionAndVersionSegmentGuard()
    {
        var helpers = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "artifacts",
            "ScriptHelpers.psm1"));

        helpers.Should().Contain("git rev-parse --verify --end-of-options");
        helpers.Should().Contain("git status --porcelain --untracked-files=all");
        helpers.Should().Contain("function Assert-SafeReleaseVersionSegment");
    }

    [Fact]
    public void PrivateMigrationScripts_ShouldRequireIntegrityCheckAndRecoverableBackup()
    {
        var prepare = ReadScript("prepare-private-migration.ps1");
        var restore = ReadScript("restore-private-migration.ps1");

        prepare.Should().Contain("Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256");
        restore.Should().Contain("Assert-PackageIntegrity");
        restore.Should().Contain("-BackupExisting");
    }

    [Fact]
    public void PortablePackageScript_ShouldCreateADataSeparatedNotifyOnlyArtifact()
    {
        var source = ReadScript("prepare-portable-package.ps1");

        source.Should().Contain("portable.mode");
        source.Should().Contain("notify-and-open-download-page");
        source.Should().Contain("data/");
    }

    [Fact]
    public void AggregateReleaseScript_ShouldRequireCleanSourceAndDeliveryChecksums()
    {
        var source = ReadScript("prepare-release-artifacts.ps1");

        source.Should().Contain("Resolve-ReleaseSourceCommit");
        source.Should().Contain("Get-DeliveryArtifactHashes");
        source.Should().Contain("Write-DeliveryChecksums");
        source.Should().Contain("SHA256SUMS.txt");
    }

    [Fact]
    public void ReleaseScripts_ShouldPropagateOneResolvedSourceCommit()
    {
        foreach (var scriptName in new[]
                 {
                     "prepare-release-artifacts.ps1",
                     "prepare-user-installers.ps1",
                     "prepare-distribution.ps1",
                     "prepare-portable-package.ps1"
                 })
        {
            ReadScript(scriptName).Should().Contain("source_commit");
        }
    }

    [Fact]
    public void StandardInstallerScript_ShouldRequireRuntimeBootstrapPayload()
    {
        var source = ReadScript("prepare-user-installers.ps1");

        source.Should().Contain("function Assert-StandardInstallerPayload");
        source.Should().Contain("bootstrap-runtime.ps1");
        source.Should().Contain("启动.bat");
        source.Should().Contain("prereq");
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
    }

    private static string ReadScript(string name)
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath("scripts", "release", name));
    }
}
