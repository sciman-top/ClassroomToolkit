using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.UseCases.Photos;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

/// <summary>
/// MainWindow core: fields, constructor, lifecycle, roll-call, Z-order policy, and settings persistence.
/// Paint/Ink logic → MainWindow.Paint.cs
/// Photo/Presentation logic → MainWindow.Photo.cs
/// Launcher UI logic → MainWindow.Launcher.cs
/// </summary>
public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    private Paint.PaintOverlayWindow? _overlayWindow => _paintWindowOrchestrator.OverlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow => _paintWindowOrchestrator.ToolbarWindow;
    private Photos.ImageManagerWindow? _imageManagerWindow;
    private readonly List<ZOrderSurface> _surfaceStack = new();
    private SurfaceZOrderDecisionRuntimeState _surfaceZOrderDecisionState = SurfaceZOrderDecisionRuntimeState.Default;
    private readonly DispatcherTimer _presentationForegroundSuppressionTimer;
    private IDisposable? _presentationForegroundSuppression;
    private bool _zOrderPolicyApplying;
    private FloatingCoordinationRuntimeState _floatingCoordinationState = FloatingCoordinationRuntimeState.Default;
    private FloatingDispatchQueueState _floatingDispatchQueueState = FloatingDispatchQueueState.Default;
    private ToolbarInteractionRetouchRuntimeState _toolbarInteractionRetouchState = ToolbarInteractionRetouchRuntimeState.Default;
    private bool _toolbarDirectRepairBackgroundQueued;
    private bool _toolbarDirectRepairRerunRequested;
    private ExplicitForegroundRetouchRuntimeState _explicitForegroundRetouchState = ExplicitForegroundRetouchRuntimeState.Default;
    private OverlayActivatedRetouchRuntimeState _overlayActivatedRetouchState = OverlayActivatedRetouchRuntimeState.Default;
    private ZOrderRequestRuntimeState _zOrderRequestState = ZOrderRequestRuntimeState.Default;
    private long _lastAppliedSessionTransitionId;
    private readonly ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationSession _photoNavigationSession = new();
    private LauncherBubbleWindow? _bubbleWindow;
    private LauncherBubbleVisibilityRuntimeState _bubbleVisibilityState = LauncherBubbleVisibilityRuntimeState.Default;
    private DateTime _lastLauncherVisibleForTopmostUtc = MainWindowRuntimeDefaults.DefaultTimestampUtc;
    private readonly DispatcherTimer _autoExitTimer;
    private readonly CancellationTokenSource _backgroundTasksCancellation = new();
    private bool _allowClose;
    private bool _settingsSaveFailedNotified;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly InkExportOptions _inkExportOptions;
    private readonly InkPersistenceService _inkPersistenceService;
    private readonly InkExportService _inkExportService;
    private readonly MainViewModel _mainViewModel;
    private readonly IConfigurationService _configurationService;
    private readonly IRollCallWindowFactory _rollCallWindowFactory;
    private readonly Services.IPaintWindowOrchestrator _paintWindowOrchestrator;
    private readonly Photos.IImageManagerWindowFactory _imageManagerWindowFactory;
    private readonly IWindowOrchestrator _windowOrchestrator;
    private bool _paintOrchestratorEventsWired;
    private Paint.PaintOverlayWindow? _lifecycleWiredOverlayWindow;
    private Paint.PaintToolbarWindow? _lifecycleWiredToolbarWindow;
    public MainWindow(
        AppSettingsService settingsService,
        AppSettings settings,
        InkExportOptions inkExportOptions,
        InkPersistenceService inkPersistenceService,
        InkExportService inkExportService,
        MainViewModel mainViewModel,
        IConfigurationService configurationService,
        IRollCallWindowFactory rollCallWindowFactory,
        Services.IPaintWindowOrchestrator paintWindowOrchestrator,
        Photos.IImageManagerWindowFactory imageManagerWindowFactory,
        IWindowOrchestrator windowOrchestrator)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _inkExportOptions = inkExportOptions;
        _inkPersistenceService = inkPersistenceService;
        _inkExportService = inkExportService;
        _inkExportOptions.Scope = settings.InkExportScope;
        _inkExportOptions.MaxParallelFiles = settings.InkExportMaxParallelFiles;
        _mainViewModel = mainViewModel;
        _configurationService = configurationService;
        _rollCallWindowFactory = rollCallWindowFactory;
        _paintWindowOrchestrator = paintWindowOrchestrator;
        _imageManagerWindowFactory = imageManagerWindowFactory;
        _windowOrchestrator = windowOrchestrator;
        _autoExitTimer = new DispatcherTimer();
        _autoExitTimer.Tick += OnAutoExitTimerTick;
        _presentationForegroundSuppressionTimer = new DispatcherTimer();
        _presentationForegroundSuppressionTimer.Tick += OnPresentationForegroundSuppressionTimerTick;
        _mainViewModel.OpenRollCallSettingsCommand = new RelayCommand(OnOpenRollCallSettings);
        _mainViewModel.OpenPaintSettingsCommand = new RelayCommand(OnOpenPaintSettings);
        DataContext = _mainViewModel;
        Loaded += OnLoaded;
        IsVisibleChanged += OnMainWindowVisibleChanged;
        Closing += OnClosing;
    }

    private void OnAutoExitTimerTick(object? sender, EventArgs e)
    {
        _autoExitTimer.Stop();
        RequestExit();
    }

    private void OnPresentationForegroundSuppressionTimerTick(object? sender, EventArgs e)
    {
        ReleasePresentationForegroundSuppression();
    }

    private void OnMainWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (MainWindowVisibleChangedPolicy.ShouldEnsureVisible(IsVisible))
        {
            WindowPlacementHelper.EnsureVisible(this);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLauncherPosition();
        WindowPlacementHelper.EnsureVisible(this);
        ScheduleAutoExitTimer();
        ScheduleInkCleanup();
        WarmupRollCallData();
        var toggleAction = MainWindowLoadedToggleActionPolicy.Resolve(_settings.LauncherMinimized);
        if (toggleAction == MainWindowLoadedToggleAction.MinimizeLauncher)
        {
            MinimizeLauncher(fromSettings: true);
        }
        else
        {
            UpdateToggleButtons();
        }
        RunStartupDiagnostics();
    }

    private void WarmupRollCallData()
    {
        // Warmup is performed inside RollCallWindow initialization.
    }

    // ── Roll-call ──

    private void OnRollCallClick(object sender, RoutedEventArgs e)
    {
        EnsureRollCallWindow();
        if (_rollCallWindow == null)
        {
            return;
        }
        var transitionPlan = RollCallVisibilityTransitionPolicy.Resolve(
            CaptureRollCallVisibilityTransitionContext());
        ApplyRollCallTransition(transitionPlan);
        UpdateToggleButtons();
    }

    private RollCallVisibilityTransitionContext CaptureRollCallVisibilityTransitionContext()
    {
        return new RollCallVisibilityTransitionContext(
            RollCallVisible: _rollCallWindow?.IsVisible == true,
            RollCallActive: _rollCallWindow?.IsActive == true,
            OverlayVisible: _overlayWindow?.IsVisible == true);
    }

    private void ApplyRollCallTransition(RollCallVisibilityTransitionPlan transitionPlan)
    {
        if (_rollCallWindow == null)
        {
            return;
        }

        if (transitionPlan.HideWindow)
        {
            ExecuteLifecycleSafe("rollcall-toggle", "hide-rollcall-window", _rollCallWindow.HideRollCall);
        }
        if (transitionPlan.SyncOwnerToOverlay)
        {
            SyncOverlayOwnedWindow(_rollCallWindow);
        }
        if (transitionPlan.ShowWindow)
        {
            ExecuteLifecycleSafe("rollcall-toggle", "show-rollcall-window", _rollCallWindow.Show);
        }
        UserInitiatedWindowExecutionExecutor.Apply(
            _rollCallWindow,
            transitionPlan.ActivateWindow);
        FloatingZOrderApplyExecutor.Apply(
            transitionPlan.RequestZOrderApply,
            transitionPlan.ForceEnforceZOrder,
            RequestApplyZOrderPolicy);
    }

    private void EnsureRollCallWindow()
    {
        if (_rollCallWindow != null)
        {
            return;
        }
        var path = ResolveStudentWorkbookPath();
        _rollCallWindow = _rollCallWindowFactory.Create(path);
        WireRollCallWindow(_rollCallWindow);
    }

    private void WireRollCallWindow(RollCallWindow rollCallWindow)
    {
        rollCallWindow.IsVisibleChanged += OnRollCallWindowVisibleChanged;
        rollCallWindow.Closed += OnRollCallWindowClosed;
    }

    private void OnRollCallWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateToggleButtons();
    }

    private void OnRollCallWindowClosed(object? sender, EventArgs e)
    {
        _rollCallWindow = null;
        UpdateToggleButtons();
    }

    private void UpdateToggleButtons()
    {
        var toggleState = ViewModels.MainWindowToggleStatePolicy.Resolve(
            overlayVisible: _overlayWindow?.IsVisible == true,
            rollCallVisible: _rollCallWindow?.IsVisible == true);
        _mainViewModel.IsPaintActive = toggleState.IsPaintActive;
        _mainViewModel.IsRollCallVisible = toggleState.IsRollCallVisible;
    }

    private void OnOpenRollCallSettings()
    {
        var dialog = new RollCallSettingsDialog(_settings, ResolveAvailableClasses())
        {
            Owner = this
        };
        if (!TryShowDialogWithDiagnostics(dialog, nameof(RollCallSettingsDialog)))
        {
            return;
        }
        var patch = new RollCallSettingsPatch(
            dialog.RollCallShowId,
            dialog.RollCallShowName,
            dialog.RollCallShowPhoto,
            dialog.RollCallPhotoDurationSeconds,
            dialog.RollCallPhotoSharedClass,
            dialog.RollCallTimerSoundEnabled,
            dialog.RollCallTimerReminderEnabled,
            dialog.RollCallTimerReminderIntervalMinutes,
            dialog.RollCallTimerSoundVariant,
            dialog.RollCallTimerReminderSoundVariant,
            dialog.RollCallSpeechEnabled,
            dialog.RollCallSpeechEngine,
            dialog.RollCallSpeechVoiceId,
            dialog.RollCallSpeechOutputId,
            dialog.RollCallRemoteEnabled,
            dialog.RollCallRemoteGroupSwitchEnabled,
            dialog.RemotePresenterKey,
            dialog.RemoteGroupSwitchKey);
        RollCallSettingsApplier.Apply(_settings, patch);
        SaveSettings();
        _rollCallWindow?.ApplySettings(_settings);
    }

    private IReadOnlyList<string> ResolveAvailableClasses()
    {
        return _rollCallWindow?.AvailableClasses ?? Array.Empty<string>();
    }

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
        try
        {
            Dispatcher.BeginInvoke(action, priority);
            return true;
        }
        catch (Exception ex)
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
                ResolveLauncherWindow(coordination.Launcher),
                coordination.Runtime.ImageManagerVisible ? _imageManagerWindow : null);
            SessionTransitionDecisionStateUpdater.Apply(
                ref _floatingCoordinationState,
                state);
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

    // ── Lifecycle & settings ──

    private void RequestExit()
    {
        var exitPlan = MainWindowExitPlanPolicy.Resolve(
            _allowClose,
            _backgroundTasksCancellation.IsCancellationRequested,
            _bubbleWindow != null,
            _rollCallWindow != null);
        if (!exitPlan.ShouldExit)
        {
            return;
        }

        _allowClose = true;
        const string phase = "request-exit";

        if (exitPlan.ShouldCancelBackgroundTasks)
        {
            ExecuteLifecycleSafe(phase, "cancel-background-tasks", () => _backgroundTasksCancellation.Cancel());
        }
        ExecuteLifecycleSafe(phase, "reset-toolbar-retouch-runtime", () => ResetToolbarInteractionRetouchRuntime(ToolbarInteractionRetouchRuntimeResetReason.RequestExit));
        ExecuteLifecycleSafe(phase, "trigger-ink-cleanup", TriggerInkCleanup);
        ExecuteLifecycleSafe(phase, "capture-toolbar-position", () => CapturePaintToolbarPosition(save: true));
        ExecuteLifecycleSafe(phase, "save-launcher-settings", SaveLauncherSettings);
        if (exitPlan.ShouldCloseBubbleWindow && _bubbleWindow != null)
        {
            var bubbleWindow = _bubbleWindow;
            ExecuteLifecycleSafe(phase, "close-launcher-bubble-window", bubbleWindow.Close);
            _bubbleWindow = null;
        }
        if (exitPlan.ShouldCloseRollCallWindow && _rollCallWindow != null)
        {
            var rollCallWindow = _rollCallWindow;
            ExecuteLifecycleSafe(phase, "close-rollcall-window", rollCallWindow.RequestClose);
            _rollCallWindow = null;
        }
        
        ExecuteLifecycleSafe(phase, "close-paint-window-orchestrator", _paintWindowOrchestrator.Close);
        ExecuteLifecycleSafe(phase, "shutdown-application", () => System.Windows.Application.Current.Shutdown());
    }

    private void ExecuteLifecycleSafe(string phase, string operation, Action action)
    {
        SafeActionExecutionExecutor.TryExecute(
            action,
            ex => System.Diagnostics.Debug.WriteLine(
                LifecycleSafeExecutionDiagnosticsPolicy.FormatFailureMessage(
                    phase,
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private void TryFixWindowBorders(Window window, string phase, string target)
    {
        ExecuteLifecycleSafe(
            phase,
            $"border-fix-{target}",
            () =>
            {
                try
                {
                    BorderFixHelper.FixAllBorders(window);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        BorderFixDiagnosticsPolicy.FormatFailureMessage(
                            phase,
                            target,
                            ex.GetType().Name,
                            ex.Message));
                }
            });
    }

    private bool TryShowDialogWithDiagnostics(Window dialog, string dialogName)
    {
        var result = false;
        SafeActionExecutionExecutor.TryExecute(
            () => DialogShowResultStateUpdater.MarkFromDialogResult(ref result, dialog.SafeShowDialog()),
            ex => System.Diagnostics.Debug.WriteLine(
                DialogShowDiagnosticsPolicy.FormatFailureMessage(
                    dialogName,
                    ex.Message)));
        return result;
    }

    private void ShowMainInfoMessageSafe(string operation, string message)
    {
        ExecuteLifecycleSafe(
            "main-message",
            operation,
            () => System.Windows.MessageBox.Show(
                this,
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information));
    }

    private void SaveSettings()
    {
        try
        {
            if (_overlayWindow != null &&
                _overlayWindow.TryGetStylusAdaptiveState(
                    out var pressureProfile,
                    out var sampleRateTier,
                    out var predictionHorizonMs,
                    out var calibratedLow,
                    out var calibratedHigh))
            {
                _settings.StylusAdaptivePressureProfile = pressureProfile;
                _settings.StylusAdaptiveSampleRateTier = sampleRateTier;
                _settings.StylusAdaptivePredictionHorizonMs = predictionHorizonMs;
                _settings.StylusPressureCalibratedLow = calibratedLow;
                _settings.StylusPressureCalibratedHigh = calibratedHigh;
            }
            _settingsService.Save(_settings);
            SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);
        }
        catch (Exception ex)
        {
            var notificationPlan = SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified);
            SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(
                ref _settingsSaveFailedNotified,
                notificationPlan);
            if (!notificationPlan.ShouldNotify)
            {
                return;
            }
            var detail = $"设置保存失败：{ex.Message}\n请检查设置文件权限或磁盘状态。";
            ShowMainInfoMessageSafe("settings-save-failed", detail);
        }
    }

    private void ScheduleInkCleanup()
    {
        _ = System.Threading.Tasks.Task.Run(TriggerInkCleanup);
    }

    private void TriggerInkCleanup()
    {
        try
        {
            var candidates = InkCleanupCandidateDirectoryPolicy.Resolve(
                AppDomain.CurrentDomain.BaseDirectory,
                _settings.InkPhotoRootPath,
                _settings.PhotoRecentFolders,
                _settings.PhotoFavoriteFolders,
                Directory.Exists);

            var totalSidecars = 0;
            var totalComposites = 0;
            foreach (var directory in candidates)
            {
                totalSidecars += _inkPersistenceService.CleanupOrphanSidecarsInDirectory(directory);
                totalComposites += _inkExportService.CleanupOrphanCompositeOutputsInDirectory(directory);
            }
            var summary = new InkStartupCleanupSummary(
                TotalSidecars: totalSidecars,
                TotalComposites: totalComposites);
            if (InkStartupCleanupLogPolicy.ShouldLogDeletionSummary(summary))
            {
                System.Diagnostics.Debug.WriteLine(InkStartupCleanupLogPolicy.FormatDeletionSummary(summary));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(InkStartupCleanupLogPolicy.FormatFailureMessage(ex.Message));
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var closingPlan = MainWindowOnClosingPlanPolicy.Resolve(_allowClose);
        if (!closingPlan.ShouldCancelClose)
        {
            return;
        }
        e.Cancel = closingPlan.ShouldCancelClose;
        if (closingPlan.ShouldRequestExit)
        {
            RequestExit();
        }
    }
}





















