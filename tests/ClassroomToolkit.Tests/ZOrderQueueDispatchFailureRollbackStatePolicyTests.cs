using System;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ZOrderQueueDispatchFailureRollbackStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPreviousState_WhenQueueDispatchFailed()
    {
        var previous = new ZOrderRequestRuntimeState(new DateTime(2026, 1, 1), LastForceEnforceZOrder: false);
        var current = new ZOrderRequestRuntimeState(new DateTime(2026, 1, 2), LastForceEnforceZOrder: true);

        var state = ZOrderQueueDispatchFailureRollbackStatePolicy.Resolve(
            queueDispatchFailed: true,
            previousState: previous,
            currentState: current);

        state.Should().Be(previous);
    }

    [Fact]
    public void Resolve_ShouldReturnCurrentState_WhenQueueDispatchSucceeds()
    {
        var previous = new ZOrderRequestRuntimeState(new DateTime(2026, 1, 1), LastForceEnforceZOrder: false);
        var current = new ZOrderRequestRuntimeState(new DateTime(2026, 1, 2), LastForceEnforceZOrder: true);

        var state = ZOrderQueueDispatchFailureRollbackStatePolicy.Resolve(
            queueDispatchFailed: false,
            previousState: previous,
            currentState: current);

        state.Should().Be(current);
    }
}
