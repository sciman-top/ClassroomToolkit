using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Threading;
using IoPath = System.IO.Path;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Application.UseCases.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.Services.Presentation;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfPath = System.Windows.Shapes.Path;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using System.Windows.Interop;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Presentation;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{

    
    // Win32 绐楀彛鏍峰紡甯搁噺
    private const int GwlStyle = -16;
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;

    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    
    /// <summary>婕旂ず鏂囩鐒︾偣鐩戞帶闂撮殧锛坢s锛?- 骞宠　鍝嶅簲閫熷害鍜?CPU 鍗犵敤</summary>
    private const int PresentationFocusMonitorIntervalMs = PresentationRuntimeDefaults.FocusMonitorIntervalMs;
    
    /// <summary>婕旂ず鏂囩鐒︾偣鎭㈠鍐峰嵈鏃堕棿锛坢s锛?- 闃叉棰戠箒鍒囨崲瀵艰嚧闂儊</summary>
    private const int PresentationFocusCooldownMs = PresentationRuntimeDefaults.FocusRestoreCooldownMs;
    

    
    /// <summary>璺ㄩ〉鏇存柊鏈€灏忛棿闅旓紙ms锛?- 杩炵画婊氬姩鏃剁殑娓叉煋鑺傛祦</summary>
    private const int CrossPageUpdateMinIntervalMs = CrossPageRuntimeDefaults.UpdateMinIntervalMs;
    
    /// <summary>鐓х墖婊氳疆缂╂斁鍩烘暟 - 姣忔婊氳疆鍒诲害鐨勭缉鏀剧郴鏁帮紙鎺ヨ繎 1.0 浣跨缉鏀惧钩婊戯級</summary>
    private const double PhotoWheelZoomBaseDefault = PhotoZoomInputDefaults.WheelZoomBaseDefault;
    
    /// <summary>鐓х墖鎸夐敭缂╂斁姝ヨ繘 - 浣跨敤 +/- 閿椂鐨勭缉鏀惧箙搴?/summary>
    private const double PhotoKeyZoomStep = 1.06;
    private const double PhotoGestureZoomNoiseThreshold = PhotoZoomInputDefaults.GestureZoomNoiseThreshold;
    private const double PhotoZoomMinEventFactor = PhotoZoomInputDefaults.ZoomMinEventFactor;
    private const double PhotoZoomMaxEventFactor = PhotoZoomInputDefaults.ZoomMaxEventFactor;
    private const int PhotoWheelSuppressAfterGestureMs = PhotoTransformTimingDefaults.WheelSuppressAfterGestureMs;

    private IntPtr _hwnd;
    private bool _inputPassthroughEnabled;
    private bool _focusBlocked;
    private bool? _lastAppliedInputPassthroughEnabled;
    private bool? _lastAppliedFocusBlocked;
    private bool _forcePresentationForegroundOnFullscreen;
    private bool _presentationClassifierAutoLearnEnabled;
    private readonly DispatcherTimer _presentationFocusMonitor;
    private DateTime _nextPresentationFocusAttempt = PresentationRuntimeDefaults.UnsetTimestampUtc;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;


    private PaintToolMode _mode = PaintToolMode.Brush;
    private PaintShapeType _shapeType = PaintShapeType.Line;

    private bool _isDrawingShape;
    private WpfPoint _shapeStart;
    private Shape? _activeShape;
    private bool _triangleFirstEdgeCommitted;
    private bool _triangleAnchorSet;
    private WpfPoint _trianglePoint1;
    private WpfPoint _trianglePoint2;
    private bool _isRegionSelecting;
    private WpfPoint _regionStart;
    private WpfRectangle? _regionRect;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlService _presentationService;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlOptions _presentationOptions;
    private readonly PresentationInputPipeline _presentationInputPipeline;
    private readonly IOverlayPresentationTargetSnapshotProvider _presentationTargetSnapshotProvider;
    private readonly OverlayPresentationDispatchCoordinator _presentationDispatchCoordinator;
    private PresentationClassifier _presentationClassifier;
    private readonly Win32PresentationResolver _presentationResolver;
    private readonly WpsSlideshowNavigationHook? _wpsNavHook;
    private readonly IWpsNavHookClient? _wpsNavHookClient;
    private readonly WpsHookOrchestrator _wpsHookOrchestrator = new();
    private readonly IWpsHookUnavailableNotifier _wpsHookUnavailableNotifier = new MessageBoxWpsHookUnavailableNotifier();
    private readonly LatestOnlyAsyncGate _wpsNavHookStateGate = new();
    private const int WpsNavDebounceMs = PresentationRuntimeDefaults.WpsNavDebounceMs;
    private bool _wpsNavHookActive;
    private bool _wpsHookInterceptKeyboard = true;
    private bool _wpsHookInterceptWheel = true;
    private bool _wpsHookBlockOnly;
    private int _wpsHookUnavailableNotifiedState;
    private DateTime _wpsNavBlockUntil = PresentationRuntimeDefaults.UnsetTimestampUtc;
    private (int Code, IntPtr Target, DateTime Timestamp)? _lastWpsNavEvent;
    private DateTime _lastWpsHookInput = PresentationRuntimeDefaults.UnsetTimestampUtc;
    private readonly List<RasterSnapshot> _history = new();


    private WpfPoint? _lastPointerPosition;
    private DateTime _lastPhotoGestureInputUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
    private bool _presentationFocusRestoreEnabled = false;

    private readonly RefreshOrchestrator _refreshOrchestrator;
    private readonly PerfStats _perfMonitor;
    private readonly PerfStats _perfSavePage;
    private readonly PerfStats _perfLoadPage;
    private readonly PerfStats _perfRedrawSurface;
    private readonly PerfStats _perfEnsureSurface;
    private readonly PerfStats _perfClearSurface;
    private readonly PerfStats _perfApplyStrokes;

    private bool _presentationFullscreenActive;
    private DateTime _currentCourseDate = DateTime.Today;
    private string _currentDocumentName = string.Empty;
    private string _currentDocumentPath = string.Empty;
    private int _currentPageIndex = 1;
    private string _currentCacheKey = string.Empty;
    private PresentationType _currentPresentationType = PresentationType.None;

    private const double PdfDefaultDpi = PhotoDocumentRuntimeDefaults.PdfDefaultDpi;
    private const int PdfCacheLimit = PhotoDocumentRuntimeDefaults.PdfCacheLimit;
    private bool _photoModeActive;
    private bool _photoInputTelemetryEnabled;
    private bool _photoFullscreen;
    private bool _photoLoading;
    private CrossPageDisplayUpdateRuntimeState _crossPageDisplayUpdateState = CrossPageDisplayUpdateRuntimeState.Default;
    private CrossPageReplayRuntimeState _crossPageReplayState = CrossPageReplayRuntimeState.Default;
    private CrossPageDisplayUpdateClockState _crossPageDisplayUpdateClockState = CrossPageDisplayUpdateClockState.Default;
    private CrossPageUpdateRequestRuntimeState _crossPageUpdateRequestState = CrossPageUpdateRequestRuntimeState.Default;
    private CrossPageInkVisualSyncRuntimeState _crossPageInkVisualSyncState = CrossPageInkVisualSyncRuntimeState.Default;
    private ScaleTransform _photoPageScale = new ScaleTransform(PhotoTransformViewportDefaults.DefaultScale, PhotoTransformViewportDefaults.DefaultScale);
    private ScaleTransform _photoScale = new ScaleTransform(PhotoTransformViewportDefaults.DefaultScale, PhotoTransformViewportDefaults.DefaultScale);
    private TranslateTransform _photoTranslate = new TranslateTransform(0, 0);
    private TranslateTransform _photoInkPanCompensation = new TranslateTransform(0, 0);
    private double _lastInkRedrawPhotoTranslateX;
    private double _lastInkRedrawPhotoTranslateY;
    private double _lastPhotoInteractiveRefreshTranslateX;
    private double _lastPhotoInteractiveRefreshTranslateY;
    private bool _photoPanning;
    private bool _photoManipulating;
    private bool _photoPanHadEffectiveMovement;
    private bool _photoRightClickPending;
    private WpfPoint _photoRightClickStart;
    private WpfPoint _photoPanStart;
    private double _photoPanOriginX;
    private double _photoPanOriginY;
    private readonly List<PhotoPanVelocitySample> _photoPanVelocitySamples = new(PhotoPanInertiaDefaults.MouseVelocitySampleCapacity);
    private string _photoInertiaProfile = PhotoInertiaProfileDefaults.Standard;
    private PhotoPanInertiaTuning _photoPanInertiaTuning = PhotoPanInertiaTuning.Default;
    private Vector _photoPanInertiaVelocityDipPerMs;
    private DateTime _photoPanInertiaLastTickUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
    private DateTime _photoPanInertiaStartUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
    private TimeSpan _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
    private bool _photoPanInertiaRenderingAttached;
    private bool _photoRestoreFullscreenPending;
    private int _photoFullscreenBoundsToken;
    private bool _photoDocumentIsPdf;
    private PdfDocumentHost? _pdfDocument;
    private int _pdfPageCount;
    private int _lastPdfNavigationDirection;
    private readonly Dictionary<int, BitmapSource> _pdfPageCache = new();
    private readonly LinkedList<int> _pdfPageOrder = new();
    private readonly object _pdfRenderLock = new();
    private const int NeighborPageCacheLimit = PhotoDocumentRuntimeDefaults.NeighborPageCacheLimit;
    private int _pdfPrefetchInFlight;
    private int _pdfPrefetchToken;
    private readonly HashSet<int> _pdfPinnedPages = new();
    private int _pdfVisiblePrefetchInFlight;
    private int _pdfVisiblePrefetchToken;
    private int _photoLoadToken;
    private double _photoWheelZoomBase = PhotoWheelZoomBaseDefault;
    private double _photoGestureZoomSensitivity = PhotoZoomInputDefaults.GestureSensitivityDefault;
    private readonly HashSet<string> _neighborInkRenderPending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _neighborInkSidecarLoadPending = new(StringComparer.OrdinalIgnoreCase);
    private bool _rememberPhotoTransform;
    private bool _photoUserTransformDirty;
    private double _lastPhotoScaleX = PhotoTransformViewportDefaults.DefaultScale;
    private double _lastPhotoScaleY = PhotoTransformViewportDefaults.DefaultScale;
    private double _lastPhotoTranslateX;
    private double _lastPhotoTranslateY;
    private readonly Dictionary<string, PhotoTransformState> _photoPageTransforms = new(StringComparer.OrdinalIgnoreCase);
    private bool _photoUnifiedTransformReady;
    private DispatcherTimer? _photoUnifiedTransformSaveTimer;
    private DispatcherTimer? _photoTransformSaveTimer;
    private bool _photoTransformSavePending;
    private bool _photoTransformSaveUserAdjusted;
    private bool _foregroundPresentationActive;
    private PresentationType _foregroundPresentationType;
    private IntPtr _foregroundPresentationHandle;
    private bool _foregroundPhotoActive;
    private double _pendingUnifiedScaleX;
    private double _pendingUnifiedScaleY;
    private double _pendingUnifiedTranslateX;
    private double _pendingUnifiedTranslateY;
    // Cross-page display (continuous scroll) feature
    private bool _crossPageDisplayEnabled;
    private bool _crossPageDragging;
    private bool _crossPageTranslateClamped;
    private bool _crossPageUpdateDeferredByInkInput;
    private int _lastInputSwitchFromPage;
    private int _lastInputSwitchToPage;
    private DateTime _lastInputSwitchUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private int _photoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputRefreshDefaultMs;
    private DateTime _lastCrossPagePointerUpUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private int _crossPagePostInputRefreshToken;
    private int _crossPageMissingBitmapRefreshToken;
    private DateTime _lastCrossPageMissingBitmapRefreshUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private long _crossPagePointerUpSequence;
    private long _crossPagePostInputRefreshAppliedSequence = -1;
    private List<string> _photoSequencePaths = new();
    private int _photoSequenceIndex = -1;
    private readonly Dictionary<int, BitmapSource> _neighborImageCache = new();
    private readonly Dictionary<int, double> _neighborPageHeightCache = new();
    private readonly HashSet<int> _neighborImagePrefetchPending = new();
    private System.Windows.Controls.Canvas? _neighborPagesCanvas;
    private readonly List<WpfImage> _neighborPageImages = new();
    private readonly List<WpfImage> _neighborInkImages = new();
    private DateTime _lastNeighborPagesNonEmptyUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private readonly Dictionary<string, InkBitmapCacheEntry> _neighborInkCache = new(StringComparer.OrdinalIgnoreCase);
    private int _interactiveSwitchPinnedNeighborPage;
    private DateTime _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private double _crossPageNormalizedWidthDip;
    private bool _crossPageBoundsCacheValid;
    private bool _crossPageBoundsCacheIncludeSlack;
    private int _crossPageBoundsCacheCurrentPage;
    private int _crossPageBoundsCacheTotalPages;
    private double _crossPageBoundsCacheViewportWidth;
    private double _crossPageBoundsCacheViewportHeight;
    private double _crossPageBoundsCacheNormalizedWidthDip;
    private double _crossPageBoundsCacheScaleX;
    private double _crossPageBoundsCacheScaleY;
    private double _crossPageBoundsCacheMinX;
    private double _crossPageBoundsCacheMaxX;
    private double _crossPageBoundsCacheMinY;
    private double _crossPageBoundsCacheMaxY;
    private DateTime _crossPageBoundsCacheUpdatedUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    private TransformGroup? _photoContentTransform;
    private readonly SessionCoordinator _sessionCoordinator;
    private readonly CancellationTokenSource _overlayLifecycleCancellation = new();
    private int _overlayClosed;


    public event Action<string, DateTime>? InkContextChanged;
    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action<FloatingZOrderRequest>? FloatingZOrderRequested;
    public event Action? PresentationFullscreenDetected;
    public event Action<PresentationForegroundSource>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;
    public event Action? PhotoCloseRequested;
    public event Action? PhotoCursorModeFocusRequested;
    public event Action<UiSessionTransition>? UiSessionTransitionOccurred;

    public UiSessionState CurrentSessionState => _sessionCoordinator.CurrentState;
    public IReadOnlyList<string> CurrentSessionViolations => _sessionCoordinator.LastViolations;

    private static DateTime GetCurrentUtcTimestamp() => DateTime.UtcNow;




    public PaintOverlayWindow()
    {
        InitializeComponent();
        _inkRendererFactory = InkRendererFactoryResolver.Resolve(
            AppFlags.UseGpuInkRenderer,
            out _);
        _neighborPagesCanvas = FindName("NeighborPagesCanvas") as System.Windows.Controls.Canvas;
        _visualHost = new DrawingVisualHost();
        CustomDrawHost.Child = _visualHost;
        _photoContentTransform = new TransformGroup();
        _photoContentTransform.Children.Add(_photoPageScale);
        _photoContentTransform.Children.Add(_photoScale);
        _photoContentTransform.Children.Add(_photoTranslate);
        PhotoBackground.RenderTransform = _photoContentTransform;
        RasterImage.RenderTransform = _photoInkPanCompensation;
        
        WindowStateTransitionExecutor.Apply(this, WindowState.Maximized);
        _refreshOrchestrator = new RefreshOrchestrator(Dispatcher, MonitorInkContext);
        _presentationFocusMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PresentationFocusMonitorIntervalMs)
        };
        _presentationFocusMonitor.Tick += OnPresentationFocusMonitorTick;
        _inkMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InkMonitorActiveIntervalMs)
        };
        _inkMonitor.Tick += OnInkMonitorTick;
        _inkSidecarAutoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InkSidecarAutoSaveDelayMs)
        };
        _inkSidecarAutoSaveTimer.Tick += OnInkSidecarAutoSaveTimerTick;
        _perfMonitor = new PerfStats("MonitorInkContext");
        _perfSavePage = new PerfStats("SavePage");
        _perfLoadPage = new PerfStats("LoadPage");
        _perfRedrawSurface = new PerfStats("RedrawSurface");
        _perfEnsureSurface = new PerfStats("EnsureSurface");
        _perfClearSurface = new PerfStats("ClearSurface");
        _perfApplyStrokes = new PerfStats("ApplyStrokes");
        
        KeyDown += OnKeyDown;
        Loaded += OnOverlayLoaded;
        IsVisibleChanged += OnOverlayVisibleChanged;
        SourceInitialized += OnOverlaySourceInitialized;
        Deactivated += OnOverlayDeactivated;
        OverlayRoot.MouseLeftButtonDown += OnMouseDown;
        OverlayRoot.MouseMove += OnMouseMove;
        OverlayRoot.MouseLeftButtonUp += OnMouseUp;
        OverlayRoot.MouseRightButtonDown += OnRightButtonDown;
        OverlayRoot.MouseRightButtonUp += OnRightButtonUp;
        OverlayRoot.MouseLeave += OnOverlayMouseLeave;
        OverlayRoot.LostMouseCapture += OnOverlayLostMouseCapture;
        OverlayRoot.IsManipulationEnabled = true;
        OverlayRoot.ManipulationStarting += OnManipulationStarting;
        OverlayRoot.ManipulationInertiaStarting += OnManipulationInertiaStarting;
        OverlayRoot.ManipulationDelta += OnManipulationDelta;
        OverlayRoot.ManipulationCompleted += OnManipulationCompleted;
        OverlayRoot.StylusDown += OnStylusDown;
        OverlayRoot.StylusMove += OnStylusMove;
        OverlayRoot.StylusUp += OnStylusUp;
        MouseWheel += OnMouseWheel;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        UpdateBoardBackground();

        _presentationClassifier = new PresentationClassifier();
        var planner = new ClassroomToolkit.Services.Presentation.PresentationControlPlanner(_presentationClassifier);
        var mapper = new ClassroomToolkit.Services.Presentation.PresentationCommandMapper();
        var sender = new Win32InputSender();
        _presentationResolver = new Win32PresentationResolver();
        _presentationService = new ClassroomToolkit.Services.Presentation.PresentationControlService(planner, mapper, sender, _presentationResolver, new ClassroomToolkit.Services.Presentation.Win32PresentationWindowValidator());
        _presentationOptions = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = InputStrategy.Auto,
            WheelAsKey = false,
            WpsDebounceMs = PresentationRuntimeDefaults.WpsNavDebounceMs,
            LockStrategyWhenDegraded = true,
            AllowOffice = true,
            AllowWps = true
        };
        _presentationInputPipeline = new PresentationInputPipeline(
            _presentationService,
            _presentationOptions.Strategy,
            InputStrategy.Auto);
        _presentationTargetSnapshotProvider = new OverlayPresentationTargetSnapshotProvider(
            _presentationResolver,
            () => _presentationClassifier,
            IsFullscreenWindow,
            _currentProcessId);
        _presentationDispatchCoordinator = new OverlayPresentationDispatchCoordinator(_presentationTargetSnapshotProvider);
        _wpsNavHook = new WpsSlideshowNavigationHook();
        var navHook = _wpsNavHook;
        _wpsNavHookClient = navHook == null ? null : new WpsNavHookClient(navHook);
        if (navHook != null && navHook.Available)
        {
            navHook.NavigationRequested += OnWpsNavHookRequested;
        }
        _sessionCoordinator = new SessionCoordinator(
            new PaintOverlaySessionEffectRunner(
                applyOverlayTopmost: ApplySessionOverlayTopmost,
                applyNavigationMode: ApplySessionNavigationMode,
                applyInkVisibility: ApplySessionInkVisibility,
                applyWidgetVisibility: ApplySessionWidgetVisibility,
                onTransition: LogSessionTransition));
        DispatchSessionEvent(new SwitchToolModeEvent(MapSessionToolMode(_mode)));
        Closed += OnOverlayClosed;
    }

    private void OnOverlayLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
        EnsureRasterSurface();
    }

    private void OnPresentationFocusMonitorTick(object? sender, EventArgs e)
    {
        MonitorPresentationFocus();
    }

    private void OnInkMonitorTick(object? sender, EventArgs e)
    {
        _refreshOrchestrator.RequestRefresh("poll");
    }

    private void OnInkSidecarAutoSaveTimerTick(object? sender, EventArgs e)
    {
        _inkSidecarAutoSaveTimer?.Stop();
        if (IsInkOperationActive())
        {
            _inkDiagnostics?.OnAutoSaveDeferred("timer-active-operation");
            ScheduleSidecarAutoSave();
            return;
        }
        if (!TryCaptureSidecarPersistSnapshot(requireDirty: true, out var snapshot) || snapshot == null)
        {
            return;
        }
        QueueSidecarAutoSave(snapshot);
    }

    private void OnOverlayVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            WindowPlacementHelper.EnsureVisible(this);
        }

        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _lastAppliedInputPassthroughEnabled = null;
        _lastAppliedFocusBlocked = null;
        UpdateInputPassthrough();
        UpdateFocusAcceptance();
    }

    private void OnOverlayDeactivated(object? sender, EventArgs e)
    {
        CancelPendingTriangleDraft("overlay-deactivated");
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _overlayClosed, 1);
        _overlayLifecycleCancellation.Cancel();
        Closed -= OnOverlayClosed;
        KeyDown -= OnKeyDown;
        Loaded -= OnOverlayLoaded;
        IsVisibleChanged -= OnOverlayVisibleChanged;
        SourceInitialized -= OnOverlaySourceInitialized;
        Deactivated -= OnOverlayDeactivated;
        MouseWheel -= OnMouseWheel;
        SizeChanged -= OnWindowSizeChanged;
        StateChanged -= OnWindowStateChanged;
        OverlayRoot.MouseLeftButtonDown -= OnMouseDown;
        OverlayRoot.MouseMove -= OnMouseMove;
        OverlayRoot.MouseLeftButtonUp -= OnMouseUp;
        OverlayRoot.MouseRightButtonDown -= OnRightButtonDown;
        OverlayRoot.MouseRightButtonUp -= OnRightButtonUp;
        OverlayRoot.MouseLeave -= OnOverlayMouseLeave;
        OverlayRoot.LostMouseCapture -= OnOverlayLostMouseCapture;
        OverlayRoot.ManipulationStarting -= OnManipulationStarting;
        OverlayRoot.ManipulationInertiaStarting -= OnManipulationInertiaStarting;
        OverlayRoot.ManipulationDelta -= OnManipulationDelta;
        OverlayRoot.ManipulationCompleted -= OnManipulationCompleted;
        if (_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = false;
        }
        OverlayRoot.StylusDown -= OnStylusDown;
        OverlayRoot.StylusMove -= OnStylusMove;
        OverlayRoot.StylusUp -= OnStylusUp;
        SaveCurrentPageIfNeeded();
        _photoTransformSaveTimer?.Stop();
        _photoTransformSaveTimer?.Tick -= OnPhotoTransformSaveTimerTick;
        _photoUnifiedTransformSaveTimer?.Stop();
        _photoUnifiedTransformSaveTimer?.Tick -= OnPhotoUnifiedTransformSaveTimerTick;
        _presentationFocusMonitor.Tick -= OnPresentationFocusMonitorTick;
        _inkMonitor.Tick -= OnInkMonitorTick;
        _inkSidecarAutoSaveTimer?.Tick -= OnInkSidecarAutoSaveTimerTick;
        _inkSidecarAutoSaveTimer?.Stop();
        _inkSidecarAutoSaveGate.NextGeneration();
        _wpsNavHookStateGate.NextGeneration();
        StopWpsNavHook();
        if (_wpsNavHook != null && _wpsNavHook.Available)
        {
            _wpsNavHook.NavigationRequested -= OnWpsNavHookRequested;
            _wpsNavHook.Dispose();
        }
        _wpsNavHookStateGate.Dispose();
        _inkSidecarAutoSaveGate.Dispose();
        _presentationFocusMonitor.Stop();
        _inkMonitor.Stop();
        ClosePdfDocument();
        _overlayLifecycleCancellation.Dispose();
    }

    public void SetMode(PaintToolMode mode)
    {
        if (mode != PaintToolMode.Shape)
        {
            CancelPendingTriangleDraft($"mode-switch:{_mode}->{mode}");
        }

        _mode = mode;
        DispatchSessionEvent(new SwitchToolModeEvent(MapSessionToolMode(mode)));
        UpdateOverlayHitTestVisibility();
        if (PhotoCursorModeFocusRequestPolicy.ShouldRequestFocus(_photoModeActive, mode))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => PhotoCursorModeFocusRequested?.Invoke(),
                ex => Debug.WriteLine($"[PhotoCursorModeFocusRequested] callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
        
        // 鏇存柊鍏ㄥ眬缁樺浘妯″紡鐘舵€?
        var isPaintMode = mode != PaintToolMode.Cursor;
        PaintModeManager.Instance.IsPaintMode = isPaintMode;
        
        // 绔嬪嵆璁剧疆鍏夋爣锛堝厜鏍囨ā寮忎娇鐢ㄧ郴缁熷厜鏍囷紝鏃犻渶鍒涘缓鏂囦欢锛?
        if (mode == PaintToolMode.Cursor)
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        else
        {
            // 鍏朵粬妯″紡鐨勫厜鏍囨洿鏂板欢杩熸墽琛岋紝閬垮厤闃诲
            var cursorUpdateScheduled = TryBeginInvoke(() =>
            {
                UpdateCursor(mode);
            }, System.Windows.Threading.DispatcherPriority.Normal);
            if (!cursorUpdateScheduled && Dispatcher.CheckAccess())
            {
                UpdateCursor(mode);
            }
        }
        
        if (mode != PaintToolMode.RegionErase)
        {
            ClearRegionSelection();
        }
        if (mode != PaintToolMode.Shape)
        {
            ClearShapePreview();
        }
        HideEraserPreview();
        
        // 绔嬪嵆鏇存柊杈撳叆绌块€忕姸鎬侊紙杞婚噺绾ф搷浣滐級
        UpdateInputPassthrough();
        if (PhotoPanModeSwitchPolicy.ShouldEndPan(
                _photoPanning,
                _photoModeActive,
                IsBoardActive(),
                _mode,
                IsInkOperationActive()))
        {
            EndPhotoPan(allowInertia: false);
        }
        
        // 寤惰繜鏇存柊閽╁瓙鍜岀劍鐐圭姸鎬侊紝閬垮厤鍗￠】
        var modeFollowUpScheduled = TryBeginInvoke(() =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            
            // 鍏夋爣妯″紡涓嬫仮澶嶇劍鐐?
            if (mode == PaintToolMode.Cursor)
            {
                RestorePresentationFocusIfNeeded(requireFullscreen: false);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
        if (!modeFollowUpScheduled && Dispatcher.CheckAccess())
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            if (mode == PaintToolMode.Cursor)
            {
                RestorePresentationFocusIfNeeded(requireFullscreen: false);
            }
        }
    }

    private void DispatchSessionEvent(UiSessionEvent sessionEvent)
    {
        var transition = _sessionCoordinator.Dispatch(sessionEvent);
        SafeActionExecutionExecutor.TryExecute(
            () => UiSessionTransitionOccurred?.Invoke(transition),
            ex => Debug.WriteLine($"[UiSessionTransitionOccurred] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private void ApplySessionOverlayTopmost(bool topmostRequired)
    {
        if (!topmostRequired)
        {
            return;
        }

        EnsureOverlayTopmost(enforceZOrder: false);
        if (UiSessionFloatingZOrderRequestPolicy.TryResolveForOverlayTopmost(
                topmostRequired,
                out var request))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(request),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] session callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private void EnsureOverlayTopmost(bool enforceZOrder)
    {
        if (!OverlayTopmostApplyGatePolicy.ShouldApply(IsVisible, WindowState))
        {
            return;
        }

        WindowTopmostExecutor.ApplyNoActivate(this, enabled: true, enforceZOrder: enforceZOrder);
    }

    private void ApplySessionNavigationMode(UiNavigationMode _)
    {
        UpdateWpsNavHookState();
        UpdateInputPassthrough();
        UpdateFocusAcceptance();
    }

    private void ApplySessionInkVisibility(UiInkVisibility _)
    {
        // Place-holder effect hook for future ink visibility policy.
    }

    private void ApplySessionWidgetVisibility(UiSessionWidgetVisibility _)
    {
        if (UiSessionFloatingZOrderRequestPolicy.TryResolveForWidgetVisibility(_, out var request))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(request),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] session callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private static UiToolMode MapSessionToolMode(PaintToolMode mode)
    {
        return mode == PaintToolMode.Cursor ? UiToolMode.Cursor : UiToolMode.Draw;
    }

    private static PresentationSourceKind MapPresentationSource(PresentationType type)
    {
        return SessionSceneSourceMapper.MapPresentationSource(MapPresentationForegroundSource(type));
    }

    private static PresentationForegroundSource MapPresentationForegroundSource(PresentationType type)
    {
        return type switch
        {
            PresentationType.Office => PresentationForegroundSource.Office,
            PresentationType.Wps => PresentationForegroundSource.Wps,
            _ => PresentationForegroundSource.Unknown
        };
    }

    private static PhotoSourceKind MapPhotoSource(bool isPdf)
    {
        return SessionSceneSourceMapper.MapPhotoSource(isPdf);
    }

    private static void LogSessionTransition(UiSessionTransition transition)
    {
        Debug.WriteLine(
            $"[UiSession] #{transition.Id} evt={transition.Event.GetType().Name} " +
            $"scene={transition.Previous.Scene}->{transition.Current.Scene} " +
            $"tool={transition.Previous.ToolMode}->{transition.Current.ToolMode} " +
            $"nav={transition.Previous.NavigationMode}->{transition.Current.NavigationMode} " +
            $"focus={transition.Previous.FocusOwner}->{transition.Current.FocusOwner} " +
            $"widgets={transition.Current.RollCallVisible}/{transition.Current.LauncherVisible}/{transition.Current.ToolbarVisible} " +
            $"inkDirty={transition.Previous.InkDirty}->{transition.Current.InkDirty}");
    }

    private void UpdateCursor(PaintToolMode mode)
    {
        System.Windows.Input.Cursor cursor = mode switch
        {
            PaintToolMode.Cursor => System.Windows.Input.Cursors.Arrow,
            PaintToolMode.Brush => Utilities.CustomCursors.GetBrushCursor(_brushColor),  // 甯﹂鑹茬殑鐢荤瑪鏍峰紡
            PaintToolMode.Eraser => Utilities.CustomCursors.GetEraserCursor(_eraserSize),  // 姗＄毊鎿︽牱寮?
            PaintToolMode.Shape => System.Windows.Input.Cursors.Cross,     // 鍗佸瓧鍑嗘槦锛岀簿纭粯鍒?
            PaintToolMode.RegionErase => Utilities.CustomCursors.RegionErase, // 妗嗛€夋牱寮?
            _ => System.Windows.Input.Cursors.Arrow
        };

        this.Cursor = cursor;
    }

    public void SetBrush(MediaColor color, double size, byte opacity)
    {
        _brushColor = color;
        _brushSize = Math.Max(1.0, size);
        _brushOpacity = opacity;

        // 濡傛灉褰撳墠鏄敾绗旀ā寮忥紝鏇存柊鍏夋爣浠ユ樉绀烘柊棰滆壊
        if (_mode == PaintToolMode.Brush)
        {
            UpdateCursor(PaintToolMode.Brush);
        }
    }

    public void SetEraserSize(double size)
    {
        _eraserSize = Math.Max(4.0, size);
        if (_mode == PaintToolMode.Eraser)
        {
            UpdateCursor(PaintToolMode.Eraser);
        }
    }

    public void SetShapeType(PaintShapeType type)
    {
        if (_shapeType == PaintShapeType.Triangle)
        {
            CancelPendingTriangleDraft($"shape-switch:{_shapeType}->{type}");
        }

        _shapeType = type;
        if (_shapeType != PaintShapeType.Triangle)
        {
            ResetTriangleState();
        }
    }



    public void ClearAll()
    {
        if (_hasDrawing)
        {
            PushHistory();
        }
        ClearSurface();
        _visualHost.Clear();
        ClearShapePreview();
        ClearRegionSelection();
        _hasDrawing = false;
        if (_inkRecordEnabled)
        {
            _inkStrokes.Clear();
            NotifyInkStateChanged(updateActiveSnapshot: true);
        }

        if (_photoModeActive)
        {
            ClearPhotoInkStateAfterClearAll();
        }
    }

    private void ClearPhotoInkStateAfterClearAll()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }

        var sourcePath = _currentDocumentPath;
        var pageIndex = Math.Max(1, _currentPageIndex);
        var currentCacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, _photoDocumentIsPdf);
        if (!string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            _photoCache.Remove(_currentCacheKey);
            InvalidateNeighborInkCache(_currentCacheKey);
        }
        if (!string.IsNullOrWhiteSpace(currentCacheKey))
        {
            _photoCache.Remove(currentCacheKey);
            InvalidateNeighborInkCache(currentCacheKey);
        }

        MarkInkPageModified(sourcePath, pageIndex, "empty", Array.Empty<InkStrokeData>());
        ClearInkWalSnapshot(sourcePath, pageIndex);

        if (_inkSaveEnabled && _inkPersistence != null)
        {
            _ = SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    PersistInkHistorySnapshot(sourcePath, pageIndex, new List<InkStrokeData>(), _inkPersistence);
                    _inkExport?.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
                });
        }

        _neighborInkCache.Clear();
        _neighborInkRenderPending.Clear();
        _neighborInkSidecarLoadPending.Clear();
        ClearNeighborInkVisuals(clearSlotIdentity: true);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.InkStateChanged);
    }

    public MediaColor CurrentBrushColor => _brushColor;
    public byte CurrentBrushOpacity => _brushOpacity;
    public string CurrentDocumentName => _currentDocumentName;
    public string CurrentDocumentPath => _currentDocumentPath;
    public ClassroomToolkit.Application.UseCases.Photos.PhotoFileType CurrentPhotoFileType => !_photoModeActive
        ? ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Unknown
        : _photoDocumentIsPdf
            ? ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Pdf
            : ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationPlanner.ClassifyPath(_currentDocumentPath);
    public DateTime CurrentCourseDate => _currentCourseDate;
    public int CurrentPageIndex => _currentPageIndex;

    // 杈呭姪灞炴€э細绠€鍖栭噸澶嶇殑澶嶅悎鍒ゆ柇閫昏緫锛堟浛浠?30+ 澶勯噸澶嶄唬鐮侊級
    public void Undo()
    {
        if (_inkRecordEnabled && _photoModeActive && _globalInkHistory.Count > 0)
        {
            if (TryUndoAcrossPages())
            {
                return;
            }
        }
        if (_inkRecordEnabled && _inkHistory.Count > 0)
        {
            var snapshot = _inkHistory[^1];
            _inkHistory.RemoveAt(_inkHistory.Count - 1);
            _inkStrokes.Clear();
            _inkStrokes.AddRange(CloneInkStrokes(snapshot.Strokes));
            RedrawInkSurface();
            NotifyInkStateChanged(updateActiveSnapshot: true);
            return;
        }
        if (_history.Count == 0)
        {
            return;
        }
        var rasterSnapshot = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        RestoreSnapshot(rasterSnapshot);
    }

    private bool TryUndoAcrossPages()
    {
        if (_globalInkHistory.Count == 0)
        {
            return false;
        }

        var snapshot = _globalInkHistory[^1];
        _globalInkHistory.RemoveAt(_globalInkHistory.Count - 1);

        if (!TryApplyGlobalUndoSnapshot(snapshot))
        {
            return false;
        }
        return true;
    }

    private bool TryApplyGlobalUndoSnapshot(GlobalInkSnapshot snapshot)
    {
        if (!_photoModeActive || _currentCacheScope != InkCacheScope.Photo)
        {
            return false;
        }

        var snapshotStrokes = CloneInkStrokes(snapshot.Strokes);
        var snapshotHash = ComputeInkHash(snapshotStrokes);
        var cacheKey = snapshot.CacheKey ?? string.Empty;

        if (string.Equals(_currentCacheKey, snapshot.CacheKey, StringComparison.OrdinalIgnoreCase))
        {
            _inkStrokes.Clear();
            _inkStrokes.AddRange(snapshotStrokes);
            RedrawInkSurface();
            MarkInkPageModified(_currentDocumentPath, _currentPageIndex, snapshotHash, _inkStrokes);
            NotifyInkStateChanged(updateActiveSnapshot: true);
            return true;
        }

        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (_inkCacheEnabled)
        {
            if (snapshotStrokes.Count == 0)
            {
                _photoCache.Remove(cacheKey);
            }
            else
            {
                _photoCache.Set(cacheKey, snapshotStrokes);
            }
        }

        MarkInkPageModified(snapshot.SourcePath, snapshot.PageIndex, snapshotHash, snapshotStrokes);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.UndoSnapshot);
        return true;
    }

    public void SetBrushOpacity(byte opacity)
    {
        _brushOpacity = opacity;
    }

    public void SetCalligraphyOptions(bool inkBloomEnabled, bool sealEnabled)
    {
        _calligraphyInkBloomEnabled = inkBloomEnabled;
        _calligraphySealEnabled = sealEnabled;
    }

    public void SetCalligraphyOverlayOpacityThreshold(byte threshold)
    {
        _calligraphyOverlayOpacityThreshold = threshold;
    }

    public void SetClassroomWritingMode(ClassroomWritingMode mode)
    {
        _classroomWritingMode = mode;
        _stylusPressureAnalyzer.Reset();
        _stylusPressureCalibrator.Reset();
        _stylusDeviceAdaptiveProfiler.Reset();
        ApplyClassroomRuntimeProfile();
        EnsureActiveRenderer(force: true);
    }

    public void RestoreStylusAdaptiveState(
        int pressureProfile,
        int sampleRateTier,
        int predictionHorizonMs,
        double calibratedLow,
        double calibratedHigh)
    {
        StylusPressureDeviceProfile resolvedPressure = StylusPressureDeviceProfile.Unknown;
        StylusSampleRateTier resolvedRate = StylusSampleRateTier.Unknown;

        if (Enum.IsDefined(typeof(StylusPressureDeviceProfile), pressureProfile))
        {
            resolvedPressure = (StylusPressureDeviceProfile)pressureProfile;
        }
        if (Enum.IsDefined(typeof(StylusSampleRateTier), sampleRateTier))
        {
            resolvedRate = (StylusSampleRateTier)sampleRateTier;
        }

        _stylusDeviceAdaptiveProfiler.Seed(resolvedPressure, resolvedRate, predictionHorizonMs);
        if (calibratedHigh - calibratedLow >= StylusRuntimeDefaults.CalibratedRangeSeedMinWidth)
        {
            _stylusPressureCalibrator.SeedRange(calibratedLow, calibratedHigh);
        }
        ApplyClassroomRuntimeProfile();
        EnsureActiveRenderer(force: true);
    }

    public bool TryGetStylusAdaptiveState(
        out int pressureProfile,
        out int sampleRateTier,
        out int predictionHorizonMs,
        out double calibratedLow,
        out double calibratedHigh)
    {
        var profile = _stylusDeviceAdaptiveProfiler.CurrentProfile;
        pressureProfile = (int)profile.PressureProfile;
        sampleRateTier = (int)profile.SampleRateTier;
        predictionHorizonMs = profile.PredictionHorizonMs;

        if (_stylusPressureCalibrator.TryExportRange(out calibratedLow, out calibratedHigh))
        {
            return true;
        }

        calibratedLow = StylusRuntimeDefaults.CalibratedLowDefault;
        calibratedHigh = StylusRuntimeDefaults.CalibratedHighDefault;
        return pressureProfile != 0 || sampleRateTier != 0;
    }

    public void SetBrushTuning(WhiteboardBrushPreset whiteboardPreset, CalligraphyBrushPreset calligraphyPreset)
    {
        _whiteboardPreset = whiteboardPreset;
        _calligraphyPreset = calligraphyPreset;
        EnsureActiveRenderer(force: true);
    }

    public void SetBrushStyle(PaintBrushStyle style)
    {
        _brushStyle = style;
        EnsureActiveRenderer(force: true);
        
        // Refresh mode to apply correct input handling
        SetMode(_mode);
    }

    private void ApplyClassroomRuntimeProfile()
    {
        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(_classroomWritingMode);
        _stylusPseudoPressureLowThreshold = runtime.PseudoPressureLowThreshold;
        _stylusPseudoPressureHighThreshold = runtime.PseudoPressureHighThreshold;
        _calligraphyPreviewMinDistance = runtime.CalligraphyPreviewMinDistance;
        _brushPredictionHorizonMs = _stylusDeviceAdaptiveProfiler.CurrentProfile.PredictionHorizonMs;
    }

    private MarkerBrushConfig BuildMarkerConfig()
    {
        var config = _whiteboardPreset switch
        {
            WhiteboardBrushPreset.Sharp => MarkerBrushConfig.Sharp,
            WhiteboardBrushPreset.Balanced => MarkerBrushConfig.Balanced,
            _ => MarkerBrushConfig.Smooth
        };

        ClassroomWritingModeTuner.ApplyToMarkerConfig(config, _classroomWritingMode);
        StylusDeviceAdaptiveProfiler.ApplyToMarkerConfig(config, _stylusDeviceAdaptiveProfiler.CurrentProfile);
        return config;
    }

    private BrushPhysicsConfig BuildCalligraphyConfig()
    {
        _calligraphyRenderMode = ResolveCalligraphyRenderMode(_calligraphyPreset);
        var config = _calligraphyPreset switch
        {
            CalligraphyBrushPreset.Sharp => BrushPhysicsConfig.CreateCalligraphySharp(),
            CalligraphyBrushPreset.Soft => BrushPhysicsConfig.CreateCalligraphyInkFeel(),
            _ => BrushPhysicsConfig.CreateCalligraphyClarity()
        };

        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(config, _classroomWritingMode);
        StylusDeviceAdaptiveProfiler.ApplyToCalligraphyConfig(config, _stylusDeviceAdaptiveProfiler.CurrentProfile);
        return config;
    }

    private static CalligraphyRenderMode ResolveCalligraphyRenderMode(CalligraphyBrushPreset preset)
    {
        return preset == CalligraphyBrushPreset.Soft
            ? CalligraphyRenderMode.Ink
            : CalligraphyRenderMode.Clarity;
    }

    private void EnsureActiveRenderer(bool force = false)
    {
        ApplyClassroomRuntimeProfile();
        if (!force && _activeRenderer != null && _inkRendererFactory.CanReuse(_brushStyle, _activeRenderer))
        {
            return;
        }

        var markerConfig = BuildMarkerConfig();
        var calligraphyConfig = BuildCalligraphyConfig();
        _activeRenderer = _inkRendererFactory.Create(_brushStyle, markerConfig, calligraphyConfig);
    }

    private MediaColor EffectiveBrushColor()
    {
        return MediaColor.FromArgb(_brushOpacity, _brushColor.R, _brushColor.G, _brushColor.B);
    }







    private void UpdateInputPassthrough()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var enable = OverlayInputPassthroughPolicy.ShouldEnable(
            _mode,
            _boardOpacity,
            _photoModeActive);
        _inputPassthroughEnabled = enable;
        ApplyWindowStyles();
        UpdateFocusAcceptance();
    }

    private void UpdateOverlayHitTestVisibility()
    {
        if (OverlayRoot == null)
        {
            return;
        }

        OverlayRoot.IsHitTestVisible = OverlayHitTestPolicy.ShouldEnableOverlayHitTest(
            _mode,
            _photoModeActive,
            _photoLoading);
    }

    private void UpdateFocusAcceptance()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var blockFocus = ShouldBlockFocus();
        if (_focusBlocked == blockFocus)
        {
            return;
        }
        _focusBlocked = blockFocus;
        ApplyWindowStyles();
    }

    private bool ShouldBlockFocus()
    {
        var navigationMode = _sessionCoordinator.CurrentState.NavigationMode;
        var presentationAllowed = PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
            _presentationOptions.AllowOffice,
            _presentationOptions.AllowWps);
        var allowResolver = OverlayFocusResolverGatePolicy.ShouldResolvePresentationTarget(
            presentationAllowed,
            UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode));
        var (presentationTargetValid, wpsRawTargetValid) = ResolvePresentationFocusTargets(allowResolver);

        return OverlayFocusAcceptancePolicy.ShouldBlockFocus(
            navigationMode,
            _inputPassthroughEnabled,
            _mode,
            _photoModeActive,
            IsBoardActive(),
            presentationAllowed,
            presentationTargetValid,
            wpsRawTargetValid);
    }

    private (bool PresentationTargetValid, bool WpsRawTargetValid) ResolvePresentationFocusTargets(bool allowResolver)
    {
        if (!allowResolver)
        {
            return (false, false);
        }

        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        var presentationTargetValid = target.IsValid;
        if (!WpsRawFallbackTargetPolicy.ShouldResolveWpsRawTarget(
                presentationTargetValid,
                _presentationOptions.AllowWps))
        {
            return (presentationTargetValid, false);
        }

        var wpsTarget = ResolveWpsTarget();
        var wpsRawTargetValid = WpsRawFallbackTargetPolicy.IsValid(
            wpsTarget.IsValid,
            ResolveWpsSendMode(wpsTarget));
        return (presentationTargetValid, wpsRawTargetValid);
    }

    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        if (!OverlayWindowStyleApplyPolicy.ShouldApply(
                _inputPassthroughEnabled,
                _focusBlocked,
                _lastAppliedInputPassthroughEnabled,
                _lastAppliedFocusBlocked))
        {
            return;
        }
        var styleMask = OverlayWindowStyleBitsPolicy.Resolve(_inputPassthroughEnabled, _focusBlocked);

        var updated = WindowStyleExecutor.TryUpdateStyleBits(
            _hwnd,
            NativeMethods.GwlExstyle,
            styleMask.SetMask,
            styleMask.ClearMask,
            out _);
        if (updated)
        {
            _lastAppliedInputPassthroughEnabled = _inputPassthroughEnabled;
            _lastAppliedFocusBlocked = _focusBlocked;
        }
    }


    public void UpdateInkCacheEnabled(bool enabled)
    {
        var transitionPlan = InkCacheUpdateTransitionPolicy.Resolve(
            enabled,
            _inkMonitor.IsEnabled);
        _inkCacheEnabled = enabled;
        if (transitionPlan.ShouldStartMonitor)
        {
            _inkMonitor.Start();
        }
        if (transitionPlan.ShouldClearCache)
        {
            _photoCache.Clear();
        }
        if (transitionPlan.ShouldRequestRefresh)
        {
            _refreshOrchestrator.RequestRefresh("ink-cache");
        }
    }

    public void UpdateInkSaveEnabled(bool enabled)
    {
        var transitionPlan = InkSaveUpdateTransitionPolicy.Resolve(enabled);
        _inkSaveEnabled = enabled;
        if (transitionPlan.ShouldStopAutoSaveTimer)
        {
            _inkSidecarAutoSaveTimer?.Stop();
            if (transitionPlan.ShouldCancelPendingAutoSave)
            {
                _inkSidecarAutoSaveGate.NextGeneration();
            }
            return;
        }
        if (transitionPlan.ShouldScheduleAutoSave)
        {
            ScheduleSidecarAutoSave();
        }
    }

    public void UpdateInkShowEnabled(bool enabled)
    {
        InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: _inkShowEnabled,
            requestedEnabled: enabled,
            photoModeActive: _photoModeActive,
            setInkShowEnabled: nextEnabled => _inkShowEnabled = nextEnabled,
            purgePersistedInkForHiddenCurrentDocument: PurgePersistedInkForHiddenCurrentDocumentIfNeeded,
            clearInkSurfaceState: ClearInkSurfaceState,
            clearNeighborInkVisuals: () => ClearNeighborInkVisuals(clearSlotIdentity: true),
            clearNeighborInkCache: () => _neighborInkCache.Clear(),
            clearNeighborInkRenderPending: () => _neighborInkRenderPending.Clear(),
            clearNeighborInkSidecarLoadPending: () => _neighborInkSidecarLoadPending.Clear(),
            loadCurrentPageIfExists: () => LoadCurrentPageIfExists(),
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate);
    }

    public void UpdateInkRecordEnabled(bool enabled)
    {
        _inkRecordEnabled = enabled;
    }

    public void UpdateInkReplayPreviousEnabled(bool enabled)
    {
        _inkReplayPreviousEnabled = enabled;
    }

    public void UpdateInkRetentionDays(int days)
    {
        _inkRetentionDays = Math.Max(0, days);
        if (_inkRetentionDays > 0 && _inkRecordEnabled)
        {
            var lifecycleToken = _overlayLifecycleCancellation.Token;
            _ = SafeTaskRunner.Run(
                "PaintOverlayWindow.UpdateInkRetentionDays",
                _ => _inkStorage.CleanupOldRecords(_inkRetentionDays),
                lifecycleToken,
                onError: ex => System.Diagnostics.Debug.WriteLine(
                    $"[InkStorage] retention-cleanup failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    public void UpdateInkPhotoRootPath(string path)
    {
        _inkPhotoRootPath = path ?? string.Empty;
        _inkStorage = new InkStorageService(photoRootPath: _inkPhotoRootPath);
    }

    public void UpdatePhotoTransformMemoryEnabled(bool enabled)
    {
        _rememberPhotoTransform = enabled;
        if (PhotoTransformMemoryTogglePolicy.ShouldResetUserDirtyState(_rememberPhotoTransform))
        {
            _photoUserTransformDirty = false;
        }
        if (PhotoTransformMemoryTogglePolicy.ShouldResetUnifiedTransformState(_rememberPhotoTransform))
        {
            _photoUnifiedTransformReady = false;
        }
    }

    public void LoadInkPage(int pageIndex)
    {
        // Ink history view is removed; keep for compatibility.
    }

    public void UpdateCrossPageDisplayEnabled(bool enabled)
    {
        CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            requestedEnabled: enabled,
            photoInkModeActive: IsPhotoInkModeActive(),
            photoDocumentIsPdf: _photoDocumentIsPdf,
            photoUnifiedTransformReady: _photoUnifiedTransformReady,
            setCrossPageDisplayEnabled: nextEnabled => _crossPageDisplayEnabled = nextEnabled,
            resetCrossPageNormalizedWidth: ResetCrossPageNormalizedWidth,
            restoreUnifiedTransformAndRedraw: RestoreUnifiedPhotoTransformAndRequestRedraw,
            saveUnifiedTransformState: () => SavePhotoTransformState(userAdjusted: _photoUserTransformDirty),
            updateCurrentPageWidthNormalization: () => UpdateCurrentPageWidthNormalization(),
            resetCrossPageReplayState: ResetCrossPageReplayState,
            clearNeighborPages: ClearNeighborPages,
            refreshCurrentImageSequenceSourceAfterToggle: RefreshCurrentImageSequenceSourceAfterCrossPageToggle,
            reloadPdfInkCacheAfterToggle: ReloadPdfInkCacheAfterCrossPageToggle);
    }

    private void RestoreUnifiedPhotoTransformAndRequestRedraw()
    {
        ApplyLastUnifiedPhotoTransform(markUserDirty: true);
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        RequestInkRedraw();
        UpdateCurrentPageWidthNormalization();
    }

    private void ApplyLastUnifiedPhotoTransform(bool markUserDirty)
    {
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = _lastPhotoScaleX;
        _photoScale.ScaleY = _lastPhotoScaleY;
        _photoTranslate.X = _lastPhotoTranslateX;
        _photoTranslate.Y = _lastPhotoTranslateY;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        if (markUserDirty)
        {
            _photoUserTransformDirty = true;
        }
    }

    private void RefreshCurrentImageSequenceSourceAfterCrossPageToggle()
    {
        // Reset image cache to avoid mixing different decode policies
        // between cross-page and single-page rendering.
        ClearNeighborImageCache();
        var currentPage = GetCurrentPageIndexForCrossPage();
        var bitmap = GetPageBitmap(currentPage);
        if (bitmap == null)
        {
            return;
        }

        PhotoBackground.Source = bitmap;
        RefreshPhotoBackgroundVisibility();
        UpdateCurrentPageWidthNormalization(bitmap);
    }

    private void ReloadPdfInkCacheAfterCrossPageToggle()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
        _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, isPdf: true);
        ResetInkHistory();
        LoadCurrentPageIfExists();
    }

    private void ApplyLoadedBitmapTransform(BitmapSource bitmap, bool useCrossPageUnifiedPath)
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath,
            _rememberPhotoTransform,
            _photoUnifiedTransformReady);
        switch (path)
        {
            case PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform:
                ApplyLastUnifiedPhotoTransform(markUserDirty: false);
                return;
            case PhotoLoadedBitmapTransformPath.FitToViewport:
                ApplyPhotoFitToViewport(bitmap);
                return;
            case PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit:
                var appliedStored = TryApplyStoredPhotoTransform(GetCurrentPhotoTransformKey());
                if (!appliedStored)
                {
                    ApplyPhotoFitToViewport(bitmap);
                }
                return;
            default:
                ApplyPhotoFitToViewport(bitmap);
                return;
        }
    }

    public void UpdateNeighborPrefetchRadiusMax(int maxRadius)
    {
        _neighborPrefetchRadiusMaxSetting = Math.Clamp(maxRadius, CrossPageNeighborPrefetchRadiusMin, CrossPageNeighborPrefetchRadiusMax);
    }

    public void SetPhotoUnifiedTransformState(bool enabled, double scaleX, double scaleY, double translateX, double translateY)
    {
        _photoUnifiedTransformReady = enabled;
        if (!enabled)
        {
            return;
        }
        _lastPhotoScaleX = scaleX;
        _lastPhotoScaleY = scaleY;
        _lastPhotoTranslateX = translateX;
        _lastPhotoTranslateY = translateY;
        _photoUserTransformDirty = true;
        if (PhotoUnifiedTransformApplyPolicy.ShouldApplyRuntimeTransform(
                IsPhotoInkModeActive(),
                IsCrossPageDisplayActive()))
        {
            ApplyLastUnifiedPhotoTransform(markUserDirty: true);
            UpdateCurrentPageWidthNormalization();
            RequestInkRedraw();
        }
    }

    public bool TryGetPhotoUnifiedTransformState(out double scaleX, out double scaleY, out double translateX, out double translateY)
    {
        if (!_photoUnifiedTransformReady)
        {
            scaleX = PhotoTransformViewportDefaults.DefaultScale;
            scaleY = PhotoTransformViewportDefaults.DefaultScale;
            translateX = PhotoUnifiedTransformDefaults.DefaultTranslateDip;
            translateY = PhotoUnifiedTransformDefaults.DefaultTranslateDip;
            return false;
        }
        scaleX = _lastPhotoScaleX;
        scaleY = _lastPhotoScaleY;
        translateX = _lastPhotoTranslateX;
        translateY = _lastPhotoTranslateY;
        return true;
    }



    private bool TryBeginInvoke(Action action, DispatcherPriority priority)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return false;
        }
        return PaintActionInvoker.TryInvoke(() =>
        {
            Dispatcher.BeginInvoke(action, priority);
            return true;
        }, fallback: false);
    }

    private void RecoverOverlayFullscreenBounds()
    {
        OverlayFullscreenBoundsRecoveryExecutor.Apply(
            shouldRecover: true,
            normalizeWindowState: NormalizeOverlayWindowState,
            applyImmediateBounds: ApplyFullscreenBounds,
            applyDeferredBounds: RequestDeferredFullscreenBoundsRecovery);
    }

    private void NormalizeOverlayWindowState(bool shouldNormalize)
    {
        WindowStateNormalizationExecutor.Apply(this, shouldNormalize);
    }

    private void RequestDeferredFullscreenBoundsRecovery()
    {
        var scheduled = TryBeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyFullscreenBounds();
        }
    }

}
