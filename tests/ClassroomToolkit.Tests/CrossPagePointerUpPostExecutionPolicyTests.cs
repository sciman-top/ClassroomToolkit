using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

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

        var postPlan = CrossPagePointerUpPostExecutionPolicy.Resolve(
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

        var postPlan = CrossPagePointerUpPostExecutionPolicy.Resolve(
            executionPlan,
            crossPageFirstInputTraceActive: false);

        postPlan.ShouldEndFirstInputTrace.Should().BeFalse();
        postPlan.ShouldRequestInkContextRefresh.Should().BeTrue();
    }
}
