using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoCloseTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldDetachOwnersAndRequestForcedZOrder_WhenOverlayVisible()
    {
        var plan = PhotoCloseTransitionPolicy.Resolve(overlayVisible: true);

        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldOnlyDetachOwners_WhenOverlayNotVisible()
    {
        var plan = PhotoCloseTransitionPolicy.Resolve(overlayVisible: false);

        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldMatchOverlayVisibleBehavior()
    {
        var context = new PhotoCloseTransitionContext(OverlayVisible: true);

        var plan = PhotoCloseTransitionPolicy.Resolve(context);

        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }
}
