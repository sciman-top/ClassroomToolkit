using System;
using System.Windows.Threading;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageReplayDispatchExecutionResult(
    bool ScheduledDispatch,
    bool RanInlineFallback,
    bool RequeuedPending,
    string? Source);

internal static class CrossPageReplayDispatchCoordinator
{
    internal delegate bool TryBeginInvokeDelegate(Action action, DispatcherPriority priority);

    internal static CrossPageReplayDispatchExecutionResult Apply(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayDispatchTarget target,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished)
    {
        ArgumentNullException.ThrowIfNull(requestCrossPageDisplayUpdate);
        ArgumentNullException.ThrowIfNull(tryBeginInvoke);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownStarted);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownFinished);

        if (!CrossPageReplayPendingStateUpdater.TryMarkDispatchScheduled(ref state, target))
        {
            return default;
        }

        var source = CrossPageReplayDispatchRequestPolicy.ResolveSource(target);
        if (string.IsNullOrWhiteSpace(source))
        {
            CrossPageReplayPendingStateUpdater.MarkDispatchFailed(ref state, target);
            return new CrossPageReplayDispatchExecutionResult(
                ScheduledDispatch: false,
                RanInlineFallback: false,
                RequeuedPending: true,
                Source: null);
        }

        var scheduled = tryBeginInvoke(
            () => requestCrossPageDisplayUpdate(source),
            DispatcherPriority.Background);
        if (scheduled)
        {
            return new CrossPageReplayDispatchExecutionResult(
                ScheduledDispatch: true,
                RanInlineFallback: false,
                RequeuedPending: false,
                Source: source);
        }

        var fallbackDecision = CrossPageReplayDispatchScheduleFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: dispatcherCheckAccess(),
            dispatcherShutdownStarted: dispatcherShutdownStarted(),
            dispatcherShutdownFinished: dispatcherShutdownFinished());
        if (fallbackDecision.ShouldRunInline)
        {
            var inlineFallbackSucceeded = SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    requestCrossPageDisplayUpdate(source);
                    return true;
                },
                fallback: false);
            if (inlineFallbackSucceeded)
            {
                return new CrossPageReplayDispatchExecutionResult(
                    ScheduledDispatch: false,
                    RanInlineFallback: true,
                    RequeuedPending: false,
                    Source: source);
            }
        }

        if (fallbackDecision.ShouldRequeuePending || fallbackDecision.ShouldRunInline)
        {
            CrossPageReplayPendingStateUpdater.MarkDispatchFailed(ref state, target);
            return new CrossPageReplayDispatchExecutionResult(
                ScheduledDispatch: false,
                RanInlineFallback: fallbackDecision.ShouldRunInline,
                RequeuedPending: true,
                Source: source);
        }

        return new CrossPageReplayDispatchExecutionResult(
            ScheduledDispatch: false,
            RanInlineFallback: false,
            RequeuedPending: false,
            Source: source);
    }
}
