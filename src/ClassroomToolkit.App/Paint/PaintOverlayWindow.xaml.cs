using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.App.Helpers;
using MediaColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;
using WpfBrush = System.Windows.Media.Brush;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow : Window
{
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
    private bool _isDrawingShape;
    private WpfPoint _shapeStart;
    private Shape? _activeShape;
    private bool _isRegionSelecting;
    private WpfPoint _regionStart;
    private WpfRectangle? _regionRect;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlService _presentationService;
    private readonly ClassroomToolkit.Services.Presentation.PresentationControlOptions _presentationOptions;
    private readonly Stack<PaintSnapshot> _history = new();
    private bool _erasing;

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
        InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
        InkLayer.DefaultDrawingAttributes = BuildDrawingAttributes(Colors.Red, 12, 255);
        InkLayer.EraserShape = new RectangleStylusShape(24, 24);
        InkLayer.StrokeCollected += OnStrokeCollected;
        InkLayer.StrokeErasing += OnStrokeErasing;
        InkLayer.MouseLeftButtonDown += OnMouseDown;
        InkLayer.MouseMove += OnMouseMove;
        InkLayer.MouseLeftButtonUp += OnMouseUp;
        MouseWheel += OnMouseWheel;

        var classifier = new ClassroomToolkit.Interop.Presentation.PresentationClassifier();
        var planner = new ClassroomToolkit.Services.Presentation.PresentationControlPlanner(classifier);
        var mapper = new ClassroomToolkit.Services.Presentation.PresentationCommandMapper();
        var sender = new ClassroomToolkit.Interop.Presentation.Win32InputSender();
        var resolver = new ClassroomToolkit.Interop.Presentation.Win32PresentationResolver();
        _presentationService = new ClassroomToolkit.Services.Presentation.PresentationControlService(planner, mapper, sender, resolver);
        _presentationOptions = new ClassroomToolkit.Services.Presentation.PresentationControlOptions
        {
            Strategy = ClassroomToolkit.Interop.Presentation.InputStrategy.Auto,
            WheelAsKey = false,
            AllowOffice = true,
            AllowWps = true
        };
    }

    public void SetMode(PaintToolMode mode)
    {
        _mode = mode;
        OverlayRoot.IsHitTestVisible = mode != PaintToolMode.Cursor;
        InkLayer.IsHitTestVisible = mode != PaintToolMode.Cursor;
        switch (mode)
        {
            case PaintToolMode.Brush:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
                break;
            case PaintToolMode.Eraser:
                InkLayer.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;
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
        OverlayRoot.Background = new SolidColorBrush(color);
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
            return;
        }
        if (_mode != PaintToolMode.Shape)
        {
            return;
        }
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
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
            _isRegionSelecting = false;
            var end = e.GetPosition(ShapeCanvas);
            var region = BuildRegionRect(_regionStart, end);
            ClearRegionSelection();
            if (region.Width > 2 && region.Height > 2)
            {
                EraseRegion(region);
            }
            _erasing = false;
            return;
        }
        if (_mode == PaintToolMode.Shape)
        {
            _isDrawingShape = false;
            _activeShape = null;
            PushHistory();
        }
        _erasing = false;
    }

    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_mode == PaintToolMode.Cursor || _mode == PaintToolMode.Brush || _mode == PaintToolMode.Shape || _mode == PaintToolMode.Eraser || _mode == PaintToolMode.RegionErase)
        {
            var command = e.Delta < 0
                ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
                : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
            _presentationService.TrySendForeground(command, _presentationOptions);
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
        if (OverlayRoot.Background is SolidColorBrush brush)
        {
            var color = brush.Color;
            color.A = opacity;
            brush.Color = color;
        }
    }

    public void UpdateWpsMode(string mode)
    {
        _presentationOptions.Strategy = mode switch
        {
            "raw" => ClassroomToolkit.Interop.Presentation.InputStrategy.Raw,
            "message" => ClassroomToolkit.Interop.Presentation.InputStrategy.Message,
            _ => ClassroomToolkit.Interop.Presentation.InputStrategy.Auto
        };
    }

    public void UpdateWpsWheelMapping(bool enabled)
    {
        _presentationOptions.WheelAsKey = enabled;
    }

    public void UpdatePresentationTargets(bool allowOffice, bool allowWps)
    {
        _presentationOptions.AllowOffice = allowOffice;
        _presentationOptions.AllowWps = allowWps;
    }

    private void OnStrokeCollected(object? sender, InkCanvasStrokeCollectedEventArgs e)
    {
        PushHistory();
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
            var hits = InkLayer.Strokes.HitTest(region, 60);
            foreach (var stroke in hits.ToList())
            {
                InkLayer.Strokes.Remove(stroke);
            }
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
