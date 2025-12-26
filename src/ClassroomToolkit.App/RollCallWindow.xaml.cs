using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadData();
        _viewModel.SetRemotePresenterKey(_settings.RemotePresenterKey);
        _stopwatch.Restart();
        _timer.Start();
        StartKeyboardHook();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _stopwatch.Stop();
        StopKeyboardHook();
        _settings.RemotePresenterKey = _viewModel.RemotePresenterKey;
        _settingsService.Save(_settings);
        _viewModel.SaveState();
    }

    private void OnGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string group)
        {
            _viewModel.SetCurrentGroup(group);
        }
    }

    private void OnRollClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RollNext();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetCurrentGroup();
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleMode();
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _stopwatch.Elapsed;
        _stopwatch.Restart();
        _viewModel.TickTimer(elapsed);
    }

    private void StartKeyboardHook()
    {
        if (_keyboardHook != null)
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

    private void RestartKeyboardHook()
    {
        StopKeyboardHook();
        StartKeyboardHook();
    }
}
