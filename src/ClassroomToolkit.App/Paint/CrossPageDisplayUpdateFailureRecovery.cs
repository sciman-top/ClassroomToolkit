using System;

namespace ClassroomToolkit.App.Paint;

internal enum CrossPageDisplayUpdateDispatchFailureFallbackReason
{
    None = 0,
    InlineCurrentThread = 1,
    QueueReplay = 2
}

internal readonly record struct CrossPageDisplayUpdateDispatchFailureFallbackDecision(
    bool ShouldRunInline,
    bool ShouldQueueReplay,
    CrossPageDisplayUpdateDispatchFailureFallbackReason Reason);

internal static class CrossPageDisplayUpdateDispatchFailureFallbackPolicy
{
    internal static CrossPageDisplayUpdateDispatchFailureFallbackDecision Resolve(
        bool dispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (dispatchScheduled)
        {
            return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
                ShouldRunInline: false,
                ShouldQueueReplay: false,
                Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.None);
        }

        if (dispatcherCheckAccess && !dispatcherShutdownStarted && !dispatcherShutdownFinished)
        {
            return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
                ShouldRunInline: true,
                ShouldQueueReplay: false,
                Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.InlineCurrentThread);
        }

        return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
            ShouldRunInline: false,
            ShouldQueueReplay: true,
            Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.QueueReplay);
    }
}

internal readonly record struct CrossPageDisplayUpdateDispatchFailureExecutionResult(
    bool RanInline,
    bool QueuedReplay,
    bool RequestedReplayFlush);

internal static class CrossPageDisplayUpdateDispatchFailureCoordinator
{
    internal static CrossPageDisplayUpdateDispatchFailureExecutionResult Apply(
        ref CrossPageReplayRuntimeState replayState,
        CrossPageUpdateSourceKind kind,
        string source,
        string mode,
        bool emitAbortDiagnostics,
        Action<string, string, bool> executeCrossPageDisplayUpdateRun,
        Action<string, string, string> emitDiagnostics,
        Action flushCrossPageReplay,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(executeCrossPageDisplayUpdateRun);
        ArgumentNullException.ThrowIfNull(emitDiagnostics);
        ArgumentNullException.ThrowIfNull(flushCrossPageReplay);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownStarted);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownFinished);

        var fallbackDecision = CrossPageDisplayUpdateDispatchFailureFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: dispatcherCheckAccess(),
            dispatcherShutdownStarted: dispatcherShutdownStarted(),
            dispatcherShutdownFinished: dispatcherShutdownFinished());
        if (fallbackDecision.ShouldRunInline)
        {
            executeCrossPageDisplayUpdateRun(
                source,
                $"{mode}-inline-fallback",
                emitAbortDiagnostics);
            return new CrossPageDisplayUpdateDispatchFailureExecutionResult(
                RanInline: true,
                QueuedReplay: false,
                RequestedReplayFlush: false);
        }

        if (fallbackDecision.ShouldQueueReplay)
        {
            var replayQueueDecision = CrossPageReplayQueuePolicy.Resolve(kind, source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref replayState,
                replayQueueDecision);
            emitDiagnostics("recover", source, "dispatch-failed-queue-replay");
            flushCrossPageReplay();
            return new CrossPageDisplayUpdateDispatchFailureExecutionResult(
                RanInline: false,
                QueuedReplay: true,
                RequestedReplayFlush: true);
        }

        return default;
    }
}
