using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingDispatchQueueExecutorTests
{
    [Fact]
    public void RequestApply_ShouldQueue_WhenStateIsIdle()
    {
        var scheduled = false;

        var state = FloatingDispatchQueueExecutor.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true,
            queueApply: () =>
            {
                scheduled = true;
                return true;
            });

        scheduled.Should().BeTrue();
        state.ApplyQueued.Should().BeTrue();
        state.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void RequestApply_ShouldMergeForceWithoutSchedulingTwice()
    {
        var scheduleCount = 0;
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;

        var queuedState = FloatingDispatchQueueExecutor.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: false,
            queueApply: () =>
            {
                scheduleCount++;
                return true;
            });
        var mergedState = FloatingDispatchQueueExecutor.RequestApply(
            queuedState,
            forceEnforceZOrder: true,
            queueApply: () =>
            {
                scheduleCount++;
                return true;
            },
            onDecision: decision => callbackReason = decision.Reason);

        scheduleCount.Should().Be(1);
        callbackReason.Should().Be(FloatingDispatchQueueReason.MergedIntoQueuedRequest);
        mergedState.ApplyQueued.Should().BeTrue();
        mergedState.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ExecuteQueuedApply_ShouldInvokeApplyAndResetState()
    {
        var invoked = false;
        var force = false;

        var nextState = FloatingDispatchQueueExecutor.ExecuteQueuedApply(
            new FloatingDispatchQueueState(
                ApplyQueued: true,
                ForceEnforceZOrder: true),
            apply: shouldForce =>
            {
                invoked = true;
                force = shouldForce;
            });

        invoked.Should().BeTrue();
        force.Should().BeTrue();
        nextState.Should().Be(FloatingDispatchQueueState.Default);
    }

    [Fact]
    public void RequestApply_ShouldNotQueue_WhenDispatchFails()
    {
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;

        var state = FloatingDispatchQueueExecutor.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true,
            queueApply: () => false,
            onDecision: decision => callbackReason = decision.Reason);

        state.Should().Be(FloatingDispatchQueueState.Default);
        callbackReason.Should().Be(FloatingDispatchQueueReason.QueueDispatchFailed);
    }

    [Fact]
    public void RequestApply_ShouldNotQueue_WhenDispatchThrows()
    {
        FloatingDispatchQueueReason callbackReason = FloatingDispatchQueueReason.None;
        Exception? dispatchFailure = null;

        var state = FloatingDispatchQueueExecutor.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true,
            queueApply: () => throw new InvalidOperationException("boom"),
            onDecision: decision => callbackReason = decision.Reason,
            onDispatchFailure: ex => dispatchFailure = ex);

        state.Should().Be(FloatingDispatchQueueState.Default);
        callbackReason.Should().Be(FloatingDispatchQueueReason.QueueDispatchFailed);
        dispatchFailure.Should().BeOfType<InvalidOperationException>();
        dispatchFailure!.Message.Should().Be("boom");
    }

    [Fact]
    public void ExecuteQueuedApply_ShouldResetState_WhenApplyThrows()
    {
        var callbackCalled = false;

        var nextState = FloatingDispatchQueueExecutor.ExecuteQueuedApply(
            new FloatingDispatchQueueState(
                ApplyQueued: true,
                ForceEnforceZOrder: true),
            apply: _ => throw new InvalidOperationException("boom"),
            onFailure: _ => callbackCalled = true);

        callbackCalled.Should().BeTrue();
        nextState.Should().Be(FloatingDispatchQueueState.Default);
    }

    [Fact]
    public void RequestApply_ShouldRethrowFatal_WhenDispatchThrowsFatal()
    {
        var act = () => FloatingDispatchQueueExecutor.RequestApply(
            FloatingDispatchQueueState.Default,
            forceEnforceZOrder: true,
            queueApply: () => throw new BadImageFormatException("fatal"));

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void ExecuteQueuedApply_ShouldRethrowFatal_WhenFailureCallbackThrowsFatal()
    {
        var act = () => FloatingDispatchQueueExecutor.ExecuteQueuedApply(
            new FloatingDispatchQueueState(
                ApplyQueued: true,
                ForceEnforceZOrder: true),
            apply: _ => throw new InvalidOperationException("boom"),
            onFailure: _ => throw new BadImageFormatException("fatal-callback"));

        act.Should().Throw<BadImageFormatException>();
    }
}
