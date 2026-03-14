using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderQueueDispatchFailureRollbackStateUpdaterTests
{
    [Fact]
    public void Apply_ShouldRestorePreviousState_WhenQueueDispatchFails()
    {
        var previous = new ZOrderRequestRuntimeState(
            new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc),
            LastForceEnforceZOrder: false);
        var current = new ZOrderRequestRuntimeState(
            new DateTime(2026, 3, 11, 10, 1, 0, DateTimeKind.Utc),
            LastForceEnforceZOrder: true);

        ZOrderQueueDispatchFailureRollbackStateUpdater.Apply(
            ref current,
            queueDispatchFailed: true,
            previousState: previous);

        current.Should().Be(previous);
    }

    [Fact]
    public void Apply_ShouldKeepCurrentState_WhenQueueDispatchSucceeds()
    {
        var previous = new ZOrderRequestRuntimeState(
            new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc),
            LastForceEnforceZOrder: false);
        var current = new ZOrderRequestRuntimeState(
            new DateTime(2026, 3, 11, 10, 1, 0, DateTimeKind.Utc),
            LastForceEnforceZOrder: true);

        ZOrderQueueDispatchFailureRollbackStateUpdater.Apply(
            ref current,
            queueDispatchFailed: false,
            previousState: previous);

        current.LastRequestUtc.Should().Be(new DateTime(2026, 3, 11, 10, 1, 0, DateTimeKind.Utc));
        current.LastForceEnforceZOrder.Should().BeTrue();
    }
}
