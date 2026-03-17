using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageMissingNeighborRefreshExecutionResult(
    bool Scheduled,
    bool RequestedDelayedRefresh,
    bool RecoveredInlineAfterFailure,
    int DelayMs,
    DateTime LastScheduledUtc);

internal static class CrossPageMissingNeighborRefreshCoordinator
{
    internal delegate bool TryBeginInvokeDelegate(Action action, DispatcherPriority priority);
    internal delegate void DiagnosticsDelegate(string action, string source, string detail);

    internal static async Task<CrossPageMissingNeighborRefreshExecutionResult> ScheduleAsync(
        int missingCount,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive,
        DateTime lastScheduledUtc,
        DateTime nowUtc,
        Func<bool> isCrossPageDisplayActive,
        Action<DateTime> updateLastScheduledUtc,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<int, Task> delayAsync,
        Func<int> incrementRefreshToken,
        Func<int> readRefreshToken,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished,
        DiagnosticsDelegate diagnostics)
    {
        ArgumentNullException.ThrowIfNull(isCrossPageDisplayActive);
        ArgumentNullException.ThrowIfNull(updateLastScheduledUtc);
        ArgumentNullException.ThrowIfNull(requestCrossPageDisplayUpdate);
        ArgumentNullException.ThrowIfNull(tryBeginInvoke);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentNullException.ThrowIfNull(incrementRefreshToken);
        ArgumentNullException.ThrowIfNull(readRefreshToken);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownStarted);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownFinished);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
            photoModeActive,
            crossPageDisplayEnabled,
            interactionActive,
            missingCount,
            lastScheduledUtc,
            nowUtc);
        if (!decision.ShouldSchedule)
        {
            return new CrossPageMissingNeighborRefreshExecutionResult(
                Scheduled: false,
                RequestedDelayedRefresh: false,
                RecoveredInlineAfterFailure: false,
                DelayMs: decision.DelayMs,
                LastScheduledUtc: lastScheduledUtc);
        }

        updateLastScheduledUtc(decision.LastScheduledUtc);
        diagnostics("defer-schedule", CrossPageUpdateSources.NeighborMissing, $"count={missingCount}");
        var token = incrementRefreshToken();

        var delayOutcome = await CrossPageDelayExecutionHelper.TryDelayAsync(decision.DelayMs, delayAsync).ConfigureAwait(false);
        if (!delayOutcome.Success)
        {
            return RecoverAfterFailure(
                source: CrossPageUpdateSources.NeighborMissingDelayed,
                failureDetail: delayOutcome.FailureDetail!,
                requestCrossPageDisplayUpdate: requestCrossPageDisplayUpdate,
                tryBeginInvoke: tryBeginInvoke,
                dispatcherCheckAccess: dispatcherCheckAccess,
                dispatcherShutdownStarted: dispatcherShutdownStarted,
                dispatcherShutdownFinished: dispatcherShutdownFinished,
                diagnostics: diagnostics,
                scheduled: true,
                delayMs: decision.DelayMs,
                lastScheduledUtc: decision.LastScheduledUtc);
        }

        var requestedDelayed = false;
        var scheduledInvoke = tryBeginInvoke(() =>
        {
            if (token != readRefreshToken())
            {
                return;
            }

            if (!isCrossPageDisplayActive())
            {
                return;
            }

            requestCrossPageDisplayUpdate(CrossPageUpdateSources.NeighborMissingDelayed);
            requestedDelayed = true;
        }, DispatcherPriority.Background);

        if (scheduledInvoke)
        {
            return new CrossPageMissingNeighborRefreshExecutionResult(
                Scheduled: true,
                RequestedDelayedRefresh: requestedDelayed,
                RecoveredInlineAfterFailure: false,
                DelayMs: decision.DelayMs,
                LastScheduledUtc: decision.LastScheduledUtc);
        }

        return RecoverAfterFailure(
            source: CrossPageUpdateSources.NeighborMissingDelayed,
            failureDetail: "missing-neighbor-delayed-dispatch-failed",
            requestCrossPageDisplayUpdate: requestCrossPageDisplayUpdate,
            tryBeginInvoke: tryBeginInvoke,
            dispatcherCheckAccess: dispatcherCheckAccess,
            dispatcherShutdownStarted: dispatcherShutdownStarted,
            dispatcherShutdownFinished: dispatcherShutdownFinished,
            diagnostics: diagnostics,
            scheduled: true,
            delayMs: decision.DelayMs,
            lastScheduledUtc: decision.LastScheduledUtc);
    }

    private static CrossPageMissingNeighborRefreshExecutionResult RecoverAfterFailure(
        string source,
        string failureDetail,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished,
        DiagnosticsDelegate diagnostics,
        bool scheduled,
        int delayMs,
        DateTime lastScheduledUtc)
    {
        var recoverySource = CrossPageUpdateSources.WithImmediate(source);
        var scheduledRecovery = tryBeginInvoke(
            () => requestCrossPageDisplayUpdate(recoverySource),
            DispatcherPriority.Background);
        var recoveryDecision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
            recoveryDispatchScheduled: scheduledRecovery,
            dispatcherCheckAccess: dispatcherCheckAccess(),
            dispatcherShutdownStarted: dispatcherShutdownStarted(),
            dispatcherShutdownFinished: dispatcherShutdownFinished());
        if (recoveryDecision.ShouldRecoverInline)
        {
            requestCrossPageDisplayUpdate(recoverySource);
            return new CrossPageMissingNeighborRefreshExecutionResult(
                Scheduled: scheduled,
                RequestedDelayedRefresh: false,
                RecoveredInlineAfterFailure: true,
                DelayMs: delayMs,
                LastScheduledUtc: lastScheduledUtc);
        }

        diagnostics("defer-abort", source, failureDetail);
        return new CrossPageMissingNeighborRefreshExecutionResult(
            Scheduled: scheduled,
            RequestedDelayedRefresh: false,
            RecoveredInlineAfterFailure: false,
            DelayMs: delayMs,
            LastScheduledUtc: lastScheduledUtc);
    }
}
