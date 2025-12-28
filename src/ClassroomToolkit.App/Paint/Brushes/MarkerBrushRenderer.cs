using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public class MarkerBrushRenderer : IBrushRenderer
{
    private const double SpeedWidthFactor = 0.18;
    private const double MinWidthFactor = 0.7;
    private const double PositionSmoothing = 0.6;
    private const double MinMoveDistance = 0.8;
    private const bool DebugDrawEdges = false;

    private static readonly WpfPen DebugLeftPen = CreateFrozenPen(Colors.Red, 1);
    private static readonly WpfPen DebugRightPen = CreateFrozenPen(Colors.DodgerBlue, 1);

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

#pragma warning disable CS0162
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
        if (_points.Count < 2) return null;
        int count = _points.Count;

        var leftEdge = new List<WpfPoint>(count);
        var rightEdge = new List<WpfPoint>(count);
        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < count; i++)
        {
            var pos = _points[i].Position;
            var width = _points[i].Width;

            Vector tangent;
            if (i == 0)
            {
                tangent = _points[i + 1].Position - pos;
            }
            else if (i == count - 1)
            {
                tangent = pos - _points[i - 1].Position;
            }
            else
            {
                var dirPrev = pos - _points[i - 1].Position;
                var dirNext = _points[i + 1].Position - pos;
                if (dirPrev.LengthSquared > 0.0001) dirPrev.Normalize();
                if (dirNext.LengthSquared > 0.0001) dirNext.Normalize();
                tangent = dirPrev + dirNext;
            }

            var normal = GetNormalFromVector(tangent, lastNormal);
            lastNormal = normal;

            var half = Math.Max(width * 0.5, 0.1);
            leftEdge.Add(pos + normal * half);
            rightEdge.Add(pos - normal * half);
        }

#pragma warning disable CS0162
        if (DebugDrawEdges)
        {
            _debugLeftEdge = leftEdge;
            _debugRightEdge = rightEdge;
        }
#pragma warning restore CS0162

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = geometry.Open())
        {
            double targetSign = 0;
            const double areaEpsilon = 1e-6;

            for (int i = 0; i < count - 1; i++)
            {
                var l0 = leftEdge[i];
                var l1 = leftEdge[i + 1];
                var r1 = rightEdge[i + 1];
                var r0 = rightEdge[i];

                double area = QuadSignedArea(l0, l1, r1, r0);
                if (targetSign == 0 && Math.Abs(area) > areaEpsilon)
                {
                    targetSign = Math.Sign(area);
                }

                bool flip = targetSign != 0 && area * targetSign < 0;

                ctx.BeginFigure(l0, isFilled: true, isClosed: true);
                if (flip)
                {
                    ctx.LineTo(r0, isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(r1, isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(l1, isStroked: false, isSmoothJoin: false);
                }
                else
                {
                    ctx.LineTo(l1, isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(r1, isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(r0, isStroked: false, isSmoothJoin: false);
                }
            }

            if (targetSign == 0)
            {
                targetSign = 1;
            }

            var startTangent = _points[1].Position - _points[0].Position;
            var endTangent = _points[count - 1].Position - _points[count - 2].Position;
            var startOutward = EnsureNormalized(-startTangent, new Vector(0, 1));
            var endOutward = EnsureNormalized(endTangent, new Vector(0, 1));

            DrawCap(ctx, leftEdge[0], rightEdge[0], _points[0].Position, startOutward, targetSign);
            DrawCap(ctx, leftEdge[count - 1], rightEdge[count - 1], _points[count - 1].Position, endOutward, targetSign);
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
        if (fallback.LengthSquared < 0.0001)
        {
            return normal;
        }

        var lastNormal = fallback;
        lastNormal.Normalize();
        double dot = Vector.Multiply(normal, lastNormal);
        if (dot < -0.01)
        {
            normal = -normal;
        }
        else if (dot > -0.01 && dot < 0.01)
        {
            normal = lastNormal;
        }
        return normal;
    }

    private static WpfPen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new WpfPen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static void DrawCap(StreamGeometryContext ctx, WpfPoint start, WpfPoint end, WpfPoint center, Vector outward, double targetSign)
    {
        double radius = (start - center).Length;
        if (radius < 0.1) return;

        var sweep = ChooseCapSweep(center, start, end, outward);
        var mid = CapMidPoint(center, radius, sweep, start, end);
        double sign = TriangleSignedArea(start, mid, end);

        if (sign * targetSign < 0)
        {
            var temp = start;
            start = end;
            end = temp;
            sweep = ChooseCapSweep(center, start, end, outward);
        }

        ctx.BeginFigure(start, isFilled: true, isClosed: true);
        ctx.ArcTo(end, new WpfSize(radius, radius), 0, false, sweep, isStroked: false, isSmoothJoin: true);
    }

    private static Vector EnsureNormalized(Vector value, Vector fallback)
    {
        if (value.LengthSquared < 0.0001)
        {
            if (fallback.LengthSquared < 0.0001)
            {
                return new Vector(0, 1);
            }
            fallback.Normalize();
            return fallback;
        }
        value.Normalize();
        return value;
    }

    private static double QuadSignedArea(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3)
    {
        double area = 0;
        area += (p0.X * p1.Y - p1.X * p0.Y);
        area += (p1.X * p2.Y - p2.X * p1.Y);
        area += (p2.X * p3.Y - p3.X * p2.Y);
        area += (p3.X * p0.Y - p0.X * p3.Y);
        return area * 0.5;
    }

    private static double TriangleSignedArea(WpfPoint p0, WpfPoint p1, WpfPoint p2)
    {
        return (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
    }

    private static SweepDirection ChooseCapSweep(WpfPoint center, WpfPoint start, WpfPoint end, Vector outward)
    {
        var vStart = start - center;
        var vEnd = end - center;
        if (vStart.LengthSquared < 0.0001 || vEnd.LengthSquared < 0.0001)
        {
            return SweepDirection.Clockwise;
        }

        double angleStart = Math.Atan2(vStart.Y, vStart.X);
        double angleEnd = Math.Atan2(vEnd.Y, vEnd.X);

        double deltaClockwise = NormalizeAnglePositive(angleEnd - angleStart);
        double deltaCounter = NormalizeAnglePositive(angleStart - angleEnd);

        double midClockwise = angleStart + (deltaClockwise * 0.5);
        double midCounter = angleStart - (deltaCounter * 0.5);

        var midClockDir = new Vector(Math.Cos(midClockwise), Math.Sin(midClockwise));
        var midCounterDir = new Vector(Math.Cos(midCounter), Math.Sin(midCounter));

        var outwardNorm = EnsureNormalized(outward, midClockDir);
        double dotClock = Vector.Multiply(midClockDir, outwardNorm);
        double dotCounter = Vector.Multiply(midCounterDir, outwardNorm);

        return dotClock >= dotCounter ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
    }

    private static double NormalizeAnglePositive(double angle)
    {
        const double twoPi = Math.PI * 2;
        while (angle < 0)
        {
            angle += twoPi;
        }
        while (angle >= twoPi)
        {
            angle -= twoPi;
        }
        return angle;
    }

    private static WpfPoint CapMidPoint(WpfPoint center, double radius, SweepDirection sweep, WpfPoint start, WpfPoint end)
    {
        var vStart = start - center;
        var vEnd = end - center;
        if (vStart.LengthSquared < 0.0001 || vEnd.LengthSquared < 0.0001)
        {
            return new WpfPoint(center.X + radius, center.Y);
        }

        double angleStart = Math.Atan2(vStart.Y, vStart.X);
        double angleEnd = Math.Atan2(vEnd.Y, vEnd.X);
        double delta = sweep == SweepDirection.Clockwise
            ? NormalizeAnglePositive(angleEnd - angleStart)
            : NormalizeAnglePositive(angleStart - angleEnd);

        double midAngle = sweep == SweepDirection.Clockwise
            ? angleStart + (delta * 0.5)
            : angleStart - (delta * 0.5);

        return new WpfPoint(center.X + Math.Cos(midAngle) * radius, center.Y + Math.Sin(midAngle) * radius);
    }
}
