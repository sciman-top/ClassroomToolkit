using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchQueueStateUpdaterTests
{
    [Fact]
    public void ApplyRequest_ShouldQueue_WhenIdle()
    {
        var state = FloatingDispatchQueueState.Default;
        var queueCalls = 0;

        FloatingDispatchQueueStateUpdater.ApplyRequest(
            ref state,
            forceEnforceZOrder: false,
            () =>
            {
                queueCalls++;
                return true;
            });

        state.ApplyQueued.Should().BeTrue();
        state.ForceEnforceZOrder.Should().BeFalse();
        queueCalls.Should().Be(1);
    }

    [Fact]
    public void ApplyRequest_ShouldMergeForce_WhenAlreadyQueued()
    {
        var state = new FloatingDispatchQueueState(
            ApplyQueued: true,
            ForceEnforceZOrder: false);
        var queueCalls = 0;
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;

        FloatingDispatchQueueStateUpdater.ApplyRequest(
            ref state,
            forceEnforceZOrder: true,
            () =>
            {
                queueCalls++;
                return true;
            },
            decision => callbackReason = decision.Reason);

        state.ApplyQueued.Should().BeTrue();
        state.ForceEnforceZOrder.Should().BeTrue();
        queueCalls.Should().Be(0);
        callbackReason.Should().Be(FloatingDispatchQueueReason.MergedIntoQueuedRequest);
    }

    [Fact]
    public void ApplyExecuteQueued_ShouldRunApply_AndResetQueueState()
    {
        var state = new FloatingDispatchQueueState(
            ApplyQueued: true,
            ForceEnforceZOrder: true);
        var appliedForce = false;
        var callCount = 0;

        FloatingDispatchQueueStateUpdater.ApplyExecuteQueued(
            ref state,
            force =>
            {
                callCount++;
                appliedForce = force;
            });

        callCount.Should().Be(1);
        appliedForce.Should().BeTrue();
        state.ApplyQueued.Should().BeFalse();
        state.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ApplyRequest_ShouldRemainIdle_WhenQueueDispatchFails()
    {
        var state = FloatingDispatchQueueState.Default;
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;

        FloatingDispatchQueueStateUpdater.ApplyRequest(
            ref state,
            forceEnforceZOrder: true,
            () => false,
            decision => callbackReason = decision.Reason);

        state.Should().Be(FloatingDispatchQueueState.Default);
        callbackReason.Should().Be(FloatingDispatchQueueReason.QueueDispatchFailed);
    }

    [Fact]
    public void ApplyRequest_ShouldRemainIdle_WhenQueueDispatchThrows()
    {
        var state = FloatingDispatchQueueState.Default;
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;
        Exception? dispatchFailure = null;

        FloatingDispatchQueueStateUpdater.ApplyRequest(
            ref state,
            forceEnforceZOrder: true,
            () => throw new InvalidOperationException("boom"),
            decision => callbackReason = decision.Reason,
            ex => dispatchFailure = ex);

        state.Should().Be(FloatingDispatchQueueState.Default);
        callbackReason.Should().Be(FloatingDispatchQueueReason.QueueDispatchFailed);
        dispatchFailure.Should().BeOfType<InvalidOperationException>();
        dispatchFailure!.Message.Should().Be("boom");
    }

    [Fact]
    public void ApplyExecuteQueued_ShouldResetState_WhenApplyThrows()
    {
        var state = new FloatingDispatchQueueState(
            ApplyQueued: true,
            ForceEnforceZOrder: true);
        var callbackCalled = false;

        FloatingDispatchQueueStateUpdater.ApplyExecuteQueued(
            ref state,
            _ => throw new InvalidOperationException("boom"),
            _ => callbackCalled = true);

        callbackCalled.Should().BeTrue();
        state.Should().Be(FloatingDispatchQueueState.Default);
    }
}
