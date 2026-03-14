using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookEnableGatePolicyTests
{
    [Fact]
    public void ShouldAttemptResolveTarget_ShouldReturnFalse_WhenRuntimeGateNotPassed()
    {
        WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(
                allowWps: false,
                boardActive: false,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeFalse();

        WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(
                allowWps: true,
                boardActive: true,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldAttemptResolveTarget_ShouldReturnTrue_WhenRuntimeGatePassed()
    {
        WpsHookEnableGatePolicy.ShouldAttemptResolveTarget(
                allowWps: true,
                boardActive: false,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldEnableWithTarget_ShouldMatchExpected(
        bool shouldAttemptResolveTarget,
        bool targetValid,
        bool targetIsSlideshow,
        bool expected)
    {
        WpsHookEnableGatePolicy.ShouldEnableWithTarget(
                shouldAttemptResolveTarget,
                targetValid,
                targetIsSlideshow)
            .Should()
            .Be(expected);
    }
}
