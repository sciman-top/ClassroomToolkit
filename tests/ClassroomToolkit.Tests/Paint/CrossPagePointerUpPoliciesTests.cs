using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class CrossPagePointerUpDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableTrackFlushScheduleAndImmediate_WhenCrossPageActiveAndInkEnded()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDecision(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: false);

        decision.ShouldTrackPointerUp.Should().BeTrue();
        decision.ShouldFlushReplay.Should().BeTrue();
        decision.ShouldSchedulePostInputRefresh.Should().BeTrue();
        decision.ShouldRequestImmediateRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDisableImmediate_WhenUpdateAlreadyPending()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDecision(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: true);

        decision.ShouldTrackPointerUp.Should().BeTrue();
        decision.ShouldFlushReplay.Should().BeTrue();
        decision.ShouldSchedulePostInputRefresh.Should().BeTrue();
        decision.ShouldRequestImmediateRefresh.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableScheduleAndImmediate_WhenCrossPageActiveAndDeferredRequested()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDecision(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: true,
            updatePending: false);

        decision.ShouldTrackPointerUp.Should().BeTrue();
        decision.ShouldFlushReplay.Should().BeTrue();
        decision.ShouldSchedulePostInputRefresh.Should().BeTrue();
        decision.ShouldRequestImmediateRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDisableAll_WhenCrossPageInactive()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDecision(
            crossPageDisplayActive: false,
            hadInkOperation: true,
            deferredRefreshRequested: true,
            updatePending: false);

        decision.ShouldTrackPointerUp.Should().BeFalse();
        decision.ShouldFlushReplay.Should().BeFalse();
        decision.ShouldSchedulePostInputRefresh.Should().BeFalse();
        decision.ShouldRequestImmediateRefresh.Should().BeFalse();
    }
}

public sealed class CrossPagePointerUpDeferredRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldNotConsume_WhenDeferredFlagIsOff()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDeferredRefresh(
            deferredByInkInput: false,
            crossPageDisplayActive: true);

        decision.ShouldConsumeDeferredFlag.Should().BeFalse();
        decision.ShouldRequestPostRefresh.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldConsumeAndRequest_WhenDeferredFlagOnAndSceneEligible()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDeferredRefresh(
            deferredByInkInput: true,
            crossPageDisplayActive: true);

        decision.ShouldConsumeDeferredFlag.Should().BeTrue();
        decision.ShouldRequestPostRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldConsumeWithoutRequest_WhenDeferredFlagOnButSceneIneligible()
    {
        var decision = CrossPagePointerUpPolicies.ResolveDeferredRefresh(
            deferredByInkInput: true,
            crossPageDisplayActive: false);

        decision.ShouldConsumeDeferredFlag.Should().BeTrue();
        decision.ShouldRequestPostRefresh.Should().BeFalse();
    }
}

public sealed class CrossPagePointerUpDeferredStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepFlags_WhenDeferredIsOff()
    {
        var result = CrossPagePointerUpPolicies.ResolveDeferredState(
            deferredByInkInput: false,
            crossPageDisplayActive: true);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeFalse();
        result.ShouldLogStableRecover.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldConsumeAndRequestRefresh_WhenDeferredOnAndCrossPageActive()
    {
        var result = CrossPagePointerUpPolicies.ResolveDeferredState(
            deferredByInkInput: true,
            crossPageDisplayActive: true);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeTrue();
        result.ShouldLogStableRecover.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldConsumeWithoutRefresh_WhenDeferredOnButCrossPageInactive()
    {
        var result = CrossPagePointerUpPolicies.ResolveDeferredState(
            deferredByInkInput: true,
            crossPageDisplayActive: false);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeTrue();
        result.ShouldLogStableRecover.Should().BeFalse();
    }
}

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

        var plan = CrossPagePointerUpPolicies.ResolveExecutionPlan(
            decision,
            hadInkOperation: true,
            pendingInkContextCheck: true);

        plan.ShouldTrackPointerUp.Should().BeTrue();
        plan.ShouldApplyFastRefresh.Should().BeTrue();
        plan.ShouldScheduleDeferredRefresh.Should().BeTrue();
        plan.DeferredRefreshSource.Should().Be(CrossPagePointerUpPolicies.PointerUpInk);
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

        var plan = CrossPagePointerUpPolicies.ResolveExecutionPlan(
            decision,
            hadInkOperation: false,
            pendingInkContextCheck: false);

        plan.DeferredRefreshSource.Should().Be(CrossPagePointerUpPolicies.PointerUp);
        plan.ShouldTrackPointerUp.Should().BeFalse();
        plan.ShouldApplyFastRefresh.Should().BeFalse();
        plan.ShouldScheduleDeferredRefresh.Should().BeFalse();
        plan.ShouldFlushReplay.Should().BeFalse();
        plan.ShouldRequestInkContextRefresh.Should().BeFalse();
    }
}

public sealed class CrossPagePointerUpImmediateRefreshPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenCrossPageInactive()
    {
        var shouldRequest = CrossPagePointerUpPolicies.ShouldRequest(
            crossPageDisplayActive: false,
            hadInkOperation: true,
            deferredRefreshRequested: true,
            updatePending: false);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnFalse_WhenUpdatePending()
    {
        var shouldRequest = CrossPagePointerUpPolicies.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: true);

        shouldRequest.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenCrossPageActiveAndInkOrDeferred()
    {
        var byInk = CrossPagePointerUpPolicies.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false,
            updatePending: false);
        var byDeferred = CrossPagePointerUpPolicies.ShouldRequest(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: true,
            updatePending: false);

        byInk.Should().BeTrue();
        byDeferred.Should().BeTrue();
    }
}

public sealed class CrossPagePointerUpPostExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldPreserveExecutionPlanAndSetTraceFlag()
    {
        var executionPlan = new CrossPagePointerUpExecutionPlan(
            ShouldTrackPointerUp: true,
            ShouldApplyFastRefresh: true,
            ShouldScheduleDeferredRefresh: true,
            DeferredRefreshSource: "pointer-up",
            ShouldFlushReplay: true,
            ShouldRequestInkContextRefresh: false);

        var postPlan = CrossPagePointerUpPolicies.ResolvePostExecution(
            executionPlan,
            crossPageFirstInputTraceActive: true);

        postPlan.ShouldTrackPointerUp.Should().BeTrue();
        postPlan.ShouldApplyFastRefresh.Should().BeTrue();
        postPlan.ShouldScheduleDeferredRefresh.Should().BeTrue();
        postPlan.DeferredRefreshSource.Should().Be("pointer-up");
        postPlan.ShouldFlushReplay.Should().BeTrue();
        postPlan.ShouldEndFirstInputTrace.Should().BeTrue();
        postPlan.ShouldRequestInkContextRefresh.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableTraceEnd_WhenTraceNotActive()
    {
        var executionPlan = new CrossPagePointerUpExecutionPlan(
            ShouldTrackPointerUp: false,
            ShouldApplyFastRefresh: false,
            ShouldScheduleDeferredRefresh: false,
            DeferredRefreshSource: "none",
            ShouldFlushReplay: false,
            ShouldRequestInkContextRefresh: true);

        var postPlan = CrossPagePointerUpPolicies.ResolvePostExecution(
            executionPlan,
            crossPageFirstInputTraceActive: false);

        postPlan.ShouldEndFirstInputTrace.Should().BeFalse();
        postPlan.ShouldRequestInkContextRefresh.Should().BeTrue();
    }
}

public sealed class CrossPagePointerUpRefreshPolicyTests
{
    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnFalse_WhenCrossPageDisplayInactive()
    {
        CrossPagePointerUpPolicies.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: false,
            hadInkOperation: true,
            deferredRefreshRequested: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnTrue_WhenInkOperationEndedInCrossPage()
    {
        CrossPagePointerUpPolicies.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: true,
            deferredRefreshRequested: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnTrue_WhenDeferredRefreshRequestedInCrossPage()
    {
        CrossPagePointerUpPolicies.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldSchedulePostInputRefresh_ShouldReturnFalse_WhenNoInkAndNoDeferred()
    {
        CrossPagePointerUpPolicies.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive: true,
            hadInkOperation: false,
            deferredRefreshRequested: false).Should().BeFalse();
    }
}

public sealed class CrossPagePointerUpRefreshSourcePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnInkSource_WhenInkOperationEnded()
    {
        CrossPagePointerUpPolicies.ResolveRefreshSource(hadInkOperation: true)
            .Should().Be(CrossPagePointerUpPolicies.PointerUpInk);
    }

    [Fact]
    public void Resolve_ShouldReturnDefaultSource_WhenNoInkOperation()
    {
        CrossPagePointerUpPolicies.ResolveRefreshSource(hadInkOperation: false)
            .Should().Be(CrossPagePointerUpPolicies.PointerUp);
    }
}

public sealed class CrossPagePointerUpStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableBothFlags_WhenPhotoActiveBoardOffAndCrossPageEnabled()
    {
        var state = CrossPagePointerUpPolicies.ResolveState(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);

        state.PhotoTransformActive.Should().BeTrue();
        state.CrossPageDisplayActive.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDisableCrossPage_WhenBoardActive()
    {
        var state = CrossPagePointerUpPolicies.ResolveState(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        state.PhotoTransformActive.Should().BeFalse();
        state.CrossPageDisplayActive.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableCrossPage_WhenSwitchOff()
    {
        var state = CrossPagePointerUpPolicies.ResolveState(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false);

        state.PhotoTransformActive.Should().BeTrue();
        state.CrossPageDisplayActive.Should().BeFalse();
    }
}
