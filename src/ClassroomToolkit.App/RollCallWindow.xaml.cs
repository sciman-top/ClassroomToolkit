using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.Domain.Models;
using ClassroomToolkit.Domain.Timers;
using ClassroomToolkit.Interop;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Commands;
using System.Windows.Interop;

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
    private readonly Services.Input.GlobalHookService _hookService;
    private readonly Services.Speech.SpeechService _speechService;
    private readonly string _dataPath;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _rollStateSaveTimer;
    private readonly DispatcherTimer _windowBoundsSaveTimer;
    private readonly Stopwatch _stopwatch;
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

    public RollCallWindow(
        string dataPath, 
        AppSettingsService settingsService, 
        AppSettings settings,
        Services.Input.GlobalHookService hookService,
        Services.Speech.SpeechService speechService)
    {
        InitializeComponent();
        _dataPath = dataPath;
        _settingsService = settingsService;
        _settings = settings;
        _hookService = hookService;
        _speechService = speechService;
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

    public IReadOnlyList<string> AvailableClasses => _viewModel.AvailableClasses;

    public void RequestClose()
    {
        _allowClose = true;
        Close();
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
}
