using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayDispatchCoordinatorTests
{
    [Fact]
    public void Apply_ShouldScheduleViaDispatcher_WhenBeginInvokeSucceeds()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);
        string? requested = null;

        var result = CrossPageReplayDispatchCoordinator.Apply(
            ref state,
            CrossPageReplayDispatchTarget.VisualSync,
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: (action, _) =>
            {
                action();
                return true;
            },
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.ScheduledDispatch.Should().BeTrue();
        requested.Should().Be(CrossPageUpdateSources.InkVisualSyncReplay);
        state.VisualSyncReplayPending.Should().BeFalse();
        state.LastDispatchTarget.Should().Be(CrossPageReplayDispatchTarget.VisualSync);
    }

    [Fact]
    public void Apply_ShouldRunInlineFallback_WhenBeginInvokeFailsButDispatcherAccessible()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: false,
            InteractionReplayPending: true,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);
        string? requested = null;

        var result = CrossPageReplayDispatchCoordinator.Apply(
            ref state,
            CrossPageReplayDispatchTarget.Interaction,
            requestCrossPageDisplayUpdate: source => requested = source,
            tryBeginInvoke: static (_, _) => false,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.RanInlineFallback.Should().BeTrue();
        result.RequeuedPending.Should().BeFalse();
        requested.Should().Be(CrossPageUpdateSources.InteractionReplay);
    }

    [Fact]
    public void Apply_ShouldRequeue_WhenBeginInvokeFailsAndDispatcherUnavailable()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: false,
            InteractionReplayPending: true,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var result = CrossPageReplayDispatchCoordinator.Apply(
            ref state,
            CrossPageReplayDispatchTarget.Interaction,
            requestCrossPageDisplayUpdate: static _ => { },
            tryBeginInvoke: static (_, _) => false,
            dispatcherCheckAccess: static () => false,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.RequeuedPending.Should().BeTrue();
        state.InteractionReplayPending.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldRequeue_WhenInlineFallbackThrows()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var result = CrossPageReplayDispatchCoordinator.Apply(
            ref state,
            CrossPageReplayDispatchTarget.VisualSync,
            requestCrossPageDisplayUpdate: static _ => throw new System.InvalidOperationException("boom"),
            tryBeginInvoke: static (_, _) => false,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.RequeuedPending.Should().BeTrue();
        state.VisualSyncReplayPending.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldRethrowFatal_WhenInlineFallbackThrowsFatal()
    {
        var state = new CrossPageReplayRuntimeState(
            VisualSyncReplayPending: true,
            InteractionReplayPending: false,
            PreferInteractionReplay: false,
            LastDispatchTarget: CrossPageReplayDispatchTarget.None);

        var act = () => CrossPageReplayDispatchCoordinator.Apply(
            ref state,
            CrossPageReplayDispatchTarget.VisualSync,
            requestCrossPageDisplayUpdate: static _ => throw new BadImageFormatException("fatal"),
            tryBeginInvoke: static (_, _) => false,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        act.Should().Throw<BadImageFormatException>();
    }
}
