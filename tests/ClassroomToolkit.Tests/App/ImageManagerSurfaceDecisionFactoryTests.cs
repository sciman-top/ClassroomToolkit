using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerSurfaceDecisionFactoryTests
{
    [Fact]
    public void TouchImageManager_ShouldTouchAndRequestApply()
    {
        var decision = ImageManagerSurfaceDecisionFactory.TouchImageManager(forceEnforceZOrder: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.ImageManager);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void NoTouch_ShouldRespectApplyAndForceFlags()
    {
        var decision = ImageManagerSurfaceDecisionFactory.NoTouch(
            requestZOrderApply: false,
            forceEnforceZOrder: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }
}
