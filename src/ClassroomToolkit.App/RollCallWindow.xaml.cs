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
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Interop.Presentation;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App;

public partial class RollCallWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    private readonly RollCallViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly string _dataPath;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _rollStateSaveTimer;
    private readonly Stopwatch _stopwatch;
    private KeyboardHook? _keyboardHook;
    private Action<ClassroomToolkit.Interop.Presentation.KeyBinding>? _remoteHandler;
    private PhotoOverlayWindow? _photoOverlay;
    private StudentPhotoResolver? _photoResolver;
    private string? _lastPhotoStudentId;
    private SpeechSynthesizer? _speechSynthesizer;
    private string _lastVoiceId = string.Empty;
    private bool _allowClose;
    private bool _timerStateApplied;
    private bool _rollStateDirty;
    private bool _speechUnavailableNotified;
    private bool _remoteHookUnavailableNotified;
    private bool _classSelectionReady;
    private bool _initialized;
    private bool _hovering;
    private bool _suppressRollClick;
    private DateTime _suppressRollUntil = DateTime.MinValue;
    private string? _classSelectionBefore;
    private IntPtr _hwnd;
    private readonly DispatcherTimer _hoverCheckTimer;
    
    public ICommand OpenRemoteKeyCommand { get; }

    public RollCallWindow(string dataPath, AppSettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _dataPath = dataPath;
        _settingsService = settingsService;
        _settings = settings;
        ApplyWindowBounds(settings);
        
        _viewModel = new RollCallViewModel(dataPath);
        DataContext = _viewModel;
        
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
        var insertAfter = enabled ? HwndTopmost : HwndNoTopmost;
        SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        ApplySettings(_settings, updatePhoto: false);
        await _viewModel.LoadDataAsync(_settings.RollCallCurrentClass, Dispatcher);
        RestoreGroupSelection();
        SyncClassSelection();
        _classSelectionReady = true;
        UpdatePhotoDisplay();
        WindowPlacementHelper.EnsureVisible(this);
        _stopwatch.Restart();
        _timer.Start();
        UpdateRemoteHookState();
    }

    // --- Window Control ---

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnBackgroundDrag(object sender, MouseButtonEventArgs e)
    {
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
            PersistSettings();
            _viewModel.SaveState();
            _rollStateSaveTimer.Stop();
            _rollStateDirty = false;
            return;
        }
        _timer.Stop();
        _stopwatch.Stop();
        _rollStateSaveTimer.Stop();
        _rollStateDirty = false;
        StopKeyboardHook();
        ClosePhotoOverlay();
        _speechSynthesizer?.Dispose();
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
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        if (allowTransparent)
        {
            exStyle |= WsExTransparent;
        }
        else
        {
            exStyle &= ~WsExTransparent;
        }
        SetWindowLong(_hwnd, GwlExstyle, exStyle);
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
        if (!GetCursorPos(out var point))
        {
            return;
        }
        if (!GetWindowRect(_hwnd, out var rect))
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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
    }

    private void OnClassSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_classSelectionReady || !_viewModel.IsRollCallMode)
        {
            return;
        }
        if (sender is not System.Windows.Controls.ComboBox combo)
        {
            return;
        }
        var selected = e.AddedItems.Count > 0 ? e.AddedItems[0] as string : combo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }
        TryApplyClassSelection(selected);
    }

    private void OnClassDropDownOpened(object sender, EventArgs e)
    {
        _suppressRollClick = true;
        _classSelectionBefore = ClassCombo?.SelectedItem as string;
        StopKeyboardHook();
    }

    private void OnClassDropDownClosed(object sender, EventArgs e)
    {
        _suppressRollClick = false;
        SuppressRollClicks(TimeSpan.FromMilliseconds(250));
        UpdateRemoteHookState();
        if (!_classSelectionReady || !_viewModel.IsRollCallMode || ClassCombo == null)
        {
            return;
        }
        if (ClassCombo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
        {
            if (!string.Equals(_viewModel.ActiveClassName, selected, StringComparison.OrdinalIgnoreCase))
            {
                TryApplyClassSelection(selected);
                _classSelectionBefore = null;
                return;
            }
        }
        if (!string.IsNullOrWhiteSpace(_classSelectionBefore))
        {
            var classes = _viewModel.AvailableClasses?.Count > 0
                ? string.Join("、", _viewModel.AvailableClasses)
                : "（空）";
            var current = _viewModel.ActiveClassName;
            var selectedClass = ClassCombo.SelectedItem as string ?? string.Empty;
            var message = $"班级选择未生效。\n打开前：{_classSelectionBefore}\n当前选中：{selectedClass}\n当前班级：{current}\n可用班级：{classes}\n名册路径：{_dataPath}";
            ShowRollCallMessage(message);
            _classSelectionBefore = null;
        }
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
        _settingsService.Save(_settings);
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
            // 强制等待，确保窗口完全关闭
            Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
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

    private void SpeakStudentName()
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
            _speechSynthesizer ??= new SpeechSynthesizer();
            var voiceId = _viewModel.SpeechVoiceId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(voiceId) && !voiceId.Equals(_lastVoiceId, StringComparison.OrdinalIgnoreCase))
            {
                _speechSynthesizer.SelectVoice(voiceId);
                _lastVoiceId = voiceId;
            }
            _speechSynthesizer.SpeakAsyncCancelAll();
            _speechSynthesizer.SpeakAsync(name);
        }
        catch
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
    }

    private void StartKeyboardHook()
    {
        if (_keyboardHook != null)
        {
            return;
        }
        if (!_viewModel.RemotePresenterEnabled || !_viewModel.IsRollCallMode)
        {
            return;
        }
        var fallback = new ClassroomToolkit.Interop.Presentation.KeyBinding(VirtualKey.Tab, KeyModifiers.None);
        var binding = KeyBindingParser.ParseOrDefault(_viewModel.RemotePresenterKey, fallback);
        _keyboardHook = new KeyboardHook
        {
            TargetBinding = binding,
            SuppressWhenMatched = true
        };
        _remoteHandler = _ =>
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
        };
        _keyboardHook.BindingTriggered += _remoteHandler;
        _keyboardHook.Start();
        if (!_keyboardHook.IsActive && !_remoteHookUnavailableNotified)
        {
            _remoteHookUnavailableNotified = true;
            var error = _keyboardHook.LastError;
            Dispatcher.BeginInvoke(() =>
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
        if (_keyboardHook == null)
        {
            return;
        }
        if (_remoteHandler != null)
        {
            _keyboardHook.BindingTriggered -= _remoteHandler;
        }
        _keyboardHook.Stop();
        _keyboardHook = null;
        _remoteHandler = null;
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
            _settingsService.Save(_settings);
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
        _viewModel.SetRemotePresenterKey(settings.RemotePresenterKey);
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
        _settings.RollCallShowId = _viewModel.ShowId;
        _settings.RollCallShowName = _viewModel.ShowName;
        _settings.RollCallShowPhoto = _viewModel.ShowPhoto;
        _settings.RollCallPhotoDurationSeconds = _viewModel.PhotoDurationSeconds;
        _settings.RollCallPhotoSharedClass = _viewModel.PhotoSharedClass;
        _settings.RollCallTimerSoundEnabled = _viewModel.TimerSoundEnabled;
        _settings.RollCallTimerReminderEnabled = _viewModel.TimerReminderEnabled;
        _settings.RollCallTimerReminderIntervalMinutes = _viewModel.TimerReminderIntervalMinutes;
        _settings.RollCallTimerSoundVariant = _viewModel.TimerSoundVariant;
        _settings.RollCallTimerReminderSoundVariant = _viewModel.TimerReminderSoundVariant;
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
        _settings.RollCallSpeechEnabled = _viewModel.SpeechEnabled;
        _settings.RollCallSpeechEngine = _viewModel.SpeechEngine;
        _settings.RollCallSpeechVoiceId = _viewModel.SpeechVoiceId;
        _settings.RollCallSpeechOutputId = _viewModel.SpeechOutputId;
        _settings.RollCallRemoteEnabled = _viewModel.RemotePresenterEnabled;
        _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
        _settings.RollCallCurrentClass = _viewModel.ActiveClassName;
        _settings.RollCallCurrentGroup = _viewModel.CurrentGroup;
        _settingsService.Save(_settings);
    }

    private void RestartKeyboardHook()
    {
        StopKeyboardHook();
        StartKeyboardHook();
    }

    private void ShowRollCallMessage(string message)
    {
        System.Windows.MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateRemoteHookState()
    {
        if (_viewModel.RemotePresenterEnabled && _viewModel.IsRollCallMode)
        {
            RestartKeyboardHook();
        }
        else
        {
            StopKeyboardHook();
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

    private void SyncClassSelection()
    {
        if (ClassCombo == null)
        {
            return;
        }
        var target = _viewModel.ActiveClassName;
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }
        if (!string.Equals(ClassCombo.SelectedItem as string, target, StringComparison.OrdinalIgnoreCase))
        {
            ClassCombo.SelectedItem = target;
        }
    }

    private void TryApplyClassSelection(string selected)
    {
        if (_viewModel.SwitchClass(selected, updatePhoto: false))
        {
            UpdatePhotoDisplay(forceHide: true);
            SyncClassSelection();
            PersistSettings();
            _viewModel.SaveState();
            SuppressRollClicks(TimeSpan.FromMilliseconds(250));
            return;
        }
        SyncClassSelection();
        if (!string.Equals(_viewModel.ActiveClassName, selected, StringComparison.OrdinalIgnoreCase))
        {
            var classes = _viewModel.AvailableClasses?.Count > 0
                ? string.Join("、", _viewModel.AvailableClasses)
                : "（空）";
            var message = $"切换班级失败：{selected}\n当前班级：{_viewModel.ActiveClassName}\n可用班级：{classes}\n名册路径：{_dataPath}";
            ShowRollCallMessage(message);
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
        if (_suppressRollClick)
        {
            return true;
        }
        if (_suppressRollUntil >= DateTime.UtcNow)
        {
            return true;
        }
        if (ClassCombo != null && ClassCombo.IsDropDownOpen)
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
}
