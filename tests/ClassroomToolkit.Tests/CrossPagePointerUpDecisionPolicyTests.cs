using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableTrackFlushScheduleAndImmediate_WhenCrossPageActiveAndInkEnded()
    {
        var decision = CrossPagePointerUpDecisionPolicy.Resolve(
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
        var decision = CrossPagePointerUpDecisionPolicy.Resolve(
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
        var decision = CrossPagePointerUpDecisionPolicy.Resolve(
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
        var decision = CrossPagePointerUpDecisionPolicy.Resolve(
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
