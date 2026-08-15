using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchExecutionPlanPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, true, true)]
    public void Resolve_ShouldMapDecisionToOneExecutionPath(
        bool shouldRetouch,
        bool forceEnforce,
        bool applyDirectRepair,
        bool requestZOrderApply,
        bool forceZOrderApply)
    {
        var plan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(
            new ToolbarInteractionRetouchDecision(
                shouldRetouch,
                forceEnforce,
                ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().Be(applyDirectRepair);
        plan.RequestZOrderApply.Should().Be(requestZOrderApply);
        plan.ForceEnforceZOrder.Should().Be(forceZOrderApply);
    }
}
