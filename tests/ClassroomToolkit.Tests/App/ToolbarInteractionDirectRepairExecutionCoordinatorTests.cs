using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionDirectRepairExecutionCoordinatorTests
{
    [Fact]
    public void Apply_ShouldRunImmediateRepair_WhenDispatchModeIsImmediate()
    {
        var applyCount = 0;
        var queued = false;
        var rerunRequested = false;

        var outcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            ToolbarInteractionRetouchDispatchMode.Immediate,
            () => queued,
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued),
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref rerunRequested),
            () => applyCount++,
            _ => throw new InvalidOperationException("scheduler should not be used"));

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.ImmediateApplied);
        applyCount.Should().Be(1);
        queued.Should().BeFalse();
        rerunRequested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldRequestRerun_WhenBackgroundRepairIsAlreadyQueued()
    {
        var applyCount = 0;
        var queued = true;
        var rerunRequested = false;

        var outcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            ToolbarInteractionRetouchDispatchMode.Background,
            () => queued,
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued),
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref rerunRequested),
            () => applyCount++,
            _ => true);

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected);
        applyCount.Should().Be(0);
        queued.Should().BeTrue();
        rerunRequested.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldReplayOnce_WhenSecondBackgroundRequestArrivesWhileQueued()
    {
        var applyCount = 0;
        var queued = false;
        var rerunRequested = false;
        Action? queuedAction = null;

        var firstOutcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            ToolbarInteractionRetouchDispatchMode.Background,
            () => queued,
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued),
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref rerunRequested),
            () => applyCount++,
            action =>
            {
                queuedAction = action;
                return true;
            });

        var secondOutcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            ToolbarInteractionRetouchDispatchMode.Background,
            () => queued,
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued),
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref rerunRequested),
            () => applyCount++,
            _ => true);

        firstOutcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduled);
        secondOutcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected);
        queued.Should().BeTrue();
        rerunRequested.Should().BeTrue();

        queuedAction.Should().NotBeNull();
        queuedAction!();

        applyCount.Should().Be(2);
        queued.Should().BeFalse();
        rerunRequested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldClearQueuedAndRerunFlags_WhenBackgroundScheduleFails()
    {
        var applyCount = 0;
        var queued = false;
        var rerunRequested = true;

        var outcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            ToolbarInteractionRetouchDispatchMode.Background,
            () => queued,
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref queued),
            () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref queued),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref rerunRequested),
            () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref rerunRequested),
            () => applyCount++,
            _ => false);

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed);
        applyCount.Should().Be(0);
        queued.Should().BeFalse();
        rerunRequested.Should().BeFalse();
    }
}
