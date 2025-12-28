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
    }

    public Geometry? GetLastStrokeGeometry()
    {
        var geometry = GenerateMarkerGeometry();
        if (geometry != null) geometry.Freeze();
        return geometry;
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

            var half = width * 0.5;
            leftEdge.Add(pos + normal * half);
            rightEdge.Add(pos - normal * half);
        }

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = geometry.Open())
        {
            // ===== 修复绕向问题：绘制单一连续轮廓，保持顺时针方向 =====
            // 1. 从左边缘起点开始
            ctx.BeginFigure(leftEdge[0], isFilled: true, isClosed: true);

            // 2. 绘制左边缘（向前）
            for (int i = 1; i < leftEdge.Count; i++)
            {
                ctx.LineTo(leftEdge[i], isStroked: true, isSmoothJoin: true);
            }

            // 3. 末端圆弧：从左边缘末尾到右边缘末尾（保持顺时针）
            var lastLeft = leftEdge[^1];
            var lastRight = rightEdge[^1];
            double endRadius = Math.Max(_points[^1].Width * 0.5, 0.1);

            // 计算末端圆弧的控制点
            var endMidPoint = new WpfPoint((lastLeft.X + lastRight.X) * 0.5, (lastLeft.Y + lastRight.Y) * 0.5);
            var endDir = lastRight - lastLeft;
            if (endDir.LengthSquared > 0.0001) endDir.Normalize();
            var endNormal = new Vector(-endDir.Y, endDir.X);
            var endControlPoint = endMidPoint + endNormal * (endRadius * 0.5);

            // 使用二次贝塞尔曲线模拟圆弧（避免 ArcTo 的绕向问题）
            ctx.QuadraticBezierTo(endControlPoint, lastRight, isStroked: true, isSmoothJoin: true);

            // 4. 绘制右边缘（向后，从末尾到起点）
            for (int i = rightEdge.Count - 2; i >= 0; i--)
            {
                ctx.LineTo(rightEdge[i], isStroked: true, isSmoothJoin: true);
            }

            // 5. 起始圆弧：从右边缘起点回到左边缘起点（保持顺时针）
            var firstRight = rightEdge[0];
            var firstLeft = leftEdge[0];
            double startRadius = Math.Max(_points[0].Width * 0.5, 0.1);

            // 计算起始圆弧的控制点
            var startMidPoint = new WpfPoint((firstRight.X + firstLeft.X) * 0.5, (firstRight.Y + firstLeft.Y) * 0.5);
            var startDir = firstLeft - firstRight;
            if (startDir.LengthSquared > 0.0001) startDir.Normalize();
            var startNormal = new Vector(-startDir.Y, startDir.X);
            var startControlPoint = startMidPoint + startNormal * (startRadius * 0.5);

            // 使用二次贝塞尔曲线模拟圆弧
            ctx.QuadraticBezierTo(startControlPoint, firstLeft, isStroked: true, isSmoothJoin: true);
        }

        return geometry;
    }

    private static Vector GetNormalFromVector(Vector dir, Vector fallback)
    {
        if (dir.LengthSquared < 0.0001)
        {
            if (fallback.LengthSquared < 0.0001) return new Vector(0, 1);
            return fallback;
        }

        dir.Normalize();
        return new Vector(-dir.Y, dir.X);
    }
}
