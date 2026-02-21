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

public partial class VariableWidthBrushRenderer : IBrushRenderer
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
    private WpfPoint _lastRawPos;
    private long _lastRawTimestamp;
    private bool _hasRawPoint;

    // v10: 用于速度归一化的范围跟踪
    private double _minVelocity = double.MaxValue;
    private double _maxVelocity = double.MinValue;
    private readonly Random _random = new Random();

    // v11: 顿笔墨水累积状态
    private double _accumulatedWidth;
    private double _lastInkFlow = 1.0;
    private Vector _lastStrokeDirection = new Vector(1, 0);
    private bool _cacheDirty = true;
    private List<RibbonGeometry>? _cachedRibbons;
    private Geometry? _cachedCoreGeometry;
    private List<InkBloomGeometry>? _cachedBlooms;

    private readonly BrushPhysicsConfig _config;

    public bool IsActive => _isActive;
    public double LastInkFlow => _lastInkFlow;
    public Vector LastStrokeDirection => _lastStrokeDirection;

    public VariableWidthBrushRenderer()
        : this(BrushPhysicsConfig.DefaultSmooth)
    {
    }

    public VariableWidthBrushRenderer(BrushPhysicsConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

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
        _lastRawTimestamp = _lastTimestamp;
        _lastRawPos = point;
        _hasRawPoint = true;
        _lastInkFlow = 1.0;
        _lastStrokeDirection = new Vector(1, 0);
        MarkGeometryDirty();

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

        var rawNow = Stopwatch.GetTimestamp();
        double rawDtMs = (rawNow - _lastRawTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (rawDtMs < 1) rawDtMs = 1;
        double rawSpeed = 0;
        if (_hasRawPoint)
        {
            rawSpeed = (point - _lastRawPos).Length / rawDtMs;
        }
        _lastRawPos = point;
        _lastRawTimestamp = rawNow;
        _hasRawPoint = true;

        // Position smoothing (adaptive EMA)
        double speedAlpha = Math.Clamp(rawSpeed / Math.Max(_config.PositionSmoothingSpeedReference, 0.001), 0, 1);
        double posAlpha = Lerp(_config.PositionSmoothingMinAlpha, _config.PositionSmoothingMaxAlpha, speedAlpha);
        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - posAlpha) + point.X * posAlpha,
            _smoothedPos.Y * (1 - posAlpha) + point.Y * posAlpha
        );

        var lastPt = _points.Last();
        var dist = (_smoothedPos - lastPt.Position).Length;

        // 去噪：忽略过小的移动（阈值随当前宽度调整）
        double minDistFactor = 0.15;
        if (_config.EnableAdaptiveSampling)
        {
            double speedNorm = Math.Clamp(rawSpeed / Math.Max(_config.AdaptiveSamplingSpeedReference, 0.001), 0, 1);
            minDistFactor = Lerp(_config.AdaptiveSamplingMinFactor, _config.AdaptiveSamplingMaxFactor, speedNorm);
        }
        double minDist = Math.Clamp(_smoothedWidth * minDistFactor, 0.4, 2.6);
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

        UpdateStrokeDirection(lastPt.Position, _smoothedPos);
        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth, smoothVelocity, 0, 0, _accumulatedWidth));
        UpdateInkFlow();
        MarkGeometryDirty();
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
        UpdateStrokeDirection(last.Position, endPos);

        var minWidth = ClampWidth(_baseSize * _config.TaperMinWidthFactor);
        _points.Add(new StrokePoint(endPos, minWidth, 0, 0, 1));
        if (_config.EnableRdpSimplify)
        {
            double epsilon = Math.Max(_baseSize * _config.RdpEpsilonFactor, _config.RdpMinEpsilon);
            SimplifyPointsRdp(epsilon);
        }
        UpdateInkFlow();
        MarkGeometryDirty();
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
        _hasRawPoint = false;
        MarkGeometryDirty();
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var core = GetLastCoreGeometry();
        if (core == null)
        {
            return;
        }
        var brush = new SolidColorBrush(_color);
        brush.Freeze();
        dc.DrawGeometry(brush, null, core);
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

    private int ResolveRibbonCount()
    {
        if (!_config.EnableMultiRibbon) return 1;
        return Math.Clamp(_config.MultiRibbonCount, 1, 7);
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

    private void UpdateStrokeDirection(WpfPoint from, WpfPoint to)
    {
        var dir = to - from;
        if (dir.LengthSquared < 0.0001)
        {
            return;
        }
        dir.Normalize();
        _lastStrokeDirection = dir;
    }

    private Vector ResolvePointDirection(int index)
    {
        if (_points.Count == 0)
        {
            return new Vector(1, 0);
        }

        var current = _points[index].Position;
        var prev = index > 0 ? _points[index - 1].Position : current;
        var next = index < _points.Count - 1 ? _points[index + 1].Position : current;
        var dir = next - prev;

        if (dir.LengthSquared < 0.0001)
        {
            dir = _lastStrokeDirection;
        }

        if (dir.LengthSquared < 0.0001)
        {
            return new Vector(1, 0);
        }

        dir.Normalize();
        return dir;
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
        double velocityScale = Math.Max(_config.VelocityThreshold, 0.001);
        var t = Math.Min(clampedVelocity / velocityScale, 1.0);
        var factor = 1.0 - (t * t);
        factor = Lerp(1.0, factor, _config.VelocityWidthFactor);
        factor = Math.Clamp(factor, 0.0, 1.0);

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
}
