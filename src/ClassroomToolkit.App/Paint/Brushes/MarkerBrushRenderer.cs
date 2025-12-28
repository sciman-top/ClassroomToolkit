using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public class MarkerBrushRenderer : IBrushRenderer
{
    private const double SpeedWidthFactor = 0.18;
    private const double MinWidthFactor = 0.7;
    private const double PositionSmoothing = 0.6;
    private const double MinMoveDistance = 0.8;
    private const bool DebugDrawCenters = false;
    private const double WidthQuantizationStep = 0.5;
    private static readonly WpfPen DebugCenterPen = CreateFrozenPen(Colors.LimeGreen, 1);

    private struct MarkerPoint
    {
        public WpfPoint Position;
        public double Width;

        public MarkerPoint(WpfPoint position, double width)
        {
            Position = position;
            Width = width;
        }
    }

    private readonly List<MarkerPoint> _points = new();
    private WpfColor _color;
    private double _baseSize;
    private bool _isActive;
    private long _lastTimestamp;
    private double _smoothedWidth;
    private WpfPoint _smoothedPos;
    private double _maxVelocity;

    public bool IsActive => _isActive;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = baseSize;
        _smoothedPos = new WpfPoint(0, 0);
        _maxVelocity = 0.001;
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        _smoothedWidth = _baseSize;
        _smoothedPos = point;
        _maxVelocity = 0.001;
        _points.Add(new MarkerPoint(point, _smoothedWidth));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - PositionSmoothing) + point.X * PositionSmoothing,
            _smoothedPos.Y * (1 - PositionSmoothing) + point.Y * PositionSmoothing
        );

        var lastPt = _points[_points.Count - 1].Position;
        var dist = (_smoothedPos - lastPt).Length;
        if (dist < MinMoveDistance) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;
        _maxVelocity = Math.Max(_maxVelocity, velocity);
        double normSpeed = Math.Clamp(velocity / _maxVelocity, 0, 1);

        double targetWidth = _baseSize * (1.0 - (SpeedWidthFactor * normSpeed));
        double minWidth = _baseSize * MinWidthFactor;
        targetWidth = Math.Max(targetWidth, minWidth);

        _smoothedWidth = (_smoothedWidth * 0.6) + (targetWidth * 0.4);
        _points.Add(new MarkerPoint(_smoothedPos, _smoothedWidth));
        _lastTimestamp = now;
    }

    public void OnUp(WpfPoint point)
    {
        if (!_isActive) return;
        var last = _points[_points.Count - 1].Position;
        if ((point - last).Length > 0.1)
        {
            _points.Add(new MarkerPoint(point, _smoothedWidth));
        }
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count == 0)
        {
            return;
        }

        var brush = new SolidColorBrush(GetMarkerColor());
        brush.Freeze();

        var pen = new WpfPen(brush, Math.Max(QuantizeWidth(_points[0].Width), 0.1))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var first = _points[0];
        DrawStamp(dc, brush, first.Position, first.Width);
        int batchStart = 0;
        double batchWidth = QuantizeWidth(first.Width);

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];
            double w0 = Math.Max(p0.Width, 0.1);
            double w1 = Math.Max(p1.Width, 0.1);
            double nextWidth = QuantizeWidth(w1);
            bool split = nextWidth != batchWidth || ShouldStampJoin(i, w0, w1);

            if (split)
            {
                DrawBatchStroke(dc, pen, batchStart, i + 1, batchWidth);
                DrawStamp(dc, brush, p1.Position, w1);
                batchStart = i + 1;
                batchWidth = nextWidth;
            }
        }

        DrawBatchStroke(dc, pen, batchStart, _points.Count - 1, batchWidth);

        if (_points.Count > 1)
        {
            var last = _points[_points.Count - 1];
            DrawStamp(dc, brush, last.Position, last.Width);
        }

#pragma warning disable CS0162
        if (DebugDrawCenters && _points.Count > 1)
        {
            for (int i = 1; i < _points.Count; i++)
            {
                dc.DrawLine(DebugCenterPen, _points[i - 1].Position, _points[i].Position);
            }
        }
#pragma warning restore CS0162
    }

    public Geometry? GetLastStrokeGeometry()
    {
        var geometry = GenerateMarkerGeometry();
        if (geometry != null) geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// 获取最后一笔的原始点数据（用于部分删除）
    /// </summary>
    public List<StrokePointData>? GetLastStrokePoints()
    {
        if (_points.Count < 2) return null;

        var result = new List<StrokePointData>(_points.Count);
        foreach (var point in _points)
        {
            result.Add(new StrokePointData(point.Position, point.Width));
        }
        return result;
    }

    public void Reset()
    {
        _points.Clear();
        _isActive = false;
    }

    private WpfColor GetMarkerColor()
    {
        byte alpha = Math.Min(_color.A, (byte)0xE6);
        return WpfColor.FromArgb(alpha, _color.R, _color.G, _color.B);
    }

    private Geometry? GenerateMarkerGeometry()
    {
        if (_points.Count == 0)
        {
            return null;
        }

        var geometry = new GeometryGroup();
        var pen = new WpfPen(WpfBrushes.Black, QuantizeWidth(_points[0].Width))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var first = _points[0];
        geometry.Children.Add(CreateStampGeometry(first.Position, first.Width));
        int batchStart = 0;
        double batchWidth = QuantizeWidth(first.Width);

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];
            double w0 = Math.Max(p0.Width, 0.1);
            double w1 = Math.Max(p1.Width, 0.1);
            double nextWidth = QuantizeWidth(w1);
            bool split = nextWidth != batchWidth || ShouldStampJoin(i, w0, w1);

            if (split)
            {
                var batch = CreateBatchGeometry(pen, batchStart, i + 1, batchWidth);
                if (batch != null)
                {
                    geometry.Children.Add(batch);
                }
                geometry.Children.Add(CreateStampGeometry(p1.Position, w1));
                batchStart = i + 1;
                batchWidth = nextWidth;
            }
        }

        var lastBatch = CreateBatchGeometry(pen, batchStart, _points.Count - 1, batchWidth);
        if (lastBatch != null)
        {
            geometry.Children.Add(lastBatch);
        }

        if (_points.Count > 1)
        {
            var last = _points[_points.Count - 1];
            geometry.Children.Add(CreateStampGeometry(last.Position, last.Width));
        }

        if (geometry.CanFreeze) geometry.Freeze();
        return geometry;
    }

    private static WpfPen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new WpfPen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static void DrawStamp(DrawingContext dc, WpfBrush brush, WpfPoint point, double width)
    {
        double radius = Math.Max(width * 0.5, 0.1);
        dc.DrawEllipse(brush, null, point, radius, radius);
    }

    private static EllipseGeometry CreateStampGeometry(WpfPoint point, double width)
    {
        double radius = Math.Max(width * 0.5, 0.1);
        var geometry = new EllipseGeometry(point, radius, radius);
        if (geometry.CanFreeze) geometry.Freeze();
        return geometry;
    }

    private static double QuantizeWidth(double width)
    {
        double clamped = Math.Max(width, 0.1);
        return Math.Max(0.1, Math.Round(clamped / WidthQuantizationStep) * WidthQuantizationStep);
    }

    private void DrawBatchStroke(DrawingContext dc, WpfPen pen, int startIndex, int endIndex, double width)
    {
        if (endIndex <= startIndex)
        {
            return;
        }

        pen.Thickness = width;
        var geometry = BuildPolylineGeometry(startIndex, endIndex);
        if (geometry == null)
        {
            return;
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private Geometry? CreateBatchGeometry(WpfPen pen, int startIndex, int endIndex, double width)
    {
        if (endIndex <= startIndex)
        {
            return null;
        }

        pen.Thickness = width;
        var geometry = BuildPolylineGeometry(startIndex, endIndex);
        if (geometry == null)
        {
            return null;
        }

        var widened = geometry.GetWidenedPathGeometry(pen);
        if (widened.CanFreeze) widened.Freeze();
        return widened;
    }

    private StreamGeometry? BuildPolylineGeometry(int startIndex, int endIndex)
    {
        if (endIndex <= startIndex)
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_points[startIndex].Position, isFilled: false, isClosed: false);
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                ctx.LineTo(_points[i].Position, isStroked: true, isSmoothJoin: true);
            }
        }
        if (geometry.CanFreeze) geometry.Freeze();
        return geometry;
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + ((end - start) * t);
    }

    private bool ShouldStampJoin(int index, double w0, double w1)
    {
        int nextIndex = index + 2;
        if (nextIndex >= _points.Count)
        {
            return false;
        }

        var p0 = _points[index].Position;
        var p1 = _points[index + 1].Position;
        var p2 = _points[nextIndex].Position;

        var v1 = p1 - p0;
        var v2 = p2 - p1;
        double len1 = v1.Length;
        double len2 = v2.Length;

        bool sharpTurn = false;
        if (len1 > 0.001 && len2 > 0.001)
        {
            double cos = Vector.Multiply(v1, v2) / (len1 * len2);
            sharpTurn = cos < 0.5;
        }

        double w2 = Math.Max(_points[nextIndex].Width, 0.1);
        bool widthJump = w1 > Math.Max(w0, w2) * 1.25;

        return sharpTurn || widthJump;
    }
}
