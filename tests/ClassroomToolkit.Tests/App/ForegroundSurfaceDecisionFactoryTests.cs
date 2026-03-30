using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundSurfaceDecisionFactoryTests
{
    [Fact]
    public void NoTouch_ShouldDisableTouch_AndRespectRequestFlag()
    {
        var decision = ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Touch_ShouldEnableTouch_WithRequestedSurface()
    {
        var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.Whiteboard);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ExplicitForeground_ShouldForce_WhenOverlayExists()
    {
        var decision = ForegroundSurfaceDecisionFactory.ExplicitForeground(
            overlayExists: true,
            surface: ZOrderSurface.PresentationFullscreen);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }
}
