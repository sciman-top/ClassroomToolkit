using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public class MarkerBrushRenderer : IBrushRenderer
{
    private const double SpeedWidthFactor = 0.18;
    private const double MinWidthFactor = 0.7;
    private const double PositionSmoothing = 0.6;
    private const double MinMoveDistance = 0.8;
    private const bool DebugDrawCenters = false;
    private const double PenThicknessQuantization = 0.1;
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

        var penCache = new Dictionary<int, WpfPen>();
        WpfPen GetPen(double thickness)
        {
            double clamped = Math.Max(thickness, 0.1);
            int key = (int)Math.Round(clamped / PenThicknessQuantization);
            if (key < 1) key = 1;

            if (!penCache.TryGetValue(key, out var cached))
            {
                double actual = key * PenThicknessQuantization;
                var pen = new WpfPen(brush, actual)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                pen.Freeze();
                penCache[key] = pen;
                cached = pen;
            }

            return cached;
        }

        var first = _points[0];
        DrawStamp(dc, brush, first.Position, first.Width);

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];
            double w0 = Math.Max(p0.Width, 0.1);
            double w1 = Math.Max(p1.Width, 0.1);

            double dx = p1.Position.X - p0.Position.X;
            double dy = p1.Position.Y - p0.Position.Y;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));

            if (dist < 0.001)
            {
                DrawStamp(dc, brush, p1.Position, w1);
                continue;
            }

            double step = Math.Max(0.5, Math.Min(w0, w1) * 0.6);
            int segments = Math.Max(1, (int)Math.Ceiling(dist / step));

            var prev = p0.Position;
            for (int s = 1; s <= segments; s++)
            {
                double t = (double)s / segments;
                var cur = new WpfPoint(
                    p0.Position.X + (dx * t),
                    p0.Position.Y + (dy * t));
                double width = Lerp(w0, w1, t);
                var pen = GetPen(width);
                dc.DrawLine(pen, prev, cur);
                DrawStamp(dc, brush, cur, width);
                prev = cur;
            }
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
        var penCache = new Dictionary<int, WpfPen>();

        WpfPen GetPen(double thickness)
        {
            double clamped = Math.Max(thickness, 0.1);
            int key = (int)Math.Round(clamped / PenThicknessQuantization);
            if (key < 1) key = 1;

            if (!penCache.TryGetValue(key, out var cached))
            {
                double actual = key * PenThicknessQuantization;
                var pen = new WpfPen(Brushes.Black, actual)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                pen.Freeze();
                penCache[key] = pen;
                cached = pen;
            }
            return cached;
        }

        var first = _points[0];
        geometry.Children.Add(CreateStampGeometry(first.Position, first.Width));

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];
            double w0 = Math.Max(p0.Width, 0.1);
            double w1 = Math.Max(p1.Width, 0.1);

            double dx = p1.Position.X - p0.Position.X;
            double dy = p1.Position.Y - p0.Position.Y;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));

            if (dist < 0.001)
            {
                geometry.Children.Add(CreateStampGeometry(p1.Position, w1));
                continue;
            }

            double step = Math.Max(0.5, Math.Min(w0, w1) * 0.6);
            int segments = Math.Max(1, (int)Math.Ceiling(dist / step));

            var prev = p0.Position;
            for (int s = 1; s <= segments; s++)
            {
                double t = (double)s / segments;
                var cur = new WpfPoint(
                    p0.Position.X + (dx * t),
                    p0.Position.Y + (dy * t));
                double width = Lerp(w0, w1, t);
                var pen = GetPen(width);
                var line = new LineGeometry(prev, cur);
                var capsule = line.GetWidenedPathGeometry(pen);
                if (capsule.CanFreeze) capsule.Freeze();
                geometry.Children.Add(capsule);
                geometry.Children.Add(CreateStampGeometry(cur, width));
                prev = cur;
            }
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

    private static double Lerp(double start, double end, double t)
    {
        return start + ((end - start) * t);
    }
}
