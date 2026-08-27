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

    private static string ReadScript(string name)
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath("scripts", "release", name));
    }
}
