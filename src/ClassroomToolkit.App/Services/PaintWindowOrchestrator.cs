using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Interop.Presentation;
using Microsoft.Extensions.Logging;

namespace ClassroomToolkit.App.Services;

public interface IPaintWindowOrchestrator
{
    PaintOverlayWindow? OverlayWindow { get; }
    PaintToolbarWindow? ToolbarWindow { get; }
    
    event Action<bool>? PhotoModeChanged;
    event Action<int>? PhotoNavigationRequested;
    event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    event Action? PresentationFullscreenDetected;
    event Action<PresentationType>? PresentationForegroundDetected;
    event Action? PhotoForegroundDetected;
    event Action? PhotoCloseRequested;
    event Action? FloatingZOrderRequested;
    event Action? OverlayActivated;
    event Action? SettingsRequested;
    event Action? PhotoOpenRequested;

    void EnsureWindows(AppSettings settings);
    void Show(bool photoMode = false);
    void Hide();
    void ApplySettings(AppSettings settings);
    void CaptureToolbarPosition(AppSettings settings, bool save = true);
    void UpdateToggleButtons();
    void Close();
}

public class PaintWindowOrchestrator : IPaintWindowOrchestrator
{
    private readonly IPaintWindowFactory _paintWindowFactory;
    private readonly AppSettingsService _appSettingsService;
    private readonly ILogger<PaintWindowOrchestrator> _logger;

    public PaintOverlayWindow? OverlayWindow { get; private set; }
    public PaintToolbarWindow? ToolbarWindow { get; private set; }

    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action? PresentationFullscreenDetected;
    public event Action<PresentationType>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;
    public event Action? PhotoCloseRequested;
    public event Action? FloatingZOrderRequested;
    public event Action? OverlayActivated;
    public event Action? SettingsRequested;
    public event Action? PhotoOpenRequested;

    public PaintWindowOrchestrator(
        IPaintWindowFactory paintWindowFactory,
        AppSettingsService appSettingsService,
        ILogger<PaintWindowOrchestrator> logger)
    {
        _paintWindowFactory = paintWindowFactory;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public void EnsureWindows(AppSettings settings)
    {
        if (OverlayWindow != null && ToolbarWindow != null)
        {
            return;
        }

        _logger.LogInformation("Creating Paint Overlay and Toolbar windows.");
        var windows = _paintWindowFactory.Create();
        OverlayWindow = windows.overlay;
        ToolbarWindow = windows.toolbar;

        ToolbarWindow.AttachOverlay(OverlayWindow);

        // Forward Overlay Events
        OverlayWindow.PhotoModeChanged += active => PhotoModeChanged?.Invoke(active);
        OverlayWindow.PhotoNavigationRequested += dir => PhotoNavigationRequested?.Invoke(dir);
        OverlayWindow.PhotoUnifiedTransformChanged += (sx, sy, tx, ty) => PhotoUnifiedTransformChanged?.Invoke(sx, sy, tx, ty);
        OverlayWindow.PresentationFullscreenDetected += () => PresentationFullscreenDetected?.Invoke();
        OverlayWindow.PresentationForegroundDetected += type => PresentationForegroundDetected?.Invoke(type);
        OverlayWindow.PhotoForegroundDetected += () => PhotoForegroundDetected?.Invoke();
        OverlayWindow.PhotoCloseRequested += () => PhotoCloseRequested?.Invoke();
        OverlayWindow.FloatingZOrderRequested += () => FloatingZOrderRequested?.Invoke();
        OverlayWindow.Activated += (_, _) => OverlayActivated?.Invoke();
        
        // Handle Window Closing
        OverlayWindow.Closed += (_, _) =>
        {
            OverlayWindow = null;
            UpdateToggleButtons();
        };
        ToolbarWindow.Closed += (_, _) =>
        {
            if (ToolbarWindow != null)
            {
                CaptureToolbarPosition(settings, save: true);
            }
            ToolbarWindow = null;
            UpdateToggleButtons();
        };

        ToolbarWindow.LocationChanged += (_, _) => CaptureToolbarPosition(settings, save: false);

        ApplyPaintToolbarPosition(settings);
        ToolbarWindow.ApplySettings(settings);

        // Wiring Toolbar -> Overlay interaction
        ToolbarWindow.ModeChanged += mode =>
        {
            if (ToolbarWindow.HasOverlay) return;
            OverlayWindow.SetMode(mode);
        };
        ToolbarWindow.BrushColorChanged += color =>
        {
            if (!ToolbarWindow.HasOverlay)
            {
                OverlayWindow.SetBrush(color, ToolbarWindow.BrushSize, OverlayWindow.CurrentBrushOpacity);
            }
            settings.BrushColor = color;
            _appSettingsService.Save(settings);
        };
        ToolbarWindow.BoardColorChanged += color =>
        {
            settings.BoardColor = color;
            _appSettingsService.Save(settings);
            if (ToolbarWindow.BoardActive && !ToolbarWindow.HasOverlay)
            {
                OverlayWindow.SetBoardColor(color);
                OverlayWindow.SetBoardOpacity(255);
            }
        };
        ToolbarWindow.ClearRequested += () => OverlayWindow.ClearAll();
        ToolbarWindow.UndoRequested += () => OverlayWindow.Undo();
        ToolbarWindow.QuickColorSlotChanged += (index, color) =>
        {
            switch (index)
            {
                case 0: settings.QuickColor1 = color; break;
                case 1: settings.QuickColor2 = color; break;
                case 2: settings.QuickColor3 = color; break;
            }
            _appSettingsService.Save(settings);
        };
        ToolbarWindow.ShapeTypeChanged += type =>
        {
            settings.ShapeType = type;
            _appSettingsService.Save(settings);
            OverlayWindow?.SetShapeType(type);
        };
        ToolbarWindow.WhiteboardToggled += active =>
        {
            if (!ToolbarWindow.HasOverlay)
            {
                if (active)
                {
                    if (OverlayWindow.IsPhotoModeActive) OverlayWindow.ExitPhotoMode();
                    OverlayWindow.SetBoardColor(settings.BoardColor);
                    OverlayWindow.SetBoardOpacity(255);
                }
                else
                {
                    OverlayWindow.SetBoardColor(Colors.Transparent);
                    OverlayWindow.SetBoardOpacity(0);
                }
            }
            // Note: ZOrder and TouchSurface logic relies on events propagating back to MainWindow via this Orchestrator or direct binding.
            // But here we are inside Orchestrator. We might need to expose an event for WhiteboardToggled if MainWindow does extra stuff.
            // Accessing OverlayActivated event might cover z-order needs.
        };

        // Forward Settings Requests
        ToolbarWindow.SettingsRequested += () => SettingsRequested?.Invoke();
        ToolbarWindow.PhotoOpenRequested += () => PhotoOpenRequested?.Invoke();
        
        ApplyInitialOverlaySettings(settings);
    }

    private void ApplyInitialOverlaySettings(AppSettings settings)
    {
        if (OverlayWindow == null || ToolbarWindow == null) return;
        
        OverlayWindow.SetMode(PaintToolMode.Brush);
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
        
        if (ToolbarWindow.BoardActive)
        {
            OverlayWindow.SetBoardColor(settings.BoardColor);
            OverlayWindow.SetBoardOpacity(255);
        }
        else
        {
            OverlayWindow.SetBoardColor(Colors.Transparent);
            OverlayWindow.SetBoardOpacity(0);
        }

        OverlayWindow.UpdateWpsMode(settings.WpsInputMode);
        OverlayWindow.UpdateWpsWheelMapping(settings.WpsWheelForward);
        OverlayWindow.UpdateWpsDebounceMs(settings.WpsDebounceMs);
        OverlayWindow.UpdatePresentationDegradeLock(settings.PresentationLockStrategyWhenDegraded);
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
        OverlayWindow.SetPhotoUnifiedTransformState(
            settings.PhotoUnifiedTransformEnabled,
            settings.PhotoUnifiedScaleX,
            settings.PhotoUnifiedScaleY,
            settings.PhotoUnifiedTranslateX,
            settings.PhotoUnifiedTranslateY);
    }

    public void Show(bool photoMode = false)
    {
        if (OverlayWindow == null) return;
        
        OverlayWindow.Show();
        if (ToolbarWindow != null)
        {
            if (ToolbarWindow.Owner != OverlayWindow && OverlayWindow.IsVisible)
            {
                ToolbarWindow.Owner = OverlayWindow;
            }
            ToolbarWindow.Show();
            WindowPlacementHelper.EnsureVisible(ToolbarWindow);
            OverlayWindow.SetMode(ToolbarWindow.CurrentMode);
        }
        
        OverlayWindow.RestorePresentationFocusIfNeeded(requireFullscreen: true);
    }

    public void Hide()
    {
        if (OverlayWindow == null) return;
        
        if (OverlayWindow.IsVisible)
        {
            OverlayWindow.Hide();
            ToolbarWindow?.Hide();
        }
    }

    public void Close()
    {
        OverlayWindow?.Close();
        ToolbarWindow?.Close();
        OverlayWindow = null;
        ToolbarWindow = null;
    }

    public void ApplySettings(AppSettings settings)
    {
        ApplyInitialOverlaySettings(settings);
        ToolbarWindow?.ApplySettings(settings);
    }

    public void CaptureToolbarPosition(AppSettings settings, bool save = true)
    {
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
