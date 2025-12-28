using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

public class BrushPhysicsConfig
{
    public double MinWidthFactor { get; set; } = 0.15;
    public double MaxWidthFactor { get; set; } = 1.5;
    public double MinStrokeWidthPx { get; set; } = 2.0;
    public double MaxStrokeWidthMultiplier { get; set; } = 2.5;
    public double WidthSmoothing { get; set; } = 0.85;
    public bool SimulateStartCap { get; set; } = true;
    public bool SimulateEndTaper { get; set; } = true;
    public double VelocityThreshold { get; set; } = 1.5;
    public int VelocitySmoothWindow { get; set; } = 4;
    public int PressureSmoothWindow { get; set; } = 5;
    public double CapIgnoreVelocityRatio { get; set; } = 0.1;

    // 笔锋效果参数
    public double StartCapLength { get; set; } = 0.05; // 起笔占笔画长度的比例
    public double EndTaperLength { get; set; } = 0.12;  // 收笔占笔画长度的比例
    public int MinTaperPoints { get; set; } = 5;        // 笔锋最少点数

    // 修复蝌蚪头：起笔阶段忽略速度
    public int StartVelocityRampUpPoints { get; set; } = 10; // 起笔前N个点逐渐增加速度影响
    public double MinVelocityClamp { get; set; } = 0.3;     // 最小速度限制，防止除零

    // 数据验证参数
    public double MaxPointJumpDistance { get; set; } = 100.0; // 最大允许点间距

    public static BrushPhysicsConfig DefaultSmooth => new();
}

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private const int UpsampleSteps = 8;
    private const double CornerAngleThreshold = 90.0;
    private const double CornerMinAngle = 5.0;
    private const int CornerArcSegments = 4;

    private struct StrokePoint
    {
        public WpfPoint Position;
        public double Width;

        public StrokePoint(WpfPoint pos, double width)
        {
            Position = pos;
            Width = width;
        }
    }

    private readonly List<StrokePoint> _points = new();
    private readonly Queue<double> _velocityBuffer = new();
    private readonly Queue<double> _widthBuffer = new();
    private WpfColor _color;
    private double _baseSize;
    private bool _isActive;
    private long _lastTimestamp;
    private int _pointCount; // 跟踪笔画中的点数

    private double _smoothedWidth;
    private WpfPoint _smoothedPos;

    private readonly BrushPhysicsConfig _config = BrushPhysicsConfig.DefaultSmooth;

    public bool IsActive => _isActive;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = ClampWidth(baseSize * 0.8);
        _smoothedPos = new WpfPoint(0, 0); 
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _velocityBuffer.Clear();
        _widthBuffer.Clear();
        _isActive = true;
        _pointCount = 0; // 重置点计数
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        _smoothedWidth = ClampWidth(_baseSize * 0.5);
        _smoothedPos = point;

        _points.Add(new StrokePoint(point, _smoothedWidth));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        // 数据验证：检查 NaN/Infinity
        if (double.IsNaN(point.X) || double.IsNaN(point.Y) ||
            double.IsInfinity(point.X) || double.IsInfinity(point.Y))
        {
            return; // 丢弃异常点
        }

        // 坐标平滑 (EMA)
        double posAlpha = 0.7;
        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - posAlpha) + point.X * posAlpha,
            _smoothedPos.Y * (1 - posAlpha) + point.Y * posAlpha
        );

        var lastPt = _points.Last();
        var dist = (_smoothedPos - lastPt.Position).Length;

        // 去噪：忽略过小的移动
        if (dist < 2.0) return;

        // 数据验证：检查异常跳变（飞线）
        if (dist > _config.MaxPointJumpDistance)
        {
            return; // 丢弃异常跳变的点
        }

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;
        double smoothVelocity = PushAndAverage(_velocityBuffer, _config.VelocitySmoothWindow, velocity);
        double targetWidth = CalculateTargetWidth(smoothVelocity, _pointCount);
        targetWidth = PushAndAverage(_widthBuffer, _config.PressureSmoothWindow, targetWidth);
        targetWidth = ClampWidth(targetWidth);

        double widthAlpha = _config.WidthSmoothing;
        _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));
        _smoothedWidth = ClampWidth(_smoothedWidth);

        // 数据验证：再次检查宽度是否有效
        if (double.IsNaN(_smoothedWidth) || double.IsInfinity(_smoothedWidth) || _smoothedWidth <= 0)
        {
            return; // 丢弃无效宽度
        }

        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth));
        _lastTimestamp = now;
        _pointCount++; // 增加点计数
    }

    public void OnUp(WpfPoint point)
    {
        if (!_isActive) return;
        
        var last = _points.Last();
        var dir = point - last.Position;
        if (dir.Length > 0.1) dir.Normalize();
        else dir = new Vector(1, 0);
        
        var extension = _baseSize * 0.5;
        var endPos = point + dir * extension;

        _points.Add(new StrokePoint(endPos, 0.1));
        _isActive = false;
    }

    public void Reset()
    {
        _points.Clear();
        _velocityBuffer.Clear();
        _widthBuffer.Clear();
        _isActive = false;
        _pointCount = 0;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var geometry = GenerateSmoothGeometry();
        if (geometry != null)
        {
            var brush = new SolidColorBrush(_color);
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
        }
    }

    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geo = GenerateSmoothGeometry();
        if (geo != null) geo.Freeze();
        return geo;
    }

    private double CalculateTargetWidth(double velocity, int pointIndex)
    {
        // 修复蝌蚪头：起笔阶段逐渐增加速度影响
        double velocityInfluence = 1.0;
        if (pointIndex < _config.StartVelocityRampUpPoints)
        {
            // 前 N 个点：速度影响从 0% 线性增长到 100%
            velocityInfluence = (double)pointIndex / _config.StartVelocityRampUpPoints;
        }

        // 限制最小速度，防止除零或过小速度导致的过宽
        double clampedVelocity = Math.Max(velocity, _config.MinVelocityClamp);

        // 软化速度曲线：从三次方改为二次方，更温和
        var t = Math.Min(clampedVelocity / _config.VelocityThreshold, 1.0);
        var factor = 1.0 - (t * t); // 二次函数，比三次方温和

        // 起笔阶段：在固定宽度和速度影响宽度之间插值
        var baseFactor = 0.8; // 起笔基础宽度因子
        var finalFactor = baseFactor * (1.0 - velocityInfluence) + factor * velocityInfluence;

        var range = _config.MaxWidthFactor - _config.MinWidthFactor;
        var width = _baseSize * (_config.MinWidthFactor + (range * finalFactor));
        return ClampWidth(width);
    }

    private double ClampWidth(double width)
    {
        double minWidth = _config.MinStrokeWidthPx;
        double maxWidth = _baseSize * _config.MaxStrokeWidthMultiplier;
        if (maxWidth < minWidth) maxWidth = minWidth;
        return Math.Clamp(width, minWidth, maxWidth);
    }

    private static double PushAndAverage(Queue<double> buffer, int maxCount, double value)
    {
        int limit = Math.Max(1, maxCount);
        buffer.Enqueue(value);
        while (buffer.Count > limit) buffer.Dequeue();
        return buffer.Average();
    }

    private Geometry? GenerateSmoothGeometry()
    {
        if (_points.Count < 2) return null;

        var geometry = new StreamGeometry();
        geometry.FillRule = FillRule.Nonzero;

        using (var ctx = geometry.Open())
        {
            var samples = BuildCenterlineSamples();
            if (samples.Count < 2) return null;

            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();

            BuildRibbonEdges(samples, leftEdge, rightEdge);

            // --- 阶段 2：过滤倒刺 (Spike Removal) ---
            // 简单的距离过滤器：如果下一个点离当前点太近（或者回头了），说明内侧打结了
            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            // --- 阶段 3：构建 Path ---
            if (leftEdge.Count > 1 && rightEdge.Count > 1)
            {
                ctx.BeginFigure(leftEdge[0], true, true);

                // 左边缘
                AddBezierPath(ctx, leftEdge);

                // 收笔圆头
                var lastLeft = leftEdge.Last();
                var lastRight = rightEdge.Last();
                AddRoundCap(ctx, lastLeft, lastRight);

                // 右边缘 (倒序)
                var rightEdgeReversed = rightEdge.AsEnumerable().Reverse().ToList();
                AddBezierPath(ctx, rightEdgeReversed);

                // 起笔圆头
                var firstRight = rightEdge[0];
                AddRoundCap(ctx, firstRight, leftEdge[0]);
            }
        }
        return geometry;
    }

    private List<StrokePoint> BuildCenterlineSamples()
    {
        var samples = new List<StrokePoint>();
        if (_points.Count == 0) return samples;
        if (_points.Count == 1)
        {
            samples.Add(_points[0]);
            return samples;
        }

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[Math.Max(i - 1, 0)];
            var p1 = _points[i];
            var p2 = _points[i + 1];
            var p3 = _points[Math.Min(i + 2, _points.Count - 1)];

            int startStep = (i == 0) ? 0 : 1;
            for (int step = startStep; step <= UpsampleSteps; step++)
            {
                double t = step / (double)UpsampleSteps;
                var pos = CatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);
                double width = CatmullRomValue(p0.Width, p1.Width, p2.Width, p3.Width, t);
                width = ClampWidth(width);
                samples.Add(new StrokePoint(pos, width));
            }
        }

        SmoothWidths(samples);
        ApplyCapPressureOverride(samples);
        ApplyTaperingEffect(samples); // 应用笔锋效果
        return samples;
    }

    private void ApplyCapPressureOverride(List<StrokePoint> samples)
    {
        if (samples.Count < 3) return;

        double ratio = _config.CapIgnoreVelocityRatio;
        if (ratio <= 0) return;

        int capCount = Math.Max(1, (int)Math.Round(samples.Count * ratio));
        if (capCount * 2 >= samples.Count) return;

        double pressureWidth = ClampWidth(CalculatePressureWidth(samples));
        int capEnd = capCount - 1;

        for (int i = 0; i < capCount; i++)
        {
            double t = capEnd > 0 ? i / (double)capEnd : 1.0;
            double blended = Lerp(pressureWidth, samples[i].Width, t);
            samples[i] = new StrokePoint(samples[i].Position, ClampWidth(blended));
        }

        int startIndex = samples.Count - capCount;
        for (int i = startIndex; i < samples.Count; i++)
        {
            double t = capEnd > 0 ? (i - startIndex) / (double)capEnd : 1.0;
            double blended = Lerp(samples[i].Width, pressureWidth, t);
            samples[i] = new StrokePoint(samples[i].Position, ClampWidth(blended));
        }
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private static double SmoothStep(double t)
    {
        return t * t * (3.0 - 2.0 * t);
    }

    private static double CalculatePressureWidth(List<StrokePoint> samples)
    {
        int start = (int)(samples.Count * 0.2);
        int end = (int)(samples.Count * 0.8);
        if (end <= start)
        {
            start = 0;
            end = samples.Count;
        }

        double sum = 0;
        int count = 0;
        for (int i = start; i < end; i++)
        {
            sum += samples[i].Width;
            count++;
        }

        if (count == 0) return samples[0].Width;
        return sum / count;
    }

    /// <summary>
    /// 应用笔锋效果：起笔顿笔、收笔削尖
    /// </summary>
    private void ApplyTaperingEffect(List<StrokePoint> samples)
    {
        if (samples.Count < _config.MinTaperPoints * 2) return;

        // 计算笔画总长度（用于确定笔锋范围）
        double totalLength = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            totalLength += (samples[i].Position - samples[i - 1].Position).Length;
        }

        if (totalLength < 1.0) return; // 笔画太短，不应用笔锋

        // 起笔笔锋（顿笔效果）
        if (_config.SimulateStartCap)
        {
            int startCapPoints = Math.Max(_config.MinTaperPoints, (int)(samples.Count * _config.StartCapLength));
            double startTargetWidth = samples[startCapPoints].Width;

            for (int i = 0; i < startCapPoints; i++)
            {
                double t = (double)i / startCapPoints;
                double factor = SmoothStep(t);
                double taperedWidth = startTargetWidth * (0.35 + 0.65 * factor);
                samples[i] = new StrokePoint(samples[i].Position, ClampWidth(taperedWidth));
            }
        }

        // 收笔笔锋（削尖效果）
        if (_config.SimulateEndTaper)
        {
            int endTaperPoints = Math.Max(_config.MinTaperPoints, (int)(samples.Count * _config.EndTaperLength));
            int startIndex = samples.Count - endTaperPoints;
            double baseWidth = samples[startIndex].Width;

            for (int i = 0; i < endTaperPoints; i++)
            {
                int idx = startIndex + i;
                double t = (double)i / endTaperPoints;
                double factor = 1.0 - SmoothStep(t);
                double taperedWidth = baseWidth * Math.Max(0.15, factor);
                samples[idx] = new StrokePoint(samples[idx].Position, ClampWidth(taperedWidth));
            }
        }
    }

    private static WpfPoint CatmullRomPoint(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t +
                          (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                          (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

        double y = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t +
                          (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                          (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

        return new WpfPoint(x, y);
    }

    private static double CatmullRomValue(double v0, double v1, double v2, double v3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        return 0.5 * ((2 * v1) + (-v0 + v2) * t +
                      (2 * v0 - 5 * v1 + 4 * v2 - v3) * t2 +
                      (-v0 + 3 * v1 - 3 * v2 + v3) * t3);
    }

    private void SmoothWidths(List<StrokePoint> samples)
    {
        if (samples.Count < 3) return;

        int window = Math.Max(1, _config.PressureSmoothWindow);
        int half = window / 2;
        var smoothed = new double[samples.Count];

        for (int i = 0; i < samples.Count; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(samples.Count - 1, i + half);
            double sum = 0;
            int count = 0;

            for (int j = start; j <= end; j++)
            {
                sum += samples[j].Width;
                count++;
            }

            smoothed[i] = sum / count;
        }

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            samples[i] = new StrokePoint(sample.Position, ClampWidth(smoothed[i]));
        }
    }

    private void BuildRibbonEdges(List<StrokePoint> samples, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge)
    {
        if (samples.Count < 2) return;

        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < samples.Count; i++)
        {
            var pos = samples[i].Position;
            var width = ClampWidth(samples[i].Width);

            Vector dir;
            if (i == 0) dir = samples[i + 1].Position - pos;
            else if (i == samples.Count - 1) dir = pos - samples[i - 1].Position;
            else dir = samples[i + 1].Position - samples[i - 1].Position;

            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            var leftPoint = pos + normal * (width * 0.5);
            var rightPoint = pos - normal * (width * 0.5);

            bool handledCorner = false;
            if (i > 0 && i < samples.Count - 1)
            {
                var dirPrev = pos - samples[i - 1].Position;
                var dirNext = samples[i + 1].Position - pos;

                if (dirPrev.LengthSquared > 0.0001 && dirNext.LengthSquared > 0.0001)
                {
                    dirPrev.Normalize();
                    dirNext.Normalize();

                    double angle = Math.Abs(Vector.AngleBetween(dirPrev, dirNext));
                    if (angle < CornerAngleThreshold && angle > CornerMinAngle)
                    {
                        double cross = (dirPrev.X * dirNext.Y) - (dirPrev.Y * dirNext.X);
                        var normalPrev = GetNormalFromVector(dirPrev, normal);
                        var normalNext = GetNormalFromVector(dirNext, normal);

                        if (cross > 0.0001)
                        {
                            AddCornerArc(leftEdge, pos, normalPrev, normalNext, width * 0.5, false);
                            rightEdge.Add(rightPoint);
                            handledCorner = true;
                        }
                        else if (cross < -0.0001)
                        {
                            AddCornerArc(rightEdge, pos, -normalPrev, -normalNext, width * 0.5, true);
                            leftEdge.Add(leftPoint);
                            handledCorner = true;
                        }
                    }
                }
            }

            if (!handledCorner)
            {
                leftEdge.Add(leftPoint);
                rightEdge.Add(rightPoint);
            }
        }
    }

    private static void AddCornerArc(List<WpfPoint> edge, WpfPoint center, Vector startNormal, Vector endNormal, double radius, bool clockwise)
    {
        if (startNormal.LengthSquared < 0.0001 || endNormal.LengthSquared < 0.0001)
        {
            edge.Add(center + startNormal * radius);
            edge.Add(center + endNormal * radius);
            return;
        }

        startNormal.Normalize();
        endNormal.Normalize();

        double startAngle = Math.Atan2(startNormal.Y, startNormal.X);
        double endAngle = Math.Atan2(endNormal.Y, endNormal.X);
        double delta = endAngle - startAngle;

        if (clockwise)
        {
            if (delta > 0) delta -= Math.PI * 2;
        }
        else
        {
            if (delta < 0) delta += Math.PI * 2;
        }

        int segments = CornerArcSegments;
        var startPoint = center + startNormal * radius;
        if (edge.Count == 0 || (edge.Last() - startPoint).Length > 0.1)
        {
            edge.Add(startPoint);
        }

        for (int i = 1; i < segments; i++)
        {
            double t = i / (double)segments;
            double angle = startAngle + delta * t;
            edge.Add(new WpfPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }

        var endPoint = center + endNormal * radius;
        edge.Add(endPoint);
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

    private static void AddRoundCap(StreamGeometryContext ctx, WpfPoint from, WpfPoint to)
    {
        double radius = (from - to).Length * 0.5;
        if (radius < 0.1)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        ctx.ArcTo(to, new WpfSize(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
    }

    private static void AddBezierPath(StreamGeometryContext ctx, List<WpfPoint> points)
    {
        if (points.Count < 2) return;
        var bezierPoints = GetBezierPoints(points);
        if (bezierPoints.Count == 0)
        {
            for (int i = 1; i < points.Count; i++) ctx.LineTo(points[i], true, true);
            return;
        }

        ctx.PolyBezierTo(bezierPoints, true, true);
    }

    private static List<WpfPoint> GetBezierPoints(List<WpfPoint> points)
    {
        var bezierPoints = new List<WpfPoint>();
        if (points.Count < 2) return bezierPoints;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = (i == 0) ? points[i] : points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = (i + 2 < points.Count) ? points[i + 2] : points[i + 1];

            var c1 = new WpfPoint(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var c2 = new WpfPoint(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

            bezierPoints.Add(c1);
            bezierPoints.Add(c2);
            bezierPoints.Add(p2);
        }

        return bezierPoints;
    }

    /// <summary>
    /// 简单的去倒刺逻辑：移除距离过近的点
    /// </summary>
    private void FilterLoops(List<WpfPoint> edge)
    {
        if (edge.Count < 3) return;
        
        // 我们新建一个列表，只保留“有效推进”的点
        // 这是一种简化的 Loop Removal，不是严格的几何裁剪
        // 但对于手写笔画来说，大部分倒刺都是因为内侧点“退回”造成的
        
        for (int i = edge.Count - 2; i >= 1; i--)
        {
            var prev = edge[i - 1];
            var curr = edge[i];
            var next = edge[i + 1];

            // 检查锐角：如果 prev->curr 和 curr->next 夹角过小 (<30度)，说明这里是个尖刺
            var v1 = curr - prev;
            var v2 = next - curr;
            
            if (v1.Length < 0.1 || v2.Length < 0.1) continue; // 忽略重合点

            double angle = Vector.AngleBetween(v1, v2);
            // AngleBetween 返回 -180 到 180
            // 尖刺通常意味着角度接近 180 (反向)
            if (Math.Abs(angle) > 135) 
            {
                // 这是一个折返点，移除它
                edge.RemoveAt(i);
            }
        }
    }

    private void TessellateBezier(WpfPoint start, WpfPoint control, WpfPoint end, 
                                  double wStart, double wControl, double wEnd, 
                                  List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        const int steps = 6; 

        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            double u = 1.0 - t;

            double x = u * u * start.X + 2 * u * t * control.X + t * t * end.X;
            double y = u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y;
            var pos = new WpfPoint(x, y);

            double tx = 2 * u * (control.X - start.X) + 2 * t * (end.X - control.X);
            double ty = 2 * u * (control.Y - start.Y) + 2 * t * (end.Y - control.Y);
            
            var normal = new Vector(-ty, tx);
            if (normal.LengthSquared > 0.0001) normal.Normalize();

            double width = u * u * wStart + 2 * u * t * wControl + t * t * wEnd;

            // --- 倒刺预防 (Angle Limiting) ---
            // 不再无脑延伸，我们检查这个偏移点是否与骨架线“打架”
            // 但在 Tessellate 阶段很难获取全局上下文。
            // 最好的办法是在生成后统一 Filter (见 FilterLoops)
            
            AddRibbonPoints(pos, normal, width, lefts, rights);
        }
    }

    private void AddRibbonPoints(WpfPoint center, Vector normal, double width, List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        var half = width * 0.5;
        lefts.Add(center + normal * half);
        rights.Add(center - normal * half);
    }

    private static WpfPoint Mid(WpfPoint a, WpfPoint b)
    {
        return new WpfPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
    }

    private static Vector GetNormal(WpfPoint a, WpfPoint b)
    {
        var dir = b - a;
        if (dir.LengthSquared < 0.0001) return new Vector(0, 1);
        dir.Normalize();
        return new Vector(-dir.Y, dir.X);
    }
}
