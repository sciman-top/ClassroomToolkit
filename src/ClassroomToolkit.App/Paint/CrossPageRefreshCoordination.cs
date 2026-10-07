using System.Threading.Tasks;
using System.Windows.Threading;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDeferredDiagnosticReason
{
    internal const string Inactive = "inactive";
    internal const string InkActive = "ink-active";
    internal const string InteractionActive = "interaction-active";
    internal const string InactiveOrInkActive = "inactive-or-ink-active";
    internal const string InactiveOrInteractionActive = "inactive-or-interaction-active";
}

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

internal readonly record struct CrossPageDeferredRefreshGateDecision(
    bool ShouldProceed,
    string? Reason);

internal static class CrossPageDeferredRefreshGatePolicy
{
    internal static CrossPageDeferredRefreshGateDecision ResolveBeforeSchedule(
        bool crossPageDisplayActive,
        bool interactionActive)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.Inactive);
        }

        if (interactionActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.InteractionActive);
        }

        return new CrossPageDeferredRefreshGateDecision(
            ShouldProceed: true,
            Reason: null);
    }

    internal static CrossPageDeferredRefreshGateDecision ResolveBeforeDelayedDispatch(
        bool crossPageDisplayActive,
        bool interactionActive)
    {
        if (!crossPageDisplayActive || interactionActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.InactiveOrInteractionActive);
        }

        return new CrossPageDeferredRefreshGateDecision(
            ShouldProceed: true,
            Reason: null);
    }
}

internal static class CrossPageDeferredRefreshPolicy
{
    internal static bool ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode refreshMode)
    {
        return refreshMode == CrossPageInteractiveSwitchRefreshMode.DeferredByInput;
    }

    internal static bool ShouldRunOnPointerUp(
        bool deferredByInkInput,
        bool crossPageDisplayActive)
    {
        return deferredByInkInput && crossPageDisplayActive;
    }
}

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

        try
        {
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
        catch (Exception ex) when (global::ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            diagnostics("defer-abort", CrossPageUpdateSources.NeighborMissing, $"nonfatal:{ex.GetType().Name}");
            return new CrossPageMissingNeighborRefreshExecutionResult(
                Scheduled: false,
                RequestedDelayedRefresh: false,
                RecoveredInlineAfterFailure: false,
                DelayMs: 0,
                LastScheduledUtc: lastScheduledUtc);
        }
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

internal static class CrossPageMissingNeighborRefreshNormalizationDefaults
{
    internal const int MinPositiveIntervalMs = 1;
    internal const int MinMissingThreshold = 1;
}

internal readonly record struct CrossPageMissingNeighborRefreshDecision(
    bool ShouldSchedule,
    DateTime LastScheduledUtc,
    int DelayMs);

internal static class CrossPageMissingNeighborRefreshPolicy
{
    internal static CrossPageMissingNeighborRefreshDecision Resolve(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive,
        int missingCount,
        DateTime lastScheduledUtc,
        DateTime nowUtc,
        int minIntervalMs = CrossPageMissingNeighborRefreshThresholds.MinIntervalMs,
        int delayMs = CrossPageMissingNeighborRefreshThresholds.DelayMs,
        int interactionMinIntervalMs = CrossPageMissingNeighborRefreshThresholds.InteractionMinIntervalMs,
        int interactionDelayMs = CrossPageMissingNeighborRefreshThresholds.InteractionDelayMs,
        int interactionMissingThreshold = CrossPageMissingNeighborRefreshThresholds.InteractionMissingThreshold)
    {
        var normalizedMinIntervalMs = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
            minIntervalMs);
        var normalizedDelayMs = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
            delayMs);
        var normalizedInteractionMinIntervalMs = Math.Max(
            normalizedMinIntervalMs,
            Math.Max(
                CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
                interactionMinIntervalMs));
        var normalizedInteractionDelayMs = Math.Max(
            normalizedDelayMs,
            Math.Max(
                CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
                interactionDelayMs));
        var normalizedInteractionMissingThreshold = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinMissingThreshold,
            interactionMissingThreshold);

        if (!photoModeActive || !crossPageDisplayEnabled || missingCount <= 0)
        {
            return new CrossPageMissingNeighborRefreshDecision(
                ShouldSchedule: false,
                LastScheduledUtc: lastScheduledUtc,
                DelayMs: normalizedDelayMs);
        }

        var effectiveMinIntervalMs = normalizedMinIntervalMs;
        var effectiveDelayMs = normalizedDelayMs;
        if (interactionActive)
        {
            if (missingCount < normalizedInteractionMissingThreshold)
            {
                return new CrossPageMissingNeighborRefreshDecision(
                    ShouldSchedule: false,
                    LastScheduledUtc: lastScheduledUtc,
                    DelayMs: normalizedDelayMs);
            }

            effectiveMinIntervalMs = normalizedInteractionMinIntervalMs;
            effectiveDelayMs = normalizedInteractionDelayMs;
        }

        if (lastScheduledUtc != CrossPageRuntimeDefaults.UnsetTimestampUtc
            && (nowUtc - lastScheduledUtc).TotalMilliseconds < effectiveMinIntervalMs)
        {
            return new CrossPageMissingNeighborRefreshDecision(
                ShouldSchedule: false,
                LastScheduledUtc: lastScheduledUtc,
                DelayMs: effectiveDelayMs);
        }

        return new CrossPageMissingNeighborRefreshDecision(
            ShouldSchedule: true,
            LastScheduledUtc: nowUtc,
            DelayMs: effectiveDelayMs);
    }
}

internal static class CrossPageMissingNeighborRefreshThresholds
{
    internal const int MinIntervalMs = 140;
    internal const int DelayMs = 120;
    internal const int InteractionMinIntervalMs = 420;
    internal const int InteractionDelayMs = 220;
    internal const int InteractionMissingThreshold = 2;
}

internal static class CrossPageNavigationCurrentInkRefreshPolicy
{
    internal static bool ShouldRequest(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        PaintToolMode mode)
    {
        if (!pageChanged || !photoInkModeActive)
        {
            return false;
        }

        if (interactiveSwitch)
        {
            return mode == PaintToolMode.Brush || mode == PaintToolMode.Eraser;
        }

        return mode == PaintToolMode.Brush
            || mode == PaintToolMode.Eraser
            || mode == PaintToolMode.RegionErase;
    }
}

internal static class CrossPagePostInputDelayPolicy
{
    internal static int ResolveMs(
        string source,
        int configuredDelayMs,
        int fallbackDelayMs = CrossPagePostInputDelayThresholds.FallbackDelayMs,
        int? delayOverrideMs = null)
    {
        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var baseSource = parsed.BaseSource;

        var baseline = delayOverrideMs ?? configuredDelayMs;
        if (baseline <= 0)
        {
            baseline = fallbackDelayMs;
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborRender, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.NeighborSidecar, StringComparison.Ordinal))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.NeighborRenderMinMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborMissing, StringComparison.Ordinal))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.NeighborMissingMinMs);
        }

        if (CrossPageUpdateReplayPolicy.IsReplayBaseSource(baseSource))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.ReplayMinMs);
        }

        return Math.Max(1, baseline);
    }
}

internal static class CrossPagePostInputDelayThresholds
{
    internal const int FallbackDelayMs = CrossPageRuntimeDefaults.PostInputRefreshDelayMs;
    internal const int NeighborRenderMinMs = 180;
    internal const int NeighborMissingMinMs = 200;
    internal const int ReplayMinMs = 220;
}

internal static class CrossPagePostInputRefreshDelayClampPolicy
{
    internal const int MinDelayMs = 40;
    internal const int MaxDelayMs = 400;

    internal static int Clamp(int delayMs)
    {
        return Math.Clamp(delayMs, MinDelayMs, MaxDelayMs);
    }
}

internal readonly record struct CrossPagePostInputRefreshSlotAcquireResult(
    bool Acquired,
    long PointerUpSequence);

internal static class CrossPagePostInputRefreshSlotCoordinator
{
    internal delegate long ReadAppliedSequenceDelegate();
    internal delegate long CompareExchangeAppliedSequenceDelegate(long nextValue, long comparand);

    internal static CrossPagePostInputRefreshSlotAcquireResult TryAcquire(
        long pointerUpSequence,
        DateTime lastPointerUpUtc,
        ReadAppliedSequenceDelegate readAppliedSequence,
        CompareExchangeAppliedSequenceDelegate compareExchangeAppliedSequence)
    {
        ArgumentNullException.ThrowIfNull(readAppliedSequence);
        ArgumentNullException.ThrowIfNull(compareExchangeAppliedSequence);

        if (lastPointerUpUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return new CrossPagePostInputRefreshSlotAcquireResult(
                Acquired: true,
                PointerUpSequence: pointerUpSequence);
        }

        while (true)
        {
            var appliedSequence = readAppliedSequence();
            if (appliedSequence == pointerUpSequence)
            {
                return new CrossPagePostInputRefreshSlotAcquireResult(
                    Acquired: false,
                    PointerUpSequence: pointerUpSequence);
            }

            var exchanged = compareExchangeAppliedSequence(pointerUpSequence, appliedSequence);
            if (exchanged == appliedSequence)
            {
                return new CrossPagePostInputRefreshSlotAcquireResult(
                    Acquired: true,
                    PointerUpSequence: pointerUpSequence);
            }
        }
    }
}
