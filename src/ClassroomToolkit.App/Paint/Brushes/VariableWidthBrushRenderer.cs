using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public double WidthSmoothing { get; set; } = 0.91;
    public bool SimulateStartCap { get; set; } = true;
    public bool SimulateEndTaper { get; set; } = true;
    public double VelocityThreshold { get; set; } = 1.5;
    public int VelocitySmoothWindow { get; set; } = 6;
    public int PressureSmoothWindow { get; set; } = 7;
    public double CapIgnoreVelocityRatio { get; set; } = 0.1;

    // 笔锋效果参数
    public double StartCapLength { get; set; } = 0.05;
    public double EndTaperLength { get; set; } = 0.3;  // v10: 增加到25%
    public int MinTaperPoints { get; set; } = 5;

    // 修复蝌蚪头：起笔阶段忽略速度
    public int StartVelocityRampUpPoints { get; set; } = 10;
    public double MinVelocityClamp { get; set; } = 0.3;

    // 数据验证参数
    public double MaxPointJumpDistance { get; set; } = 100.0;

    // v10 新增参数
    public double VelocityWidthFactor { get; set; } = 0.66;  // kWidth: 速度影响强度
    public double EndTaperStartProgress { get; set; } = 0.75;  // 收笔开始位置
    public double EndVelocityDecoupleStart { get; set; } = 0.85;  // 速度解耦开始位置
    public double FlyingWhiteThreshold { get; set; } = 0.86;  // 飞白速度阈值
    public double FlyingWhiteNoiseIntensity { get; set; } = 0.02;  // 飞白噪声强度

    // v11 新增参数
    public double DunBiSpeedThreshold { get; set; } = 0.4;  // 顿笔速度阈值
    public double DunBiSpreadRate { get; set; } = 0.55;     // 墨水扩散速率
    public double DunBiMaxAccumulation { get; set; } = 1.2; // 最大累积倍数
    public double DunBiDecayRate { get; set; } = 0.2;     // 累积衰减速率
    public double FlyingWhiteNoiseFrequency { get; set; } = 4.2;  // 噪声频率
    public double FlyingWhiteNoiseReductionProgress { get; set; } = 0.78;  // 噪声减少起点
    public double CapRoundThreshold { get; set; } = 0.72;   // 圆笔锋阈值（相对于 baseSize）
    public double TaperMinWidthFactor { get; set; } = 0.6; // 笔锋最小宽度因子
    public double FiberNoiseIntensity { get; set; } = 0.003; // 纸张纤维噪声强度
    public double FiberNoiseFrequency { get; set; } = 0.35; // 纸张纤维噪声频率
    public double AnisotropyStrength { get; set; } = 0.05; // 笔锋方向性强度
    public double BrushAngleDegrees { get; set; } = -45.0; // 笔锋默认角度

    // 多毫叠加参数
    public bool EnableMultiRibbon { get; set; } = true;
    public int MultiRibbonCount { get; set; } = 3;
    public double MultiRibbonOffsetFactor { get; set; } = 0.12;
    public double MultiRibbonOffsetJitter { get; set; } = 0.03;
    public double MultiRibbonWidthJitter { get; set; } = 0.06;
    public double MultiRibbonWidthFalloff { get; set; } = 0.2;

    public static BrushPhysicsConfig DefaultSmooth => new();
}

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private const double DirectionNoiseAmplitude = 0.009;
    private const double DirectionNoiseFrequency = 0.4;

    private const int UpsampleSteps = 8;
    private const double CornerAngleThreshold = 90.0;
    private const double CornerMinAngle = 5.0;
    private const int CornerArcSegments = 6;

    // v10/v11: 扩展点结构以存储速度、进度和累积宽度信息
    private struct StrokePoint
    {
        public WpfPoint Position;
        public double Width;
        public double Speed;            // 原始速度（px/ms）
        public double NormalizedSpeed;  // [0, 1] 归一化速度
        public double Progress;         // [0, 1] 沿笔画进度
        public double AccumulatedWidth; // v11: 墨水累积宽度（顿笔效果）

        public StrokePoint(WpfPoint pos, double width, double speed = 0, double normalizedSpeed = 0, double progress = 0, double accumulated = 0)
        {
            Position = pos;
            Width = width;
            Speed = speed;
            NormalizedSpeed = normalizedSpeed;
            Progress = progress;
            AccumulatedWidth = accumulated;
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
    private double _noiseSeed;

    private double _smoothedWidth;
    private WpfPoint _smoothedPos;

    // v10: 用于速度归一化的范围跟踪
    private double _minVelocity = double.MaxValue;
    private double _maxVelocity = double.MinValue;
    private readonly Random _random = new Random();

    // v11: 顿笔墨水累积状态
    private double _accumulatedWidth;
    private double _lastInkFlow = 1.0;

    private readonly BrushPhysicsConfig _config = BrushPhysicsConfig.DefaultSmooth;

    public bool IsActive => _isActive;
    public double LastInkFlow => _lastInkFlow;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = ClampWidth(baseSize * 0.8);
        _smoothedPos = new WpfPoint(0, 0);
        _lastInkFlow = 1.0;
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
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _noiseSeed = _random.NextDouble() * 1000.0;
        _lastTimestamp = Stopwatch.GetTimestamp();
        _lastInkFlow = 1.0;

        _smoothedWidth = ClampWidth(_baseSize * 0.5);
        _smoothedPos = point;

        _points.Add(new StrokePoint(point, _smoothedWidth, 0, 0, 0, 0));
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

        // 去噪：忽略过小的移动（阈值随当前宽度调整）
        double minDist = Math.Clamp(_smoothedWidth * 0.15, 0.4, 2.0);
        if (dist < minDist) return;

        // 数据验证：检查异常跳变
        if (dist > _config.MaxPointJumpDistance)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        double dtMs = (now - _lastTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (dtMs < 1) dtMs = 1;

        double velocity = dist / dtMs;

        // v10: 跟踪速度范围用于归一化
        _minVelocity = Math.Min(_minVelocity, velocity);
        _maxVelocity = Math.Max(_maxVelocity, velocity);

        double smoothVelocity = PushAndAverage(_velocityBuffer, _config.VelocitySmoothWindow, velocity);
        double targetWidth = CalculateTargetWidth(smoothVelocity, _pointCount);

        // 轻微各向异性：模拟毛笔扁平笔锋（仅轻度影响宽度）
        if (dist > 0.001)
        {
            var dir = _smoothedPos - lastPt.Position;
            if (dir.LengthSquared > 0.0001)
            {
                double angle = Math.Atan2(dir.Y, dir.X);
                double brushAngle = _config.BrushAngleDegrees * Math.PI / 180.0;
                double angleFactor = 1.0 - (_config.AnisotropyStrength * Math.Cos(2 * (angle - brushAngle)));
                angleFactor = Math.Clamp(angleFactor, 0.9, 1.1);
                targetWidth = ClampWidth(targetWidth * angleFactor);
            }
        }

        // v11: 顿笔逻辑 - 低速时累积墨水扩散
        double normalizedSpeed = Math.Clamp((smoothVelocity - _config.MinVelocityClamp) /
                                            (_config.VelocityThreshold - _config.MinVelocityClamp), 0, 1);

        if (normalizedSpeed < _config.DunBiSpeedThreshold)
        {
            // 低速时累积宽度（墨水扩散）
            double accumulationRate = _config.DunBiSpreadRate / Math.Max(velocity, 0.1);
            double deltaTime = dtMs / 1000.0; // 转换为秒
            _accumulatedWidth += accumulationRate * deltaTime;

            // 限制最大累积
            double maxAccumulation = targetWidth * (_config.DunBiMaxAccumulation - 1.0);
            _accumulatedWidth = Math.Min(_accumulatedWidth, maxAccumulation);
        }
        else
        {
            // 高速时衰减累积
            _accumulatedWidth *= (1.0 - _config.DunBiDecayRate);
        }

        // 应用累积宽度
        double effectiveWidth = targetWidth + _accumulatedWidth;

        targetWidth = PushAndAverage(_widthBuffer, _config.PressureSmoothWindow, effectiveWidth);
        targetWidth = ClampWidth(targetWidth);

        double widthAlpha = _config.WidthSmoothing;
        _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));
        _smoothedWidth = ClampWidth(_smoothedWidth);

        // 数据验证：检查宽度有效性
        if (double.IsNaN(_smoothedWidth) || double.IsInfinity(_smoothedWidth) || _smoothedWidth <= 0)
        {
            return;
        }

        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth, smoothVelocity, 0, 0, _accumulatedWidth));
        UpdateInkFlow();
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

        var extension = _baseSize * 0.2;
        var endPos = point + dir * extension;

        var minWidth = ClampWidth(_baseSize * _config.TaperMinWidthFactor);
        _points.Add(new StrokePoint(endPos, minWidth, 0, 0, 1));
        UpdateInkFlow();
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
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _lastInkFlow = 1.0;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var geometries = GetLastRibbonGeometries();
        if (geometries == null || geometries.Count == 0) return;

        foreach (var item in geometries)
        {
            var brush = new SolidColorBrush(_color)
            {
                Opacity = GetRibbonOpacity(item.RibbonT)
            };
            brush.Freeze();
            dc.DrawGeometry(brush, null, item.Geometry);
        }
    }

    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geometries = GetLastRibbonGeometries();
        if (geometries == null || geometries.Count == 0) return null;
        if (geometries.Count == 1)
        {
            var single = geometries[0].Geometry;
            if (single != null) single.Freeze();
            return single;
        }

        var group = new GeometryGroup
        {
            FillRule = FillRule.Nonzero
        };
        foreach (var item in geometries)
        {
            group.Children.Add(item.Geometry);
        }
        group.Freeze();
        return group;
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

    // ========================================
    // v10 算法核心实现
    // ========================================

    /// <summary>
    /// v13: 生产级笔画几何生成（主体保持稳定，端帽自然）
    /// </summary>
    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;
        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2) return null;

        var geometries = BuildRibbonGeometries(samples);
        if (geometries.Count == 0) return null;
        if (geometries.Count == 1) return geometries[0].Geometry;

        var group = new GeometryGroup
        {
            FillRule = FillRule.Nonzero
        };
        foreach (var item in geometries)
        {
            group.Children.Add(item.Geometry);
        }
        return group;
    }

    private int ResolveRibbonCount()
    {
        if (!_config.EnableMultiRibbon) return 1;
        return Math.Clamp(_config.MultiRibbonCount, 1, 7);
    }

    public sealed class RibbonGeometry
    {
        public RibbonGeometry(Geometry geometry, double ribbonT)
        {
            Geometry = geometry;
            RibbonT = ribbonT;
        }

        public Geometry Geometry { get; }
        public double RibbonT { get; }
    }

    public IReadOnlyList<RibbonGeometry>? GetLastRibbonGeometries()
    {
        if (_points.Count < 2) return null;
        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2) return null;
        var geometries = BuildRibbonGeometries(samples);
        return geometries.Count == 0 ? null : geometries;
    }

    public double GetRibbonOpacity(double ribbonT)
    {
        double baseOpacity = Lerp(1.0, 0.45, ribbonT);
        double inkOpacity = Lerp(0.65, 1.0, _lastInkFlow);
        return Math.Clamp(baseOpacity * inkOpacity, 0.2, 1.0);
    }

    private List<RibbonGeometry> BuildRibbonGeometries(List<StrokePoint> samples)
    {
        var result = new List<RibbonGeometry>();
        int ribbonCount = ResolveRibbonCount();
        if (ribbonCount <= 1)
        {
            var single = BuildRibbonGeometry(samples, 0, 0);
            if (single != null) result.Add(new RibbonGeometry(single, 0));
            return result;
        }

        double centerIndex = (ribbonCount - 1) * 0.5;
        for (int i = 0; i < ribbonCount; i++)
        {
            double ribbonT = centerIndex > 0 ? Math.Abs(i - centerIndex) / centerIndex : 0;
            var ribbonSamples = BuildRibbonSamples(samples, i, ribbonCount);
            var ribbonGeometry = BuildRibbonGeometry(ribbonSamples, ribbonT, i * 17.7);
            if (ribbonGeometry != null)
            {
                result.Add(new RibbonGeometry(ribbonGeometry, ribbonT));
            }
        }

        return result;
    }

    private Geometry? BuildRibbonGeometry(List<StrokePoint> samples, double ribbonT, double noiseSeedOffset)
    {
        if (samples.Count < 2) return null;

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = geometry.Open())
        {
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();
            BuildRibbonEdgesV10(samples, leftEdge, rightEdge, noiseSeedOffset, ribbonT);

            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            if (leftEdge.Count > 1 && rightEdge.Count > 1)
            {
                BuildStrokePathV10(ctx, leftEdge, rightEdge, samples);
            }
            else
            {
                return null;
            }
        }

        return geometry;
    }

    private List<StrokePoint> BuildRibbonSamples(List<StrokePoint> baseSamples, int ribbonIndex, int ribbonCount)
    {
        var result = new List<StrokePoint>(baseSamples.Count);
        if (baseSamples.Count == 0) return result;

        double centerIndex = (ribbonCount - 1) * 0.5;
        double offsetIndex = ribbonIndex - centerIndex;
        double ribbonT = centerIndex > 0 ? Math.Abs(offsetIndex) / centerIndex : 0;
        double ribbonPhase = _noiseSeed + (ribbonIndex + 1) * 31.7;

        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < baseSamples.Count; i++)
        {
            var sample = baseSamples[i];

            Vector dir;
            if (i == 0) dir = baseSamples[i + 1].Position - sample.Position;
            else if (i == baseSamples.Count - 1) dir = sample.Position - baseSamples[i - 1].Position;
            else dir = baseSamples[i + 1].Position - baseSamples[i - 1].Position;

            if (dir.LengthSquared > 0.0001) dir.Normalize();
            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            double offsetStep = sample.Width * _config.MultiRibbonOffsetFactor;
            double offsetNoise = FractalNoise(ribbonPhase + (i * 0.18), 0.35) * (sample.Width * _config.MultiRibbonOffsetJitter);
            double offset = (offsetIndex * offsetStep) + offsetNoise;

            double widthScale = 1.0 - (ribbonT * _config.MultiRibbonWidthFalloff);
            double widthNoise = FractalNoise(ribbonPhase + (i * 0.12), 0.55) * _config.MultiRibbonWidthJitter;
            double scaledWidth = ClampWidth(sample.Width * Math.Clamp(widthScale + widthNoise, 0.6, 1.2));

            var pos = sample.Position + normal * offset;
            result.Add(new StrokePoint(pos, scaledWidth, sample.Speed, sample.NormalizedSpeed, sample.Progress, sample.AccumulatedWidth));
        }

        return result;
    }

    private void UpdateInkFlow()
    {
        if (_points.Count == 0)
        {
            _lastInkFlow = 1.0;
            return;
        }

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        double speedSum = 0;
        double accumulationSum = 0;

        foreach (var point in _points)
        {
            speedSum += Math.Clamp(point.Speed / maxSpeed, 0, 1);
            accumulationSum += point.AccumulatedWidth;
        }

        double avgSpeed = speedSum / _points.Count;
        double avgAccumulation = accumulationSum / Math.Max(_baseSize, 0.001);
        double flow = 1.0 - avgSpeed;
        flow += Math.Clamp(avgAccumulation * 0.35, 0, 0.4);
        _lastInkFlow = Math.Clamp(flow, 0.25, 1.0);
    }

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
    /// v11 修复: 确保使用 NonZero 填充规则防止裂纹
    /// </summary>
    private Geometry? GenerateSmoothGeometryV10()
    {
        if (_points.Count < 2) return null;

        var geometry = new StreamGeometry();
        geometry.FillRule = FillRule.Nonzero; // v11: 使用 NonZero 防止白裂纹

        using (var ctx = geometry.Open())
        {
            // 阶段1: 构建采样点并应用v10算法
            var samples = BuildCenterlineSamplesV10();
            if (samples.Count < 2) return null;

            // 阶段2: 生成左右边缘
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();
            BuildRibbonEdgesV10(samples, leftEdge, rightEdge, 0, 0);

            // 阶段3: 过滤倒刺
            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            // 阶段4: 构建最终路径（使用改进的尖端）
            if (leftEdge.Count > 1 && rightEdge.Count > 1)
            {
                BuildStrokePathV10(ctx, leftEdge, rightEdge, samples);
            }
        }

        return geometry;
    }

    /// <summary>
    /// v13: 最终采样与宽度计算（严格按速度映射 + 骨头修复 + 平滑）
    /// </summary>
    private List<StrokePoint> BuildCenterlineSamplesFinal()
    {
        var samples = new List<StrokePoint>();
        if (_points.Count == 0) return samples;
        if (_points.Count == 1)
        {
            var p = _points[0];
            samples.Add(new StrokePoint(p.Position, ClampWidth(p.Width), 0, 0, 0, p.AccumulatedWidth));
            return samples;
        }

        double totalLength = 0;
        for (int i = 1; i < _points.Count; i++)
        {
            totalLength += (_points[i].Position - _points[i - 1].Position).Length;
        }

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        double accumulatedLength = 0;

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

                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);

                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }

                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);
                double normSpeed = Math.Clamp(speed / maxSpeed, 0, 1);

                double velocityFactor = 1.0 - (0.65 * normSpeed);

                double taperZone = 0;
                if (progress > 0.85)
                {
                    taperZone = Math.Clamp((progress - 0.85) / 0.15, 0, 1);
                    velocityFactor = Lerp(velocityFactor, 1.0, taperZone);
                }

                double pressure = 1.0 + (accumulatedWidth / Math.Max(_baseSize, 0.001));
                double targetWidth = _baseSize * pressure * velocityFactor;

                double currentWidth = targetWidth;
                if (samples.Count > 0)
                {
                    currentWidth = samples[^1].Width * 0.6 + targetWidth * 0.4;
                }

                if (progress > 0.85)
                {
                    double taperFactor = 1.0 - (taperZone * taperZone);
                    currentWidth *= taperFactor;
                }

                currentWidth = ClampWidth(currentWidth);
                samples.Add(new StrokePoint(pos, currentWidth, speed, normSpeed, progress, accumulatedWidth));
            }
        }

        return samples;
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

                // 插值速度、进度和累积宽度
                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);

                // 更新进度
                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }
                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);

                // v10 核心算法: 应用增强的动力学
                double normalizedSpeed = Math.Clamp((speed - _minVelocity) / velocityRange, 0, 1);
                double width = CalculateWidthV10(p1.Width, normalizedSpeed, progress);

                samples.Add(new StrokePoint(pos, width, speed, normalizedSpeed, progress, accumulatedWidth));
            }
        }

        // 应用宽度平滑（低通滤波）
        SmoothWidthsV10(samples);

        return samples;
    }

    /// <summary>
    /// v10 核心算法: 速度→宽度映射 + 防止骨头效果 + 笔锋约束
    /// v11 改进: 使用 ease-out 笔锋曲线
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

        // ===== 步骤4: v11 改进 - 使用 ease-out 凸曲线笔锋 =====
        if (progress > _config.EndTaperStartProgress)
        {
            // 收笔区域: 最后 25% 的笔画
            double taperProgress = Math.Clamp(
                (progress - _config.EndTaperStartProgress) / (1.0 - _config.EndTaperStartProgress),
                0.0, 1.0
            );

            // v11: 使用凸曲线 (ease-out): 1.0 - t²
            // 而不是线性: 1.0 - t
            // 这样让笔画在大部分时间保持粗壮，只在最后急剧收缩
            double taperCurve = 1.0 - (taperProgress * taperProgress);

            // 应用曲线，从 100% 缩减到 35%
            double taperFactor = _config.TaperMinWidthFactor + (1.0 - _config.TaperMinWidthFactor) * taperCurve;
            targetWidth *= taperFactor;

            // ===== 约束: 最小宽度防止老鼠尾巴 =====
            double minWidth = _baseSize * _config.TaperMinWidthFactor;
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
                samples[i].Speed,
                samples[i].NormalizedSpeed,
                samples[i].Progress,
                samples[i].AccumulatedWidth // v11: 保留累积宽度
            );
        }
    }

    /// <summary>
    /// v10: 生成左右边缘，添加飞白效果（边缘噪声）
    /// v11 修复: 使用平滑噪声函数，防止白裂纹和交叉
    /// </summary>
    private void BuildRibbonEdgesV10(List<StrokePoint> samples, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, double noiseSeedOffset, double ribbonT)
    {
        if (samples.Count < 2) return;

        Vector lastNormal = new Vector(0, 1);
        double edgeNoiseScale = Lerp(0.4, 1.0, ribbonT);
        double fiberNoiseScale = Lerp(0.5, 1.0, ribbonT);
        double flyingWhiteThreshold = Math.Clamp(_config.FlyingWhiteThreshold - ((1.0 - _lastInkFlow) * 0.12), 0.6, 0.95);

        for (int i = 0; i < samples.Count; i++)
        {
            var pos = samples[i].Position;
            var width = ClampWidth(samples[i].Width);
            var speed = samples[i].NormalizedSpeed;
            var progress = samples[i].Progress;

            // 方向扰动宽度噪声：沿笔画方向变化，避免侧向锯齿
            double directionNoise = Math.Sin(i * DirectionNoiseFrequency) * width * DirectionNoiseAmplitude;
            directionNoise *= (1.0 - progress);
            width = ClampWidth(width + directionNoise);

            // ===== v11: 计算切线和法线（确保噪声只在法线方向）=====
            Vector dir;
            if (i == 0) dir = samples[i + 1].Position - pos;
            else if (i == samples.Count - 1) dir = pos - samples[i - 1].Position;
            else dir = samples[i + 1].Position - samples[i - 1].Position;

            if (dir.LengthSquared > 0.0001)
                dir.Normalize();

            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            // ===== v11 修复: 使用平滑噪声函数代替随机噪声 =====
            double edgeNoise = 0;
            double phase = _noiseSeed + noiseSeedOffset + i * 0.45 + (progress * 3.2) + ((pos.X + pos.Y) * 0.01);

            if (speed > flyingWhiteThreshold && progress < _config.FlyingWhiteNoiseReductionProgress)
            {
                double frequency = _config.FlyingWhiteNoiseFrequency;
                double smoothNoise = FractalNoise(phase, frequency);

                // 噪声振幅
                double inkDryBoost = Lerp(1.0, 1.35, 1.0 - _lastInkFlow);
                double noiseAmplitude = _baseSize * _config.FlyingWhiteNoiseIntensity * speed * inkDryBoost * edgeNoiseScale;

                // v11: 在收笔区域减少噪声（防止毛发状边缘）
                double noiseReduction = 1.0;
                if (progress > _config.FlyingWhiteNoiseReductionProgress)
                {
                    double t = Math.Clamp(
                        (progress - _config.FlyingWhiteNoiseReductionProgress) /
                        (1.0 - _config.FlyingWhiteNoiseReductionProgress), 0, 1);
                    noiseReduction = 1.0 - t; // 线性减少到 0
                }

                edgeNoise = smoothNoise * noiseAmplitude * noiseReduction;

                // ===== v11 安全约束: 防止左右轮廓交叉 =====
                // 确保噪声不超过宽度的一半
                double maxNoise = width * 0.09; // 最大为 9% 半宽
                edgeNoise = Math.Clamp(edgeNoise, -maxNoise, maxNoise);
            }

            // 纸张纤维噪声（轻微，增强质感）
            double fiberNoise = FractalNoise(phase, _config.FiberNoiseFrequency);
            double fiberFactor = 0.2 + (speed * 0.8);
            double accumulation = Math.Clamp(samples[i].AccumulatedWidth / (_baseSize * 0.8), 0, 1);
            double fiberAttenuation = 1.0 - (accumulation * 0.6);
            double fiberAmplitude = width * _config.FiberNoiseIntensity * fiberFactor * fiberAttenuation * fiberNoiseScale;
            edgeNoise += fiberNoise * fiberAmplitude;

            // 应用噪声（只在法线方向）
            var halfWidth = width * 0.5;
            var leftOffset = halfWidth + edgeNoise;
            var rightOffset = halfWidth - edgeNoise;

            // ===== v11 安全约束: 确保左右不交叉 =====
            // 左右偏移必须都保持正值
            leftOffset = Math.Max(leftOffset, width * 0.1);  // 最小 10%
            rightOffset = Math.Max(rightOffset, width * 0.1); // 最小 10%

            var leftPoint = pos + normal * leftOffset;
            var rightPoint = pos - normal * rightOffset;

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

                        bool allowCornerPatch = speed < 0.35 && angle < 80 && angle > 12;

                        if (allowCornerPatch && cross > 0.0001)
                        {
                            AddCornerReinforcement(leftEdge, pos, normalPrev, normalNext, width);
                            AddCornerArc(leftEdge, pos, normalPrev, normalNext, halfWidth, false);
                            rightEdge.Add(rightPoint);
                            handledCorner = true;
                        }
                        else if (allowCornerPatch && cross < -0.0001)
                        {
                            AddCornerReinforcement(rightEdge, pos, -normalPrev, -normalNext, width);
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
    /// v13: 自然笔锋尖端（起笔/收笔）处理
    /// </summary>
    private void BuildStrokePathV10(StreamGeometryContext ctx, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, List<StrokePoint> samples)
    {
        ctx.BeginFigure(leftEdge[0], true, true);

        // 左边缘
        AddBezierPath(ctx, leftEdge);

        var endCap = BuildCapData(samples, true);
        AddCapV13(ctx, leftEdge.Last(), rightEdge.Last(), endCap);

        // 右边缘 (倒序)
        var rightEdgeReversed = rightEdge.AsEnumerable().Reverse().ToList();
        AddBezierPath(ctx, rightEdgeReversed);

        var startCap = BuildCapData(samples, false);
        AddCapV13(ctx, rightEdge[0], leftEdge[0], startCap);
    }

    private struct CapData
    {
        public WpfPoint TipPoint;
        public double Width;
        public double PressureDropRate;

        public CapData(WpfPoint tipPoint, double width, double pressureDropRate)
        {
            TipPoint = tipPoint;
            Width = width;
            PressureDropRate = pressureDropRate;
        }
    }

    private CapData BuildCapData(List<StrokePoint> samples, bool isEnd)
    {
        int count = samples.Count;
        if (count < 2)
        {
            return new CapData(samples[0].Position, ClampWidth(samples[0].Width), 0);
        }

        int lastIndex = count - 1;
        int prevIndex = Math.Max(0, lastIndex - 1);

        WpfPoint basePoint = isEnd ? samples[lastIndex].Position : samples[0].Position;
        WpfPoint refPoint = isEnd ? samples[prevIndex].Position : samples[1].Position;

        var dir = isEnd ? (basePoint - refPoint) : (refPoint - basePoint);
        if (dir.LengthSquared < 0.0001)
        {
            dir = new Vector(1, 0);
        }
        else
        {
            dir.Normalize();
        }

        double width = ClampWidth(isEnd ? samples[lastIndex].Width : samples[0].Width);
        double baseForTip = isEnd ? width : _baseSize;

        // TipPoint 示例: tip = basePoint +/- dir * tipLen
        double tipLen = ClampTipLength(baseForTip);
        var tipPoint = isEnd ? basePoint + dir * tipLen : basePoint - dir * tipLen;

        double dropRate = ComputePressureDropRate(samples, isEnd);
        return new CapData(tipPoint, width, dropRate);
    }

    private static double ClampTipLength(double width)
    {
        double minLen = width * 0.3;
        double maxLen = width * 1.2;
        double desired = width * 0.9;
        return Math.Clamp(desired, minLen, maxLen);
    }

    private double ComputePressureDropRate(List<StrokePoint> samples, bool isEnd)
    {
        int count = samples.Count;
        if (count < 3) return 0;

        int window = Math.Max(2, count / 10);

        if (isEnd)
        {
            int prevIndex = Math.Max(0, count - 1 - window);
            double dp = Math.Max(samples[^1].Progress - samples[prevIndex].Progress, 0.001);
            double drop = Math.Max(0, samples[prevIndex].Width - samples[^1].Width);
            return drop / dp;
        }

        int nextIndex = Math.Min(count - 1, window);
        double dpStart = Math.Max(samples[nextIndex].Progress - samples[0].Progress, 0.001);
        double dropStart = Math.Max(0, samples[0].Width - samples[nextIndex].Width);
        return dropStart / dpStart;
    }

    private void AddCapV13(StreamGeometryContext ctx, WpfPoint from, WpfPoint to, CapData cap)
    {
        double sharpThreshold = _baseSize * 0.2;
        double dropThreshold = 3.2;

        double normalizedDrop = cap.PressureDropRate / Math.Max(_baseSize, 0.001);
        bool useSharp = cap.Width < sharpThreshold && normalizedDrop > dropThreshold;

        if (useSharp)
        {
            ctx.LineTo(cap.TipPoint, true, true);
            ctx.LineTo(to, true, true);
            return;
        }

        AddRoundedCapArc(ctx, from, to, cap.TipPoint);
    }

    private static void AddRoundedCapArc(StreamGeometryContext ctx, WpfPoint from, WpfPoint to, WpfPoint tip)
    {
        double chord = (to - from).Length;
        if (chord < 0.1)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        var mid = new WpfPoint((from.X + to.X) * 0.5, (from.Y + to.Y) * 0.5);
        var chordVec = to - from;
        var normal = new Vector(-chordVec.Y, chordVec.X);
        if (normal.LengthSquared < 0.0001)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        normal.Normalize();
        var tipVec = tip - mid;
        double h = Math.Abs(Vector.Multiply(tipVec, normal));
        h = Math.Max(h, chord * 0.15);

        double radius = (h / 2.0) + (chord * chord / (8.0 * h));
        radius = Math.Max(radius, chord * 0.5);
        radius = Math.Min(radius, chord * 3.0);

        double side = Vector.Multiply(tipVec, normal);
        var sweep = side >= 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

        ctx.ArcTo(to, new WpfSize(radius, radius), 0, false, sweep, true, true);
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

    private void AddCornerReinforcement(List<WpfPoint> edge, WpfPoint center, Vector normalPrev, Vector normalNext, double width)
    {
        var bisector = normalPrev + normalNext;
        if (bisector.LengthSquared < 0.0001)
        {
            bisector = normalPrev;
        }

        if (bisector.LengthSquared < 0.0001)
        {
            return;
        }

        bisector.Normalize();

        double baseOffset = _baseSize * 0.1;
        double minOffset = _baseSize * 0.05;
        double maxOffset = _baseSize * 0.35;
        double offset = Math.Clamp(baseOffset, minOffset, maxOffset);
        offset = Math.Min(offset, width * 0.45);

        if (offset < 0.1) return;

        var point = center + bisector * offset;
        if (edge.Count == 0 || (edge.Last() - point).Length > 0.1)
        {
            edge.Add(point);
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

    private static double FractalNoise(double phase, double frequency)
    {
        double n1 = ValueNoise(phase * frequency);
        double n2 = ValueNoise((phase * frequency * 2.07) + 13.7);
        double n3 = ValueNoise((phase * frequency * 4.11) + 37.9);
        return (n1 * 0.6) + (n2 * 0.3) + (n3 * 0.1);
    }

    private static double ValueNoise(double x)
    {
        int x0 = (int)Math.Floor(x);
        int x1 = x0 + 1;
        double t = x - x0;
        double v0 = HashToUnit(x0);
        double v1 = HashToUnit(x1);
        t = t * t * (3 - 2 * t);
        return Lerp(v0, v1, t) * 2.0 - 1.0;
    }

    private static double HashToUnit(int x)
    {
        int n = (x << 13) ^ x;
        int nn = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
        return nn / 2147483648.0;
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
