using System;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class MainWindow
{
    // ── Z-order policy ──

    internal void RequestImmediateFloatingZOrderRetouch()
    {
        if (FloatingTopmostDialogSuppressionState.IsSuppressed)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            RequestApplyZOrderPolicy(forceEnforceZOrder: true);
            return;
        }

        try
        {
            ApplyZOrderPolicy(forceEnforceZOrder: true);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"RequestImmediateFloatingZOrderRetouch failed: {ex.GetType().Name} - {ex.Message}");
            RequestApplyZOrderPolicy(forceEnforceZOrder: true);
        }
    }

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

        var touchChanged = !decision.ShouldTouchSurface
            || _windowOrchestrator.TouchSurface(_surfaceStack, decision.Surface);
        if (decision.RequestZOrderApply
            && (touchChanged || decision.ForceEnforceZOrder))
        {
            RequestApplyZOrderPolicy(decision.ForceEnforceZOrder);
        }
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
        _zOrderRequestState = new ZOrderRequestRuntimeState(
            admission.LastRequestUtc,
            admission.LastForceEnforceZOrder);
        if (!admission.ShouldQueue)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ZOrderRequest] skip reason={admission.Reason} force={forceEnforceZOrder} queued={_floatingDispatchQueueState.ApplyQueued} applying={_zOrderPolicyApplying}");
            return;
        }

        if (admission.Reason == ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ZOrderRequest] queued reason={admission.Reason} force={forceEnforceZOrder}");
        }

        var queueDispatchFailed = false;
        _floatingDispatchQueueState = FloatingDispatchQueueExecutor.RequestApply(
            _floatingDispatchQueueState,
            forceEnforceZOrder,
            () => TryBeginInvoke(ExecuteQueuedApplyZOrderPolicy, DispatcherPriority.Background, "ExecuteQueuedApplyZOrderPolicy"),
            decision =>
            {
                if (decision.Reason is FloatingDispatchQueueReason.MergedIntoQueuedRequest
                    or FloatingDispatchQueueReason.QueueDispatchFailed)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FloatingDispatchQueue] action={decision.Action} reason={decision.Reason} force={forceEnforceZOrder}");
                }

                if (decision.Reason == FloatingDispatchQueueReason.QueueDispatchFailed)
                {
                    queueDispatchFailed = true;
                }
            },
            ex => System.Diagnostics.Debug.WriteLine(
                $"[FloatingDispatchQueue] dispatch-failed ex={ex.GetType().Name} msg={ex.Message}"));
        if (queueDispatchFailed)
        {
            _zOrderRequestState = previousRequestState;
            System.Diagnostics.Debug.WriteLine(
                $"[ZOrderRequest] rollback reason=queue-dispatch-failed force={forceEnforceZOrder}");
        }
    }

    private void ExecuteQueuedApplyZOrderPolicy()
    {
        if (!_floatingDispatchQueueState.ApplyQueued)
        {
            System.Diagnostics.Debug.WriteLine(
                "[FloatingDispatchQueue][Execute] skip reason=NotQueued");
            return;
        }

        _floatingDispatchQueueState = FloatingDispatchQueueExecutor.ExecuteQueuedApply(
            _floatingDispatchQueueState,
            ApplyZOrderPolicy,
            ex => System.Diagnostics.Debug.WriteLine(
                $"[FloatingDispatchQueue][Execute] failed ex={ex.GetType().Name} msg={ex.Message}"));
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
        return new FloatingUtilityActivitySnapshot(
            ToolbarActive: _toolbarWindow?.IsActive == true,
            RollCallActive: _rollCallWindow?.IsActive == true,
            ImageManagerActive: _imageManagerWindow?.IsActive == true,
            LauncherActive: launcherSnapshot.Active);
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
            _floatingCoordinationState = state;
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
        return new FloatingWindowCoordinationSnapshot(
            Runtime: runtimeSnapshot,
            Launcher: launcherSnapshot,
            TopmostVisibility: new FloatingTopmostVisibilitySnapshot(
                ToolbarVisible: _toolbarWindow?.IsVisible == true,
                RollCallVisible: _rollCallWindow?.IsVisible == true,
                LauncherVisible: runtimeSnapshot.LauncherVisible,
                ImageManagerVisible: runtimeSnapshot.ImageManagerVisible,
                OverlayVisible: runtimeSnapshot.OverlayVisible),
            UtilityActivity: CaptureFloatingUtilityActivity(launcherSnapshot),
            Owner: new FloatingOwnerRuntimeSnapshot(
                OverlayVisible: runtimeSnapshot.OverlayVisible,
                ToolbarOwnerAlreadyOverlay: _toolbarWindow?.Owner == overlay && overlay != null,
                RollCallOwnerAlreadyOverlay: _rollCallWindow?.Owner == overlay && overlay != null,
                ImageManagerOwnerAlreadyOverlay: _imageManagerWindow?.Owner == overlay && overlay != null));
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

        _rollCallWindow?.RetouchAuxOverlayWindowsTopmost(strictEnforceZOrder, ResolvePhotoOverlayZOrderAnchor());
        WindowTopmostExecutor.ApplyNoActivate(_toolbarWindow, toolbarVisible, strictEnforceZOrder);
        WindowTopmostExecutor.ApplyNoActivate(_rollCallWindow, rollCallVisible, strictEnforceZOrder);
        WindowTopmostExecutor.ApplyNoActivate(launcherWindow, launcherVisible, strictEnforceZOrder);
    }

    internal Window? ResolvePhotoOverlayZOrderAnchor()
    {
        // Match the final critical-window retouch order: toolbar is the lowest
        // critical floating control, so inserting the photo after it keeps the
        // photo below toolbar/roll-call/launcher from its first topmost apply.
        if (IsVisibleZOrderAnchor(_toolbarWindow))
        {
            return _toolbarWindow;
        }

        if (IsVisibleZOrderAnchor(_rollCallWindow))
        {
            return _rollCallWindow;
        }

        var launcherWindow = ResolveLauncherWindow(CaptureLauncherWindowRuntimeSnapshot());
        return IsVisibleZOrderAnchor(launcherWindow) ? launcherWindow : null;
    }

    private static bool IsVisibleZOrderAnchor(Window? window)
    {
        return window?.IsVisible == true
            && window.WindowState != WindowState.Minimized;
    }

    private void SyncOverlayOwnedWindow(Window? child)
    {
        var overlay = _overlayWindow;
        var action = child == null
            ? FloatingOwnerBindingAction.None
            : FloatingOwnerBindingPolicy.Resolve(
                overlayVisible: overlay?.IsVisible == true,
                ownerAlreadyOverlay: child.Owner == overlay && overlay != null);
        FloatingSingleOwnerExecutionExecutor.Apply(action, child, overlay);
    }

    private void DetachOverlayOwnedWindow(Window? child)
    {
        var overlay = _overlayWindow;
        var action = child == null
            ? FloatingOwnerBindingAction.None
            : FloatingOwnerBindingPolicy.Resolve(
                overlayVisible: false,
                ownerAlreadyOverlay: child.Owner == overlay && overlay != null);
        FloatingSingleOwnerExecutionExecutor.Apply(action, child, overlay);
    }

    private void SyncFloatingWindowOwners(bool overlayVisible)
    {
        var overlay = _overlayWindow;
        var snapshot = new FloatingOwnerRuntimeSnapshot(
            OverlayVisible: overlayVisible,
            ToolbarOwnerAlreadyOverlay: _toolbarWindow?.Owner == overlay && overlay != null,
            RollCallOwnerAlreadyOverlay: _rollCallWindow?.Owner == overlay && overlay != null,
            ImageManagerOwnerAlreadyOverlay: _imageManagerWindow?.Owner == overlay && overlay != null);
        var plan = FloatingOwnerExecutionPlanPolicy.Resolve(snapshot);
        FloatingOwnerExecutionExecutor.Apply(
            plan,
            overlay,
            _toolbarWindow,
            _rollCallWindow,
            _imageManagerWindow);
    }
}
