using ClassroomToolkit.Services.Compatibility;
using AwesomeAssertions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.Tests.Services;

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
    public void Collect_ShouldNotRequireArchivedPdfiumNativeDependency()
    {
        var report = StartupCompatibilityProbe.Collect(settingsPath: string.Empty);

        report.Issues.Should().NotContain(issue =>
            issue.Code.Contains("pdfium", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void ProcessIdentityHelpers_ShouldReadCurrentProcessSafely()
    {
        using var process = Process.GetCurrentProcess();

        StartupCompatibilityProbe.TryGetProcessId(process, out var processId).Should().BeTrue();
        StartupCompatibilityProbe.TryGetProcessName(process, out var processName).Should().BeTrue();

        processId.Should().Be(Environment.ProcessId);
        processName.Should().NotBeNullOrWhiteSpace();
    }
}
