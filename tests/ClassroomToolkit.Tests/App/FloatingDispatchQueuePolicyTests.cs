using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchQueuePolicyTests
{
    [Fact]
    public void RequestApply_ShouldQueueAndCaptureForceFlag()
    {
        var decision = FloatingDispatchQueuePolicy.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true);

        decision.Action.Should().Be(FloatingDispatchQueueAction.QueueApply);
        decision.Reason.Should().Be(FloatingDispatchQueueReason.QueuedNewRequest);
        decision.State.ApplyQueued.Should().BeTrue();
        decision.State.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void RequestApply_ShouldMergeForceFlag_WhenAlreadyQueued()
    {
        var state = new FloatingDispatchQueueState(
            ApplyQueued: true,
            ForceEnforceZOrder: false);

        var decision = FloatingDispatchQueuePolicy.RequestApply(
            state,
            forceEnforceZOrder: true);

        decision.Action.Should().Be(FloatingDispatchQueueAction.None);
        decision.Reason.Should().Be(FloatingDispatchQueueReason.MergedIntoQueuedRequest);
        decision.State.ApplyQueued.Should().BeTrue();
        decision.State.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void OnApplyExecuted_ShouldClearQueuedAndForceFlags()
    {
        var next = FloatingDispatchQueuePolicy.OnApplyExecuted(
            new FloatingDispatchQueueState(
                ApplyQueued: true,
                ForceEnforceZOrder: true));

        next.ApplyQueued.Should().BeFalse();
        next.ForceEnforceZOrder.Should().BeFalse();
    }
}
