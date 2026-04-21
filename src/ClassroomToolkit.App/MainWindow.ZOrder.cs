using System;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class MainWindow
{
    // ── Z-order policy ──

    private void TouchSurface(ZOrderSurface surface, bool applyPolicy = true)
    {
        var changed = _windowOrchestrator.TouchSurface(_surfaceStack, surface);
        FloatingZOrderApplyExecutor.ApplyTouchResult(
            applyPolicy,
            changed,
            RequestApplyZOrderPolicy);
    }

    private void ApplySurfaceZOrderDecision(SurfaceZOrderDecision decision)
    {
        var interactionState = CaptureOverlayInteractionState();
        var dedupIntervalMs = MainWindowZOrderDedupIntervalPolicy.ResolveSurfaceDecisionIntervalMs(interactionState);
        var dedupDecision = SurfaceZOrderDecisionDedupPolicy.Resolve(
            decision,
            _surfaceZOrderDecisionState,
            GetCurrentUtcTimestamp(),
            minIntervalMs: dedupIntervalMs);
        SurfaceZOrderDecisionStateUpdater.Apply(
            ref _surfaceZOrderDecisionState,
            dedupDecision);
        if (!dedupDecision.ShouldApply)
        {
            System.Diagnostics.Debug.WriteLine(
                SurfaceZOrderDecisionDiagnosticsPolicy.FormatDedupSkipMessage(
                    dedupDecision.Reason));
            return;
        }

        SurfaceZOrderCoordinator.Apply(
            decision,
            surface => _windowOrchestrator.TouchSurface(_surfaceStack, surface),
            RequestApplyZOrderPolicy);
    }

    private void RequestApplyZOrderPolicy(bool forceEnforceZOrder = false)
    {
        if (FloatingTopmostDialogSuppressionState.IsSuppressed)
        {
            System.Diagnostics.Debug.WriteLine(
                "RequestApplyZOrderPolicy skipped: dialog-topmost-suppressed");
            return;
        }

        var nowUtc = GetCurrentUtcTimestamp();
        var previousRequestState = _zOrderRequestState;
        var interactionState = CaptureOverlayInteractionState();
        var dedupIntervalMs = MainWindowZOrderDedupIntervalPolicy.ResolveRequestIntervalMs(interactionState);
        var admission = ZOrderRequestAdmissionPolicy.Resolve(
            _zOrderPolicyApplying,
            _floatingDispatchQueueState.ApplyQueued,
            _zOrderRequestState,
            nowUtc,
            forceEnforceZOrder,
            dedupIntervalMs: dedupIntervalMs);
        ZOrderRequestStateUpdater.Apply(
            ref _zOrderRequestState,
            admission);
        if (!admission.ShouldQueue)
        {
            System.Diagnostics.Debug.WriteLine(ZOrderRequestDiagnosticsPolicy.FormatSkipMessage(
                admission.Reason,
                forceEnforceZOrder,
                _floatingDispatchQueueState.ApplyQueued,
                _zOrderPolicyApplying));
            return;
        }

        if (ZOrderRequestQueuedDiagnosticsAdmissionPolicy.ShouldLog(admission.Reason))
        {
            System.Diagnostics.Debug.WriteLine(ZOrderRequestDiagnosticsPolicy.FormatQueuedMessage(
                admission.Reason,
                forceEnforceZOrder));
        }

        var queueDispatchFailed = false;
        FloatingDispatchQueueStateUpdater.ApplyRequest(
            ref _floatingDispatchQueueState,
            forceEnforceZOrder,
            () => TryBeginInvoke(ExecuteQueuedApplyZOrderPolicy, DispatcherPriority.Background, "ExecuteQueuedApplyZOrderPolicy"),
            decision =>
            {
                var handlingPlan = ZOrderQueueDispatchDecisionHandlingPolicy.Resolve(decision.Reason);
                if (handlingPlan.ShouldLogDecision)
                {
                    System.Diagnostics.Debug.WriteLine(
                        FloatingDispatchQueueDiagnosticsPolicy.FormatRequestDecisionMessage(
                            decision.Action,
                            decision.Reason,
                            forceEnforceZOrder));
                }

                if (handlingPlan.ShouldMarkQueueDispatchFailed)
                {
                    queueDispatchFailed = true;
                }
            },
            ex => System.Diagnostics.Debug.WriteLine(
                FloatingDispatchQueueDiagnosticsPolicy.FormatQueueDispatchFailureExceptionMessage(
                    ex.GetType().Name,
                    ex.Message)));
        ZOrderQueueDispatchFailureRollbackStateUpdater.Apply(
            ref _zOrderRequestState,
            queueDispatchFailed,
            previousRequestState);
        if (queueDispatchFailed)
        {
            System.Diagnostics.Debug.WriteLine(
                ZOrderRequestDiagnosticsPolicy.FormatQueueDispatchFailedRollbackMessage(forceEnforceZOrder));
        }
    }

    private void ExecuteQueuedApplyZOrderPolicy()
    {
        var admission = FloatingDispatchExecuteAdmissionPolicy.Resolve(_floatingDispatchQueueState.ApplyQueued);
        if (!admission.ShouldExecute)
        {
            System.Diagnostics.Debug.WriteLine(
                FloatingDispatchExecuteDiagnosticsPolicy.FormatSkipMessage(admission.Reason));
            return;
        }

        FloatingDispatchQueueStateUpdater.ApplyExecuteQueued(
            ref _floatingDispatchQueueState,
            ApplyZOrderPolicy,
            ex => System.Diagnostics.Debug.WriteLine(
                FloatingDispatchExecuteDiagnosticsPolicy.FormatFailureMessage(
                    ex.GetType().Name,
                    ex.Message)));
    }

    private bool TryBeginInvoke(Action action, DispatcherPriority priority, string operation)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            System.Diagnostics.Debug.WriteLine(
                DispatcherBeginInvokeDiagnosticsPolicy.FormatFailureMessage(
                    operation,
                    "DispatcherShutdown",
                    "dispatcher is shutting down"));
            return false;
        }

        try
        {
            Dispatcher.BeginInvoke(action, priority);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                DispatcherBeginInvokeDiagnosticsPolicy.FormatFailureMessage(
                    operation,
                    ex.GetType().Name,
                    ex.Message));
            return false;
        }
    }

    private static DateTime GetCurrentUtcTimestamp() => DateTime.UtcNow;

    private MainWindowOverlayInteractionState CaptureOverlayInteractionState()
    {
        return MainWindowOverlayInteractionStatePolicy.Resolve(
            _overlayWindow?.IsVisible == true,
            _overlayWindow?.IsPhotoModeActive == true,
            _toolbarWindow?.BoardActive == true);
    }

    private FloatingUtilityActivitySnapshot CaptureFloatingUtilityActivity()
    {
        return CaptureFloatingUtilityActivity(CaptureLauncherWindowRuntimeSnapshot());
    }

    private FloatingUtilityActivitySnapshot CaptureFloatingUtilityActivity(
        LauncherWindowRuntimeSnapshot launcherSnapshot)
    {
        return FloatingUtilityActivitySnapshotPolicy.Resolve(
            toolbarActive: _toolbarWindow?.IsActive == true,
            rollCallActive: _rollCallWindow?.IsActive == true,
            imageManagerActive: _imageManagerWindow?.IsActive == true,
            launcherActive: launcherSnapshot.Active);
    }

    private void ApplyZOrderPolicy(bool forceEnforceZOrder = false)
    {
        if (!ZOrderApplyGuardStateUpdater.TryEnter(ref _zOrderPolicyApplying))
        {
            return;
        }

        try
        {
            var coordination = CaptureFloatingWindowCoordinationSnapshot();
            var launcherWindow = ResolveLauncherWindow(coordination.Launcher);
            var suppressionDecision = OverlayActivationSuppressionPolicy.Resolve(
                _overlayActivatedRetouchState.SuppressNextApply);
            if (suppressionDecision.ShouldSuppress)
            {
                System.Diagnostics.Debug.WriteLine(
                    OverlayActivationDiagnosticsPolicy.FormatSuppressionMessage(
                        suppressionDecision.Reason));
            }

            var state = FloatingWindowCoordinator.Apply(
                _windowOrchestrator,
                _surfaceStack,
                coordination,
                new FloatingWindowCoordinationState(
                    LastFrontSurface: _floatingCoordinationState.LastFrontSurface,
                    LastTopmostPlan: _floatingCoordinationState.LastTopmostPlan),
                forceEnforceZOrder,
                suppressionDecision.ShouldSuppress,
                _overlayWindow,
                _toolbarWindow,
                _rollCallWindow,
                launcherWindow,
                coordination.Runtime.ImageManagerVisible ? _imageManagerWindow : null);
            SessionTransitionDecisionStateUpdater.Apply(
                ref _floatingCoordinationState,
                state);
            EnsureCriticalFloatingWindowsTopmost(
                launcherWindow,
                enforceZOrder: forceEnforceZOrder);
        }
        finally
        {
            ZOrderApplyGuardStateUpdater.Exit(ref _zOrderPolicyApplying);
        }
    }

    private FloatingWindowRuntimeSnapshot CaptureFloatingWindowRuntimeSnapshot(
        LauncherWindowRuntimeSnapshot launcherSnapshot)
    {
        return FloatingWindowRuntimeSnapshotPolicy.Resolve(
            overlayVisible: _overlayWindow?.IsVisible == true,
            overlayActive: _overlayWindow?.IsActive == true,
            photoActive: _overlayWindow?.IsPhotoModeActive == true,
            presentationFullscreen: _overlayWindow?.IsPresentationFullscreenActive == true,
            whiteboardActive: _toolbarWindow?.BoardActive == true,
            imageManagerVisible: _imageManagerWindow?.IsVisible == true,
            imageManagerMinimized: _imageManagerWindow?.WindowState == WindowState.Minimized,
            launcherVisible: launcherSnapshot.VisibleForTopmost);
    }

    private FloatingWindowCoordinationSnapshot CaptureFloatingWindowCoordinationSnapshot()
    {
        var launcherSnapshot = CaptureLauncherWindowRuntimeSnapshot();
        var runtimeSnapshot = CaptureFloatingWindowRuntimeSnapshot(launcherSnapshot);
        var overlay = _overlayWindow;
        return FloatingWindowCoordinationSnapshotPolicy.Resolve(
            runtimeSnapshot,
            launcherSnapshot,
            toolbarVisible: _toolbarWindow?.IsVisible == true,
            rollCallVisible: _rollCallWindow?.IsVisible == true,
            toolbarActive: _toolbarWindow?.IsActive == true,
            rollCallActive: _rollCallWindow?.IsActive == true,
            imageManagerActive: _imageManagerWindow?.IsActive == true,
            launcherActive: launcherSnapshot.Active,
            toolbarOwnerAlreadyOverlay: _toolbarWindow?.Owner == overlay && overlay != null,
            rollCallOwnerAlreadyOverlay: _rollCallWindow?.Owner == overlay && overlay != null,
            imageManagerOwnerAlreadyOverlay: _imageManagerWindow?.Owner == overlay && overlay != null);
    }

    private LauncherWindowRuntimeSnapshot CaptureLauncherWindowRuntimeSnapshot()
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(
            launcherMinimized: _settings.LauncherMinimized,
            mainVisible: IsVisible,
            mainMinimized: WindowState == WindowState.Minimized,
            mainActive: IsActive,
            bubbleVisible: _bubbleWindow?.IsVisible == true,
            bubbleMinimized: _bubbleWindow?.WindowState == WindowState.Minimized,
            bubbleActive: _bubbleWindow?.IsActive == true);
        LauncherTopmostVisibilityStateUpdater.ApplyResolvedTimestamp(
            ref _lastLauncherVisibleForTopmostUtc,
            nowUtc,
            snapshot.VisibleForTopmost);
        if (LauncherWindowRuntimeSelectionLogPolicy.ShouldLog(snapshot.SelectionReason))
        {
            System.Diagnostics.Debug.WriteLine(
                LauncherWindowRuntimeDiagnosticsPolicy.FormatSelectionMessage(snapshot.SelectionReason));
        }

        return snapshot;
    }

    private Window? ResolveLauncherWindow(LauncherWindowRuntimeSnapshot launcherSnapshot)
    {
        var resolvedKind = LauncherWindowResolverPolicy.Resolve(
            launcherSnapshot.WindowKind,
            bubbleExists: _bubbleWindow != null,
            bubbleVisible: _bubbleWindow?.IsVisible == true,
            mainVisible: IsVisible);

        return LauncherWindowResolutionPolicy.ShouldUseBubbleWindow(
            resolvedKind,
            bubbleWindowExists: _bubbleWindow != null)
            ? _bubbleWindow
            : this;
    }

    private void EnsureCriticalFloatingWindowsTopmost(Window? launcherWindow, bool enforceZOrder)
    {
        var toolbarVisible = _toolbarWindow?.IsVisible == true;
        var rollCallVisible = _rollCallWindow?.IsVisible == true;
        var launcherVisible = launcherWindow?.IsVisible == true;
        var imageManagerVisible = _imageManagerWindow?.IsVisible == true;
        var rollCallAuxOverlayVisible = _rollCallWindow?.HasVisibleAuxOverlay() == true;
        var strictEnforceZOrder = enforceZOrder || FloatingTopmostWatchdogPolicy.ShouldForceRetouch(
            toolbarVisible,
            rollCallVisible,
            launcherVisible,
            imageManagerVisible,
            rollCallAuxOverlayVisible,
            photoModeActive: _overlayWindow?.IsPhotoModeActive == true);

        WindowTopmostExecutor.ApplyNoActivate(_toolbarWindow, toolbarVisible, strictEnforceZOrder);
        WindowTopmostExecutor.ApplyNoActivate(_rollCallWindow, rollCallVisible, strictEnforceZOrder);
        WindowTopmostExecutor.ApplyNoActivate(launcherWindow, launcherVisible, strictEnforceZOrder);
        _rollCallWindow?.RetouchAuxOverlayWindowsTopmost(strictEnforceZOrder);
    }

    private void SyncOverlayOwnedWindow(Window? child)
    {
        var overlay = _overlayWindow;
        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: child != null,
            overlayVisible: overlay?.IsVisible == true,
            ownerAlreadyOverlay: child?.Owner == overlay && overlay != null);
        FloatingSingleOwnerExecutionExecutor.Apply(action, child, overlay);
    }

    private void DetachOverlayOwnedWindow(Window? child)
    {
        var overlay = _overlayWindow;
        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: child != null,
            overlayVisible: false,
            ownerAlreadyOverlay: child?.Owner == overlay && overlay != null);
        FloatingSingleOwnerExecutionExecutor.Apply(action, child, overlay);
    }

    private void SyncFloatingWindowOwners(bool overlayVisible)
    {
        var overlay = _overlayWindow;
        var snapshot = FloatingOwnerRuntimeSnapshotPolicy.Resolve(
            overlayVisible: overlayVisible,
            toolbarOwnerAlreadyOverlay: _toolbarWindow?.Owner == overlay && overlay != null,
            rollCallOwnerAlreadyOverlay: _rollCallWindow?.Owner == overlay && overlay != null,
            imageManagerOwnerAlreadyOverlay: _imageManagerWindow?.Owner == overlay && overlay != null);
        var plan = FloatingOwnerExecutionPlanPolicy.Resolve(snapshot);
        FloatingOwnerExecutionExecutor.Apply(
            plan,
            overlay,
            _toolbarWindow,
            _rollCallWindow,
            _imageManagerWindow);
    }
}
