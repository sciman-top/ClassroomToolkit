using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.Application.UseCases.Photos;
using ClassroomToolkit.App.ViewModels;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

/// <summary>
/// MainWindow core: fields and constructor.
/// Lifecycle/startup/exit/settings logic -> MainWindow.Lifecycle.cs
/// Z-order coordination -> MainWindow.ZOrder.cs
/// Roll-call flow -> MainWindow.RollCall.cs
/// Paint/Ink logic → MainWindow.Paint.cs
/// Photo/Presentation logic → MainWindow.Photo.cs
/// Launcher UI logic → MainWindow.Launcher.cs
/// </summary>
public partial class MainWindow : Window
{
    private RollCallWindow? _rollCallWindow;
    private Paint.PaintOverlayWindow? _overlayWindow => _paintWindowOrchestrator.OverlayWindow;
    private Paint.PaintToolbarWindow? _toolbarWindow => _paintWindowOrchestrator.ToolbarWindow;
    private Photos.ImageManagerWindow? _imageManagerWindow;
    private readonly List<ZOrderSurface> _surfaceStack = new();
    private SurfaceZOrderDecisionRuntimeState _surfaceZOrderDecisionState = SurfaceZOrderDecisionRuntimeState.Default;
    private readonly DispatcherTimer _presentationForegroundSuppressionTimer;
    private readonly DispatcherTimer _floatingTopmostWatchdogTimer;
    private IDisposable? _presentationForegroundSuppression;
    private bool _zOrderPolicyApplying;
    private FloatingCoordinationRuntimeState _floatingCoordinationState = FloatingCoordinationRuntimeState.Default;
    private FloatingDispatchQueueState _floatingDispatchQueueState = FloatingDispatchQueueState.Default;
    private ToolbarInteractionRetouchRuntimeState _toolbarInteractionRetouchState = ToolbarInteractionRetouchRuntimeState.Default;
    private bool _toolbarDirectRepairBackgroundQueued;
    private bool _toolbarDirectRepairRerunRequested;
    private ExplicitForegroundRetouchRuntimeState _explicitForegroundRetouchState = ExplicitForegroundRetouchRuntimeState.Default;
    private OverlayActivatedRetouchRuntimeState _overlayActivatedRetouchState = OverlayActivatedRetouchRuntimeState.Default;
    private ZOrderRequestRuntimeState _zOrderRequestState = ZOrderRequestRuntimeState.Default;
    private long _lastAppliedSessionTransitionId;
    private readonly ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationSession _photoNavigationSession = new();
    private LauncherBubbleWindow? _bubbleWindow;
    private LauncherBubbleVisibilityRuntimeState _bubbleVisibilityState = LauncherBubbleVisibilityRuntimeState.Default;
    private DateTime _lastLauncherVisibleForTopmostUtc = MainWindowRuntimeDefaults.DefaultTimestampUtc;
    private readonly DispatcherTimer _autoExitTimer;
    private readonly CancellationTokenSource _backgroundTasksCancellation = new();
    private bool _backgroundTasksCancellationDisposed;
    private bool _allowClose;
    private bool _settingsSaveFailedNotified;
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly InkExportOptions _inkExportOptions;
    private readonly InkPersistenceService _inkPersistenceService;
    private readonly InkExportService _inkExportService;
    private readonly MainViewModel _mainViewModel;
    private readonly IConfigurationService _configurationService;
    private readonly IRollCallWindowFactory _rollCallWindowFactory;
    private readonly Services.IPaintWindowOrchestrator _paintWindowOrchestrator;
    private readonly Photos.IImageManagerWindowFactory _imageManagerWindowFactory;
    private readonly IWindowOrchestrator _windowOrchestrator;
    private bool _paintOrchestratorEventsWired;
    private Paint.PaintOverlayWindow? _lifecycleWiredOverlayWindow;
    private Paint.PaintToolbarWindow? _lifecycleWiredToolbarWindow;
    public MainWindow(
        AppSettingsService settingsService,
        AppSettings settings,
        InkExportOptions inkExportOptions,
        InkPersistenceService inkPersistenceService,
        InkExportService inkExportService,
        MainViewModel mainViewModel,
        IConfigurationService configurationService,
        IRollCallWindowFactory rollCallWindowFactory,
        Services.IPaintWindowOrchestrator paintWindowOrchestrator,
        Photos.IImageManagerWindowFactory imageManagerWindowFactory,
        IWindowOrchestrator windowOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(inkExportOptions);
        ArgumentNullException.ThrowIfNull(inkPersistenceService);
        ArgumentNullException.ThrowIfNull(inkExportService);
        ArgumentNullException.ThrowIfNull(mainViewModel);
        ArgumentNullException.ThrowIfNull(configurationService);
        ArgumentNullException.ThrowIfNull(rollCallWindowFactory);
        ArgumentNullException.ThrowIfNull(paintWindowOrchestrator);
        ArgumentNullException.ThrowIfNull(imageManagerWindowFactory);
        ArgumentNullException.ThrowIfNull(windowOrchestrator);

        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _inkExportOptions = inkExportOptions;
        _inkPersistenceService = inkPersistenceService;
        _inkExportService = inkExportService;
        _inkExportOptions.Scope = settings.InkExportScope;
        _inkExportOptions.MaxParallelFiles = settings.InkExportMaxParallelFiles;
        _mainViewModel = mainViewModel;
        _configurationService = configurationService;
        _rollCallWindowFactory = rollCallWindowFactory;
        _paintWindowOrchestrator = paintWindowOrchestrator;
        _imageManagerWindowFactory = imageManagerWindowFactory;
        _windowOrchestrator = windowOrchestrator;
        _autoExitTimer = new DispatcherTimer();
        _autoExitTimer.Tick += OnAutoExitTimerTick;
        _presentationForegroundSuppressionTimer = new DispatcherTimer();
        _presentationForegroundSuppressionTimer.Tick += OnPresentationForegroundSuppressionTimerTick;
        _floatingTopmostWatchdogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FloatingTopmostWatchdogPolicy.ResolveIntervalMs())
        };
        _floatingTopmostWatchdogTimer.Tick += OnFloatingTopmostWatchdogTick;
        _mainViewModel.OpenRollCallSettingsCommand = new RelayCommand(OnOpenRollCallSettings);
        _mainViewModel.OpenPaintSettingsCommand = new RelayCommand(OnOpenPaintSettings);
        DataContext = _mainViewModel;
        Loaded += OnLoaded;
        IsVisibleChanged += OnMainWindowVisibleChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

}
