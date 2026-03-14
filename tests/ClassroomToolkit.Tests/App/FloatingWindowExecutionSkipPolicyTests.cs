using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingWindowExecutionSkipPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoExecutionIntent_WhenPlanIsNoOp()
    {
        var plan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: true,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                EnforceZOrder: false),
            ActivationPlan: new FloatingWindowActivationPlan(
                ActivateOverlay: false,
                ActivateImageManager: false),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                ToolbarAction: FloatingOwnerBindingAction.None,
                RollCallAction: FloatingOwnerBindingAction.None,
                ImageManagerAction: FloatingOwnerBindingAction.None));

        var decision = FloatingWindowExecutionSkipPolicy.Resolve(plan);
        decision.ShouldExecute.Should().BeFalse();
        decision.Reason.Should().Be(FloatingWindowExecutionSkipReason.NoExecutionIntent);
    }

    [Fact]
    public void Resolve_ShouldReturnEnforceZOrder_WhenEnforceZOrderRequested()
    {
        var plan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: true,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                EnforceZOrder: true),
            ActivationPlan: new FloatingWindowActivationPlan(false, false),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None));

        var decision = FloatingWindowExecutionSkipPolicy.Resolve(plan);
        decision.ShouldExecute.Should().BeTrue();
        decision.Reason.Should().Be(FloatingWindowExecutionSkipReason.EnforceZOrder);
    }

    [Fact]
    public void Resolve_ShouldReturnOwnerOrActivationReason_WhenOwnerOrActivationRequired()
    {
        var ownerPlan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: true,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                EnforceZOrder: false),
            ActivationPlan: new FloatingWindowActivationPlan(false, false),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                FloatingOwnerBindingAction.AttachOverlay,
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None));
        var activationPlan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: true,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                EnforceZOrder: false),
            ActivationPlan: new FloatingWindowActivationPlan(true, false),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None));

        var ownerDecision = FloatingWindowExecutionSkipPolicy.Resolve(ownerPlan);
        ownerDecision.ShouldExecute.Should().BeTrue();
        ownerDecision.Reason.Should().Be(FloatingWindowExecutionSkipReason.OwnerBindingIntent);

        var activationDecision = FloatingWindowExecutionSkipPolicy.Resolve(activationPlan);
        activationDecision.ShouldExecute.Should().BeTrue();
        activationDecision.Reason.Should().Be(FloatingWindowExecutionSkipReason.ActivationIntent);
    }

    [Fact]
    public void ShouldExecute_ShouldMapResolveDecision()
    {
        var plan = new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: true,
                RollCallTopmost: true,
                LauncherTopmost: true,
                ImageManagerTopmost: false,
                EnforceZOrder: true),
            ActivationPlan: new FloatingWindowActivationPlan(false, false),
            OwnerPlan: new FloatingOwnerExecutionPlan(
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None,
                FloatingOwnerBindingAction.None));

        FloatingWindowExecutionSkipPolicy.ShouldExecute(plan).Should().BeTrue();
    }
}
