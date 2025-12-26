using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.ViewModels;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadData();
        ApplySettings(_settings);
        _stopwatch.Restart();
        _timer.Start();
        UpdateRemoteHookState();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _stopwatch.Stop();
        StopKeyboardHook();
        ClosePhotoOverlay();
        _settings.RollCallShowId = _viewModel.ShowId;
        _settings.RollCallShowName = _viewModel.ShowName;
        _settings.RollCallShowPhoto = _viewModel.ShowPhoto;
        _settings.RollCallPhotoDurationSeconds = _viewModel.PhotoDurationSeconds;
        _settings.RollCallPhotoSharedClass = _viewModel.PhotoSharedClass;
        _settings.RollCallTimerSoundEnabled = _viewModel.TimerSoundEnabled;
        _settings.RollCallTimerReminderEnabled = _viewModel.TimerReminderEnabled;
        _settings.RollCallTimerReminderIntervalMinutes = _viewModel.TimerReminderIntervalMinutes;
        _settings.RollCallRemoteEnabled = _viewModel.RemotePresenterEnabled;
        _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
        _settingsService.Save(_settings);
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
        _viewModel.RollNext();
        UpdatePhotoDisplay();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetCurrentGroup();
        UpdatePhotoDisplay(forceHide: true);
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleMode();
        UpdateRemoteHookState();
        UpdatePhotoDisplay(forceHide: true);
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
            SystemSounds.Exclamation.Play();
        }
    }

    private void OnReminderTriggered()
    {
        if (_viewModel.TimerReminderEnabled)
        {
            SystemSounds.Asterisk.Play();
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
            if (_viewModel.IsRollCallMode)
            {
                _viewModel.RollNext();
            }
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
        _viewModel.ShowPhoto = settings.RollCallShowPhoto;
        _viewModel.PhotoDurationSeconds = settings.RollCallPhotoDurationSeconds;
        _viewModel.PhotoSharedClass = settings.RollCallPhotoSharedClass;
        _viewModel.TimerSoundEnabled = settings.RollCallTimerSoundEnabled;
        _viewModel.TimerReminderEnabled = settings.RollCallTimerReminderEnabled;
        _viewModel.TimerReminderIntervalMinutes = settings.RollCallTimerReminderIntervalMinutes;
        _viewModel.RemotePresenterEnabled = settings.RollCallRemoteEnabled;
        _viewModel.SetRemotePresenterKey(settings.RemotePresenterKey);
        UpdateRemoteHookState();
        UpdatePhotoDisplay();
    }

    private void RestartKeyboardHook()
    {
        StopKeyboardHook();
        StartKeyboardHook();
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
