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
using ClassroomToolkit.Interop;
using ClassroomToolkit.Interop.Presentation;
using ClassroomToolkit.Services.Presentation;
using MonitorInfo = ClassroomToolkit.Interop.NativeMethods.MonitorInfo;
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
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{
    /// <summary>透明但可点击测试的颜色（Alpha=1）</summary>
    private static readonly MediaColor TransparentHitTestColor = MediaColor.FromArgb(1, 255, 255, 255);
    
    // Win32 窗口样式常量
    private const int GwlStyle = -16;
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;

    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    
    /// <summary>演示文稿焦点监控间隔（ms） - 平衡响应速度和 CPU 占用</summary>
    private const int PresentationFocusMonitorIntervalMs = 500;
    
    /// <summary>演示文稿焦点恢复冷却时间（ms） - 防止频繁切换导致闪烁</summary>
    private const int PresentationFocusCooldownMs = 1200;
    

    
    /// <summary>跨页更新最小间隔（ms） - 连续滚动时的渲染节流</summary>
    private const int CrossPageUpdateMinIntervalMs = 24;
    
    /// <summary>照片滚轮缩放基数 - 每次滚轮刻度的缩放系数（接近 1.0 使缩放平滑）</summary>
    private const double PhotoWheelZoomBase = 1.0008;
    
    /// <summary>照片按键缩放步进 - 使用 +/- 键时的缩放幅度</summary>
    private const double PhotoKeyZoomStep = 1.06;

    private IntPtr _hwnd;
    private bool _inputPassthroughEnabled;
    private bool _focusBlocked;
    private bool _forcePresentationForegroundOnFullscreen;
    private readonly DispatcherTimer _presentationFocusMonitor;
    private DateTime _nextPresentationFocusAttempt = DateTime.MinValue;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private const int HistoryLimit = 30;


    private PaintToolMode _mode = PaintToolMode.Brush;
    private PaintShapeType _shapeType = PaintShapeType.Line;
    private MediaColor _boardColor = Colors.Transparent;
    private byte _boardOpacity;
    private bool _isDrawingShape;
    private WpfPoint _shapeStart;
    private Shape? _activeShape;
    private bool _isRegionSelecting;
    private WpfPoint _regionStart;
    private WpfRectangle? _regionRect;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlService _presentationService;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlOptions _presentationOptions;
    private readonly ClassroomToolkit.Interop.Presentation.PresentationClassifier _presentationClassifier;
    private readonly ClassroomToolkit.Interop.Presentation.Win32PresentationResolver _presentationResolver;
    private readonly ClassroomToolkit.Interop.Presentation.WpsSlideshowNavigationHook? _wpsNavHook;
    private const int WpsNavDebounceMs = 200;
    private bool _wpsNavHookActive;
    private bool _wpsHookInterceptKeyboard = true;
    private bool _wpsHookInterceptWheel = true;
    private bool _wpsForceMessageFallback;
    private bool _wpsHookUnavailableNotified;
    private DateTime _wpsNavBlockUntil = DateTime.MinValue;
    private (int Code, IntPtr Target, DateTime Timestamp)? _lastWpsNavEvent;
    private DateTime _lastWpsHookInput = DateTime.MinValue;
    private readonly List<RasterSnapshot> _history = new();


    private WpfPoint? _lastPointerPosition;
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
    private ClassroomToolkit.Interop.Presentation.PresentationType _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;

    private const double PdfDefaultDpi = 96;
    private const int PdfCacheLimit = 6;
    private bool _photoModeActive;
    private bool _photoFullscreen;
    private bool _photoLoading;
    private bool _photoCrossPageDisplayEnabled;
    private bool _crossPageUpdatePending;
    private DateTime _lastCrossPageUpdateUtc = DateTime.MinValue;
    private int _crossPageUpdateToken;
    private ScaleTransform _photoScale = new ScaleTransform(1.0, 1.0);
    private TranslateTransform _photoTranslate = new TranslateTransform(0, 0);
    private bool _photoPanning;
    private bool _photoRightClickPending;
    private WpfPoint _photoRightClickStart;
    private WpfPoint _photoPanStart;
    private double _photoPanOriginX;
    private double _photoPanOriginY;
    private bool _photoRestoreFullscreenPending;
    private bool _photoDocumentIsPdf;
    private PdfDocumentHost? _pdfDocument;
    private int _pdfPageCount;
    private int _lastPdfNavigationDirection;
    private readonly Dictionary<int, BitmapSource> _pdfPageCache = new();
    private readonly LinkedList<int> _pdfPageOrder = new();
    private readonly object _pdfRenderLock = new();
    private const int NeighborPageCacheLimit = 5;
    private int _pdfPrefetchInFlight;
    private int _pdfPrefetchToken;
    private readonly HashSet<int> _pdfPinnedPages = new();
    private int _pdfVisiblePrefetchInFlight;
    private int _pdfVisiblePrefetchToken;
    private int _photoLoadToken;
    private readonly HashSet<string> _neighborInkRenderPending = new(StringComparer.OrdinalIgnoreCase);
    private string _photoSessionPath = string.Empty;
    private bool _photoSessionIsPdf;
    private int _photoSessionPageIndex = 1;
    private bool _photoSessionHasTransform;
    private double _photoSessionScaleX = 1.0;
    private double _photoSessionScaleY = 1.0;
    private double _photoSessionTranslateX;
    private double _photoSessionTranslateY;
    private bool _rememberPhotoTransform;
    private bool _photoUserTransformDirty;
    private double _lastPhotoScaleX = 1.0;
    private double _lastPhotoScaleY = 1.0;
    private double _lastPhotoTranslateX;
    private double _lastPhotoTranslateY;
    private readonly Dictionary<string, PhotoTransformState> _photoPageTransforms = new(StringComparer.OrdinalIgnoreCase);
    private bool _photoUnifiedTransformReady;
    private DispatcherTimer? _photoUnifiedTransformSaveTimer;
    private DispatcherTimer? _photoTransformSaveTimer;
    private bool _photoTransformSavePending;
    private bool _photoTransformSaveUserAdjusted;
    private bool _foregroundPresentationActive;
    private ClassroomToolkit.Interop.Presentation.PresentationType _foregroundPresentationType;
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
    private List<string> _photoSequencePaths = new();
    private int _photoSequenceIndex = -1;
    private readonly Dictionary<int, BitmapSource> _neighborImageCache = new();
    private readonly HashSet<int> _neighborImagePrefetchPending = new();
    private System.Windows.Controls.Canvas? _neighborPagesCanvas;
    private readonly List<WpfImage> _neighborPageImages = new();
    private readonly List<WpfImage> _neighborInkImages = new();
    private readonly Dictionary<string, InkBitmapCacheEntry> _neighborInkCache = new(StringComparer.OrdinalIgnoreCase);


    public event Action<string, DateTime>? InkContextChanged;
    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action? FloatingZOrderRequested;
    public event Action? PresentationFullscreenDetected;
    public event Action<ClassroomToolkit.Interop.Presentation.PresentationType>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;





    public PaintOverlayWindow()
    {
        InitializeComponent();
        _neighborPagesCanvas = FindName("NeighborPagesCanvas") as System.Windows.Controls.Canvas;
        _visualHost = new DrawingVisualHost();
        CustomDrawHost.Child = _visualHost;
        var photoTransform = new TransformGroup();
        photoTransform.Children.Add(_photoScale);
        photoTransform.Children.Add(_photoTranslate);
        PhotoBackground.RenderTransform = photoTransform;
        
        WindowState = WindowState.Maximized;
        _refreshOrchestrator = new RefreshOrchestrator(Dispatcher, MonitorInkContext);
        _presentationFocusMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PresentationFocusMonitorIntervalMs)
        };
        _presentationFocusMonitor.Tick += (_, _) => MonitorPresentationFocus();
        _inkMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InkMonitorActiveIntervalMs)
        };
        _inkMonitor.Tick += (_, _) => _refreshOrchestrator.RequestRefresh("poll");
        _perfMonitor = new PerfStats("MonitorInkContext");
        _perfSavePage = new PerfStats("SavePage");
        _perfLoadPage = new PerfStats("LoadPage");
        _perfRedrawSurface = new PerfStats("RedrawSurface");
        _perfEnsureSurface = new PerfStats("EnsureSurface");
        _perfClearSurface = new PerfStats("ClearSurface");
        _perfApplyStrokes = new PerfStats("ApplyStrokes");
        
        KeyDown += OnKeyDown;
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
            }
        };
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            UpdateInputPassthrough();
            UpdateFocusAcceptance();
        };
        OverlayRoot.MouseLeftButtonDown += OnMouseDown;
        OverlayRoot.MouseMove += OnMouseMove;
        OverlayRoot.MouseLeftButtonUp += OnMouseUp;
        OverlayRoot.MouseRightButtonDown += OnRightButtonDown;
        OverlayRoot.MouseRightButtonUp += OnRightButtonUp;
        OverlayRoot.MouseMove += OnRightButtonMove;
        OverlayRoot.LostMouseCapture += OnOverlayLostMouseCapture;
        OverlayRoot.IsManipulationEnabled = true;
        OverlayRoot.ManipulationStarting += OnManipulationStarting;
        OverlayRoot.ManipulationDelta += OnManipulationDelta;
        OverlayRoot.StylusDown += OnStylusDown;
        OverlayRoot.StylusMove += OnStylusMove;
        OverlayRoot.StylusUp += OnStylusUp;
        MouseWheel += OnMouseWheel;
        Loaded += (_, _) => EnsureRasterSurface();
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        UpdateBoardBackground();

        _presentationClassifier = new ClassroomToolkit.Interop.Presentation.PresentationClassifier();
        var planner = new ClassroomToolkit.Services.Presentation.PresentationControlPlanner(_presentationClassifier);
        var mapper = new ClassroomToolkit.Services.Presentation.PresentationCommandMapper();
        var sender = new ClassroomToolkit.Interop.Presentation.Win32InputSender();
        _presentationResolver = new ClassroomToolkit.Interop.Presentation.Win32PresentationResolver();
        _presentationService = new ClassroomToolkit.Services.Presentation.PresentationControlService(planner, mapper, sender, _presentationResolver, new ClassroomToolkit.Services.Presentation.Win32PresentationWindowValidator());
        _presentationOptions = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Auto,
            WheelAsKey = false,
            AllowOffice = true,
            AllowWps = true
        };
        _wpsNavHook = new ClassroomToolkit.Interop.Presentation.WpsSlideshowNavigationHook();
        if (_wpsNavHook.Available)
        {
            _wpsNavHook.NavigationRequested += OnWpsNavHookRequested;
        }
        Closed += (_, _) =>
        {
            SaveCurrentPageIfNeeded();
            StopWpsNavHook();
            _presentationFocusMonitor.Stop();
            _inkMonitor.Stop();
        };
        IsVisibleChanged += (_, _) =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            UpdatePresentationFocusMonitor();
        };
    }

    public void SetMode(PaintToolMode mode)
    {
        _mode = mode;
        OverlayRoot.IsHitTestVisible = mode != PaintToolMode.Cursor || _photoModeActive;
        if (_photoModeActive && mode == PaintToolMode.Cursor)
        {
            Focus();
            Keyboard.Focus(this);
        }
        
        // 更新全局绘图模式状态
        var isPaintMode = mode != PaintToolMode.Cursor;
        PaintModeManager.Instance.IsPaintMode = isPaintMode;
        
        // 立即设置光标（光标模式使用系统光标，无需创建文件）
        if (mode == PaintToolMode.Cursor)
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        else
        {
            // 其他模式的光标更新延迟执行，避免阻塞
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateCursor(mode);
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
        
        if (mode != PaintToolMode.RegionErase)
        {
            ClearRegionSelection();
        }
        if (mode != PaintToolMode.Shape)
        {
            ClearShapePreview();
        }
        
        // 立即更新输入穿透状态（轻量级操作）
        UpdateInputPassthrough();
        
        // 延迟更新钩子和焦点状态，避免卡顿
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            
            // 光标模式下恢复焦点
            if (mode == PaintToolMode.Cursor)
            {
                RestorePresentationFocusIfNeeded(requireFullscreen: false);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void UpdateCursor(PaintToolMode mode)
    {
        System.Windows.Input.Cursor cursor = mode switch
        {
            PaintToolMode.Cursor => System.Windows.Input.Cursors.Arrow,
            PaintToolMode.Brush => Utilities.CustomCursors.GetBrushCursor(_brushColor),  // 带颜色的画笔样式
            PaintToolMode.Eraser => Utilities.CustomCursors.Eraser,  // 橡皮擦样式
            PaintToolMode.Shape => System.Windows.Input.Cursors.Cross,     // 十字准星，精确绘制
            PaintToolMode.RegionErase => Utilities.CustomCursors.RegionErase, // 框选样式
            _ => System.Windows.Input.Cursors.Arrow
        };

        this.Cursor = cursor;
    }

    public void SetBrush(MediaColor color, double size, byte opacity)
    {
        _brushColor = color;
        _brushSize = Math.Max(1.0, size);
        _brushOpacity = opacity;

        // 如果当前是画笔模式，更新光标以显示新颜色
        if (_mode == PaintToolMode.Brush)
        {
            UpdateCursor(PaintToolMode.Brush);
        }
    }

    public void SetEraserSize(double size)
    {
        _eraserSize = Math.Max(4.0, size);
    }

    public void SetShapeType(PaintShapeType type)
    {
        _shapeType = type == PaintShapeType.RectangleFill ? PaintShapeType.Rectangle : type;
    }

    public void SetBoardColor(MediaColor color)
    {
        var wasActive = IsBoardActive();
        _boardColor = color;
        UpdateBoardBackground();
        var isActive = IsBoardActive();
        HandleBoardStateChange(wasActive, isActive);
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
            MarkInkCacheDirty();
        }
    }

    public MediaColor CurrentBrushColor => _brushColor;
    public byte CurrentBrushOpacity => _brushOpacity;
    public string CurrentDocumentName => _currentDocumentName;
    public DateTime CurrentCourseDate => _currentCourseDate;
    public int CurrentPageIndex => _currentPageIndex;

    // 辅助属性：简化重复的复合判断逻辑（替代 30+ 处重复代码）
    private bool IsPhotoDocumentPdf => _photoModeActive && _photoDocumentIsPdf;
    private bool IsPhotoFullscreen => _photoModeActive && _photoFullscreen;

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerDown(position);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed
                && e.RightButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                EndPhotoPan();
                e.Handled = true;
                return;
            }
            UpdatePhotoPan(e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerMove(position);
        e.Handled = true;
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
            e.Handled = true;
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerUp(position);
        e.Handled = true;
    }

    private void OnRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoModeActive && _photoFullscreen && _mode == PaintToolMode.Cursor)
        {
            _photoRightClickPending = true;
            _photoRightClickStart = e.GetPosition(OverlayRoot);
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
    }

    private void OnRightButtonMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoRightClickPending)
        {
            var point = e.GetPosition(OverlayRoot);
            var delta = point - _photoRightClickStart;
            if (delta.Length > 6)
            {
                _photoRightClickPending = false;
            }
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            if (e.RightButton != System.Windows.Input.MouseButtonState.Pressed
                && e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                EndPhotoPan();
                e.Handled = true;
                return;
            }
            UpdatePhotoPan(e.GetPosition(OverlayRoot));
            e.Handled = true;
        }
    }

    private void OnOverlayLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
        }
        _photoRightClickPending = false;
    }

    private void OnRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoRightClickPending && _photoModeActive && _photoFullscreen && _mode == PaintToolMode.Cursor)
        {
            _photoRightClickPending = false;
            ShowPhotoContextMenu(e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
            e.Handled = true;
        }
    }

    private void HandlePointerDown(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        // 设置正在绘图状态
        PaintModeManager.Instance.IsDrawing = true;
        
        if (_mode == PaintToolMode.RegionErase)
        {
            BeginRegionSelection(position);
            OverlayRoot.CaptureMouse();
            return;
        }
        if (_mode == PaintToolMode.Eraser)
        {
            BeginEraser(position);
            OverlayRoot.CaptureMouse();
            return;
        }
        if (_mode == PaintToolMode.Shape)
        {
            BeginShape(position);
            OverlayRoot.CaptureMouse();
            return;
        }
        if (_mode == PaintToolMode.Brush)
        {
            BeginBrushStroke(position);
            OverlayRoot.CaptureMouse();
        }
    }

    private void HandlePointerMove(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        if (_mode == PaintToolMode.Brush)
        {
            UpdateBrushStroke(position);
            return;
        }
        if (_mode == PaintToolMode.Eraser)
        {
            UpdateEraser(position);
            return;
        }
        if (_mode == PaintToolMode.RegionErase)
        {
            UpdateRegionSelection(position);
            return;
        }
        if (_mode == PaintToolMode.Shape)
        {
            UpdateShapePreview(position);
        }
    }

    private void HandlePointerUp(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        if (_mode == PaintToolMode.Brush)
        {
            EndBrushStroke(position);
        }
        else if (_mode == PaintToolMode.Eraser)
        {
            EndEraser(position);
        }
        else if (_mode == PaintToolMode.RegionErase)
        {
            EndRegionSelection(position);
        }
        else if (_mode == PaintToolMode.Shape)
        {
            EndShape(position);
        }
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (_pendingInkContextCheck)
        {
            _pendingInkContextCheck = false;
            _refreshOrchestrator.RequestRefresh("pointer-up");
        }
    }

    public bool TryExitPresentationFullscreen()
    {
        if (_photoModeActive)
        {
            return false;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return false;
        }
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        if (!target.IsValid || !IsFullscreenPresentationWindow(target))
        {
            return false;
        }
        ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.EnsureForeground(target.Handle);
        var sender = new ClassroomToolkit.Interop.Presentation.Win32InputSender();
        return sender.SendKey(
            target.Handle,
            ClassroomToolkit.Interop.Presentation.VirtualKey.Escape,
            ClassroomToolkit.Interop.Presentation.KeyModifiers.None,
            ClassroomToolkit.Interop.Presentation.InputStrategy.Message,
            keyDownOnly: false);
    }

    private bool IsWithinPhotoControls(DependencyObject? source)
    {
        if (source == null)
        {
            return false;
        }
        return IsDescendantOf(source, PhotoTitleBar) ||
               IsDescendantOf(source, PhotoCloseButton) ||
               IsDescendantOf(source, PhotoMinimizeLeftButton) ||
               IsDescendantOf(source, PhotoMinimizeRightButton) ||
               IsDescendantOf(source, PhotoPrevButtonLeft) ||
               IsDescendantOf(source, PhotoNextButtonLeft) ||
               IsDescendantOf(source, PhotoPrevButtonRight) ||
               IsDescendantOf(source, PhotoNextButtonRight);
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject? ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor))
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private static bool IsPhotoNavigationKey(Key key, out int direction)
    {
        direction = 0;
        if (key == Key.Right || key == Key.Down || key == Key.PageDown || key == Key.Space || key == Key.Enter)
        {
            direction = 1;
            return true;
        }
        if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            direction = -1;
            return true;
        }
        return false;
    }

    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            if (IsBoardActive())
            {
                e.Handled = true;
            }
            return;
        }
        e.ManipulationContainer = OverlayRoot;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            if (IsBoardActive())
            {
                e.Handled = true;
            }
            return;
        }
        EnsurePhotoTransformsWritable();
        var scale = e.DeltaManipulation.Scale;
        if (Math.Abs(scale.X - 1.0) > 0.001 || Math.Abs(scale.Y - 1.0) > 0.001)
        {
            var factor = (scale.X + scale.Y) / 2.0;
            ApplyPhotoScale(factor, e.ManipulationOrigin);
        }
        var translation = e.DeltaManipulation.Translation;
        if (Math.Abs(translation.X) > 0.01 || Math.Abs(translation.Y) > 0.01)
        {
            _photoTranslate.X += translation.X;
            _photoTranslate.Y += translation.Y;
            SchedulePhotoTransformSave(userAdjusted: true);
        }
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
        RequestInkRedraw();
        e.Handled = true;
    }

    private void ZoomPhoto(int delta, WpfPoint center)
    {
        double scaleFactor = Math.Pow(PhotoWheelZoomBase, delta);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
    }

    private void ZoomPhotoByFactor(double scaleFactor)
    {
        var center = new WpfPoint(OverlayRoot.ActualWidth / 2.0, OverlayRoot.ActualHeight / 2.0);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
    }

    private void ApplyPhotoScale(double scaleFactor, WpfPoint center)
    {
        EnsurePhotoTransformsWritable();
        double newScale = Math.Clamp(_photoScale.ScaleX * scaleFactor, 0.2, 4.0);
        if (Math.Abs(newScale - _photoScale.ScaleX) < 0.001)
        {
            return;
        }
        var before = ToPhotoSpace(center);
        _photoScale.ScaleX = newScale;
        _photoScale.ScaleY = newScale;
        _photoTranslate.X = center.X - before.X * newScale;
        _photoTranslate.Y = center.Y - before.Y * newScale;
        SchedulePhotoTransformSave(userAdjusted: true);
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
    }

    private WpfPoint ToPhotoSpace(WpfPoint point)
    {
        if (!_photoModeActive)
        {
            return point;
        }
        var inverse = GetPhotoInverseMatrix();
        return inverse.Transform(point);
    }

    private Geometry? ToPhotoGeometry(Geometry geometry)
    {
        if (!_photoModeActive || geometry == null)
        {
            return geometry;
        }
        var inverse = GetPhotoInverseMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }
        return flattened;
    }

    private Geometry? ToScreenGeometry(Geometry geometry)
    {
        if (!_photoModeActive || geometry == null)
        {
            return geometry;
        }
        var transform = GetPhotoMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(transform);
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }
        return clone;
    }

    private Matrix GetPhotoMatrix()
    {
        var matrix = Matrix.Identity;
        matrix.Scale(_photoScale.ScaleX, _photoScale.ScaleY);
        matrix.Translate(_photoTranslate.X, _photoTranslate.Y);
        return matrix;
    }

    private Matrix GetPhotoInverseMatrix()
    {
        var scaleX = _photoScale.ScaleX;
        var scaleY = _photoScale.ScaleY;
        if (Math.Abs(scaleX) < 0.0001 || Math.Abs(scaleY) < 0.0001)
        {
            return Matrix.Identity;
        }
        var matrix = Matrix.Identity;
        matrix.Scale(1.0 / scaleX, 1.0 / scaleY);
        matrix.Translate(-_photoTranslate.X / scaleX, -_photoTranslate.Y / scaleY);
        return matrix;
    }

    public void Undo()
    {
        if (_inkRecordEnabled && _inkHistory.Count > 0)
        {
            var snapshot = _inkHistory[^1];
            _inkHistory.RemoveAt(_inkHistory.Count - 1);
            _inkStrokes.Clear();
            _inkStrokes.AddRange(CloneInkStrokes(snapshot.Strokes));
            RedrawInkSurface();
            MarkInkCacheDirty();
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

    private void EnsureActiveRenderer(bool force = false)
    {
        if (_brushStyle == PaintBrushStyle.Calligraphy)
        {
            if (force || _activeRenderer is not VariableWidthBrushRenderer)
            {
                var config = _calligraphyPreset switch
                {
                    CalligraphyBrushPreset.Sharp => BrushPhysicsConfig.CreateCalligraphySharp(),
                    CalligraphyBrushPreset.Soft => BrushPhysicsConfig.CreateCalligraphySoft(),
                    _ => BrushPhysicsConfig.CreateCalligraphyBalanced()
                };
                _activeRenderer = new VariableWidthBrushRenderer(config);
            }
            return;
        }
        if (_brushStyle == PaintBrushStyle.StandardRibbon)
        {
            if (force || _activeRenderer is not MarkerBrushRenderer marker || marker.RenderMode != MarkerRenderMode.Ribbon)
            {
                var config = _whiteboardPreset switch
                {
                    WhiteboardBrushPreset.Sharp => MarkerBrushConfig.Sharp,
                    WhiteboardBrushPreset.Balanced => MarkerBrushConfig.Balanced,
                    _ => MarkerBrushConfig.Smooth
                };
                _activeRenderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
            }
            return;
        }
        if (force || _activeRenderer is not MarkerBrushRenderer)
        {
            var config = _whiteboardPreset switch
            {
                WhiteboardBrushPreset.Sharp => MarkerBrushConfig.Sharp,
                WhiteboardBrushPreset.Balanced => MarkerBrushConfig.Balanced,
                _ => MarkerBrushConfig.Smooth
            };
            _activeRenderer = new MarkerBrushRenderer(MarkerRenderMode.SegmentUnion, config);
        }
    }

    private MediaColor EffectiveBrushColor()
    {
        return MediaColor.FromArgb(_brushOpacity, _brushColor.R, _brushColor.G, _brushColor.B);
    }

    public void SetBoardOpacity(byte opacity)
    {
        var wasActive = IsBoardActive();
        _boardOpacity = opacity;
        UpdateBoardBackground();
        UpdateInputPassthrough();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        var isActive = IsBoardActive();
        HandleBoardStateChange(wasActive, isActive);
    }

    private void HandleBoardStateChange(bool wasActive, bool isActive)
    {
        if (isActive && !wasActive)
        {
            if (!_photoModeActive)
            {
                WindowState = WindowState.Normal;
                ApplyFullscreenBounds();
                Dispatcher.BeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
            }
            SaveCurrentPageOnNavigate(forceBackground: false);
            _presentationFullscreenActive = false;
            _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            _boardSuspendedPhotoCache = _photoModeActive;
            if (_photoModeActive && _crossPageDisplayEnabled)
            {
                ClearNeighborPages();
            }
            ClearInkSurfaceState();
            return;
        }
        if (!isActive && wasActive)
        {
            if (!_photoModeActive)
            {
                WindowState = WindowState.Maximized;
            }
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
            if (_photoModeActive && _boardSuspendedPhotoCache)
            {
                _boardSuspendedPhotoCache = false;
                _currentCacheScope = InkCacheScope.Photo;
                _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
                LoadCurrentPageIfExists();
            }
            if (_photoModeActive && _crossPageDisplayEnabled)
            {
                RequestCrossPageDisplayUpdate();
            }
            _refreshOrchestrator.RequestRefresh("board-exit");
        }
    }

    private void UpdateBoardBackground()
    {
        var color = _boardColor;
        var opacity = _boardOpacity;
        if (opacity == 0 || color.A == 0)
        {
            color = TransparentHitTestColor;
        }
        else
        {
            color = MediaColor.FromArgb(opacity, color.R, color.G, color.B);
        }
        OverlayRoot.Background = new SolidColorBrush(color);
        if (_photoModeActive)
        {
            var active = IsBoardActive();
            PhotoBackground.Visibility = active
                ? Visibility.Collapsed
                : (PhotoBackground.Source != null ? Visibility.Visible : Visibility.Collapsed);
            PhotoControlLayer.Visibility = !active
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void UpdateInputPassthrough()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var enable = _mode == PaintToolMode.Cursor && _boardOpacity == 0 && !_photoModeActive;
        _inputPassthroughEnabled = enable;
        ApplyWindowStyles();
        UpdateFocusAcceptance();
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
        // 光标模式下，不阻止焦点，让输入事件自由传递到演示文稿
        // 这样可以确保键盘和滚轮事件正常工作
        if (_mode == PaintToolMode.Cursor)
        {
            return false;
        }
        
        if (_inputPassthroughEnabled)
        {
            return true;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return false;
        }
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        if (target.IsValid)
        {
            return true;
        }
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            if (wpsTarget.IsValid && ResolveWpsSendMode(wpsTarget) == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
            {
                return true;
            }
        }
        return false;
    }

    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GwlExstyle);
        if (_inputPassthroughEnabled)
        {
            exStyle |= NativeMethods.WsExTransparent;
        }
        else
        {
            exStyle &= ~NativeMethods.WsExTransparent;
        }
        if (_focusBlocked)
        {
            exStyle |= NativeMethods.WsExNoActivate;
        }
        else
        {
            exStyle &= ~NativeMethods.WsExNoActivate;
        }
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GwlExstyle, exStyle);
    }


    public void UpdateInkCacheEnabled(bool enabled)
    {
        _inkCacheEnabled = enabled;
        if (!_inkMonitor.IsEnabled)
        {
            _inkMonitor.Start();
        }
        if (!_inkCacheEnabled)
        {
            _photoCache.Clear();
        }
        _refreshOrchestrator.RequestRefresh("ink-cache");
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
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    _inkStorage.CleanupOldRecords(_inkRetentionDays);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
            });
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
        if (!_rememberPhotoTransform)
        {
            _photoUserTransformDirty = false;
        }
    }

    public void LoadInkPage(int pageIndex)
    {
        // Ink history view is removed; keep for compatibility.
    }

    public void UpdateCrossPageDisplayEnabled(bool enabled)
    {
        if (_crossPageDisplayEnabled == enabled && _photoCrossPageDisplayEnabled == enabled)
        {
            return;
        }
        _crossPageDisplayEnabled = enabled;
        _photoCrossPageDisplayEnabled = enabled;
        if (_photoModeActive && _crossPageDisplayEnabled)
        {
            if (_photoUnifiedTransformReady)
            {
                EnsurePhotoTransformsWritable();
                _photoScale.ScaleX = _lastPhotoScaleX;
                _photoScale.ScaleY = _lastPhotoScaleY;
                _photoTranslate.X = _lastPhotoTranslateX;
                _photoTranslate.Y = _lastPhotoTranslateY;
                _photoUserTransformDirty = true;
                RequestInkRedraw();
            }
            else
            {
                SavePhotoTransformState(userAdjusted: _photoUserTransformDirty);
            }
        }
        if (!_crossPageDisplayEnabled)
        {
            ClearNeighborPages();
        }
        if (_photoModeActive && _photoDocumentIsPdf)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, isPdf: true);
            ResetInkHistory();
            LoadCurrentPageIfExists();
        }
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
        if (_photoModeActive && _crossPageDisplayEnabled)
        {
            EnsurePhotoTransformsWritable();
            _photoScale.ScaleX = _lastPhotoScaleX;
            _photoScale.ScaleY = _lastPhotoScaleY;
            _photoTranslate.X = _lastPhotoTranslateX;
            _photoTranslate.Y = _lastPhotoTranslateY;
            RequestInkRedraw();
        }
    }

    public bool TryGetPhotoUnifiedTransformState(out double scaleX, out double scaleY, out double translateX, out double translateY)
    {
        if (!_photoUnifiedTransformReady)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            translateX = 0.0;
            translateY = 0.0;
            return false;
        }
        scaleX = _lastPhotoScaleX;
        scaleY = _lastPhotoScaleY;
        translateX = _lastPhotoTranslateX;
        translateY = _lastPhotoTranslateY;
        return true;
    }

    public void SetPhotoSequence(IReadOnlyList<string> paths, int currentIndex)
    {
        _photoSequencePaths = paths?.ToList() ?? new List<string>();
        _photoSequenceIndex = currentIndex;
        ClearNeighborImageCache();
    }

    public bool IsPhotoModeActive => _photoModeActive;
    public bool IsWhiteboardActive => IsBoardActive();
    public bool IsPresentationFullscreenActive => _presentationFullscreenActive;

    public void EnterPhotoMode(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }
        _foregroundPhotoActive = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        if (_photoModeActive && string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            Activate();
            return;
        }
        var wasFullscreen = true;
        var wasPresentationFullscreen = false;
        if (!_photoModeActive && (_presentationOptions.AllowOffice || _presentationOptions.AllowWps))
        {
            var target = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                _presentationOptions.AllowWps,
                _presentationOptions.AllowOffice,
                _currentProcessId);
            wasPresentationFullscreen = IsFullscreenPresentationWindow(target);
        }
        if (_photoModeActive)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
        }
        else if (wasPresentationFullscreen || _presentationFullscreenActive)
        {
            _presentationFullscreenActive = false;
            _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
        }
        else if (!_photoModeActive && (_inkStrokes.Count > 0 || _hasDrawing))
        {
            ClearInkSurfaceState();
        }
        var isPdf = IsPdfFile(sourcePath);
        if (_photoModeActive && _photoDocumentIsPdf)
        {
            ClosePdfDocument();
        }
        var restoreSession = false;
        if (_photoSessionPageIndex > 0
            && string.Equals(_photoSessionPath, sourcePath, StringComparison.OrdinalIgnoreCase)
            && _photoSessionIsPdf == isPdf)
        {
            restoreSession = true;
            _currentPageIndex = Math.Max(1, _photoSessionPageIndex);
            if (_rememberPhotoTransform && _photoSessionHasTransform)
            {
                _lastPhotoScaleX = _photoSessionScaleX;
                _lastPhotoScaleY = _photoSessionScaleY;
                _lastPhotoTranslateX = _photoSessionTranslateX;
                _lastPhotoTranslateY = _photoSessionTranslateY;
                _photoUserTransformDirty = true;
            }
            else
            {
                _photoUserTransformDirty = false;
            }
        }
        else
        {
            _currentPageIndex = 1;
            _photoUserTransformDirty = false;
        }
        EnsurePhotoTransformsWritable();
        if (_crossPageDisplayEnabled)
        {
            if (_photoUnifiedTransformReady)
            {
                _photoScale.ScaleX = _lastPhotoScaleX;
                _photoScale.ScaleY = _lastPhotoScaleY;
                _photoTranslate.X = _lastPhotoTranslateX;
                _photoTranslate.Y = _lastPhotoTranslateY;
                _photoUserTransformDirty = true;
            }
            else if (_photoUserTransformDirty)
            {
                _photoScale.ScaleX = _lastPhotoScaleX;
                _photoScale.ScaleY = _lastPhotoScaleY;
                _photoTranslate.X = _lastPhotoTranslateX;
                _photoTranslate.Y = _lastPhotoTranslateY;
                _photoUnifiedTransformReady = true;
            }
            else
            {
                _photoScale.ScaleX = 1.0;
                _photoScale.ScaleY = 1.0;
                _photoTranslate.X = 0;
                _photoTranslate.Y = 0;
            }
        }
        else if (_rememberPhotoTransform)
        {
            var initialKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
            if (!TryApplyStoredPhotoTransform(initialKey))
            {
                _photoScale.ScaleX = 1.0;
                _photoScale.ScaleY = 1.0;
                _photoTranslate.X = 0;
                _photoTranslate.Y = 0;
            }
        }
        else
        {
            _photoScale.ScaleX = 1.0;
            _photoScale.ScaleY = 1.0;
            _photoTranslate.X = 0;
            _photoTranslate.Y = 0;
        }
        _photoModeActive = true;
        _photoFullscreen = wasFullscreen;
        _photoRestoreFullscreenPending = false;
        _presentationFullscreenActive = false;
        _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
        Topmost = true;
        _currentCourseDate = DateTime.Today;
        _currentDocumentName = IoPath.GetFileNameWithoutExtension(sourcePath);
        _currentDocumentPath = sourcePath;
        if (!restoreSession)
        {
            _currentPageIndex = 1;
        }
        _currentCacheScope = InkCacheScope.Photo;
        _currentCacheKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
        _photoDocumentIsPdf = isPdf;
        SetPhotoWindowMode(_photoFullscreen);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        HidePhotoLoadingOverlay();
        if (isPdf)
        {
            ClosePdfDocument();
            ShowPhotoLoadingOverlay("正在加载PDF...");
            StartPdfOpenAsync(sourcePath);
        }
        else
        {
            if (!TrySetPhotoBackground(sourcePath))
            {
                HidePhotoLoadingOverlay();
                ExitPhotoMode();
                return;
            }
        }
        PhotoModeChanged?.Invoke(true);
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = IoPath.GetFileName(sourcePath);
        }
        InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate);
        ResetInkHistory();
        LoadCurrentPageIfExists();
        if (_crossPageDisplayEnabled)
        {
            UpdateCrossPageDisplay();
        }
    }

    public void ExitPhotoMode()
    {
        if (!_photoModeActive)
        {
            return;
        }
        Interlocked.Increment(ref _photoLoadToken);
        HidePhotoLoadingOverlay();
        _foregroundPhotoActive = false;
        FlushPhotoTransformSave();
        SaveCurrentPageOnNavigate(forceBackground: false);
        PhotoBackground.Source = null;
        PhotoBackground.Visibility = Visibility.Collapsed;
        ClearNeighborPages();
        ClosePdfDocument();
        if (!_rememberPhotoTransform)
        {
            EnsurePhotoTransformsWritable();
            _photoScale.ScaleX = 1.0;
            _photoScale.ScaleY = 1.0;
            _photoTranslate.X = 0;
            _photoTranslate.Y = 0;
            _photoUserTransformDirty = false;
        }
        _photoModeActive = false;
        _photoFullscreen = false;
        _photoRestoreFullscreenPending = false;
        _photoDocumentIsPdf = false;
        SetPhotoWindowMode(fullscreen: false);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;
        UpdateInputPassthrough();
        Topmost = true;
        PhotoModeChanged?.Invoke(false);
        _currentDocumentName = string.Empty;
        _currentDocumentPath = string.Empty;
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = "图片应用";
        }
        _currentPageIndex = 1;
        _currentCacheScope = InkCacheScope.None;
        _currentCacheKey = string.Empty;
        ClearInkSurfaceState();
    }





    private void MarkInkInput()
    {
        _lastInkInputUtc = DateTime.UtcNow;
        UpdateInkMonitorInterval();
    }

    private bool ShouldDeferInkContext()
    {
        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            return true;
        }
        if (_lastInkInputUtc == DateTime.MinValue)
        {
            return false;
        }
        return (DateTime.UtcNow - _lastInkInputUtc).TotalMilliseconds < InkInputCooldownMs;
    }

    private void UpdateInkMonitorInterval()
    {
        var nowUtc = DateTime.UtcNow;
        var idle = _lastInkInputUtc != DateTime.MinValue
                   && (nowUtc - _lastInkInputUtc).TotalMilliseconds > InkIdleThresholdMs;

        var targetMs = idle ? InkMonitorIdleIntervalMs : InkMonitorActiveIntervalMs;
        var currentMs = _inkMonitor.Interval.TotalMilliseconds;
        if (Math.Abs(currentMs - targetMs) < 1)
        {
            return;
        }
        _inkMonitor.Interval = TimeSpan.FromMilliseconds(targetMs);
    }

    private void LoadCurrentPageIfExists()
    {
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        if (!_inkCacheEnabled)
        {
            ClearInkSurfaceState();
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            return;
        }
        if (_photoCache.TryGet(_currentCacheKey, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[InkCache] Loaded {cached.Count} strokes for key={_currentCacheKey}");
            ApplyInkStrokes(cached);
            return;
        }
        ClearInkSurfaceState();
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes)
    {
        var applySw = Stopwatch.StartNew();
        _inkStrokes.Clear();
        _inkStrokes.AddRange(CloneInkStrokes(strokes));
        RedrawInkSurface();
        _inkCacheDirty = false;
        _perfApplyStrokes.Add(applySw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void MarkInkCacheDirty()
    {
        _inkCacheDirty = true;
    }

    private void SaveCurrentPageOnNavigate(bool forceBackground)
    {
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        if (!forceBackground && !_inkCacheDirty)
        {
            return;
        }
        FinalizeActiveInkOperation();
        var cacheKey = _currentCacheKey;
        if (!_inkCacheEnabled || string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            _inkCacheDirty = false;
            return;
        }
        _photoCache.Set(cacheKey, strokes);
        _inkCacheDirty = false;
        System.Diagnostics.Debug.WriteLine($"[InkCache] Saved {strokes.Count} strokes for key={cacheKey}");
    }

    private void FinalizeActiveInkOperation()
    {
        if (_lastPointerPosition == null)
        {
            return;
        }
        var position = _lastPointerPosition.Value;
        if (_strokeInProgress)
        {
            EndBrushStroke(position);
        }
        else if (_isErasing)
        {
            EndEraser(position);
        }
        else if (_isRegionSelecting)
        {
            EndRegionSelection(position);
        }
        else if (_isDrawingShape)
        {
            EndShape(position);
        }
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
    }

    private void CopyPhotoBackground(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }
        try
        {
            if (string.Equals(IoPath.GetFullPath(_currentDocumentPath),
                IoPath.GetFullPath(imagePath),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch
        {
            // Ignore path normalize failures.
        }
        try
        {
            File.Copy(_currentDocumentPath, imagePath, overwrite: true);
        }
        catch
        {
            // Ignore copy exceptions.
        }
    }

    private void ResetInkHistory()
    {
        _history.Clear();
        _inkHistory.Clear();
    }

    private void SetPhotoWindowMode(bool fullscreen)
    {
        var wasFullscreen = _photoFullscreen;
        _photoFullscreen = fullscreen;
        if (_photoModeActive && wasFullscreen && !fullscreen)
        {
            SaveAndClearInkSurface();
        }
        PhotoControlLayer.Visibility = _photoModeActive && !IsBoardActive()
            ? Visibility.Visible
            : Visibility.Collapsed;
        PhotoWindowFrame.BorderThickness = _photoModeActive && !_photoFullscreen
            ? new Thickness(1)
            : new Thickness(0);
        if (_photoModeActive)
        {
            PhotoWindowFrame.Background = TryFindResource("Brush_Background") as MediaBrush ?? MediaBrushes.White;
        }
        else
        {
            PhotoWindowFrame.Background = MediaBrushes.Transparent;
        }
        if (_photoModeActive)
        {
            ResizeMode = _photoFullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
            ApplyPhotoWindowBounds(_photoFullscreen);
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        ShowInTaskbar = _photoModeActive;
        OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;
        UpdateInputPassthrough();
    }

    private void ApplyPhotoWindowBounds(bool fullscreen)
    {
        WindowState = WindowState.Normal;
        var rect = GetCurrentMonitorRectInDip(useWorkArea: !fullscreen);
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private void GetCurrentMonitorRect(out NativeMethods.NativeRect monitorRect, out NativeMethods.NativeRect workRect)
    {
        monitorRect = new NativeMethods.NativeRect();
        workRect = new NativeMethods.NativeRect();
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var hMonitor = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            monitorRect = info.Monitor;
            workRect = info.Work;
        }
    }

    private Rect GetCurrentMonitorRectInDip(bool useWorkArea)
    {
        NativeMethods.NativeRect monitorRect;
        NativeMethods.NativeRect workRect;
        GetCurrentMonitorRect(out monitorRect, out workRect);
        
        var target = useWorkArea ? workRect : monitorRect;
        var rect = new Rect(
            target.Left,
            target.Top,
            Math.Max(1, target.Right - target.Left),
            Math.Max(1, target.Bottom - target.Top));

        var dpi = VisualTreeHelper.GetDpi(this);
        if (dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0)
        {
            return rect;
        }
        return new Rect(
            rect.Left / dpi.DpiScaleX,
            rect.Top / dpi.DpiScaleY,
            rect.Width / dpi.DpiScaleX,
            rect.Height / dpi.DpiScaleY);
    }


    private void ApplyFullscreenBounds()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        NativeMethods.NativeRect monitorRect;
        NativeMethods.NativeRect workRect;
        GetCurrentMonitorRect(out monitorRect, out workRect);
        var rect = new Rect(
            monitorRect.Left,
            monitorRect.Top,
            Math.Max(1, monitorRect.Right - monitorRect.Left),
            Math.Max(1, monitorRect.Bottom - monitorRect.Top));

        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            (int)Math.Round(rect.Left),
            (int)Math.Round(rect.Top),
            (int)Math.Round(rect.Width),
            (int)Math.Round(rect.Height),
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    private bool TryBeginPhotoPan(MouseButtonEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            return false;
        }
        _photoPanning = true;
        _photoPanStart = e.GetPosition(OverlayRoot);
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
        OverlayRoot.CaptureMouse();
        e.Handled = true;
        return true;
    }

    private void UpdatePhotoPan(WpfPoint point)
    {
        if (!_photoPanning)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var delta = point - _photoPanStart;
        _photoTranslate.X = _photoPanOriginX + delta.X;
        _photoTranslate.Y = _photoPanOriginY + delta.Y;
        // Enable cross-page display when dragging vertically
        if (_crossPageDisplayEnabled && Math.Abs(delta.Y) > 5)
        {
            _crossPageDragging = true;
            ApplyCrossPageBoundaryLimits();
        }
        UpdateNeighborTransformsForPan();
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        RequestInkRedraw();
    }

    private void ApplyCrossPageBoundaryLimits()
    {
        if (!_crossPageDisplayEnabled || !_photoModeActive)
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var currentPageHeight = GetScaledPageHeight(currentBitmap);
        // Calculate total document height
        double totalHeightAbove = 0;
        for (int i = 1; i < currentPage; i++)
        {
            var height = _photoDocumentIsPdf
                ? GetScaledPdfPageHeight(i)
                : GetScaledPageHeight(GetPageBitmap(i));
            if (height > 0)
            {
                totalHeightAbove += height;
            }
        }
        double totalHeightBelow = 0;
        for (int i = currentPage + 1; i <= totalPages; i++)
        {
            var height = _photoDocumentIsPdf
                ? GetScaledPdfPageHeight(i)
                : GetScaledPageHeight(GetPageBitmap(i));
            if (height > 0)
            {
                totalHeightBelow += height;
            }
        }
        // Calculate limits
        // When at first page, can't scroll up past the top
        var maxY = totalHeightAbove;
        // When at last page, can't scroll down past the bottom
        var minY = -(currentPageHeight + totalHeightBelow - viewportHeight);
        if (minY > maxY)
        {
            var middle = (minY + maxY) * 0.5;
            minY = middle;
            maxY = middle;
        }
        // Apply limits
        var originalY = _photoTranslate.Y;
        _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
        _crossPageTranslateClamped = Math.Abs(originalY - _photoTranslate.Y) > 0.5;
    }

    private void EnsurePhotoTransformsWritable()
    {
        if (!_photoScale.IsFrozen
            && !_photoTranslate.IsFrozen
            && PhotoBackground.RenderTransform is TransformGroup group
            && group.Children.Count == 2
            && ReferenceEquals(group.Children[0], _photoScale)
            && ReferenceEquals(group.Children[1], _photoTranslate))
        {
            return;
        }
        var scale = _photoScale;
        var translate = _photoTranslate;
        _photoScale = new ScaleTransform(scale.ScaleX, scale.ScaleY);
        _photoTranslate = new TranslateTransform(translate.X, translate.Y);
        var photoTransform = new TransformGroup();
        photoTransform.Children.Add(_photoScale);
        photoTransform.Children.Add(_photoTranslate);
        PhotoBackground.RenderTransform = photoTransform;
    }

    private void EndPhotoPan()
    {
        if (!_photoPanning)
        {
            return;
        }
        _photoPanning = false;
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (_crossPageDragging && _crossPageDisplayEnabled)
        {
            _crossPageDragging = false;
            _crossPageTranslateClamped = false;
            FinalizeCurrentPageFromScroll();
        }
        FlushPhotoTransformSave();
        RequestInkRedraw();
    }

    // Cross-page display helper methods
    private void ClearNeighborPages()
    {
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Children.Clear();
        _neighborPagesCanvas.Visibility = Visibility.Collapsed;
        _neighborPageImages.Clear();
        _neighborInkImages.Clear();
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
    }

    private int GetTotalPageCount()
    {
        if (_photoDocumentIsPdf)
        {
            return _pdfPageCount;
        }
        return _photoSequencePaths.Count;
    }

    private int GetCurrentPageIndexForCrossPage()
    {
        if (_photoDocumentIsPdf)
        {
            return _currentPageIndex;
        }
        return _photoSequenceIndex >= 0 ? _photoSequenceIndex + 1 : 1;
    }

    private void SetCurrentPageIndexForCrossPage(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = pageIndex;
        }
        else
        {
            _photoSequenceIndex = pageIndex - 1;
        }
    }

    private BitmapSource? GetPageBitmap(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return GetPdfPageBitmap(pageIndex);
        }
        // For image sequence, pageIndex is 1-based
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return null;
        }
        if (_neighborImageCache.TryGetValue(pageIndex, out var cached))
        {
            return cached;
        }
        var path = _photoSequencePaths[arrayIndex];
        var bitmap = TryLoadBitmapSource(path);
        if (bitmap != null)
        {
            _neighborImageCache[pageIndex] = bitmap;
            // Limit cache size
            if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
            {
                var keysToRemove = _neighborImageCache.Keys
                    .OrderBy(k => Math.Abs(k - pageIndex))
                    .Skip(NeighborPageCacheLimit)
                    .ToList();
                foreach (var k in keysToRemove)
                {
                    _neighborImageCache.Remove(k);
                }
            }
        }
        return bitmap;
    }

    private BitmapSource? GetNeighborPageBitmap(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return TryGetCachedPdfPageBitmap(pageIndex, out var cached) ? cached : null;
        }
        return GetPageBitmap(pageIndex);
    }

    private BitmapSource? TryLoadBitmapSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void ScheduleNeighborImagePrefetch(int pageIndex)
    {
        if (!_photoModeActive || _photoDocumentIsPdf)
        {
            return;
        }
        if (!_crossPageDisplayEnabled && (_photoPanning || _crossPageDragging))
        {
            return;
        }
        if (_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count)
        {
            return;
        }
        if (_neighborImageCache.ContainsKey(pageIndex))
        {
            return;
        }
        if (!_neighborImagePrefetchPending.Add(pageIndex))
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!_photoModeActive || _photoDocumentIsPdf || _crossPageDragging)
                {
                    return;
                }
                if (_neighborImageCache.ContainsKey(pageIndex))
                {
                    return;
                }
                var path = _photoSequencePaths[pageIndex - 1];
                var bitmap = TryLoadBitmapSource(path);
                if (bitmap == null)
                {
                    return;
                }
                _neighborImageCache[pageIndex] = bitmap;
                if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
                {
                    var keysToRemove = _neighborImageCache.Keys
                        .OrderBy(k => Math.Abs(k - pageIndex))
                        .Skip(NeighborPageCacheLimit)
                        .ToList();
                    foreach (var k in keysToRemove)
                    {
                        _neighborImageCache.Remove(k);
                    }
                }
            }
            finally
            {
                _neighborImagePrefetchPending.Remove(pageIndex);
            }
        }, DispatcherPriority.Background);
    }

    private double GetScaledPageHeight(BitmapSource? bitmap)
    {
        if (bitmap == null)
        {
            return 0;
        }
        var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        var imageHeight = bitmap.PixelHeight * 96.0 / dpiY;
        return imageHeight * _photoScale.ScaleY;
    }

    private void UpdateCrossPageDisplay()
    {
        if (!_crossPageDisplayEnabled || !_photoModeActive)
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var currentPageHeight = GetScaledPageHeight(currentBitmap);
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;
        // Determine which neighbor pages are visible
        var visiblePages = new List<(int PageIndex, double Top)>();
        // Always include current page
        visiblePages.Add((currentPage, currentTop));
        // Check if previous page is visible (above viewport top)
        if (currentTop > 0 && currentPage > 1)
        {
            var prevBitmap = GetNeighborPageBitmap(currentPage - 1);
            if (prevBitmap != null)
            {
                var prevHeight = GetScaledPageHeight(prevBitmap);
                visiblePages.Insert(0, (currentPage - 1, currentTop - prevHeight));
                // Check if page before that is visible
                if (currentTop - prevHeight > 0 && currentPage > 2)
                {
                    var prevPrevBitmap = GetNeighborPageBitmap(currentPage - 2);
                    if (prevPrevBitmap != null)
                    {
                        var prevPrevHeight = GetScaledPageHeight(prevPrevBitmap);
                        visiblePages.Insert(0, (currentPage - 2, currentTop - prevHeight - prevPrevHeight));
                    }
                }
            }
        }
        // Check if next page is visible (current bottom above viewport bottom)
        if (currentBottom < viewportHeight && currentPage < totalPages)
        {
            visiblePages.Add((currentPage + 1, currentBottom));
            // Check if page after that is visible
            var nextBitmap = GetNeighborPageBitmap(currentPage + 1);
            if (nextBitmap != null)
            {
                var nextHeight = GetScaledPageHeight(nextBitmap);
                if (currentBottom + nextHeight < viewportHeight && currentPage + 1 < totalPages)
                {
                    visiblePages.Add((currentPage + 2, currentBottom + nextHeight));
                }
            }
        }
        if (_photoDocumentIsPdf)
        {
            var missingPages = visiblePages
                .Where(p => p.PageIndex != currentPage)
                .Select(p => p.PageIndex)
                .Distinct()
                .Where(p => !TryGetCachedPdfPageBitmap(p, out _))
                .ToList();
            if (missingPages.Count > 0)
            {
                SchedulePdfVisiblePrefetch(missingPages);
            }
            _pdfPinnedPages.Clear();
            foreach (var page in visiblePages.Select(p => p.PageIndex).Distinct())
            {
                _pdfPinnedPages.Add(page);
            }
        }
        if (!_photoDocumentIsPdf)
        {
            ScheduleNeighborImagePrefetch(currentPage - 1);
            ScheduleNeighborImagePrefetch(currentPage + 1);
        }
        // Render neighbor pages
        var neighborPages = visiblePages.Where(p => p.PageIndex != currentPage).ToList();
        if (_crossPageDragging && _crossPageTranslateClamped && neighborPages.Count == 0)
        {
            return;
        }
        RenderNeighborPages(neighborPages);
    }

    private void RenderNeighborPages(List<(int PageIndex, double Top)> neighborPages)
    {
        if (neighborPages.Count == 0)
        {
            ClearNeighborPages();
            return;
        }
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Visibility = Visibility.Visible;
        // Ensure we have enough Image elements
        while (_neighborPageImages.Count < neighborPages.Count)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }
        while (_neighborInkImages.Count < neighborPages.Count)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }
        // Hide excess images
        for (int i = neighborPages.Count; i < _neighborPageImages.Count; i++)
        {
            _neighborPageImages[i].Visibility = Visibility.Collapsed;
            if (i < _neighborInkImages.Count)
            {
                _neighborInkImages[i].Visibility = Visibility.Collapsed;
            }
        }
        // Update visible neighbor page images
        for (int i = 0; i < neighborPages.Count; i++)
        {
            var (pageIndex, top) = neighborPages[i];
            var bitmap = GetNeighborPageBitmap(pageIndex);
            var img = _neighborPageImages[i];
            img.Source = bitmap;
            img.Visibility = bitmap != null ? Visibility.Visible : Visibility.Collapsed;
            var inkImg = _neighborInkImages[i];
            if (bitmap != null)
            {
                var inkBitmap = TryGetNeighborInkBitmap(pageIndex, bitmap);
                inkImg.Source = inkBitmap;
                inkImg.Visibility = inkBitmap != null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                inkImg.Source = null;
                inkImg.Visibility = Visibility.Collapsed;
            }
            if (bitmap != null)
            {
                var baseTop = top - _photoTranslate.Y;
                img.Tag = baseTop;
                inkImg.Tag = baseTop;
                // Apply same transform as current page
                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(_photoScale.ScaleX, _photoScale.ScaleY));
                transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));
                img.RenderTransform = transform;
                inkImg.RenderTransform = transform;
            }
        }
    }

    private void UpdateNeighborTransformsForPan()
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        if (_neighborPageImages.Count == 0 || _neighborInkImages.Count == 0)
        {
            return;
        }
        for (int i = 0; i < _neighborPageImages.Count; i++)
        {
            var img = _neighborPageImages[i];
            if (img.Visibility != Visibility.Visible || img.RenderTransform is not TransformGroup group)
            {
                continue;
            }
            if (img.Tag is not double baseTop || group.Children.Count < 2)
            {
                continue;
            }
            if (group.Children[1] is TranslateTransform translate)
            {
                translate.X = _photoTranslate.X;
                translate.Y = _photoTranslate.Y + baseTop;
            }
            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (inkImg.Visibility != Visibility.Visible || inkImg.RenderTransform is not TransformGroup inkGroup)
                {
                    continue;
                }
                if (inkGroup.Children.Count < 2)
                {
                    continue;
                }
                if (inkGroup.Children[1] is TranslateTransform inkTranslate)
                {
                    inkTranslate.X = _photoTranslate.X;
                    inkTranslate.Y = _photoTranslate.Y + baseTop;
                }
            }
        }
    }

    private void FinalizeCurrentPageFromScroll()
    {
        if (!_crossPageDisplayEnabled)
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var viewportCenter = viewportHeight / 2;
        var currentPageHeight = GetScaledPageHeight(currentBitmap);
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;

        // Determine which page contains the viewport center
        int newCurrentPage = currentPage;
        double newTranslateY = currentTop;

        if (currentTop > viewportCenter && currentPage > 1)
        {
            // Previous page is at center
            newCurrentPage = currentPage - 1;
            var newHeight = _photoDocumentIsPdf
                ? GetScaledPdfPageHeight(newCurrentPage)
                : GetScaledPageHeight(GetPageBitmap(newCurrentPage));
            newTranslateY = currentTop - newHeight;
        }
        else if (currentBottom < viewportCenter && currentPage < totalPages)
        {
            // Next page is at center
            newCurrentPage = currentPage + 1;
            var newHeight = _photoDocumentIsPdf
                ? GetScaledPdfPageHeight(newCurrentPage)
                : GetScaledPageHeight(GetPageBitmap(newCurrentPage));
            newTranslateY = currentTop + (newHeight > 0 ? newHeight : currentPageHeight);
        }

        if (newCurrentPage != currentPage)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            NavigateToPage(newCurrentPage, newTranslateY);
        }
        else
        {
            UpdateCrossPageDisplay();
        }
    }

    private void NavigateToPage(int newPageIndex, double newTranslateY)
    {
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = newPageIndex;
            _currentCacheKey = BuildPdfCacheKey(_currentDocumentPath, _currentPageIndex);
            ResetInkHistory();
            LoadCurrentPageIfExists();
            if (!RenderPdfPage(_currentPageIndex))
            {
                return;
            }
        }
        else
        {
            _photoSequenceIndex = newPageIndex - 1;
            if (_photoSequenceIndex >= 0 && _photoSequenceIndex < _photoSequencePaths.Count)
            {
                var newPath = _photoSequencePaths[_photoSequenceIndex];
                _currentDocumentName = IoPath.GetFileNameWithoutExtension(newPath);
                _currentDocumentPath = newPath;
                _currentCacheKey = BuildPhotoCacheKey(newPath);
                ResetInkHistory();
                LoadCurrentPageIfExists();
                var newBitmap = GetPageBitmap(newPageIndex);
                if (newBitmap != null)
                {
                    PhotoBackground.Source = newBitmap;
                    PhotoBackground.Visibility = Visibility.Visible;
                }
                if (PhotoTitleText != null)
                {
                    PhotoTitleText.Text = IoPath.GetFileName(newPath);
                }
            }
        }
        // Apply new position
        _photoTranslate.Y = newTranslateY;

        // Clamp to reasonable bounds
        var newBitmapSource = PhotoBackground.Source as BitmapSource;
        if (newBitmapSource != null)
        {
            var newPageHeight = GetScaledPageHeight(newBitmapSource);
            var minY = -(newPageHeight - OverlayRoot.ActualHeight * 0.1);
            var maxY = OverlayRoot.ActualHeight * 0.9;
            _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
        }
        InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate);
        RedrawInkSurface();
        UpdateCrossPageDisplay();
    }

    private void OnPhotoTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            FloatingZOrderRequested?.Invoke();
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag exceptions.
            }
            FloatingZOrderRequested?.Invoke();
        }
    }

    private void OnPhotoMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        ExecutePhotoMinimize();
        if (e.RoutedEvent != null)
        {
            e.Handled = true;
        }
    }

    private void OnPhotoPrevClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        // 尝试 PDF 内部导航
        if (TryNavigatePdf(-1))
        {
            return;
        }
        // 触发外部导航事件 (MainWindow 会处理文件间切换)
        if (!IsAtFileSequenceBoundary(-1))
        {
            PhotoNavigationRequested?.Invoke(-1);
        }
        // 注意: 如果到达边界,MainWindow 会简单返回而不做任何事
        // 这样可以保持当前状态,不会退出全屏模式
    }

    private void OnPhotoNextClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        // 尝试 PDF 内部导航
        if (TryNavigatePdf(1))
        {
            return;
        }
        // 触发外部导航事件 (MainWindow 会处理文件间切换)
        if (!IsAtFileSequenceBoundary(1))
        {
            PhotoNavigationRequested?.Invoke(1);
        }
        // 注意: 如果到达边界,MainWindow 会简单返回而不做任何事
        // 这样可以保持当前状态,不会退出全屏模式
    }

    private BitmapSource? TryGetNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap)
    {
        if (!_inkCacheEnabled || pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return null;
        }
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return null;
        }
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            _neighborInkCache.Remove(cacheKey);
            return null;
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var entry) && ReferenceEquals(entry.Strokes, strokes))
        {
            return entry.Bitmap;
        }
        ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
        return null;
    }

    private void ScheduleNeighborInkRender(
        string cacheKey,
        int pageIndex,
        BitmapSource pageBitmap,
        List<InkStrokeData> strokes)
    {
        if (_neighborInkRenderPending.Contains(cacheKey))
        {
            return;
        }
        _neighborInkRenderPending.Add(cacheKey);
        var scheduled = TryBeginInvoke(() =>
        {
            try
            {
                if (!_photoModeActive || !_crossPageDisplayEnabled)
                {
                    return;
                }
                if (!_photoCache.TryGet(cacheKey, out var currentStrokes) || currentStrokes.Count == 0)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                if (_neighborInkCache.TryGetValue(cacheKey, out var existing) && ReferenceEquals(existing.Strokes, currentStrokes))
                {
                    return;
                }
                var page = new InkPageData
                {
                    PageIndex = pageIndex,
                    DocumentName = _currentDocumentName,
                    SourcePath = _currentDocumentPath,
                    Strokes = currentStrokes
                };
                var bitmap = _inkStrokeRenderer.RenderPage(
                    page,
                    pageBitmap.PixelWidth,
                    pageBitmap.PixelHeight,
                    pageBitmap.DpiX,
                    pageBitmap.DpiY);
                _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(currentStrokes, bitmap);
                RequestCrossPageDisplayUpdate();
            }
            finally
            {
                _neighborInkRenderPending.Remove(cacheKey);
            }
        }, DispatcherPriority.Background);
        if (!scheduled)
        {
            _neighborInkRenderPending.Remove(cacheKey);
        }
    }

    private string BuildNeighborInkCacheKey(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return BuildPdfCacheKey(_currentDocumentPath, pageIndex);
        }
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return string.Empty;
        }
        return BuildPhotoCacheKey(_photoSequencePaths[arrayIndex]);
    }

    private sealed record InkBitmapCacheEntry(List<InkStrokeData> Strokes, BitmapSource Bitmap);

    private bool TrySetPhotoBackground(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            PhotoBackground.Source = bitmap;
            PhotoBackground.Visibility = Visibility.Visible;
            if (_crossPageDisplayEnabled)
            {
                if (_photoUnifiedTransformReady)
                {
                    EnsurePhotoTransformsWritable();
                    _photoScale.ScaleX = _lastPhotoScaleX;
                    _photoScale.ScaleY = _lastPhotoScaleY;
                    _photoTranslate.X = _lastPhotoTranslateX;
                    _photoTranslate.Y = _lastPhotoTranslateY;
                }
                else
                {
                    ApplyPhotoFitToViewport(bitmap);
                }
                return true;
            }
            var appliedStored = TryApplyStoredPhotoTransform(GetCurrentPhotoTransformKey());
            if (!appliedStored)
            {
                ApplyPhotoFitToViewport(bitmap);
            }
            return true;
        }
        catch
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
    }





    private bool IsAtFileSequenceBoundary(int direction)
    {
        if (_photoSequencePaths.Count == 0)
        {
            return true;
        }
        var next = _photoSequenceIndex + direction;
        return next < 0 || next >= _photoSequencePaths.Count;
    }

    private void ApplyPhotoFitToViewport(BitmapSource bitmap, double? dpiOverride = null)
    {
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var viewportWidth = OverlayRoot.ActualWidth;
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
            viewportHeight = monitor.Height;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            return;
        }
        var dpiX = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiX;
        var dpiY = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiY;
        var imageWidth = dpiX > 0 ? bitmap.PixelWidth * 96.0 / dpiX : bitmap.PixelWidth;
        var imageHeight = dpiY > 0 ? bitmap.PixelHeight * 96.0 / dpiY : bitmap.PixelHeight;

        if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
        {
            (imageWidth, imageHeight) = (imageHeight, imageWidth);
        }

        var scaleX = viewportWidth / imageWidth;
        var scaleY = viewportHeight / imageHeight;
        var scale = Math.Min(scaleX, scaleY);
        _photoScale.ScaleX = scale;
        _photoScale.ScaleY = scale;
        var scaledWidth = imageWidth * scale;
        var scaledHeight = imageHeight * scale;
        _photoTranslate.X = (viewportWidth - scaledWidth) / 2.0;
        _photoTranslate.Y = (viewportHeight - scaledHeight) / 2.0;
        SavePhotoTransformState(userAdjusted: false);
        RequestInkRedraw();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureRasterSurface();
        if (!_photoModeActive || _photoUserTransformDirty)
        {
            return;
        }
        if (PhotoBackground.Source is BitmapSource bitmap)
        {
            ApplyPhotoFitToViewport(bitmap);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (WindowState == WindowState.Minimized)
        {
            _photoRestoreFullscreenPending = true;
            // Save current zoom/pan state before minimizing
            SavePhotoTransformState(true);
            return;
        }
        if (_photoRestoreFullscreenPending)
        {
            _photoRestoreFullscreenPending = false;
            _photoFullscreen = true;
            SetPhotoWindowMode(fullscreen: true);

            // Restore PDF page rendering
            if (_photoDocumentIsPdf && _pdfDocument != null)
            {
                RenderPdfPage(_currentPageIndex);
            }

            // Restore zoom/pan state if remember transform is enabled
            if (_rememberPhotoTransform)
            {
                var key = GetCurrentPhotoTransformKey();
                if (!_crossPageDisplayEnabled && TryApplyStoredPhotoTransform(key))
                {
                }
                else
                {
                    EnsurePhotoTransformsWritable();
                    _photoScale.ScaleX = _lastPhotoScaleX;
                    _photoScale.ScaleY = _lastPhotoScaleY;
                    _photoTranslate.X = _lastPhotoTranslateX;
                    _photoTranslate.Y = _lastPhotoTranslateY;
                }
            }
        }
    }

    private readonly struct PhotoTransformState
    {
        public PhotoTransformState(double scaleX, double scaleY, double translateX, double translateY, bool userAdjusted)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            TranslateX = translateX;
            TranslateY = translateY;
            UserAdjusted = userAdjusted;
        }

        public double ScaleX { get; }
        public double ScaleY { get; }
        public double TranslateX { get; }
        public double TranslateY { get; }
        public bool UserAdjusted { get; }
    }

    private string GetCurrentPhotoTransformKey()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return string.Empty;
        }
        return BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
    }

    private bool TryApplyStoredPhotoTransform(string cacheKey)
    {
        if (_crossPageDisplayEnabled)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (!_photoPageTransforms.TryGetValue(cacheKey, out var state))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = state.ScaleX;
        _photoScale.ScaleY = state.ScaleY;
        _photoTranslate.X = state.TranslateX;
        _photoTranslate.Y = state.TranslateY;
        _photoUserTransformDirty = state.UserAdjusted;
        return true;
    }

    private void SavePhotoTransformState(bool userAdjusted)
    {
        _lastPhotoScaleX = _photoScale.ScaleX;
        _lastPhotoScaleY = _photoScale.ScaleY;
        _lastPhotoTranslateX = _photoTranslate.X;
        _lastPhotoTranslateY = _photoTranslate.Y;
        _photoUserTransformDirty = userAdjusted;
        if (_crossPageDisplayEnabled)
        {
            _photoUnifiedTransformReady = true;
            SchedulePhotoUnifiedTransformSave();
            return;
        }
        if (_rememberPhotoTransform && _photoModeActive)
        {
            var key = GetCurrentPhotoTransformKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                _photoPageTransforms[key] = new PhotoTransformState(
                    _photoScale.ScaleX,
                    _photoScale.ScaleY,
                    _photoTranslate.X,
                    _photoTranslate.Y,
                    userAdjusted);
            }
        }
    }

    private void SavePhotoSession()
    {
        if (!_photoModeActive || string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }
        _photoSessionPath = _currentDocumentPath;
        _photoSessionIsPdf = _photoDocumentIsPdf;
        _photoSessionPageIndex = _currentPageIndex;
        if (_rememberPhotoTransform && _photoUserTransformDirty)
        {
            _photoSessionHasTransform = true;
            _photoSessionScaleX = _photoScale.ScaleX;
            _photoSessionScaleY = _photoScale.ScaleY;
            _photoSessionTranslateX = _photoTranslate.X;
            _photoSessionTranslateY = _photoTranslate.Y;
        }
        else
        {
            _photoSessionHasTransform = false;
        }
    }

    private static string BuildPhotoCacheKey(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }
        try
        {
            return $"img|{IoPath.GetFullPath(sourcePath)}";
        }
        catch
        {
            return $"img|{sourcePath}";
        }
    }

    private string BuildPhotoModeCacheKey(string sourcePath, int pageIndex, bool isPdf)
    {
        if (!isPdf)
        {
            return BuildPhotoCacheKey(sourcePath);
        }
        return BuildPdfCacheKey(sourcePath, pageIndex);
    }

    private static string BuildPdfCacheKey(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return string.Empty;
        }
        try
        {
            return $"pdf|{IoPath.GetFullPath(sourcePath)}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
        catch
        {
            return $"pdf|{sourcePath}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
    }

    private static bool IsPdfFile(string path)
    {
        var ext = IoPath.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }


    private bool IsBoardActive()
    {
        return _boardOpacity > 0 && _boardColor.A > 0;
    }

    private void OnStylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
    {
        var position = e.GetPosition(OverlayRoot);
        HandlePointerDown(position);
        e.Handled = true;
    }

    private void OnStylusMove(object sender, System.Windows.Input.StylusEventArgs e)
    {
        if (e.InAir)
        {
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerMove(position);
        e.Handled = true;
    }

    private void OnStylusUp(object sender, System.Windows.Input.StylusEventArgs e)
    {
        var position = e.GetPosition(OverlayRoot);
        HandlePointerUp(position);
        e.Handled = true;
    }


    private void RequestCrossPageDisplayUpdate()
    {
        if (_crossPageUpdatePending)
        {
            return;
        }
        var nowUtc = DateTime.UtcNow;
        var throttleActive = _photoPanning || _crossPageDragging;
        var elapsedMs = (nowUtc - _lastCrossPageUpdateUtc).TotalMilliseconds;
        if (throttleActive && elapsedMs < CrossPageUpdateMinIntervalMs)
        {
            _crossPageUpdatePending = true;
            var token = Interlocked.Increment(ref _crossPageUpdateToken);
            var delay = Math.Max(1, (int)Math.Ceiling(CrossPageUpdateMinIntervalMs - elapsedMs));
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _crossPageUpdateToken)
                    {
                        return;
                    }
                    _crossPageUpdatePending = false;
                    if (!_photoModeActive || !_crossPageDisplayEnabled)
                    {
                        return;
                    }
                    _lastCrossPageUpdateUtc = DateTime.UtcNow;
                    UpdateCrossPageDisplay();
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    _crossPageUpdatePending = false;
                }
            });
            return;
        }
        _crossPageUpdatePending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            _crossPageUpdatePending = false;
            if (!_photoModeActive || !_crossPageDisplayEnabled)
            {
                return;
            }
            _lastCrossPageUpdateUtc = DateTime.UtcNow;
            UpdateCrossPageDisplay();
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _crossPageUpdatePending = false;
        }
    }

    private bool TryBeginInvoke(Action action, DispatcherPriority priority)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return false;
        }
        try
        {
            Dispatcher.BeginInvoke(action, priority);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ShowPhotoLoadingOverlay(string message)
    {
        _photoLoading = true;
        if (PhotoLoadingText != null)
        {
            PhotoLoadingText.Text = message;
        }
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Visible;
        }
        if (OverlayRoot != null)
        {
            OverlayRoot.IsHitTestVisible = false;
        }
    }

    private void HidePhotoLoadingOverlay()
    {
        _photoLoading = false;
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Collapsed;
        }
        if (OverlayRoot != null)
        {
            OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;
        }
    }

    private void ShowPhotoContextMenu(WpfPoint position)
    {
        if (!_photoModeActive || !_photoFullscreen || _mode != PaintToolMode.Cursor)
        {
            return;
        }
        var menu = new ContextMenu();
        var minimizeItem = new MenuItem
        {
            Header = "最小化"
        };
        minimizeItem.Click += (_, _) => ExecutePhotoMinimize();
        menu.Items.Add(minimizeItem);
        menu.PlacementTarget = OverlayRoot;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void ExecutePhotoMinimize()
    {
        if (!_photoModeActive)
        {
            return;
        }
        SavePhotoSession();
        ExitPhotoMode();
    }

    private void SchedulePhotoTransformSave(bool userAdjusted)
    {
        if (!_photoModeActive)
        {
            return;
        }
        _photoTransformSavePending = true;
        if (userAdjusted)
        {
            _photoTransformSaveUserAdjusted = true;
        }
        if (_photoTransformSaveTimer == null)
        {
            _photoTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _photoTransformSaveTimer.Tick += (_, _) =>
            {
                _photoTransformSaveTimer?.Stop();
                if (!_photoTransformSavePending)
                {
                    _photoTransformSaveUserAdjusted = false;
                    return;
                }
                var adjusted = _photoTransformSaveUserAdjusted;
                _photoTransformSavePending = false;
                _photoTransformSaveUserAdjusted = false;
                SavePhotoTransformState(adjusted);
            };
        }
        _photoTransformSaveTimer.Stop();
        _photoTransformSaveTimer.Start();
    }

    private void FlushPhotoTransformSave()
    {
        if (!_photoTransformSavePending)
        {
            return;
        }
        _photoTransformSaveTimer?.Stop();
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void SchedulePhotoUnifiedTransformSave()
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        _pendingUnifiedScaleX = _lastPhotoScaleX;
        _pendingUnifiedScaleY = _lastPhotoScaleY;
        _pendingUnifiedTranslateX = _lastPhotoTranslateX;
        _pendingUnifiedTranslateY = _lastPhotoTranslateY;
        if (_photoUnifiedTransformSaveTimer == null)
        {
            _photoUnifiedTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _photoUnifiedTransformSaveTimer.Tick += (_, _) =>
            {
                _photoUnifiedTransformSaveTimer?.Stop();
                PhotoUnifiedTransformChanged?.Invoke(
                    _pendingUnifiedScaleX,
                    _pendingUnifiedScaleY,
                    _pendingUnifiedTranslateX,
                    _pendingUnifiedTranslateY);
            };
        }
        _photoUnifiedTransformSaveTimer.Stop();
        _photoUnifiedTransformSaveTimer.Start();
    }

}
