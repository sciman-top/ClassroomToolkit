using System;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

/// <summary>
/// Paint window management, overlay/toolbar lifecycle, and paint/ink settings.
/// </summary>
public partial class MainWindow
{
    private void OnPaintClick(object sender, RoutedEventArgs e)
    {
        EnsurePaintWindows();
        if (_overlayWindow == null || _toolbarWindow == null)
        {
            return;
        }
        if (_overlayWindow.IsVisible)
        {
            CapturePaintToolbarPosition(save: true);
            _overlayWindow.Hide();
            _toolbarWindow.Hide();
            if (_rollCallWindow != null)
            {
                _rollCallWindow.Owner = null;
                _rollCallWindow.SyncTopmost(true);
            }
        }
        else
        {
            _overlayWindow.Show();
            if (_toolbarWindow.Owner != _overlayWindow && _overlayWindow.IsVisible)
            {
                _toolbarWindow.Owner = _overlayWindow;
            }
            _toolbarWindow.Show();
            WindowPlacementHelper.EnsureVisible(_toolbarWindow);
            _overlayWindow.SetMode(_toolbarWindow.CurrentMode);
            _overlayWindow.RestorePresentationFocusIfNeeded(requireFullscreen: true);
            if (_rollCallWindow != null && _rollCallWindow.IsVisible)
            {
                _rollCallWindow.Owner = _overlayWindow;
                _rollCallWindow.SyncTopmost(true);
            }
        }
        UpdateToggleButtons();
    }

    private void EnsurePaintWindows()
    {
        if (_overlayWindow != null && _toolbarWindow != null)
        {
            return;
        }
        _overlayWindow = new Paint.PaintOverlayWindow();
        _toolbarWindow = new Paint.PaintToolbarWindow();
        _toolbarWindow.AttachOverlay(_overlayWindow);
        _overlayWindow.PhotoModeChanged += OnPhotoModeChanged;
        _overlayWindow.PhotoNavigationRequested += OnPhotoNavigateRequested;
        _overlayWindow.PhotoUnifiedTransformChanged += OnPhotoUnifiedTransformChanged;
        _overlayWindow.PresentationFullscreenDetected += OnPresentationFullscreenDetected;
        _overlayWindow.PresentationForegroundDetected += OnPresentationForegroundDetected;
        _overlayWindow.PhotoForegroundDetected += OnPhotoForegroundDetected;
        _overlayWindow.FloatingZOrderRequested += () =>
            Dispatcher.BeginInvoke(EnsureFloatingWindowsOnTop, System.Windows.Threading.DispatcherPriority.Background);
        _overlayWindow.Activated += (_, _) => OnOverlayActivated();
        _overlayWindow.Closed += (_, _) =>
        {
            _overlayWindow = null;
            UpdateToggleButtons();
        };
        _toolbarWindow.Closed += (_, _) =>
        {
            CapturePaintToolbarPosition(save: true);
            _toolbarWindow = null;
            UpdateToggleButtons();
        };
        _toolbarWindow.LocationChanged += (_, _) => CapturePaintToolbarPosition(save: false);
        ApplyPaintToolbarPosition();
        _toolbarWindow.ApplySettings(_settings);
        _toolbarWindow.ModeChanged += mode =>
        {
            if (_toolbarWindow.HasOverlay)
            {
                return;
            }
            _overlayWindow.SetMode(mode);
        };
        _toolbarWindow.BrushColorChanged += color =>
        {
            if (!_toolbarWindow.HasOverlay)
            {
                _overlayWindow.SetBrush(color, _toolbarWindow.BrushSize, _overlayWindow.CurrentBrushOpacity);
            }
            _settings.BrushColor = color;
            SaveSettings();
        };
        _toolbarWindow.BoardColorChanged += color =>
        {
            _settings.BoardColor = color;
            SaveSettings();
            if (_toolbarWindow.BoardActive && !_toolbarWindow.HasOverlay)
            {
                _overlayWindow.SetBoardColor(color);
                _overlayWindow.SetBoardOpacity(255);
            }
        };
        _toolbarWindow.ClearRequested += () => _overlayWindow.ClearAll();
        _toolbarWindow.UndoRequested += () => _overlayWindow.Undo();
        _toolbarWindow.QuickColorSlotChanged += (index, color) =>
        {
            switch (index)
            {
                case 0:
                    _settings.QuickColor1 = color;
                    break;
                case 1:
                    _settings.QuickColor2 = color;
                    break;
                case 2:
                    _settings.QuickColor3 = color;
                    break;
            }
            SaveSettings();
        };
        _toolbarWindow.ShapeTypeChanged += type =>
        {
            _settings.ShapeType = type;
            SaveSettings();
            if (_overlayWindow != null)
            {
                _overlayWindow.SetShapeType(type);
            }
        };
        _toolbarWindow.WhiteboardToggled += active =>
        {
            if (!_toolbarWindow.HasOverlay)
            {
                if (active)
                {
                    if (_overlayWindow.IsPhotoModeActive)
                    {
                        _overlayWindow.ExitPhotoMode();
                    }
                    _overlayWindow.SetBoardColor(_settings.BoardColor);
                    _overlayWindow.SetBoardOpacity(255);
                }
                else
                {
                    _overlayWindow.SetBoardColor(Colors.Transparent);
                    _overlayWindow.SetBoardOpacity(0);
                }
            }
            if (active)
            {
                TouchSurface(ZOrderSurface.Whiteboard, applyPolicy: false);
            }
            if (_rollCallWindow != null && _rollCallWindow.IsVisible)
            {
                _rollCallWindow.Owner = _overlayWindow;
                _rollCallWindow.SyncTopmost(true);
            }
            ApplyZOrderPolicy();
        };
        _toolbarWindow.SettingsRequested += OnOpenPaintSettings;
        _toolbarWindow.PhotoOpenRequested += OnOpenPhotoTeaching;

        _overlayWindow.SetMode(Paint.PaintToolMode.Brush);
        _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
        _overlayWindow.SetBrushStyle(_settings.BrushStyle);
        _overlayWindow.SetBrushTuning(_settings.WhiteboardPreset, _settings.CalligraphyPreset);
        _overlayWindow.SetCalligraphyOptions(
            _settings.CalligraphyInkBloomEnabled,
            _settings.CalligraphySealEnabled);
        _overlayWindow.SetCalligraphyOverlayOpacityThreshold(_settings.CalligraphyOverlayOpacityThreshold);
        _overlayWindow.SetEraserSize(_settings.EraserSize);
        _overlayWindow.SetShapeType(_settings.ShapeType);
        if (_toolbarWindow.BoardActive)
        {
            _overlayWindow.SetBoardColor(_settings.BoardColor);
            _overlayWindow.SetBoardOpacity(255);
        }
        else
        {
            _overlayWindow.SetBoardColor(Colors.Transparent);
            _overlayWindow.SetBoardOpacity(0);
        }
        _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
        _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
        _overlayWindow.UpdatePresentationTargets(_settings.ControlMsPpt, _settings.ControlWpsPpt);
        _overlayWindow.UpdatePresentationForegroundPolicy(_settings.ForcePresentationForegroundOnFullscreen);
        _overlayWindow.UpdateInkCacheEnabled(_settings.InkCacheEnabled);
        _overlayWindow.UpdateInkRecordEnabled(_settings.InkRecordEnabled);
        _overlayWindow.UpdateInkReplayPreviousEnabled(_settings.InkReplayPreviousEnabled);
        _overlayWindow.UpdateInkRetentionDays(_settings.InkRetentionDays);
        _overlayWindow.UpdateInkPhotoRootPath(_settings.InkPhotoRootPath);
        _overlayWindow.UpdatePhotoTransformMemoryEnabled(_settings.PhotoRememberTransform);
        _overlayWindow.UpdateCrossPageDisplayEnabled(_settings.PhotoCrossPageDisplay);
        _overlayWindow.SetPhotoUnifiedTransformState(
            _settings.PhotoUnifiedTransformEnabled,
            _settings.PhotoUnifiedScaleX,
            _settings.PhotoUnifiedScaleY,
            _settings.PhotoUnifiedTranslateX,
            _settings.PhotoUnifiedTranslateY);
    }

    private void ApplyPaintToolbarPosition()
    {
        if (_toolbarWindow == null)
        {
            return;
        }
        _toolbarWindow.Left = _settings.PaintToolbarX;
        _toolbarWindow.Top = _settings.PaintToolbarY;
        if (_settings.PaintToolbarX == AppSettings.UnsetPosition
            && _settings.PaintToolbarY == AppSettings.UnsetPosition)
        {
            WindowPlacementHelper.CenterOnVirtualScreen(_toolbarWindow);
            return;
        }
        WindowPlacementHelper.EnsureVisible(_toolbarWindow);
    }

    private void CapturePaintToolbarPosition(bool save)
    {
        if (_toolbarWindow == null)
        {
            return;
        }
        _settings.PaintToolbarX = (int)Math.Round(_toolbarWindow.Left);
        _settings.PaintToolbarY = (int)Math.Round(_toolbarWindow.Top);
        if (save)
        {
            SaveSettings();
        }
    }

    private void ShowPaintOverlayIfNeeded()
    {
        if (_overlayWindow == null || _toolbarWindow == null)
        {
            return;
        }
        if (_overlayWindow.IsVisible)
        {
            return;
        }
        _overlayWindow.Show();
        if (_toolbarWindow.Owner != _overlayWindow && _overlayWindow.IsVisible)
        {
            _toolbarWindow.Owner = _overlayWindow;
        }
        _toolbarWindow.Show();
        WindowPlacementHelper.EnsureVisible(_toolbarWindow);
        _overlayWindow.SetMode(_toolbarWindow.CurrentMode);
        if (_rollCallWindow != null && _rollCallWindow.IsVisible)
        {
            _rollCallWindow.Owner = _overlayWindow;
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
            _settings.ForcePresentationForegroundOnFullscreen = dialog.ForcePresentationForegroundOnFullscreen;
            _settings.BrushSize = dialog.BrushSize;
            _settings.BrushOpacity = dialog.BrushOpacity;
            _settings.BrushStyle = dialog.BrushStyle;
            _settings.WhiteboardPreset = dialog.WhiteboardPreset;
            _settings.CalligraphyPreset = dialog.CalligraphyPreset;
            _settings.CalligraphyInkBloomEnabled = dialog.CalligraphyInkBloomEnabled;
            _settings.CalligraphySealEnabled = dialog.CalligraphySealEnabled;
            _settings.CalligraphyOverlayOpacityThreshold = dialog.CalligraphyOverlayOpacityThreshold;
            _settings.EraserSize = dialog.EraserSize;
            _settings.BoardOpacity = 255;
            _settings.ShapeType = dialog.ShapeType;
            _settings.BrushColor = dialog.BrushColor;
            _settings.PaintToolbarScale = dialog.ToolbarScale;
            _settings.InkCacheEnabled = dialog.InkCacheEnabled;
            _settings.PhotoRememberTransform = dialog.PhotoRememberTransform;
            _settings.PhotoCrossPageDisplay = dialog.PhotoCrossPageDisplay;
            SaveSettings();

            if (_overlayWindow != null)
            {
                _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
                _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
                _overlayWindow.UpdatePresentationTargets(_settings.ControlMsPpt, _settings.ControlWpsPpt);
                _overlayWindow.UpdatePresentationForegroundPolicy(_settings.ForcePresentationForegroundOnFullscreen);
                _overlayWindow.UpdateInkCacheEnabled(_settings.InkCacheEnabled);
                _overlayWindow.UpdateInkRecordEnabled(_settings.InkRecordEnabled);
                _overlayWindow.UpdateInkReplayPreviousEnabled(_settings.InkReplayPreviousEnabled);
                _overlayWindow.UpdateInkRetentionDays(_settings.InkRetentionDays);
                _overlayWindow.UpdateInkPhotoRootPath(_settings.InkPhotoRootPath);
                _overlayWindow.UpdatePhotoTransformMemoryEnabled(_settings.PhotoRememberTransform);
                _overlayWindow.UpdateCrossPageDisplayEnabled(_settings.PhotoCrossPageDisplay);
                _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
                _overlayWindow.SetBrushStyle(_settings.BrushStyle);
                _overlayWindow.SetBrushTuning(_settings.WhiteboardPreset, _settings.CalligraphyPreset);
                _overlayWindow.SetCalligraphyOptions(
                    _settings.CalligraphyInkBloomEnabled,
                    _settings.CalligraphySealEnabled);
                _overlayWindow.SetCalligraphyOverlayOpacityThreshold(_settings.CalligraphyOverlayOpacityThreshold);
                _overlayWindow.SetEraserSize(_settings.EraserSize);
                _overlayWindow.SetShapeType(_settings.ShapeType);
                _overlayWindow.SetMode(_settings.ShapeType == Paint.PaintShapeType.None
                    ? Paint.PaintToolMode.Brush
                    : Paint.PaintToolMode.Shape);
                if (_toolbarWindow?.BoardActive == true)
                {
                    _overlayWindow.SetBoardColor(_settings.BoardColor);
                    _overlayWindow.SetBoardOpacity(255);
                }
            }
            _toolbarWindow?.ApplySettings(_settings);
        }
        _overlayWindow?.RestorePresentationFocusIfNeeded(requireFullscreen: true);
    }

    private void OnOpenInkSettings()
    {
        var dialog = new Ink.InkSettingsDialog(_settings)
        {
            Owner = _overlayWindow != null && _overlayWindow.IsVisible ? (Window)_overlayWindow : this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _settings.InkRecordEnabled = dialog.InkRecordEnabled;
        _settings.InkReplayPreviousEnabled = dialog.InkReplayPreviousEnabled;
        _settings.InkRetentionDays = dialog.InkRetentionDays;
        _settings.InkPhotoRootPath = dialog.InkPhotoRootPath;
        SaveSettings();

        if (_overlayWindow != null)
        {
            _overlayWindow.UpdateInkCacheEnabled(_settings.InkCacheEnabled);
        }
        _toolbarWindow?.ApplySettings(_settings);
    }
}
