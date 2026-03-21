using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationProcessSignaturePolicyTests
{
    [Theory]
    [InlineData("POWERPNT.EXE", true)]
    [InlineData("powerpoint_gov", true)]
    [InlineData("pptview_service", true)]
    [InlineData("notepad", false)]
    public void IsOfficeProcessName_ShouldMatchExpected(string processName, bool expected)
    {
        PresentationProcessSignaturePolicy.IsOfficeProcessName(processName).Should().Be(expected);
    }

    [Theory]
    [InlineData("WPP", true)]
    [InlineData("wppt_classroom", true)]
    [InlineData("campus_kwpp_launcher", true)]
    [InlineData("campus_kwps_launcher", true)]
    [InlineData("wpspresentation", true)]
    [InlineData("notepad", false)]
    public void IsWpsProcessName_ShouldMatchExpected(string processName, bool expected)
    {
        PresentationProcessSignaturePolicy.IsWpsProcessName(processName).Should().Be(expected);
    }

    [Fact]
    public void MatchesAnyProcessToken_ShouldNormalizeExeAndMatchContains()
    {
        var tokens = new[] { "pptgov", "edu_wpp" };
        PresentationProcessSignaturePolicy.MatchesAnyProcessToken("PPTGOV_CUSTOM.EXE", tokens).Should().BeTrue();
        PresentationProcessSignaturePolicy.MatchesAnyProcessToken("notepad.exe", tokens).Should().BeFalse();
    }
}
