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

namespace ClassroomToolkit.App;

/// <summary>
/// MainWindow core: fields, constructor, lifecycle, roll-call, Z-order policy, and settings persistence.
/// Paint/Ink logic → MainWindow.Paint.cs
/// Photo/Presentation logic → MainWindow.Photo.cs
/// Launcher UI logic → MainWindow.Launcher.cs
/// </summary>
public partial class MainWindow : Window
{
    private enum ZOrderSurface
    {
        None,
        PresentationFullscreen,
        PhotoFullscreen,
        Whiteboard,
        ImageManager
    }

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
        _presentationForegroundSuppressionTimer = new DispatcherTimer();
        _presentationForegroundSuppressionTimer.Tick += (_, _) => ReleasePresentationForegroundSuppression();
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
        if (surface == ZOrderSurface.None)
        {
            return;
        }
        _surfaceStack.Remove(surface);
        _surfaceStack.Add(surface);
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

            PruneSurfaceStack(photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible);
            var frontSurface = ResolveFrontSurface(photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible);
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

    private ZOrderSurface ResolveFrontSurface(
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        for (int i = _surfaceStack.Count - 1; i >= 0; i--)
        {
            var surface = _surfaceStack[i];
            if (IsSurfaceActive(surface, photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible))
            {
                return surface;
            }
        }
        if (imageManagerVisible)
        {
            return ZOrderSurface.ImageManager;
        }
        if (photoActive)
        {
            return ZOrderSurface.PhotoFullscreen;
        }
        if (presentationFullscreen)
        {
            return ZOrderSurface.PresentationFullscreen;
        }
        if (whiteboardActive)
        {
            return ZOrderSurface.Whiteboard;
        }
        return ZOrderSurface.None;
    }

    private void PruneSurfaceStack(
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        for (int i = _surfaceStack.Count - 1; i >= 0; i--)
        {
            if (!IsSurfaceActive(_surfaceStack[i], photoActive, presentationFullscreen, whiteboardActive, imageManagerVisible))
            {
                _surfaceStack.RemoveAt(i);
            }
        }
    }

    private static bool IsSurfaceActive(
        ZOrderSurface surface,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible)
    {
        return surface switch
        {
            ZOrderSurface.PhotoFullscreen => photoActive,
            ZOrderSurface.PresentationFullscreen => presentationFullscreen,
            ZOrderSurface.Whiteboard => whiteboardActive,
            ZOrderSurface.ImageManager => imageManagerVisible,
            _ => false
        };
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
