using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using Microsoft.Extensions.Logging;

namespace ClassroomToolkit.App.Services;

[SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based events are the existing app orchestration contract.")]
public interface IPaintWindowOrchestrator
{
    PaintOverlayWindow? OverlayWindow { get; }
    PaintToolbarWindow? ToolbarWindow { get; }
    UiSessionState? CurrentOverlaySessionState { get; }
    IReadOnlyList<string> CurrentOverlaySessionViolations { get; }
    
    event Action<bool>? PhotoModeChanged;
    event Action<int>? PhotoNavigationRequested;
    event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    event Action? PresentationFullscreenDetected;
    event Action<PresentationForegroundSource>? PresentationForegroundDetected;
    event Action? PhotoForegroundDetected;
    event Action? PhotoCloseRequested;
    event Action? PhotoCursorModeFocusRequested;
    event Action<FloatingZOrderRequest>? FloatingZOrderRequested;
    event Action? OverlayActivated;
    event Action? SettingsRequested;
    event Action? PhotoOpenRequested;
    event Action? RegionCaptureRequested;
    event Action<UiSessionTransition>? OverlaySessionTransitionOccurred;

    void EnsureWindows(AppSettings settings);
    void Show(bool photoMode = false);
    void Hide();
    void ApplySettings(AppSettings settings);
    void CaptureToolbarPosition(AppSettings settings, bool save = true);
    void UpdateToggleButtons();
    void Close();
}

[SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based events are the existing app orchestration contract.")]
public class PaintWindowOrchestrator : IPaintWindowOrchestrator
{
    private static readonly Action<ILogger, bool, bool, Exception?> LogPartialPaintWindowLifecycleState =
        LoggerMessage.Define<bool, bool>(
            LogLevel.Warning,
            new EventId(1, nameof(LogPartialPaintWindowLifecycleState)),
            "Detected partial paint window lifecycle state. Rebuilding pair. overlayExists={OverlayExists}, toolbarExists={ToolbarExists}");

    private static readonly Action<ILogger, Exception?> LogCreatingPaintWindows =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogCreatingPaintWindows)),
            "Creating Paint Overlay and Toolbar windows.");

    private static readonly Action<ILogger, string, Exception?> LogEventCallbackFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogEventCallbackFailed)),
            "PaintWindowOrchestrator event callback failed: {EventName}");

    private static readonly Action<ILogger, string, Exception?> LogPresentationClassifierAutoLearnApplied =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, nameof(LogPresentationClassifierAutoLearnApplied)),
            "Presentation classifier auto-learn applied. {Reason}");

    private readonly IPaintWindowFactory _paintWindowFactory;
    private readonly AppSettingsService _appSettingsService;
    private readonly ILogger<PaintWindowOrchestrator> _logger;
    private AppSettings? _currentSettings;

    public PaintOverlayWindow? OverlayWindow { get; private set; }
    public PaintToolbarWindow? ToolbarWindow { get; private set; }
    public UiSessionState? CurrentOverlaySessionState => OverlayWindow?.CurrentSessionState;
    public IReadOnlyList<string> CurrentOverlaySessionViolations => OverlayWindow?.CurrentSessionViolations ?? Array.Empty<string>();

    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action? PresentationFullscreenDetected;
    public event Action<PresentationForegroundSource>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;
    public event Action? PhotoCloseRequested;
    public event Action? PhotoCursorModeFocusRequested;
    public event Action<FloatingZOrderRequest>? FloatingZOrderRequested;
    public event Action? OverlayActivated;
    public event Action? SettingsRequested;
    public event Action? PhotoOpenRequested;
    public event Action? RegionCaptureRequested;
    public event Action<UiSessionTransition>? OverlaySessionTransitionOccurred;

    public PaintWindowOrchestrator(
        IPaintWindowFactory paintWindowFactory,
        AppSettingsService appSettingsService,
        ILogger<PaintWindowOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(paintWindowFactory);
        ArgumentNullException.ThrowIfNull(appSettingsService);
        ArgumentNullException.ThrowIfNull(logger);

        _paintWindowFactory = paintWindowFactory;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public void EnsureWindows(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (OverlayWindow != null && ToolbarWindow != null)
        {
            return;
        }

        if (OverlayWindow != null || ToolbarWindow != null)
        {
            LogPartialPaintWindowLifecycleState(_logger, OverlayWindow != null, ToolbarWindow != null, null);
            Close();
        }

        LogCreatingPaintWindows(_logger, null);
        var windows = _paintWindowFactory.Create();
        OverlayWindow = windows.overlay;
        ToolbarWindow = windows.toolbar;
        _currentSettings = settings;

        ToolbarWindow.AttachOverlay(OverlayWindow);
        WireOverlayWindowEvents();
        WireToolbarWindowEvents();

        ApplyPaintToolbarPosition(settings);
        ToolbarWindow.ApplySettings(settings);
        WireToolbarBehaviorEvents();
        
        ApplyInitialOverlaySettings(settings, ResolvePreferredPrimaryToolMode());
    }

    private void WireOverlayWindowEvents()
    {
        if (OverlayWindow == null)
        {
            return;
        }

        OverlayWindow.PhotoModeChanged += OnOverlayPhotoModeChanged;
        OverlayWindow.PhotoNavigationRequested += OnOverlayPhotoNavigationRequested;
        OverlayWindow.PhotoUnifiedTransformChanged += OnOverlayPhotoUnifiedTransformChanged;
        OverlayWindow.PresentationFullscreenDetected += OnOverlayPresentationFullscreenDetected;
        OverlayWindow.PresentationForegroundDetected += OnOverlayPresentationForegroundDetected;
        OverlayWindow.PhotoForegroundDetected += OnOverlayPhotoForegroundDetected;
        OverlayWindow.PhotoCloseRequested += OnOverlayPhotoCloseRequested;
        OverlayWindow.PhotoCursorModeFocusRequested += OnOverlayPhotoCursorModeFocusRequested;
        OverlayWindow.FloatingZOrderRequested += OnOverlayFloatingZOrderRequested;
        OverlayWindow.UiSessionTransitionOccurred += OnOverlaySessionTransitionOccurred;
        OverlayWindow.Activated += OnOverlayWindowActivated;
        OverlayWindow.Closed += OnOverlayWindowClosed;
    }

    private void UnwireOverlayWindowEvents(PaintOverlayWindow? overlayWindow)
    {
        if (overlayWindow == null)
        {
            return;
        }

        overlayWindow.PhotoModeChanged -= OnOverlayPhotoModeChanged;
        overlayWindow.PhotoNavigationRequested -= OnOverlayPhotoNavigationRequested;
        overlayWindow.PhotoUnifiedTransformChanged -= OnOverlayPhotoUnifiedTransformChanged;
        overlayWindow.PresentationFullscreenDetected -= OnOverlayPresentationFullscreenDetected;
        overlayWindow.PresentationForegroundDetected -= OnOverlayPresentationForegroundDetected;
        overlayWindow.PhotoForegroundDetected -= OnOverlayPhotoForegroundDetected;
        overlayWindow.PhotoCloseRequested -= OnOverlayPhotoCloseRequested;
        overlayWindow.PhotoCursorModeFocusRequested -= OnOverlayPhotoCursorModeFocusRequested;
        overlayWindow.FloatingZOrderRequested -= OnOverlayFloatingZOrderRequested;
        overlayWindow.UiSessionTransitionOccurred -= OnOverlaySessionTransitionOccurred;
        overlayWindow.Activated -= OnOverlayWindowActivated;
        overlayWindow.Closed -= OnOverlayWindowClosed;
    }

    private void WireToolbarWindowEvents()
    {
        if (ToolbarWindow == null)
        {
            return;
        }

        ToolbarWindow.Closed += OnToolbarWindowClosed;
        ToolbarWindow.LocationChanged += OnToolbarWindowLocationChanged;
    }

    private void UnwireToolbarWindowEvents(PaintToolbarWindow? toolbarWindow)
    {
        if (toolbarWindow == null)
        {
            return;
        }

        toolbarWindow.Closed -= OnToolbarWindowClosed;
        toolbarWindow.LocationChanged -= OnToolbarWindowLocationChanged;
    }

    private void WireToolbarBehaviorEvents()
    {
        if (ToolbarWindow == null)
        {
            return;
        }

        ToolbarWindow.ModeChanged += OnToolbarModeChanged;
        ToolbarWindow.BrushColorChanged += OnToolbarBrushColorChanged;
        ToolbarWindow.BoardColorChanged += OnToolbarBoardColorChanged;
        ToolbarWindow.ClearRequested += OnToolbarClearRequested;
        ToolbarWindow.UndoRequested += OnToolbarUndoRequested;
        ToolbarWindow.QuickColorSlotChanged += OnToolbarQuickColorSlotChanged;
        ToolbarWindow.ShapeTypeChanged += OnToolbarShapeTypeChanged;
        ToolbarWindow.WhiteboardToggled += OnToolbarWhiteboardToggled;
        ToolbarWindow.SettingsRequested += OnToolbarSettingsRequested;
        ToolbarWindow.PhotoOpenRequested += OnToolbarPhotoOpenRequested;
        ToolbarWindow.RegionCaptureRequested += OnToolbarRegionCaptureRequested;
    }

    private void UnwireToolbarBehaviorEvents(PaintToolbarWindow? toolbarWindow)
    {
        if (toolbarWindow == null)
        {
            return;
        }

        toolbarWindow.ModeChanged -= OnToolbarModeChanged;
        toolbarWindow.BrushColorChanged -= OnToolbarBrushColorChanged;
        toolbarWindow.BoardColorChanged -= OnToolbarBoardColorChanged;
        toolbarWindow.ClearRequested -= OnToolbarClearRequested;
        toolbarWindow.UndoRequested -= OnToolbarUndoRequested;
        toolbarWindow.QuickColorSlotChanged -= OnToolbarQuickColorSlotChanged;
        toolbarWindow.ShapeTypeChanged -= OnToolbarShapeTypeChanged;
        toolbarWindow.WhiteboardToggled -= OnToolbarWhiteboardToggled;
        toolbarWindow.SettingsRequested -= OnToolbarSettingsRequested;
        toolbarWindow.PhotoOpenRequested -= OnToolbarPhotoOpenRequested;
        toolbarWindow.RegionCaptureRequested -= OnToolbarRegionCaptureRequested;
    }

    private void OnOverlayPhotoModeChanged(bool active)
        => RaiseEventSafely(() => PhotoModeChanged?.Invoke(active), nameof(PhotoModeChanged));

    private void OnOverlayPhotoNavigationRequested(int direction)
        => RaiseEventSafely(() => PhotoNavigationRequested?.Invoke(direction), nameof(PhotoNavigationRequested));

    private void OnOverlayPhotoUnifiedTransformChanged(double scaleX, double scaleY, double translateX, double translateY)
        => RaiseEventSafely(
            () => PhotoUnifiedTransformChanged?.Invoke(scaleX, scaleY, translateX, translateY),
            nameof(PhotoUnifiedTransformChanged));

    private void OnOverlayPresentationFullscreenDetected()
        => RaiseEventSafely(() => PresentationFullscreenDetected?.Invoke(), nameof(PresentationFullscreenDetected));

    private void OnOverlayPresentationForegroundDetected(PresentationForegroundSource source)
    {
        RaiseEventSafely(() => PresentationForegroundDetected?.Invoke(source), nameof(PresentationForegroundDetected));
        TryAutoLearnPresentationClassifierOverrides();
    }

    private void OnOverlayPhotoForegroundDetected()
        => RaiseEventSafely(() => PhotoForegroundDetected?.Invoke(), nameof(PhotoForegroundDetected));

    private void OnOverlayPhotoCloseRequested()
        => RaiseEventSafely(() => PhotoCloseRequested?.Invoke(), nameof(PhotoCloseRequested));

    private void OnOverlayPhotoCursorModeFocusRequested()
        => RaiseEventSafely(() => PhotoCursorModeFocusRequested?.Invoke(), nameof(PhotoCursorModeFocusRequested));

    private void OnOverlayFloatingZOrderRequested(FloatingZOrderRequest request)
        => RaiseEventSafely(() => FloatingZOrderRequested?.Invoke(request), nameof(FloatingZOrderRequested));

    private void OnOverlaySessionTransitionOccurred(UiSessionTransition transition)
        => RaiseEventSafely(
            () => OverlaySessionTransitionOccurred?.Invoke(transition),
            nameof(OverlaySessionTransitionOccurred));

    private void OnOverlayWindowActivated(object? sender, EventArgs e)
        => RaiseEventSafely(() => OverlayActivated?.Invoke(), nameof(OverlayActivated));

    private void OnOverlayWindowClosed(object? sender, EventArgs e)
    {
        if (ToolbarWindow?.BoardActive == true)
        {
            ToolbarWindow.SetBoardActive(false);
        }
        if (sender is PaintOverlayWindow overlayWindow)
        {
            UnwireOverlayWindowEvents(overlayWindow);
        }
        OverlayWindow = null;
        UpdateToggleButtons();
    }

    private void OnToolbarWindowClosed(object? sender, EventArgs e)
    {
        if (ToolbarWindow != null && _currentSettings != null)
        {
            CaptureToolbarPosition(_currentSettings, save: true);
        }
        if (sender is PaintToolbarWindow toolbarWindow)
        {
            UnwireToolbarBehaviorEvents(toolbarWindow);
            UnwireToolbarWindowEvents(toolbarWindow);
        }

        ToolbarWindow = null;
        UpdateToggleButtons();
    }

    private void OnToolbarWindowLocationChanged(object? sender, EventArgs e)
    {
        if (_currentSettings != null)
        {
            CaptureToolbarPosition(_currentSettings, save: false);
        }
    }

    private void OnToolbarModeChanged(PaintToolMode mode)
    {
        if (ToolbarWindow?.HasOverlay == true || OverlayWindow == null)
        {
            return;
        }

        OverlayWindow.SetMode(mode);
    }

    private void OnToolbarBrushColorChanged(System.Windows.Media.Color color)
    {
        if (ToolbarWindow == null || OverlayWindow == null || _currentSettings == null)
        {
            return;
        }

        if (!ToolbarWindow.HasOverlay)
        {
            OverlayWindow.SetBrush(color, ToolbarWindow.BrushSize, OverlayWindow.CurrentBrushOpacity);
        }

        _currentSettings.BrushColor = color;
        _appSettingsService.Save(_currentSettings);
    }

    private void OnToolbarBoardColorChanged(System.Windows.Media.Color color)
    {
        if (ToolbarWindow == null || OverlayWindow == null || _currentSettings == null)
        {
            return;
        }

        _currentSettings.BoardColor = color;
        _appSettingsService.Save(_currentSettings);
        if (!ToolbarWindow.HasOverlay)
        {
            OverlayWindow.SetBoardColor(color);
            if (ToolbarWindow.BoardActive)
            {
                OverlayWindow.SetBoardOpacity(255);
            }
        }
    }

    private void OnToolbarClearRequested()
    {
        OverlayWindow?.ClearAll();
    }

    private void OnToolbarUndoRequested()
    {
        OverlayWindow?.Undo();
    }

    private void OnToolbarQuickColorSlotChanged(int index, System.Windows.Media.Color color)
    {
        if (_currentSettings == null)
        {
            return;
        }

        switch (index)
        {
            case 0: _currentSettings.QuickColor1 = color; break;
            case 1: _currentSettings.QuickColor2 = color; break;
            case 2: _currentSettings.QuickColor3 = color; break;
        }

        _appSettingsService.Save(_currentSettings);
    }

    private void OnToolbarShapeTypeChanged(PaintShapeType type)
    {
        if (_currentSettings == null)
        {
            return;
        }

        _currentSettings.ShapeType = type;
        _appSettingsService.Save(_currentSettings);
        OverlayWindow?.SetShapeType(type);
    }

    private void OnToolbarWhiteboardToggled(bool active)
    {
        if (ToolbarWindow?.HasOverlay == true || OverlayWindow == null || _currentSettings == null)
        {
            return;
        }

        OverlayWindow.SetBoardColor(_currentSettings.BoardColor);
        OverlayWindow.SetBoardOpacity(active ? (byte)255 : (byte)0);
        RaiseEventSafely(
            () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(true)),
            nameof(FloatingZOrderRequested));
    }

    private void OnToolbarSettingsRequested()
        => RaiseEventSafely(() => SettingsRequested?.Invoke(), nameof(SettingsRequested));

    private void OnToolbarPhotoOpenRequested()
        => RaiseEventSafely(() => PhotoOpenRequested?.Invoke(), nameof(PhotoOpenRequested));

    private void OnToolbarRegionCaptureRequested()
        => RaiseEventSafely(() => RegionCaptureRequested?.Invoke(), nameof(RegionCaptureRequested));

    private void RaiseEventSafely(Action callback, string eventName)
    {
        SafeActionExecutionExecutor.TryExecute(
            callback,
            ex => LogEventCallbackFailed(_logger, eventName, ex));
    }

    private PaintToolMode ResolvePreferredPrimaryToolMode()
    {
        var mode = ToolbarWindow?.CurrentMode ?? PaintToolMode.Brush;
        return mode switch
        {
            PaintToolMode.Cursor => PaintToolMode.Cursor,
            PaintToolMode.Eraser => PaintToolMode.Eraser,
            PaintToolMode.RegionErase => PaintToolMode.RegionErase,
            PaintToolMode.Brush => PaintToolMode.Brush,
            _ => PaintToolMode.Brush
        };
    }

    private void ApplyInitialOverlaySettings(AppSettings settings, PaintToolMode preferredPrimaryMode)
    {
        if (OverlayWindow == null || ToolbarWindow == null) return;
        
        OverlayWindow.SetMode(preferredPrimaryMode);
        OverlayWindow.SetBrush(settings.BrushColor, settings.BrushSize, settings.BrushOpacity);
        OverlayWindow.SetClassroomWritingMode(settings.ClassroomWritingMode);
        OverlayWindow.RestoreStylusAdaptiveState(
            settings.StylusAdaptivePressureProfile,
            settings.StylusAdaptiveSampleRateTier,
            settings.StylusAdaptivePredictionHorizonMs,
            settings.StylusPressureCalibratedLow,
            settings.StylusPressureCalibratedHigh);
        OverlayWindow.SetBrushStyle(settings.BrushStyle);
        OverlayWindow.SetBrushTuning(settings.WhiteboardPreset, settings.CalligraphyPreset);
        OverlayWindow.SetCalligraphyOptions(settings.CalligraphyInkBloomEnabled, settings.CalligraphySealEnabled);
        OverlayWindow.SetCalligraphyOverlayOpacityThreshold(settings.CalligraphyOverlayOpacityThreshold);
        OverlayWindow.SetEraserSize(settings.EraserSize);
        OverlayWindow.SetShapeType(settings.ShapeType);
        
        OverlayWindow.SetBoardColor(settings.BoardColor);
        OverlayWindow.SetBoardOpacity(ToolbarWindow.BoardActive ? (byte)255 : (byte)0);

        OverlayWindow.UpdateOfficeMode(settings.OfficeInputMode);
        OverlayWindow.UpdateWpsMode(settings.WpsInputMode);
        OverlayWindow.UpdatePresentationClassifierOverrides(settings.PresentationClassifierOverridesJson);
        OverlayWindow.UpdatePresentationClassifierAutoLearn(settings.PresentationClassifierAutoLearnEnabled);
        OverlayWindow.UpdateWpsWheelMapping(settings.WpsWheelForward);
        OverlayWindow.UpdateWpsDebounceMs(settings.WpsDebounceMs);
        OverlayWindow.UpdatePresentationDegradeLock(settings.PresentationLockStrategyWhenDegraded);
        OverlayWindow.UpdatePresentationAutoFallbackFailureThreshold(settings.PresentationAutoFallbackFailureThreshold);
        OverlayWindow.UpdatePresentationAutoFallbackProbeIntervalCommands(settings.PresentationAutoFallbackProbeIntervalCommands);
        OverlayWindow.UpdatePresentationTargets(settings.ControlMsPpt, settings.ControlWpsPpt);
        OverlayWindow.UpdatePresentationForegroundPolicy(settings.ForcePresentationForegroundOnFullscreen);
        OverlayWindow.UpdateInkCacheEnabled(settings.InkCacheEnabled);
        OverlayWindow.UpdateInkSaveEnabled(settings.InkSaveEnabled);
        OverlayWindow.UpdateInkRecordEnabled(settings.InkRecordEnabled);
        OverlayWindow.UpdateInkReplayPreviousEnabled(settings.InkReplayPreviousEnabled);
        OverlayWindow.UpdateInkRetentionDays(settings.InkRetentionDays);
        OverlayWindow.UpdateInkPhotoRootPath(settings.InkPhotoRootPath);
        OverlayWindow.UpdatePhotoTransformMemoryEnabled(settings.PhotoRememberTransform);
        OverlayWindow.UpdateCrossPageDisplayEnabled(settings.PhotoCrossPageDisplay);
        OverlayWindow.UpdatePhotoInputTelemetryEnabled(settings.PhotoInputTelemetryEnabled);
        OverlayWindow.UpdateNeighborPrefetchRadiusMax(settings.PhotoNeighborPrefetchRadiusMax);
        OverlayWindow.UpdatePhotoPostInputRefreshDelayMs(settings.PhotoPostInputRefreshDelayMs);
        OverlayWindow.UpdatePhotoZoomTuning(settings.PhotoWheelZoomBase, settings.PhotoGestureZoomSensitivity);
        OverlayWindow.UpdatePhotoInertiaProfile(settings.PhotoInertiaProfile);
        OverlayWindow.SetPhotoUnifiedTransformState(
            settings.PhotoUnifiedTransformEnabled,
            settings.PhotoUnifiedScaleX,
            settings.PhotoUnifiedScaleY,
            settings.PhotoUnifiedTranslateX,
            settings.PhotoUnifiedTranslateY);
    }

    private void TryAutoLearnPresentationClassifierOverrides()
    {
        if (_currentSettings == null || OverlayWindow == null)
        {
            return;
        }
        if (!_currentSettings.PresentationClassifierAutoLearnEnabled)
        {
            return;
        }

        if (!OverlayWindow.TryBuildPresentationClassifierAutoLearnJson(
                _currentSettings.PresentationClassifierOverridesJson,
                out var mergedOverridesJson,
                out var reason))
        {
            return;
        }
        if (string.Equals(
                mergedOverridesJson,
                _currentSettings.PresentationClassifierOverridesJson,
                StringComparison.Ordinal))
        {
            return;
        }

        var learnedAtUtc = DateTime.UtcNow;
        _currentSettings.PresentationClassifierOverridesJson = mergedOverridesJson;
        _currentSettings.PresentationClassifierLastLearnUtc = learnedAtUtc.ToString("O");
        _currentSettings.PresentationClassifierLastLearnDetail = reason;
        _currentSettings.PresentationClassifierRecentLearnRecordsJson =
            PresentationClassifierLearnHistoryPolicy.Append(
                _currentSettings.PresentationClassifierRecentLearnRecordsJson,
                learnedAtUtc,
                reason);
        _appSettingsService.Save(_currentSettings);
        OverlayWindow.UpdatePresentationClassifierOverrides(mergedOverridesJson);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogPresentationClassifierAutoLearnApplied(_logger, reason, null);
        }
    }

    public void Show(bool photoMode = false)
    {
        if (OverlayWindow == null) return;

        var showContext = new PaintWindowVisibilityShowContext(
            OverlayVisible: OverlayWindow.IsVisible,
            ToolbarExists: ToolbarWindow != null,
            ToolbarOwnerAlreadyOverlay: ToolbarWindow?.Owner == OverlayWindow);
        var showPlan = PaintWindowVisibilityPolicy.ResolveShow(showContext);
        if (showPlan.ShowOverlay)
        {
            OverlayWindow.Show();
        }
        if (ToolbarWindow != null)
        {
            FloatingSingleOwnerExecutionExecutor.Apply(
                showPlan.ToolbarOwnerAction,
                ToolbarWindow,
                OverlayWindow);
            if (showPlan.ShowToolbar)
            {
                ToolbarWindow.Show();
            }
            if (showPlan.EnsureToolbarVisible)
            {
                WindowPlacementHelper.EnsureVisible(ToolbarWindow);
            }
            if (showPlan.RestoreToolbarMode)
            {
                OverlayWindow.SetMode(ToolbarWindow.CurrentMode);
            }
        }
        if (showPlan.RestorePresentationFocus)
        {
            OverlayWindow.RestorePresentationFocusIfNeeded(requireFullscreen: true);
        }
    }

    public void Hide()
    {
        if (OverlayWindow == null) return;

        var hideContext = new PaintWindowVisibilityHideContext(
            OverlayVisible: OverlayWindow.IsVisible,
            ToolbarVisible: ToolbarWindow?.IsVisible == true);
        var hidePlan = PaintWindowVisibilityPolicy.ResolveHide(hideContext);
        if (hidePlan.HideOverlay)
        {
            OverlayWindow.Hide();
        }
        if (hidePlan.HideToolbar)
        {
            ToolbarWindow?.Hide();
        }
        if (ToolbarWindow?.BoardActive == true)
        {
            ToolbarWindow.SetBoardActive(false);
        }
    }

    public void Close()
    {
        var overlay = OverlayWindow;
        var toolbar = ToolbarWindow;
        if (toolbar?.BoardActive == true)
        {
            toolbar.SetBoardActive(false);
        }
        UnwireOverlayWindowEvents(overlay);
        UnwireToolbarBehaviorEvents(toolbar);
        UnwireToolbarWindowEvents(toolbar);
        overlay?.Close();
        toolbar?.Close();
        OverlayWindow = null;
        ToolbarWindow = null;
    }

    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ToolbarWindow?.ApplySettings(settings);
        ApplyInitialOverlaySettings(settings, ResolvePreferredPrimaryToolMode());
    }

    public void CaptureToolbarPosition(AppSettings settings, bool save = true)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (ToolbarWindow == null) return;

        settings.PaintToolbarX = (int)Math.Round(ToolbarWindow.Left);
        settings.PaintToolbarY = (int)Math.Round(ToolbarWindow.Top);
        if (save)
        {
            _appSettingsService.Save(settings);
        }
    }

    public void UpdateToggleButtons()
    {
        // Placeholder for logic if needed, or event firing.
        // MainWindow uses this to update its own UI toggle buttons.
    }

    private void ApplyPaintToolbarPosition(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (ToolbarWindow == null) return;

        ToolbarWindow.Left = settings.PaintToolbarX;
        ToolbarWindow.Top = settings.PaintToolbarY;

        if (settings.PaintToolbarX == AppSettings.UnsetPosition
            && settings.PaintToolbarY == AppSettings.UnsetPosition)
        {
            WindowPlacementHelper.CenterOnVirtualScreen(ToolbarWindow);
            return;
        }

        WindowPlacementHelper.EnsureVisible(ToolbarWindow);
    }
}
