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
using ClassroomToolkit.App.Utilities;
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
    private const int PresentationFocusMonitorIntervalMs = 500;
    
    /// <summary>婕旂ず鏂囩鐒︾偣鎭㈠鍐峰嵈鏃堕棿锛坢s锛?- 闃叉棰戠箒鍒囨崲瀵艰嚧闂儊</summary>
    private const int PresentationFocusCooldownMs = 1200;
    

    
    /// <summary>璺ㄩ〉鏇存柊鏈€灏忛棿闅旓紙ms锛?- 杩炵画婊氬姩鏃剁殑娓叉煋鑺傛祦</summary>
    private const int CrossPageUpdateMinIntervalMs = 24;
    
    /// <summary>鐓х墖婊氳疆缂╂斁鍩烘暟 - 姣忔婊氳疆鍒诲害鐨勭缉鏀剧郴鏁帮紙鎺ヨ繎 1.0 浣跨缉鏀惧钩婊戯級</summary>
    private const double PhotoWheelZoomBase = 1.0008;
    
    /// <summary>鐓х墖鎸夐敭缂╂斁姝ヨ繘 - 浣跨敤 +/- 閿椂鐨勭缉鏀惧箙搴?/summary>
    private const double PhotoKeyZoomStep = 1.06;

    private IntPtr _hwnd;
    private bool _inputPassthroughEnabled;
    private bool _focusBlocked;
    private bool _forcePresentationForegroundOnFullscreen;
    private readonly DispatcherTimer _presentationFocusMonitor;
    private DateTime _nextPresentationFocusAttempt = DateTime.MinValue;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;


    private PaintToolMode _mode = PaintToolMode.Brush;
    private PaintShapeType _shapeType = PaintShapeType.Line;

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
    private readonly LatestOnlyAsyncGate _wpsNavHookStateGate = new();
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
    private ScaleTransform _photoPageScale = new ScaleTransform(1.0, 1.0);
    private ScaleTransform _photoScale = new ScaleTransform(1.0, 1.0);
    private TranslateTransform _photoTranslate = new TranslateTransform(0, 0);
    private bool _photoPanning;
    private bool _photoRightClickPending;
    private WpfPoint _photoRightClickStart;
    private WpfPoint _photoPanStart;
    private double _photoPanOriginX;
    private double _photoPanOriginY;
    private bool _photoRestoreFullscreenPending;
    private int _photoFullscreenBoundsToken;
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
    private double _crossPageNormalizedWidthDip;


    public event Action<string, DateTime>? InkContextChanged;
    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action<double, double, double, double>? PhotoUnifiedTransformChanged;
    public event Action? FloatingZOrderRequested;
    public event Action? PresentationFullscreenDetected;
    public event Action<ClassroomToolkit.Interop.Presentation.PresentationType>? PresentationForegroundDetected;
    public event Action? PhotoForegroundDetected;
    public event Action? PhotoCloseRequested;





    public PaintOverlayWindow()
    {
        InitializeComponent();
        _neighborPagesCanvas = FindName("NeighborPagesCanvas") as System.Windows.Controls.Canvas;
        _visualHost = new DrawingVisualHost();
        CustomDrawHost.Child = _visualHost;
        var photoTransform = new TransformGroup();
        photoTransform.Children.Add(_photoPageScale);
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
            _wpsNavHookStateGate.NextGeneration();
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
        
        // 绔嬪嵆鏇存柊杈撳叆绌块€忕姸鎬侊紙杞婚噺绾ф搷浣滐級
        UpdateInputPassthrough();
        
        // 寤惰繜鏇存柊閽╁瓙鍜岀劍鐐圭姸鎬侊紝閬垮厤鍗￠】
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            
            // 鍏夋爣妯″紡涓嬫仮澶嶇劍鐐?
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
            PaintToolMode.Brush => Utilities.CustomCursors.GetBrushCursor(_brushColor),  // 甯﹂鑹茬殑鐢荤瑪鏍峰紡
            PaintToolMode.Eraser => Utilities.CustomCursors.Eraser,  // 姗＄毊鎿︽牱寮?
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
    }

    public void SetShapeType(PaintShapeType type)
    {
        _shapeType = type == PaintShapeType.RectangleFill ? PaintShapeType.Rectangle : type;
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

    // 杈呭姪灞炴€э細绠€鍖栭噸澶嶇殑澶嶅悎鍒ゆ柇閫昏緫锛堟浛浠?30+ 澶勯噸澶嶄唬鐮侊級
    private bool IsPhotoDocumentPdf => _photoModeActive && _photoDocumentIsPdf;
    private bool IsPhotoFullscreen => _photoModeActive && _photoFullscreen;

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
        // 鍏夋爣妯″紡涓嬶紝涓嶉樆姝㈢劍鐐癸紝璁╄緭鍏ヤ簨浠惰嚜鐢变紶閫掑埌婕旂ず鏂囩
        // 杩欐牱鍙互纭繚閿洏鍜屾粴杞簨浠舵甯稿伐浣?
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
        ResetCrossPageNormalizedWidth();
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
            UpdateCurrentPageWidthNormalization();
        }
        if (!_crossPageDisplayEnabled)
        {
            ClearNeighborPages();
            UpdateCurrentPageWidthNormalization();
        }
        if (_photoModeActive && !_photoDocumentIsPdf)
        {
            // Reset image cache to avoid mixing different decode policies
            // between cross-page and single-page rendering.
            ClearNeighborImageCache();
            var currentPage = GetCurrentPageIndexForCrossPage();
            var bitmap = GetPageBitmap(currentPage);
            if (bitmap != null)
            {
                PhotoBackground.Source = bitmap;
                PhotoBackground.Visibility = Visibility.Visible;
                UpdateCurrentPageWidthNormalization(bitmap);
            }
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
            UpdateCurrentPageWidthNormalization();
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

}
