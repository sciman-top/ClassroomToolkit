using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Interop;
using WpfSize = System.Windows.Size;
using System.Windows.Threading;

using ClassroomToolkit.App.Photos;

using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.Interop;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Timers;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App;

public partial class RollCallWindow : Window
{
    public static readonly DependencyProperty IsDataLoadedProperty =
        DependencyProperty.Register(
            nameof(IsDataLoaded),
            typeof(bool),
            typeof(RollCallWindow),
            new PropertyMetadata(false));



    private readonly RollCallViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly string _dataPath;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _rollStateSaveTimer;
    private readonly DispatcherTimer _windowBoundsSaveTimer;
    private readonly Stopwatch _stopwatch;
    private readonly List<KeyboardHook> _keyboardHooks = new();
    private readonly List<KeyboardHook> _groupSwitchHooks = new();
    private Action<ClassroomToolkit.Interop.Presentation.KeyBinding>? _remoteHandler;
    private Action<ClassroomToolkit.Interop.Presentation.KeyBinding>? _groupSwitchHandler;
    private Photos.RollCallGroupOverlayWindow? _groupOverlay;
    private readonly LatestOnlyAsyncGate _remoteHookStartGate = new();
    private PhotoOverlayWindow? _photoOverlay;
    private StudentPhotoResolver? _photoResolver;
    private string? _lastPhotoStudentId;

    private string _lastVoiceId = string.Empty;
    private bool _allowClose;
    private bool _timerStateApplied;
    private bool _rollStateDirty;
    private bool _speechUnavailableNotified;
    private bool _remoteHookUnavailableNotified;
    private bool _initialized;
    private bool _dataLoaded;
    private bool _settingsSaveFailedNotified;
    private bool _hovering;
    private bool _windowBoundsDirty;
    private DateTime _suppressRollUntil = DateTime.MinValue;
    private IntPtr _hwnd;
    private readonly DispatcherTimer _hoverCheckTimer;
    
    public ICommand OpenRemoteKeyCommand { get; }

    public bool IsDataLoaded
    {
        get => (bool)GetValue(IsDataLoadedProperty);
        set => SetValue(IsDataLoadedProperty, value);
    }

    public RollCallWindow(string dataPath, AppSettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _dataPath = dataPath;
        _settingsService = settingsService;
        _settings = settings;
        ApplyWindowBounds(settings);
        
        _viewModel = new RollCallViewModel(dataPath);
        DataContext = _viewModel;
        _viewModel.GroupButtons.CollectionChanged += (_, _) => UpdateMinWindowSize();
        
        Loaded += OnLoaded;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            UpdateWindowTransparency();
        };
        MouseEnter += OnWindowMouseEnter;
        MouseLeave += OnWindowMouseLeave;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
                UpdateWindowTransparency();
            }
        };
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;

        _stopwatch = new Stopwatch();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _timer.Tick += OnTimerTick;

        _rollStateSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _rollStateSaveTimer.Tick += OnRollStateSaveTick;

        _windowBoundsSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(240)
        };
        _windowBoundsSaveTimer.Tick += (_, _) => SaveWindowBoundsIfNeeded();
        
        _hoverCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _hoverCheckTimer.Tick += (_, _) => UpdateHoverState();

        OpenRemoteKeyCommand = new RelayCommand(OpenRemoteKeyDialog);
        _viewModel.TimerCompleted += OnTimerCompleted;
        _viewModel.ReminderTriggered += OnReminderTriggered;
        _viewModel.DataLoadFailed += OnDataLoadFailed;
        _viewModel.DataSaveFailed += OnDataSaveFailed;

        PaintModeManager.Instance.PaintModeChanged += _ => UpdateWindowTransparency();
        PaintModeManager.Instance.IsDrawingChanged += _ => UpdateWindowTransparency();

        SizeChanged += (_, _) => ScheduleWindowBoundsSave();
        LocationChanged += (_, _) => ScheduleWindowBoundsSave();
        
        IsVisibleChanged += OnWindowVisibilityChanged;
        StateChanged += OnWindowStateChanged;
    }

    private void OnDataLoadFailed(string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var detail = string.IsNullOrWhiteSpace(message) ? "学生名册读取失败，请检查文件是否被占用或已损坏。" : message;
        System.Windows.MessageBox.Show(owner ?? this, detail, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDataSaveFailed(string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var detail = string.IsNullOrWhiteSpace(message) ? "学生名册保存失败，请关闭 Excel 后重试。" : message;
        System.Windows.MessageBox.Show(owner ?? this, detail, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public IReadOnlyList<string> AvailableClasses => _viewModel.AvailableClasses;

    public void SyncTopmost(bool enabled)
    {
        Topmost = enabled;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var insertAfter = enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost;
        NativeMethods.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        try
        {
            _initialized = true;
            ApplySettings(_settings, updatePhoto: false);
            await _viewModel.LoadDataAsync(_settings.RollCallCurrentClass, Dispatcher);
            RestoreGroupSelection();
            UpdatePhotoDisplay();
            WindowPlacementHelper.EnsureVisible(this);
            _stopwatch.Restart();
            _timer.Start();
            UpdateRemoteHookState();
            _dataLoaded = true;
            IsDataLoaded = true;
            UpdateMinWindowSize();
            UpdateWindowTransparency();
        }
        catch (Exception ex)
        {
            _initialized = false;
            System.Diagnostics.Debug.WriteLine($"RollCallWindow 初始化失败: {ex.Message}");
            var owner = System.Windows.Application.Current?.MainWindow;
            System.Windows.MessageBox.Show(
                owner ?? this,
                "点名窗口初始化失败，请稍后重试。",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    // --- Window Control ---

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_dataLoaded)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
            {
                return;
            }
            DragMove();
        }
    }

    private void OnBackgroundDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_dataLoaded)
        {
            return;
        }
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (e.OriginalSource is DependencyObject source)
        {
            if (IsInteractiveElement(source))
            {
                return;
            }
            // 姓名卡片和计时器卡片需要响应点击/展示，不应用作窗口拖动区域
            if (IsDescendantOf(source, RollNameCard) || IsDescendantOf(source, TimerCard))
            {
                return;
            }
        }
        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // Ignore drag exceptions.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ----------------------

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            HidePhotoOverlay();
            UpdateGroupNameDisplay();
            PersistSettings();
            _viewModel.SaveState();
            _rollStateSaveTimer.Stop();
            _rollStateDirty = false;
            return;
        }
        _timer.Stop();
        _stopwatch.Stop();
        _rollStateSaveTimer.Stop();
        _windowBoundsSaveTimer.Stop();
        _rollStateDirty = false;
        _remoteHookStartGate.NextGeneration();
        StopKeyboardHook();
        ClosePhotoOverlay();
        if (_groupOverlay != null)
        {
            try { _groupOverlay.Close(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RollCallWindow: close group overlay failed: {ex.GetType().Name} - {ex.Message}");
            }
            _groupOverlay = null;
        }
        if (_speechSynthesizer != null)
        {
            try { _speechSynthesizer.Dispose(); _speechSynthesizer = null; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RollCallWindow: dispose speech synthesizer failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
        PersistSettings();
        _viewModel.SaveState();
    }

    private static bool IsInteractiveElement(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase
                || current is System.Windows.Controls.ComboBox
                || current is System.Windows.Controls.Primitives.TextBoxBase
                || current is System.Windows.Controls.Primitives.Selector
                || current is System.Windows.Controls.Primitives.MenuBase
                || current is System.Windows.Controls.Primitives.ScrollBar
                || current is System.Windows.Controls.Primitives.Thumb
                || current is System.Windows.Controls.Primitives.ToggleButton)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject? ancestor)
    {
        if (ancestor == null)
        {
            return false;
        }
        var current = source;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hovering = true;
        UpdateWindowTransparency();
    }

    private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hovering = false;
        UpdateWindowTransparency();
    }

    private void UpdateWindowTransparency()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var allowTransparent = !_hovering && PaintModeManager.Instance.ShouldAllowTransparency(isToolbar: false);
        UpdateHoverTimer(allowTransparent);
        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GwlExstyle);
        if (allowTransparent)
        {
            exStyle |= NativeMethods.WsExTransparent;
        }
        else
        {
            exStyle &= ~NativeMethods.WsExTransparent;
        }
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GwlExstyle, exStyle);
    }

    private void UpdateHoverTimer(bool transparentEnabled)
    {
        if (!transparentEnabled)
        {
            _hoverCheckTimer.Stop();
            return;
        }
        if (!_hoverCheckTimer.IsEnabled)
        {
            _hoverCheckTimer.Start();
        }
    }

    private void UpdateHoverState()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }
        NativeMethods.NativeRect rect;
        if (!NativeMethods.GetWindowRect(_hwnd, out rect))
        {
            return;
        }
        var inside = point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;
        if (inside == _hovering)
        {
            return;
        }
        _hovering = inside;
        UpdateWindowTransparency();
    }

    private void OnGroupEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is GroupButtonItem item)
        {
            if (item.IsReset)
            {
                OnResetClick(sender, e);
                return;
            }
            if (!string.IsNullOrWhiteSpace(item.Label))
            {
                _viewModel.SetCurrentGroup(item.Label);
                UpdatePhotoDisplay(forceHide: true);
                PersistSettings();
            }
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
        if (ShouldSuppressRollClick())
        {
            return;
        }
        if (_viewModel.TryRollNext(out var message))
        {
            UpdatePhotoDisplay();
            SpeakStudentName();
            ScheduleRollStateSave();
            return;
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowRollCallMessage(message);
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var group = _viewModel.CurrentGroup;
        var prompt = group == ClassroomToolkit.Domain.Utilities.IdentityUtils.AllGroupName
            ? "确定要重置所有分组的点名状态并重新开始吗？"
            : $"确定要重置“{group}”分组的点名状态并重新开始吗？";
        var result = System.Windows.MessageBox.Show(prompt, "提示", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
        {
            return;
        }
        _viewModel.ResetCurrentGroup();
        UpdatePhotoDisplay(forceHide: true);
        ScheduleRollStateSave();
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleMode();
        UpdateRemoteHookState();
        UpdatePhotoDisplay(forceHide: true);
        UpdateMinWindowSize();
    }

    private void OnClassSelectClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRollCallMode)
        {
            return;
        }
        OpenClassSelectionDialog();
    }

    private void OnListClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRollCallMode)
        {
            return;
        }
        if (!_viewModel.TryBuildStudentList(out var students, out var message))
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                ShowRollCallMessage(message);
            }
            return;
        }
        var dialog = new StudentListDialog(students)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedIndex.HasValue)
        {
            if (_viewModel.SetCurrentStudentByIndex(dialog.SelectedIndex.Value))
            {
                SpeakStudentName();
                UpdatePhotoDisplay();
            }
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RollCallSettingsDialog(_settings, _viewModel.AvailableClasses)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var patch = BuildPatchFromDialog(dialog);
        RollCallSettingsApplier.Apply(_settings, patch);
        SaveSettingsSafe();
        ApplySettings(_settings, updatePhoto: false);
        HidePhotoOverlay();
        _lastPhotoStudentId = null;
    }

    private void OnTimerModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTimerMode();
    }

    private void OnTimerStartPauseClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTimer();
    }

    private void OnTimerResetClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetTimer();
    }

    private void OnTimerSetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TimerSetDialog(_viewModel.TimerMinutes, _viewModel.TimerSeconds)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SetCountdown(dialog.Minutes, dialog.Seconds);
        }
    }

    private void UpdatePhotoDisplay(bool forceHide = false)
    {
        if (forceHide || !_viewModel.ShowPhoto || !_viewModel.IsRollCallMode)
        {
            HidePhotoOverlay();
            _lastPhotoStudentId = null;
            return;
        }
        var studentId = _viewModel.CurrentStudentId;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            HidePhotoOverlay();
            _lastPhotoStudentId = null;
            return;
        }
        
        // 参考 Python 版本的策略：当学生ID变化时，先隐藏上一张照片
        if (_lastPhotoStudentId != studentId)
        {
            // 完全关闭并销毁照片覆盖窗口，确保没有任何残留
            ClosePhotoOverlay();
            _photoOverlay = null;
        }
        
        _lastPhotoStudentId = studentId;
        var resolver = EnsurePhotoResolver();
        var className = ResolvePhotoClassName();
        var path = resolver.ResolvePhotoPath(className, studentId);
        if (string.IsNullOrWhiteSpace(path))
        {
            HidePhotoOverlay();
            return;
        }
        var overlay = EnsurePhotoOverlay();
        overlay.ShowPhoto(path, _viewModel.CurrentStudentName, _viewModel.CurrentStudentId, _viewModel.PhotoDurationSeconds, this);
    }

    private StudentPhotoResolver EnsurePhotoResolver()
    {
        if (_photoResolver != null)
        {
            return _photoResolver;
        }
        var root = ResolvePhotoRoot();
        _photoResolver = new StudentPhotoResolver(root);
        return _photoResolver;
    }

    private string ResolvePhotoRoot()
    {
        return StudentResourceLocator.ResolveStudentPhotoRoot();
    }

    private string ResolvePhotoClassName()
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.PhotoSharedClass))
        {
            return _viewModel.PhotoSharedClass;
        }
        return _viewModel.ActiveClassName;
    }

    private PhotoOverlayWindow EnsurePhotoOverlay()
    {
        if (_photoOverlay != null)
        {
            return _photoOverlay;
        }
        _photoOverlay = new PhotoOverlayWindow();
        _photoOverlay.PhotoClosed += OnPhotoClosed;
        return _photoOverlay;
    }

    private void HidePhotoOverlay()
    {
        if (_photoOverlay == null)
        {
            return;
        }
        _photoOverlay.CloseOverlay();
    }

    private void ClosePhotoOverlay()
    {
        if (_photoOverlay == null)
        {
            return;
        }
        _photoOverlay.CloseOverlay();
        _photoOverlay.Close();
        _photoOverlay.PhotoClosed -= OnPhotoClosed;
        _photoOverlay = null;
    }

    private void OnPhotoClosed(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || _photoResolver == null)
        {
            return;
        }
        var className = ResolvePhotoClassName();
        _photoResolver.InvalidateStudentCache(className, studentId);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        _stopwatch.Restart();
        _viewModel.TickTimer(elapsed);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }
        if (_photoOverlay != null && _photoOverlay.IsVisible)
        {
            _photoOverlay.CloseOverlay();
            e.Handled = true;
        }
    }

    private void OnRollStateSaveTick(object? sender, EventArgs e)
    {
        _rollStateSaveTimer.Stop();
        if (!_rollStateDirty)
        {
            return;
        }
        _rollStateDirty = false;
        _viewModel.SaveState();
    }

    private void OnTimerCompleted()
    {
        if (_viewModel.TimerSoundEnabled)
        {
            PlayTimerSound(_viewModel.TimerSoundVariant, isReminder: false);
        }
    }

    private void OnReminderTriggered()
    {
        if (_viewModel.TimerReminderEnabled)
        {
            PlayTimerSound(_viewModel.TimerReminderSoundVariant, isReminder: true);
        }
    }

    private static void PlayTimerSound(string? variant, bool isReminder)
    {
        var key = (variant ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "bell":
                SystemSounds.Exclamation.Play();
                break;
            case "digital":
                SystemSounds.Beep.Play();
                break;
            case "buzz":
                SystemSounds.Hand.Play();
                break;
            case "urgent":
                SystemSounds.Hand.Play();
                break;
            case "ping":
                SystemSounds.Beep.Play();
                break;
            case "chime":
                SystemSounds.Asterisk.Play();
                break;
            case "pulse":
                SystemSounds.Asterisk.Play();
                break;
            case "short_bell":
                SystemSounds.Exclamation.Play();
                break;
            default:
                if (isReminder)
                {
                    SystemSounds.Asterisk.Play();
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
                break;
        }
    }

     private SpeechSynthesizer? _speechSynthesizer;
     private async void SpeakStudentName()
     {
         if (!_viewModel.SpeechEnabled)
         {
             return;
         }
         var name = _viewModel.CurrentStudentName;
         if (string.IsNullOrWhiteSpace(name))
         {
             return;
         }

         try
         {
             if (_speechSynthesizer == null)
             {
                 _speechSynthesizer = await Task.Run(() => new SpeechSynthesizer());
             }

             var voiceId = _viewModel.SpeechVoiceId ?? string.Empty;
             if (!string.IsNullOrWhiteSpace(voiceId) && !voiceId.Equals(_lastVoiceId, StringComparison.OrdinalIgnoreCase))
             {
                 _speechSynthesizer.SelectVoice(voiceId);
                 _lastVoiceId = voiceId;
             }
             
             _speechSynthesizer.SpeakAsyncCancelAll();
             _speechSynthesizer.SpeakAsync(name);
         }
         catch (Exception ex)
         {
             System.Diagnostics.Debug.WriteLine($"RollCallWindow: SpeakStudentName failed: {ex.GetType().Name} - {ex.Message}");
             NotifySpeechError();
         }
    }




    private void NotifySpeechError()
    {
        if (_speechUnavailableNotified)
        {
            return;
        }
        _speechUnavailableNotified = true;
        Dispatcher.BeginInvoke(() =>
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            var message = "语音播报不可用，可能缺少系统语音包或相关组件。请安装中文语音包后重启。";
            System.Windows.MessageBox.Show(owner ?? this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async Task StartKeyboardHookCoreAsync(Func<bool> isCurrent)
    {
        if (!isCurrent() || _keyboardHooks.Count > 0)
        {
            return;
        }
        if (!ShouldEnableRemotePresenterHook())
        {
            return;
        }
        var fallback = new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.Tab, KeyModifiers.None);
        var bindings = ResolveRemoteBindings(_viewModel.RemotePresenterKey, fallback);
        var handler = new Action<ClassroomToolkit.Interop.Presentation.KeyBinding>(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_viewModel.IsRollCallMode)
                {
                    return;
                }
                if (_viewModel.TryRollNext(out var message))
                {
                    UpdatePhotoDisplay();
                    SpeakStudentName();
                    ScheduleRollStateSave();
                    return;
                }
                if (!string.IsNullOrWhiteSpace(message))
                {
                    ShowRollCallMessage(message);
                }
            });
        });
        var startedHooks = new List<KeyboardHook>();
        foreach (var binding in bindings)
        {
            if (!isCurrent() || !ShouldEnableRemotePresenterHook())
            {
                CleanupHooks(startedHooks, handler);
                return;
            }
            var hook = new KeyboardHook
            {
                TargetBinding = binding,
                SuppressWhenMatched = true
            };
            hook.BindingTriggered += handler;
            await hook.StartAsync();
            if (!isCurrent() || !ShouldEnableRemotePresenterHook())
            {
                hook.BindingTriggered -= handler;
                hook.Stop();
                CleanupHooks(startedHooks, handler);
                return;
            }
            startedHooks.Add(hook);
        }

        if (!isCurrent() || !ShouldEnableRemotePresenterHook())
        {
            CleanupHooks(startedHooks, handler);
            return;
        }

        _remoteHandler = handler;
        _keyboardHooks.AddRange(startedHooks);

        if (startedHooks.All(h => !h.IsActive) && !_remoteHookUnavailableNotified)
        {
            _remoteHookUnavailableNotified = true;
            var error = startedHooks.Select(h => h.LastError).FirstOrDefault(e => e != 0);
            _ = Dispatcher.BeginInvoke(() =>
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                var suffix = error == 0 ? string.Empty : $"（错误码：{error}）";
                var message = $"翻页笔全局监听不可用，可能被系统权限或安全软件拦截{suffix}。可尝试以管理员身份运行。";
                System.Windows.MessageBox.Show(owner ?? this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }

    private void StopKeyboardHook()
    {
        if (_keyboardHooks.Count > 0)
        {
            foreach (var hook in _keyboardHooks)
            {
                if (_remoteHandler != null)
                {
                    hook.BindingTriggered -= _remoteHandler;
                }
                hook.Stop();
            }
            _keyboardHooks.Clear();
            _remoteHandler = null;
        }

        if (_groupSwitchHooks.Count > 0)
        {
            foreach (var hook in _groupSwitchHooks)
            {
                if (_groupSwitchHandler != null)
                {
                    hook.BindingTriggered -= _groupSwitchHandler;
                }
                hook.Stop();
            }
            _groupSwitchHooks.Clear();
            _groupSwitchHandler = null;
        }
    }

    private async Task StartGroupSwitchHookCoreAsync(Func<bool> isCurrent)
    {
        if (!isCurrent() || _groupSwitchHooks.Count > 0)
        {
            return;
        }
        if (!ShouldEnableGroupSwitchHook())
        {
            return;
        }

        var fallback = new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.B, KeyModifiers.None);
        var bindings = ResolveRemoteBindings(_viewModel.RemoteGroupSwitchKey, fallback);
        
        var handler = new Action<ClassroomToolkit.Interop.Presentation.KeyBinding>(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_viewModel.IsRollCallMode) return;
                
                _viewModel.SwitchToNextGroup();
                ShowGroupOverlay();
                ScheduleRollStateSave();
            });
        });
        var startedHooks = new List<KeyboardHook>();

        foreach (var binding in bindings)
        {
            if (!isCurrent() || !ShouldEnableGroupSwitchHook())
            {
                CleanupHooks(startedHooks, handler);
                return;
            }
            var hook = new KeyboardHook
            {
                TargetBinding = binding,
                SuppressWhenMatched = true
            };
            hook.BindingTriggered += handler;
            await hook.StartAsync();
            if (!isCurrent() || !ShouldEnableGroupSwitchHook())
            {
                hook.BindingTriggered -= handler;
                hook.Stop();
                CleanupHooks(startedHooks, handler);
                return;
            }
            startedHooks.Add(hook);
        }

        if (!isCurrent() || !ShouldEnableGroupSwitchHook())
        {
            CleanupHooks(startedHooks, handler);
            return;
        }

        _groupSwitchHandler = handler;
        _groupSwitchHooks.AddRange(startedHooks);
    }

    private void ShowGroupOverlay()
    {
        UpdateGroupNameDisplay();
    }



    private void OnWindowVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateGroupNameDisplay();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateGroupNameDisplay();
    }

    private void UpdateGroupNameDisplay()
    {
        if (!_viewModel.RemoteGroupSwitchEnabled || !_viewModel.IsRollCallMode)
        {
            // 未启用分组切换功能或不在点名模式，不显示组名
            if (_groupOverlay != null)
            {
                _groupOverlay.HideGroup();
            }
            return;
        }

        var shouldShowPersistent = WindowState == WindowState.Minimized || !IsVisible;
        
        if (shouldShowPersistent)
        {
            // 点名窗口隐藏时：持久显示组名
            if (_groupOverlay == null)
            {
                _groupOverlay = new Photos.RollCallGroupOverlayWindow();
                _groupOverlay.Closed += (s, e) => _groupOverlay = null;
            }
            _groupOverlay.ShowGroup(_viewModel.CurrentGroup, persistent: true);
        }
        else
        {
            // 点名窗口显示时：绝不显示组名
            if (_groupOverlay != null)
            {
                _groupOverlay.HideGroup();
            }
        }
    }

    private void OpenRemoteKeyDialog()
    {
        var dialog = new RemoteKeyDialog(_viewModel.RemotePresenterKey)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SetRemotePresenterKey(dialog.SelectedKey);
            _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
            SaveSettingsSafe();
            RestartKeyboardHook();
        }
    }

    public void ApplySettings(AppSettings settings, bool updatePhoto = true)
    {
        _viewModel.ShowId = settings.RollCallShowId;
        _viewModel.ShowName = settings.RollCallShowName;
        if (!_viewModel.ShowId && !_viewModel.ShowName)
        {
            _viewModel.ShowName = true;
        }
        _viewModel.ShowPhoto = settings.RollCallShowPhoto;
        _viewModel.PhotoDurationSeconds = settings.RollCallPhotoDurationSeconds;
        _viewModel.PhotoSharedClass = settings.RollCallPhotoSharedClass;
        _viewModel.TimerSoundEnabled = settings.RollCallTimerSoundEnabled;
        _viewModel.TimerReminderEnabled = settings.RollCallTimerReminderEnabled;
        _viewModel.TimerReminderIntervalMinutes = settings.RollCallTimerReminderIntervalMinutes;
        _viewModel.TimerSoundVariant = settings.RollCallTimerSoundVariant;
        _viewModel.TimerReminderSoundVariant = settings.RollCallTimerReminderSoundVariant;
        _viewModel.SpeechEnabled = settings.RollCallSpeechEnabled;
        _viewModel.SpeechEngine = settings.RollCallSpeechEngine;
        _viewModel.SpeechVoiceId = settings.RollCallSpeechVoiceId;
        _viewModel.SpeechOutputId = settings.RollCallSpeechOutputId;
        _viewModel.RemotePresenterEnabled = settings.RollCallRemoteEnabled;
        _viewModel.RemoteGroupSwitchEnabled = settings.RollCallRemoteGroupSwitchEnabled;
        _viewModel.SetRemotePresenterKey(settings.RemotePresenterKey);
        _viewModel.RemoteGroupSwitchKey = settings.RemoteGroupSwitchKey;
        if (!_timerStateApplied)
        {
            var isRollCallMode = !string.Equals(settings.RollCallMode, "timer", StringComparison.OrdinalIgnoreCase);
            var timerMode = settings.RollCallTimerMode?.Trim().ToLowerInvariant() switch
            {
                "stopwatch" => TimerMode.Stopwatch,
                "clock" => TimerMode.Clock,
                _ => TimerMode.Countdown
            };
            _viewModel.ApplyTimerState(
                isRollCallMode,
                timerMode,
                settings.RollCallTimerMinutes,
                settings.RollCallTimerSeconds,
                settings.RollCallTimerSecondsLeft,
                settings.RollCallStopwatchSeconds,
                settings.RollCallTimerRunning);
            _timerStateApplied = true;
        }
        
        UpdateRemoteHookState();
        if (updatePhoto)
        {
            UpdatePhotoDisplay();
        }
    }

    public void RequestClose()
    {
        _allowClose = true;
        Close();
    }

    private void PersistSettings()
    {
        CaptureWindowBounds();
        RollCallSettingsApplier.Apply(_settings, BuildPatchFromViewModel());
        _settings.RollCallMode = _viewModel.IsRollCallMode ? "roll_call" : "timer";
        _settings.RollCallTimerMode = _viewModel.CurrentTimerMode switch
        {
            TimerMode.Stopwatch => "stopwatch",
            TimerMode.Clock => "clock",
            _ => "countdown"
        };
        _settings.RollCallTimerMinutes = _viewModel.TimerMinutes;
        _settings.RollCallTimerSeconds = _viewModel.TimerSeconds;
        _settings.RollCallTimerSecondsLeft = _viewModel.TimerSecondsLeft;
        _settings.RollCallStopwatchSeconds = _viewModel.TimerStopwatchSeconds;
        _settings.RollCallTimerRunning = _viewModel.TimerRunning;
        _settings.RollCallCurrentClass = _viewModel.ActiveClassName;
        _settings.RollCallCurrentGroup = _viewModel.CurrentGroup;
        SaveSettingsSafe();
    }

    private static RollCallSettingsPatch BuildPatchFromDialog(RollCallSettingsDialog dialog)
    {
        return new RollCallSettingsPatch(
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
    }

    private RollCallSettingsPatch BuildPatchFromViewModel()
    {
        return new RollCallSettingsPatch(
            _viewModel.ShowId,
            _viewModel.ShowName,
            _viewModel.ShowPhoto,
            _viewModel.PhotoDurationSeconds,
            _viewModel.PhotoSharedClass,
            _viewModel.TimerSoundEnabled,
            _viewModel.TimerReminderEnabled,
            _viewModel.TimerReminderIntervalMinutes,
            _viewModel.TimerSoundVariant,
            _viewModel.TimerReminderSoundVariant,
            _viewModel.SpeechEnabled,
            _viewModel.SpeechEngine,
            _viewModel.SpeechVoiceId,
            _viewModel.SpeechOutputId,
            _viewModel.RemotePresenterEnabled,
            _viewModel.RemoteGroupSwitchEnabled,
            _viewModel.RemotePresenterKey,
            _viewModel.RemoteGroupSwitchKey);
    }

    private void UpdateRemoteHookState()
    {
        RestartKeyboardHook();
        UpdateGroupNameDisplay();
    }
    
    private void RestartKeyboardHook()
    {
        var generation = _remoteHookStartGate.NextGeneration();
        StopKeyboardHook();
        _ = _remoteHookStartGate.RunAsync(generation, StartKeyboardHookCoreAsync);
        _ = _remoteHookStartGate.RunAsync(generation, StartGroupSwitchHookCoreAsync);
    }

    private bool ShouldEnableRemotePresenterHook() => _viewModel.RemotePresenterEnabled && _viewModel.IsRollCallMode;

    private bool ShouldEnableGroupSwitchHook() => _viewModel.RemoteGroupSwitchEnabled && _viewModel.IsRollCallMode;

    private static void CleanupHooks(
        IEnumerable<KeyboardHook> hooks,
        Action<ClassroomToolkit.Interop.Presentation.KeyBinding> handler)
    {
        foreach (var hook in hooks)
        {
            hook.BindingTriggered -= handler;
            hook.Stop();
        }
    }

    private static bool IsUnsupportedRemoteBinding(ClassroomToolkit.Interop.Presentation.KeyBinding binding)
    {
        return binding.Modifiers == KeyModifiers.None
            && binding.Key == VirtualKey.W; // W (removed)
    }

    private static IReadOnlyList<ClassroomToolkit.Interop.Presentation.KeyBinding> ResolveRemoteBindings(
        string configuredKey,
        ClassroomToolkit.Interop.Presentation.KeyBinding fallback)
    {
        if (string.Equals(configuredKey?.Trim(), "f5", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.F5, KeyModifiers.None),
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.F5, KeyModifiers.Shift),
                new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.Escape, KeyModifiers.None)
            };
        }

        var binding = KeyBindingParser.ParseOrDefault(configuredKey, fallback);
        if (IsUnsupportedRemoteBinding(binding))
        {
            binding = fallback;
        }
        return new[] { binding };
    }

    private void ShowRollCallMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveSettingsSafe()
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
            var owner = System.Windows.Application.Current?.MainWindow;
            var detail = $"设置保存失败：{ex.Message}\n请检查设置文件权限或磁盘状态。";
            System.Windows.MessageBox.Show(owner ?? this, detail, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RestoreGroupSelection()
    {
        var group = _settings.RollCallCurrentGroup;
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }
        if (_viewModel.Groups.Contains(group))
        {
            _viewModel.SetCurrentGroup(group);
            UpdatePhotoDisplay(forceHide: true);
        }
    }

    private void TryApplyClassSelection(string selected)
    {
        if (_viewModel.SwitchClass(selected, updatePhoto: false))
        {
            UpdatePhotoDisplay(forceHide: true);
            PersistSettings();
            _viewModel.SaveState();
            UpdateMinWindowSize();
            SuppressRollClicks(TimeSpan.FromMilliseconds(250));
            return;
        }
        if (!string.Equals(_viewModel.ActiveClassName, selected, StringComparison.OrdinalIgnoreCase))
        {
            var classes = _viewModel.AvailableClasses?.Count > 0
                ? string.Join("、", _viewModel.AvailableClasses)
                : "（空）";
            var message = $"切换班级失败：{selected}\n当前班级：{_viewModel.ActiveClassName}\n可用班级：{classes}\n名册路径：{_dataPath}";
            ShowRollCallMessage(message);
        }
    }

    private void OpenClassSelectionDialog()
    {
        var classes = _viewModel.AvailableClasses;
        if (classes == null || classes.Count == 0)
        {
            ShowRollCallMessage("暂无班级可供选择。");
            return;
        }
        var dialog = new ClassSelectDialog(classes, _viewModel.ActiveClassName)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedClass))
        {
            TryApplyClassSelection(dialog.SelectedClass);
        }
    }

    private void SuppressRollClicks(TimeSpan duration)
    {
        var until = DateTime.UtcNow.Add(duration);
        if (until > _suppressRollUntil)
        {
            _suppressRollUntil = until;
        }
    }

    private bool ShouldSuppressRollClick()
    {
        if (_suppressRollUntil >= DateTime.UtcNow)
        {
            return true;
        }
        return false;
    }

    private void ScheduleRollStateSave()
    {
        _rollStateDirty = true;
        if (_rollStateSaveTimer.IsEnabled)
        {
            _rollStateSaveTimer.Stop();
        }
        _rollStateSaveTimer.Start();
    }

    private void ApplyWindowBounds(AppSettings settings)
    {
        if (settings.RollCallWindowWidth > 0)
        {
            Width = settings.RollCallWindowWidth;
        }
        if (settings.RollCallWindowHeight > 0)
        {
            Height = settings.RollCallWindowHeight;
        }
        if (settings.RollCallWindowX != AppSettings.UnsetPosition
            && settings.RollCallWindowY != AppSettings.UnsetPosition)
        {
            Left = settings.RollCallWindowX;
            Top = settings.RollCallWindowY;
            WindowPlacementHelper.EnsureVisible(this);
        }
        else
        {
            WindowPlacementHelper.CenterOnVirtualScreen(this);
        }
    }

    private void CaptureWindowBounds()
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        _settings.RollCallWindowWidth = (int)Math.Round(width);
        _settings.RollCallWindowHeight = (int)Math.Round(height);
        _settings.RollCallWindowX = (int)Math.Round(Left);
        _settings.RollCallWindowY = (int)Math.Round(Top);
    }

    private void ScheduleWindowBoundsSave()
    {
        if (!IsLoaded)
        {
            return;
        }
        _windowBoundsDirty = true;
        if (_windowBoundsSaveTimer.IsEnabled)
        {
            _windowBoundsSaveTimer.Stop();
        }
        _windowBoundsSaveTimer.Start();
    }

    private void SaveWindowBoundsIfNeeded()
    {
        _windowBoundsSaveTimer.Stop();
        if (!_windowBoundsDirty)
        {
            return;
        }
        _windowBoundsDirty = false;
        CaptureWindowBounds();
        SaveSettingsSafe();
    }

    private void UpdateMinWindowSize()
    {
        if (!IsLoaded)
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            var titleSize = MeasureElement(TitleBarRoot);
            var bottomSize = MeasureElement(BottomBarRoot);
            var groupButtonsSize = MeasureElement(GroupButtonsControl);

            var minWidth = titleSize.Width;
            if (_viewModel.IsRollCallMode)
            {
                minWidth = Math.Max(minWidth, groupButtonsSize.Width + GetBottomBarChromeWidth());
            }

            var minHeight = titleSize.Height + bottomSize.Height;

            MinWidth = Math.Max(240, Math.Ceiling(minWidth));
            MinHeight = Math.Max(240, Math.Ceiling(minHeight));
        }, DispatcherPriority.Background);
    }

    private double GetBottomBarChromeWidth()
    {
        if (BottomBarRoot == null)
        {
            return 0;
        }
        var padding = BottomBarRoot.Padding;
        var border = BottomBarRoot.BorderThickness;
        var margin = BottomBarRoot.Margin;
        return padding.Left + padding.Right + border.Left + border.Right + margin.Left + margin.Right;
    }

    private static WpfSize MeasureElement(FrameworkElement? element)
    {
        if (element == null)
        {
            return new WpfSize(0, 0);
        }
        element.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var desired = element.DesiredSize;
        var margin = element.Margin;
        return new WpfSize(
            desired.Width + margin.Left + margin.Right,
            desired.Height + margin.Top + margin.Bottom);
    }
}
