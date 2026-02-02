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
    private const int WsCaption = 0x00C00000;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    
    /// <summary>演示文稿焦点监控间隔（ms） - 平衡响应速度和 CPU 占用</summary>
    private const int PresentationFocusMonitorIntervalMs = 500;
    
    /// <summary>演示文稿焦点恢复冷却时间（ms） - 防止频繁切换导致闪烁</summary>
    private const int PresentationFocusCooldownMs = 1200;
    
    /// <summary>墨迹输入冷却时间（ms） - 防抖动</summary>
    private const int InkInputCooldownMs = 120;
    
    /// <summary>墨迹监控激活间隔（ms） - 有绘图活动时的检查频率</summary>
    private const int InkMonitorActiveIntervalMs = 600;
    
    /// <summary>墨迹监控空闲间隔（ms） - 无绘图活动时降低检查频率以节省性能</summary>
    private const int InkMonitorIdleIntervalMs = 1400;
    
    /// <summary>墨迹空闲阈值（ms） - 超过此时间无输入视为空闲状态</summary>
    private const int InkIdleThresholdMs = 2500;
    
    /// <summary>墨迹重绘最小间隔（ms） - 约 60fps，平衡流畅度和性能</summary>
    private const int InkRedrawMinIntervalMs = 16;
    
    /// <summary>跨页更新最小间隔（ms） - 连续滚动时的渲染节流</summary>
    private const int CrossPageUpdateMinIntervalMs = 24;
    
    /// <summary>书法印章笔画宽度因子 - 相对于笔画宽度的比例</summary>
    private const double CalligraphySealStrokeWidthFactor = 0.08;
    
    /// <summary>书法覆盖层不透明度阈值（0-255） - 大于此值时应用墨迹渗透效果</summary>
    private const byte DefaultCalligraphyOverlayOpacityThreshold = 230;
    
    /// <summary>书法预览最小间隔（ms） - 约 60fps，平衡预览效果和性能</summary>
    private const int CalligraphyPreviewMinIntervalMs = 16;
    
    /// <summary>书法预览最小距离（像素） - 鼠标移动小于此距离不触发预览更新</summary>
    private const double CalligraphyPreviewMinDistance = 2.0;
    
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
    private bool _calligraphyInkBloomEnabled = true;
    private bool _calligraphySealEnabled = true;
    private byte _calligraphyOverlayOpacityThreshold = DefaultCalligraphyOverlayOpacityThreshold;
    private WhiteboardBrushPreset _whiteboardPreset = WhiteboardBrushPreset.Smooth;
    private CalligraphyBrushPreset _calligraphyPreset = CalligraphyBrushPreset.Sharp;

    private sealed class RasterSnapshot
    {
        public RasterSnapshot(int width, int height, double dpiX, double dpiY, byte[] pixels)
        {
            PixelWidth = width;
            PixelHeight = height;
            DpiX = dpiX;
            DpiY = dpiY;
            Pixels = pixels;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public double DpiX { get; }
        public double DpiY { get; }
        public byte[] Pixels { get; }
    }
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

    private PaintBrushStyle _brushStyle = PaintBrushStyle.Standard;
    private IBrushRenderer? _activeRenderer;
    private DrawingVisualHost _visualHost;
    private WriteableBitmap? _rasterSurface;
    private int _surfacePixelWidth;
    private int _surfacePixelHeight;
    private double _surfaceDpiX = 96.0;
    private double _surfaceDpiY = 96.0;
    private MediaColor _brushColor = Colors.Red;
    private double _brushSize = 12.0;
    private byte _brushOpacity = 255;
    private double _eraserSize = 24.0;
    private bool _isErasing;
    private bool _strokeInProgress;
    private WpfPoint? _lastEraserPoint;
    private bool _hasDrawing;
    private readonly Random _inkRandom = new Random();
    private DateTime _lastCalligraphyPreviewUtc = DateTime.MinValue;
    private WpfPoint? _lastCalligraphyPreviewPoint;
    private WpfPoint? _lastPointerPosition;
    private bool _presentationFocusRestoreEnabled = false;
    private readonly List<InkStrokeData> _inkStrokes = new();
    private bool _inkCacheDirty;
    private bool _boardSuspendedPhotoCache;
    private readonly List<InkSnapshot> _inkHistory = new();
    private readonly DispatcherTimer _inkMonitor;
    private readonly Random _inkSeedRandom = new Random();
    private bool _inkRecordEnabled = true;
    private bool _inkReplayPreviousEnabled;
    private int _inkRetentionDays = 30;
    private string _inkPhotoRootPath = string.Empty;
    private bool _inkCacheEnabled = true;
    private DateTime _lastInkInputUtc = DateTime.MinValue;
    private bool _pendingInkContextCheck;
    private readonly RefreshOrchestrator _refreshOrchestrator;
    private readonly PerfStats _perfMonitor;
    private readonly PerfStats _perfSavePage;
    private readonly PerfStats _perfLoadPage;
    private readonly PerfStats _perfRedrawSurface;
    private readonly PerfStats _perfEnsureSurface;
    private readonly PerfStats _perfClearSurface;
    private readonly PerfStats _perfApplyStrokes;
    private static readonly ArrayPool<byte> PixelPool = ArrayPool<byte>.Shared;
    private const int InkNoiseTileCacheLimit = 96;
    private static readonly object InkNoiseTileCacheLock = new();
    private static readonly Dictionary<InkNoiseTileKey, InkNoiseTileEntry> InkNoiseTileCache = new();
    private static readonly LinkedList<InkNoiseTileKey> InkNoiseTileOrder = new();
    private byte[]? _clearSurfaceBuffer;
    private int _clearSurfaceBufferSize;
    private bool _presentationFullscreenActive;
    private DateTime _currentCourseDate = DateTime.Today;
    private string _currentDocumentName = string.Empty;
    private string _currentDocumentPath = string.Empty;
    private int _currentPageIndex = 1;
    private string _currentCacheKey = string.Empty;
    private ClassroomToolkit.Interop.Presentation.PresentationType _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
    private InkCacheScope _currentCacheScope = InkCacheScope.None;
    private readonly InkFinalCache _photoCache = new(80);
    private InkStorageService _inkStorage = new();
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
    private readonly InkStrokeRenderer _inkStrokeRenderer = new();
    private const int NeighborPageCacheLimit = 4;
    private bool _redrawPending;
    private bool _redrawInProgress;
    private DateTime _lastInkRedrawUtc = DateTime.MinValue;
    private int _inkRedrawToken;
    private enum InkCacheScope
    {
        None = 0,
        Photo = 1
    }

    public event Action<string, DateTime>? InkContextChanged;
    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action? FloatingZOrderRequested;
    public event Action? PresentationFullscreenDetected;
    public event Action<ClassroomToolkit.Interop.Presentation.PresentationType>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;

    private sealed record InkSnapshot(List<InkStrokeData> Strokes);

    private class DrawingVisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;

        public DrawingVisualHost()
        {
            _children = new VisualCollection(this);
        }

        public void AddVisual(Visual visual)
        {
            _children.Add(visual);
        }
        
        public void RemoveVisual(Visual visual)
        {
            _children.Remove(visual);
        }

        public void Clear()
        {
            _children.Clear();
        }

        public void UpdateVisual(Action<DrawingContext> renderAction)
        {
            if (_children.Count == 0)
            {
                _children.Add(new DrawingVisual());
            }

            var visual = (DrawingVisual)_children[0];
            using (var dc = visual.RenderOpen())
            {
                renderAction(dc);
            }
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            return _children[index];
        }
    }

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
        _presentationService = new ClassroomToolkit.Services.Presentation.PresentationControlService(planner, mapper, sender, _presentationResolver);
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

    private void BeginBrushStroke(WpfPoint position)
    {
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _strokeInProgress = true;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(position);
        _visualHost.UpdateVisual(_activeRenderer.Render);
        _lastCalligraphyPreviewUtc = DateTime.UtcNow;
        _lastCalligraphyPreviewPoint = position;
    }

    private void UpdateBrushStroke(WpfPoint position)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        _activeRenderer.OnMove(position);
        if (_brushStyle == PaintBrushStyle.Calligraphy && ShouldThrottleCalligraphyPreview(position))
        {
            return;
        }
        _visualHost.UpdateVisual(_activeRenderer.Render);
    }

    private void EndBrushStroke(WpfPoint position)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        _activeRenderer.OnUp(position);
        var geometry = _activeRenderer.GetLastStrokeGeometry();
        if (geometry != null)
        {
            CommitGeometryFill(geometry, EffectiveBrushColor());
            RecordBrushStroke(geometry);
        }
        _activeRenderer.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        _lastCalligraphyPreviewPoint = null;
    }

    private bool ShouldThrottleCalligraphyPreview(WpfPoint position)
    {
        if (_lastCalligraphyPreviewPoint.HasValue)
        {
            var delta = position - _lastCalligraphyPreviewPoint.Value;
            if (delta.Length >= CalligraphyPreviewMinDistance)
            {
                _lastCalligraphyPreviewPoint = position;
                _lastCalligraphyPreviewUtc = DateTime.UtcNow;
                return false;
            }
        }
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastCalligraphyPreviewUtc).TotalMilliseconds >= CalligraphyPreviewMinIntervalMs)
        {
            _lastCalligraphyPreviewUtc = nowUtc;
            _lastCalligraphyPreviewPoint = position;
            return false;
        }
        return true;
    }

    private void BeginEraser(WpfPoint position)
    {
        PushHistory();
        _isErasing = true;
        _lastEraserPoint = position;
        ApplyEraserAt(position);
    }

    private void UpdateEraser(WpfPoint position)
    {
        if (!_isErasing || _lastEraserPoint == null)
        {
            return;
        }
        var last = _lastEraserPoint.Value;
        var distance = (position - last).Length;
        var threshold = Math.Max(1.0, _eraserSize * 0.2);
        if (distance < threshold)
        {
            return;
        }
        var geometry = BuildEraserGeometry(last, position);
        if (geometry != null)
        {
            EraseGeometry(geometry);
        }
        _lastEraserPoint = position;
    }

    private void EndEraser(WpfPoint position)
    {
        if (!_isErasing)
        {
            return;
        }
        if (_lastEraserPoint == null || (_lastEraserPoint.Value - position).Length < 0.5)
        {
            ApplyEraserAt(position);
        }
        _isErasing = false;
        _lastEraserPoint = null;
        UpdateActiveCacheSnapshot();
    }

    private void BeginRegionSelection(WpfPoint position)
    {
        PushHistory();
        _regionStart = position;
        if (_regionRect == null)
        {
            _regionRect = new WpfRectangle
            {
                Stroke = new SolidColorBrush(MediaColor.FromArgb(200, 255, 200, 60)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Fill = new SolidColorBrush(MediaColor.FromArgb(30, 255, 200, 60)),
                IsHitTestVisible = false
            };
            PreviewCanvas.Children.Add(_regionRect);
        }
        UpdateSelectionRect(_regionRect, _regionStart, position);
        _isRegionSelecting = true;
    }

    private void UpdateRegionSelection(WpfPoint position)
    {
        if (_isRegionSelecting && _regionRect != null)
        {
            UpdateSelectionRect(_regionRect, _regionStart, position);
        }
    }

    private void EndRegionSelection(WpfPoint position)
    {
        if (!_isRegionSelecting)
        {
            return;
        }
        _isRegionSelecting = false;
        var region = BuildRegionRect(_regionStart, position);
        ClearRegionSelection();
        if (region.Width > 2 && region.Height > 2)
        {
            EraseRect(region);
        }
        UpdateActiveCacheSnapshot();
    }

    private void BeginShape(WpfPoint position)
    {
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _shapeStart = position;
        _activeShape = CreateShape(_shapeType);
        if (_activeShape == null)
        {
            return;
        }
        ApplyShapeStyle(_activeShape);
        PreviewCanvas.Children.Add(_activeShape);
        UpdateShape(_activeShape, _shapeStart, position);
        _isDrawingShape = true;
    }

    private void UpdateShapePreview(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        UpdateShape(_activeShape, _shapeStart, position);
    }

    private void EndShape(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        var geometry = BuildShapeGeometry(_shapeType, _shapeStart, position);
        if (geometry != null)
        {
            var pen = BuildShapePen();
            CommitGeometryStroke(geometry, pen);
            RecordShapeStroke(geometry, pen);
        }
        ClearShapePreview();
    }

    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (IsBoardActive())
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive)
        {
            ZoomPhoto(e.Delta, e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (_mode != PaintToolMode.Cursor
            && _mode != PaintToolMode.Brush
            && _mode != PaintToolMode.Shape
            && _mode != PaintToolMode.Eraser
            && _mode != PaintToolMode.RegionErase)
        {
            return;
        }
        if (_mode == PaintToolMode.Cursor && _inputPassthroughEnabled)
        {
            return;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        if (_wpsNavHookActive && _wpsHookInterceptWheel)
        {
            var foregroundType = ResolveForegroundPresentationType();
            if (foregroundType == ClassroomToolkit.Interop.Presentation.PresentationType.Wps)
            {
                return;
            }
        }
        if (WpsHookRecentlyFired())
        {
            return;
        }
        var command = e.Delta < 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        if (TrySendPresentationCommand(command))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (TryHandlePhotoKey(e.Key))
        {
            e.Handled = true;
            return;
        }
        if (IsBoardActive() || _photoModeActive)
        {
            return;
        }
        if (TryHandlePresentationKey(e.Key))
        {
            e.Handled = true;
        }
    }

    public bool TryHandlePhotoKey(Key key)
    {
        if (!_photoModeActive || IsBoardActive())
        {
            return false;
        }
        if (key == Key.Escape && _photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            return true;
        }
        if (IsPhotoNavigationKey(key, out var direction))
        {
            if (TryNavigatePdf(direction))
            {
                return true;
            }
            if (!IsAtFileSequenceBoundary(direction))
            {
                PhotoNavigationRequested?.Invoke(direction);
            }
            return true;
        }
        if (key == Key.Add || key == Key.OemPlus)
        {
            ZoomPhotoByFactor(PhotoKeyZoomStep);
            return true;
        }
        if (key == Key.Subtract || key == Key.OemMinus)
        {
            ZoomPhotoByFactor(1.0 / PhotoKeyZoomStep);
            return true;
        }
        return false;
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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

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
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        if (_inputPassthroughEnabled)
        {
            exStyle |= WsExTransparent;
        }
        else
        {
            exStyle &= ~WsExTransparent;
        }
        if (_focusBlocked)
        {
            exStyle |= WsExNoActivate;
        }
        else
        {
            exStyle &= ~WsExNoActivate;
        }
        SetWindowLong(_hwnd, GwlExstyle, exStyle);
    }

    public void UpdateWpsMode(string mode)
    {
        _presentationOptions.Strategy = mode switch
        {
            "raw" => ClassroomToolkit.Interop.Presentation.InputStrategy.Raw,
            "message" => ClassroomToolkit.Interop.Presentation.InputStrategy.Message,
            _ => ClassroomToolkit.Interop.Presentation.InputStrategy.Auto
        };
        _presentationService.ResetWpsAutoFallback();
        _presentationService.ResetOfficeAutoFallback();
        _wpsForceMessageFallback = false;
        _wpsHookUnavailableNotified = false;
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdateWpsWheelMapping(bool enabled)
    {
        _presentationOptions.WheelAsKey = enabled;
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdatePresentationTargets(bool allowOffice, bool allowWps)
    {
        _presentationOptions.AllowOffice = allowOffice;
        _presentationOptions.AllowWps = allowWps;
        if (!allowWps)
        {
            _wpsForceMessageFallback = false;
            _wpsHookUnavailableNotified = false;
        }
        _presentationService.ResetOfficeAutoFallback();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    public void UpdatePresentationForegroundPolicy(bool forceForegroundOnFullscreen)
    {
        _forcePresentationForegroundOnFullscreen = forceForegroundOnFullscreen;
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

    public bool RestorePresentationFocusIfNeeded(bool requireFullscreen = false)
    {
        if (_photoModeActive || IsBoardActive())
        {
            return false;
        }
        if (!IsVisible)
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
        if (!target.IsValid)
        {
            return false;
        }
        if (!_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return false;
        }
        if (requireFullscreen && !IsFullscreenPresentationWindow(target))
        {
            return false;
        }
        var force = ShouldForcePresentationForeground(target);
        if (!force && !IsForegroundOwnedByCurrentProcess())
        {
            return false;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.EnsureForeground(target.Handle);
    }

    public void ForwardKeyboardToPresentation(Key key)
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        // 将键盘按键转换为演示文稿命令
        ClassroomToolkit.Services.Presentation.PresentationCommand? command = null;
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Next;
        }
        else if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        }
        if (command == null)
        {
            return;
        }
        if (TrySendPresentationCommand(command.Value))
        {
        }
    }

    private void UpdatePresentationFocusMonitor()
    {
        var shouldMonitor = IsVisible
            && (_presentationOptions.AllowOffice
                || _presentationOptions.AllowWps
                || (_photoModeActive && _photoFullscreen));
        if (shouldMonitor)
        {
            if (!_presentationFocusMonitor.IsEnabled)
            {
                _presentationFocusMonitor.Start();
            }
            return;
        }
        if (_presentationFocusMonitor.IsEnabled)
        {
            _presentationFocusMonitor.Stop();
        }
    }

    private void DetectForegroundPresentation()
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            _foregroundPresentationActive = false;
            return;
        }
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            _foregroundPresentationActive = false;
            return;
        }
        var type = _presentationClassifier.Classify(target.Info);
        if (type == ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (!IsFullscreenPresentationWindow(target))
        {
            _foregroundPresentationActive = false;
            return;
        }
        if (_foregroundPresentationActive
            && _foregroundPresentationHandle == target.Handle
            && _foregroundPresentationType == type)
        {
            return;
        }
        _foregroundPresentationActive = true;
        _foregroundPresentationHandle = target.Handle;
        _foregroundPresentationType = type;
        PresentationForegroundDetected?.Invoke(type);
    }

    private void DetectForegroundPhoto()
    {
        if (!_photoModeActive || !_photoFullscreen)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_hwnd == IntPtr.Zero)
        {
            _foregroundPhotoActive = false;
            return;
        }
        var foreground = GetForegroundWindow();
        if (foreground != _hwnd)
        {
            _foregroundPhotoActive = false;
            return;
        }
        if (_foregroundPhotoActive)
        {
            return;
        }
        _foregroundPhotoActive = true;
        PhotoForegroundDetected?.Invoke();
    }

    private void MonitorPresentationFocus()
    {
        DetectForegroundPresentation();
        DetectForegroundPhoto();
        if (!_presentationFocusRestoreEnabled)
        {
            return;
        }
        if (_photoModeActive || IsBoardActive())
        {
            return;
        }
        if (DateTime.UtcNow < _nextPresentationFocusAttempt)
        {
            return;
        }
        if (!IsForegroundOwnedByCurrentProcess())
        {
            return;
        }
        var restored = RestorePresentationFocusIfNeeded(requireFullscreen: true);
        if (restored)
        {
            _nextPresentationFocusAttempt = DateTime.UtcNow.AddMilliseconds(PresentationFocusCooldownMs);
        }
    }

    private void MonitorInkContext()
    {
        var monitorStart = Stopwatch.StartNew();
        bool uiThread = Dispatcher.CheckAccess();
        _pendingInkContextCheck = false;

        var allowPresentation = _presentationOptions.AllowOffice || _presentationOptions.AllowWps;
        if (!allowPresentation)
        {
            if (_presentationFullscreenActive)
            {
                _presentationFullscreenActive = false;
                _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
                PresentationFullscreenDetected?.Invoke();
                if (!_photoModeActive && !IsBoardActive())
                {
                    _currentCacheScope = InkCacheScope.None;
                    _currentCacheKey = string.Empty;
                    ClearInkSurfaceState();
                }
            }
        }
        else
        {
            UpdatePresentationFullscreenState(clearInkOnExit: !_photoModeActive && !IsBoardActive());
        }

        if (_photoModeActive || IsBoardActive())
        {
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }
        if (ShouldDeferInkContext())
        {
            _pendingInkContextCheck = true;
            _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
            return;
        }

        UpdateInkMonitorInterval();
        _perfMonitor.Add(monitorStart.Elapsed.TotalMilliseconds, uiThread);
    }

    private void UpdatePresentationFullscreenState(bool clearInkOnExit)
    {
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        var fullscreenNow = target.IsValid && IsFullscreenPresentationWindow(target);
        var nextType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
        if (fullscreenNow && target.Info != null)
        {
            nextType = _presentationClassifier.Classify(target.Info);
        }
        var stateChanged = fullscreenNow != _presentationFullscreenActive;
        _presentationFullscreenActive = fullscreenNow;
        _currentPresentationType = fullscreenNow ? nextType : ClassroomToolkit.Interop.Presentation.PresentationType.None;
        if (!stateChanged)
        {
            return;
        }
        PresentationFullscreenDetected?.Invoke();
        if (!fullscreenNow && clearInkOnExit)
        {
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
        }
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolveForegroundPresentationType()
    {
        var target = _presentationResolver.ResolveForeground();
        if (!target.IsValid || target.Info == null)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.None;
        }
        return _presentationClassifier.Classify(target.Info);
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

    private Rect GetCurrentMonitorRect(bool useWorkArea)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }
        var monitor = MonitorFromWindow(_hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return SystemParameters.WorkArea;
        }
        var info = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return SystemParameters.WorkArea;
        }
        var target = useWorkArea ? info.Work : info.Monitor;
        return new Rect(
            target.Left,
            target.Top,
            Math.Max(1, target.Right - target.Left),
            Math.Max(1, target.Bottom - target.Top));
    }

    private Rect GetCurrentMonitorRectInDip(bool useWorkArea)
    {
        var rect = GetCurrentMonitorRect(useWorkArea);
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
        var rect = GetCurrentMonitorRect(useWorkArea: false);
        SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            (int)Math.Round(rect.Left),
            (int)Math.Round(rect.Top),
            (int)Math.Round(rect.Width),
            (int)Math.Round(rect.Height),
            SwpNoZorder | SwpNoActivate | SwpShowWindow);
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

    private void StartPdfOpenAsync(string sourcePath)
    {
        var token = Interlocked.Increment(ref _photoLoadToken);
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            if (!TryOpenPdfDocumentCore(sourcePath, out var document, out var pageCount))
            {
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _photoLoadToken)
                    {
                        return;
                    }
                    HidePhotoLoadingOverlay();
                    ExitPhotoMode();
                }, DispatcherPriority.Normal);
                if (!scheduled && document != null)
                {
                    document.Dispose();
                }
                return;
            }
            var openedDocument = document!;
            var scheduledApply = TryBeginInvoke(() =>
            {
                if (token != _photoLoadToken
                    || !_photoModeActive
                    || !_photoDocumentIsPdf
                    || !string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    openedDocument.Dispose();
                    return;
                }
                ApplyPdfDocument(openedDocument, pageCount);
                _lastPdfNavigationDirection = 1;
                if (!RenderPdfPage(_currentPageIndex))
                {
                    HidePhotoLoadingOverlay();
                    ExitPhotoMode();
                    return;
                }
                HidePhotoLoadingOverlay();
            }, DispatcherPriority.Render);
            if (!scheduledApply)
            {
                openedDocument.Dispose();
            }
        });
    }

    private static bool TryOpenPdfDocumentCore(string path, out PdfDocumentHost? document, out int pageCount)
    {
        document = null;
        pageCount = 0;
        try
        {
            document = PdfDocumentHost.Open(path);
        }
        catch
        {
            document?.Dispose();
            return false;
        }
        if (document == null)
        {
            return false;
        }
        pageCount = document.PageCount;
        return pageCount > 0;
    }

    private void ApplyPdfDocument(PdfDocumentHost document, int pageCount)
    {
        lock (_pdfRenderLock)
        {
            _pdfDocument = document;
            _pdfPageCount = pageCount;
            _pdfPageCache.Clear();
            _pdfPageOrder.Clear();
            _pdfPinnedPages.Clear();
            Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfPrefetchToken);
            Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        }
    }

    private void ClosePdfDocument()
    {
        lock (_pdfRenderLock)
        {
            _pdfDocument?.Dispose();
            _pdfDocument = null;
            _pdfPageCount = 0;
            _pdfPageCache.Clear();
            _pdfPageOrder.Clear();
            _pdfPinnedPages.Clear();
            Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfPrefetchToken);
            Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        }
    }

    private bool RenderPdfPage(int pageIndex)
    {
        var bitmap = GetPdfPageBitmap(pageIndex);
        if (bitmap == null)
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
        PhotoBackground.Source = bitmap;
        PhotoBackground.Visibility = Visibility.Visible;
        SchedulePdfPrefetch(pageIndex, _lastPdfNavigationDirection);
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

    private bool TryGetCachedPdfPageBitmap(int pageIndex, out BitmapSource? bitmap)
    {
        bitmap = null;
        if (!Monitor.TryEnter(_pdfRenderLock, 2))
        {
            return false;
        }
        try
        {
            if (_pdfDocument == null || _pdfPageCount <= 0)
            {
                return false;
            }
            var safeIndex = Math.Clamp(pageIndex, 1, _pdfPageCount);
            if (!_pdfPageCache.TryGetValue(safeIndex, out var cached))
            {
                return false;
            }
            TouchPdfCacheUnsafe(safeIndex);
            bitmap = cached;
            return true;
        }
        finally
        {
            Monitor.Exit(_pdfRenderLock);
        }
    }

    private bool TryGetPdfPageSize(int pageIndex, out System.Windows.Size size)
    {
        size = default;
        if (_pdfDocument == null)
        {
            return false;
        }
        if (!_pdfDocument.TryGetPageSize(pageIndex, out var sizeF))
        {
            return false;
        }
        size = new System.Windows.Size(sizeF.Width * 96.0 / 72.0, sizeF.Height * 96.0 / 72.0);
        return size.Width > 0 && size.Height > 0;
    }

    private double GetScaledPdfPageHeight(int pageIndex)
    {
        if (!_photoDocumentIsPdf)
        {
            return 0;
        }
        if (TryGetPdfPageSize(pageIndex, out var size))
        {
            return size.Height * _photoScale.ScaleY;
        }
        if (TryGetCachedPdfPageBitmap(pageIndex, out var cached))
        {
            return GetScaledPageHeight(cached);
        }
        return 0;
    }

    private BitmapSource? GetPdfPageBitmap(int pageIndex)
    {
        lock (_pdfRenderLock)
        {
            if (_pdfDocument == null || _pdfPageCount <= 0)
            {
                return null;
            }
            var safeIndex = Math.Clamp(pageIndex, 1, _pdfPageCount);
            if (_pdfPageCache.TryGetValue(safeIndex, out var cached))
            {
                TouchPdfCacheUnsafe(safeIndex);
                return cached;
            }
            var rendered = _pdfDocument.RenderPage(safeIndex, PdfDefaultDpi);
            if (rendered == null)
            {
                return null;
            }
            _pdfPageCache[safeIndex] = rendered;
            TouchPdfCacheUnsafe(safeIndex);
            TrimPdfCacheUnsafe();
            return rendered;
        }
    }

    private void TouchPdfCache(int pageIndex)
    {
        lock (_pdfRenderLock)
        {
            TouchPdfCacheUnsafe(pageIndex);
        }
    }

    private void TouchPdfCacheUnsafe(int pageIndex)
    {
        var node = _pdfPageOrder.Find(pageIndex);
        if (node != null)
        {
            _pdfPageOrder.Remove(node);
        }
        _pdfPageOrder.AddLast(pageIndex);
    }

    private void TrimPdfCacheUnsafe()
    {
        while (_pdfPageOrder.Count > PdfCacheLimit)
        {
            var node = _pdfPageOrder.First;
            while (node != null && _pdfPinnedPages.Contains(node.Value))
            {
                node = node.Next;
            }
            if (node == null)
            {
                break;
            }
            _pdfPageOrder.Remove(node);
            _pdfPageCache.Remove(node.Value);
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

    private bool TryNavigatePdf(int direction)
    {
        if (!_photoModeActive || !_photoDocumentIsPdf || _pdfDocument == null)
        {
            return false;
        }
        var next = _currentPageIndex + direction;
        if (next < 1 || next > _pdfPageCount)
        {
            return false;  // PDF到边界时返回false，允许跳转到序列中的下一个文件
        }
        SaveCurrentPageOnNavigate(forceBackground: false);
        _currentPageIndex = next;
        _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, isPdf: true);
        ResetInkHistory();
        LoadCurrentPageIfExists();
        _lastPdfNavigationDirection = direction >= 0 ? 1 : -1;
        RenderPdfPage(_currentPageIndex);
        if (_crossPageDisplayEnabled)
        {
            UpdateCrossPageDisplay();
        }
        return true;
    }

    private void SchedulePdfPrefetch(int pageIndex, int direction)
    {
        if (!_photoDocumentIsPdf || _pdfDocument == null || _pdfPageCount <= 0)
        {
            return;
        }
        if (Interlocked.Exchange(ref _pdfPrefetchInFlight, 1) == 1)
        {
            Interlocked.Increment(ref _pdfPrefetchToken);
            return;
        }
        var token = _pdfPrefetchToken;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var delay = _crossPageDisplayEnabled ? 0 : 120;
                if (delay > 0)
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                if (token != _pdfPrefetchToken || !_photoModeActive || !_photoDocumentIsPdf)
                {
                    return;
                }
                PrefetchPdfNeighbors(pageIndex, direction, token);
            }
            finally
            {
                Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            }
        });
    }

    private void SchedulePdfVisiblePrefetch(IReadOnlyList<int> pageIndexes)
    {
        if (!_photoDocumentIsPdf || _pdfDocument == null || _pdfPageCount <= 0)
        {
            return;
        }
        if (pageIndexes == null || pageIndexes.Count == 0)
        {
            return;
        }
        var unique = pageIndexes
            .Where(p => p >= 1 && p <= _pdfPageCount)
            .Distinct()
            .ToArray();
        if (unique.Length == 0)
        {
            return;
        }
        var token = Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        if (Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 1) == 1)
        {
            return;
        }
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                foreach (var pageIndex in unique)
                {
                    if (token != _pdfVisiblePrefetchToken)
                    {
                        return;
                    }
                    if (TryGetCachedPdfPageBitmap(pageIndex, out _))
                    {
                        continue;
                    }
                    GetPdfPageBitmap(pageIndex);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
                if (token == _pdfVisiblePrefetchToken)
                {
                    TryBeginInvoke(() =>
                    {
                        if (_photoModeActive && _photoDocumentIsPdf && _crossPageDisplayEnabled)
                        {
                            UpdateCrossPageDisplay();
                        }
                    }, DispatcherPriority.Background);
                }
            }
        });
    }

    private void PrefetchPdfNeighbors(int pageIndex, int direction, int token)
    {
        var next = pageIndex + 1;
        var prev = pageIndex - 1;
        if (direction < 0)
        {
            if (!PrefetchPdfPage(prev, token))
            {
                PrefetchPdfPage(next, token);
            }
            return;
        }
        if (!PrefetchPdfPage(next, token))
        {
            PrefetchPdfPage(prev, token);
        }
    }

    private bool PrefetchPdfPage(int pageIndex, int token)
    {
        if (pageIndex < 1 || pageIndex > _pdfPageCount)
        {
            return false;
        }
        if (TryGetCachedPdfPageBitmap(pageIndex, out _))
        {
            return true;
        }
        if (!Monitor.TryEnter(_pdfRenderLock, 30))
        {
            return false;
        }
        try
        {
            if (token != _pdfPrefetchToken || _pdfDocument == null || _pdfPageCount <= 0)
            {
                return false;
            }
            if (_pdfPageCache.ContainsKey(pageIndex))
            {
                TouchPdfCacheUnsafe(pageIndex);
                return true;
            }
            var rendered = _pdfDocument.RenderPage(pageIndex, PdfDefaultDpi);
            if (rendered == null)
            {
                return false;
            }
            _pdfPageCache[pageIndex] = rendered;
            TouchPdfCacheUnsafe(pageIndex);
            TrimPdfCacheUnsafe();
        }
        finally
        {
            Monitor.Exit(_pdfRenderLock);
        }
        return true;
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

    private bool IsForegroundOwnedByCurrentProcess()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }
        GetWindowThreadProcessId(foreground, out var processId);
        return processId == _currentProcessId;
    }

    private bool ShouldForcePresentationForeground(
        ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!_forcePresentationForegroundOnFullscreen || !target.IsValid)
        {
            return false;
        }
        return IsFullscreenPresentationWindow(target);
    }

    private bool IsFullscreenPresentationWindow(
        ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }
        if (!_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return false;
        }
        return IsFullscreenWindow(target.Handle);
    }

    private static bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        var style = GetWindowLong(hwnd, GwlStyle);
        if ((style & WsCaption) != 0)
        {
            return false;
        }
        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }
        var info = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }
        const int tolerance = 2;
        return Math.Abs(rect.Left - info.Monitor.Left) <= tolerance
               && Math.Abs(rect.Top - info.Monitor.Top) <= tolerance
               && Math.Abs(rect.Right - info.Monitor.Right) <= tolerance
               && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= tolerance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private void OnWpsNavHookRequested(int direction, string source)
    {
        if (!_presentationOptions.AllowWps)
        {
            return;
        }
        MarkWpsHookInput();
        if (IsBoardActive() || direction == 0)
        {
            return;
        }
        var target = ResolveWpsTarget();
        if (!target.IsValid)
        {
            return;
        }
        var passthrough = IsWpsRawInputPassthrough(target);
        var interceptSource = source == "wheel" ? _wpsHookInterceptWheel : _wpsHookInterceptKeyboard;
        if (passthrough && !interceptSource)
        {
            return;
        }
        if (ShouldSuppressWpsNav(direction, target.Handle))
        {
            return;
        }
        var command = direction > 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        var options = BuildWpsOptions(source);
        if (_presentationService.TrySendToTarget(target, command, options))
        {
            RememberWpsNav(direction, target.Handle);
        }
    }

    private bool TrySendWpsNavigation(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        if (!_presentationOptions.AllowWps)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        var target = ResolveWpsTarget();
        if (!target.IsValid)
        {
            return false;
        }
        return TrySendWpsNavigation(command, target, allowBackground: false);
    }

    private bool TrySendWpsNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        ClassroomToolkit.Interop.Presentation.PresentationTarget target,
        bool allowBackground)
    {
        if (!_presentationOptions.AllowWps)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (!IsPresentationSlideshow(target))
        {
            return false;
        }
        if (!allowBackground && !IsTargetForeground(target))
        {
            return false;
        }
        var direction = command == ClassroomToolkit.Services.Presentation.PresentationCommand.Next ? 1 : -1;
        if (ShouldSuppressWpsNav(direction, target.Handle))
        {
            return false;
        }
        var options = BuildWpsOptions("wheel");
        var sent = _presentationService.TrySendToTarget(target, command, options);
        if (sent)
        {
            RememberWpsNav(direction, target.Handle);
        }
        return sent;
    }

    private bool TryHandlePresentationKey(Key key)
    {
        ClassroomToolkit.Services.Presentation.PresentationCommand? command = null;
        if (key == Key.Right || key == Key.Down || key == Key.Space || key == Key.Enter || key == Key.PageDown)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Next;
        }
        else if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            command = ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        }
        if (command == null)
        {
            return false;
        }
        if (!TrySendPresentationCommand(command.Value))
        {
            return false;
        }
        return true;
    }

    private bool TrySendPresentationCommand(ClassroomToolkit.Services.Presentation.PresentationCommand command)
    {
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return false;
        }
        var wpsTarget = _presentationOptions.AllowWps
            ? ResolveWpsTarget()
            : ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        var officeTarget = _presentationOptions.AllowOffice
            ? _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId)
            : ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        var wpsSlideshow = IsPresentationSlideshow(wpsTarget);
        var officeSlideshow = IsPresentationSlideshow(officeTarget);
        var foreground = ResolveForegroundPresentationType();

        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Wps
            && wpsSlideshow
            && TrySendWpsNavigation(command, wpsTarget, allowBackground: false))
        {
            return true;
        }
        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Office
            && officeSlideshow
            && TrySendOfficeNavigation(command, officeTarget, allowBackground: false))
        {
            return true;
        }

        if (wpsSlideshow && officeSlideshow)
        {
            if (_currentPresentationType == ClassroomToolkit.Interop.Presentation.PresentationType.Wps
                && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
            {
                return true;
            }
            if (_currentPresentationType == ClassroomToolkit.Interop.Presentation.PresentationType.Office
                && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
            {
                return true;
            }
            var wpsFullscreen = IsFullscreenWindow(wpsTarget.Handle);
            var officeFullscreen = IsFullscreenWindow(officeTarget.Handle);
            if (wpsFullscreen && !officeFullscreen
                && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
            {
                return true;
            }
            if (officeFullscreen && !wpsFullscreen
                && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
            {
                return true;
            }
        }

        if (wpsSlideshow && TrySendWpsNavigation(command, wpsTarget, allowBackground: true))
        {
            return true;
        }
        if (officeSlideshow && TrySendOfficeNavigation(command, officeTarget, allowBackground: true))
        {
            return true;
        }
        return false;
    }

    private bool TrySendOfficeNavigation(
        ClassroomToolkit.Services.Presentation.PresentationCommand command,
        ClassroomToolkit.Interop.Presentation.PresentationTarget target,
        bool allowBackground)
    {
        if (!_presentationOptions.AllowOffice)
        {
            return false;
        }
        if (IsBoardActive())
        {
            return false;
        }
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (!IsPresentationSlideshow(target))
        {
            return false;
        }
        if (!allowBackground && !IsTargetForeground(target))
        {
            return false;
        }
        var options = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = _presentationOptions.Strategy,
            WheelAsKey = _presentationOptions.WheelAsKey,
            AllowOffice = true,
            AllowWps = false
        };
        return _presentationService.TrySendToTarget(target, command, options);
    }

    private bool IsPresentationSlideshow(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }
        if (_presentationClassifier.IsSlideshowWindow(target.Info))
        {
            return true;
        }
        return IsFullscreenWindow(target.Handle);
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolvePreferredPresentationType()
    {
        var foreground = ResolveForegroundPresentationType();
        if (foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Office
            || foreground == ClassroomToolkit.Interop.Presentation.PresentationType.Wps)
        {
            return foreground;
        }
        var fullscreen = ResolveFullscreenPresentationType();
        if (fullscreen != ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            return fullscreen;
        }
        return _currentPresentationType;
    }

    private ClassroomToolkit.Interop.Presentation.PresentationType ResolveFullscreenPresentationType()
    {
        bool wpsFullscreen = false;
        bool officeFullscreen = false;
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            wpsFullscreen = IsFullscreenPresentationWindow(wpsTarget);
        }
        if (_presentationOptions.AllowOffice)
        {
            var officeTarget = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                allowWps: false,
                allowOffice: true,
                _currentProcessId);
            officeFullscreen = IsFullscreenPresentationWindow(officeTarget);
        }
        if (wpsFullscreen && !officeFullscreen)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.Wps;
        }
        if (officeFullscreen && !wpsFullscreen)
        {
            return ClassroomToolkit.Interop.Presentation.PresentationType.Office;
        }
        if (wpsFullscreen && officeFullscreen
            && _currentPresentationType != ClassroomToolkit.Interop.Presentation.PresentationType.None)
        {
            return _currentPresentationType;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationType.None;
    }

    private ClassroomToolkit.Services.Presentation.PresentationControlOptions BuildWpsOptions(string? source = null)
    {
        var strategy = _presentationOptions.Strategy;
        if (_wpsForceMessageFallback)
        {
            strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        if (string.Equals(source, "wheel", StringComparison.OrdinalIgnoreCase) && _presentationOptions.WheelAsKey)
        {
            strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        return new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = strategy,
            WheelAsKey = _presentationOptions.WheelAsKey,
            AllowOffice = false,
            AllowWps = true
        };
    }

    private void UpdateWpsNavHookState()
    {
        if (_wpsNavHook == null || !_wpsNavHook.Available)
        {
            _wpsNavHookActive = false;
            if (_presentationOptions.AllowWps)
            {
                var hookTarget = ResolveWpsTarget();
                MarkWpsHookUnavailable(hookTarget.IsValid);
            }
            return;
        }
        _wpsForceMessageFallback = false;
        var shouldEnable = _presentationOptions.AllowWps && !IsBoardActive() && IsVisible && !_photoModeActive;
        var blockOnly = false;
        var interceptKeyboard = true;
        var interceptWheel = true;
        var emitWheelOnBlock = true;
        var target = ClassroomToolkit.Interop.Presentation.PresentationTarget.Empty;
        if (shouldEnable)
        {
            target = ResolveWpsTarget();
            shouldEnable = target.IsValid;
        }
        var sendMode = ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        var wheelForward = false;
        if (shouldEnable)
        {
            sendMode = ResolveWpsSendMode(target);
            wheelForward = _presentationOptions.WheelAsKey;
            interceptWheel = wheelForward;
            emitWheelOnBlock = wheelForward;
        }
        
        // 光标模式下，直接禁用钩子拦截，让输入直接传递到 WPS
        if (_mode == PaintToolMode.Cursor)
        {
            interceptKeyboard = false;
            interceptWheel = false;
        }
        else if (shouldEnable && !IsTargetForeground(target))
        {
            interceptKeyboard = false;
            interceptWheel = false;
            emitWheelOnBlock = false;
        }
        else if (shouldEnable && sendMode == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
        {
            blockOnly = true;
            if (IsTargetForeground(target))
            {
                if (!wheelForward)
                {
                    blockOnly = false;
                    emitWheelOnBlock = false;
                }
            }
        }
        
        if (shouldEnable)
        {
            _wpsNavHook.SetInterceptEnabled(true);
            _wpsNavHook.SetBlockOnly(blockOnly);
            _wpsNavHook.SetInterceptKeyboard(interceptKeyboard);
            _wpsNavHook.SetInterceptWheel(interceptWheel);
            _wpsNavHook.SetEmitWheelOnBlock(emitWheelOnBlock);
            _wpsHookInterceptKeyboard = interceptKeyboard;
            _wpsHookInterceptWheel = interceptWheel;
            if (!_wpsNavHookActive)
            {
                _wpsNavHookActive = _wpsNavHook.Start();
            }
            if (!_wpsNavHookActive)
            {
                StopWpsNavHook();
                MarkWpsHookUnavailable(target.IsValid);
            }
            else
            {
                _wpsForceMessageFallback = false;
            }
            return;
        }
        StopWpsNavHook();
    }

    private void StopWpsNavHook()
    {
        if (_wpsNavHook == null)
        {
            return;
        }
        _wpsNavHook.SetInterceptEnabled(false);
        _wpsNavHook.SetBlockOnly(false);
        _wpsNavHook.SetInterceptKeyboard(true);
        _wpsNavHook.SetInterceptWheel(true);
        _wpsNavHook.SetEmitWheelOnBlock(true);
        _wpsNavHook.Stop();
        _wpsNavHookActive = false;
        _wpsHookInterceptKeyboard = true;
        _wpsHookInterceptWheel = true;
    }

    private ClassroomToolkit.Interop.Presentation.PresentationTarget ResolveWpsTarget()
    {
        return _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            allowWps: true,
            allowOffice: false,
            (uint)Environment.ProcessId);
    }

    private ClassroomToolkit.Interop.Presentation.InputStrategy ResolveWpsSendMode(
        ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (_wpsForceMessageFallback)
        {
            return ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        var mode = _presentationOptions.Strategy;
        if (mode == ClassroomToolkit.Interop.Presentation.InputStrategy.Auto)
        {
            if (_presentationService.IsWpsAutoForcedMessage)
            {
                return ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
            }
            return target.IsValid
                ? ClassroomToolkit.Interop.Presentation.InputStrategy.Raw
                : ClassroomToolkit.Interop.Presentation.InputStrategy.Message;
        }
        return mode;
    }

    private void MarkWpsHookUnavailable(bool notify)
    {
        _wpsForceMessageFallback = true;
        if (notify)
        {
            NotifyWpsHookUnavailable();
        }
    }

    private void NotifyWpsHookUnavailable()
    {
        if (_wpsHookUnavailableNotified)
        {
            return;
        }
        _wpsHookUnavailableNotified = true;
        Dispatcher.BeginInvoke(() =>
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            var message = "检测到 WPS 放映全局钩子不可用，已自动切换为消息投递模式。";
            System.Windows.MessageBox.Show(owner ?? this, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private bool IsWpsRawInputPassthrough(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (ResolveWpsSendMode(target) != ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
        {
            return false;
        }
        return IsTargetForeground(target);
    }

    private bool IsTargetForeground(ClassroomToolkit.Interop.Presentation.PresentationTarget target)
    {
        if (!target.IsValid)
        {
            return false;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.IsForeground(target.Handle);
    }

    private bool ShouldSuppressWpsNav(int direction, IntPtr target)
    {
        if (target == IntPtr.Zero)
        {
            return false;
        }
        if (_wpsNavBlockUntil > DateTime.UtcNow)
        {
            return true;
        }
        if (_lastWpsNavEvent.HasValue)
        {
            var last = _lastWpsNavEvent.Value;
            if (last.Code == direction && last.Target == target)
            {
                var elapsed = DateTime.UtcNow - last.Timestamp;
                if (elapsed.TotalMilliseconds < WpsNavDebounceMs)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void RememberWpsNav(int direction, IntPtr target)
    {
        _lastWpsNavEvent = (direction, target, DateTime.UtcNow);
        _wpsNavBlockUntil = DateTime.UtcNow.AddMilliseconds(WpsNavDebounceMs);
    }

    private void MarkWpsHookInput()
    {
        _lastWpsHookInput = DateTime.UtcNow;
    }

    private bool WpsHookRecentlyFired()
    {
        if (_lastWpsHookInput == DateTime.MinValue)
        {
            return false;
        }
        return (DateTime.UtcNow - _lastWpsHookInput).TotalMilliseconds < WpsNavDebounceMs;
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

    private Shape? CreateShape(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.None => null,
            PaintShapeType.Line => new Line(),
            PaintShapeType.DashedLine => new Line(),
            PaintShapeType.Rectangle => new System.Windows.Shapes.Rectangle(),
            PaintShapeType.RectangleFill => new System.Windows.Shapes.Rectangle(),
            PaintShapeType.Ellipse => new Ellipse(),
            PaintShapeType.Path => new WpfPath(),
            _ => null
        };
    }

    private void ApplyShapeStyle(Shape shape)
    {
        var stroke = new SolidColorBrush(EffectiveBrushColor());
        stroke.Freeze();
        shape.Stroke = stroke;
        shape.StrokeThickness = Math.Max(1, _brushSize);
        shape.StrokeStartLineCap = PenLineCap.Round;
        shape.StrokeEndLineCap = PenLineCap.Round;
        shape.StrokeLineJoin = PenLineJoin.Round;
        if (_shapeType == PaintShapeType.DashedLine)
        {
            shape.StrokeDashArray = new DoubleCollection { 6, 4 };
        }
        shape.Fill = null;
        shape.IsHitTestVisible = false;
    }

    private static void UpdateShape(Shape shape, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (shape is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
        }
        else
        {
            System.Windows.Controls.Canvas.SetLeft(shape, left);
            System.Windows.Controls.Canvas.SetTop(shape, top);
            shape.Width = Math.Max(1, width);
            shape.Height = Math.Max(1, height);
        }
    }

    private void ClearShapePreview()
    {
        if (_activeShape != null)
        {
            PreviewCanvas.Children.Remove(_activeShape);
            _activeShape = null;
        }
        _isDrawingShape = false;
    }

    private Geometry? BuildShapeGeometry(PaintShapeType type, WpfPoint start, WpfPoint end)
    {
        var rect = new Rect(start, end);
        return type switch
        {
            PaintShapeType.Line => new LineGeometry(start, end),
            PaintShapeType.DashedLine => new LineGeometry(start, end),
            PaintShapeType.Rectangle => new RectangleGeometry(rect),
            PaintShapeType.RectangleFill => new RectangleGeometry(rect),
            PaintShapeType.Ellipse => new EllipseGeometry(rect),
            _ => null
        };
    }

    private MediaPen BuildShapePen()
    {
        var brush = new SolidColorBrush(EffectiveBrushColor());
        brush.Freeze();
        var pen = new MediaPen(brush, Math.Max(1.0, _brushSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (_shapeType == PaintShapeType.DashedLine)
        {
            pen.DashStyle = new DashStyle(new double[] { 6, 4 }, 0);
            pen.DashCap = PenLineCap.Round;
        }
        pen.Freeze();
        return pen;
    }

    private Geometry? BuildEraserGeometry(WpfPoint start, WpfPoint end)
    {
        var radius = Math.Max(2.0, _eraserSize * 0.5);
        var delta = end - start;
        if (delta.Length < 0.5)
        {
            return new EllipseGeometry(start, radius, radius);
        }
        var path = new StreamGeometry();
        using (var ctx = path.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.LineTo(end, isStroked: true, isSmoothJoin: true);
        }
        var pen = new MediaPen(MediaBrushes.Black, Math.Max(1.0, _eraserSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        return path.GetWidenedPathGeometry(pen);
    }

    private void ApplyEraserAt(WpfPoint position)
    {
        var radius = Math.Max(2.0, _eraserSize * 0.5);
        var geometry = new EllipseGeometry(position, radius, radius);
        EraseGeometry(geometry);
    }

    private void EraseRect(Rect region)
    {
        if (_photoModeActive)
        {
            EraseGeometry(new RectangleGeometry(region));
            return;
        }
        var eraseGeometry = new RectangleGeometry(region);
        ApplyInkErase(eraseGeometry);
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new Int32Rect(
            (int)Math.Floor(region.X * dpi.DpiScaleX),
            (int)Math.Floor(region.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(region.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(region.Height * dpi.DpiScaleY));
        rect = IntersectRects(rect, new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var stride = rect.Width * 4;
        var clear = new byte[stride * rect.Height];
        _rasterSurface.WritePixels(rect, clear, stride, 0);
        _hasDrawing = true;
    }

    private void ClearRegionSelection()
    {
        if (_regionRect != null)
        {
            PreviewCanvas.Children.Remove(_regionRect);
            _regionRect = null;
        }
        _isRegionSelecting = false;
    }

    private void RecordBrushStroke(Geometry geometry)
    {
        if (!_inkRecordEnabled || geometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = _brushStyle,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            MaskSeed = _inkSeedRandom.Next(),
            CalligraphyInkBloomEnabled = _calligraphyInkBloomEnabled,
            CalligraphySealEnabled = _calligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = _calligraphyOverlayOpacityThreshold
        };
        if (_brushStyle == PaintBrushStyle.Calligraphy && _activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
        {
            var core = calligraphyRenderer.GetLastCoreGeometry();
            var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
            var strokeGeometry = core ?? geometry;
            if (ribbons != null && ribbons.Count > 0)
            {
                var union = UnionGeometries(ribbons.Select(item => item.Geometry).ToList());
                if (union != null)
                {
                    strokeGeometry = union;
                }
            }
            var storeGeometry = _photoModeActive ? ToPhotoGeometry(strokeGeometry) : strokeGeometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
            stroke.InkFlow = calligraphyRenderer.LastInkFlow;
            stroke.StrokeDirectionX = calligraphyRenderer.LastStrokeDirection.X;
            stroke.StrokeDirectionY = calligraphyRenderer.LastStrokeDirection.Y;
            var blooms = calligraphyRenderer.GetInkBloomGeometries();
            if (blooms != null)
            {
                foreach (var bloom in blooms)
                {
                    var bloomGeometry = _photoModeActive ? ToPhotoGeometry(bloom.Geometry) : bloom.Geometry;
                    if (bloomGeometry == null)
                    {
                        continue;
                    }
                    stroke.Blooms.Add(new InkBloomData
                    {
                        GeometryPath = InkGeometrySerializer.Serialize(bloomGeometry),
                        Opacity = bloom.Opacity
                    });
                }
            }
        }
        else
        {
            var storeGeometry = _photoModeActive ? ToPhotoGeometry(geometry) : geometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        CommitStroke(stroke);
    }

    private void RecordShapeStroke(Geometry geometry, MediaPen pen)
    {
        if (!_inkRecordEnabled || geometry == null || pen == null)
        {
            return;
        }
        var widened = geometry.GetWidenedPathGeometry(pen);
        if (widened == null || widened.Bounds.IsEmpty)
        {
            return;
        }
        var storeGeometry = _photoModeActive ? ToPhotoGeometry(widened) : widened;
        if (storeGeometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Shape,
            BrushStyle = PaintBrushStyle.StandardRibbon,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            MaskSeed = _inkSeedRandom.Next(),
            GeometryPath = InkGeometrySerializer.Serialize(storeGeometry)
        };
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        CommitStroke(stroke);
    }

    private void ApplyInkErase(Geometry geometry)
    {
        if (!_inkRecordEnabled || _inkStrokes.Count == 0 || geometry == null)
        {
            return;
        }
        var eraseGeometry = _photoModeActive ? ToPhotoGeometry(geometry) : geometry;
        if (eraseGeometry == null)
        {
            return;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var updatedPath = ExcludeGeometry(stroke.GeometryPath, eraseGeometry);
            if (updatedPath == null)
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(updatedPath))
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }
            stroke.GeometryPath = updatedPath;
            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometry(bloom.GeometryPath, eraseGeometry);
                    if (string.IsNullOrWhiteSpace(bloomUpdated))
                    {
                        stroke.Blooms.RemoveAt(j);
                        changed = true;
                        continue;
                    }
                    bloom.GeometryPath = bloomUpdated;
                }
            }
            changed = true;
        }
        if (changed)
        {
            MarkInkCacheDirty();
        }
    }

    private void CaptureStrokeContext()
    {
    }

    private void CommitStroke(InkStrokeData stroke)
    {
        _inkStrokes.Add(stroke);
        if (_currentCacheScope == InkCacheScope.Photo)
        {
            MarkInkCacheDirty();
            UpdateActiveCacheSnapshot();
        }
    }

    private void UpdateActiveCacheSnapshot()
    {
        if (!_inkCacheEnabled)
        {
            return;
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        var cacheKey = _currentCacheKey;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            return;
        }
        _photoCache.Set(cacheKey, strokes);
    }


    private int GetCommittedStrokeCount()
    {
        return _inkStrokes.Count;
    }

    private List<InkStrokeData> CloneCommittedInkStrokes()
    {
        return CloneInkStrokes(_inkStrokes);
    }

    private void RedrawInkSurface()
    {
        var redrawSw = Stopwatch.StartNew();
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        if (_inkStrokes.Count == 0)
        {
            if (!_hasDrawing)
            {
                _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
                return;
            }
            ClearSurface();
            _hasDrawing = false;
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        ClearSurface();
        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke);
        }
        _hasDrawing = _inkStrokes.Count > 0;
        _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void RequestInkRedraw()
    {
        if (_inkStrokes.Count == 0 && !_hasDrawing)
        {
            return;
        }
        if (_redrawPending)
        {
            return;
        }
        var throttleActive = _photoModeActive && (_photoPanning || _crossPageDragging);
        var elapsedMs = (DateTime.UtcNow - _lastInkRedrawUtc).TotalMilliseconds;
        if (throttleActive && elapsedMs < InkRedrawMinIntervalMs)
        {
            _redrawPending = true;
            var token = Interlocked.Increment(ref _inkRedrawToken);
            var delay = Math.Max(1, (int)Math.Ceiling(InkRedrawMinIntervalMs - elapsedMs));
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
                    if (token != _inkRedrawToken)
                    {
                        return;
                    }
                    _redrawPending = false;
                    if (_redrawInProgress)
                    {
                        return;
                    }
                    _redrawInProgress = true;
                    try
                    {
                        _lastInkRedrawUtc = DateTime.UtcNow;
                        RedrawInkSurface();
                    }
                    finally
                    {
                        _redrawInProgress = false;
                    }
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    _redrawPending = false;
                }
            });
            return;
        }
        _redrawPending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            _redrawPending = false;
            if (_redrawInProgress)
            {
                return;
            }
            _redrawInProgress = true;
            try
            {
                _lastInkRedrawUtc = DateTime.UtcNow;
                RedrawInkSurface();
            }
            finally
            {
                _redrawInProgress = false;
            }
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _redrawPending = false;
        }
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

    private void ClearInkSurfaceState()
    {
        _inkStrokes.Clear();
        ResetInkHistory();
        RedrawInkSurface();
        _inkCacheDirty = false;
    }

    private void SaveAndClearInkSurface()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
        ClearInkSurfaceState();
    }

    private void RenderStoredStroke(InkStrokeData stroke)
    {
        var geometry = InkGeometrySerializer.Deserialize(stroke.GeometryPath);
        if (geometry == null)
        {
            return;
        }
        var renderGeometry = _photoModeActive ? ToScreenGeometry(geometry) : geometry;
        if (renderGeometry == null)
        {
            return;
        }
        var color = (MediaColor)MediaColorConverter.ConvertFromString(stroke.ColorHex);
        color.A = stroke.Opacity;
        if (stroke.Type == InkStrokeType.Shape || stroke.BrushStyle != PaintBrushStyle.Calligraphy)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            RenderAndBlend(renderGeometry, brush, null, erase: false, null);
            return;
        }
        var inkFlow = stroke.InkFlow;
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        List<(Geometry Geometry, double Opacity)>? blooms = null;
        if (stroke.CalligraphyInkBloomEnabled && stroke.Blooms.Count > 0 && !suppressOverlays)
        {
            blooms = new List<(Geometry Geometry, double Opacity)>();
            foreach (var bloom in stroke.Blooms)
            {
                var bloomGeometry = InkGeometrySerializer.Deserialize(bloom.GeometryPath);
                if (bloomGeometry == null)
                {
                    continue;
                }
                var renderBloom = _photoModeActive ? ToScreenGeometry(bloomGeometry) : bloomGeometry;
                if (renderBloom == null)
                {
                    continue;
                }
                blooms.Add((renderBloom, bloom.Opacity));
            }
        }
        RenderCalligraphyComposite(
            renderGeometry,
            color,
            stroke.BrushSize,
            inkFlow,
            strokeDirection,
            stroke.CalligraphySealEnabled,
            stroke.CalligraphyInkBloomEnabled,
            blooms,
            suppressOverlays,
            stroke.MaskSeed);
    }

    private void RenderStoredInkLayers(
        Geometry geometry,
        MediaColor color,
        double inkFlow,
        double ribbonOpacity,
        Vector strokeDirection,
        double brushSize,
        int maskSeed)
    {
        var solidBrush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(ribbonOpacity, 0.1, 1.0)
        };
        solidBrush.Freeze();
        var mask = IsInkMaskEligible(geometry, brushSize)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed)
            : null;
        RenderAndBlend(geometry, solidBrush, null, erase: false, mask);
    }

    private void RenderStoredInkCore(Geometry geometry, MediaColor color, double brushSize, bool sealEnabled)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        RenderAndBlend(geometry, brush, null, erase: false, null);
        if (!sealEnabled)
        {
            return;
        }
        double sealWidth = Math.Max(brushSize * CalligraphySealStrokeWidthFactor, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var pen = new MediaPen(brush, sealWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void RenderStoredInkEdge(
        Geometry geometry,
        MediaColor color,
        double inkFlow,
        Vector strokeDirection,
        double brushSize,
        int maskSeed)
    {
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var pen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        var mask = IsInkMaskEligible(geometry, brushSize)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed)
            : null;
        RenderAndBlend(geometry, null, pen, erase: false, mask);
    }

    private static string? ExcludeGeometry(string geometryPath, Geometry eraser)
    {
        if (string.IsNullOrWhiteSpace(geometryPath))
        {
            return null;
        }
        var geometry = InkGeometrySerializer.Deserialize(geometryPath);
        if (geometry == null)
        {
            return null;
        }
        if (!geometry.Bounds.IntersectsWith(eraser.Bounds))
        {
            return geometryPath;
        }
        var combined = Geometry.Combine(geometry, eraser, GeometryCombineMode.Exclude, null);
        if (combined == null || combined.Bounds.IsEmpty)
        {
            return string.Empty;
        }
        return InkGeometrySerializer.Serialize(combined);
    }

    private static List<InkStrokeData> CloneInkStrokes(IEnumerable<InkStrokeData> source)
    {
        return source.Select(stroke => new InkStrokeData
        {
            Type = stroke.Type,
            BrushStyle = stroke.BrushStyle,
            GeometryPath = stroke.GeometryPath,
            ColorHex = stroke.ColorHex,
            Opacity = stroke.Opacity,
            BrushSize = stroke.BrushSize,
            MaskSeed = stroke.MaskSeed,
            InkFlow = stroke.InkFlow,
            StrokeDirectionX = stroke.StrokeDirectionX,
            StrokeDirectionY = stroke.StrokeDirectionY,
            CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
            CalligraphySealEnabled = stroke.CalligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold,
            Blooms = stroke.Blooms.Select(bloom => new InkBloomData
            {
                GeometryPath = bloom.GeometryPath,
                Opacity = bloom.Opacity
            }).ToList()
        }).ToList();
    }

    private static string ToHex(MediaColor color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private static void UpdateSelectionRect(WpfRectangle rect, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        System.Windows.Controls.Canvas.SetLeft(rect, left);
        System.Windows.Controls.Canvas.SetTop(rect, top);
        rect.Width = Math.Max(1, width);
        rect.Height = Math.Max(1, height);
    }

    private static Rect BuildRegionRect(WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
    }

    private void PushHistory()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        var stride = _surfacePixelWidth * 4;
        var pixels = new byte[stride * _surfacePixelHeight];
        _rasterSurface.CopyPixels(pixels, stride, 0);
        _history.Add(new RasterSnapshot(_surfacePixelWidth, _surfacePixelHeight, _surfaceDpiX, _surfaceDpiY, pixels));
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(0);
        }
        if (_inkRecordEnabled)
        {
            _inkHistory.Add(new InkSnapshot(CloneInkStrokes(_inkStrokes)));
            if (_inkHistory.Count > HistoryLimit)
            {
                _inkHistory.RemoveAt(0);
            }
        }
    }

    private void RestoreSnapshot(RasterSnapshot snapshot)
    {
        if (_rasterSurface == null
            || snapshot.PixelWidth != _surfacePixelWidth
            || snapshot.PixelHeight != _surfacePixelHeight)
        {
            _rasterSurface = new WriteableBitmap(
                snapshot.PixelWidth,
                snapshot.PixelHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32,
                null);
            _surfacePixelWidth = snapshot.PixelWidth;
            _surfacePixelHeight = snapshot.PixelHeight;
            _surfaceDpiX = snapshot.DpiX;
            _surfaceDpiY = snapshot.DpiY;
            RasterImage.Source = _rasterSurface;
        }
        var rect = new Int32Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight);
        var stride = snapshot.PixelWidth * 4;
        _rasterSurface.WritePixels(rect, snapshot.Pixels, stride, 0);
        _hasDrawing = true;
    }

    private void EnsureRasterSurface()
    {
        if (!IsLoaded)
        {
            return;
        }
        var ensureSw = Stopwatch.StartNew();
        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelWidth = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        if (_rasterSurface != null
            && pixelWidth == _surfacePixelWidth
            && pixelHeight == _surfacePixelHeight)
        {
            _perfEnsureSurface.Add(ensureSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        var newSurface = new WriteableBitmap(
            pixelWidth,
            pixelHeight,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32,
            null);
        if (_rasterSurface != null)
        {
            CopyBitmapToSurface(_rasterSurface, newSurface);
        }
        _rasterSurface = newSurface;
        _surfacePixelWidth = pixelWidth;
        _surfacePixelHeight = pixelHeight;
        _surfaceDpiX = dpi.PixelsPerInchX;
        _surfaceDpiY = dpi.PixelsPerInchY;
        RasterImage.Source = _rasterSurface;
        _perfEnsureSurface.Add(ensureSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void ClearSurface()
    {
        var clearSw = Stopwatch.StartNew();
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            _perfClearSurface.Add(clearSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        var rect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        var stride = _surfacePixelWidth * 4;
        var bytesRequired = stride * _surfacePixelHeight;
        if (_clearSurfaceBuffer == null || _clearSurfaceBufferSize < bytesRequired)
        {
            _clearSurfaceBuffer = new byte[bytesRequired];
            _clearSurfaceBufferSize = bytesRequired;
        }
        _rasterSurface.WritePixels(rect, _clearSurfaceBuffer, stride, 0);
        _perfClearSurface.Add(clearSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void CopyBitmapToSurface(BitmapSource source, WriteableBitmap target)
    {
        var stride = target.PixelWidth * 4;
        if (source.PixelWidth == target.PixelWidth && source.PixelHeight == target.PixelHeight)
        {
            var pixels = new byte[stride * target.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            target.WritePixels(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight), pixels, stride, 0);
            return;
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var dipWidth = target.PixelWidth * 96.0 / target.DpiX;
            var dipHeight = target.PixelHeight * 96.0 / target.DpiY;
            dc.DrawImage(source, new Rect(0, 0, dipWidth, dipHeight));
        }
        var rtb = new RenderTargetBitmap(target.PixelWidth, target.PixelHeight, target.DpiX, target.DpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var pixelsOut = new byte[stride * target.PixelHeight];
        rtb.CopyPixels(pixelsOut, stride, 0);
        target.WritePixels(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight), pixelsOut, stride, 0);
    }

    private void CommitGeometryFill(Geometry geometry, MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        bool isCalligraphy = _brushStyle == PaintBrushStyle.Calligraphy;
        bool suppressOverlays = ShouldSuppressCalligraphyOverlays();
        double inkFlow = 1.0;
        Vector? strokeDirection = null;
        if (isCalligraphy)
        {
            if (_activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
            {
                inkFlow = calligraphyRenderer.LastInkFlow;
                strokeDirection = calligraphyRenderer.LastStrokeDirection;
                var coreGeometry = calligraphyRenderer.GetLastCoreGeometry();
                if (coreGeometry != null)
                {
                    var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
                    var strokeGeometry = coreGeometry;
                    if (ribbons != null && ribbons.Count > 0)
                    {
                        var union = UnionGeometries(ribbons.Select(item => item.Geometry).ToList());
                        if (union != null)
                        {
                            strokeGeometry = union;
                        }
                    }
                    var blooms = _calligraphyInkBloomEnabled
                        ? calligraphyRenderer.GetInkBloomGeometries()
                        : null;
                    RenderCalligraphyComposite(
                        strokeGeometry,
                        color,
                        _brushSize,
                        inkFlow,
                        strokeDirection,
                        _calligraphySealEnabled,
                        _calligraphyInkBloomEnabled,
                        blooms?.Select(bloom => (bloom.Geometry, bloom.Opacity)),
                        suppressOverlays,
                        maskSeed: null);
                    return;
                }
            }
        }
        if (isCalligraphy)
        {
            RenderInkLayers(geometry, color, inkFlow, 1.0, strokeDirection);
            return;
        }
        RenderAndBlend(geometry, brush, null, erase: false, null);
    }

    private void CommitGeometryStroke(Geometry geometry, MediaPen pen)
    {
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void EraseGeometry(Geometry geometry)
    {
        ApplyInkErase(geometry);
        RenderAndBlend(geometry, MediaBrushes.White, null, erase: true, null);
    }

    private void RenderInkLayers(Geometry geometry, MediaColor color, double inkFlow, double ribbonOpacity, Vector? strokeDirection)
    {
        var solidBrush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(ribbonOpacity, 0.1, 1.0)
        };
        solidBrush.Freeze();
        var mask = IsInkMaskEligible(geometry)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
            : null;
        RenderAndBlend(geometry, solidBrush, null, erase: false, mask);
    }

    private void RenderInkCore(Geometry geometry, MediaColor color, bool enableSeal)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        RenderAndBlend(geometry, brush, null, erase: false, null);
        if (!enableSeal || !_calligraphySealEnabled)
        {
            return;
        }
        double sealWidth = Math.Max(_brushSize * CalligraphySealStrokeWidthFactor, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var pen = new MediaPen(brush, sealWidth);
        pen.Freeze();
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void RenderInkRibbonSolid(Geometry geometry, MediaColor color, double opacity)
    {
        var brush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(opacity, 0.08, 0.85)
        };
        brush.Freeze();
        RenderAndBlend(geometry, brush, null, erase: false, null);
    }

    private static Geometry? UnionGeometries(IReadOnlyList<Geometry> geometries)
    {
        if (geometries.Count == 0)
        {
            return null;
        }

        Geometry combined = geometries[0];
        for (int i = 1; i < geometries.Count; i++)
        {
            combined = new CombinedGeometry(GeometryCombineMode.Union, combined, geometries[i]);
        }
        combined.Freeze();
        return combined;
    }

    private void RenderInkSeal(Geometry geometry, MediaColor color)
    {
        if (!_calligraphySealEnabled)
        {
            return;
        }
        double sealWidth = Math.Max(_brushSize * CalligraphySealStrokeWidthFactor, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new MediaPen(brush, sealWidth);
        pen.Freeze();
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void RenderInkEdge(Geometry coreGeometry, MediaColor color, double inkFlow, Vector? strokeDirection)
    {
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(_brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var pen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        var mask = IsInkMaskEligible(coreGeometry)
            ? BuildInkOpacityMask(coreGeometry.Bounds, inkFlow, strokeDirection)
            : null;
        RenderAndBlend(coreGeometry, null, pen, erase: false, mask);
    }

    private readonly struct DrawCommand
    {
        public DrawCommand(Geometry geometry, MediaBrush? fill, MediaPen? pen, MediaBrush? opacityMask, Geometry? clipGeometry)
        {
            Geometry = geometry;
            Fill = fill;
            Pen = pen;
            OpacityMask = opacityMask;
            ClipGeometry = clipGeometry;
        }

        public Geometry Geometry { get; }
        public MediaBrush? Fill { get; }
        public MediaPen? Pen { get; }
        public MediaBrush? OpacityMask { get; }
        public Geometry? ClipGeometry { get; }
    }

    private void RenderCalligraphyComposite(
        Geometry geometry,
        MediaColor color,
        double brushSize,
        double inkFlow,
        Vector? strokeDirection,
        bool sealEnabled,
        bool bloomEnabled,
        IEnumerable<(Geometry Geometry, double Opacity)>? blooms,
        bool suppressOverlays,
        int? maskSeed)
    {
        var commands = new List<DrawCommand>();

        if (!suppressOverlays && bloomEnabled && blooms != null)
        {
            foreach (var bloom in blooms)
            {
                var bloomBrush = new SolidColorBrush(color)
                {
                    Opacity = bloom.Opacity
                };
                bloomBrush.Freeze();
                commands.Add(new DrawCommand(bloom.Geometry, bloomBrush, null, null, geometry));
            }
        }

        var coreBrush = new SolidColorBrush(color);
        coreBrush.Freeze();
        commands.Add(new DrawCommand(geometry, coreBrush, null, null, null));

        if (!suppressOverlays && sealEnabled)
        {
            double sealWidth = Math.Max(brushSize * CalligraphySealStrokeWidthFactor, 0.6);
            if (sealWidth > 0)
            {
                var sealPen = new MediaPen(coreBrush, sealWidth)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                sealPen.Freeze();
                commands.Add(new DrawCommand(geometry, null, sealPen, null, null));
            }
        }

        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var edgePen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        edgePen.Freeze();
        var edgeMask = maskSeed.HasValue
            ? (IsInkMaskEligible(geometry, brushSize)
                ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed.Value)
                : null)
            : (IsInkMaskEligible(geometry)
                ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
                : null);
        commands.Add(new DrawCommand(geometry, null, edgePen, edgeMask, null));

        if (!suppressOverlays)
        {
            var ribbonBrush = new SolidColorBrush(color)
            {
                Opacity = Math.Clamp(0.28, 0.1, 1.0)
            };
            ribbonBrush.Freeze();
            var ribbonMask = maskSeed.HasValue
                ? (IsInkMaskEligible(geometry, brushSize)
                    ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed.Value)
                    : null)
                : (IsInkMaskEligible(geometry)
                    ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
                    : null);
            commands.Add(new DrawCommand(geometry, ribbonBrush, null, ribbonMask, null));
        }

        RenderAndBlendBatch(commands);
    }

    private bool ShouldSuppressCalligraphyOverlays()
    {
        // Lower opacity amplifies edge overlap; suppress overlays for clarity.
        return _brushOpacity < _calligraphyOverlayOpacityThreshold;
    }
    
    private bool IsInkMaskEligible(Geometry geometry)
    {
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = geometry.Bounds;
        double minSize = Math.Max(_brushSize * 1.0, 14.0);
        return bounds.Width >= minSize && bounds.Height >= minSize;
    }

    private static bool IsInkMaskEligible(Geometry geometry, double brushSize)
    {
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = geometry.Bounds;
        double minSize = Math.Max(brushSize * 1.0, 14.0);
        return bounds.Width >= minSize && bounds.Height >= minSize;
    }
    

    private void RenderAndBlend(Geometry geometry, MediaBrush? fill, MediaPen? pen, bool erase, MediaBrush? opacityMask, Geometry? clipGeometry = null)
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        if (!TryRenderGeometry(geometry, fill, pen, opacityMask, clipGeometry, out var rect, out var pixels, out var stride, out var bufferLength))
        {
            return;
        }
        try
        {
            if (erase)
            {
                ApplyEraseMask(rect, pixels, stride);
            }
            else
            {
                BlendSourceOver(rect, pixels, stride);
            }
            _hasDrawing = true;
        }
        finally
        {
            PixelPool.Return(pixels, clearArray: false);
        }
    }

    private void RenderAndBlendBatch(IReadOnlyList<DrawCommand> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            return;
        }
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        if (!TryRenderGeometryBatch(commands, out var rect, out var pixels, out var stride, out var bufferLength))
        {
            return;
        }
        try
        {
            BlendSourceOver(rect, pixels, stride);
            _hasDrawing = true;
        }
        finally
        {
            PixelPool.Return(pixels, clearArray: false);
        }
    }

    private bool TryRenderGeometry(
        Geometry geometry,
        MediaBrush? fill,
        MediaPen? pen,
        MediaBrush? opacityMask,
        Geometry? clipGeometry,
        out Int32Rect destRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        destRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;
        if (_rasterSurface == null || geometry == null)
        {
            return false;
        }
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = pen != null ? geometry.GetRenderBounds(pen) : geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return false;
        }
        bounds.Inflate(2, 2);
        var dpi = VisualTreeHelper.GetDpi(this);
        var rawRect = new Int32Rect(
            (int)Math.Floor(bounds.X * dpi.DpiScaleX),
            (int)Math.Floor(bounds.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(bounds.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(bounds.Height * dpi.DpiScaleY));
        var surfaceRect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        destRect = IntersectRects(rawRect, surfaceRect);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var offsetX = destRect.X / dpi.DpiScaleX;
            var offsetY = destRect.Y / dpi.DpiScaleY;
            dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
            if (clipGeometry != null)
            {
                dc.PushClip(clipGeometry);
            }
            if (opacityMask != null)
            {
                dc.PushOpacityMask(opacityMask);
            }
            dc.DrawGeometry(fill, pen, geometry);
            if (opacityMask != null)
            {
                dc.Pop();
            }
            if (clipGeometry != null)
            {
                dc.Pop();
            }
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(destRect.Width, destRect.Height, _surfaceDpiX, _surfaceDpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        stride = destRect.Width * 4;
        bufferLength = stride * destRect.Height;
        pixels = PixelPool.Rent(bufferLength);
        rtb.CopyPixels(pixels, stride, 0);
        return true;
    }

    private bool TryRenderGeometryBatch(
        IReadOnlyList<DrawCommand> commands,
        out Int32Rect destRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        destRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;
        if (_rasterSurface == null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var surfaceRect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        bool hasBounds = false;
        var unionRect = new Int32Rect(0, 0, 0, 0);

        foreach (var command in commands)
        {
            var geometry = command.Geometry;
            if (geometry == null || geometry.Bounds.IsEmpty)
            {
                continue;
            }
            var bounds = command.Pen != null ? geometry.GetRenderBounds(command.Pen) : geometry.Bounds;
            if (bounds.IsEmpty)
            {
                continue;
            }
            bounds.Inflate(2, 2);
            var rawRect = new Int32Rect(
                (int)Math.Floor(bounds.X * dpi.DpiScaleX),
                (int)Math.Floor(bounds.Y * dpi.DpiScaleY),
                (int)Math.Ceiling(bounds.Width * dpi.DpiScaleX),
                (int)Math.Ceiling(bounds.Height * dpi.DpiScaleY));
            if (!hasBounds)
            {
                unionRect = rawRect;
                hasBounds = true;
            }
            else
            {
                unionRect = UnionRects(unionRect, rawRect);
            }
        }

        if (!hasBounds)
        {
            return false;
        }
        destRect = IntersectRects(unionRect, surfaceRect);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var offsetX = destRect.X / dpi.DpiScaleX;
            var offsetY = destRect.Y / dpi.DpiScaleY;
            dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
            foreach (var command in commands)
            {
                if (command.ClipGeometry != null)
                {
                    dc.PushClip(command.ClipGeometry);
                }
                if (command.OpacityMask != null)
                {
                    dc.PushOpacityMask(command.OpacityMask);
                }
                dc.DrawGeometry(command.Fill, command.Pen, command.Geometry);
                if (command.OpacityMask != null)
                {
                    dc.Pop();
                }
                if (command.ClipGeometry != null)
                {
                    dc.Pop();
                }
            }
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(destRect.Width, destRect.Height, _surfaceDpiX, _surfaceDpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        stride = destRect.Width * 4;
        bufferLength = stride * destRect.Height;
        pixels = PixelPool.Rent(bufferLength);
        rtb.CopyPixels(pixels, stride, 0);
        return true;
    }

    private MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }

        int tileSize = (int)Math.Round(Math.Clamp(_brushSize * 2.2, 18, 90));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.68, 0.96, inkFlow);
        double variation = Lerp(0.08, 0.24, dryFactor);
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, _inkRandom.Next());

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.72 + (inkFlow * 0.28), 0.6, 1.0)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor);
        texture.Freeze();

        var centerOpacity = Math.Clamp(0.95 + (inkFlow * 0.05), 0.85, 1.0);
        var edgeOpacity = Math.Clamp(0.72 + (inkFlow * 0.08), 0.6, 0.9);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WpfPoint(bounds.X + bounds.Width * 0.5, bounds.Y + bounds.Height * 0.5),
            GradientOrigin = new WpfPoint(bounds.X + bounds.Width * 0.48, bounds.Y + bounds.Height * 0.48),
            RadiusX = bounds.Width * 0.55,
            RadiusY = bounds.Height * 0.55
        };
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(MediaBrushes.White, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(radial, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(texture, null, new RectangleGeometry(bounds)));
        group.Freeze();
        return new DrawingBrush(group) { Stretch = Stretch.None };
    }

    private static MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection, double brushSize, int seed)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }
        int tileSize = (int)Math.Round(Math.Clamp(brushSize * 2.2, 18, 90));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.68, 0.96, inkFlow);
        double variation = Lerp(0.08, 0.24, dryFactor);
        int effectiveSeed = seed == 0 ? 17 : seed;
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, effectiveSeed);

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.72 + (inkFlow * 0.28), 0.6, 1.0)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor);
        texture.Freeze();

        var centerOpacity = Math.Clamp(0.95 + (inkFlow * 0.05), 0.85, 1.0);
        var edgeOpacity = Math.Clamp(0.72 + (inkFlow * 0.08), 0.6, 0.9);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WpfPoint(bounds.X + bounds.Width * 0.5, bounds.Y + bounds.Height * 0.5),
            GradientOrigin = new WpfPoint(bounds.X + bounds.Width * 0.48, bounds.Y + bounds.Height * 0.48),
            RadiusX = bounds.Width * 0.55,
            RadiusY = bounds.Height * 0.55
        };
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(MediaBrushes.White, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(radial, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(texture, null, new RectangleGeometry(bounds)));
        group.Freeze();
        return new DrawingBrush(group) { Stretch = Stretch.None };
    }

    private static void ApplyInkTextureTransform(ImageBrush brush, Rect bounds, Vector? strokeDirection, double dryFactor)
    {
        var dir = strokeDirection ?? new Vector(1, 0);
        if (dir.LengthSquared < 0.0001)
        {
            dir = new Vector(1, 0);
        }
        else
        {
            dir.Normalize();
        }

        double angle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
        double centerX = bounds.X + bounds.Width * 0.5;
        double centerY = bounds.Y + bounds.Height * 0.5;
        double stretch = Lerp(1.3, 1.8, dryFactor);
        double squash = Lerp(0.85, 0.6, dryFactor);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(stretch, squash, centerX, centerY));
        transforms.Children.Add(new RotateTransform(angle, centerX, centerY));
        brush.Transform = transforms;
    }

    private sealed class InkNoiseTileEntry
    {
        public InkNoiseTileEntry(BitmapSource tile, LinkedListNode<InkNoiseTileKey> node)
        {
            Tile = tile;
            Node = node;
        }

        public BitmapSource Tile { get; }
        public LinkedListNode<InkNoiseTileKey> Node { get; }
    }

    private readonly struct InkNoiseTileKey : IEquatable<InkNoiseTileKey>
    {
        public InkNoiseTileKey(int size, int seed, double baseAlpha, double variation)
        {
            Size = size;
            Seed = seed;
            BaseAlphaBits = BitConverter.DoubleToInt64Bits(baseAlpha);
            VariationBits = BitConverter.DoubleToInt64Bits(variation);
        }

        public int Size { get; }
        public int Seed { get; }
        public long BaseAlphaBits { get; }
        public long VariationBits { get; }

        public bool Equals(InkNoiseTileKey other)
        {
            return Size == other.Size
                && Seed == other.Seed
                && BaseAlphaBits == other.BaseAlphaBits
                && VariationBits == other.VariationBits;
        }

        public override bool Equals(object? obj)
        {
            return obj is InkNoiseTileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, Seed, BaseAlphaBits, VariationBits);
        }
    }

    private static BitmapSource CreateInkNoiseTile(int size, double baseAlpha, double variation, int seed)
    {
        var key = new InkNoiseTileKey(size, seed, baseAlpha, variation);
        lock (InkNoiseTileCacheLock)
        {
            if (InkNoiseTileCache.TryGetValue(key, out var entry))
            {
                InkNoiseTileOrder.Remove(entry.Node);
                InkNoiseTileOrder.AddLast(entry.Node);
                return entry.Tile;
            }
        }

        var bitmap = CreateInkNoiseTileCore(size, baseAlpha, variation, seed);
        lock (InkNoiseTileCacheLock)
        {
            if (InkNoiseTileCache.TryGetValue(key, out var existing))
            {
                InkNoiseTileOrder.Remove(existing.Node);
                InkNoiseTileOrder.AddLast(existing.Node);
                return existing.Tile;
            }
            var node = InkNoiseTileOrder.AddLast(key);
            InkNoiseTileCache[key] = new InkNoiseTileEntry(bitmap, node);
            while (InkNoiseTileOrder.Count > InkNoiseTileCacheLimit)
            {
                var oldest = InkNoiseTileOrder.First;
                if (oldest == null)
                {
                    break;
                }
                InkNoiseTileOrder.RemoveFirst();
                InkNoiseTileCache.Remove(oldest.Value);
            }
        }
        return bitmap;
    }

    private static BitmapSource CreateInkNoiseTileCore(int size, double baseAlpha, double variation, int seed)
    {
        var rng = new Random(seed);
        int grid = 14;
        var gridValues = new double[grid + 1, grid + 1];

        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                double jitter = (rng.NextDouble() * 2.0 - 1.0) * variation;
                gridValues[x, y] = Math.Clamp(baseAlpha + jitter, 0.0, 1.0);
            }
        }

        double angle = rng.NextDouble() * Math.PI;
        double fx = Math.Cos(angle);
        double fy = Math.Sin(angle);
        double fiberFreq = 2.6 + rng.NextDouble() * 2.2;
        double fiberPhase = rng.NextDouble() * Math.PI * 2.0;
        double fiberAmp = variation * 0.2;

        int stride = size * 4;
        var pixels = new byte[stride * size];
        double scale = grid / (double)(size - 1);

        for (int y = 0; y < size; y++)
        {
            double gy = y * scale;
            int y0 = (int)Math.Floor(gy);
            int y1 = Math.Min(y0 + 1, grid);
            double ty = gy - y0;

            for (int x = 0; x < size; x++)
            {
                double gx = x * scale;
                int x0 = (int)Math.Floor(gx);
                int x1 = Math.Min(x0 + 1, grid);
                double tx = gx - x0;

                double n0 = Lerp(gridValues[x0, y0], gridValues[x1, y0], tx);
                double n1 = Lerp(gridValues[x0, y1], gridValues[x1, y1], tx);
                double noise = Lerp(n0, n1, ty);

                double fiber = Math.Sin(((x * fx + y * fy) / size) * (Math.PI * 2.0 * fiberFreq) + fiberPhase) * fiberAmp;
                double value = Math.Clamp(noise + fiber, 0.0, 1.0);
                byte alpha = (byte)Math.Round(value * 255);

                int idx = (y * size + x) * 4;
                pixels[idx] = alpha;
                pixels[idx + 1] = alpha;
                pixels[idx + 2] = alpha;
                pixels[idx + 3] = alpha;
            }
        }

        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private void BlendSourceOver(Int32Rect rect, byte[] srcPixels, int srcStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destLength = destStride * rect.Height;
        var destPixels = PixelPool.Rent(destLength);
        _rasterSurface.CopyPixels(rect, destPixels, destStride, 0);
        for (int y = 0; y < rect.Height; y++)
        {
            var srcRow = y * srcStride;
            var destRow = y * destStride;
            for (int x = 0; x < rect.Width; x++)
            {
                int i = srcRow + x * 4;
                byte srcA = srcPixels[i + 3];
                if (srcA == 0)
                {
                    continue;
                }
                int invA = 255 - srcA;
                int d = destRow + x * 4;
                destPixels[d] = (byte)(srcPixels[i] + destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(srcPixels[i + 1] + destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(srcPixels[i + 2] + destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(srcA + destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
        PixelPool.Return(destPixels, clearArray: false);
    }

    private void ApplyEraseMask(Int32Rect rect, byte[] maskPixels, int maskStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destLength = destStride * rect.Height;
        var destPixels = PixelPool.Rent(destLength);
        _rasterSurface.CopyPixels(rect, destPixels, destStride, 0);
        for (int y = 0; y < rect.Height; y++)
        {
            var maskRow = y * maskStride;
            var destRow = y * destStride;
            for (int x = 0; x < rect.Width; x++)
            {
                int i = maskRow + x * 4;
                byte maskA = maskPixels[i + 3];
                if (maskA == 0)
                {
                    continue;
                }
                int invA = 255 - maskA;
                int d = destRow + x * 4;
                destPixels[d] = (byte)(destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
        PixelPool.Return(destPixels, clearArray: false);
    }

    private static Int32Rect IntersectRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }

    private static Int32Rect UnionRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int right = Math.Max(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }

    private sealed class RefreshOrchestrator
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action _refreshAction;
        private int _refreshRunning;
        private int _refreshRequested;

        public RefreshOrchestrator(Dispatcher dispatcher, Action refreshAction)
        {
            _dispatcher = dispatcher;
            _refreshAction = refreshAction;
        }

        public void RequestRefresh(string reason)
        {
            Interlocked.Exchange(ref _refreshRequested, 1);
            if (Interlocked.CompareExchange(ref _refreshRunning, 1, 0) == 0)
            {
                _dispatcher.BeginInvoke(new Action(Drain), DispatcherPriority.Background);
            }
        }

        private void Drain()
        {
            int loops = 0;
            while (true)
            {
                Interlocked.Exchange(ref _refreshRequested, 0);
                _refreshAction();
                loops++;
                if (Interlocked.CompareExchange(ref _refreshRequested, 0, 0) == 0 || loops >= 2)
                {
                    Interlocked.Exchange(ref _refreshRunning, 0);
                    if (Interlocked.Exchange(ref _refreshRequested, 0) == 1
                        && Interlocked.CompareExchange(ref _refreshRunning, 1, 0) == 0)
                    {
                        _dispatcher.BeginInvoke(new Action(Drain), DispatcherPriority.Background);
                    }
                    return;
                }
            }
        }
    }

    private sealed class PerfStats
    {
        private readonly string _name;
        private int _count;
        private double _total;
        private double _max;
        private int _uiThreadSamples;
        private DateTime _lastLogUtc = DateTime.MinValue;

        public PerfStats(string name)
        {
            _name = name;
        }

        public void Add(double ms, bool uiThread)
        {
            _count++;
            _total += ms;
            if (ms > _max)
            {
                _max = ms;
            }
            if (uiThread)
            {
                _uiThreadSamples++;
            }

            if (_count < 30 && (DateTime.UtcNow - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }
            if (_count % 30 != 0 && (DateTime.UtcNow - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }

            var avg = _total / Math.Max(1, _count);
            var uiRatio = _count == 0 ? 0 : (double)_uiThreadSamples / _count * 100.0;
            System.Diagnostics.Debug.WriteLine(
                $"[Perf] {_name}: avg={avg:F2}ms max={_max:F2}ms samples={_count} ui={uiRatio:F0}%");
            _lastLogUtc = DateTime.UtcNow;
        }
    }
}
