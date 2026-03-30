using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateDispatchFailureCoordinatorTests
{
    [Fact]
    public void Apply_ShouldRunInline_WhenDispatcherCanRunCurrentThread()
    {
        var replayState = CrossPageReplayRuntimeState.Default;
        string? mode = null;
        var flushCount = 0;

        var result = CrossPageDisplayUpdateDispatchFailureCoordinator.Apply(
            ref replayState,
            CrossPageUpdateSourceKind.VisualSync,
            source: CrossPageUpdateSources.PhotoPan,
            mode: "direct",
            emitAbortDiagnostics: true,
            executeCrossPageDisplayUpdateRun: (source, selectedMode, emitAbort) =>
            {
                source.Should().Be(CrossPageUpdateSources.PhotoPan);
                emitAbort.Should().BeTrue();
                mode = selectedMode;
            },
            emitDiagnostics: static (_, _, _) => { },
            flushCrossPageReplay: () => flushCount++,
            dispatcherCheckAccess: static () => true,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.RanInline.Should().BeTrue();
        mode.Should().Be("direct-inline-fallback");
        flushCount.Should().Be(0);
    }

    [Fact]
    public void Apply_ShouldQueueReplayAndFlush_WhenDispatcherUnavailable()
    {
        var replayState = CrossPageReplayRuntimeState.Default;
        string? diag = null;
        var flushCount = 0;

        var result = CrossPageDisplayUpdateDispatchFailureCoordinator.Apply(
            ref replayState,
            CrossPageUpdateSourceKind.VisualSync,
            source: CrossPageUpdateSources.PhotoPan,
            mode: "direct",
            emitAbortDiagnostics: false,
            executeCrossPageDisplayUpdateRun: static (_, _, _) => throw new Xunit.Sdk.XunitException("should not run inline"),
            emitDiagnostics: (action, source, detail) => diag = $"{action}:{source}:{detail}",
            flushCrossPageReplay: () => flushCount++,
            dispatcherCheckAccess: static () => false,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        result.QueuedReplay.Should().BeTrue();
        result.RequestedReplayFlush.Should().BeTrue();
        replayState.VisualSyncReplayPending.Should().BeTrue();
        flushCount.Should().Be(1);
        diag.Should().Be($"recover:{CrossPageUpdateSources.PhotoPan}:dispatch-failed-queue-replay");
    }

    [Fact]
    public void Apply_ShouldQueueInteractionReplay_ForInteractionSource()
    {
        var replayState = CrossPageReplayRuntimeState.Default;

        _ = CrossPageDisplayUpdateDispatchFailureCoordinator.Apply(
            ref replayState,
            CrossPageUpdateSourceKind.Interaction,
            source: CrossPageUpdateSources.InteractionReplay,
            mode: "direct",
            emitAbortDiagnostics: false,
            executeCrossPageDisplayUpdateRun: static (_, _, _) => throw new Xunit.Sdk.XunitException("should not run inline"),
            emitDiagnostics: static (_, _, _) => { },
            flushCrossPageReplay: static () => { },
            dispatcherCheckAccess: static () => false,
            dispatcherShutdownStarted: static () => false,
            dispatcherShutdownFinished: static () => false);

        replayState.InteractionReplayPending.Should().BeTrue();
    }
}
