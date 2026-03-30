using System;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

/// <summary>
/// Paint window management, overlay/toolbar lifecycle, and paint/ink settings.
/// </summary>
public partial class MainWindow
{
    private void OnPaintClick(object sender, RoutedEventArgs e)
    {
        EnsurePaintWindows();
        var overlay = _paintWindowOrchestrator.OverlayWindow;
        var toolbar = _paintWindowOrchestrator.ToolbarWindow;
        
        if (overlay == null || toolbar == null)
        {
            return;
        }
        
        var transitionPlan = PaintVisibilityTransitionPolicy.ResolvePaintToggle(overlay.IsVisible);
        ApplyPaintToggleTransition(transitionPlan);
        UpdateToggleButtons();
    }

    private void ApplyPaintToggleTransition(PaintVisibilityTransitionPlan transitionPlan)
    {
        if (transitionPlan.CaptureToolbarPosition)
        {
            _paintWindowOrchestrator.CaptureToolbarPosition(_settings, save: true);
        }
        if (transitionPlan.HideOverlay)
        {
            ExecuteLifecycleSafe("paint-toggle", "hide-paint-orchestrator", () => _paintWindowOrchestrator.Hide());
            ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.PaintHidden);
        }
        if (transitionPlan.ShowOverlay)
        {
            ExecuteLifecycleSafe("paint-toggle", "show-paint-orchestrator", () => _paintWindowOrchestrator.Show());
        }
        SyncFloatingWindowOwners(overlayVisible: transitionPlan.SyncFloatingOwnersVisible);
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void EnsurePaintWindows()
    {
        var overlayWindow = _paintWindowOrchestrator.OverlayWindow;
        var toolbarWindow = _paintWindowOrchestrator.ToolbarWindow;
        var shouldWireOverlayLifecycle = WindowLifecycleSubscriptionPolicy.ShouldWire(_lifecycleWiredOverlayWindow, overlayWindow);
        var shouldWireToolbarLifecycle = WindowLifecycleSubscriptionPolicy.ShouldWire(_lifecycleWiredToolbarWindow, toolbarWindow);

        if (PaintWindowEnsureSkipPolicy.ShouldSkip(
                hasOverlayWindow: overlayWindow != null,
                hasToolbarWindow: toolbarWindow != null,
                eventsWired: _paintOrchestratorEventsWired,
                shouldWireOverlayLifecycle: shouldWireOverlayLifecycle,
                shouldWireToolbarLifecycle: shouldWireToolbarLifecycle))
        {
            return;
        }

        if (PaintWindowCreationPolicy.ShouldEnsureWindows(
                hasOverlayWindow: overlayWindow != null,
                hasToolbarWindow: toolbarWindow != null))
        {
            _paintWindowOrchestrator.EnsureWindows(_settings);
        }
        WirePaintWindowOrchestrator();
        WirePaintWindowLifecycle();
    }

    private void WirePaintWindowOrchestrator()
    {
        if (_paintOrchestratorEventsWired)
        {
            return;
        }
        _paintWindowOrchestrator.PhotoModeChanged += OnPhotoModeChanged;
        _paintWindowOrchestrator.PhotoNavigationRequested += OnPhotoNavigateRequested;
        _paintWindowOrchestrator.PhotoUnifiedTransformChanged += OnPhotoUnifiedTransformChanged;
        _paintWindowOrchestrator.PresentationFullscreenDetected += OnPresentationFullscreenDetected;
        _paintWindowOrchestrator.PresentationForegroundDetected += OnPresentationForegroundDetected;
        _paintWindowOrchestrator.PhotoForegroundDetected += OnPhotoForegroundDetected;
        _paintWindowOrchestrator.PhotoCloseRequested += OnPhotoCloseRequested;
        _paintWindowOrchestrator.PhotoCursorModeFocusRequested += OnPhotoCursorModeFocusRequested;
        _paintWindowOrchestrator.FloatingZOrderRequested += OnFloatingZOrderRequested;
        _paintWindowOrchestrator.OverlayActivated += OnOverlayActivated;
        _paintWindowOrchestrator.SettingsRequested += OnOpenPaintSettings;
        _paintWindowOrchestrator.PhotoOpenRequested += OnOpenPhotoTeaching;
        _paintWindowOrchestrator.OverlaySessionTransitionOccurred += OnOverlaySessionTransitionOccurred;
        _paintOrchestratorEventsWired = true;
    }

    private void WirePaintWindowLifecycle()
    {
        var overlayWindow = _paintWindowOrchestrator.OverlayWindow;
        var overlayRewired = WindowLifecycleSubscriptionPolicy.ShouldWire(_lifecycleWiredOverlayWindow, overlayWindow);
        if (overlayRewired)
        {
            if (_lifecycleWiredOverlayWindow != null)
            {
                _lifecycleWiredOverlayWindow.Closed -= OnPaintOverlayWindowClosed;
            }
            overlayWindow!.Closed += OnPaintOverlayWindowClosed;
            _lifecycleWiredOverlayWindow = overlayWindow;
            var resetDecision = SessionTransitionDuplicateResetPolicy.Resolve(
                overlayWindowRewired: true,
                lastAppliedTransitionId: _lastAppliedSessionTransitionId);
            if (resetDecision.ShouldReset)
            {
                SessionTransitionDuplicateStateUpdater.Reset(ref _lastAppliedSessionTransitionId);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    SessionTransitionDiagnosticsPolicy.FormatDuplicateResetMessage(resetDecision.Reason));
            }
        }

        var toolbarWindow = _paintWindowOrchestrator.ToolbarWindow;
        if (WindowLifecycleSubscriptionPolicy.ShouldWire(_lifecycleWiredToolbarWindow, toolbarWindow))
        {
            if (_lifecycleWiredToolbarWindow != null)
            {
                _lifecycleWiredToolbarWindow.Closed -= OnPaintToolbarWindowClosed;
                _lifecycleWiredToolbarWindow.Activated -= OnPaintToolbarWindowActivated;
                _lifecycleWiredToolbarWindow.PreviewMouseDown -= OnPaintToolbarWindowPreviewMouseDown;
            }
            toolbarWindow!.Closed += OnPaintToolbarWindowClosed;
            toolbarWindow.Activated += OnPaintToolbarWindowActivated;
            toolbarWindow.PreviewMouseDown += OnPaintToolbarWindowPreviewMouseDown;
            _lifecycleWiredToolbarWindow = toolbarWindow;
        }
    }

    private void OnFloatingZOrderRequested(FloatingZOrderRequest request)
    {
        FloatingZOrderApplyExecutor.Apply(request, RequestApplyZOrderPolicy);
    }

    private void OnPaintOverlayWindowClosed(object? sender, EventArgs e)
    {
        ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.OverlayClosed);
        if (ReferenceEquals(sender, _lifecycleWiredOverlayWindow))
        {
            _lifecycleWiredOverlayWindow = null;
            SessionTransitionDuplicateStateUpdater.Reset(ref _lastAppliedSessionTransitionId);
        }
        UpdateToggleButtons();
    }

    private void OnPaintToolbarWindowClosed(object? sender, EventArgs e)
    {
        ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.ToolbarClosed);
        if (ReferenceEquals(sender, _lifecycleWiredToolbarWindow))
        {
            _lifecycleWiredToolbarWindow = null;
        }
        UpdateToggleButtons();
    }

    private void OnPaintToolbarWindowActivated(object? sender, EventArgs e)
    {
        RetouchFloatingTopmostOnToolbarInteraction(ToolbarInteractionRetouchTrigger.Activated);
    }

    private void OnPaintToolbarWindowPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        RetouchFloatingTopmostOnToolbarInteraction(ToolbarInteractionRetouchTrigger.PreviewMouseDown);
    }

    private void RetouchFloatingTopmostOnToolbarInteraction(ToolbarInteractionRetouchTrigger trigger)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        if (trigger == ToolbarInteractionRetouchTrigger.PreviewMouseDown)
        {
            ToolbarInteractionRetouchStateUpdater.MarkPreviewMouseDown(
                ref _toolbarInteractionRetouchState,
                nowUtc);
        }

        var snapshot = CaptureToolbarInteractionRetouchSnapshot(out var launcherWindow);
        var suppressionDecision = ToolbarInteractionActivationSuppressionPolicy.Resolve(
            trigger,
            snapshot,
            _toolbarInteractionRetouchState.LastPreviewMouseDownUtc,
            _toolbarInteractionRetouchState.LastRetouchUtc,
            nowUtc,
            launcherOnlySuppressionMs: ToolbarInteractionActivationSuppressionWindowPolicy.ResolveMs(snapshot));
        if (suppressionDecision.ShouldSuppress)
        {
            System.Diagnostics.Debug.WriteLine(ToolbarInteractionRetouchDiagnosticsPolicy.FormatActivationSuppressionSkipMessage(
                trigger,
                suppressionDecision.Reason));
            return;
        }

        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(snapshot, trigger);
        if (!decision.ShouldRetouch)
        {
            System.Diagnostics.Debug.WriteLine(ToolbarInteractionRetouchDiagnosticsPolicy.FormatDecisionSkipMessage(
                trigger,
                decision.Reason));
            return;
        }

        var admission = ToolbarInteractionRetouchAdmissionPolicy.Resolve(
            _zOrderPolicyApplying,
            _floatingDispatchQueueState.ApplyQueued,
            decision.ForceEnforceZOrder);
        if (!admission.ShouldRequest)
        {
            System.Diagnostics.Debug.WriteLine(ToolbarInteractionRetouchDiagnosticsPolicy.FormatAdmissionSkipMessage(
                trigger,
                admission.Reason,
                decision.ForceEnforceZOrder));
            return;
        }

        var intervalMs = ToolbarInteractionRetouchIntervalPolicy.ResolveMs(snapshot, trigger);
        var throttleDecision = ToolbarInteractionRetouchThrottlePolicy.Resolve(
            _toolbarInteractionRetouchState.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs: intervalMs);
        if (!throttleDecision.ShouldAllow)
        {
            System.Diagnostics.Debug.WriteLine(ToolbarInteractionRetouchDiagnosticsPolicy.FormatThrottleSkipMessage(
                trigger,
                throttleDecision.Reason,
                intervalMs));
            return;
        }

        var executionPlan = ToolbarInteractionRetouchExecutionPlanPolicy.Resolve(snapshot, decision);
        if (ToolbarInteractionRetouchStateStampPolicy.ShouldMarkRetouched(trigger, executionPlan))
        {
            ToolbarInteractionRetouchStateUpdater.MarkRetouched(
                ref _toolbarInteractionRetouchState,
                nowUtc);
        }
        System.Diagnostics.Debug.WriteLine(
            ToolbarInteractionRetouchDiagnosticsPolicy.FormatExecutionPlanMessage(
                trigger,
                executionPlan));
        if (executionPlan.ApplyDirectDriftRepair)
        {
            var directRepairAdmission = ToolbarInteractionDirectRepairAdmissionPolicy.Resolve(
                _zOrderPolicyApplying,
                _floatingDispatchQueueState.ApplyQueued);
            if (!directRepairAdmission.ShouldApply)
            {
                System.Diagnostics.Debug.WriteLine(
                    ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairAdmissionSkipMessage(
                        trigger,
                        directRepairAdmission.Reason));
                return;
            }

            var dispatchMode = ToolbarInteractionRetouchDispatchPolicy.Resolve(
                trigger,
                snapshot,
                executionPlan);
            System.Diagnostics.Debug.WriteLine(
                ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchMessage(
                    trigger,
                    dispatchMode));

            var executionOutcome = ToolbarInteractionDirectRepairExecutionCoordinator.Apply(
                dispatchMode,
                () => _toolbarDirectRepairBackgroundQueued,
                () => ToolbarInteractionDirectRepairDispatchStateUpdater.TryMarkQueued(ref _toolbarDirectRepairBackgroundQueued),
                () => ToolbarInteractionDirectRepairDispatchStateUpdater.Clear(ref _toolbarDirectRepairBackgroundQueued),
                () => ToolbarInteractionDirectRepairRerunStateUpdater.Request(ref _toolbarDirectRepairRerunRequested),
                () => ToolbarInteractionDirectRepairRerunStateUpdater.TryConsume(ref _toolbarDirectRepairRerunRequested),
                () => ToolbarInteractionDirectRepairRerunStateUpdater.Clear(ref _toolbarDirectRepairRerunRequested),
                () => ApplyToolbarDirectRepair(trigger, launcherWindow),
                action => TryBeginInvoke(
                    action,
                    System.Windows.Threading.DispatcherPriority.Background,
                    "RetouchFloatingTopmostOnToolbarInteraction.DirectRepair"));

            if (executionOutcome == ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected
                || executionOutcome == ToolbarInteractionDirectRepairExecutionOutcome.BackgroundMarkQueuedFailed)
            {
                System.Diagnostics.Debug.WriteLine(
                    ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchAdmissionSkipMessage(
                        trigger,
                        ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued));
                return;
            }

            if (executionOutcome == ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed)
            {
                System.Diagnostics.Debug.WriteLine(
                    ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchFailureMessage(
                        trigger,
                        nameof(InvalidOperationException),
                        "dispatcher-begininvoke-failed"));
                return;
            }

            return;
        }

        FloatingZOrderApplyExecutor.Apply(
            requestZOrderApply: executionPlan.RequestZOrderApply,
            forceEnforceZOrder: executionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void ApplyToolbarDirectRepair(
        ToolbarInteractionRetouchTrigger trigger,
        Window? fallbackLauncherWindow)
    {
        var currentSnapshot = CaptureToolbarInteractionRetouchSnapshot(out var currentLauncherWindow);
        currentLauncherWindow ??= fallbackLauncherWindow;
        var repairPlan = FloatingTopmostDriftRepairPolicy.Resolve(currentSnapshot);
        var enforceRepairZOrder = FloatingTopmostDriftRepairEnforcePolicy.Resolve(currentSnapshot, trigger);
        FloatingTopmostDriftRepairExecutor.Apply(
            repairPlan,
            _toolbarWindow,
            _rollCallWindow,
            currentLauncherWindow,
            enforceRepairZOrder,
            ex => System.Diagnostics.Debug.WriteLine(
                ToolbarInteractionRetouchDiagnosticsPolicy.FormatDirectRepairDispatchFailureMessage(
                    trigger,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private ToolbarInteractionRetouchSnapshot CaptureToolbarInteractionRetouchSnapshot(out Window? launcherWindow)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var launcherSnapshot = CaptureLauncherWindowRuntimeSnapshot();
        launcherWindow = ResolveLauncherWindow(launcherSnapshot);
        var launcherVisibleForRepair = LauncherTopmostVisibilityHoldPolicy.ResolveVisibleForRepair(
            launcherSnapshot.VisibleForTopmost,
            _lastLauncherVisibleForTopmostUtc,
            nowUtc);
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: _overlayWindow?.IsVisible == true,
            PhotoModeActive: _overlayWindow?.IsPhotoModeActive == true,
            WhiteboardActive: _toolbarWindow?.BoardActive == true,
            ToolbarVisible: _toolbarWindow?.IsVisible == true,
            ToolbarTopmost: _toolbarWindow?.Topmost == true,
            RollCallVisible: _rollCallWindow?.IsVisible == true,
            RollCallTopmost: _rollCallWindow?.Topmost == true,
            LauncherVisible: launcherVisibleForRepair,
            LauncherTopmost: launcherWindow?.Topmost == true);
    }

    private void ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason reason)
    {
        ToolbarInteractionRetouchRuntimeResetExecutor.Apply(
            ref _toolbarDirectRepairBackgroundQueued,
            ref _toolbarDirectRepairRerunRequested,
            ref _toolbarInteractionRetouchState);
        if (reason != ToolbarInteractionRetouchRuntimeResetReason.None)
        {
            System.Diagnostics.Debug.WriteLine(
                ToolbarInteractionRetouchDiagnosticsPolicy.FormatRuntimeResetMessage(reason));
        }
    }

    // ApplyPaintToolbarPosition moved to Orchestrator

    private void CapturePaintToolbarPosition(bool save)
    {
        _paintWindowOrchestrator.CaptureToolbarPosition(_settings, save);
    }

    private void ShowPaintOverlayIfNeeded()
    {
        var overlay = _paintWindowOrchestrator.OverlayWindow;
        if (overlay == null)
        {
            return;
        }

        var transitionPlan = PaintVisibilityTransitionPolicy.ResolveEnsureOverlayVisible(
            overlayVisible: overlay.IsVisible);
        ApplyEnsurePaintOverlayVisibleTransition(transitionPlan);
    }

    private void ApplyEnsurePaintOverlayVisibleTransition(PaintVisibilityTransitionPlan transitionPlan)
    {
        if (transitionPlan.ShowOverlay)
        {
            ExecuteLifecycleSafe("paint-ensure-overlay-visible", "show-paint-orchestrator", () => _paintWindowOrchestrator.Show());
        }
        if (transitionPlan.SyncFloatingOwnersVisible)
        {
            SyncFloatingWindowOwners(overlayVisible: true);
        }
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void OnOpenPaintSettings()
    {
        var dialog = new Paint.PaintSettingsDialog(_settings)
        {
            Owner = _toolbarWindow != null ? (Window)_toolbarWindow : this
        };
        TryFixWindowBorders(this, "paint-settings", "main-window");
        TryFixWindowBorders(dialog, "paint-settings", "paint-settings-dialog");
        var result = TryShowDialogWithDiagnostics(dialog, nameof(Paint.PaintSettingsDialog));
        
        var applied = result;
        if (applied)
        {
            _settings.ControlMsPpt = dialog.ControlMsPpt;
            _settings.ControlWpsPpt = dialog.ControlWpsPpt;
            _settings.OfficeInputMode = dialog.OfficeInputMode;
            _settings.WpsInputMode = dialog.WpsInputMode;
            _settings.WpsWheelForward = dialog.WpsWheelForward;
            _settings.WpsDebounceMs = dialog.WpsDebounceMs;
            _settings.PresentationLockStrategyWhenDegraded = dialog.PresentationLockStrategyWhenDegraded;
            _settings.PresentationClassifierAutoLearnEnabled = dialog.PresentationClassifierAutoLearnEnabled;
            if (dialog.PresentationClassifierClearOverridesRequested)
            {
                _settings.PresentationClassifierOverridesJson = string.Empty;
                _settings.PresentationClassifierLastLearnUtc = string.Empty;
                _settings.PresentationClassifierLastLearnDetail = string.Empty;
                _settings.PresentationClassifierRecentLearnRecordsJson = string.Empty;
            }
            _settings.ForcePresentationForegroundOnFullscreen = dialog.ForcePresentationForegroundOnFullscreen;
            _settings.BrushSize = dialog.BrushSize;
            _settings.BrushOpacity = dialog.BrushOpacity;
            _settings.BrushStyle = dialog.BrushStyle;
            _settings.WhiteboardPreset = dialog.WhiteboardPreset;
            _settings.CalligraphyPreset = dialog.CalligraphyPreset;
            _settings.PresetScheme = dialog.PresetScheme;
            _settings.ClassroomWritingMode = dialog.ClassroomWritingMode;
            _settings.CalligraphyInkBloomEnabled = dialog.CalligraphyInkBloomEnabled;
            _settings.CalligraphySealEnabled = dialog.CalligraphySealEnabled;
            _settings.CalligraphyOverlayOpacityThreshold = dialog.CalligraphyOverlayOpacityThreshold;
            _settings.EraserSize = dialog.EraserSize;
            _settings.BoardOpacity = 255;
            _settings.ShapeType = dialog.ShapeType;
            _settings.BrushColor = dialog.BrushColor;
            _settings.PaintToolbarScale = dialog.ToolbarScale;
            _settings.InkSaveEnabled = dialog.InkSaveEnabled;
            _settings.InkExportScope = dialog.InkExportScope;
            _settings.InkExportMaxParallelFiles = dialog.InkExportMaxParallelFiles;
            _settings.PhotoRememberTransform = dialog.PhotoRememberTransform;
            _settings.PhotoCrossPageDisplay = dialog.PhotoCrossPageDisplay;
            _settings.PhotoInputTelemetryEnabled = dialog.PhotoInputTelemetryEnabled;
            _settings.PhotoNeighborPrefetchRadiusMax = dialog.PhotoNeighborPrefetchRadiusMax;
            _settings.PhotoPostInputRefreshDelayMs = dialog.PhotoPostInputRefreshDelayMs;
            _settings.PhotoWheelZoomBase = dialog.PhotoWheelZoomBase;
            _settings.PhotoGestureZoomSensitivity = dialog.PhotoGestureZoomSensitivity;
            _settings.PhotoInertiaProfile = dialog.PhotoInertiaProfile;
            _settings.QuickColor1 = dialog.QuickColor1;
            _settings.QuickColor2 = dialog.QuickColor2;
            _settings.QuickColor3 = dialog.QuickColor3;
            SaveSettings();
            _inkExportOptions.Scope = _settings.InkExportScope;
            _inkExportOptions.MaxParallelFiles = _settings.InkExportMaxParallelFiles;

            _paintWindowOrchestrator.ApplySettings(_settings);
        }
        _paintWindowOrchestrator.OverlayWindow?.RestorePresentationFocusIfNeeded(requireFullscreen: true);
    }

    private void OnPhotoCloseRequested()
    {
        var context = new PhotoCloseTransitionContext(
            OverlayVisible: _overlayWindow?.IsVisible == true);
        var transitionPlan = PhotoCloseTransitionPolicy.Resolve(context);
        ApplyPhotoCloseTransition(transitionPlan);
    }

    private void ApplyPhotoCloseTransition(PhotoCloseTransitionPlan transitionPlan)
    {
        if (PhotoCloseOwnerDetachmentPolicy.ShouldDetachOwners(transitionPlan.SyncFloatingOwnersVisible))
        {
            // 断开 owner 链，避免关闭图片模式时浮层关系滞留。
            SyncFloatingWindowOwners(overlayVisible: false);
        }
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void OnOverlaySessionTransitionOccurred(UiSessionTransition transition)
    {
        var admissionDecision = SessionTransitionEventAdmissionPolicy.Resolve(
            transition.HasStateChange,
            _lastAppliedSessionTransitionId,
            transition.Id);
        if (!admissionDecision.ShouldProcess)
        {
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatAdmissionSkipMessage(
                transition.Id,
                admissionDecision.Reason));
            return;
        }
        SessionTransitionDuplicateStateUpdater.MarkApplied(
            ref _lastAppliedSessionTransitionId,
            transition.Id);

        var violations = _paintWindowOrchestrator.CurrentOverlaySessionViolations;
        if (SessionTransitionViolationLogPolicy.ShouldLog(violations.Count))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[UiSession][Violation] #{transition.Id} {string.Join(" | ", violations)}");
        }

        var windowingDecision = SessionTransitionWindowingPolicy.ResolveDecision(transition);
        var decision = windowingDecision.ZOrderDecision;
        if (windowingDecision.SurfaceReason == SessionTransitionSurfaceReason.SurfaceRetouchRequested)
        {
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatSurfaceReasonMessage(
                transition.Id,
                windowingDecision.SurfaceReason));
        }
        if (windowingDecision.ApplyReason != SessionTransitionApplyReason.None)
        {
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatApplyReasonMessage(
                transition.Id,
                windowingDecision.ApplyReason));
        }
        if (windowingDecision.WidgetVisibilityReason != SessionFloatingWidgetVisibilityReason.None)
        {
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatWidgetVisibilityReasonMessage(
                transition.Id,
                windowingDecision.WidgetVisibilityReason));
        }
        var applyGateDecision = SessionTransitionApplyGatePolicy.Resolve(decision);
        if (!applyGateDecision.ShouldApply)
        {
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatWindowingReasonMessage(
                transition.Id,
                windowingDecision.Reason));
            System.Diagnostics.Debug.WriteLine(SessionTransitionDiagnosticsPolicy.FormatApplyGateSkipMessage(
                transition.Id,
                applyGateDecision.Reason));
            return;
        }

        ApplySurfaceZOrderDecision(decision);
    }
}




