using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.App;
using System.Threading.Tasks;
using System.Windows.Threading;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDelayExecutionHelper
{
    internal static async Task<(bool Success, string? FailureDetail)> TryDelayAsync(
        int delayMs,
        Func<int, Task> delayAsync)
    {
        try
        {
            await delayAsync(delayMs).ConfigureAwait(false);
            return (Success: true, FailureDetail: null);
        }
        catch (Exception ex) when (AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            return (
                Success: false,
                FailureDetail: CrossPageReplayPolicies.FormatDelayFailureDetail(
                    ex.GetType().Name));
        }
    }
}

internal readonly record struct CrossPageDelayedDispatchFailureRecoveryDecision(
    bool ShouldRecoverInline);

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

        var source = CrossPageReplayPolicies.ResolveSource(target);
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

        var fallbackDecision = CrossPageReplayPolicies.ResolveDispatchScheduleFallback(
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

internal enum CrossPageReplayDispatchTarget
{
    None = 0,
    VisualSync = 1,
    Interaction = 2
}

internal enum CrossPageReplayDispatchScheduleFallbackReason
{
    None = 0,
    InlineCurrentThread = 1,
    RequeuePending = 2
}

internal readonly record struct CrossPageReplayDispatchScheduleFallbackDecision(
    bool ShouldRunInline,
    bool ShouldRequeuePending,
    CrossPageReplayDispatchScheduleFallbackReason Reason);

internal readonly record struct CrossPageReplayFlushExecutionResult(
    bool ShouldFlush,
    bool HasDispatchTarget,
    CrossPageReplayDispatchTarget DispatchTarget);

internal static class CrossPageReplayFlushCoordinator
{
    internal static CrossPageReplayFlushExecutionResult Resolve(
        CrossPageReplayRuntimeState replayState,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        var replayPending = CrossPageReplayPendingStateUpdater.HasPending(replayState);
        var shouldFlush = CrossPageDisplayUpdatePolicies.ShouldFlushReplay(
            replayPending,
            crossPageUpdatePending,
            photoModeActive,
            crossPageDisplayEnabled,
            interactionActive);
        if (!shouldFlush)
        {
            return new CrossPageReplayFlushExecutionResult(
                ShouldFlush: false,
                HasDispatchTarget: false,
                DispatchTarget: CrossPageReplayDispatchTarget.None);
        }

        var target = CrossPageReplayPolicies.ResolveDispatch(
            replayState.VisualSyncReplayPending,
            replayState.InteractionReplayPending,
            replayState.LastDispatchTarget,
            replayState.PreferInteractionReplay);
        if (target == CrossPageReplayDispatchTarget.None)
        {
            return new CrossPageReplayFlushExecutionResult(
                ShouldFlush: true,
                HasDispatchTarget: false,
                DispatchTarget: CrossPageReplayDispatchTarget.None);
        }

        return new CrossPageReplayFlushExecutionResult(
            ShouldFlush: true,
            HasDispatchTarget: true,
            DispatchTarget: target);
    }
}

internal static class CrossPageReplayPendingStateUpdater
{
    internal static bool HasPending(CrossPageReplayRuntimeState state)
    {
        return state.VisualSyncReplayPending || state.InteractionReplayPending;
    }

    internal static void ApplyQueueDecision(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayQueueDecision decision)
    {
        state = state with
        {
            VisualSyncReplayPending = state.VisualSyncReplayPending || decision.QueueVisualSyncReplay,
            InteractionReplayPending = state.InteractionReplayPending || decision.QueueInteractionReplay,
            PreferInteractionReplay = state.PreferInteractionReplay
                || (decision.QueueVisualSyncReplay && decision.QueueInteractionReplay)
        };
    }

    internal static bool TryMarkDispatchScheduled(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayDispatchTarget target)
    {
        if (target == CrossPageReplayDispatchTarget.VisualSync)
        {
            state = state with
            {
                VisualSyncReplayPending = false,
                LastDispatchTarget = target
            };
            return true;
        }

        if (target == CrossPageReplayDispatchTarget.Interaction)
        {
            state = state with
            {
                InteractionReplayPending = false,
                PreferInteractionReplay = false,
                LastDispatchTarget = target
            };
            return true;
        }

        return false;
    }

    internal static void MarkDispatchFailed(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayDispatchTarget target)
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchFailure(target);
        ApplyQueueDecision(ref state, decision);
    }

    internal static void Reset(ref CrossPageReplayRuntimeState state)
    {
        state = CrossPageReplayRuntimeState.Default;
    }

    internal static void ApplyQueueDecision(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending,
        CrossPageReplayQueueDecision decision)
    {
        visualSyncReplayPending |= decision.QueueVisualSyncReplay;
        interactionReplayPending |= decision.QueueInteractionReplay;
    }

    internal static void MarkDispatchFailed(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending,
        CrossPageReplayDispatchTarget target)
    {
        var decision = CrossPageReplayPolicies.ResolveDispatchFailure(target);
        ApplyQueueDecision(
            ref visualSyncReplayPending,
            ref interactionReplayPending,
            decision);
    }

    internal static void Reset(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending)
    {
        visualSyncReplayPending = false;
        interactionReplayPending = false;
    }
}

internal static class CrossPageReplayQueueDecisionFactory
{
    internal static CrossPageReplayQueueDecision None()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: false,
            QueueInteractionReplay: false);
    }

    internal static CrossPageReplayQueueDecision VisualSync()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: false);
    }

    internal static CrossPageReplayQueueDecision Interaction()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: false,
            QueueInteractionReplay: true);
    }

    internal static CrossPageReplayQueueDecision VisualSyncAndInteraction()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: true);
    }
}

internal readonly record struct CrossPageReplayQueueDecision(
    bool QueueVisualSyncReplay,
    bool QueueInteractionReplay);

internal readonly record struct CrossPageReplayRuntimeState(
    bool VisualSyncReplayPending,
    bool InteractionReplayPending,
    bool PreferInteractionReplay,
    CrossPageReplayDispatchTarget LastDispatchTarget)
{
    internal static CrossPageReplayRuntimeState Default => new(
        VisualSyncReplayPending: false,
        InteractionReplayPending: false,
        PreferInteractionReplay: false,
        LastDispatchTarget: CrossPageReplayDispatchTarget.None);
}

/// <summary>
/// Serializes a background request stream while retaining the newest request.
/// A newer request invalidates the currently running work; when that work
/// completes, the newest request is admitted exactly once.
/// </summary>
internal sealed class LatestRequestCoordinator<TRequest>
{
    private readonly object _gate = new();
    private TRequest? _latestRequest;
    private long _generation;
    private long _activeGeneration;
    private bool _hasLatestRequest;
    private bool _inFlight;

    internal bool TryBegin(TRequest request, out LatestRequestTicket<TRequest> ticket)
    {
        lock (_gate)
        {
            _latestRequest = request;
            _hasLatestRequest = true;
            _generation++;
            if (_inFlight)
            {
                ticket = default;
                return false;
            }

            _inFlight = true;
            _activeGeneration = _generation;
            ticket = new LatestRequestTicket<TRequest>(_generation, request);
            return true;
        }
    }

    internal bool IsCurrent(LatestRequestTicket<TRequest> ticket)
    {
        lock (_gate)
        {
            return _hasLatestRequest && ticket.Generation == _generation;
        }
    }

    internal bool TryComplete(
        LatestRequestTicket<TRequest> ticket,
        out LatestRequestTicket<TRequest> nextTicket)
    {
        lock (_gate)
        {
            if (!_inFlight || _activeGeneration != ticket.Generation)
            {
                nextTicket = default;
                return false;
            }

            _inFlight = false;
            _activeGeneration = 0;
            if (!_hasLatestRequest || ticket.Generation == _generation)
            {
                nextTicket = default;
                return false;
            }

            _inFlight = true;
            _activeGeneration = _generation;
            nextTicket = new LatestRequestTicket<TRequest>(_generation, _latestRequest!);
            return true;
        }
    }

    internal void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _activeGeneration = 0;
            _latestRequest = default;
            _hasLatestRequest = false;
            _inFlight = false;
        }
    }
}

internal readonly record struct LatestRequestTicket<TRequest>(long Generation, TRequest Request);

internal static class CrossPageReplayPolicies
{
    internal static string FormatDelayFailureDetail(string exceptionType)
    {
        return $"delayed-delay-failed ex={exceptionType}";
    }

    internal static string FormatInlineRecoveryDetail(bool tokenMatched)
    {
        return tokenMatched
            ? "delayed-delay-failed-inline-recovered"
            : "delayed-delay-failed-inline-skip-token-mismatch";
    }

    internal static CrossPageDelayedDispatchFailureRecoveryDecision ResolveCrossPageDelayedDispatchFailureRecovery(
        bool recoveryDispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (recoveryDispatchScheduled)
        {
            return new CrossPageDelayedDispatchFailureRecoveryDecision(
                ShouldRecoverInline: false);
        }

        if (dispatcherShutdownStarted || dispatcherShutdownFinished)
        {
            return new CrossPageDelayedDispatchFailureRecoveryDecision(
                ShouldRecoverInline: false);
        }

        return new CrossPageDelayedDispatchFailureRecoveryDecision(
            ShouldRecoverInline: dispatcherCheckAccess);
    }

    internal static CrossPageReplayQueueDecision ResolveCrossPageDuplicateSkipReplayQueue(
        CrossPageDuplicateWindowDecision duplicateDecision,
        CrossPageUpdateSourceKind kind,
        string source)
    {
        if (!duplicateDecision.ShouldSkip)
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        if (duplicateDecision.Reason is not CrossPageDuplicateWindowSkipReason.VisualSync
            and not CrossPageDuplicateWindowSkipReason.Interaction)
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        return CrossPageReplayPolicies.ResolveQueue(kind, source);
    }

    internal static CrossPageDisplayUpdateDispatchDecision ResolveCrossPageImmediateDispatch(
        CrossPageDisplayUpdateDispatchDecision decision,
        CrossPageUpdateDispatchSuffix suffix)
    {
        if (suffix != CrossPageUpdateDispatchSuffix.Immediate)
        {
            return decision;
        }

        if (decision.Mode != CrossPageDisplayUpdateDispatchMode.Delayed)
        {
            return decision;
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: 0);
    }

    internal static CrossPageReplayQueueDecision ResolveDispatchFailure(CrossPageReplayDispatchTarget target)
    {
        return target switch
        {
            CrossPageReplayDispatchTarget.VisualSync => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageReplayDispatchTarget.Interaction => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }

    internal static CrossPageReplayDispatchTarget ResolveDispatch(
        bool visualSyncReplayPending,
        bool interactionReplayPending)
    {
        if (visualSyncReplayPending)
        {
            return CrossPageReplayDispatchTarget.VisualSync;
        }

        if (interactionReplayPending)
        {
            return CrossPageReplayDispatchTarget.Interaction;
        }

        return CrossPageReplayDispatchTarget.None;
    }

    internal static CrossPageReplayDispatchTarget ResolveDispatch(
        bool visualSyncReplayPending,
        bool interactionReplayPending,
        CrossPageReplayDispatchTarget lastDispatchedTarget,
        bool preferInteractionReplay)
    {
        if (!visualSyncReplayPending && !interactionReplayPending)
        {
            return CrossPageReplayDispatchTarget.None;
        }

        if (visualSyncReplayPending && interactionReplayPending)
        {
            if (preferInteractionReplay)
            {
                return CrossPageReplayDispatchTarget.Interaction;
            }

            return lastDispatchedTarget == CrossPageReplayDispatchTarget.VisualSync
                ? CrossPageReplayDispatchTarget.Interaction
                : CrossPageReplayDispatchTarget.VisualSync;
        }

        return ResolveDispatch(visualSyncReplayPending, interactionReplayPending);
    }

    internal static string? ResolveSource(CrossPageReplayDispatchTarget target)
    {
        return target switch
        {
            CrossPageReplayDispatchTarget.VisualSync => CrossPageUpdateSources.InkVisualSyncReplay,
            CrossPageReplayDispatchTarget.Interaction => CrossPageUpdateSources.InteractionReplay,
            _ => null
        };
    }

    internal static CrossPageReplayDispatchScheduleFallbackDecision ResolveDispatchScheduleFallback(
        bool dispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (dispatchScheduled)
        {
            return new CrossPageReplayDispatchScheduleFallbackDecision(
                ShouldRunInline: false,
                ShouldRequeuePending: false,
                Reason: CrossPageReplayDispatchScheduleFallbackReason.None);
        }

        if (dispatcherCheckAccess && !dispatcherShutdownStarted && !dispatcherShutdownFinished)
        {
            return new CrossPageReplayDispatchScheduleFallbackDecision(
                ShouldRunInline: true,
                ShouldRequeuePending: false,
                Reason: CrossPageReplayDispatchScheduleFallbackReason.InlineCurrentThread);
        }

        return new CrossPageReplayDispatchScheduleFallbackDecision(
            ShouldRunInline: false,
            ShouldRequeuePending: true,
            Reason: CrossPageReplayDispatchScheduleFallbackReason.RequeuePending);
    }

    internal static CrossPageReplayQueueDecision ResolveQueue(CrossPageUpdateSourceKind kind)
    {
        return ResolveQueue(kind, source: CrossPageUpdateSources.Unspecified);
    }

    internal static CrossPageReplayQueueDecision ResolveQueue(CrossPageUpdateSourceKind kind, string source)
    {
        if (!CrossPageDisplayUpdatePolicies.ShouldQueueReplay(kind))
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var immediateSuffix = parsed.Suffix == CrossPageUpdateDispatchSuffix.Immediate;

        return kind switch
        {
            CrossPageUpdateSourceKind.VisualSync when immediateSuffix
                => CrossPageReplayQueueDecisionFactory.VisualSyncAndInteraction(),
            CrossPageUpdateSourceKind.VisualSync
                => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageUpdateSourceKind.Interaction
                => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}
