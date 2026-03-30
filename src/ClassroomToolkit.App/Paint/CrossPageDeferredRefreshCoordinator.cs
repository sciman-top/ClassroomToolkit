using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDeferredRefreshExecutionResult(
    bool SkippedBeforeSchedule,
    bool RequestedImmediateRefresh,
    bool ScheduledDelayedRefresh,
    bool RequestedDelayedRefresh,
    bool RecoveredInlineAfterFailure,
    int DelayMs);

internal static class CrossPageDeferredRefreshCoordinator
{
    internal delegate bool TryAcquirePostInputRefreshSlotDelegate(out long pointerUpSequence);
    internal delegate bool TryBeginInvokeDelegate(Action action, DispatcherPriority priority);
    internal delegate void DiagnosticsDelegate(string action, string source, string detail);

    internal static async Task<CrossPageDeferredRefreshExecutionResult> ScheduleAsync(
        string source,
        bool singlePerPointerUp,
        int? delayOverrideMs,
        int configuredDelayMs,
        DateTime lastPointerUpUtc,
        Func<DateTime> getCurrentUtcTimestamp,
        Func<bool> isCrossPageDisplayActive,
        Func<bool> isCrossPageInteractionActive,
        TryAcquirePostInputRefreshSlotDelegate tryAcquirePostInputRefreshSlot,
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
        ArgumentNullException.ThrowIfNull(getCurrentUtcTimestamp);
        ArgumentNullException.ThrowIfNull(isCrossPageDisplayActive);
        ArgumentNullException.ThrowIfNull(isCrossPageInteractionActive);
        ArgumentNullException.ThrowIfNull(tryAcquirePostInputRefreshSlot);
        ArgumentNullException.ThrowIfNull(requestCrossPageDisplayUpdate);
        ArgumentNullException.ThrowIfNull(tryBeginInvoke);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentNullException.ThrowIfNull(incrementRefreshToken);
        ArgumentNullException.ThrowIfNull(readRefreshToken);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownStarted);
        ArgumentNullException.ThrowIfNull(dispatcherShutdownFinished);
        ArgumentNullException.ThrowIfNull(diagnostics);

        try
        {
            var scheduleGate = CrossPageDeferredRefreshGatePolicy.ResolveBeforeSchedule(
                isCrossPageDisplayActive(),
                isCrossPageInteractionActive());
            if (!scheduleGate.ShouldProceed)
            {
                diagnostics(
                    "defer-skip",
                    source,
                    scheduleGate.Reason ?? CrossPageDeferredDiagnosticReason.Inactive);
                return new CrossPageDeferredRefreshExecutionResult(
                    SkippedBeforeSchedule: true,
                    RequestedImmediateRefresh: false,
                    ScheduledDelayedRefresh: false,
                    RequestedDelayedRefresh: false,
                    RecoveredInlineAfterFailure: false,
                    DelayMs: 0);
            }

            var targetDelayMs = CrossPagePostInputDelayPolicy.ResolveMs(
                source,
                configuredDelayMs,
                fallbackDelayMs: CrossPageRuntimeDefaults.PostInputRefreshDelayMs,
                delayOverrideMs: delayOverrideMs);

            var elapsedMs = (getCurrentUtcTimestamp() - lastPointerUpUtc).TotalMilliseconds;
            if (elapsedMs >= targetDelayMs || lastPointerUpUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
            {
                if (singlePerPointerUp && !tryAcquirePostInputRefreshSlot(out var seqImmediate))
                {
                    diagnostics("defer-skip", source, $"already-refreshed seq={seqImmediate}");
                    return new CrossPageDeferredRefreshExecutionResult(
                        SkippedBeforeSchedule: false,
                        RequestedImmediateRefresh: false,
                        ScheduledDelayedRefresh: false,
                        RequestedDelayedRefresh: false,
                        RecoveredInlineAfterFailure: false,
                        DelayMs: 0);
                }

                requestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(source));
                return new CrossPageDeferredRefreshExecutionResult(
                    SkippedBeforeSchedule: false,
                    RequestedImmediateRefresh: true,
                    ScheduledDelayedRefresh: false,
                    RequestedDelayedRefresh: false,
                    RecoveredInlineAfterFailure: false,
                    DelayMs: 0);
            }

            var delay = Math.Max(1, (int)Math.Ceiling(targetDelayMs - elapsedMs));
            diagnostics("defer-schedule", source, $"delayMs={delay}");
            var token = incrementRefreshToken();

            var delayOutcome = await CrossPageDelayExecutionHelper.TryDelayAsync(delay, delayAsync).ConfigureAwait(false);
            if (!delayOutcome.Success)
            {
                return RecoverAfterDelayFailure(
                    source,
                    delayOutcome.FailureDetail!,
                    requestCrossPageDisplayUpdate,
                    tryBeginInvoke,
                    dispatcherCheckAccess,
                    dispatcherShutdownStarted,
                    dispatcherShutdownFinished,
                    diagnostics);
            }

            var delayedRequested = false;
            var delayedSkipped = false;
            var delayedAborted = false;
            var scheduled = tryBeginInvoke(() =>
            {
                if (token != readRefreshToken())
                {
                    return;
                }

                var delayedDispatchGate = CrossPageDeferredRefreshGatePolicy.ResolveBeforeDelayedDispatch(
                    isCrossPageDisplayActive(),
                    isCrossPageInteractionActive());
                if (!delayedDispatchGate.ShouldProceed)
                {
                    diagnostics(
                        "defer-abort",
                        source,
                        delayedDispatchGate.Reason ?? CrossPageDeferredDiagnosticReason.InactiveOrInteractionActive);
                    delayedAborted = true;
                    return;
                }

                if (singlePerPointerUp && !tryAcquirePostInputRefreshSlot(out var seqDelayed))
                {
                    diagnostics("defer-skip", source, $"already-refreshed seq={seqDelayed}");
                    delayedSkipped = true;
                    return;
                }

                requestCrossPageDisplayUpdate(CrossPageUpdateSources.WithDelayed(source));
                delayedRequested = true;
            }, DispatcherPriority.Background);

            if (scheduled)
            {
                return new CrossPageDeferredRefreshExecutionResult(
                    SkippedBeforeSchedule: false,
                    RequestedImmediateRefresh: false,
                    ScheduledDelayedRefresh: true,
                    RequestedDelayedRefresh: delayedRequested && !delayedSkipped && !delayedAborted,
                    RecoveredInlineAfterFailure: false,
                    DelayMs: delay);
            }

            var failureResult = RecoverAfterDelayedDispatchFailure(
                source,
                requestCrossPageDisplayUpdate,
                tryBeginInvoke,
                dispatcherCheckAccess,
                dispatcherShutdownStarted,
                dispatcherShutdownFinished,
                diagnostics);
            return failureResult with { DelayMs = delay, ScheduledDelayedRefresh = true };
        }
        catch (Exception ex) when (global::ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            diagnostics("defer-abort", source, $"nonfatal:{ex.GetType().Name}");
            return new CrossPageDeferredRefreshExecutionResult(
                SkippedBeforeSchedule: false,
                RequestedImmediateRefresh: false,
                ScheduledDelayedRefresh: false,
                RequestedDelayedRefresh: false,
                RecoveredInlineAfterFailure: false,
                DelayMs: 0);
        }
    }

    private static CrossPageDeferredRefreshExecutionResult RecoverAfterDelayFailure(
        string source,
        string failureDetail,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished,
        DiagnosticsDelegate diagnostics)
    {
        return RecoverAfterFailureCore(
            source,
            requestCrossPageDisplayUpdate,
            tryBeginInvoke,
            dispatcherCheckAccess,
            dispatcherShutdownStarted,
            dispatcherShutdownFinished,
            diagnostics,
            abortDetail: failureDetail,
            recoverDiagnosticsDetail: CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatInlineRecoveryDetail(
                tokenMatched: true));
    }

    private static CrossPageDeferredRefreshExecutionResult RecoverAfterDelayedDispatchFailure(
        string source,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished,
        DiagnosticsDelegate diagnostics)
    {
        return RecoverAfterFailureCore(
            source,
            requestCrossPageDisplayUpdate,
            tryBeginInvoke,
            dispatcherCheckAccess,
            dispatcherShutdownStarted,
            dispatcherShutdownFinished,
            diagnostics,
            abortDetail: "delayed-dispatch-failed",
            recoverDiagnosticsDetail: null);
    }

    private static CrossPageDeferredRefreshExecutionResult RecoverAfterFailureCore(
        string source,
        Action<string> requestCrossPageDisplayUpdate,
        TryBeginInvokeDelegate tryBeginInvoke,
        Func<bool> dispatcherCheckAccess,
        Func<bool> dispatcherShutdownStarted,
        Func<bool> dispatcherShutdownFinished,
        DiagnosticsDelegate diagnostics,
        string abortDetail,
        string? recoverDiagnosticsDetail)
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
            if (!string.IsNullOrWhiteSpace(recoverDiagnosticsDetail))
            {
                diagnostics("defer-recover", source, recoverDiagnosticsDetail);
            }

            return new CrossPageDeferredRefreshExecutionResult(
                SkippedBeforeSchedule: false,
                RequestedImmediateRefresh: false,
                ScheduledDelayedRefresh: true,
                RequestedDelayedRefresh: false,
                RecoveredInlineAfterFailure: true,
                DelayMs: 0);
        }

        diagnostics("defer-abort", source, abortDetail);
        return new CrossPageDeferredRefreshExecutionResult(
            SkippedBeforeSchedule: false,
            RequestedImmediateRefresh: false,
            ScheduledDelayedRefresh: true,
            RequestedDelayedRefresh: false,
            RecoveredInlineAfterFailure: false,
            DelayMs: 0);
    }
}
