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
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.RollCall;
using ClassroomToolkit.App.Windowing;
using System.Windows.Interop;
using ClassroomToolkit.Application.UseCases.RollCall;

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
    private readonly ClassroomToolkit.Services.Input.GlobalHookService _hookService;
    private readonly RollCallRemoteHookCoordinator _remoteHookCoordinator;
    private readonly ClassroomToolkit.Services.Speech.SpeechService _speechService;
    private readonly string _dataPath;
    private readonly RollCallWorkbookUseCase _rollCallWorkbookUseCase;
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
    private int _speechUnavailableNotifiedState;
    private int _remoteHookUnavailableNotifiedState;
    private bool _initialized;
    private bool _dataLoaded;
    private bool _settingsSaveFailedNotified;
    private bool _hovering;
    private bool _windowBoundsDirty;
    private DateTime _suppressRollUntil = RollCallRuntimeDefaults.UnsetTimestampUtc;
    private IntPtr _hwnd;
    private bool? _lastTransparentStyleEnabled;
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
        ClassroomToolkit.Services.Input.GlobalHookService hookService,
        ClassroomToolkit.Services.Speech.SpeechService speechService,
        RollCallWorkbookUseCase rollCallWorkbookUseCase)
    {
        InitializeComponent();
        _dataPath = dataPath;
        _settingsService = settingsService;
        _settings = settings;
        _hookService = hookService;
        _remoteHookCoordinator = new RollCallRemoteHookCoordinator(
            RegisterRemoteHookAsync,
            RollCallRemoteHookBindingPolicy.ResolveTokens,
            () => _hookService.UnregisterAll());
        _speechService = speechService;
        _speechService.SpeechUnavailable += OnSpeechUnavailable;
        _rollCallWorkbookUseCase = rollCallWorkbookUseCase;
        ApplyWindowBounds(settings);
        
        _viewModel = new RollCallViewModel(dataPath, _rollCallWorkbookUseCase);
        DataContext = _viewModel;
        _viewModel.GroupButtons.CollectionChanged += OnGroupButtonsCollectionChanged;
        
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        MouseEnter += OnWindowMouseEnter;
        MouseLeave += OnWindowMouseLeave;
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
        _windowBoundsSaveTimer.Tick += OnWindowBoundsSaveTick;
        
        _hoverCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _hoverCheckTimer.Tick += OnHoverCheckTimerTick;

        OpenRemoteKeyCommand = new RelayCommand(OpenRemoteKeyDialog);
        _viewModel.TimerCompleted += OnTimerCompleted;
        _viewModel.ReminderTriggered += OnReminderTriggered;
        _viewModel.DataLoadFailed += OnDataLoadFailed;
        _viewModel.DataSaveFailed += OnDataSaveFailed;

        PaintModeManager.Instance.PaintModeChanged += OnPaintModeChanged;
        PaintModeManager.Instance.IsDrawingChanged += OnDrawingStateChanged;

        SizeChanged += OnWindowSizeChanged;
        LocationChanged += OnWindowLocationChanged;
        
        IsVisibleChanged += OnWindowVisibilityChanged;
        StateChanged += OnWindowStateChanged;
    }

    public IReadOnlyList<string> AvailableClasses => _viewModel.AvailableClasses;

    public void RequestClose()
    {
        _allowClose = true;
        ExecuteRollCallSafe("request-close", Close);
    }

    private void ExecuteRollCallSafe(string operation, Action action)
    {
        SafeActionExecutionExecutor.TryExecute(
            action,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatWindowLifecycleFailureMessage(
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private bool TryShowDialogSafe(Window dialog, string dialogName)
    {
        var result = false;
        SafeActionExecutionExecutor.TryExecute(
            () => result = dialog.SafeShowDialog() == true,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatDialogShowFailureMessage(
                    dialogName,
                    ex.GetType().Name,
                    ex.Message)));
        return result;
    }

    private void ShowRollCallInfoMessageSafe(string operation, string message, Window? owner = null)
    {
        SafeActionExecutionExecutor.TryExecute(
            () => System.Windows.MessageBox.Show(
                owner ?? this,
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information),
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatWindowLifecycleFailureMessage(
                    $"messagebox-{operation}",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private bool TryShowRollCallConfirmationSafe(string operation, string prompt)
    {
        var confirmed = false;
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var result = System.Windows.MessageBox.Show(
                    this,
                    prompt,
                    "提示",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                confirmed = result == MessageBoxResult.OK;
            },
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatConfirmationFailureMessage(
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
        return confirmed;
    }

    private Task<bool> RegisterRemoteHookAsync(
        IEnumerable<string> tokens,
        Action callback,
        Func<bool> shouldKeepActive)
    {
        return _hookService.RegisterHookAsync(tokens, callback, shouldKeepActive);
    }

    private void OnPaintModeChanged(bool _)
    {
        UpdateWindowTransparency();
    }

    private void OnDrawingStateChanged(bool _)
    {
        UpdateWindowTransparency();
    }

    private void OnGroupButtonsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateMinWindowSize();
    }

    private void OnWindowBoundsSaveTick(object? sender, EventArgs e)
    {
        SaveWindowBoundsIfNeeded();
    }

    private void OnHoverCheckTimerTick(object? sender, EventArgs e)
    {
        UpdateHoverState();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleWindowBoundsSave();
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        ScheduleWindowBoundsSave();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        UpdateWindowTransparency();
        SourceInitialized -= OnSourceInitialized;
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
            _viewModel.WarmupData(_dataPath);
            await _viewModel.LoadDataAsync(_settings.RollCallCurrentClass, Dispatcher);
            RestoreGroupSelection();
            // 启动时不显示之前点名状态的学生照片，避免意外显示
            UpdatePhotoDisplay(forceHide: true);
            WindowPlacementHelper.EnsureVisible(this);
            _stopwatch.Restart();
            _timer.Start();
            UpdateRemoteHookState();
            _dataLoaded = true;
            IsDataLoaded = true;
            UpdateMinWindowSize();
            UpdateWindowTransparency();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            _initialized = false;
            System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatInitializationFailureMessage(
                    ex.GetType().Name,
                    ex.Message));
            var owner = System.Windows.Application.Current?.MainWindow;
            ShowRollCallInfoMessageSafe(
                "initialize",
                "点名窗口初始化失败，请稍后重试。",
                owner);
        }
    }
}

