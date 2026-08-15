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
        var state = new RuntimeState();

        var outcome = Apply(
            state,
            ToolbarInteractionRetouchDispatchMode.Immediate,
            () => applyCount++,
            _ => throw new InvalidOperationException("scheduler should not be used"));

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.ImmediateApplied);
        applyCount.Should().Be(1);
        state.Queued.Should().BeFalse();
        state.RerunRequested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldRequestRerun_WhenBackgroundRepairIsAlreadyQueued()
    {
        var applyCount = 0;
        var state = new RuntimeState { Queued = true };

        var outcome = Apply(
            state,
            ToolbarInteractionRetouchDispatchMode.Background,
            () => applyCount++,
            _ => true);

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected);
        applyCount.Should().Be(0);
        state.Queued.Should().BeTrue();
        state.RerunRequested.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldReplayOnce_WhenSecondBackgroundRequestArrivesWhileQueued()
    {
        var applyCount = 0;
        var state = new RuntimeState();
        Action? queuedAction = null;

        var firstOutcome = Apply(
            state,
            ToolbarInteractionRetouchDispatchMode.Background,
            () => applyCount++,
            action =>
            {
                queuedAction = action;
                return true;
            });
        var secondOutcome = Apply(
            state,
            ToolbarInteractionRetouchDispatchMode.Background,
            () => applyCount++,
            _ => true);

        firstOutcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduled);
        secondOutcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected);
        state.Queued.Should().BeTrue();
        state.RerunRequested.Should().BeTrue();

        queuedAction.Should().NotBeNull();
        queuedAction!();

        applyCount.Should().Be(2);
        state.Queued.Should().BeFalse();
        state.RerunRequested.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldClearQueuedAndRerunFlags_WhenBackgroundScheduleFails()
    {
        var applyCount = 0;
        var state = new RuntimeState { RerunRequested = true };

        var outcome = Apply(
            state,
            ToolbarInteractionRetouchDispatchMode.Background,
            () => applyCount++,
            _ => false);

        outcome.Should().Be(ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed);
        applyCount.Should().Be(0);
        state.Queued.Should().BeFalse();
        state.RerunRequested.Should().BeFalse();
    }

    private static ToolbarInteractionDirectRepairExecutionOutcome Apply(
        RuntimeState state,
        ToolbarInteractionRetouchDispatchMode mode,
        Action repair,
        Func<Action, bool> schedule)
    {
        return ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
            mode,
            () => state.Queued,
            state.TryMarkQueued,
            () => state.Queued = false,
            () => state.RerunRequested = true,
            state.TryConsumeRerun,
            () => state.RerunRequested = false,
            repair,
            schedule);
    }

    private sealed class RuntimeState
    {
        internal bool Queued { get; set; }
        internal bool RerunRequested { get; set; }

        internal bool TryMarkQueued()
        {
            if (Queued)
            {
                return false;
            }

            Queued = true;
            return true;
        }

        internal bool TryConsumeRerun()
        {
            if (!RerunRequested)
            {
                return false;
            }

            RerunRequested = false;
            return true;
        }
    }
}
