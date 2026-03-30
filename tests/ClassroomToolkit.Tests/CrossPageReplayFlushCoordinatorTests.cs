using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayFlushCoordinatorTests
{
    [Fact]
    public void Resolve_ShouldNotFlush_WhenReplayNotPending()
    {
        var state = CrossPageReplayRuntimeState.Default;

        var result = CrossPageReplayFlushCoordinator.Resolve(
            replayState: state,
            crossPageUpdatePending: false,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false);

        result.ShouldFlush.Should().BeFalse();
        result.HasDispatchTarget.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldNotFlush_WhenCrossPageUpdateAlreadyPending()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var result = CrossPageReplayFlushCoordinator.Resolve(
            replayState: state,
            crossPageUpdatePending: true,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false);

        result.ShouldFlush.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldFlushVisualSync_WhenOnlyVisualSyncPending()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var result = CrossPageReplayFlushCoordinator.Resolve(
            replayState: state,
            crossPageUpdatePending: false,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false);

        result.ShouldFlush.Should().BeTrue();
        result.HasDispatchTarget.Should().BeTrue();
        result.DispatchTarget.Should().Be(CrossPageReplayDispatchTarget.VisualSync);
    }

    [Fact]
    public void Resolve_ShouldPreferInteraction_WhenBothPendingAndPreferenceSet()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: true,
            PreferInteractionReplay: true,
            LastDispatchTarget: CrossPageReplayDispatchTarget.VisualSync);

        var result = CrossPageReplayFlushCoordinator.Resolve(
            replayState: state,
            crossPageUpdatePending: false,
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false);

        result.HasDispatchTarget.Should().BeTrue();
        result.DispatchTarget.Should().Be(CrossPageReplayDispatchTarget.Interaction);
    }
}
