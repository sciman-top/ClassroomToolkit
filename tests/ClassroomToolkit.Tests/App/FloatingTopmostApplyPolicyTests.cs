using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingTopmostApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnforce_WhenNoLastState()
    {
        var current = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: null,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: null,
            currentPlan: current);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.MissingLastState);
    }

    [Fact]
    public void Resolve_ShouldEnforce_WhenFrontSurfaceChanged()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: ZOrderSurface.Whiteboard,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.FrontSurfaceChanged);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenFrontSurfaceAndPlanUnchanged()
    {
        var plan = new FloatingTopmostPlan(true, true, false, false, false);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.Unchanged);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenOnlyOverlayActivationIntentChanged()
    {
        var lastPlan = new FloatingTopmostPlan(true, true, false, false, false);
        var currentPlan = new FloatingTopmostPlan(true, true, false, false, true);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: lastPlan,
            currentPlan: currentPlan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.Unchanged);
    }

    [Theory]
    [InlineData(ZOrderSurface.PresentationFullscreen)]
    [InlineData(ZOrderSurface.Whiteboard)]
    public void Resolve_ShouldEnforce_WhenLauncherVisibleOnRetouchSurfaceAndPlanUnchanged(ZOrderSurface surface)
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, true);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: surface,
            currentFrontSurface: surface,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.LauncherInteractiveRetouch);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenLauncherVisibleOnPhotoSurfaceAndPlanUnchanged()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, true);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.Unchanged);
    }

    [Fact]
    public void Resolve_ShouldEnforce_WhenForceRequested()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostApplyPolicy.Resolve(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan,
            forceEnforceZOrder: true);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.ForceRequested);
    }

    [Fact]
    public void ShouldEnforceZOrder_ShouldMapResolveDecision()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        FloatingTopmostApplyPolicy.ShouldEnforceZOrder(
            lastFrontSurface: null,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: null,
            currentPlan: plan).Should().BeTrue();
    }
}
