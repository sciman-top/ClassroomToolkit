using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfBrush = System.Windows.Media.Brush;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{
    private static readonly MediaColor TransparentHitTestColor = MediaColor.FromArgb(1, 255, 255, 255);
    private static readonly BlurEffect CalligraphyWashEffect = CreateInkBleedEffect(8.0);
    private static readonly BlurEffect CalligraphyBridgeEffect = CreateInkBleedEffect(2.5);
    private static readonly BlurEffect CalligraphyCoreEffect = CreateInkBleedEffect(0.6);
    private static readonly BitmapCache CalligraphyInkBleedCache = CreateInkBleedCache();
    private static readonly SolidColorBrush CalligraphyCanvasBackground = CreateFrozenBrush(MediaColor.FromArgb(255, 0xF8, 0xF5, 0xEE));
    private static readonly SolidColorBrush CalligraphyWashBrush = CreateFrozenBrush(MediaColor.FromArgb(0x15, 0, 0, 0));
    private static readonly SolidColorBrush CalligraphyBridgeBrush = CreateFrozenBrush(MediaColor.FromArgb(0x40, 0x10, 0x10, 0x10));
    private static readonly SolidColorBrush CalligraphyCoreBrush = CreateFrozenBrush(MediaColor.FromArgb(0xFF, 0x15, 0x15, 0x15));
    private const int GwlStyle = -16;
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsCaption = 0x00C00000;
    private const uint MonitorDefaultToNearest = 2;
    private const int PresentationFocusMonitorIntervalMs = 500;
    private const int PresentationFocusCooldownMs = 1200;
    private IntPtr _hwnd;
    private bool _inputPassthroughEnabled;
    private bool _focusBlocked;
    private bool _forcePresentationForegroundOnFullscreen;
    private readonly DispatcherTimer _presentationFocusMonitor;
    private DateTime _nextPresentationFocusAttempt = DateTime.MinValue;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private sealed class PaintSnapshot
    {
        public PaintSnapshot(StrokeCollection strokes, List<ShapeSnapshot> shapes, List<CustomStrokeData> customStrokes)
        {
            Strokes = strokes;
            Shapes = shapes;
            CustomStrokes = customStrokes;
        }

        public StrokeCollection Strokes { get; }
        public List<ShapeSnapshot> Shapes { get; }
        public List<CustomStrokeData> CustomStrokes { get; }
    }

    private sealed class ShapeSnapshot
    {
        public PaintShapeType Type { get; init; }
        public WpfPoint Start { get; init; }
        public WpfPoint End { get; init; }
        public Rect Bounds { get; init; }
        public MediaColor StrokeColor { get; init; }
        public double StrokeThickness { get; init; }
        public DoubleCollection? DashArray { get; init; }
        public MediaColor? FillColor { get; init; }
        public string? PathData { get; init; }
        public string? CalligraphyGroupId { get; init; }
        public CalligraphyLayerRole? CalligraphyRole { get; init; }

        public static ShapeSnapshot? FromShape(Shape shape)
        {
            if (shape is Path path)
            {
                var tag = path.Tag as CalligraphyLayerTag;
                return new ShapeSnapshot
                {
                    Type = PaintShapeType.Path,
                    StrokeColor = ResolveColor(path.Fill), // Note: For our filled paths, the "stroke" is the fill
                    PathData = path.Data.ToString(),
                    CalligraphyGroupId = tag?.GroupId,
                    CalligraphyRole = tag?.Role
                };
            }

            if (shape is Line line)
            {
                return new ShapeSnapshot
                {
                    Type = line.StrokeDashArray?.Count > 0 ? PaintShapeType.DashedLine : PaintShapeType.Line,
                    Start = new WpfPoint(line.X1, line.Y1),
                    End = new WpfPoint(line.X2, line.Y2),
                    StrokeColor = ResolveColor(line.Stroke),
                    StrokeThickness = line.StrokeThickness,
                    DashArray = line.StrokeDashArray
                };
            }

            if (shape is System.Windows.Shapes.Rectangle rectangle)
            {
                var fill = ResolveColor(rectangle.Fill, allowTransparent: true);
                return new ShapeSnapshot
                {
                    Type = rectangle.Fill != null ? PaintShapeType.RectangleFill : PaintShapeType.Rectangle,
                    Bounds = new Rect(
                        System.Windows.Controls.Canvas.GetLeft(rectangle),
                        System.Windows.Controls.Canvas.GetTop(rectangle),
                        rectangle.Width,
                        rectangle.Height),
                    StrokeColor = ResolveColor(rectangle.Stroke),
                    StrokeThickness = rectangle.StrokeThickness,
                    DashArray = rectangle.StrokeDashArray,
                    FillColor = fill
                };
            }

            if (shape is Ellipse ellipse)
            {
                return new ShapeSnapshot
                {
                    Type = PaintShapeType.Ellipse,
                    Bounds = new Rect(
                        System.Windows.Controls.Canvas.GetLeft(ellipse),
                        System.Windows.Controls.Canvas.GetTop(ellipse),
                        ellipse.Width,
                        ellipse.Height),
                    StrokeColor = ResolveColor(ellipse.Stroke),
                    StrokeThickness = ellipse.StrokeThickness,
                    DashArray = ellipse.StrokeDashArray,
                    FillColor = ResolveColor(ellipse.Fill, allowTransparent: true)
                };
            }

            return null;
        }

        private static MediaColor ResolveColor(WpfBrush? brush, bool allowTransparent = false)
        {
            if (brush is SolidColorBrush solid)
            {
                if (allowTransparent || solid.Color.A > 0)
                {
                    return solid.Color;
                }
            }
            return Colors.Transparent;
        }
    }

    private enum CalligraphyLayerRole
    {
        Wash,
        Bridge,
        Core
    }

    private sealed class CalligraphyLayerTag
    {
        public CalligraphyLayerTag(string groupId, CalligraphyLayerRole role)
        {
            GroupId = groupId;
            Role = role;
        }

        public string GroupId { get; }
        public CalligraphyLayerRole Role { get; }
    }

    private sealed class CustomStrokeTag
    {
        public CustomStrokeTag(string groupId)
        {
            GroupId = groupId;
        }

        public string GroupId { get; }
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
    private readonly Stack<PaintSnapshot> _history = new();
    private bool _erasing;
    private bool _inkStrokeInProgress;

    private PaintBrushStyle _brushStyle = PaintBrushStyle.Standard;
    private IBrushRenderer? _activeRenderer;
    private DrawingVisualHost _visualHost;

    // 笔画数据存储（用于部分删除）
    private sealed class CustomStrokeData
    {
        public string GroupId { get; }
        public List<StrokePointData> Points { get; }
        public PaintBrushStyle Style { get; }
        public MediaColor Color { get; }
        public double BaseSize { get; }
        public Guid RendererId { get; }

        public CustomStrokeData(string groupId, List<StrokePointData> points, PaintBrushStyle style,
            MediaColor color, double baseSize, Guid rendererId)
        {
            GroupId = groupId;
            Points = points;
            Style = style;
            Color = color;
            BaseSize = baseSize;
            RendererId = rendererId;
        }
    }

    private readonly List<CustomStrokeData> _customStrokes = new();

    // 橡皮擦路径跟踪（用于部分擦除）
    private readonly List<WpfPoint> _eraserPath = new();
    private double _eraserSize = 24;
    private bool _isErasing;

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
        
        WindowState = WindowState.Maximized;
        _presentationFocusMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PresentationFocusMonitorIntervalMs)
        };
        _presentationFocusMonitor.Tick += (_, _) => MonitorPresentationFocus();
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
        InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
        InkLayer.DefaultDrawingAttributes = BuildDrawingAttributes(Colors.Red, 12, 255);
        InkLayer.EraserShape = new RectangleStylusShape(24, 24);
        InkLayer.StrokeCollected += OnStrokeCollected;
        InkLayer.StrokeErasing += OnStrokeErasing;
        InkLayer.MouseLeftButtonDown += OnMouseDown;
        InkLayer.MouseMove += OnMouseMove;
        InkLayer.MouseLeftButtonUp += OnMouseUp;
        InkLayer.StylusDown += OnStylusDown;
        InkLayer.StylusUp += OnStylusUp;
        MouseWheel += OnMouseWheel;
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
            StopWpsNavHook();
            _presentationFocusMonitor.Stop();
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
        OverlayRoot.IsHitTestVisible = mode != PaintToolMode.Cursor;
        InkLayer.IsHitTestVisible = mode != PaintToolMode.Cursor;
        InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.None;
        switch (mode)
        {
            case PaintToolMode.Brush:
                if (UseCustomBrushRenderer())
                {
                    // Custom brush modes manage rendering manually
                    InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                    InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.None;
                }
                else
                {
                    InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
                    InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.Ink;
                }
                break;
            case PaintToolMode.Eraser:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.None;
                break;
            case PaintToolMode.Shape:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                break;
            case PaintToolMode.RegionErase:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                break;
            default:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                break;
        }
        if (mode != PaintToolMode.RegionErase)
        {
            ClearRegionSelection();
        }
        UpdateInputPassthrough();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void SetBrush(MediaColor color, double size, byte opacity)
    {
        InkLayer.DefaultDrawingAttributes = BuildDrawingAttributes(color, size, opacity);
    }

    public void SetEraserSize(double size)
    {
        var value = Math.Max(4, size);
        _eraserSize = value; // 保存橡皮擦大小（用于部分擦除）
        InkLayer.EraserShape = new RectangleStylusShape(value, value);
    }

    public void SetShapeType(PaintShapeType type)
    {
        _shapeType = type;
    }

    public void SetBoardColor(MediaColor color)
    {
        _boardColor = color;
        UpdateBoardBackground();
    }

    public void ClearAll()
    {
        if (InkLayer.Strokes.Count > 0 || ShapeCanvas.Children.Count > 0)
        {
            PushHistory();
        }
        InkLayer.Strokes.Clear();
        ShapeCanvas.Children.Clear();
        _visualHost.Clear();
        if (_activeRenderer != null)
        {
            _activeRenderer.Reset();
        }
    }

    public MediaColor CurrentBrushColor => InkLayer.DefaultDrawingAttributes.Color;
    public byte CurrentBrushOpacity => InkLayer.DefaultDrawingAttributes.Color.A;

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_mode == PaintToolMode.RegionErase)
        {
            e.Handled = true;
            _regionStart = e.GetPosition(ShapeCanvas);
            _regionRect = new WpfRectangle
            {
                Stroke = new SolidColorBrush(MediaColor.FromArgb(200, 255, 200, 60)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Fill = new SolidColorBrush(MediaColor.FromArgb(30, 255, 200, 60))
            };
            System.Windows.Controls.Canvas.SetLeft(_regionRect, _regionStart.X);
            System.Windows.Controls.Canvas.SetTop(_regionRect, _regionStart.Y);
            ShapeCanvas.Children.Add(_regionRect);
            _isRegionSelecting = true;
            InkLayer.CaptureMouse();
            return;
        }
        if (_mode == PaintToolMode.Brush)
        {
            if (UseCustomBrushRenderer())
            {
                if (_activeRenderer != null)
                {
                    var attr = InkLayer.DefaultDrawingAttributes;
                    _activeRenderer.Initialize(attr.Color, attr.Width, attr.Color.A);
                    _activeRenderer.OnDown(e.GetPosition(CustomDrawHost));
                    InkLayer.CaptureMouse(); // Capture on InkLayer because we are hooking its events
                    e.Handled = true;
                }
                return;
            }
            EnsureInkHistory();
        }
        if (_mode == PaintToolMode.Eraser)
        {
            // 开始橡皮擦操作：记录路径起点
            _eraserPath.Clear();
            _eraserPath.Add(e.GetPosition(ShapeCanvas));
            _isErasing = true;
            PushHistory(); // 保存当前状态
            ShapeCanvas.CaptureMouse();
        }
        if (_mode != PaintToolMode.Shape)
        {
            return;
        }
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        PushHistory();
        _shapeStart = e.GetPosition(ShapeCanvas);
        _activeShape = CreateShape(_shapeType);
        if (_activeShape == null)
        {
            return;
        }
        ApplyShapeStyle(_activeShape);
        ShapeCanvas.Children.Add(_activeShape);
        _isDrawingShape = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_mode == PaintToolMode.Brush && _activeRenderer != null && _activeRenderer.IsActive && UseCustomBrushRenderer())
        {
            _activeRenderer.OnMove(e.GetPosition(CustomDrawHost));
            _visualHost.UpdateVisual(_activeRenderer.Render);
            e.Handled = true;
            return;
        }

        if (_mode == PaintToolMode.RegionErase && _isRegionSelecting && _regionRect != null)
        {
            e.Handled = true;
            var regionPosition = e.GetPosition(ShapeCanvas);
            UpdateSelectionRect(_regionRect, _regionStart, regionPosition);
            return;
        }
        if (_mode == PaintToolMode.Eraser && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var point = e.GetPosition(ShapeCanvas);

            // 记录橡皮擦路径（用于部分擦除）
            if (_isErasing && _eraserPath.Count > 0)
            {
                var lastPoint = _eraserPath.Last();
                var distance = (point - lastPoint).Length;

                // 只有移动距离超过一定阈值时才记录点（避免点过于密集）
                if (distance > _eraserSize * 0.2)
                {
                    _eraserPath.Add(point);
                }
            }
        }
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        var shapePosition = e.GetPosition(ShapeCanvas);
        UpdateShape(_activeShape, _shapeStart, shapePosition);
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_mode == PaintToolMode.Brush && _activeRenderer != null && _activeRenderer.IsActive && UseCustomBrushRenderer())
        {
            PushHistory(); // Save state before adding new shape

            _activeRenderer.OnUp(e.GetPosition(CustomDrawHost));
            var geometry = _activeRenderer.GetLastStrokeGeometry();
            var points = _activeRenderer.GetLastStrokePoints();

            if (geometry != null && points != null)
            {
                var attr = InkLayer.DefaultDrawingAttributes;
                var groupId = Guid.NewGuid().ToString("N");
                var baseWidth = Math.Max(attr.Width, attr.Height);

                // 保存笔画数据（用于部分删除）
                var strokeData = new CustomStrokeData(
                    groupId,
                    points,
                    _brushStyle,
                    attr.Color,
                    baseWidth,
                    Guid.NewGuid() // 唯一标识此笔画
                );
                _customStrokes.Add(strokeData);

                if (_brushStyle == PaintBrushStyle.Calligraphy)
                {
                    DrawStrokeToCanvas(ShapeCanvas, geometry, baseWidth);
                }
                else
                {
                    DrawMarkerStrokeToCanvas(ShapeCanvas, geometry, attr.Color);
                }
            }

            _activeRenderer.Reset();
            _visualHost.Clear();
            InkLayer.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_mode == PaintToolMode.RegionErase && _isRegionSelecting)
        {
            e.Handled = true;
            _isRegionSelecting = false;
            var end = e.GetPosition(ShapeCanvas);
            var region = BuildRegionRect(_regionStart, end);
            ClearRegionSelection();
            if (region.Width > 2 && region.Height > 2)
            {
                EraseRegion(region);
            }
            _erasing = false;
            if (InkLayer.IsMouseCaptured)
            {
                InkLayer.ReleaseMouseCapture();
            }
            return;
        }

        // 橡皮擦部分擦除处理
        if (_mode == PaintToolMode.Eraser && _isErasing)
        {
            e.Handled = true;
            _isErasing = false;

            if (ShapeCanvas.IsMouseCaptured)
            {
                ShapeCanvas.ReleaseMouseCapture();
            }

            // 执行橡皮擦路径的部分擦除
            if (_eraserPath.Count > 0)
            {
                // 1. 擦除 InkCanvas 的内置笔画（使用路径上的矩形区域）
                EraseInkStrokesAlongPath();

                // 2. 处理自定义笔画的部分擦除
                ProcessEraserPathPartialErase();

                // 3. 处理图形工具绘制的形状（线/矩形/椭圆等）
                EraseShapesAlongPath();
            }

            _eraserPath.Clear();
            return;
        }
        if (_mode == PaintToolMode.Brush)
        {
            _inkStrokeInProgress = false;
        }
        if (_mode == PaintToolMode.Shape)
        {
            _isDrawingShape = false;
            _activeShape = null;
        }
        _erasing = false;
    }

    private static BlurEffect CreateInkBleedEffect(double radius)
    {
        var effect = new BlurEffect
        {
            Radius = radius,
            KernelType = KernelType.Gaussian,
            RenderingBias = RenderingBias.Quality
        };
        effect.Freeze();
        return effect;
    }

    private static SolidColorBrush CreateFrozenBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static BitmapCache CreateInkBleedCache()
    {
        var cache = new BitmapCache { RenderAtScale = 1.0 };
        cache.Freeze();
        return cache;
    }

    public void DrawStrokeToCanvas(Canvas canvas, Geometry geo, double baseWidth)
    {
        if (canvas.Background == null)
        {
            canvas.Background = CalligraphyCanvasBackground;
        }
        if (canvas.CacheMode == null)
        {
            canvas.CacheMode = CalligraphyInkBleedCache;
        }

        var groupId = Guid.NewGuid().ToString("N");

        var washPath = new Path
        {
            Data = geo,
            Fill = CalligraphyWashBrush,
            Effect = CalligraphyWashEffect,
            CacheMode = CalligraphyInkBleedCache,
            Tag = new CalligraphyLayerTag(groupId, CalligraphyLayerRole.Wash),
            IsHitTestVisible = false
        };
        var bridgePath = new Path
        {
            Data = geo,
            Fill = CalligraphyBridgeBrush,
            Effect = CalligraphyBridgeEffect,
            CacheMode = CalligraphyInkBleedCache,
            Tag = new CalligraphyLayerTag(groupId, CalligraphyLayerRole.Bridge),
            IsHitTestVisible = false
        };
        var corePath = new Path
        {
            Data = geo,
            Fill = CalligraphyCoreBrush,
            Effect = CalligraphyCoreEffect,
            CacheMode = CalligraphyInkBleedCache,
            Tag = new CalligraphyLayerTag(groupId, CalligraphyLayerRole.Core)
        };

        canvas.Children.Add(washPath);
        canvas.Children.Add(bridgePath);
        canvas.Children.Add(corePath);
    }

    private void DrawMarkerStrokeToCanvas(Canvas canvas, Geometry geo, MediaColor color)
    {
        var markerColor = MediaColor.FromArgb(Math.Min(color.A, (byte)0xE6), color.R, color.G, color.B);
        var brush = new SolidColorBrush(markerColor);
        brush.Freeze();

        var path = new Path
        {
            Data = geo,
            Fill = brush
        };

        canvas.Children.Add(path);
    }

    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
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

    public void Undo()
    {
        if (_history.Count == 0)
        {
            return;
        }
        var snapshot = _history.Pop();
        InkLayer.Strokes.Clear();
        InkLayer.Strokes.Add(snapshot.Strokes);
        ShapeCanvas.Children.Clear();
        RestoreShapes(snapshot.Shapes);

        // 恢复自定义笔画数据
        _customStrokes.Clear();
        foreach (var stroke in snapshot.CustomStrokes)
        {
            // 深拷贝笔画点数据
            var pointsCopy = new List<StrokePointData>(stroke.Points.Count);
            foreach (var point in stroke.Points)
            {
                pointsCopy.Add(new StrokePointData(point.Position, point.Width));
            }
            var strokeCopy = new CustomStrokeData(
                stroke.GroupId,
                pointsCopy,
                stroke.Style,
                stroke.Color,
                stroke.BaseSize,
                stroke.RendererId
            );
            _customStrokes.Add(strokeCopy);
        }
    }

    public void SetBrushOpacity(byte opacity)
    {
        var current = InkLayer.DefaultDrawingAttributes;
        var color = current.Color;
        color.A = opacity;
        current.Color = color;
        InkLayer.DefaultDrawingAttributes = current;
    }

    public void SetBrushStyle(PaintBrushStyle style)
    {
        _brushStyle = style;

        if (_brushStyle == PaintBrushStyle.Calligraphy)
        {
            if (_activeRenderer is not VariableWidthBrushRenderer)
            {
                _activeRenderer = new VariableWidthBrushRenderer();
            }
        }
        else
        {
            if (_activeRenderer is not MarkerBrushRenderer)
            {
                _activeRenderer = new MarkerBrushRenderer();
            }
        }
        
        // Refresh mode to apply correct input handling
        SetMode(_mode);
    }

    private bool UseCustomBrushRenderer()
    {
        return _brushStyle == PaintBrushStyle.Calligraphy || _brushStyle == PaintBrushStyle.Standard;
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
        var enable = _mode == PaintToolMode.Cursor && _boardOpacity == 0;
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
        if (ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.IsForeground(target.Handle))
        {
            return false;
        }
        return ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.EnsureForeground(target.Handle);
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
        if (IsBoardActive() || _mode == PaintToolMode.Cursor || direction == 0)
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
        if (IsBoardActive() || _mode == PaintToolMode.Cursor)
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
        var shouldEnable = _presentationOptions.AllowWps && !IsBoardActive() && _mode != PaintToolMode.Cursor && IsVisible;
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
        if (shouldEnable && sendMode == ClassroomToolkit.Interop.Presentation.InputStrategy.Raw)
        {
            blockOnly = true;
            if (IsTargetForeground(target))
            {
                interceptKeyboard = false;
                if (!wheelForward)
                {
                    interceptWheel = false;
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

    private void OnStrokeCollected(object? sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_mode == PaintToolMode.Brush)
        {
            if (!_inkStrokeInProgress)
            {
                var strokes = new StrokeCollection(InkLayer.Strokes);
                strokes.Remove(e.Stroke);
                var shapes = CaptureShapes();
                // 深拷贝 _customStrokes
                var customStrokes = new List<CustomStrokeData>(_customStrokes.Count);
                foreach (var stroke in _customStrokes)
                {
                    var pointsCopy = new List<StrokePointData>(stroke.Points.Count);
                    foreach (var point in stroke.Points)
                    {
                        pointsCopy.Add(new StrokePointData(point.Position, point.Width));
                    }
                    var strokeCopy = new CustomStrokeData(
                        stroke.GroupId,
                        pointsCopy,
                        stroke.Style,
                        stroke.Color,
                        stroke.BaseSize,
                        stroke.RendererId
                    );
                    customStrokes.Add(strokeCopy);
                }
                _history.Push(new PaintSnapshot(strokes, shapes, customStrokes));
            }
        }
        _inkStrokeInProgress = false;
    }

    private void OnStrokeErasing(object? sender, InkCanvasStrokeErasingEventArgs e)
    {
        if (_erasing)
        {
            return;
        }
        _erasing = true;
        PushHistory();
    }

    private void OnStylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
    {
        if (_mode == PaintToolMode.Brush)
        {
            EnsureInkHistory();
        }
    }

    private void OnStylusUp(object sender, System.Windows.Input.StylusEventArgs e)
    {
        if (_mode == PaintToolMode.Brush)
        {
            _inkStrokeInProgress = false;
        }
    }

    private static DrawingAttributes BuildDrawingAttributes(MediaColor color, double size, byte opacity)
    {
        var drawing = new DrawingAttributes
        {
            Color = MediaColor.FromArgb(opacity, color.R, color.G, color.B),
            Width = size,
            Height = size,
            FitToCurve = true,
            IgnorePressure = true
        };
        return drawing;
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
            PaintShapeType.Path => new Path(),
            _ => null
        };
    }

    private void ApplyShapeStyle(Shape shape)
    {
        var attributes = InkLayer.DefaultDrawingAttributes;
        var stroke = new SolidColorBrush(attributes.Color);
        shape.Stroke = stroke;
        shape.StrokeThickness = Math.Max(1, attributes.Width);
        if (_shapeType == PaintShapeType.DashedLine)
        {
            shape.StrokeDashArray = new DoubleCollection { 6, 4 };
        }
        if (_shapeType == PaintShapeType.RectangleFill)
        {
            var fillColor = attributes.Color;
            fillColor.A = 60;
            shape.Fill = new SolidColorBrush(fillColor);
        }
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

    private void RemoveShapeAt(WpfPoint point)
    {
        var hit = VisualTreeHelper.HitTest(ShapeCanvas, point);
        if (hit?.VisualHit is Shape shape)
        {
            // 检查是否是自定义笔画（有 CalligraphyLayerTag）
            if (shape.Tag is CalligraphyLayerTag)
            {
                // 自定义笔画不在这里删除，而是在鼠标抬起时使用 ProcessEraserPathPartialErase 进行部分删除
                return;
            }

            // 其他形状（矩形、椭圆、线条等）立即删除
            if (!_erasing)
            {
                _erasing = true;
                PushHistory();
            }

            ShapeCanvas.Children.Remove(shape);
        }
    }

    private void EraseRegion(Rect region)
    {
        PushHistory();

        // 1. 处理 InkCanvas 的内置笔画
        if (InkLayer.Strokes.Count > 0)
        {
            InkLayer.Strokes.Erase(region);
        }

        // 2. 处理自定义笔画（部分删除）
        ProcessCustomStrokesPartialErase(region);

        // 3. 处理其他形状（完全删除）
        // 注意：不处理 CalligraphyLayerTag 的形状，因为它们已经被步骤2处理过了
        var shapes = ShapeCanvas.Children.OfType<Shape>().ToList();
        foreach (var shape in shapes)
        {
            if (shape == _regionRect)
            {
                continue;
            }

            // 跳过自定义笔画（有 CalligraphyLayerTag），它们已经被 ProcessCustomStrokesPartialErase 处理
            if (shape.Tag is CalligraphyLayerTag)
            {
                continue;
            }

            if (IsShapeHit(region, shape))
            {
                ShapeCanvas.Children.Remove(shape);
            }
        }
    }

    /// <summary>
    /// 对自定义笔画进行部分删除
    /// </summary>
    private void ProcessCustomStrokesPartialErase(Rect eraseRegion)
    {
        if (_customStrokes.Count == 0) return;

        // 需要删除的笔画索引（倒序遍历以安全删除）
        for (int i = _customStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _customStrokes[i];
            if (!IsStrokeIntersectRegion(stroke, eraseRegion)) continue;

            // 分割笔画：找出在删除区域外的段落
            var segments = SplitStrokeOutsideRegion(stroke, eraseRegion);

            // 移除原笔画
            _customStrokes.RemoveAt(i);
            RemoveCalligraphyGroupById(stroke.GroupId);

            // 重新绘制未删除的段落
            foreach (var segment in segments)
            {
                RedrawStrokeSegment(segment, stroke.Style, stroke.Color, stroke.BaseSize);
            }
        }
    }

    /// <summary>
    /// 判断笔画是否与删除区域相交
    /// </summary>
    private bool IsStrokeIntersectRegion(CustomStrokeData stroke, Rect region)
    {
        foreach (var point in stroke.Points)
        {
            if (region.Contains(point.Position))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 将笔画分割成多个段落，只保留删除区域外的部分
    /// </summary>
    private List<List<StrokePointData>> SplitStrokeOutsideRegion(CustomStrokeData stroke, Rect eraseRegion)
    {
        var segments = new List<List<StrokePointData>>();
        var currentSegment = new List<StrokePointData>();
        bool isInEraseRegion = false;

        foreach (var point in stroke.Points)
        {
            bool pointInRegion = eraseRegion.Contains(point.Position);

            if (pointInRegion != isInEraseRegion)
            {
                // 状态改变：进入/离开删除区域
                if (currentSegment.Count > 0)
                {
                    // 保存当前段落
                    if (!isInEraseRegion) // 只保留删除区域外的段落
                    {
                        segments.Add(new List<StrokePointData>(currentSegment));
                    }
                    currentSegment.Clear();
                }

                isInEraseRegion = pointInRegion;
            }

            if (!isInEraseRegion)
            {
                currentSegment.Add(point);
            }
        }

        // 添加最后一个段落
        if (currentSegment.Count > 0)
        {
            segments.Add(currentSegment);
        }

        return segments;
    }

    /// <summary>
    /// 重新绘制笔画段落
    /// </summary>
    private void RedrawStrokeSegment(List<StrokePointData> segment, PaintBrushStyle style, MediaColor color, double baseSize)
    {
        if (segment.Count < 2) return;

        // 创建临时渲染器
        IBrushRenderer renderer = CreateRendererForStroke(style);
        renderer.Initialize(color, baseSize, 1.0);

        // 重建笔画点数据
        renderer.OnDown(segment[0].Position);
        for (int i = 1; i < segment.Count; i++)
        {
            renderer.OnMove(segment[i].Position);
        }
        renderer.OnUp(segment.Last().Position);

        // 获取几何并绘制
        var geometry = renderer.GetLastStrokeGeometry();
        if (geometry != null)
        {
            var groupId = Guid.NewGuid().ToString("N");

            if (style == PaintBrushStyle.Calligraphy)
            {
                DrawStrokeToCanvas(ShapeCanvas, geometry, baseSize);
            }
            else
            {
                DrawMarkerStrokeToCanvas(ShapeCanvas, geometry, color);
            }

            // 保存新的笔画数据
            var newStrokeData = new CustomStrokeData(
                groupId,
                segment,
                style,
                color,
                baseSize,
                Guid.NewGuid()
            );
            _customStrokes.Add(newStrokeData);
        }
    }

    /// <summary>
    /// 创建临时渲染器用于重绘笔画段落
    /// </summary>
    private IBrushRenderer CreateRendererForStroke(PaintBrushStyle style)
    {
        if (style == PaintBrushStyle.Calligraphy)
        {
            return new VariableWidthBrushRenderer();
        }
        return new MarkerBrushRenderer();
    }

    /// <summary>
    /// 根据GroupId删除笔画
    /// </summary>
    private void RemoveCalligraphyGroupById(string groupId)
    {
        var shapes = ShapeCanvas.Children.OfType<Shape>().ToList();
        foreach (var shape in shapes)
        {
            if (shape.Tag is CalligraphyLayerTag tag && tag.GroupId == groupId)
            {
                ShapeCanvas.Children.Remove(shape);
                continue;
            }

            if (shape.Tag is CustomStrokeTag customTag && customTag.GroupId == groupId)
            {
                ShapeCanvas.Children.Remove(shape);
            }
        }
    }

    /// <summary>
    /// 沿橡皮擦路径擦除 InkCanvas 的内置笔画
    /// </summary>
    private void EraseInkStrokesAlongPath()
    {
        if (InkLayer.Strokes.Count == 0 || _eraserPath.Count == 0) return;

        var toRemove = new List<Stroke>();
        var toAdd = new List<Stroke>();

        foreach (var stroke in InkLayer.Strokes)
        {
            if (!IsInkStrokeNearEraserPath(stroke))
            {
                continue;
            }

            var segments = SplitInkStrokeOutsideEraserPath(stroke);
            toRemove.Add(stroke);

            foreach (var segment in segments)
            {
                if (segment.Count < 2)
                {
                    continue;
                }
                var attrs = stroke.DrawingAttributes.Clone();
                toAdd.Add(new Stroke(segment, attrs));
            }
        }

        foreach (var stroke in toRemove)
        {
            InkLayer.Strokes.Remove(stroke);
        }
        if (toAdd.Count > 0)
        {
            InkLayer.Strokes.Add(new StrokeCollection(toAdd));
        }
    }

    private void EraseShapesAlongPath()
    {
        if (_eraserPath.Count == 0) return;

        var eraserGeometry = BuildEraserGeometry();
        if (eraserGeometry == null) return;

        var shapes = ShapeCanvas.Children.OfType<Shape>().ToList();
        foreach (var shape in shapes)
        {
            if (shape == _regionRect)
            {
                continue;
            }

            if (shape.Tag is CalligraphyLayerTag || shape.Tag is CustomStrokeTag)
            {
                continue;
            }

            if (!TryGetShapeGeometry(shape, out var geometry))
            {
                continue;
            }

            var strokeBrush = shape.Stroke;
            var fillBrush = shape.Fill;
            bool hasStroke = strokeBrush != null && shape.StrokeThickness > 0.1;
            bool hasFill = IsBrushVisible(fillBrush);

            Geometry? fillRemaining = null;
            Geometry? strokeRemaining = null;

            if (hasFill)
            {
                fillRemaining = Geometry.Combine(geometry, eraserGeometry, GeometryCombineMode.Exclude, null);
            }

            if (hasStroke)
            {
                var pen = CreateShapePen(shape, strokeBrush!);
                var strokeGeometry = geometry.GetWidenedPathGeometry(pen);
                strokeRemaining = Geometry.Combine(strokeGeometry, eraserGeometry, GeometryCombineMode.Exclude, null);
            }

            int index = ShapeCanvas.Children.IndexOf(shape);
            ShapeCanvas.Children.Remove(shape);

            bool added = false;
            int insertIndex = Math.Max(0, index);

            if (hasFill && fillRemaining != null && IsGeometryVisible(fillRemaining))
            {
                ShapeCanvas.Children.Insert(insertIndex++, CreateFillPath(fillRemaining, fillBrush!));
                added = true;
            }

            if (hasStroke && strokeRemaining != null && IsGeometryVisible(strokeRemaining))
            {
                ShapeCanvas.Children.Insert(insertIndex++, CreateFillPath(strokeRemaining, strokeBrush!));
                added = true;
            }

            if (!added)
            {
                continue;
            }
        }
    }

    private Geometry? BuildEraserGeometry()
    {
        if (_eraserPath.Count == 0) return null;

        if (_eraserPath.Count == 1)
        {
            double radius = Math.Max(_eraserSize * 0.5, 0.1);
            return new EllipseGeometry(_eraserPath[0], radius, radius);
        }

        var pathGeometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = pathGeometry.Open())
        {
            ctx.BeginFigure(_eraserPath[0], isFilled: false, isClosed: false);
            for (int i = 1; i < _eraserPath.Count; i++)
            {
                ctx.LineTo(_eraserPath[i], isStroked: true, isSmoothJoin: true);
            }
        }

        var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, Math.Max(1, _eraserSize))
        {
            StartLineCap = System.Windows.Media.PenLineCap.Round,
            EndLineCap = System.Windows.Media.PenLineCap.Round,
            LineJoin = System.Windows.Media.PenLineJoin.Round
        };
        return pathGeometry.GetWidenedPathGeometry(pen);
    }

    private static bool TryGetShapeGeometry(Shape shape, out Geometry geometry)
    {
        geometry = Geometry.Empty;

        if (shape is Line line)
        {
            geometry = new LineGeometry(new WpfPoint(line.X1, line.Y1), new WpfPoint(line.X2, line.Y2));
            return true;
        }

        if (shape is System.Windows.Shapes.Rectangle rect)
        {
            var left = System.Windows.Controls.Canvas.GetLeft(rect);
            var top = System.Windows.Controls.Canvas.GetTop(rect);
            geometry = new RectangleGeometry(new Rect(left, top, rect.Width, rect.Height));
            return true;
        }

        if (shape is Ellipse ellipse)
        {
            var left = System.Windows.Controls.Canvas.GetLeft(ellipse);
            var top = System.Windows.Controls.Canvas.GetTop(ellipse);
            geometry = new EllipseGeometry(new Rect(left, top, ellipse.Width, ellipse.Height));
            return true;
        }

        if (shape is Path path && path.Data != null)
        {
            geometry = path.Data;
            return true;
        }

        return false;
    }

    private static System.Windows.Media.Pen CreateShapePen(Shape shape, System.Windows.Media.Brush stroke)
    {
        var pen = new System.Windows.Media.Pen(stroke, Math.Max(1, shape.StrokeThickness))
        {
            StartLineCap = shape.StrokeStartLineCap,
            EndLineCap = shape.StrokeEndLineCap,
            LineJoin = shape.StrokeLineJoin,
            MiterLimit = shape.StrokeMiterLimit
        };

        if (shape.StrokeDashArray != null && shape.StrokeDashArray.Count > 0)
        {
            pen.DashStyle = new DashStyle(shape.StrokeDashArray, 0);
        }

        return pen;
    }

    private static Path CreateFillPath(Geometry geometry, System.Windows.Media.Brush fill)
    {
        return new Path
        {
            Data = geometry,
            Fill = fill,
            StrokeThickness = 0
        };
    }

    private static bool IsBrushVisible(System.Windows.Media.Brush? brush)
    {
        if (brush == null)
        {
            return false;
        }

        if (brush is SolidColorBrush solid)
        {
            return solid.Color.A > 0;
        }

        return true;
    }

    private static bool IsGeometryVisible(Geometry geometry)
    {
        var bounds = geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return false;
        }
        return bounds.Width > 0.1 || bounds.Height > 0.1;
    }

    /// <summary>
    /// 基于橡皮擦路径的部分擦除
    /// </summary>
    private void ProcessEraserPathPartialErase()
    {
        if (_customStrokes.Count == 0 || _eraserPath.Count == 0) return;

        // 需要删除的笔画索引（倒序遍历以安全删除）
        for (int i = _customStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _customStrokes[i];
            if (!IsStrokeNearEraserPath(stroke)) continue;

            // 分割笔画：找出不在橡皮擦路径附近的段落
            var segments = SplitStrokeOutsideEraserPath(stroke);

            // 移除原笔画
            _customStrokes.RemoveAt(i);
            RemoveCalligraphyGroupById(stroke.GroupId);

            // 重新绘制未删除的段落
            foreach (var segment in segments)
            {
                RedrawStrokeSegment(segment, stroke.Style, stroke.Color, stroke.BaseSize);
            }
        }
    }

    /// <summary>
    /// 判断笔画是否靠近橡皮擦路径
    /// </summary>
    private bool IsStrokeNearEraserPath(CustomStrokeData stroke)
    {
        foreach (var point in stroke.Points)
        {
            if (IsPointNearEraserPath(point.Position))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 判断点是否靠近橡皮擦路径
    /// </summary>
    private bool IsPointNearEraserPath(WpfPoint point)
    {
        double eraserRadius = _eraserSize * 0.5;

        for (int i = 0; i < _eraserPath.Count; i++)
        {
            var eraserPoint = _eraserPath[i];

            // 检查点是否在橡皮擦点的圆形范围内
            double distance = (point - eraserPoint).Length;
            if (distance <= eraserRadius)
            {
                return true;
            }

            // 检查点是否在橡皮擦路径段的附近
            if (i < _eraserPath.Count - 1)
            {
                var nextEraserPoint = _eraserPath[i + 1];
                if (IsPointNearLineSegment(point, eraserPoint, nextEraserPoint, eraserRadius))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsInkStrokeNearEraserPath(Stroke stroke)
    {
        foreach (var point in stroke.StylusPoints)
        {
            if (IsPointNearEraserPath(new WpfPoint(point.X, point.Y)))
            {
                return true;
            }
        }
        return false;
    }

    private List<StylusPointCollection> SplitInkStrokeOutsideEraserPath(Stroke stroke)
    {
        var segments = new List<StylusPointCollection>();
        var current = new StylusPointCollection();
        bool isNearEraser = false;

        foreach (var point in stroke.StylusPoints)
        {
            bool pointNearEraser = IsPointNearEraserPath(new WpfPoint(point.X, point.Y));

            if (pointNearEraser != isNearEraser)
            {
                if (current.Count > 0 && !isNearEraser)
                {
                    segments.Add(current);
                }
                current = new StylusPointCollection();
                isNearEraser = pointNearEraser;
            }

            if (!isNearEraser)
            {
                current.Add(point);
            }
        }

        if (current.Count > 0 && !isNearEraser)
        {
            segments.Add(current);
        }

        return segments;
    }

    /// <summary>
    /// 判断点是否靠近线段
    /// </summary>
    private static bool IsPointNearLineSegment(WpfPoint point, WpfPoint lineStart, WpfPoint lineEnd, double threshold)
    {
        var lineVec = lineEnd - lineStart;
        var pointVec = point - lineStart;

        double lineLength = lineVec.Length;
        if (lineLength < 0.001) return false;

        lineVec.Normalize();

        // 计算投影
        double projection = pointVec.X * lineVec.X + pointVec.Y * lineVec.Y;

        // 找到线段上最近的点
        WpfPoint closestPoint;
        if (projection <= 0)
        {
            closestPoint = lineStart;
        }
        else if (projection >= lineLength)
        {
            closestPoint = lineEnd;
        }
        else
        {
            closestPoint = lineStart + lineVec * projection;
        }

        // 计算距离
        double distance = (point - closestPoint).Length;
        return distance <= threshold;
    }

    /// <summary>
    /// 将笔画分割成多个段落，只保留不在橡皮擦路径附近的部分
    /// </summary>
    private List<List<StrokePointData>> SplitStrokeOutsideEraserPath(CustomStrokeData stroke)
    {
        var segments = new List<List<StrokePointData>>();
        var currentSegment = new List<StrokePointData>();
        bool isNearEraser = false;

        foreach (var point in stroke.Points)
        {
            bool pointNearEraser = IsPointNearEraserPath(point.Position);

            if (pointNearEraser != isNearEraser)
            {
                // 状态改变：进入/离开橡皮擦影响范围
                if (currentSegment.Count > 0)
                {
                    // 保存当前段落
                    if (!isNearEraser) // 只保留不在橡皮擦范围内的段落
                    {
                        segments.Add(new List<StrokePointData>(currentSegment));
                    }
                    currentSegment.Clear();
                }

                isNearEraser = pointNearEraser;
            }

            if (!isNearEraser)
            {
                currentSegment.Add(point);
            }
        }

        // 添加最后一个段落
        if (currentSegment.Count > 0)
        {
            segments.Add(currentSegment);
        }

        return segments;
    }

    private void RemoveCalligraphyGroup(Shape shape)
    {
        if (shape.Tag is CalligraphyLayerTag tag)
        {
            RemoveCalligraphyGroups(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tag.GroupId });
            return;
        }

        ShapeCanvas.Children.Remove(shape);
    }

    private void RemoveCalligraphyGroups(HashSet<string> groupIds)
    {
        var toRemove = ShapeCanvas.Children
            .OfType<Shape>()
            .Where(s => s.Tag is CalligraphyLayerTag tag && groupIds.Contains(tag.GroupId))
            .ToList();

        foreach (var shape in toRemove)
        {
            ShapeCanvas.Children.Remove(shape);
        }
    }

    private static bool IsShapeHit(Rect region, Shape shape)
    {
        try
        {
            var bounds = shape.RenderedGeometry.Bounds;
            var transform = shape.TransformToAncestor((Visual)shape.Parent);
            var transformed = transform.TransformBounds(bounds);
            return region.IntersectsWith(transformed);
        }
        catch
        {
            return false;
        }
    }

    private void ClearRegionSelection()
    {
        if (_regionRect != null)
        {
            ShapeCanvas.Children.Remove(_regionRect);
            _regionRect = null;
        }
        _isRegionSelecting = false;
        if (InkLayer.IsMouseCaptured)
        {
            InkLayer.ReleaseMouseCapture();
        }
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
        var strokes = new StrokeCollection(InkLayer.Strokes);
        var shapes = CaptureShapes();
        // 深拷贝 _customStrokes，避免后续修改影响快照
        var customStrokes = new List<CustomStrokeData>(_customStrokes.Count);
        foreach (var stroke in _customStrokes)
        {
            // 深拷贝笔画点数据
            var pointsCopy = new List<StrokePointData>(stroke.Points.Count);
            foreach (var point in stroke.Points)
            {
                pointsCopy.Add(new StrokePointData(point.Position, point.Width));
            }
            var strokeCopy = new CustomStrokeData(
                stroke.GroupId,
                pointsCopy,
                stroke.Style,
                stroke.Color,
                stroke.BaseSize,
                stroke.RendererId
            );
            customStrokes.Add(strokeCopy);
        }
        _history.Push(new PaintSnapshot(strokes, shapes, customStrokes));
    }

    private void EnsureInkHistory()
    {
        if (_inkStrokeInProgress)
        {
            return;
        }
        _inkStrokeInProgress = true;
        PushHistory();
    }

    private List<ShapeSnapshot> CaptureShapes()
    {
        var list = new List<ShapeSnapshot>();
        foreach (var shape in ShapeCanvas.Children.OfType<Shape>())
        {
            if (ReferenceEquals(shape, _regionRect))
            {
                continue;
            }
            var snapshot = ShapeSnapshot.FromShape(shape);
            if (snapshot != null)
            {
                list.Add(snapshot);
            }
        }
        return list;
    }

    private void RestoreShapes(IEnumerable<ShapeSnapshot> shapes)
    {
        foreach (var snapshot in shapes)
        {
            var shape = CreateShape(snapshot.Type);
            if (shape == null)
            {
                continue;
            }
            if (shape is Line line)
            {
                line.X1 = snapshot.Start.X;
                line.Y1 = snapshot.Start.Y;
                line.X2 = snapshot.End.X;
                line.Y2 = snapshot.End.Y;
            }
            else if (shape is Path path && !string.IsNullOrEmpty(snapshot.PathData))
            {
                path.Data = Geometry.Parse(snapshot.PathData);
                // For paths we treat StrokeColor as the fill
                path.Fill = new SolidColorBrush(snapshot.StrokeColor); 
            }
            else
            {
                System.Windows.Controls.Canvas.SetLeft(shape, snapshot.Bounds.Left);
                System.Windows.Controls.Canvas.SetTop(shape, snapshot.Bounds.Top);
                shape.Width = Math.Max(1, snapshot.Bounds.Width);
                shape.Height = Math.Max(1, snapshot.Bounds.Height);
            }
            shape.Stroke = new SolidColorBrush(snapshot.StrokeColor);
            shape.StrokeThickness = Math.Max(1, snapshot.StrokeThickness);
            if (snapshot.DashArray != null && snapshot.DashArray.Count > 0)
            {
                shape.StrokeDashArray = new DoubleCollection(snapshot.DashArray);
            }
            if (snapshot.FillColor.HasValue && snapshot.FillColor.Value.A > 0)
            {
                shape.Fill = new SolidColorBrush(snapshot.FillColor.Value);
            }
            if (shape is Path calligraphyPath && snapshot.CalligraphyGroupId != null && snapshot.CalligraphyRole.HasValue)
            {
                calligraphyPath.Tag = new CalligraphyLayerTag(snapshot.CalligraphyGroupId, snapshot.CalligraphyRole.Value);
                ApplyCalligraphyLayerStyle(calligraphyPath, snapshot.CalligraphyRole.Value);
            }
            ShapeCanvas.Children.Add(shape);
        }
    }

    private static void ApplyCalligraphyLayerStyle(Path path, CalligraphyLayerRole role)
    {
        switch (role)
        {
            case CalligraphyLayerRole.Wash:
                path.Fill = CalligraphyWashBrush;
                path.Effect = CalligraphyWashEffect;
                path.CacheMode = CalligraphyInkBleedCache;
                path.IsHitTestVisible = false;
                break;
            case CalligraphyLayerRole.Bridge:
                path.Fill = CalligraphyBridgeBrush;
                path.Effect = CalligraphyBridgeEffect;
                path.CacheMode = CalligraphyInkBleedCache;
                path.IsHitTestVisible = false;
                break;
            case CalligraphyLayerRole.Core:
                path.Fill = CalligraphyCoreBrush;
                path.Effect = CalligraphyCoreEffect;
                path.CacheMode = CalligraphyInkBleedCache;
                break;
        }
    }
}
