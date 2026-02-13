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
using ClassroomToolkit.App.Settings;
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
    private Paint.PaintOverlayWindow? _overlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow;
    private Photos.ImageManagerWindow? _imageManagerWindow;
    private readonly List<ZOrderSurface> _surfaceStack = new();
    private readonly DispatcherTimer _presentationForegroundSuppressionTimer;
    private IDisposable? _presentationForegroundSuppression;
    private bool _zOrderPolicyApplying;
    private List<string> _photoSequence = new();
    private int _photoSequenceIndex = -1;
    private LauncherBubbleWindow? _bubbleWindow;
    private readonly DispatcherTimer _autoExitTimer;
    private readonly CancellationTokenSource _backgroundTasksCancellation = new();
    private bool _allowClose;
    private bool _settingsSaveFailedNotified;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly MainViewModel _mainViewModel;
    private readonly IRollCallWindowFactory _rollCallWindowFactory;
    private readonly Paint.IPaintWindowFactory _paintWindowFactory;
    private readonly Photos.IImageManagerWindowFactory _imageManagerWindowFactory;
    private readonly IWindowOrchestrator _windowOrchestrator;
    public MainWindow(
        AppSettingsService settingsService,
        AppSettings settings,
        MainViewModel mainViewModel,
        IRollCallWindowFactory rollCallWindowFactory,
        Paint.IPaintWindowFactory paintWindowFactory,
        Photos.IImageManagerWindowFactory imageManagerWindowFactory,
        IWindowOrchestrator windowOrchestrator)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _mainViewModel = mainViewModel;
        _rollCallWindowFactory = rollCallWindowFactory;
        _paintWindowFactory = paintWindowFactory;
        _imageManagerWindowFactory = imageManagerWindowFactory;
        _windowOrchestrator = windowOrchestrator;
        _autoExitTimer = new DispatcherTimer();
        _autoExitTimer.Tick += (_, _) =>
        {
            _autoExitTimer.Stop();
            RequestExit();
        };
        _presentationForegroundSuppressionTimer = new DispatcherTimer();
        _presentationForegroundSuppressionTimer.Tick += (_, _) => ReleasePresentationForegroundSuppression();
        _mainViewModel.OpenRollCallSettingsCommand = new RelayCommand(OnOpenRollCallSettings);
        _mainViewModel.OpenPaintSettingsCommand = new RelayCommand(OnOpenPaintSettings);
        DataContext = _mainViewModel;
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
        ScheduleInkCleanup();
        WarmupRollCallData();
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

    private void WarmupRollCallData()
    {
        try
        {
            var path = ResolveStudentWorkbookPath();
            ClassroomToolkit.App.ViewModels.RollCallViewModel.WarmupData(path);
        }
        catch
        {
            // Ignore warmup failures.
        }
    }

    // ── Roll-call ──

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
        ApplyZOrderPolicy();
        UpdateToggleButtons();
    }

    private void EnsureRollCallWindow()
    {
        if (_rollCallWindow != null)
        {
            return;
        }
        var path = ResolveStudentWorkbookPath();
        _rollCallWindow = _rollCallWindowFactory.Create(path);
        _rollCallWindow.IsVisibleChanged += (_, _) => UpdateToggleButtons();
        _rollCallWindow.Closed += (_, _) =>
        {
            _rollCallWindow = null;
            UpdateToggleButtons();
        };
    }

    private void UpdateToggleButtons()
    {
        _mainViewModel.IsPaintActive = _overlayWindow != null && _overlayWindow.IsVisible;
        _mainViewModel.IsRollCallVisible = _rollCallWindow != null && _rollCallWindow.IsVisible;
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
        _windowOrchestrator.TouchSurface(_surfaceStack, surface);
        if (applyPolicy)
        {
            ApplyZOrderPolicy();
        }
    }

    private void ApplyZOrderPolicy()
    {
        if (_zOrderPolicyApplying)
        {
            return;
        }
        _zOrderPolicyApplying = true;
        try
        {
            var overlayVisible = _overlayWindow?.IsVisible == true;
            var photoActive = _overlayWindow?.IsPhotoModeActive == true;
            var presentationFullscreen = _overlayWindow?.IsPresentationFullscreenActive == true;
            var whiteboardActive = _toolbarWindow?.BoardActive == true;
            var imageManagerVisible = _imageManagerWindow?.IsVisible == true
                && _imageManagerWindow.WindowState != WindowState.Minimized;

            _windowOrchestrator.PruneSurfaceStack(_surfaceStack, photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible);
            var frontSurface = _windowOrchestrator.ResolveFrontSurface(_surfaceStack, photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible);
            SyncFloatingWindowOwners(overlayVisible);

            if (_imageManagerWindow != null && imageManagerVisible)
            {
                _imageManagerWindow.SyncTopmost(true);
                if (frontSurface == ZOrderSurface.ImageManager && !_imageManagerWindow.IsActive)
                {
                    _imageManagerWindow.Activate();
                }
            }

            if (_overlayWindow != null && overlayVisible)
            {
                if (frontSurface is ZOrderSurface.PhotoFullscreen or ZOrderSurface.Whiteboard)
                {
                    _overlayWindow.Activate();
                }
            }

            if (_toolbarWindow != null && _toolbarWindow.IsVisible)
            {
                _toolbarWindow.SyncTopmost(true);
            }
            if (_rollCallWindow != null && _rollCallWindow.IsVisible)
            {
                _rollCallWindow.SyncTopmost(true);
            }
        }
        finally
        {
            _zOrderPolicyApplying = false;
        }
    }
    private void SyncFloatingWindowOwners(bool overlayVisible)
    {
        var overlay = _overlayWindow;
        if (_toolbarWindow != null && _toolbarWindow.IsVisible)
        {
            if (overlayVisible && overlay != null)
            {
                if (_toolbarWindow.Owner != overlay)
                {
                    _toolbarWindow.Owner = overlay;
                }
            }
            else if (_toolbarWindow.Owner != null)
            {
                _toolbarWindow.Owner = null;
            }
        }
        if (_rollCallWindow != null && _rollCallWindow.IsVisible)
        {
            if (overlayVisible && overlay != null)
            {
                if (_rollCallWindow.Owner != overlay)
                {
                    _rollCallWindow.Owner = overlay;
                }
            }
            else if (_rollCallWindow.Owner != null)
            {
                _rollCallWindow.Owner = null;
            }
        }
    }

    // ── Lifecycle & settings ──

    private void RequestExit()
    {
        if (_allowClose)
        {
            return;
        }
        _allowClose = true;
        if (!_backgroundTasksCancellation.IsCancellationRequested)
        {
            _backgroundTasksCancellation.Cancel();
        }
        TriggerInkCleanup();
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
            try { _overlayWindow.Close(); } catch { /* ignore during shutdown */ }
            _overlayWindow = null;
        }
        if (_toolbarWindow != null)
        {
            try { _toolbarWindow.Close(); } catch { /* ignore during shutdown */ }
            _toolbarWindow = null;
        }
        System.Windows.Application.Current.Shutdown();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(_settings);
            _settingsSaveFailedNotified = false;
        }
        catch (Exception ex)
        {
            if (_settingsSaveFailedNotified)
            {
                return;
            }
            _settingsSaveFailedNotified = true;
            var detail = $"设置保存失败：{ex.Message}\n请检查设置文件权限或磁盘状态。";
            System.Windows.MessageBox.Show(this, detail, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ScheduleInkCleanup()
    {
        // Ink persistence is disabled; no cleanup needed.
    }

    private void TriggerInkCleanup()
    {
        // Ink persistence is disabled; no cleanup needed.
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



















