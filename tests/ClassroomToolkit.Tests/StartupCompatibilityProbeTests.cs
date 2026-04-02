using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

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
}
