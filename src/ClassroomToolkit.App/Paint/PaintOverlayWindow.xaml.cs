using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Helpers;
using MediaColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfBrush = System.Windows.Media.Brush;
using System.Windows.Interop;

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
    private IntPtr _hwnd;
    private bool _inputPassthroughEnabled;
    private bool _focusBlocked;
    private bool _forcePresentationForegroundOnFullscreen;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private sealed class PaintSnapshot
    {
        public PaintSnapshot(StrokeCollection strokes, List<ShapeSnapshot> shapes)
        {
            Strokes = strokes;
            Shapes = shapes;
        }

        public StrokeCollection Strokes { get; }
        public List<ShapeSnapshot> Shapes { get; }
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

        public static ShapeSnapshot? FromShape(Shape shape)
        {
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
    private DateTime _wpsNavBlockUntil = DateTime.MinValue;
    private (int Code, IntPtr Target, DateTime Timestamp)? _lastWpsNavEvent;
    private DateTime _lastWpsHookInput = DateTime.MinValue;
    private readonly Stack<PaintSnapshot> _history = new();
    private bool _erasing;
    private bool _inkStrokeInProgress;

    public PaintOverlayWindow()
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
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
        Closed += (_, _) => StopWpsNavHook();
        IsVisibleChanged += (_, _) =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
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
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
                InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.Ink;
                break;
            case PaintToolMode.Eraser:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;
                InkLayer.EditingModeInverted = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;
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
            EnsureInkHistory();
        }
        if (_mode == PaintToolMode.Eraser)
        {
            RemoveShapeAt(e.GetPosition(ShapeCanvas));
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
            RemoveShapeAt(point);
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
            return;
        }
        _presentationService.TrySendForeground(command, _presentationOptions);
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
    }

    public void SetBrushOpacity(byte opacity)
    {
        var current = InkLayer.DefaultDrawingAttributes;
        var color = current.Color;
        color.A = opacity;
        current.Color = color;
        InkLayer.DefaultDrawingAttributes = current;
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
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
    }

    public void UpdatePresentationForegroundPolicy(bool forceForegroundOnFullscreen)
    {
        _forcePresentationForegroundOnFullscreen = forceForegroundOnFullscreen;
    }

    public void RestorePresentationFocusIfNeeded()
    {
        if (!IsVisible)
        {
            return;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        var target = _presentationResolver.ResolvePresentationTarget(
            _presentationClassifier,
            _presentationOptions.AllowWps,
            _presentationOptions.AllowOffice,
            _currentProcessId);
        if (!target.IsValid)
        {
            return;
        }
        var fullscreen = IsFullscreenPresentationWindow(target);
        if (!fullscreen)
        {
            return;
        }
        var force = ShouldForcePresentationForeground(target);
        if (!force && !IsForegroundOwnedByCurrentProcess())
        {
            return;
        }
        if (ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.IsForeground(target.Handle))
        {
            return;
        }
        ClassroomToolkit.Interop.Presentation.PresentationWindowFocus.EnsureForeground(target.Handle);
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
            return;
        }
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
                _history.Push(new PaintSnapshot(strokes, shapes));
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
        if (InkLayer.Strokes.Count > 0)
        {
            InkLayer.Strokes.Erase(region);
        }

        var shapes = ShapeCanvas.Children.OfType<Shape>().ToList();
        foreach (var shape in shapes)
        {
            if (shape == _regionRect)
            {
                continue;
            }
            if (IsShapeHit(region, shape))
            {
                ShapeCanvas.Children.Remove(shape);
            }
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
        _history.Push(new PaintSnapshot(strokes, shapes));
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
            ShapeCanvas.Children.Add(shape);
        }
    }
}
