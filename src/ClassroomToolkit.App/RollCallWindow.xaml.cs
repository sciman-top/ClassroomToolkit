using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Interop.Presentation;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace ClassroomToolkit.App;

public partial class RollCallWindow : Window
{
    private const int MinFontSize = 5;
    private const int MaxFontSize = 220;

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
    private double _lastIdFontSize;
    private double _lastNameFontSize;
    private double _lastTimerFontSize;
    private MediaFontFamily _nameFontFamily = new("Microsoft YaHei UI");
    private bool _fontUpdatePending;
    private bool _classSelectionReady;
    public ICommand OpenRemoteKeyCommand { get; }

    public RollCallWindow(string dataPath, AppSettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _dataPath = dataPath;
        _settingsService = settingsService;
        _settings = settings;
        ApplyWindowBounds(settings);
        _lastIdFontSize = ClampFontSize(settings.RollCallIdFontSize, 48);
        _lastNameFontSize = ClampFontSize(settings.RollCallNameFontSize, 60);
        _lastTimerFontSize = ClampFontSize(settings.RollCallTimerFontSize, 56);
        _viewModel = new RollCallViewModel(dataPath);
        DataContext = _viewModel;
        ApplyFontFamilies();
        ApplyFontSizes();
        Loaded += OnLoaded;
        SizeChanged += (_, _) => ScheduleFontUpdate();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
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

        OpenRemoteKeyCommand = new RelayCommand(OpenRemoteKeyDialog);
        _viewModel.TimerCompleted += OnTimerCompleted;
        _viewModel.ReminderTriggered += OnReminderTriggered;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public IReadOnlyList<string> AvailableClasses => _viewModel.AvailableClasses;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadData(_settings.RollCallCurrentClass);
        ApplySettings(_settings);
        RestoreGroupSelection();
        _classSelectionReady = true;
        WindowPlacementHelper.EnsureVisible(this);
        _stopwatch.Restart();
        _timer.Start();
        UpdateRemoteHookState();
        UpdateTimerButtons();
        ScheduleFontUpdate();
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

    private void OnGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string group)
        {
            _viewModel.SetCurrentGroup(group);
            UpdatePhotoDisplay(forceHide: true);
            PersistSettings();
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
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

    private void OnClassSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_classSelectionReady || !_viewModel.IsRollCallMode)
        {
            return;
        }
        if (sender is not ComboBox combo || combo.SelectedItem is not string selected)
        {
            return;
        }
        if (_viewModel.SwitchClass(selected))
        {
            UpdatePhotoDisplay(forceHide: true);
            PersistSettings();
            _viewModel.SaveState();
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
        UpdateTimerButtons();
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

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e == null)
        {
            return;
        }
        if (e.PropertyName is nameof(RollCallViewModel.CurrentStudentId)
            or nameof(RollCallViewModel.CurrentStudentName)
            or nameof(RollCallViewModel.ShowId)
            or nameof(RollCallViewModel.ShowName)
            or nameof(RollCallViewModel.TimeDisplay)
            or nameof(RollCallViewModel.IsRollCallMode))
        {
            ScheduleFontUpdate();
        }
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
        _lastIdFontSize = ClampFontSize(settings.RollCallIdFontSize, 48);
        _lastNameFontSize = ClampFontSize(settings.RollCallNameFontSize, 60);
        _lastTimerFontSize = ClampFontSize(settings.RollCallTimerFontSize, 56);
        ApplyFontSizes();
        ScheduleFontUpdate();
        UpdateRemoteHookState();
        UpdatePhotoDisplay();
        UpdateTimerButtons();
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
        _settings.RollCallIdFontSize = (int)Math.Round(_lastIdFontSize);
        _settings.RollCallNameFontSize = (int)Math.Round(_lastNameFontSize);
        _settings.RollCallTimerFontSize = (int)Math.Round(_lastTimerFontSize);
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

    private void UpdateTimerButtons()
    {
        if (TimerStartPauseButton == null || TimerResetButton == null || TimerSetButton == null)
        {
            return;
        }
        switch (_viewModel.CurrentTimerMode)
        {
            case TimerMode.Countdown:
                TimerStartPauseButton.IsEnabled = true;
                TimerResetButton.IsEnabled = true;
                TimerSetButton.IsEnabled = true;
                break;
            case TimerMode.Stopwatch:
                TimerStartPauseButton.IsEnabled = true;
                TimerResetButton.IsEnabled = true;
                TimerSetButton.IsEnabled = false;
                break;
            default:
                TimerStartPauseButton.IsEnabled = false;
                TimerResetButton.IsEnabled = false;
                TimerSetButton.IsEnabled = false;
                break;
        }
    }

    private void ScheduleFontUpdate()
    {
        if (_fontUpdatePending)
        {
            return;
        }
        _fontUpdatePending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _fontUpdatePending = false;
            UpdateDynamicFonts();
        }), DispatcherPriority.Background);
    }

    private void UpdateDynamicFonts()
    {
        if (IdTextBlock == null || NameTextBlock == null || TimerTextBlock == null)
        {
            return;
        }
        if (_viewModel.IsRollCallMode)
        {
            double? idSize = null;
            double? nameSize = null;
            if (_viewModel.ShowId && IdBorder != null)
            {
                var width = Math.Max(40d, IdBorder.ActualWidth);
                var height = Math.Max(40d, IdBorder.ActualHeight);
                var text = IdTextBlock.Text ?? string.Empty;
                var size = CalculateFontSize(width, height, text, monospace: false);
                idSize = size;
            }
            if (_viewModel.ShowName && NameBorder != null)
            {
                var width = Math.Max(40d, NameBorder.ActualWidth);
                var height = Math.Max(40d, NameBorder.ActualHeight);
                var text = NameTextBlock.Text ?? string.Empty;
                var size = CalculateFontSize(width, height, text, monospace: false);
                nameSize = size;
            }
            if (idSize.HasValue && nameSize.HasValue)
            {
                var unified = Math.Min(idSize.Value, nameSize.Value);
                _lastIdFontSize = unified;
                _lastNameFontSize = unified;
                IdTextBlock.FontSize = unified;
                NameTextBlock.FontSize = unified;
            }
            else if (idSize.HasValue)
            {
                _lastIdFontSize = idSize.Value;
                IdTextBlock.FontSize = idSize.Value;
            }
            else if (nameSize.HasValue)
            {
                _lastNameFontSize = nameSize.Value;
                NameTextBlock.FontSize = nameSize.Value;
            }
        }
        if (!_viewModel.IsRollCallMode && TimerDisplayBorder != null)
        {
            var width = Math.Max(60d, TimerDisplayBorder.ActualWidth);
            var height = Math.Max(60d, TimerDisplayBorder.ActualHeight);
            var text = TimerTextBlock.Text ?? string.Empty;
            var size = CalculateFontSize(width, height, text, monospace: true);
            _lastTimerFontSize = size;
            TimerTextBlock.FontSize = size;
        }
    }

    private static double CalculateFontSize(double width, double height, string text, bool monospace)
    {
        if (string.IsNullOrWhiteSpace(text) || width < 20 || height < 20)
        {
            return MinFontSize;
        }
        var wEff = Math.Max(1d, width - 16d);
        var hEff = Math.Max(1d, height - 16d);
        var isCjk = false;
        foreach (var ch in text)
        {
            if (ch >= '\u4e00' && ch <= '\u9fff')
            {
                isCjk = true;
                break;
            }
        }
        var length = Math.Max(1, text.Length);
        var widthCharFactor = isCjk ? 1.0 : (monospace ? 0.58 : 0.6);
        var sizeByWidth = wEff / (length * widthCharFactor);
        var sizeByHeight = hEff * 0.70;
        var finalSize = Math.Floor(Math.Min(sizeByWidth, sizeByHeight));
        return ClampFontSize(finalSize, MinFontSize);
    }

    private static double ClampFontSize(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            value = fallback;
        }
        return Math.Max(MinFontSize, Math.Min(MaxFontSize, value));
    }

    private void ApplyFontFamilies()
    {
        var idFont = FindFontFamily("Microsoft YaHei UI")
            ?? FindFontFamily("Microsoft YaHei")
            ?? new MediaFontFamily("Segoe UI");
        _nameFontFamily = FindFontFamily("楷体")
            ?? FindFontFamily("KaiTi")
            ?? idFont;
        var timerFont = FindFontFamily("Consolas")
            ?? FindFontFamily("Courier New")
            ?? idFont;
        if (IdTextBlock != null)
        {
            IdTextBlock.FontFamily = _nameFontFamily;
            IdTextBlock.FontWeight = _nameFontFamily.Source.Contains("Kai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_nameFontFamily.Source, "楷体", StringComparison.OrdinalIgnoreCase)
                ? FontWeights.Normal
                : FontWeights.Bold;
        }
        if (NameTextBlock != null)
        {
            NameTextBlock.FontFamily = _nameFontFamily;
            NameTextBlock.FontWeight = _nameFontFamily.Source.Contains("Kai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_nameFontFamily.Source, "楷体", StringComparison.OrdinalIgnoreCase)
                ? FontWeights.Normal
                : FontWeights.Bold;
        }
        if (TimerTextBlock != null)
        {
            TimerTextBlock.FontFamily = timerFont;
            TimerTextBlock.FontWeight = FontWeights.Bold;
        }
    }

    private static MediaFontFamily? FindFontFamily(string name)
    {
        foreach (var family in Fonts.SystemFontFamilies)
        {
            if (string.Equals(family.Source, name, StringComparison.OrdinalIgnoreCase))
            {
                return family;
            }
        }
        return null;
    }

    private void ApplyFontSizes()
    {
        if (IdTextBlock != null)
        {
            IdTextBlock.FontSize = _lastIdFontSize;
        }
        if (NameTextBlock != null)
        {
            NameTextBlock.FontSize = _lastNameFontSize;
        }
        if (TimerTextBlock != null)
        {
            TimerTextBlock.FontSize = _lastTimerFontSize;
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
