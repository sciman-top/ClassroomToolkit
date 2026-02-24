using System;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
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
        
        if (overlay.IsVisible)
        {
            _paintWindowOrchestrator.CaptureToolbarPosition(_settings, save: true);
            _paintWindowOrchestrator.Hide();
            
            if (_rollCallWindow != null)
            {
                _rollCallWindow.Owner = null;
                _rollCallWindow.SyncTopmost(true);
            }
        }
        else
        {
            _paintWindowOrchestrator.Show();
            
            if (_rollCallWindow != null && _rollCallWindow.IsVisible)
            {
                _rollCallWindow.Owner = overlay;
                _rollCallWindow.SyncTopmost(true);
            }
        }
        UpdateToggleButtons();
    }

    private void EnsurePaintWindows()
    {
        if (_paintWindowOrchestrator.OverlayWindow != null)
        {
            return;
        }

        _paintWindowOrchestrator.EnsureWindows(_settings);

        // Re-wire events for Z-Order and other MainWindow specific logic
        _paintWindowOrchestrator.PhotoModeChanged += OnPhotoModeChanged;
        _paintWindowOrchestrator.PhotoNavigationRequested += OnPhotoNavigateRequested;
        _paintWindowOrchestrator.PhotoUnifiedTransformChanged += OnPhotoUnifiedTransformChanged;
        _paintWindowOrchestrator.PresentationFullscreenDetected += OnPresentationFullscreenDetected;
        _paintWindowOrchestrator.PresentationForegroundDetected += OnPresentationForegroundDetected;
        _paintWindowOrchestrator.PhotoForegroundDetected += OnPhotoForegroundDetected;
        _paintWindowOrchestrator.PhotoCloseRequested += OnPhotoCloseRequested;
        _paintWindowOrchestrator.FloatingZOrderRequested += () =>
            Dispatcher.BeginInvoke(EnsureFloatingWindowsOnTop, System.Windows.Threading.DispatcherPriority.Background);
        _paintWindowOrchestrator.OverlayActivated += OnOverlayActivated;
        _paintWindowOrchestrator.SettingsRequested += OnOpenPaintSettings;
        _paintWindowOrchestrator.PhotoOpenRequested += OnOpenPhotoTeaching;
        
        // Note: Closed events are handled by Orchestrator internally for its own cleanup, 
        // but we might need to know when they close to update toggle buttons.
        // The Orchestrator property OverlayWindow becomes null when closed.
        // We can hook into Orchestrator events if we added 'Closed' event there, or just rely on the fact 
        // that MainWindow polls properties in UpdateToggleButtons.
        // Actually, Orchestrator doesn't expose a generic 'Closed' event for MainWindow to update UI.
        // I should add that to Orchestrator or just hook the window events via the property (which is null checked).
        // BUT, since we just created them, we know they are not null.
        
        if (_paintWindowOrchestrator.OverlayWindow != null)
        {
             _paintWindowOrchestrator.OverlayWindow.Closed += (_, _) => UpdateToggleButtons();
        }
        if (_paintWindowOrchestrator.ToolbarWindow != null)
        {
             _paintWindowOrchestrator.ToolbarWindow.Closed += (_, _) => UpdateToggleButtons();
        }
    }

    // ApplyPaintToolbarPosition moved to Orchestrator

    private void CapturePaintToolbarPosition(bool save)
    {
        _paintWindowOrchestrator.CaptureToolbarPosition(_settings, save);
    }

    private void ShowPaintOverlayIfNeeded()
    {
        if (_paintWindowOrchestrator.OverlayWindow == null)
        {
            return;
        }
        
        _paintWindowOrchestrator.Show();

        // Ensure Z-Order linkage
        if (_rollCallWindow != null && _rollCallWindow.IsVisible)
        {
            _rollCallWindow.Owner = _paintWindowOrchestrator.OverlayWindow;
            _rollCallWindow.SyncTopmost(true);
        }
    }

    private void OnOpenPaintSettings()
    {
        var dialog = new Paint.PaintSettingsDialog(_settings)
        {
            Owner = _toolbarWindow != null ? (Window)_toolbarWindow : this
        };
        
        // 先修复当前窗口
        try
        {
            BorderFixHelper.FixAllBorders(this);
            System.Diagnostics.Debug.WriteLine("MainWindow: 修复当前窗口完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow 修复失败: {ex.Message}");
        }
        
        // 立即修复新创建的对话框
        try
        {
            BorderFixHelper.FixAllBorders(dialog);
            System.Diagnostics.Debug.WriteLine("MainWindow: 修复 PaintSettingsDialog 完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow 修复 PaintSettingsDialog 失败: {ex.Message}");
        }
        
        // 使用安全显示方法
        bool? result = null;
        try
        {
            result = dialog.SafeShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PaintSettingsDialog 显示失败: {ex.Message}");
            throw;
        }
        
        var applied = result == true;
        if (applied)
        {
            _settings.ControlMsPpt = dialog.ControlMsPpt;
            _settings.ControlWpsPpt = dialog.ControlWpsPpt;
            _settings.WpsInputMode = dialog.WpsInputMode;
            _settings.WpsWheelForward = dialog.WpsWheelForward;
            _settings.WpsDebounceMs = dialog.WpsDebounceMs;
            _settings.PresentationLockStrategyWhenDegraded = dialog.PresentationLockStrategyWhenDegraded;
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
            SaveSettings();
            _inkExportOptions.Scope = _settings.InkExportScope;
            _inkExportOptions.MaxParallelFiles = _settings.InkExportMaxParallelFiles;

            _paintWindowOrchestrator.ApplySettings(_settings);
        }
        _paintWindowOrchestrator.OverlayWindow?.RestorePresentationFocusIfNeeded(requireFullscreen: true);
    }

    private void OnPhotoCloseRequested()
    {
        // 断开工具条和点名窗口的Owner关系，避免关闭图片模式时受 owner 链影响
        if (_toolbarWindow != null && _toolbarWindow.Owner == _overlayWindow)
        {
            _toolbarWindow.Owner = null;
        }
        if (_rollCallWindow != null && _rollCallWindow.Owner == _overlayWindow)
        {
            _rollCallWindow.Owner = null;
        }
    }
}




