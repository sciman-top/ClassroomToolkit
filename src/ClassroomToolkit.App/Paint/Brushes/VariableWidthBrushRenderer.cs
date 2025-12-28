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
    public double StartCapLength { get; set; } = 0.05;
    public double EndTaperLength { get; set; } = 0.25;  // v10: 增加到25%
    public int MinTaperPoints { get; set; } = 5;

    // 修复蝌蚪头：起笔阶段忽略速度
    public int StartVelocityRampUpPoints { get; set; } = 10;
    public double MinVelocityClamp { get; set; } = 0.3;

    // 数据验证参数
    public double MaxPointJumpDistance { get; set; } = 100.0;

    // v10 新增参数
    public double VelocityWidthFactor { get; set; } = 0.65;  // kWidth: 速度影响强度
    public double EndTaperStartProgress { get; set; } = 0.75;  // 收笔开始位置
    public double EndVelocityDecoupleStart { get; set; } = 0.85;  // 速度解耦开始位置
    public double FlyingWhiteThreshold { get; set; } = 0.7;  // 飞白速度阈值
    public double FlyingWhiteNoiseIntensity { get; set; } = 0.15;  // 飞白噪声强度

    public static BrushPhysicsConfig DefaultSmooth => new();
}

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private const int UpsampleSteps = 8;
    private const double CornerAngleThreshold = 90.0;
    private const double CornerMinAngle = 5.0;
    private const int CornerArcSegments = 4;

    // v10: 扩展点结构以存储速度和进度信息
    private struct StrokePoint
    {
        public WpfPoint Position;
        public double Width;
        public double NormalizedSpeed;  // [0, 1] 归一化速度
        public double Progress;         // [0, 1] 沿笔画进度

        public StrokePoint(WpfPoint pos, double width, double speed = 0, double progress = 0)
        {
            Position = pos;
            Width = width;
            NormalizedSpeed = speed;
            Progress = progress;
        }
    }

    private readonly List<StrokePoint> _points = new();
    private readonly Queue<double> _velocityBuffer = new();
    private readonly Queue<double> _widthBuffer = new();
    private WpfColor _color;
    private double _baseSize;
    private bool _isActive;
    private long _lastTimestamp;
    private int _pointCount;

    private double _smoothedWidth;
    private WpfPoint _smoothedPos;

    // v10: 用于速度归一化的范围跟踪
    private double _minVelocity = double.MaxValue;
    private double _maxVelocity = double.MinValue;
    private readonly Random _random = new Random();

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
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        _smoothedWidth = ClampWidth(_baseSize * 0.5);
        _smoothedPos = point;

        _points.Add(new StrokePoint(point, _smoothedWidth, 0, 0));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        // 数据验证：检查 NaN/Infinity
        if (double.IsNaN(point.X) || double.IsNaN(point.Y) ||
            double.IsInfinity(point.X) || double.IsInfinity(point.Y))
        {
            return;
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

        // 数据验证：检查异常跳变
        if (dist > _config.MaxPointJumpDistance)
        {
            return;
        }

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;

        // v10: 跟踪速度范围用于归一化
        _minVelocity = Math.Min(_minVelocity, velocity);
        _maxVelocity = Math.Max(_maxVelocity, velocity);

        double smoothVelocity = PushAndAverage(_velocityBuffer, _config.VelocitySmoothWindow, velocity);
        double targetWidth = CalculateTargetWidth(smoothVelocity, _pointCount);
        targetWidth = PushAndAverage(_widthBuffer, _config.PressureSmoothWindow, targetWidth);
        targetWidth = ClampWidth(targetWidth);

        double widthAlpha = _config.WidthSmoothing;
        _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));
        _smoothedWidth = ClampWidth(_smoothedWidth);

        // 数据验证：检查宽度有效性
        if (double.IsNaN(_smoothedWidth) || double.IsInfinity(_smoothedWidth) || _smoothedWidth <= 0)
        {
            return;
        }

        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth, smoothVelocity, 0));
        _lastTimestamp = now;
        _pointCount++;
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

        _points.Add(new StrokePoint(endPos, 0.1, 0, 1));
        _isActive = false;
    }

    public void Reset()
    {
        _points.Clear();
        _velocityBuffer.Clear();
        _widthBuffer.Clear();
        _isActive = false;
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var geometry = GenerateSmoothGeometryV10();
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
        var geo = GenerateSmoothGeometryV10();
        if (geo != null) geo.Freeze();
        return geo;
    }

    // ========================================
    // v10 算法核心实现
    // ========================================

    private double CalculateTargetWidth(double velocity, int pointIndex)
    {
        // 起笔阶段：逐渐增加速度影响
        double velocityInfluence = 1.0;
        if (pointIndex < _config.StartVelocityRampUpPoints)
        {
            velocityInfluence = (double)pointIndex / _config.StartVelocityRampUpPoints;
        }

        // 限制最小速度
        double clampedVelocity = Math.Max(velocity, _config.MinVelocityClamp);

        // v10: 使用更温和的曲线（二次方）
        var t = Math.Min(clampedVelocity / _config.VelocityThreshold, 1.0);
        var factor = 1.0 - (t * t);

        // 起笔插值
        var baseFactor = 0.8;
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

    /// <summary>
    /// v10: 主几何生成方法 - 实现增强动力学、防止骨头效果、飞白等
    /// </summary>
    private Geometry? GenerateSmoothGeometryV10()
    {
        if (_points.Count < 2) return null;

        var geometry = new StreamGeometry();
        geometry.FillRule = FillRule.Nonzero;

        using (var ctx = geometry.Open())
        {
            // 阶段1: 构建采样点并应用v10算法
            var samples = BuildCenterlineSamplesV10();
            if (samples.Count < 2) return null;

            // 阶段2: 生成左右边缘
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();
            BuildRibbonEdgesV10(samples, leftEdge, rightEdge);

            // 阶段3: 过滤倒刺
            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            // 阶段4: 构建最终路径（使用改进的尖端）
            if (leftEdge.Count > 1 && rightEdge.Count > 1)
            {
                BuildStrokePathV10(ctx, leftEdge, rightEdge, samples.First().Width, samples.Last().Width);
            }
        }

        return geometry;
    }

    /// <summary>
    /// v10: 构建中心线采样点，应用完整的速度→宽度映射和笔锋逻辑
    /// </summary>
    private List<StrokePoint> BuildCenterlineSamplesV10()
    {
        var samples = new List<StrokePoint>();
        if (_points.Count == 0) return samples;
        if (_points.Count == 1)
        {
            samples.Add(_points[0]);
            return samples;
        }

        // 计算笔画总长度用于进度
        double totalLength = 0;
        for (int i = 1; i < _points.Count; i++)
        {
            totalLength += (_points[i].Position - _points[i - 1].Position).Length;
        }

        // 归一化速度范围
        double velocityRange = _maxVelocity - _minVelocity;
        if (velocityRange < 0.001) velocityRange = 1.0;

        double accumulatedLength = 0;

        // Catmull-Rom 插值生成密集采样
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

                // 插值速度和进度
                double speed = CatmullRomValue(p0.NormalizedSpeed, p1.NormalizedSpeed, p2.NormalizedSpeed, p3.NormalizedSpeed, t);
                double rawProgress = CatmullRomValue(p0.Progress, p1.Progress, p2.Progress, p3.Progress, t);

                // 更新进度
                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }
                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;

                // v10 核心算法: 应用增强的动力学
                double normalizedSpeed = Math.Clamp((speed - _minVelocity) / velocityRange, 0, 1);
                double width = CalculateWidthV10(p1.Width, normalizedSpeed, progress);

                samples.Add(new StrokePoint(pos, width, normalizedSpeed, progress));
            }
        }

        // 应用宽度平滑（低通滤波）
        SmoothWidthsV10(samples);

        return samples;
    }

    /// <summary>
    /// v10 核心算法: 速度→宽度映射 + 防止骨头效果 + 笔锋约束
    /// </summary>
    private double CalculateWidthV10(double baseWidth, double normalizedSpeed, double progress)
    {
        // ===== 步骤1: 增强动力学（速度→宽度映射）=====
        // velocityFactor = 1.0 - (kWidth * normalizedSpeed)
        // kWidth = 0.65 (激进地让快速笔画变细)
        double velocityFactor = 1.0 - (_config.VelocityWidthFactor * normalizedSpeed);

        // 限制在合理范围 [0.2, 1.0]，避免过细或负宽度
        velocityFactor = Math.Clamp(velocityFactor, 0.2, 1.0);

        // ===== 步骤2: 关键修复 - 收笔时解耦速度（防止"骨头"凸起）=====
        double blendedVelocityFactor = velocityFactor;

        if (progress > _config.EndVelocityDecoupleStart)
        {
            // 在收笔区域 (progress > 0.85)，逐渐减少速度影响
            double taperZoneProgress = Math.Clamp(
                (progress - _config.EndVelocityDecoupleStart) / (1.0 - _config.EndVelocityDecoupleStart),
                0.0, 1.0
            );

            // 线性插值: velocityFactor → 1.0
            blendedVelocityFactor = Lerp(velocityFactor, 1.0, taperZoneProgress);
        }

        // ===== 步骤3: 应用速度影响计算目标宽度 =====
        double targetWidth = baseWidth * blendedVelocityFactor;

        // ===== 步骤4: 精修笔锋逻辑（更长且更安全）=====
        if (progress > _config.EndTaperStartProgress)
        {
            // 收笔区域: 最后 25% 的笔画
            double taperProgress = Math.Clamp(
                (progress - _config.EndTaperStartProgress) / (1.0 - _config.EndTaperStartProgress),
                0.0, 1.0
            );

            // taperFactor = 0.3 + 0.7 * (1.0 - taperProgress)
            // 从 100% 线性减少到 30%
            double taperFactor = 0.3 + 0.7 * (1.0 - taperProgress);
            targetWidth *= taperFactor;

            // ===== 约束: 最小宽度防止老鼠尾巴 =====
            double minWidth = _baseSize * 0.3;
            targetWidth = Math.Max(targetWidth, minWidth);
        }

        return ClampWidth(targetWidth);
    }

    /// <summary>
    /// v10: 宽度平滑（低通滤波器）
    /// 使用更激进的平滑参数 (0.65 * prev + 0.35 * current)
    /// </summary>
    private void SmoothWidthsV10(List<StrokePoint> samples)
    {
        if (samples.Count < 2) return;

        for (int i = 1; i < samples.Count; i++)
        {
            double smoothed = samples[i - 1].Width * 0.65 + samples[i].Width * 0.35;
            samples[i] = new StrokePoint(
                samples[i].Position,
                ClampWidth(smoothed),
                samples[i].NormalizedSpeed,
                samples[i].Progress
            );
        }
    }

    /// <summary>
    /// v10: 生成左右边缘，添加飞白效果（边缘噪声）
    /// </summary>
    private void BuildRibbonEdgesV10(List<StrokePoint> samples, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge)
    {
        if (samples.Count < 2) return;

        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < samples.Count; i++)
        {
            var pos = samples[i].Position;
            var width = ClampWidth(samples[i].Width);
            var speed = samples[i].NormalizedSpeed;

            // 计算法线
            Vector dir;
            if (i == 0) dir = samples[i + 1].Position - pos;
            else if (i == samples.Count - 1) dir = pos - samples[i - 1].Position;
            else dir = samples[i + 1].Position - samples[i - 1].Position;

            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            // ===== v10: 模拟"飞白"效果（干笔边缘噪声）=====
            double edgeNoise = 0;

            if (speed > _config.FlyingWhiteThreshold)
            {
                // 高速时添加边缘噪声，模拟粗糙干笔效果
                double noiseMagnitude = _baseSize * _config.FlyingWhiteNoiseIntensity * speed;
                edgeNoise = _random.NextDouble() * 2.0 - 1.0; // [-1, 1]
                edgeNoise *= noiseMagnitude;
            }

            // 应用噪声（只在边缘方向，不影响切线方向）
            var halfWidth = width * 0.5;
            var leftPoint = pos + normal * (halfWidth + edgeNoise);
            var rightPoint = pos - normal * (halfWidth + edgeNoise);

            // 角处理（保持原有逻辑）
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
                            AddCornerArc(leftEdge, pos, normalPrev, normalNext, halfWidth, false);
                            rightEdge.Add(rightPoint);
                            handledCorner = true;
                        }
                        else if (cross < -0.0001)
                        {
                            AddCornerArc(rightEdge, pos, -normalPrev, -normalNext, halfWidth, true);
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

    /// <summary>
    /// v10: 构建笔画路径，使用改进的尖端构造（贝塞尔曲线代替简单圆弧）
    /// </summary>
    private void BuildStrokePathV10(StreamGeometryContext ctx, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, double startWidth, double endWidth)
    {
        ctx.BeginFigure(leftEdge[0], true, true);

        // 左边缘
        AddBezierPath(ctx, leftEdge);

        // 收笔尖端 - 使用平滑的贝塞尔曲线
        var lastLeft = leftEdge.Last();
        var lastRight = rightEdge.Last();
        AddSmoothCapV10(ctx, lastLeft, lastRight, endWidth, true);

        // 右边缘 (倒序)
        var rightEdgeReversed = rightEdge.AsEnumerable().Reverse().ToList();
        AddBezierPath(ctx, rightEdgeReversed);

        // 起笔尖端
        var firstRight = rightEdge[0];
        AddSmoothCapV10(ctx, firstRight, leftEdge[0], startWidth, false);
    }

    /// <summary>
    /// v10: 改进的尖端构造 - 根据宽度自适应形状
    /// </summary>
    private void AddSmoothCapV10(StreamGeometryContext ctx, WpfPoint from, WpfPoint to, double width, bool isEnd)
    {
        double capWidth = (from - to).Length;

        // 如果尖端很窄，直接闭合（形成尖锐笔锋）
        if (capWidth < _baseSize * 0.3)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        // 如果尖端较宽，使用圆滑的贝塞尔曲线
        // 创建一个半圆形或椭圆形的尖端
        var midPoint = new WpfPoint((from.X + to.X) * 0.5, (from.Y + to.Y) * 0.5);
        var dir = to - from;
        if (dir.LengthSquared > 0.0001) dir.Normalize();
        else dir = new Vector(1, 0);

        var normal = new Vector(-dir.Y, dir.X);
        var controlOffset = normal * (capWidth * 0.3);

        // 使用二次贝塞尔曲线创建平滑尖端
        var controlPoint = midPoint + controlOffset;
        ctx.QuadraticBezierTo(controlPoint, to, true, true);
    }

    // ========================================
    // 辅助方法
    // ========================================

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

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
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

        for (int i = edge.Count - 2; i >= 1; i--)
        {
            var prev = edge[i - 1];
            var curr = edge[i];
            var next = edge[i + 1];

            var v1 = curr - prev;
            var v2 = next - curr;

            if (v1.Length < 0.1 || v2.Length < 0.1) continue;

            double angle = Vector.AngleBetween(v1, v2);
            if (Math.Abs(angle) > 135)
            {
                edge.RemoveAt(i);
            }
        }
    }
}
