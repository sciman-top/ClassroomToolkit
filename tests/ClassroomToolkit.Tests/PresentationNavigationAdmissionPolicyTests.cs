using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationNavigationAdmissionPolicyTests
{
    [Fact]
    public void ShouldAttempt_ShouldReturnFalse_WhenChannelNotAllowed()
    {
        var allowed = PresentationNavigationAdmissionPolicy.ShouldAttempt(
            allowChannel: false,
            boardActive: false,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttempt_ShouldReturnFalse_WhenBoardActive()
    {
        var allowed = PresentationNavigationAdmissionPolicy.ShouldAttempt(
            allowChannel: true,
            boardActive: true,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ShouldAttempt_ShouldRequireValidTargetAndForegroundWhenNeeded(
        bool targetIsValid,
        bool targetHasInfo,
        bool targetIsSlideshow,
        bool targetForeground,
        bool expected)
    {
        var allowed = PresentationNavigationAdmissionPolicy.ShouldAttempt(
            allowChannel: true,
            boardActive: false,
            targetIsValid: targetIsValid,
            targetHasInfo: targetHasInfo,
            targetIsSlideshow: targetIsSlideshow,
            allowBackground: false,
            targetForeground: targetForeground);

        allowed.Should().Be(expected);
    }

    [Fact]
    public void ShouldAttempt_ShouldIgnoreForeground_WhenBackgroundAllowed()
    {
        var allowed = PresentationNavigationAdmissionPolicy.ShouldAttempt(
            allowChannel: true,
            boardActive: false,
            targetIsValid: true,
            targetHasInfo: true,
            targetIsSlideshow: true,
            allowBackground: true,
            targetForeground: false);

        allowed.Should().BeTrue();
    }
}
