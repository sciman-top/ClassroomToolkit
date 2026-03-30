using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Interop;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallTransparencyPolicyTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ResolveTransparency_ShouldFollowHoverAndPaintMode(
        bool hovering,
        bool paintAllowsTransparency,
        bool expected)
    {
        var decision = RollCallTransparencyPolicy.ResolveTransparency(hovering, paintAllowsTransparency);

        decision.TransparentEnabled.Should().Be(expected);
    }

    [Fact]
    public void ResolveStyleApply_ShouldReturnFalse_WhenStateUnchanged()
    {
        var decision = RollCallTransparencyPolicy.ResolveStyleApply(
            transparentEnabled: true,
            lastTransparentEnabled: true);

        decision.ShouldApplyStyle.Should().BeFalse();
        decision.Reason.Should().Be(RollCallTransparencyStyleApplyReason.StateUnchanged);
    }

    [Fact]
    public void ResolveStyleApply_ShouldReturnTrue_WhenStateChangedOrUnknown()
    {
        var changed = RollCallTransparencyPolicy.ResolveStyleApply(
            transparentEnabled: false,
            lastTransparentEnabled: true);
        var unknown = RollCallTransparencyPolicy.ResolveStyleApply(
            transparentEnabled: false,
            lastTransparentEnabled: null);

        changed.ShouldApplyStyle.Should().BeTrue();
        changed.Reason.Should().Be(RollCallTransparencyStyleApplyReason.StateChanged);
        unknown.ShouldApplyStyle.Should().BeTrue();
        unknown.Reason.Should().Be(RollCallTransparencyStyleApplyReason.StateUnknown);
    }

    [Fact]
    public void ResolveStyleMasks_ShouldSetTransparentMask_WhenEnabled()
    {
        var (setMask, clearMask) = RollCallTransparencyPolicy.ResolveStyleMasks(transparentEnabled: true);

        setMask.Should().Be(NativeMethods.WsExTransparent);
        clearMask.Should().Be(0);
    }

    [Fact]
    public void ResolveStyleMasks_ShouldClearTransparentMask_WhenDisabled()
    {
        var (setMask, clearMask) = RollCallTransparencyPolicy.ResolveStyleMasks(transparentEnabled: false);

        setMask.Should().Be(0);
        clearMask.Should().Be(NativeMethods.WsExTransparent);
    }

    [Fact]
    public void ResolveHoverTimer_ShouldReturnExpectedDecisions()
    {
        var startDecision = RollCallTransparencyPolicy.ResolveHoverTimer(
                transparentEnabled: true,
                hoverTimerEnabled: false);
        startDecision.ShouldStart.Should().BeTrue();

        var keepRunningDecision = RollCallTransparencyPolicy.ResolveHoverTimer(
                transparentEnabled: true,
                hoverTimerEnabled: true);
        keepRunningDecision.ShouldStart.Should().BeFalse();

        var stopDecision = RollCallTransparencyPolicy.ResolveHoverTimer(
                transparentEnabled: false,
                hoverTimerEnabled: true);
        stopDecision.ShouldStop.Should().BeTrue();

        var keepStoppedDecision = RollCallTransparencyPolicy.ResolveHoverTimer(
                transparentEnabled: true,
                hoverTimerEnabled: true);
        keepStoppedDecision.ShouldStop.Should().BeFalse();
    }

    [Fact]
    public void BoolMethods_ShouldMapResolveDecisions()
    {
        RollCallTransparencyPolicy.ShouldEnableTransparent(false, true).Should().BeTrue();
        RollCallTransparencyPolicy.ShouldApplyStyle(false, true).Should().BeTrue();
        RollCallTransparencyPolicy.ShouldStartHoverTimer(true, false).Should().BeTrue();
        RollCallTransparencyPolicy.ShouldStopHoverTimer(false, true).Should().BeTrue();
    }
}
