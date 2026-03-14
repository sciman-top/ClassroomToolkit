using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchExecutionPlanPolicyTests
{
    private static readonly ToolbarInteractionRetouchSnapshot DefaultSnapshot = new(
        OverlayVisible: true,
        PhotoModeActive: true,
        WhiteboardActive: false,
        ToolbarVisible: true,
        ToolbarTopmost: true,
        RollCallVisible: false,
        RollCallTopmost: false,
        LauncherVisible: true,
        LauncherTopmost: true);

    [Fact]
    public void Resolve_ShouldSkip_WhenDecisionDisablesRetouch()
    {
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            DefaultSnapshot,
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: false,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldUseDirectRepair_WhenRetouchWithoutForce()
    {
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            DefaultSnapshot with { ToolbarTopmost = false },
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestZOrder_WhenForceIsRequired()
    {
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            DefaultSnapshot,
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: true,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldUseDirectRepair_WhenLauncherOnlyDrift()
    {
        var snapshot = DefaultSnapshot with
        {
            LauncherTopmost = false
        };
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            snapshot,
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldUseDirectRepair_WhenLauncherAndToolbarBothDrift()
    {
        var snapshot = DefaultSnapshot with
        {
            LauncherTopmost = false,
            ToolbarTopmost = false
        };
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            snapshot,
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }
}
