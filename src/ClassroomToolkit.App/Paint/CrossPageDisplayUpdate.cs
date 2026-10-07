using System.Threading;
using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayClearPolicy
{
    internal static bool ShouldClearNeighborPages(
        int totalPages,
        bool hasCurrentBitmap,
        double currentPageHeight)
    {
        if (totalPages <= 1)
        {
            return true;
        }

        if (!hasCurrentBitmap)
        {
            return true;
        }

        return currentPageHeight <= 0;
    }
}

internal readonly record struct CrossPageDisplayToggleFlagUpdateDecision(
    bool ShouldApply,
    bool NextCrossPageDisplayEnabled);

internal static class CrossPageDisplayToggleFlagUpdatePolicy
{
    internal static CrossPageDisplayToggleFlagUpdateDecision Resolve(
        bool currentCrossPageDisplayEnabled,
        bool requestedEnabled)
    {
        var unchanged = currentCrossPageDisplayEnabled == requestedEnabled;
        if (unchanged)
        {
            return new CrossPageDisplayToggleFlagUpdateDecision(
                ShouldApply: false,
                NextCrossPageDisplayEnabled: currentCrossPageDisplayEnabled);
        }

        return new CrossPageDisplayToggleFlagUpdateDecision(
            ShouldApply: true,
            NextCrossPageDisplayEnabled: requestedEnabled);
    }
}

internal readonly record struct CrossPageDisplayToggleRuntimePlan(
    bool ShouldRestoreUnifiedTransformAndRedraw,
    bool ShouldSaveUnifiedTransformState,
    bool ShouldResetReplayAndClearNeighbors,
    bool ShouldRefreshImageSequenceSource,
    bool ShouldReloadPdfInkCache);

internal static class CrossPageDisplayToggleRuntimePlanPolicy
{
    internal static CrossPageDisplayToggleRuntimePlan Resolve(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled,
        bool photoDocumentIsPdf,
        bool photoUnifiedTransformReady)
    {
        return new CrossPageDisplayToggleRuntimePlan(
            ShouldRestoreUnifiedTransformAndRedraw: photoInkModeActive && crossPageDisplayEnabled && photoUnifiedTransformReady,
            ShouldSaveUnifiedTransformState: photoInkModeActive && crossPageDisplayEnabled && !photoUnifiedTransformReady,
            ShouldResetReplayAndClearNeighbors: !crossPageDisplayEnabled,
            ShouldRefreshImageSequenceSource: photoInkModeActive && !photoDocumentIsPdf,
            ShouldReloadPdfInkCache: photoInkModeActive && photoDocumentIsPdf);
    }
}

internal readonly record struct CrossPageDisplayToggleTransitionExecutionResult(
    bool AppliedFlagUpdate,
    bool ResetNormalizedWidth,
    bool RestoredUnifiedTransformAndRedraw,
    bool SavedUnifiedTransformState,
    bool ResetReplayAndClearedNeighbors,
    bool RefreshedImageSequenceSource,
    bool ReloadedPdfInkCache);

internal static class CrossPageDisplayToggleTransitionCoordinator
{
    internal static CrossPageDisplayToggleTransitionExecutionResult Apply(
        bool currentCrossPageDisplayEnabled,
        bool requestedEnabled,
        bool photoInkModeActive,
        bool photoDocumentIsPdf,
        bool photoUnifiedTransformReady,
        Action<bool> setCrossPageDisplayEnabled,
        Action resetCrossPageNormalizedWidth,
        Action restoreUnifiedTransformAndRedraw,
        Action saveUnifiedTransformState,
        Action updateCurrentPageWidthNormalization,
        Action resetCrossPageReplayState,
        Action clearNeighborPages,
        Action refreshCurrentImageSequenceSourceAfterToggle,
        Action reloadPdfInkCacheAfterToggle)
    {
        ArgumentNullException.ThrowIfNull(setCrossPageDisplayEnabled);
        ArgumentNullException.ThrowIfNull(resetCrossPageNormalizedWidth);
        ArgumentNullException.ThrowIfNull(restoreUnifiedTransformAndRedraw);
        ArgumentNullException.ThrowIfNull(saveUnifiedTransformState);
        ArgumentNullException.ThrowIfNull(updateCurrentPageWidthNormalization);
        ArgumentNullException.ThrowIfNull(resetCrossPageReplayState);
        ArgumentNullException.ThrowIfNull(clearNeighborPages);
        ArgumentNullException.ThrowIfNull(refreshCurrentImageSequenceSourceAfterToggle);
        ArgumentNullException.ThrowIfNull(reloadPdfInkCacheAfterToggle);

        var flagUpdate = CrossPageDisplayToggleFlagUpdatePolicy.Resolve(
            currentCrossPageDisplayEnabled,
            requestedEnabled);
        if (!flagUpdate.ShouldApply)
        {
            return default;
        }

        PaintActionInvoker.TryInvoke(() => setCrossPageDisplayEnabled(flagUpdate.NextCrossPageDisplayEnabled));

        var togglePlan = CrossPageDisplayToggleRuntimePlanPolicy.Resolve(
            photoInkModeActive: photoInkModeActive,
            crossPageDisplayEnabled: flagUpdate.NextCrossPageDisplayEnabled,
            photoDocumentIsPdf: photoDocumentIsPdf,
            photoUnifiedTransformReady: photoUnifiedTransformReady);

        PaintActionInvoker.TryInvoke(resetCrossPageNormalizedWidth);

        if (togglePlan.ShouldRestoreUnifiedTransformAndRedraw)
        {
            PaintActionInvoker.TryInvoke(restoreUnifiedTransformAndRedraw);
        }

        if (togglePlan.ShouldSaveUnifiedTransformState)
        {
            PaintActionInvoker.TryInvoke(saveUnifiedTransformState);
            PaintActionInvoker.TryInvoke(updateCurrentPageWidthNormalization);
        }

        if (togglePlan.ShouldResetReplayAndClearNeighbors)
        {
            PaintActionInvoker.TryInvoke(resetCrossPageReplayState);
            PaintActionInvoker.TryInvoke(clearNeighborPages);
            PaintActionInvoker.TryInvoke(updateCurrentPageWidthNormalization);
        }

        if (togglePlan.ShouldRefreshImageSequenceSource)
        {
            PaintActionInvoker.TryInvoke(refreshCurrentImageSequenceSourceAfterToggle);
        }

        if (togglePlan.ShouldReloadPdfInkCache)
        {
            PaintActionInvoker.TryInvoke(reloadPdfInkCacheAfterToggle);
        }

        return new CrossPageDisplayToggleTransitionExecutionResult(
            AppliedFlagUpdate: true,
            ResetNormalizedWidth: true,
            RestoredUnifiedTransformAndRedraw: togglePlan.ShouldRestoreUnifiedTransformAndRedraw,
            SavedUnifiedTransformState: togglePlan.ShouldSaveUnifiedTransformState,
            ResetReplayAndClearedNeighbors: togglePlan.ShouldResetReplayAndClearNeighbors,
            RefreshedImageSequenceSource: togglePlan.ShouldRefreshImageSequenceSource,
            ReloadedPdfInkCache: togglePlan.ShouldReloadPdfInkCache);
    }
}

internal readonly record struct CrossPageDisplayUpdateClockState(
    DateTime LastUpdateUtc)
{
    internal static CrossPageDisplayUpdateClockState Default => new(
        LastUpdateUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}

internal static class CrossPageDisplayUpdateClockStateUpdater
{
    internal static void MarkUpdated(
        ref CrossPageDisplayUpdateClockState state,
        DateTime nowUtc)
    {
        state = new CrossPageDisplayUpdateClockState(nowUtc);
    }
}

internal readonly record struct CrossPageDisplayUpdateDispatchSnapshot(
    bool Pending,
    bool Panning,
    bool Dragging,
    bool InkOperationActive)
{
    internal static string FormatDiagnosticsTag(CrossPageDisplayUpdateDispatchSnapshot snapshot)
    {
        return $"pending={snapshot.Pending} panning={snapshot.Panning} dragging={snapshot.Dragging}";
    }
}

internal readonly record struct CrossPageDisplayRunGateDecision(
    bool ShouldRun,
    string? AbortReason);

internal static class CrossPageDisplayRunGatePolicy
{
    internal static CrossPageDisplayRunGateDecision Resolve(bool crossPageDisplayActive)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageDisplayRunGateDecision(
                ShouldRun: false,
                AbortReason: CrossPageDeferredDiagnosticReason.Inactive);
        }

        return new CrossPageDisplayRunGateDecision(
            ShouldRun: true,
            AbortReason: null);
    }
}

internal static class CrossPageDisplayUpdateRunFailureReplayPolicy
{
    internal static CrossPageReplayQueueDecision Resolve(string source)
    {
        var context = CrossPageUpdateRequestContextFactory.Create(source);
        return context.Kind switch
        {
            CrossPageUpdateSourceKind.VisualSync => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageUpdateSourceKind.Interaction => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}

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

internal readonly record struct CrossPageDisplayUpdateRuntimeState(
    bool Pending,
    int Token,
    DateTime PendingSinceUtc)
{
    internal static CrossPageDisplayUpdateRuntimeState Default => new(
        Pending: false,
        Token: 0,
        PendingSinceUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}

internal static class CrossPageDisplayUpdatePendingStateUpdater
{
    internal static void MarkDirectScheduled(ref CrossPageDisplayUpdateRuntimeState state)
    {
        MarkDirectScheduled(ref state, DateTime.UtcNow);
    }

    internal static void MarkDirectScheduled(
        ref CrossPageDisplayUpdateRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            Pending = true,
            PendingSinceUtc = nowUtc
        };
    }

    internal static int MarkDelayedScheduled(ref CrossPageDisplayUpdateRuntimeState state)
    {
        return MarkDelayedScheduled(ref state, DateTime.UtcNow);
    }

    internal static int MarkDelayedScheduled(
        ref CrossPageDisplayUpdateRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            Pending = true,
            Token = state.Token + 1,
            PendingSinceUtc = nowUtc
        };
        return state.Token;
    }

    internal static void MarkPendingCleared(ref CrossPageDisplayUpdateRuntimeState state)
    {
        state = state with
        {
            Pending = false,
            PendingSinceUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc
        };
    }

    internal static bool IsTokenMatched(
        CrossPageDisplayUpdateRuntimeState state,
        int token)
    {
        return token == state.Token;
    }

    internal static void MarkDirectScheduled(ref bool pending)
    {
        pending = true;
    }

    internal static int MarkDelayedScheduled(ref bool pending, ref int token)
    {
        pending = true;
        return Interlocked.Increment(ref token);
    }

    internal static void MarkPendingCleared(ref bool pending)
    {
        pending = false;
    }
}

internal enum CrossPageDisplayUpdateDispatchMode
{
    SkipPending = 0,
    Delayed = 1,
    Direct = 2
}

internal readonly record struct CrossPageDisplayUpdateDispatchDecision(
    CrossPageDisplayUpdateDispatchMode Mode,
    int DelayMs);

internal static class CrossPageDisplayUpdateThrottleDefaults
{
    internal const int ImmediateDelayMs = 0;
    internal const int MinDelayedDispatchMs = 1;
}

internal static class CrossPageDisplayUpdateMinIntervalThresholds
{
    internal const int PanInkActiveMinMs = 24;
    internal const int PanOnlyMinMs = 20;
    internal const int InkOnlyMinMs = 16;
}

internal static class CrossPageDisplayUpdateMinIntervalPolicy
{
    internal static int ResolveMs(
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        if (photoPanning || crossPageDragging)
        {
            if (inkOperationActive)
            {
                return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs);
            }

            return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs);
        }

        if (inkOperationActive)
        {
            return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs);
        }

        return Math.Max(1, normalMinIntervalMs);
    }
}

internal static class CrossPageDisplayUpdateThrottlePolicy
{
    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchSnapshot snapshot,
        double elapsedMs,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        return Resolve(
            updatePending: snapshot.Pending,
            photoPanning: snapshot.Panning,
            crossPageDragging: snapshot.Dragging,
            inkOperationActive: snapshot.InkOperationActive,
            elapsedMs: elapsedMs,
            draggingMinIntervalMs: draggingMinIntervalMs,
            normalMinIntervalMs: normalMinIntervalMs);
    }

    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        bool updatePending,
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive,
        double elapsedMs,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        if (updatePending)
        {
            return new CrossPageDisplayUpdateDispatchDecision(
                CrossPageDisplayUpdateDispatchMode.SkipPending,
                DelayMs: CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
        }

        var throttleActive = CrossPageInteractionActivityPolicy.IsActive(
            photoPanning,
            crossPageDragging,
            inkOperationActive);
        var minIntervalMs = throttleActive
            ? CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
                photoPanning,
                crossPageDragging,
                inkOperationActive,
                draggingMinIntervalMs,
                normalMinIntervalMs)
            : normalMinIntervalMs;
        if (throttleActive && elapsedMs < minIntervalMs)
        {
            var delay = Math.Max(
                CrossPageDisplayUpdateThrottleDefaults.MinDelayedDispatchMs,
                (int)Math.Ceiling(minIntervalMs - elapsedMs));
            return new CrossPageDisplayUpdateDispatchDecision(
                CrossPageDisplayUpdateDispatchMode.Delayed,
                delay);
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
    }
}

internal static class CrossPagePdfVisiblePrefetchUpdatePolicy
{
    internal static bool ShouldRefreshCrossPageDisplay(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        if (!photoDocumentIsPdf)
        {
            return false;
        }

        return PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive,
            boardActive,
            crossPageDisplayEnabled);
    }
}

internal enum CrossPageRequestAdmissionReason
{
    None = 0,
    CrossPageInactive = 1,
    PhotoLoading = 2,
    BackgroundNotReady = 3,
    OverlayNotVisible = 4,
    OverlayMinimized = 5,
    ViewportUnavailable = 6
}

internal readonly record struct CrossPageRequestAdmissionDecision(
    bool ShouldAdmit,
    CrossPageRequestAdmissionReason Reason);

internal static class CrossPageRequestAdmissionPolicy
{
    internal static CrossPageRequestAdmissionDecision Resolve(
        bool crossPageDisplayActive,
        bool photoLoading,
        bool hasPhotoBackgroundSource,
        bool overlayVisible,
        bool overlayMinimized,
        bool hasUsableViewport)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.CrossPageInactive);
        }

        if (photoLoading)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.PhotoLoading);
        }

        if (!hasPhotoBackgroundSource)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.BackgroundNotReady);
        }

        if (!overlayVisible)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.OverlayNotVisible);
        }

        if (overlayMinimized)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.OverlayMinimized);
        }

        if (!hasUsableViewport)
        {
            return new CrossPageRequestAdmissionDecision(
                ShouldAdmit: false,
                Reason: CrossPageRequestAdmissionReason.ViewportUnavailable);
        }

        return new CrossPageRequestAdmissionDecision(
            ShouldAdmit: true,
            Reason: CrossPageRequestAdmissionReason.None);
    }

    internal static bool ShouldAdmit(
        bool crossPageDisplayActive,
        bool photoLoading,
        bool hasPhotoBackgroundSource,
        bool overlayVisible,
        bool overlayMinimized,
        bool hasUsableViewport)
    {
        return Resolve(
            crossPageDisplayActive,
            photoLoading,
            hasPhotoBackgroundSource,
            overlayVisible,
            overlayMinimized,
            hasUsableViewport).ShouldAdmit;
    }
}

internal static class CrossPageRuntimeDefaults
{
    internal const int PostInputRefreshDelayMs = 420;
    internal const int NeighborPagesClearGraceMs = 180;
    internal const int DraggingUpdateMinIntervalMs = 24;
    internal const int UpdateMinIntervalMs = 24;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}

internal static class CrossPageUpdateReplayPolicy
{
    internal static bool IsReplayBaseSource(string source)
    {
        return string.Equals(source, CrossPageUpdateSources.InkVisualSyncReplay, StringComparison.Ordinal)
            || string.Equals(source, CrossPageUpdateSources.InteractionReplay, StringComparison.Ordinal);
    }

    internal static bool ShouldQueueReplay(CrossPageUpdateSourceKind kind)
    {
        return kind is CrossPageUpdateSourceKind.VisualSync or CrossPageUpdateSourceKind.Interaction;
    }

    internal static bool ShouldFlushReplay(
        bool replayPending,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        return replayPending
            && !crossPageUpdatePending
            && photoModeActive
            && crossPageDisplayEnabled
            && !interactionActive;
    }
}

internal readonly record struct CrossPageUpdateRequestContext(
    string Source,
    string BaseSource,
    CrossPageUpdateSourceKind Kind);

internal static class CrossPageUpdateRequestContextFactory
{
    internal static CrossPageUpdateRequestContext Create(string? source)
    {
        var normalizedSource = CrossPageUpdateSources.Normalize(source);
        var parsed = CrossPageUpdateSourceParser.Parse(normalizedSource);
        var kind = CrossPageUpdateSourceClassifier.Classify(normalizedSource);
        return new CrossPageUpdateRequestContext(
            normalizedSource,
            parsed.BaseSource,
            kind);
    }
}

internal readonly record struct CrossPageUpdateRequestRuntimeState(
    CrossPageUpdateRequestContext? LastRequest,
    DateTime LastRequestUtc)
{
    internal static CrossPageUpdateRequestRuntimeState Default => new(
        LastRequest: null,
        LastRequestUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}

internal static class CrossPageUpdateRequestStateUpdater
{
    internal static void ApplyAcceptedRequest(
        ref CrossPageUpdateRequestRuntimeState state,
        CrossPageUpdateRequestContext request,
        DateTime nowUtc)
    {
        state = new CrossPageUpdateRequestRuntimeState(
            LastRequest: request,
            LastRequestUtc: nowUtc);
    }

    internal static void ApplyAcceptedRequest(
        ref CrossPageUpdateRequestContext? lastRequest,
        ref DateTime lastRequestUtc,
        CrossPageUpdateRequestContext request,
        DateTime nowUtc)
    {
        lastRequest = request;
        lastRequestUtc = nowUtc;
    }
}

internal enum CrossPageUpdateSourceKind
{
    Interaction = 0,
    VisualSync = 1,
    BackgroundRefresh = 2
}

internal static class CrossPageUpdateSourceClassifier
{
    internal static CrossPageUpdateSourceKind Classify(string source)
    {
        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var baseSource = parsed.BaseSource;
        if (IsVisualSyncSource(baseSource))
        {
            return CrossPageUpdateSourceKind.VisualSync;
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborPrefix, StringComparison.Ordinal))
        {
            return CrossPageUpdateSourceKind.BackgroundRefresh;
        }

        return CrossPageUpdateSourceKind.Interaction;
    }

    internal static bool IsVisualSyncSource(string source)
    {
        return source.StartsWith(CrossPageUpdateSources.InkStateChanged, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.InkRedrawCompleted, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.RegionEraseCrossPage, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.UndoSnapshot, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.InkShowPrefix, StringComparison.Ordinal);
    }
}

internal enum CrossPageUpdateDispatchSuffix
{
    None = 0,
    Immediate = 1,
    Delayed = 2
}

internal readonly record struct CrossPageUpdateSourceParseResult(
    string BaseSource,
    CrossPageUpdateDispatchSuffix Suffix);

internal static class CrossPageUpdateSourceParser
{
    internal static CrossPageUpdateSourceParseResult Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return new CrossPageUpdateSourceParseResult(
                CrossPageUpdateSources.Unspecified,
                CrossPageUpdateDispatchSuffix.None);
        }

        if (source.EndsWith(CrossPageUpdateSources.ImmediateSuffix, StringComparison.Ordinal))
        {
            var baseSource = source[..^CrossPageUpdateSources.ImmediateSuffix.Length];
            if (string.IsNullOrWhiteSpace(baseSource))
            {
                baseSource = CrossPageUpdateSources.Unspecified;
            }
            return new CrossPageUpdateSourceParseResult(
                baseSource,
                CrossPageUpdateDispatchSuffix.Immediate);
        }

        if (source.EndsWith(CrossPageUpdateSources.DelayedSuffix, StringComparison.Ordinal))
        {
            var baseSource = source[..^CrossPageUpdateSources.DelayedSuffix.Length];
            if (string.IsNullOrWhiteSpace(baseSource))
            {
                baseSource = CrossPageUpdateSources.Unspecified;
            }
            return new CrossPageUpdateSourceParseResult(
                baseSource,
                CrossPageUpdateDispatchSuffix.Delayed);
        }

        return new CrossPageUpdateSourceParseResult(
            source,
            CrossPageUpdateDispatchSuffix.None);
    }
}

internal static class CrossPageUpdateSources
{
    internal const string Unspecified = "unspecified";
    internal const string BoardExit = "board-exit";
    internal const string RegionEraseCrossPage = "region-erase-crosspage";
    internal const string InkStateChanged = "ink-state-changed";
    internal const string InkRedrawCompleted = "ink-redraw-completed";
    internal const string InkVisualSyncReplay = "ink-visual-sync-replay";
    internal const string InteractionReplay = "interaction-replay";
    internal const string ManipulationDelta = "manipulation-delta";
    internal const string NavigateInteractiveBrush = "navigate-interactive-brush";
    internal const string NavigateInteractive = "navigate-interactive";
    internal const string NavigateInteractiveFallback = "navigate-interactive-fallback";
    internal const string StepViewport = "step-viewport";
    internal const string ApplyScale = "apply-scale";
    internal const string PhotoPan = "photo-pan";
    internal const string FitWidth = "fit-width";
    internal const string UndoSnapshot = "undo-snapshot";
    internal const string InkShowDisabled = "ink-show-disabled";
    internal const string InkShowEnabled = "ink-show-enabled";
    internal const string InkShowPrefix = "ink-show-";
    internal const string NeighborMissingDelayed = "neighbor-missing-delayed";
    internal const string NeighborSidecar = "neighbor-sidecar";
    internal const string NeighborRender = "neighbor-render";
    internal const string NeighborMissing = "neighbor-missing";
    internal const string PostInput = "post-input";
    internal const string PointerUpFast = "pointer-up-fast";
    internal const string NeighborPrefix = "neighbor-";
    internal const string ImmediateSuffix = "-immediate";
    internal const string DelayedSuffix = "-delayed";

    internal static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Unspecified;
        }

        return source.Trim();
    }

    internal static string WithImmediate(string source)
    {
        var normalized = CrossPageUpdateSourceParser.Parse(source).BaseSource;
        return $"{normalized}{ImmediateSuffix}";
    }

    internal static string WithDelayed(string source)
    {
        var normalized = CrossPageUpdateSourceParser.Parse(source).BaseSource;
        return $"{normalized}{DelayedSuffix}";
    }
}
