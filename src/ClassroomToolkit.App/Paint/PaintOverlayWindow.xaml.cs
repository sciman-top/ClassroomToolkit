using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{
    private static readonly MediaColor TransparentHitTestColor = MediaColor.FromArgb(1, 255, 255, 255);
    private const int GwlStyle = -16;
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsCaption = 0x00C00000;
    private const uint MonitorDefaultToNearest = 2;
    private const int PresentationFocusMonitorIntervalMs = 500;
    private const int PresentationFocusCooldownMs = 1200;
    private const double CalligraphySealStrokeWidthFactor = 0.08;
    private const byte DefaultCalligraphyOverlayOpacityThreshold = 230;
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
    private bool _presentationFocusRestoreEnabled = false;
    private readonly List<InkStrokeData> _inkStrokes = new();
    private readonly List<InkSnapshot> _inkHistory = new();
    private readonly DispatcherTimer _inkMonitor;
    private readonly Random _inkSeedRandom = new Random();
    private bool _inkRecordEnabled = true;
    private bool _inkCacheEnabled = true;
    private bool _presentationActive;
    private DateTime _currentCourseDate = DateTime.Today;
    private string _currentDocumentName = string.Empty;
    private string _currentDocumentPath = string.Empty;
    private int _currentPageIndex = 1;
    private int _wpsPageIndex = 1;
    private string _currentCacheKey = string.Empty;
    private InkCacheScope _currentCacheScope = InkCacheScope.None;
    private readonly InkFinalCache _presentationCache = new(200);
    private readonly InkFinalCache _photoCache = new(80);
    private const double PdfDefaultDpi = 150;
    private const int PdfCacheLimit = 3;
    private bool _photoModeActive;
    private bool _photoFullscreen;
    private bool _reviewModeActive;
    private bool _reviewNavigationEnabled;
    private ScaleTransform _photoScale = new ScaleTransform(1.0, 1.0);
    private TranslateTransform _photoTranslate = new TranslateTransform(0, 0);
    private bool _photoPanning;
    private WpfPoint _photoPanStart;
    private double _photoPanOriginX;
    private double _photoPanOriginY;
    private bool _photoDocumentIsPdf;
    private PdfDocumentHost? _pdfDocument;
    private int _pdfPageCount;
    private readonly Dictionary<int, BitmapSource> _pdfPageCache = new();
    private readonly LinkedList<int> _pdfPageOrder = new();
    private bool _rememberPhotoTransform;
    private bool _photoUserTransformDirty;
    private double _lastPhotoScaleX = 1.0;
    private double _lastPhotoScaleY = 1.0;
    private double _lastPhotoTranslateX;
    private double _lastPhotoTranslateY;
    private enum InkCacheScope
    {
        None = 0,
        Presentation = 1,
        Photo = 2
    }

    public event Action<string, DateTime>? InkContextChanged;
    public event Action<int>? ReviewNavigationRequested;
    public event Action<bool>? ReviewModeChanged;
    public event Action<bool>? PhotoModeChanged;
    public event Action<int>? PhotoNavigationRequested;
    public event Action? FloatingZOrderRequested;

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
        _visualHost = new DrawingVisualHost();
        CustomDrawHost.Child = _visualHost;
        var photoTransform = new TransformGroup();
        photoTransform.Children.Add(_photoScale);
        photoTransform.Children.Add(_photoTranslate);
        PhotoBackground.RenderTransform = photoTransform;
        
        WindowState = WindowState.Maximized;
        _presentationFocusMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PresentationFocusMonitorIntervalMs)
        };
        _presentationFocusMonitor.Tick += (_, _) => MonitorPresentationFocus();
        _inkMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _inkMonitor.Tick += (_, _) => MonitorInkContext();
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
        OverlayRoot.IsManipulationEnabled = true;
        OverlayRoot.ManipulationStarting += OnManipulationStarting;
        OverlayRoot.ManipulationDelta += OnManipulationDelta;
        OverlayRoot.StylusDown += OnStylusDown;
        OverlayRoot.StylusMove += OnStylusMove;
        OverlayRoot.StylusUp += OnStylusUp;
        MouseWheel += OnMouseWheel;
        Loaded += (_, _) => EnsureRasterSurface();
        SizeChanged += (_, _) => EnsureRasterSurface();
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
        _boardColor = color;
        UpdateBoardBackground();
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
            TryAutoSave();
        }
    }

    public MediaColor CurrentBrushColor => _brushColor;
    public byte CurrentBrushOpacity => _brushOpacity;
    public string CurrentDocumentName => _currentDocumentName;
    public DateTime CurrentCourseDate => _currentCourseDate;
    public int CurrentPageIndex => _currentPageIndex;

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
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
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
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
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            UpdatePhotoPan(e.GetPosition(OverlayRoot));
            e.Handled = true;
        }
    }

    private void OnRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
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
    }

    private void BeginBrushStroke(WpfPoint position)
    {
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }
        PushHistory();
        _strokeInProgress = true;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(position);
        _visualHost.UpdateVisual(_activeRenderer.Render);
    }

    private void UpdateBrushStroke(WpfPoint position)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        _activeRenderer.OnMove(position);
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
        TryAutoSave();
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
        TryAutoSave();
    }

    private void BeginShape(WpfPoint position)
    {
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        PushHistory();
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
        if (_reviewNavigationEnabled)
        {
            var direction = e.Delta < 0 ? 1 : -1;
            ReviewNavigationRequested?.Invoke(direction);
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
            return;
        }
        if (WpsHookRecentlyFired())
        {
            return;
        }
        var command = e.Delta < 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        if (_presentationOptions.AllowWps)
        {
            var target = ResolveWpsTarget();
            if (target.IsValid)
            {
                var wpsForeground = IsTargetForeground(target);
                if (!IsBoardActive() && wpsForeground && !_presentationOptions.WheelAsKey && _inputPassthroughEnabled)
                {
                    return;
                }
            }
        }
        if (TrySendWpsNavigation(command))
        {
            e.Handled = true;
            return;
        }
        if (_presentationService.TrySendForeground(command, _presentationOptions))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (e.Key == Key.Escape && _photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            e.Handled = true;
            return;
        }
        if (IsPhotoNavigationKey(e.Key, out var direction))
        {
            if (TryNavigatePdf(direction))
            {
                e.Handled = true;
                return;
            }
            PhotoNavigationRequested?.Invoke(direction);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Add || e.Key == Key.OemPlus)
        {
            ZoomPhotoByFactor(1.08);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
        {
            ZoomPhotoByFactor(0.92);
            e.Handled = true;
        }
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
        if (!_photoModeActive || _mode != PaintToolMode.Cursor)
        {
            return;
        }
        e.ManipulationContainer = OverlayRoot;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor)
        {
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
        }
        RedrawInkSurface();
        e.Handled = true;
    }

    private void ZoomPhoto(int delta, WpfPoint center)
    {
        double scaleFactor = Math.Pow(1.0015, delta);
        ApplyPhotoScale(scaleFactor, center);
        RedrawInkSurface();
    }

    private void ZoomPhotoByFactor(double scaleFactor)
    {
        var center = new WpfPoint(OverlayRoot.ActualWidth / 2.0, OverlayRoot.ActualHeight / 2.0);
        ApplyPhotoScale(scaleFactor, center);
        RedrawInkSurface();
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
        SavePhotoTransformState(userAdjusted: true);
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
        _boardOpacity = opacity;
        UpdateBoardBackground();
        UpdateInputPassthrough();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
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
            _presentationCache.Clear();
            _photoCache.Clear();
        }
        MonitorInkContext();
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

    public bool IsPhotoModeActive => _photoModeActive;
    public bool IsReviewModeActive => _reviewModeActive;

    public void SetReviewNavigationEnabled(bool enabled)
    {
        _reviewNavigationEnabled = enabled;
    }

    public void EnterPhotoMode(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }
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
        if (_photoModeActive)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
        }
        var isPdf = IsPdfFile(sourcePath);
        if (_photoModeActive && _photoDocumentIsPdf)
        {
            ClosePdfDocument();
        }
        EnsurePhotoTransformsWritable();
        if (_rememberPhotoTransform && _photoUserTransformDirty)
        {
            _photoScale.ScaleX = _lastPhotoScaleX;
            _photoScale.ScaleY = _lastPhotoScaleY;
            _photoTranslate.X = _lastPhotoTranslateX;
            _photoTranslate.Y = _lastPhotoTranslateY;
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
        _presentationActive = false;
        Topmost = false;
        SetReviewModeActive(false);
        _currentCourseDate = DateTime.Today;
        _currentDocumentName = IoPath.GetFileNameWithoutExtension(sourcePath);
        _currentDocumentPath = sourcePath;
        _currentPageIndex = 1;
        _wpsPageIndex = 1;
        _currentCacheScope = InkCacheScope.Photo;
        _currentCacheKey = isPdf
            ? BuildPdfCacheKey(sourcePath, _currentPageIndex)
            : BuildPhotoCacheKey(sourcePath);
        _photoDocumentIsPdf = isPdf;
        SetPhotoWindowMode(_photoFullscreen);
        if (isPdf)
        {
            if (!TryOpenPdfDocument(sourcePath))
            {
                ExitPhotoMode();
                return;
            }
            if (!RenderPdfPage(_currentPageIndex))
            {
                ExitPhotoMode();
                return;
            }
        }
        else
        {
            if (!TrySetPhotoBackground(sourcePath))
            {
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
    }

    public void ExitPhotoMode()
    {
        if (!_photoModeActive)
        {
            return;
        }
        SaveCurrentPageOnNavigate(forceBackground: false);
        PhotoBackground.Source = null;
        PhotoBackground.Visibility = Visibility.Collapsed;
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
        _photoDocumentIsPdf = false;
        SetReviewModeActive(false);
        SetPhotoWindowMode(fullscreen: false);
        Topmost = true;
        PhotoModeChanged?.Invoke(false);
        _currentDocumentName = string.Empty;
        _currentDocumentPath = string.Empty;
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = "图片应用";
        }
        _currentPageIndex = 1;
        _wpsPageIndex = 1;
        _currentCacheScope = InkCacheScope.None;
        _currentCacheKey = string.Empty;
        ResetInkHistory();
        _inkStrokes.Clear();
        RedrawInkSurface();
    }

    public void EnterReviewMode(DateTime date, string documentName, int pageIndex)
    {
        // Ink review is removed per updated requirements.
        return;
    }

    public bool RestorePresentationFocusIfNeeded(bool requireFullscreen = false)
    {
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
        // 优先尝试 WPS，然后尝试 Office
        if (_presentationOptions.AllowWps)
        {
            var wpsTarget = ResolveWpsTarget();
            if (wpsTarget.IsValid && TrySendWpsNavigation(command.Value))
            {
                return;
            }
        }
        if (_presentationOptions.AllowOffice)
        {
            _presentationService.TrySendForeground(command.Value, _presentationOptions);
        }
    }

    public bool TryHandleReviewNavigationKey(Key key)
    {
        if (!_reviewNavigationEnabled)
        {
            return false;
        }
        int? direction = key switch
        {
            Key.Right => 1,
            Key.Down => 1,
            Key.PageDown => 1,
            Key.Space => 1,
            Key.Enter => 1,
            Key.Left => -1,
            Key.Up => -1,
            Key.PageUp => -1,
            _ => null
        };
        if (direction == null)
        {
            return false;
        }
        ReviewNavigationRequested?.Invoke(direction.Value);
        return true;
    }

    private void UpdatePresentationFocusMonitor()
    {
        var shouldMonitor = IsVisible && (_presentationOptions.AllowOffice || _presentationOptions.AllowWps);
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

    private void MonitorPresentationFocus()
    {
        if (!_presentationFocusRestoreEnabled)
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
        if (_photoModeActive)
        {
            return;
        }
        var pptInfo = PresentationSlideResolver.TryResolvePowerPoint();
        if (pptInfo != null)
        {
            var docName = IoPath.GetFileNameWithoutExtension(pptInfo.DisplayName);
            _presentationActive = true;
            UpdatePresentationContext(docName, pptInfo.FilePath, pptInfo.SlideIndex, isWps: false);
            return;
        }
        if (_presentationOptions.AllowWps)
        {
            var target = ResolveWpsTarget();
            if (!target.IsValid)
            {
                if (_presentationActive)
                {
                    SaveCurrentPageOnNavigate(forceBackground: false);
                    _presentationActive = false;
                }
                return;
            }
            var title = WindowTextHelper.GetWindowTitle(target.Handle);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = target.Info.ProcessName;
            }
            _presentationActive = true;
            var parsed = TryParseSlideIndexFromTitle(title);
            var normalizedTitle = NormalizeWpsTitle(title);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                normalizedTitle = title;
            }
            if (!string.Equals(normalizedTitle, _currentDocumentName, StringComparison.OrdinalIgnoreCase))
            {
                _wpsPageIndex = parsed ?? 1;
            }
            else if (parsed.HasValue)
            {
                _wpsPageIndex = parsed.Value;
            }
            UpdatePresentationContext(normalizedTitle, string.Empty, _wpsPageIndex, isWps: true);
        }
        else
        {
            if (_presentationActive)
            {
                SaveCurrentPageOnNavigate(forceBackground: false);
                _presentationActive = false;
            }
        }
    }

    private void UpdatePresentationContext(string documentName, string documentPath, int pageIndex, bool isWps)
    {
        if (string.IsNullOrWhiteSpace(documentName) || pageIndex <= 0)
        {
            return;
        }
        SetReviewModeActive(false);
        var nextKey = BuildPresentationCacheKey(documentName, documentPath, pageIndex, isWps);
        if (string.IsNullOrWhiteSpace(nextKey))
        {
            return;
        }
        if (!string.Equals(documentName, _currentDocumentName, StringComparison.OrdinalIgnoreCase)
            || _currentCacheScope != InkCacheScope.Presentation)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            _currentDocumentName = documentName;
            _currentDocumentPath = documentPath ?? string.Empty;
            _currentPageIndex = Math.Max(1, pageIndex);
            _currentCacheScope = InkCacheScope.Presentation;
            _currentCacheKey = nextKey;
            InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate);
            ResetInkHistory();
            _inkStrokes.Clear();
            RedrawInkSurface();
            LoadCurrentPageIfExists();
            return;
        }
        if (pageIndex != _currentPageIndex || !string.Equals(_currentCacheKey, nextKey, StringComparison.OrdinalIgnoreCase))
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            _currentPageIndex = Math.Max(1, pageIndex);
            _currentCacheKey = nextKey;
            ResetInkHistory();
            LoadCurrentPageIfExists();
        }
    }

    private void LoadCurrentPageIfExists()
    {
        if (!_inkCacheEnabled || string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            _inkStrokes.Clear();
            RedrawInkSurface();
            return;
        }
        var cache = _currentCacheScope == InkCacheScope.Photo ? _photoCache : _presentationCache;
        if (cache.TryGet(_currentCacheKey, out var cached))
        {
            ApplyInkStrokes(cached);
            return;
        }
        _inkStrokes.Clear();
        RedrawInkSurface();
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes)
    {
        _inkStrokes.Clear();
        _inkStrokes.AddRange(CloneInkStrokes(strokes));
        RedrawInkSurface();
    }

    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void TryAutoSave()
    {
        if (!_inkRecordEnabled)
        {
            return;
        }
    }

    private void SaveCurrentPageOnNavigate(bool forceBackground)
    {
        if (!_inkCacheEnabled || string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            return;
        }
        if (_inkStrokes.Count == 0)
        {
            var emptyCache = _currentCacheScope == InkCacheScope.Photo ? _photoCache : _presentationCache;
            emptyCache.Remove(_currentCacheKey);
            return;
        }
        var strokes = CloneInkStrokes(_inkStrokes);
        var cache = _currentCacheScope == InkCacheScope.Photo ? _photoCache : _presentationCache;
        cache.Set(_currentCacheKey, strokes);
    }

    private bool ShouldAutoSavePresentationBackground()
    {
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        return IsFullscreenPresentationWindow(target);
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

    private void SetReviewModeActive(bool active)
    {
        if (_reviewModeActive == active)
        {
            return;
        }
        _reviewModeActive = active;
        PhotoControlLayer.Visibility = _photoModeActive && !_reviewModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReviewModeChanged?.Invoke(active);
    }

    private void SetPhotoWindowMode(bool fullscreen)
    {
        _photoFullscreen = fullscreen;
        PhotoControlLayer.Visibility = _photoModeActive && !_reviewModeActive
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
        var rect = GetCurrentMonitorRect(useWorkArea: !fullscreen);
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

    private bool TryBeginPhotoPan(MouseButtonEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor)
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
        SavePhotoTransformState(userAdjusted: true);
        RedrawInkSurface();
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
        if (_photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            e.Handled = true;
            return;
        }
        WindowState = WindowState.Minimized;
        e.Handled = true;
    }

    private void OnPhotoPrevClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (TryNavigatePdf(-1))
        {
            return;
        }
        PhotoNavigationRequested?.Invoke(-1);
    }

    private void OnPhotoNextClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (TryNavigatePdf(1))
        {
            return;
        }
        PhotoNavigationRequested?.Invoke(1);
    }

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
            if (!_rememberPhotoTransform || !_photoUserTransformDirty)
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

    private bool TryOpenPdfDocument(string path)
    {
        try
        {
            _pdfDocument = PdfDocumentHost.Open(path);
            _pdfPageCount = _pdfDocument.PageCount;
            _pdfPageCache.Clear();
            _pdfPageOrder.Clear();
            return _pdfPageCount > 0;
        }
        catch
        {
            ClosePdfDocument();
            return false;
        }
    }

    private void ClosePdfDocument()
    {
        _pdfDocument?.Dispose();
        _pdfDocument = null;
        _pdfPageCount = 0;
        _pdfPageCache.Clear();
        _pdfPageOrder.Clear();
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
        if (!_rememberPhotoTransform || !_photoUserTransformDirty)
        {
            ApplyPhotoFitToViewport(bitmap, PdfDefaultDpi);
        }
        return true;
    }

    private BitmapSource? GetPdfPageBitmap(int pageIndex)
    {
        if (_pdfDocument == null || _pdfPageCount <= 0)
        {
            return null;
        }
        var safeIndex = Math.Clamp(pageIndex, 1, _pdfPageCount);
        if (_pdfPageCache.TryGetValue(safeIndex, out var cached))
        {
            TouchPdfCache(safeIndex);
            return cached;
        }
        var rendered = _pdfDocument.RenderPage(safeIndex, PdfDefaultDpi);
        if (rendered == null)
        {
            return null;
        }
        _pdfPageCache[safeIndex] = rendered;
        TouchPdfCache(safeIndex);
        if (_pdfPageOrder.Count > PdfCacheLimit)
        {
            var oldest = _pdfPageOrder.First?.Value;
            if (oldest.HasValue)
            {
                _pdfPageOrder.RemoveFirst();
                _pdfPageCache.Remove(oldest.Value);
            }
        }
        return rendered;
    }

    private void TouchPdfCache(int pageIndex)
    {
        var node = _pdfPageOrder.Find(pageIndex);
        if (node != null)
        {
            _pdfPageOrder.Remove(node);
        }
        _pdfPageOrder.AddLast(pageIndex);
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
            return true;
        }
        SaveCurrentPageOnNavigate(forceBackground: false);
        _currentPageIndex = next;
        _currentCacheKey = BuildPdfCacheKey(_currentDocumentPath, _currentPageIndex);
        ResetInkHistory();
        LoadCurrentPageIfExists();
        RenderPdfPage(_currentPageIndex);
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
            var monitor = GetCurrentMonitorRect(useWorkArea: false);
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
        RedrawInkSurface();
    }

    private void SavePhotoTransformState(bool userAdjusted)
    {
        _lastPhotoScaleX = _photoScale.ScaleX;
        _lastPhotoScaleY = _photoScale.ScaleY;
        _lastPhotoTranslateX = _photoTranslate.X;
        _lastPhotoTranslateY = _photoTranslate.Y;
        _photoUserTransformDirty = userAdjusted;
    }

    private void CaptureBackgroundImage(string imagePath)
    {
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        if (!target.IsValid)
        {
            return;
        }
        try
        {
            WindowCaptureHelper.TryCaptureWindow(target.Handle, imagePath);
        }
        catch
        {
            // Ignore capture exceptions.
        }
    }

    private static int? TryParseSlideIndexFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }
        var match = Regex.Match(title, @"(\d+)\s*/\s*\d+");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
        {
            return index;
        }
        match = Regex.Match(title, @"第\s*(\d+)\s*(页|张)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out index))
        {
            return index;
        }
        match = Regex.Match(title, @"\bSlide\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out index))
        {
            return index;
        }
        return null;
    }

    private static string BuildPresentationCacheKey(string documentName, string documentPath, int pageIndex, bool isWps)
    {
        if (pageIndex <= 0)
        {
            return string.Empty;
        }
        if (isWps)
        {
            var normalized = NormalizeWpsTitle(documentName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = documentName;
            }
            return $"wps|{normalized}|slide_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
        var docKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            try
            {
                docKey = IoPath.GetFullPath(documentPath);
            }
            catch
            {
                docKey = documentPath;
            }
        }
        if (string.IsNullOrWhiteSpace(docKey))
        {
            docKey = documentName;
        }
        return $"ppt|{docKey}|slide_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
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

    private static string NormalizeWpsTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }
        var normalized = Regex.Replace(title, @"\s*-\s*WPS.*$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s*\d+\s*/\s*\d+\s*$", string.Empty);
        normalized = Regex.Replace(normalized, @"\s*第\s*\d+\s*(页|张).*?$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s*Slide\s*\d+.*?$", string.Empty, RegexOptions.IgnoreCase);
        return normalized.Trim();
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
            _wpsPageIndex = Math.Max(1, _wpsPageIndex + (direction > 0 ? 1 : -1));
            MonitorInkContext();
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
        var shouldEnable = _presentationOptions.AllowWps && !IsBoardActive() && IsVisible;
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
        _inkStrokes.Add(stroke);
        TryAutoSave();
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
        _inkStrokes.Add(stroke);
        TryAutoSave();
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
        }
    }

    private void RedrawInkSurface()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        ClearSurface();
        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke);
        }
        _hasDrawing = _inkStrokes.Count > 0;
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
        if (suppressOverlays)
        {
            RenderStoredInkCore(renderGeometry, color, stroke.BrushSize, stroke.CalligraphySealEnabled);
            RenderStoredInkEdge(renderGeometry, color, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
            return;
        }
        if (stroke.CalligraphyInkBloomEnabled && stroke.Blooms.Count > 0)
        {
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
                var bloomBrush = new SolidColorBrush(color)
                {
                    Opacity = bloom.Opacity
                };
                bloomBrush.Freeze();
                RenderAndBlend(renderBloom, bloomBrush, null, erase: false, null, renderGeometry);
            }
        }
        RenderStoredInkCore(renderGeometry, color, stroke.BrushSize, stroke.CalligraphySealEnabled);
        RenderStoredInkEdge(renderGeometry, color, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
        RenderStoredInkLayers(renderGeometry, color, inkFlow, 0.28, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
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
        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelWidth = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        if (_rasterSurface != null
            && pixelWidth == _surfacePixelWidth
            && pixelHeight == _surfacePixelHeight)
        {
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
    }

    private void ClearSurface()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        var rect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        var stride = _surfacePixelWidth * 4;
        var clear = new byte[stride * _surfacePixelHeight];
        _rasterSurface.WritePixels(rect, clear, stride, 0);
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

                    if (suppressOverlays)
                    {
                        RenderInkCore(strokeGeometry, color, enableSeal: false);
                        RenderInkEdge(strokeGeometry, color, inkFlow, strokeDirection);
                        return;
                    }
                    if (_calligraphyInkBloomEnabled)
                    {
                        var blooms = calligraphyRenderer.GetInkBloomGeometries();
                        if (blooms != null)
                        {
                            foreach (var bloom in blooms)
                            {
                                var bloomBrush = new SolidColorBrush(color)
                                {
                                    Opacity = bloom.Opacity
                                };
                                bloomBrush.Freeze();
                                RenderAndBlend(bloom.Geometry, bloomBrush, null, erase: false, null, strokeGeometry);
                            }
                        }
                    }
                    RenderInkCore(strokeGeometry, color, enableSeal: true);
                    RenderInkEdge(strokeGeometry, color, inkFlow, strokeDirection);
                    RenderInkLayers(strokeGeometry, color, inkFlow, 0.28, strokeDirection);
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
        if (!TryRenderGeometry(geometry, fill, pen, opacityMask, clipGeometry, out var rect, out var pixels, out var stride))
        {
            return;
        }
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

    private bool TryRenderGeometry(
        Geometry geometry,
        MediaBrush? fill,
        MediaPen? pen,
        MediaBrush? opacityMask,
        Geometry? clipGeometry,
        out Int32Rect destRect,
        out byte[] pixels,
        out int stride)
    {
        destRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
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
        pixels = new byte[stride * destRect.Height];
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

    private static BitmapSource CreateInkNoiseTile(int size, double baseAlpha, double variation, int seed)
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
        var destPixels = new byte[destStride * rect.Height];
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
    }

    private void ApplyEraseMask(Int32Rect rect, byte[] maskPixels, int maskStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destPixels = new byte[destStride * rect.Height];
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
}
