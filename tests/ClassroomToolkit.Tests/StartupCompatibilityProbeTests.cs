using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityProbeTests
{
    [Fact]
    public void BuildPresentationProcessTokens_ShouldIncludeDefaults_WhenOverridesEmpty()
    {
        var tokens = StartupCompatibilityProbe.BuildPresentationProcessTokens(string.Empty);

        tokens.Should().Contain(new[] { "powerpnt", "wpp", "wppt" });
    }

    [Fact]
    public void BuildPresentationProcessTokens_ShouldMergeAndNormalizeOverrideTokens()
    {
        var overridesJson =
            """
            {
              "AdditionalWpsProcessTokens": ["wps_custom.exe", "WPS_CUSTOM"],
              "AdditionalOfficeProcessTokens": ["powerpoint_gov.exe"]
            }
            """;

        var tokens = StartupCompatibilityProbe.BuildPresentationProcessTokens(overridesJson);

        tokens.Should().Contain("wps_custom");
        tokens.Should().Contain("powerpoint_gov");
        tokens.Count(token => string.Equals(token, "wps_custom", StringComparison.OrdinalIgnoreCase))
            .Should()
            .Be(1);
    }

    [Fact]
    public void BuildPresentationProcessTokens_ShouldFallbackToDefaults_WhenOverridesJsonInvalid()
    {
        var tokens = StartupCompatibilityProbe.BuildPresentationProcessTokens("{not-json");

        tokens.Should().BeEquivalentTo(new[] { "powerpnt", "wpp", "wppt" });
    }

    [Fact]
    public void IsPresentationProcessNameMatch_ShouldUseTokenContainsMatching()
    {
        var tokens = StartupCompatibilityProbe.BuildPresentationProcessTokens(
            """
            {
              "AdditionalOfficeProcessTokens": ["powerpoint_gov"]
            }
            """);

        StartupCompatibilityProbe.IsPresentationProcessNameMatch("POWERPNT_16", tokens).Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessNameMatch("powerpoint_gov_edu", tokens).Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessNameMatch("notepad", tokens).Should().BeFalse();
    }

    [Fact]
    public void TryProbeNativeLibraryLoad_ShouldReturnFalse_WhenLibraryCanBeLoaded()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var kernel32Path = Path.Combine(windowsDirectory, "System32", "kernel32.dll");

        var failed = StartupCompatibilityProbe.TryProbeNativeLibraryLoad(kernel32Path, out var error);

        failed.Should().BeFalse();
        error.Should().BeEmpty();
    }

    [Fact]
    public void TryProbeNativeLibraryLoad_ShouldReturnTrue_WhenFileIsNotNativeLibrary()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"ctoolkit-{Guid.NewGuid():N}.txt");
        File.WriteAllText(filePath, "not-a-native-library");
        try
        {
            var failed = StartupCompatibilityProbe.TryProbeNativeLibraryLoad(filePath, out var error);

            failed.Should().BeTrue();
            error.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ResolveNativeDependencyPath_ShouldFindSqliteUnderRidNativeFolder()
    {
        var root = TestPathHelper.CreateDirectory("startup-native");
        var ridFolder = Path.Combine(root, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(ridFolder);
        var sqlitePath = Path.Combine(ridFolder, "e_sqlite3.dll");
        File.WriteAllText(sqlitePath, "placeholder");

        var resolved = StartupCompatibilityProbe.ResolveNativeDependencyPath(
            root,
            "e_sqlite3.dll",
            Architecture.X64);

        resolved.Should().Be(sqlitePath);
    }

    [Theory]
    [InlineData(0x014c, Architecture.X86)]
    [InlineData(0x8664, Architecture.X64)]
    [InlineData(0xAA64, Architecture.Arm64)]
    public void MapMachineToArchitecture_ShouldMapKnownMachineValues(
        ushort machine,
        Architecture expected)
    {
        var result = StartupCompatibilityProbe.MapMachineToArchitecture(machine);

        result.Should().Be(expected);
    }

    [Fact]
    public void TryGetProcessArchitecture_ShouldReturnTrue_ForCurrentProcess()
    {
        var success = StartupCompatibilityProbe.TryGetProcessArchitecture(
            Environment.ProcessId,
            out var architecture,
            out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
        architecture.Should().BeOneOf(Architecture.X64, Architecture.X86, Architecture.Arm64);
    }
}
