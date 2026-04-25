using System.IO;
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

    private static string ReadPrepareDistributionScript()
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "prepare-distribution.ps1"));
    }
}
