using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Paint.Brushes;

public class MarkerBrushRenderer : IBrushRenderer
{
    private const double SpeedWidthFactor = 0.18;
    private const double MinWidthFactor = 0.7;
    private const double PositionSmoothing = 0.6;
    private const double MinMoveDistance = 0.8;
    private const bool DebugDrawEdges = true;

    private static readonly Pen DebugLeftPen = CreateFrozenPen(Colors.Red, 1);
    private static readonly Pen DebugRightPen = CreateFrozenPen(Colors.DodgerBlue, 1);

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
    private List<WpfPoint>? _debugLeftEdge;
    private List<WpfPoint>? _debugRightEdge;

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

        var lastPt = _points.Last().Position;
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
        var last = _points.Last().Position;
        if ((point - last).Length > 0.1)
        {
            _points.Add(new MarkerPoint(point, _smoothedWidth));
        }
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        var geometry = GenerateMarkerGeometry();
        if (geometry == null) return;

        var brush = new SolidColorBrush(GetMarkerColor());
        brush.Freeze();
        dc.DrawGeometry(brush, null, geometry);

        if (DebugDrawEdges && _debugLeftEdge != null && _debugRightEdge != null)
        {
            for (int i = 1; i < _debugLeftEdge.Count; i++)
            {
                dc.DrawLine(DebugLeftPen, _debugLeftEdge[i - 1], _debugLeftEdge[i]);
            }

            for (int i = 1; i < _debugRightEdge.Count; i++)
            {
                dc.DrawLine(DebugRightPen, _debugRightEdge[i - 1], _debugRightEdge[i]);
            }
        }
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
        if (_points.Count < 2) return null;

        var leftEdge = new List<WpfPoint>(_points.Count);
        var rightEdge = new List<WpfPoint>(_points.Count);
        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < _points.Count; i++)
        {
            var pos = _points[i].Position;
            var width = _points[i].Width;

            Vector dir;
            if (i == 0)
            {
                dir = _points[i + 1].Position - pos;
            }
            else if (i == _points.Count - 1)
            {
                dir = pos - _points[i - 1].Position;
            }
            else
            {
                dir = _points[i + 1].Position - _points[i - 1].Position;
            }

            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            var half = Math.Max(width * 0.5, 0.1);
            leftEdge.Add(pos + normal * half);
            rightEdge.Add(pos - normal * half);
        }

        if (DebugDrawEdges)
        {
            _debugLeftEdge = leftEdge;
            _debugRightEdge = rightEdge;
        }

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < leftEdge.Count - 1; i++)
            {
                ctx.BeginFigure(leftEdge[i], isFilled: true, isClosed: true);
                ctx.LineTo(leftEdge[i + 1], isStroked: true, isSmoothJoin: true);
                ctx.LineTo(rightEdge[i + 1], isStroked: true, isSmoothJoin: true);
                ctx.LineTo(rightEdge[i], isStroked: true, isSmoothJoin: true);
            }
        }

        return geometry;
    }

    private static Vector GetNormalFromVector(Vector dir, Vector fallback)
    {
        if (dir.LengthSquared < 0.0001)
        {
            if (fallback.LengthSquared < 0.0001) return new Vector(0, 1);
            fallback.Normalize();
            return fallback;
        }

        dir.Normalize();
        var normal = new Vector(-dir.Y, dir.X);
        if (fallback.LengthSquared > 0.0001)
        {
            var lastNormal = fallback;
            lastNormal.Normalize();
            if (Vector.Multiply(normal, lastNormal) < -0.01)
            {
                normal = -normal;
            }
        }
        return normal;
    }

    private static Pen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
