using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    private Paint.PaintOverlayWindow? _overlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow;
    private LauncherBubbleWindow? _bubbleWindow;
    private readonly DispatcherTimer _autoExitTimer;
    private bool _allowClose;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    public ICommand OpenRollCallSettingsCommand { get; }
    public ICommand OpenPaintSettingsCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        _settingsService = new AppSettingsService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"));
        _settings = _settingsService.Load();
        _autoExitTimer = new DispatcherTimer();
        _autoExitTimer.Tick += (_, _) =>
        {
            _autoExitTimer.Stop();
            RequestExit();
        };
        OpenRollCallSettingsCommand = new RelayCommand(OnOpenRollCallSettings);
        OpenPaintSettingsCommand = new RelayCommand(OnOpenPaintSettings);
        DataContext = this;
        Loaded += OnLoaded;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
            }
        };
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLauncherPosition();
        WindowPlacementHelper.EnsureVisible(this);
        ScheduleAutoExitTimer();
        if (_settings.LauncherMinimized)
        {
            MinimizeLauncher(fromSettings: true);
        }
        else
        {
            UpdateToggleButtons();
        }
        RunStartupDiagnostics();
    }

    private void OnRollCallClick(object sender, RoutedEventArgs e)
    {
        EnsureRollCallWindow();
        if (_rollCallWindow == null)
        {
            return;
        }
        if (_rollCallWindow.IsVisible)
        {
            _rollCallWindow.Hide();
        }
        else
        {
            _rollCallWindow.Show();
            _rollCallWindow.Activate();
            _rollCallWindow.SyncTopmost(true);
            if (_overlayWindow != null && _overlayWindow.IsVisible)
            {
                _rollCallWindow.Owner = _overlayWindow;
            }
        }
        UpdateToggleButtons();
    }

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
                    _overlayWindow.SetBoardColor(_settings.BoardColor);
                    _overlayWindow.SetBoardOpacity(255);
                }
                else
                {
                    _overlayWindow.SetBoardColor(Colors.Transparent);
                    _overlayWindow.SetBoardOpacity(0);
                }
            }
            if (_rollCallWindow != null && _rollCallWindow.IsVisible)
            {
                _rollCallWindow.Owner = _overlayWindow;
                _rollCallWindow.SyncTopmost(true);
            }
        };
        _toolbarWindow.SettingsRequested += OnOpenPaintSettings;

        _overlayWindow.SetMode(Paint.PaintToolMode.Brush);
        _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
        _overlayWindow.SetBrushStyle(_settings.BrushStyle);
        _overlayWindow.SetBrushTuning(_settings.WhiteboardSmoothMode, _settings.CalligraphySharpMode);
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

    private void EnsureRollCallWindow()
    {
        if (_rollCallWindow != null)
        {
            return;
        }
        var path = ResolveStudentWorkbookPath();
        _rollCallWindow = new RollCallWindow(path, _settingsService, _settings);
        _rollCallWindow.IsVisibleChanged += (_, _) => UpdateToggleButtons();
        _rollCallWindow.Closed += (_, _) =>
        {
            _rollCallWindow = null;
            UpdateToggleButtons();
        };
    }

    private void UpdateToggleButtons()
    {
        if (_overlayWindow != null && _overlayWindow.IsVisible)
        {
            PaintButton.Content = "隐藏画笔";
        }
        else
        {
            PaintButton.Content = "画笔";
        }

        if (_rollCallWindow != null && _rollCallWindow.IsVisible)
        {
            RollCallButton.Content = "隐藏点名";
        }
        else
        {
            RollCallButton.Content = "点名";
        }
    }

    private void OnOpenRollCallSettings()
    {
        var dialog = new RollCallSettingsDialog(_settings, ResolveAvailableClasses())
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _settings.RollCallShowId = dialog.RollCallShowId;
        _settings.RollCallShowName = dialog.RollCallShowName;
        _settings.RollCallShowPhoto = dialog.RollCallShowPhoto;
        _settings.RollCallPhotoDurationSeconds = dialog.RollCallPhotoDurationSeconds;
        _settings.RollCallPhotoSharedClass = dialog.RollCallPhotoSharedClass;
        _settings.RollCallTimerSoundEnabled = dialog.RollCallTimerSoundEnabled;
        _settings.RollCallTimerReminderEnabled = dialog.RollCallTimerReminderEnabled;
        _settings.RollCallTimerReminderIntervalMinutes = dialog.RollCallTimerReminderIntervalMinutes;
        _settings.RollCallTimerSoundVariant = dialog.RollCallTimerSoundVariant;
        _settings.RollCallTimerReminderSoundVariant = dialog.RollCallTimerReminderSoundVariant;
        _settings.RollCallSpeechEnabled = dialog.RollCallSpeechEnabled;
        _settings.RollCallSpeechEngine = dialog.RollCallSpeechEngine;
        _settings.RollCallSpeechVoiceId = dialog.RollCallSpeechVoiceId;
        _settings.RollCallSpeechOutputId = dialog.RollCallSpeechOutputId;
        _settings.RollCallRemoteEnabled = dialog.RollCallRemoteEnabled;
        _settings.RemotePresenterKey = dialog.RemotePresenterKey;
        SaveSettings();
        _rollCallWindow?.ApplySettings(_settings);
    }

    private IReadOnlyList<string> ResolveAvailableClasses()
    {
        return _rollCallWindow?.AvailableClasses ?? Array.Empty<string>();
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
            _settings.WhiteboardSmoothMode = dialog.WhiteboardSmoothMode;
            _settings.CalligraphySharpMode = dialog.CalligraphySharpMode;
            _settings.CalligraphyInkBloomEnabled = dialog.CalligraphyInkBloomEnabled;
            _settings.CalligraphySealEnabled = dialog.CalligraphySealEnabled;
            _settings.CalligraphyOverlayOpacityThreshold = dialog.CalligraphyOverlayOpacityThreshold;
            _settings.EraserSize = dialog.EraserSize;
            _settings.BoardOpacity = 255;
            _settings.ShapeType = dialog.ShapeType;
            _settings.BrushColor = dialog.BrushColor;
            _settings.PaintToolbarScale = dialog.ToolbarScale;
            SaveSettings();

            if (_overlayWindow != null)
            {
                _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
                _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
                _overlayWindow.UpdatePresentationTargets(_settings.ControlMsPpt, _settings.ControlWpsPpt);
                _overlayWindow.UpdatePresentationForegroundPolicy(_settings.ForcePresentationForegroundOnFullscreen);
                _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
                _overlayWindow.SetBrushStyle(_settings.BrushStyle);
                _overlayWindow.SetBrushTuning(_settings.WhiteboardSmoothMode, _settings.CalligraphySharpMode);
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

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeLauncher(fromSettings: false);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnLauncherSettingsClick(object sender, RoutedEventArgs e)
    {
        var currentMinutes = Math.Max(0, _settings.LauncherAutoExitSeconds / 60);
        var dialog = new AutoExitDialog(currentMinutes, _settings)
        {
            Owner = this
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
            System.Diagnostics.Debug.WriteLine("MainWindow: 修复对话框完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow 修复对话框失败: {ex.Message}");
        }
        
        // 使用安全显示方法
        bool? result = null;
        try
        {
            result = dialog.SafeShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"对话框显示失败: {ex.Message}");
            throw;
        }
        
        if (result != true)
        {
            return;
        }
        _settings.LauncherAutoExitSeconds = Math.Max(0, dialog.Minutes) * 60;
        ScheduleAutoExitTimer();
        SaveLauncherSettings();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        RequestExit();
    }

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (IsDragBlocked(e.OriginalSource))
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            return;
        }
        EnsureWithinWorkArea();
        SaveLauncherSettings();
    }

    private bool IsDragBlocked(object? source)
    {
        return FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source as DependencyObject) != null;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T match)
            {
                return match;
            }
            obj = GetParent(obj);
        }
        return null;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is System.Windows.Documents.TextElement textElement)
        {
            return textElement.Parent;
        }
        if (obj is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }
        var parent = VisualTreeHelper.GetParent(obj);
        if (parent == null && obj is FrameworkElement element)
        {
            parent = element.Parent as DependencyObject;
        }
        return parent ?? LogicalTreeHelper.GetParent(obj);
    }

    private static string ResolveStudentWorkbookPath()
    {
        return ClassroomToolkit.App.Helpers.StudentResourceLocator.ResolveStudentWorkbookPath();
    }

    private void ApplyLauncherPosition()
    {
        Left = _settings.LauncherX;
        Top = _settings.LauncherY;
        EnsureWithinWorkArea();
    }

    private void EnsureWithinWorkArea()
    {
        var area = SystemParameters.WorkArea;
        if (Left < area.Left)
        {
            Left = area.Left;
        }
        if (Top < area.Top)
        {
            Top = area.Top;
        }
        if (Left + Width > area.Right)
        {
            Left = area.Right - Width;
        }
        if (Top + Height > area.Bottom)
        {
            Top = area.Bottom - Height;
        }
    }

    private void EnsureBubbleWindow()
    {
        if (_bubbleWindow != null)
        {
            return;
        }
        _bubbleWindow = new LauncherBubbleWindow();
        _bubbleWindow.RestoreRequested += RestoreLauncher;
        _bubbleWindow.PositionChanged += OnBubblePositionChanged;
    }

    private void MinimizeLauncher(bool fromSettings)
    {
        EnsureBubbleWindow();
        if (_bubbleWindow == null)
        {
            return;
        }
        var target = fromSettings
            ? new System.Windows.Point(_settings.LauncherBubbleX, _settings.LauncherBubbleY)
            : new System.Windows.Point(Left + Width / 2, Top + Height / 2);
        _bubbleWindow.PlaceNear(target);
        _bubbleWindow.Show();
        Hide();
        _settings.LauncherMinimized = true;
        SaveLauncherSettings();
    }

    private void RestoreLauncher()
    {
        _settings.LauncherMinimized = false;
        _bubbleWindow?.Hide();
        Show();
        Activate();
        EnsureWithinWorkArea();
        SaveLauncherSettings();
        UpdateToggleButtons();
    }

    private void OnBubblePositionChanged(System.Windows.Point position)
    {
        _settings.LauncherBubbleX = (int)Math.Round(position.X);
        _settings.LauncherBubbleY = (int)Math.Round(position.Y);
        if (_settings.LauncherMinimized)
        {
            SaveLauncherSettings();
        }
    }

    private void ScheduleAutoExitTimer()
    {
        _autoExitTimer.Stop();
        if (_settings.LauncherAutoExitSeconds > 0)
        {
            _autoExitTimer.Interval = TimeSpan.FromSeconds(_settings.LauncherAutoExitSeconds);
            _autoExitTimer.Start();
        }
    }

    private void RunStartupDiagnostics()
    {
        var flag = Environment.GetEnvironmentVariable("CTOOL_NO_STARTUP_DIAG");
        if (string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        _ = Task.Run(() =>
        {
            var result = SystemDiagnostics.CollectQuickDiagnostics(settingsPath);
            if (!result.HasIssues)
            {
                return;
            }
            Dispatcher.Invoke(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }
                var dialog = new DiagnosticsDialog(result)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            });
        });
    }

    private void SaveLauncherSettings()
    {
        _settings.LauncherX = (int)Math.Round(Left);
        _settings.LauncherY = (int)Math.Round(Top);
        if (_bubbleWindow != null)
        {
            _settings.LauncherBubbleX = (int)Math.Round(_bubbleWindow.Left);
            _settings.LauncherBubbleY = (int)Math.Round(_bubbleWindow.Top);
        }
        SaveSettings();
    }

    private void RequestExit()
    {
        if (_allowClose)
        {
            return;
        }
        _allowClose = true;
        CapturePaintToolbarPosition(save: true);
        SaveLauncherSettings();
        if (_bubbleWindow != null)
        {
            _bubbleWindow.Close();
            _bubbleWindow = null;
        }
        if (_rollCallWindow != null)
        {
            _rollCallWindow.RequestClose();
            _rollCallWindow = null;
        }
        if (_overlayWindow != null)
        {
            _overlayWindow.Close();
            _overlayWindow = null;
        }
        if (_toolbarWindow != null)
        {
            _toolbarWindow.Close();
            _toolbarWindow = null;
        }
        System.Windows.Application.Current.Shutdown();
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }
        e.Cancel = true;
        RequestExit();
    }
}
