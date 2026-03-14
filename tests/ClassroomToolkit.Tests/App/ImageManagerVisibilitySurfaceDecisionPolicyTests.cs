using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerVisibilitySurfaceDecisionPolicyTests
{
    [Fact]
    public void ResolveOpen_ShouldTouchImageManager_WhenPlanRequestsTouch()
    {
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: true,
            ShowWindow: true,
            NormalizeWindowState: true,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true,
            TouchImageManagerSurface: true);

        var decision = ImageManagerVisibilitySurfaceDecisionPolicy.ResolveOpen(plan);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.ImageManager);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolveOpen_ShouldProduceNoTouchDecision_WhenPlanSkipsTouch()
    {
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: false,
            ShowWindow: false,
            NormalizeWindowState: false,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false,
            TouchImageManagerSurface: false);

        var decision = ImageManagerVisibilitySurfaceDecisionPolicy.ResolveOpen(plan);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
