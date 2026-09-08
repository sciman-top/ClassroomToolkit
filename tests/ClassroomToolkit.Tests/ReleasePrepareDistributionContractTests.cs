using System.IO;
using System.Text.Json;
using AwesomeAssertions;

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
    public void ReleaseConfig_RuntimeInstaller_ShouldPinVersionHashAndPublisher()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "release-config.json")));
        var installer = config.RootElement.GetProperty("release").GetProperty("runtimeInstaller");

        installer.GetProperty("downloadUrl").GetString().Should().Contain("builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.11/");
        installer.GetProperty("fileName").GetString().Should().Be("windowsdesktop-runtime-10.0.11-win-x64.exe");
        installer.GetProperty("version").GetString().Should().Be("10.0.11");
        installer.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9A-F]{64}$");
        installer.GetProperty("publisher").GetString().Should().Be("Microsoft Corporation");
    }

    [Fact]
    public void PrepareDistribution_ShouldValidateRuntimeInstallerBeforePackaging()
    {
        var source = ReadPrepareDistributionScript();

        source.Should().Contain("Get-FileHash -LiteralPath $Path -Algorithm SHA256");
        source.Should().Contain("Get-AuthenticodeSignature -LiteralPath $Path");
        source.Should().Contain("VersionInfo.ProductVersion");
        source.Should().Contain("Test-ValidRuntimeInstaller");
    }

    private static string ReadPrepareDistributionScript()
    {
        return File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "release",
            "prepare-distribution.ps1"));
    }
}
