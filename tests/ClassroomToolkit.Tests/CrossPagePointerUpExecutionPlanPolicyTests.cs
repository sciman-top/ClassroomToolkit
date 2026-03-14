using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpExecutionPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldProjectDecisionAndSource_WhenInkOperationExists()
    {
        var decision = new CrossPagePointerUpDecision(
            ShouldTrackPointerUp: true,
            ShouldSchedulePostInputRefresh: true,
            ShouldFlushReplay: true,
            ShouldRequestImmediateRefresh: true);

        var plan = CrossPagePointerUpExecutionPlanPolicy.Resolve(
            decision,
            hadInkOperation: true,
            pendingInkContextCheck: true);

        plan.ShouldTrackPointerUp.Should().BeTrue();
        plan.ShouldApplyFastRefresh.Should().BeTrue();
        plan.ShouldScheduleDeferredRefresh.Should().BeTrue();
        plan.DeferredRefreshSource.Should().Be(CrossPagePointerUpRefreshSourcePolicy.PointerUpInk);
        plan.ShouldFlushReplay.Should().BeTrue();
        plan.ShouldRequestInkContextRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepRefreshSource_WhenNoInkOperation()
    {
        var decision = new CrossPagePointerUpDecision(
            ShouldTrackPointerUp: false,
            ShouldSchedulePostInputRefresh: false,
            ShouldFlushReplay: false,
            ShouldRequestImmediateRefresh: false);

        var plan = CrossPagePointerUpExecutionPlanPolicy.Resolve(
            decision,
            hadInkOperation: false,
            pendingInkContextCheck: false);

        plan.DeferredRefreshSource.Should().Be(CrossPagePointerUpRefreshSourcePolicy.PointerUp);
        plan.ShouldTrackPointerUp.Should().BeFalse();
        plan.ShouldApplyFastRefresh.Should().BeFalse();
        plan.ShouldScheduleDeferredRefresh.Should().BeFalse();
        plan.ShouldFlushReplay.Should().BeFalse();
        plan.ShouldRequestInkContextRefresh.Should().BeFalse();
    }
}
