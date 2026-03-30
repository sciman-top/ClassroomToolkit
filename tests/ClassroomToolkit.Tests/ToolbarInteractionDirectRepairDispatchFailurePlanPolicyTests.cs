using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarInteractionDirectRepairDispatchFailurePlanPolicyTests
{
    [Fact]
    public void ResolveAdmissionRejected_ShouldRequestRerun()
    {
        var plan = ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveAdmissionRejected();

        plan.ShouldRequestRerun.Should().BeTrue();
        plan.ShouldClearQueuedState.Should().BeFalse();
        plan.ShouldClearRerunState.Should().BeFalse();
    }

    [Fact]
    public void ResolveMarkQueuedFailed_ShouldRequestRerun()
    {
        var plan = ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveMarkQueuedFailed();

        plan.ShouldRequestRerun.Should().BeTrue();
        plan.ShouldClearQueuedState.Should().BeFalse();
        plan.ShouldClearRerunState.Should().BeFalse();
    }

    [Fact]
    public void ResolveScheduleFailed_ShouldClearDispatchStates()
    {
        var plan = ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveScheduleFailed();

        plan.ShouldRequestRerun.Should().BeFalse();
        plan.ShouldClearQueuedState.Should().BeTrue();
        plan.ShouldClearRerunState.Should().BeTrue();
    }
}
