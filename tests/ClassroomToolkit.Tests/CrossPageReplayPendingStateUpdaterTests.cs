using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayPendingStateUpdaterTests
{
    [Fact]
    public void HasPending_ShouldReturnTrue_WhenAnyReplayPending()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: false,
            InteractionReplayPending: true,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        CrossPageReplayPendingStateUpdater.HasPending(state).Should().BeTrue();
    }

    [Fact]
    public void ApplyQueueDecision_RuntimeState_ShouldMergeReplayFlags()
    {
        var state = CrossPageReplayRuntimeState.Default;
        var decision = new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: false);

        CrossPageReplayPendingStateUpdater.ApplyQueueDecision(ref state, decision);

        state.VisualSyncReplayPending.Should().BeTrue();
        state.InteractionReplayPending.Should().BeFalse();
        state.PreferInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void ApplyQueueDecision_RuntimeState_ShouldEnableInteractionPreference_ForDualQueue()
    {
        var state = CrossPageReplayRuntimeState.Default;
        var decision = new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: true);

        CrossPageReplayPendingStateUpdater.ApplyQueueDecision(ref state, decision);

        state.PreferInteractionReplay.Should().BeTrue();
    }

    [Fact]
    public void TryMarkDispatchScheduled_ShouldClearPendingAndTrackTarget()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var accepted = CrossPageReplayPendingStateUpdater.TryMarkDispatchScheduled(
            ref state,
            CrossPageReplayDispatchTarget.VisualSync);

        accepted.Should().BeTrue();
        state.VisualSyncReplayPending.Should().BeFalse();
        state.LastDispatchTarget.Should().Be(CrossPageReplayDispatchTarget.VisualSync);
    }

    [Fact]
    public void TryMarkDispatchScheduled_ShouldClearInteractionPreference_WhenInteractionDispatched()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: true,
            PreferInteractionReplay: true,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var accepted = CrossPageReplayPendingStateUpdater.TryMarkDispatchScheduled(
            ref state,
            CrossPageReplayDispatchTarget.Interaction);

        accepted.Should().BeTrue();
        state.InteractionReplayPending.Should().BeFalse();
        state.PreferInteractionReplay.Should().BeFalse();
    }

    [Fact]
    public void Reset_RuntimeState_ShouldRestoreDefault()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: true,
            PreferInteractionReplay: true,
            LastDispatchTarget: CrossPageReplayDispatchTarget.Interaction);

        CrossPageReplayPendingStateUpdater.Reset(ref state);

        state.Should().Be(CrossPageReplayRuntimeState.Default);
    }

    [Fact]
    public void MarkDispatchFailed_RuntimeState_ShouldRequeueTargetPending()
    {
        var state = CrossPageReplayRuntimeState.Default;

        CrossPageReplayPendingStateUpdater.MarkDispatchFailed(
            ref state,
            CrossPageReplayDispatchTarget.VisualSync);

        state.VisualSyncReplayPending.Should().BeTrue();
        state.InteractionReplayPending.Should().BeFalse();
    }

    [Fact]
    public void ApplyQueueDecision_ShouldMergeReplayFlags()
    {
        var visual = false;
        var interaction = false;
        var decision = new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: false);

        CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
            ref visual,
            ref interaction,
            decision);

        visual.Should().BeTrue();
        interaction.Should().BeFalse();
    }

    [Fact]
    public void MarkDispatchFailed_ShouldSetTargetFlag()
    {
        var visual = false;
        var interaction = false;

        CrossPageReplayPendingStateUpdater.MarkDispatchFailed(
            ref visual,
            ref interaction,
            CrossPageReplayDispatchTarget.Interaction);

        visual.Should().BeFalse();
        interaction.Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldClearBothFlags()
    {
        var visual = true;
        var interaction = true;

        CrossPageReplayPendingStateUpdater.Reset(ref visual, ref interaction);

        visual.Should().BeFalse();
        interaction.Should().BeFalse();
    }
}
