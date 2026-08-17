using System.IO;
using System.Text.Json;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ReleasePrepareDistributionContractTests
{
    [Fact]
    public void PrepareDistribution_ShouldRejectUnsafeReleaseVersionSegments()
    {
        var source = ReadPrepareDistributionScript();

        source.Should().Contain("function Assert-SafeReleaseVersionSegment");
        source.Should().Contain("[System.IO.Path]::GetInvalidFileNameChars()");
        source.Should().Contain("[System.IO.Path]::DirectorySeparatorChar");
        source.Should().Contain("[System.IO.Path]::AltDirectorySeparatorChar");
        source.Should().Contain("Assert-SafeReleaseVersionSegment -Value $Version");
    }

    [Fact]
    public void PrepareDistribution_ShouldRequireHttpsRuntimeInstallerDownloads()
    {
        var source = ReadPrepareDistributionScript();

        source.Should().Contain("function Assert-HttpsDownloadUrl");
        source.Should().Contain("[System.Uri]::UriSchemeHttps");
        source.Should().Contain("Assert-HttpsDownloadUrl -DownloadUrl $DownloadUrl");
        source.Should().Contain("Invoke-WebRequest -Uri $DownloadUrl -OutFile $targetPath");
    }

    [Fact]
    public void PrepareDistribution_ShouldRejectArchivedPdfiumFromBothPackages()
    {
        var source = ReadPrepareDistributionScript();

        source.Should().Contain("function Assert-FileDoesNotExistByName");
        source.Should().Contain("Assert-FileDoesNotExistByName -Root $standardApp -Name \"pdfium.dll\"");
        source.Should().Contain("Assert-FileDoesNotExistByName -Root $offlineApp -Name \"pdfium.dll\"");
        source.Should().NotContain("Assert-FileExistsByName -Root $standardApp -Name \"pdfium.dll\"");
    }

    [Fact]
    public void PrepareDistribution_ShouldPreserveCommittedPackageLocks()
    {
        var source = ReadPrepareDistributionScript();

        source.Should().Contain("\"-p:NuGetLockFilePath=obj/release-packages.lock.json\"");
        source.Should().Contain("\"-p:RestoreForceEvaluate=true\"");
    }

    [Fact]
    public void ReleaseConfig_LatestRuntimeAlias_ShouldUsePatchNeutralFileName()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "release-config.json")));
        var installer = config.RootElement.GetProperty("release").GetProperty("runtimeInstaller");

        installer.GetProperty("downloadUrl").GetString().Should().Contain("aka.ms/dotnet/10.0/");
        installer.GetProperty("fileName").GetString().Should().Be("windowsdesktop-runtime-10-latest-win-x64.exe");
    }

    private static string ReadPrepareDistributionScript()
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "prepare-distribution.ps1"));
    }
}
