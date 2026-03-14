using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayWheelPresentationExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenWpsHookBlocksDirectSend()
    {
        var action = OverlayWheelPresentationExecutionPolicy.Resolve(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: true,
            hookRecentlyFired: false,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.None);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenHookRecentlyFired()
    {
        var action = OverlayWheelPresentationExecutionPolicy.Resolve(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: true,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.None);
    }

    [Fact]
    public void Resolve_ShouldReturnSendNext_WhenDeltaNegative()
    {
        var action = OverlayWheelPresentationExecutionPolicy.Resolve(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: false,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.SendNext);
    }

    [Fact]
    public void Resolve_ShouldReturnSendPrevious_WhenDeltaNonNegative()
    {
        var action = OverlayWheelPresentationExecutionPolicy.Resolve(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: false,
            wheelDelta: 120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.SendPrevious);
    }
}
