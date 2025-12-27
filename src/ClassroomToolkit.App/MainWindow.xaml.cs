using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
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
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLauncherPosition();
        UpdateButtonMetrics();
        ScheduleAutoExitTimer();
        if (_settings.LauncherMinimized)
        {
            MinimizeLauncher(fromSettings: true);
        }
        else
        {
            UpdateToggleButtons();
        }
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
        }
        else
        {
            _overlayWindow.Show();
            _toolbarWindow.Show();
            _overlayWindow.Activate();
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
        _toolbarWindow.ModeChanged += mode => _overlayWindow.SetMode(mode);
        _toolbarWindow.ShapeTypeChanged += type =>
        {
            _overlayWindow.SetShapeType(type);
            _settings.ShapeType = type;
            SaveSettings();
        };
        _toolbarWindow.BrushColorChanged += color =>
        {
            _overlayWindow.SetBrush(color, _toolbarWindow.BrushSize, _overlayWindow.CurrentBrushOpacity);
            _settings.BrushColor = color;
            SaveSettings();
        };
        _toolbarWindow.BoardColorChanged += color =>
        {
            _overlayWindow.SetBoardColor(color);
            _settings.BoardColor = color;
            SaveSettings();
        };
        _toolbarWindow.BrushSizeChanged += size =>
        {
            _overlayWindow.SetBrush(_overlayWindow.CurrentBrushColor, size, _overlayWindow.CurrentBrushOpacity);
            _settings.BrushSize = size;
            SaveSettings();
        };
        _toolbarWindow.EraserSizeChanged += size =>
        {
            _overlayWindow.SetEraserSize(size);
            _settings.EraserSize = size;
            SaveSettings();
        };
        _toolbarWindow.ClearRequested += () => _overlayWindow.ClearAll();
        _toolbarWindow.UndoRequested += () => _overlayWindow.Undo();
        _toolbarWindow.BrushOpacityChanged += opacity =>
        {
            _overlayWindow.SetBrushOpacity(opacity);
            _settings.BrushOpacity = opacity;
            SaveSettings();
        };
        _toolbarWindow.BoardOpacityChanged += opacity =>
        {
            _overlayWindow.SetBoardOpacity(opacity);
            _settings.BoardOpacity = opacity;
            SaveSettings();
        };
        _toolbarWindow.WpsModeChanged += mode =>
        {
            _overlayWindow.UpdateWpsMode(mode);
            _settings.WpsInputMode = mode;
            SaveSettings();
        };
        _toolbarWindow.WpsWheelMappingChanged += enabled =>
        {
            _overlayWindow.UpdateWpsWheelMapping(enabled);
            _settings.WpsWheelForward = enabled;
            SaveSettings();
        };

        _overlayWindow.SetMode(Paint.PaintToolMode.Brush);
        _overlayWindow.SetBrush(_settings.BrushColor, _settings.BrushSize, _settings.BrushOpacity);
        _overlayWindow.SetEraserSize(_settings.EraserSize);
        _overlayWindow.SetShapeType(_settings.ShapeType);
        _overlayWindow.SetBoardColor(_settings.BoardColor);
        _overlayWindow.SetBoardOpacity(_settings.BoardOpacity);
        _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
        _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
        _overlayWindow.UpdatePresentationTargets(_settings.ControlMsPpt, _settings.ControlWpsPpt);
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
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "students.xlsx");
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
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _settings.ControlMsPpt = dialog.ControlMsPpt;
        _settings.ControlWpsPpt = dialog.ControlWpsPpt;
        _settings.WpsInputMode = dialog.WpsInputMode;
        _settings.WpsWheelForward = dialog.WpsWheelForward;
        SaveSettings();

        if (_overlayWindow != null)
        {
            _overlayWindow.UpdateWpsMode(_settings.WpsInputMode);
            _overlayWindow.UpdateWpsWheelMapping(_settings.WpsWheelForward);
            _overlayWindow.UpdatePresentationTargets(_settings.ControlMsPpt, _settings.ControlWpsPpt);
        }
        _toolbarWindow?.ApplySettings(_settings);
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
        var dialog = new AutoExitDialog(currentMinutes)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
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
        var button = FindAncestor<System.Windows.Controls.Button>(source as DependencyObject);
        return button != null;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T match)
            {
                return match;
            }
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
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

    private void UpdateButtonMetrics()
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var paintWidth = MeasureTextWidth("画笔", PaintButton, dpi);
        paintWidth = Math.Max(paintWidth, MeasureTextWidth("隐藏画笔", PaintButton, dpi));
        var rollWidth = MeasureTextWidth("点名/计时", RollCallButton, dpi);
        rollWidth = Math.Max(rollWidth, MeasureTextWidth("显示点名", RollCallButton, dpi));
        rollWidth = Math.Max(rollWidth, MeasureTextWidth("隐藏点名", RollCallButton, dpi));
        var unifiedWidth = Math.Max(paintWidth, rollWidth) + 28;
        PaintButton.Width = unifiedWidth;
        RollCallButton.Width = unifiedWidth;

        var minWidth = Math.Max(52, MeasureTextWidth("缩小", MinimizeButton, dpi) + 24);
        MinimizeButton.Width = minWidth;

        var infoWidth = Math.Max(52, new[]
        {
            MeasureTextWidth("关于", AboutButton, dpi),
            MeasureTextWidth("设置", SettingsButton, dpi),
            MeasureTextWidth("退出", ExitButton, dpi)
        }.Max() + 24);
        AboutButton.Width = infoWidth;
        SettingsButton.Width = infoWidth;
        ExitButton.Width = infoWidth;

        var buttons = new[] { PaintButton, RollCallButton, MinimizeButton, AboutButton, SettingsButton, ExitButton };
        var maxHeight = buttons.Max(button => button.ActualHeight);
        if (maxHeight <= 0)
        {
            return;
        }
        foreach (var button in buttons)
        {
            button.Height = maxHeight;
        }
    }

    private static double MeasureTextWidth(string text, System.Windows.Controls.Button button, double pixelsPerDip)
    {
        var typeface = new Typeface(button.FontFamily, button.FontStyle, button.FontWeight, button.FontStretch);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            button.FontSize,
            System.Windows.Media.Brushes.Black,
            pixelsPerDip);
        return formatted.Width;
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
