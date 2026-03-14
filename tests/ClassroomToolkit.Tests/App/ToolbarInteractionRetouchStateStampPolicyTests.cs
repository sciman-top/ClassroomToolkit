using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchStateStampPolicyTests
{
    [Fact]
    public void ShouldMarkRetouched_ShouldReturnFalse_ForPreviewForceZOrderReplay()
    {
        var shouldMark = ToolbarInteractionRetouchStateStampPolicy.ShouldMarkRetouched(
            ToolbarInteractionRetouchTrigger.PreviewMouseDown,
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true));

        shouldMark.Should().BeFalse();
    }

    [Fact]
    public void ShouldMarkRetouched_ShouldReturnTrue_ForActivatedForceZOrderReplay()
    {
        var shouldMark = ToolbarInteractionRetouchStateStampPolicy.ShouldMarkRetouched(
            ToolbarInteractionRetouchTrigger.Activated,
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true));

        shouldMark.Should().BeTrue();
    }

    [Fact]
    public void ShouldMarkRetouched_ShouldReturnTrue_ForDirectRepair()
    {
        var shouldMark = ToolbarInteractionRetouchStateStampPolicy.ShouldMarkRetouched(
            ToolbarInteractionRetouchTrigger.Activated,
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        shouldMark.Should().BeTrue();
    }

    [Fact]
    public void ShouldMarkRetouched_ShouldReturnFalse_ForNoOpPlan()
    {
        var shouldMark = ToolbarInteractionRetouchStateStampPolicy.ShouldMarkRetouched(
            ToolbarInteractionRetouchTrigger.Activated,
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        shouldMark.Should().BeFalse();
    }
}
