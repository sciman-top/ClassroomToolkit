using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void RequestCrossPageDisplayUpdate(string source = CrossPageUpdateSources.Unspecified)
    {
        if (Volatile.Read(ref _overlayClosed) != 0)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, "overlay-closed");
            return;
        }

        var admissionDecision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: IsCrossPageDisplayActive(),
            photoLoading: _photoLoading,
            hasPhotoBackgroundSource: PhotoBackground.Source != null,
            overlayVisible: IsVisible,
            overlayMinimized: WindowState == WindowState.Minimized,
            hasUsableViewport: OverlayRoot.ActualWidth > 0 && OverlayRoot.ActualHeight > 0);
        if (!admissionDecision.ShouldAdmit)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent(
                "skip",
                source,
                CrossPageRequestAdmissionReasonPolicy.ResolveDiagnosticTag(admissionDecision.Reason));
            return;
        }

        var request = CrossPageUpdateRequestContextFactory.Create(source);
        source = request.Source;
        var nowUtc = GetCurrentUtcTimestamp();
        var duplicateDecision = CrossPageDuplicateWindowPolicy.Resolve(
                request,
                _crossPageUpdateRequestState,
                nowUtc);
        if (duplicateDecision.ShouldSkip)
        {
            var replayQueueDecision = CrossPageDuplicateSkipReplayQueuePolicy.Resolve(
                duplicateDecision,
                request.Kind,
                source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                replayQueueDecision);
            var reason = CrossPageDuplicateWindowReasonPolicy.ResolveDiagnosticTag(duplicateDecision.Reason);
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, reason);
            return;
        }

        CrossPageUpdateRequestStateUpdater.ApplyAcceptedRequest(
            ref _crossPageUpdateRequestState,
            request,
            nowUtc);
        var dispatchSnapshot = CrossPageDisplayUpdateDispatchSnapshotPolicy.Resolve(
            pending: _crossPageDisplayUpdateState.Pending,
            panning: _photoPanning || _photoManipulating,
            dragging: _crossPageDragging,
            inkOperationActive: IsInkOperationActive());
        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "request",
            source,
            CrossPageDisplayUpdateDispatchSnapshot.FormatDiagnosticsTag(dispatchSnapshot));
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("crosspage-update-enter");
        }
        var elapsedMs = (nowUtc - _crossPageDisplayUpdateClockState.LastUpdateUtc).TotalMilliseconds;
        var dispatchDecision = CrossPageDisplayUpdateDispatchDecisionPolicy.Resolve(
            dispatchSnapshot,
            elapsedMs,
            draggingMinIntervalMs: CrossPageRuntimeDefaults.DraggingUpdateMinIntervalMs,
            normalMinIntervalMs: CrossPageRuntimeDefaults.UpdateMinIntervalMs);
        var sourceSuffix = CrossPageUpdateSourceParser.Parse(source).Suffix;
        dispatchDecision = CrossPageImmediateDispatchPolicy.Resolve(dispatchDecision, sourceSuffix);
        dispatchDecision = CrossPagePendingTakeoverPolicy.Resolve(
            dispatchDecision,
            sourceSuffix,
            _crossPageDisplayUpdateState,
            nowUtc);
        if (dispatchDecision.Mode == CrossPageDisplayUpdateDispatchMode.SkipPending)
        {
            var replayQueueDecision = CrossPageReplayQueuePolicy.Resolve(request.Kind, source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                replayQueueDecision);
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, "pending");
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("crosspage-update-skip", "pending");
            }
            return;
        }
        if (dispatchDecision.Mode == CrossPageDisplayUpdateDispatchMode.Delayed)
        {
            var token = CrossPageDisplayUpdatePendingStateUpdater.MarkDelayedScheduled(
                ref _crossPageDisplayUpdateState,
                nowUtc);
            var delay = dispatchDecision.DelayMs;
            var lifecycleToken = _overlayLifecycleCancellation.Token;
            _ = SafeTaskRunner.Run(
                "PaintOverlayWindow.CrossPageDisplayUpdate.Delayed",
                async cancellationToken =>
                {
                    var delayOutcome = await TryAwaitCrossPageDelayedDispatchAsync(
                        delay,
                        cancellationToken).ConfigureAwait(false);
                    if (!delayOutcome.ShouldContinue)
                    {
                        if (string.IsNullOrWhiteSpace(delayOutcome.FailureDetail))
                        {
                            return;
                        }

                        var detail = delayOutcome.FailureDetail;
                        var scheduledRecovery = TryBeginInvoke(() =>
                        {
                            RecoverCrossPageDelayedDispatchFailure(
                                request.Kind,
                                source,
                                token,
                                detail);
                        }, DispatcherPriority.Background);
                        var recoveryDecision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
                            recoveryDispatchScheduled: scheduledRecovery,
                            dispatcherCheckAccess: Dispatcher.CheckAccess(),
                            dispatcherShutdownStarted: Dispatcher.HasShutdownStarted,
                            dispatcherShutdownFinished: Dispatcher.HasShutdownFinished);
                        if (recoveryDecision.ShouldRecoverInline)
                        {
                            var tokenMatched = CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                                _crossPageDisplayUpdateState,
                                token);
                            RecoverCrossPageDelayedDispatchFailure(
                                request.Kind,
                                source,
                                token,
                                CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatInlineRecoveryDetail(
                                    tokenMatched));
                        }
                        return;
                    }

                    var scheduled = TryBeginInvoke(() =>
                    {
                        if (!CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                                _crossPageDisplayUpdateState,
                                token))
                        {
                            return;
                        }
                        CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
                        ExecuteCrossPageDisplayUpdateRun(
                            source,
                            mode: "delayed",
                            emitAbortDiagnostics: false);
                    }, DispatcherPriority.Render);
                    if (!scheduled)
                    {
                        HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
                            request.Kind,
                            source,
                            mode: "delayed",
                            emitAbortDiagnostics: false,
                            abortDetail: "delayed-dispatch-unavailable");
                    }
                },
                lifecycleToken,
                onError: ex =>
                {
                    HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
                        request.Kind,
                        source,
                        mode: "delayed-task-fault",
                        emitAbortDiagnostics: false,
                        abortDetail: "delayed-task-fault-dispatch-unavailable");
                    Debug.WriteLine(
                        $"[CrossPage] delayed-dispatch task fault ex={ex.GetType().Name} msg={ex.Message} source={source}");
                });
            return;
        }
        CrossPageDisplayUpdatePendingStateUpdater.MarkDirectScheduled(ref _crossPageDisplayUpdateState, nowUtc);
        var directScheduled = TryBeginInvoke(() =>
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            ExecuteCrossPageDisplayUpdateRun(
                source,
                mode: "direct",
                emitAbortDiagnostics: true);
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                request.Kind,
                source,
                mode: "direct",
                emitAbortDiagnostics: true);
        }
    }

    private static async Task<(bool ShouldContinue, string? FailureDetail)> TryAwaitCrossPageDelayedDispatchAsync(
        int delayMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return (ShouldContinue: true, FailureDetail: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (ShouldContinue: false, FailureDetail: null);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return (
                ShouldContinue: false,
                FailureDetail: CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatDelayFailureDetail(
                    ex.GetType().Name));
        }
    }

    private void RecoverCrossPageDelayedDispatchFailure(
        CrossPageUpdateSourceKind kind,
        string source,
        int token,
        string detail)
    {
        if (!CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                _crossPageDisplayUpdateState,
                token))
        {
            return;
        }

        CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
        var replayQueueDecision = CrossPageReplayQueuePolicy.Resolve(kind, source);
        CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
            ref _crossPageReplayState,
            replayQueueDecision);
        _inkDiagnostics?.OnCrossPageUpdateEvent("recover", source, detail);
    }

    private void HandleCrossPageDisplayUpdateDispatchFailure(
        CrossPageUpdateSourceKind kind,
        string source,
        string mode,
        bool emitAbortDiagnostics)
    {
        _ = CrossPageDisplayUpdateDispatchFailureCoordinator.Apply(
            ref _crossPageReplayState,
            kind,
            source,
            mode,
            emitAbortDiagnostics,
            executeCrossPageDisplayUpdateRun: ExecuteCrossPageDisplayUpdateRun,
            emitDiagnostics: (action, eventSource, detail) => _inkDiagnostics?.OnCrossPageUpdateEvent(action, eventSource, detail),
            flushCrossPageReplay: TryFlushCrossPageReplay,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished);
    }

    private void HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
        CrossPageUpdateSourceKind kind,
        string source,
        string mode,
        bool emitAbortDiagnostics,
        string abortDetail)
    {
        var scheduled = TryBeginInvoke(() =>
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                kind,
                source,
                mode,
                emitAbortDiagnostics);
        }, DispatcherPriority.Background);
        if (scheduled)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                kind,
                source,
                mode,
                emitAbortDiagnostics);
            return;
        }

        _inkDiagnostics?.OnCrossPageUpdateEvent("defer-abort", source, abortDetail);
    }

    private void ExecuteCrossPageDisplayUpdateRun(
        string source,
        string mode,
        bool emitAbortDiagnostics)
    {
        var runGate = CrossPageDisplayRunGatePolicy.Resolve(IsCrossPageDisplayActive());
        if (!runGate.ShouldRun)
        {
            if (emitAbortDiagnostics)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent(
                    "abort",
                    source,
                    runGate.AbortReason ?? CrossPageDeferredDiagnosticReason.Inactive);
            }
            return;
        }

        CrossPageDisplayUpdateClockStateUpdater.MarkUpdated(
            ref _crossPageDisplayUpdateClockState,
            GetCurrentUtcTimestamp());
        _inkDiagnostics?.OnCrossPageUpdateEvent("run", source, $"mode={mode}");
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("crosspage-update-run", mode);
        }
        _ = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                UpdateCrossPageDisplay();
                TryFlushCrossPageReplay();
                return true;
            },
            fallback: false,
            onFailure: ex =>
            {
                var replayQueueDecision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(source);
                CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                    ref _crossPageReplayState,
                    replayQueueDecision);
                _inkDiagnostics?.OnCrossPageUpdateEvent(
                    "recover",
                    source,
                    $"run-failed ex={ex.GetType().Name}");
            });
    }
}
