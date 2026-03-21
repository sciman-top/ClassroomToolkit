using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityProbeProcessFilterTests
{
    [Theory]
    [InlineData("POWERPNT", true)]
    [InlineData("powerpnt_16", true)]
    [InlineData("powerpoint_gov", true)]
    [InlineData("PowerPoint_edu_2024", true)]
    [InlineData("pptview_16", true)]
    [InlineData("wpspresentation_2025", true)]
    [InlineData("powerpnt.exe", true)]
    [InlineData("pptview", true)]
    [InlineData("WPP", true)]
    [InlineData("wppgov", true)]
    [InlineData("WPPT", true)]
    [InlineData("wpspresentationhost", true)]
    [InlineData("notepad", false)]
    [InlineData("winword", false)]
    [InlineData("", false)]
    public void IsPresentationProcessName_ShouldMatchDefaultHeuristics(string processName, bool expected)
    {
        StartupCompatibilityProbe.IsPresentationProcessName(processName).Should().Be(expected);
    }

    [Fact]
    public void IsPresentationProcessName_ShouldUseClassifierOverridesProcessTokens()
    {
        var overridesJson =
            """
            {
              "AdditionalWpsProcessTokens": ["wpp_edu"],
              "AdditionalOfficeProcessTokens": ["powerpoint_gov"]
            }
            """;

        StartupCompatibilityProbe.IsPresentationProcessName("powerpoint_gov", overridesJson).Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessName("campus_wpp_edu_service", overridesJson).Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessName("notepad", overridesJson).Should().BeFalse();
    }

    [Fact]
    public void IsPresentationProcessName_ShouldFallbackToDefaults_WhenOverridesJsonInvalid()
    {
        var act = () => StartupCompatibilityProbe.IsPresentationProcessName("powerpoint_gov", "{not-json");

        act.Should().NotThrow();
        StartupCompatibilityProbe.IsPresentationProcessName("powerpoint_gov", "{not-json").Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessName("powerpnt_16", "{not-json").Should().BeTrue();
        StartupCompatibilityProbe.IsPresentationProcessName("notepad", "{not-json").Should().BeFalse();
    }

    [Fact]
    public void TryDescribePresentationProcessMatch_ShouldReturnEvidence_ForDefaultAndOverrideMatches()
    {
        var overridesJson =
            """
            {
              "AdditionalOfficeProcessTokens": ["pptgov_custom"]
            }
            """;

        StartupCompatibilityProbe.TryDescribePresentationProcessMatch("POWERPNT", null, out var defaultEvidence)
            .Should()
            .BeTrue();
        defaultEvidence.Should().Contain("default");

        StartupCompatibilityProbe.TryDescribePresentationProcessMatch("pptgov_custom_launcher", overridesJson, out var overrideEvidence)
            .Should()
            .BeTrue();
        overrideEvidence.Should().Contain("override-token");

        StartupCompatibilityProbe.TryDescribePresentationProcessMatch("notepad", overridesJson, out var noEvidence)
            .Should()
            .BeFalse();
        noEvidence.Should().BeEmpty();
    }
}
