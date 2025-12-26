using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App;

public partial class RollCallWindow : Window
{
    private readonly RollCallViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
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
    public ICommand OpenRemoteKeyCommand { get; }

    public RollCallWindow(string dataPath, AppSettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _viewModel = new RollCallViewModel(dataPath);
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;

        _stopwatch = new Stopwatch();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTimerTick;

        OpenRemoteKeyCommand = new RelayCommand(OpenRemoteKeyDialog);
        _viewModel.TimerCompleted += OnTimerCompleted;
        _viewModel.ReminderTriggered += OnReminderTriggered;
    }

    public IReadOnlyList<string> AvailableClasses => _viewModel.AvailableClasses;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadData(_settings.RollCallCurrentClass);
        ApplySettings(_settings);
        _stopwatch.Restart();
        _timer.Start();
        UpdateRemoteHookState();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            HidePhotoOverlay();
            PersistSettings();
            _viewModel.SaveState();
            return;
        }
        _timer.Stop();
        _stopwatch.Stop();
        StopKeyboardHook();
        ClosePhotoOverlay();
        _speechSynthesizer?.Dispose();
        PersistSettings();
        _viewModel.SaveState();
    }

    private void OnGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string group)
        {
            _viewModel.SetCurrentGroup(group);
            UpdatePhotoDisplay(forceHide: true);
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryRollNext(out var message))
        {
            UpdatePhotoDisplay();
            SpeakStudentName();
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
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleMode();
        UpdateRemoteHookState();
        UpdatePhotoDisplay(forceHide: true);
    }

    private void OnClassClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRollCallMode)
        {
            return;
        }
        if (!_viewModel.HasStudentData || _viewModel.AvailableClasses.Count == 0)
        {
            ShowRollCallMessage("暂无学生数据，无法选择班级。");
            return;
        }
        var dialog = new ClassSelectDialog(_viewModel.AvailableClasses, _viewModel.ActiveClassName)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedClass))
        {
            if (_viewModel.SwitchClass(dialog.SelectedClass))
            {
                UpdatePhotoDisplay(forceHide: true);
                PersistSettings();
                _viewModel.SaveState();
            }
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
        ApplySettings(_settings);
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
        if (_lastPhotoStudentId == studentId && _photoOverlay?.IsVisible == true)
        {
            return;
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
        overlay.ShowPhoto(path, _viewModel.CurrentStudentName, _viewModel.PhotoDurationSeconds, this);
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

    private static string ResolvePhotoRoot()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var basePath = Path.Combine(baseDir, "student_photos");
        var cwdPath = Path.Combine(Environment.CurrentDirectory, "student_photos");
        if (Directory.Exists(basePath))
        {
            return basePath;
        }
        if (Directory.Exists(cwdPath))
        {
            return cwdPath;
        }
        return basePath;
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
        _photoOverlay = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        _stopwatch.Restart();
        _viewModel.TickTimer(elapsed);
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
            // 保持静默，避免语音失败影响点名流程。
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

    public void ApplySettings(AppSettings settings)
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
            var timerMode = string.Equals(settings.RollCallTimerMode, "stopwatch", StringComparison.OrdinalIgnoreCase)
                ? TimerMode.Stopwatch
                : TimerMode.Countdown;
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
        UpdatePhotoDisplay();
    }

    public void RequestClose()
    {
        _allowClose = true;
        Close();
    }

    private void PersistSettings()
    {
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
        _settings.RollCallTimerMode = _viewModel.CurrentTimerMode == TimerMode.Stopwatch ? "stopwatch" : "countdown";
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
}
