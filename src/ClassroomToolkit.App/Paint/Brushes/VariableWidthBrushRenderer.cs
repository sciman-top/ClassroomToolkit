using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

internal readonly record struct BrushMoveTelemetrySnapshot(
    string PresetName,
    string ModeTag,
    double DtAvgMs,
    double DtP95Ms,
    double DtMaxMs,
    double AllocAvgBytes,
    double AllocP95Bytes,
    long AllocMaxBytes,
    double RawAvgPoints,
    double RawP95Points,
    int RawMaxPoints,
    double ResampledAvgPoints,
    double ResampledP95Points,
    int ResampledMaxPoints,
    double EffectiveTaperBaseAvgDip,
    double EffectiveTaperBaseP95Dip,
    double EffectiveTaperBaseMinDip,
    double EffectiveTaperBaseMaxDip);

public partial class VariableWidthBrushRenderer : IBrushRenderer
{
    private const double DirectionNoiseAmplitude = 0.009;
    private const double DirectionNoiseFrequency = 0.4;

    private const double CornerAngleThreshold = 90.0;
    private const double CornerMinAngle = 5.0;
    private const int CornerArcSegments = 6;
    private static readonly bool BrushMoveTelemetryFlag = ResolveTelemetryFlagFromEnvironment();

    // v10/v11: 扩展点结构以存储速度、进度和累积宽度信息
    private struct StrokePoint
    {
        public WpfPoint Position;
        public double Width;
        public double Speed;            // 原始速度（px/ms）
        public double NormalizedSpeed;  // [0, 1] 归一化速度
        public double Progress;         // [0, 1] 沿笔画进度
        public double AccumulatedWidth; // v11: 墨水累积宽度（顿笔效果）
        public double NoisePhase;       // 连续纹理相位（降低纹理闪烁）
        public double Wetness;          // 含水量（简化物理模型）
        public double NibAngleRadians;  // 笔锋朝向（弧度）
        public double NibStrength;      // 笔锋强度（0~2）

        public StrokePoint(
            WpfPoint pos,
            double width,
            double speed = 0,
            double normalizedSpeed = 0,
            double progress = 0,
            double accumulated = 0,
            double noisePhase = 0,
            double wetness = 0.6,
            double nibAngleRadians = -Math.PI * 0.25,
            double nibStrength = 1.0)
        {
            Position = pos;
            Width = width;
            Speed = speed;
            NormalizedSpeed = normalizedSpeed;
            Progress = progress;
            AccumulatedWidth = accumulated;
            NoisePhase = noisePhase;
            Wetness = wetness;
            NibAngleRadians = nibAngleRadians;
            NibStrength = nibStrength;
        }
    }

    private readonly List<StrokePoint> _points = new();
    private readonly Queue<double> _velocityBuffer = new();
    private readonly Queue<double> _widthBuffer = new();
    private readonly Queue<double> _pressureBuffer = new();
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
    private readonly OneEuroPointFilter _positionFilter = new OneEuroPointFilter(1.1, 0.08, 1.0);
    private readonly OneEuroFilter _pressureFilter = new OneEuroFilter(1.5, 0.02, 1.0);
    private double _strokeNoisePhase;
    private double _inkWetness;

    // v10: 用于速度归一化的范围跟踪
    private double _minVelocity = double.MaxValue;
    private double _maxVelocity = double.MinValue;

    // v11: 顿笔墨水累积状态
    private double _accumulatedWidth;
    private double _lastInkFlow = 1.0;
    private Vector _lastStrokeDirection = new Vector(1, 0);
    private bool _cacheDirty = true;
    private List<RibbonGeometry>? _cachedRibbons;
    private Geometry? _cachedCoreGeometry;
    private Geometry? _cachedPreviewGeometry;
    private List<InkBloomGeometry>? _cachedBlooms;
    private Geometry? _previewBaseGeometry;
    private int _previewBasePointCount;
    private int _geometryVersion;
    private int _lastResampledPointCount;
    private double _lastEffectiveTaperBaseDip;

    private readonly BrushPhysicsConfig _config;
    private readonly BrushMoveTelemetry _moveTelemetry = new BrushMoveTelemetry();

    public bool IsActive => _isActive;
    public int GeometryVersion => _geometryVersion;
    public double LastInkFlow => _lastInkFlow;
    public Vector LastStrokeDirection => _lastStrokeDirection;
    public int LastResampledPointCount => _lastResampledPointCount;

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

    public void OnDown(BrushInputSample input)
    {
        var point = input.Position;
        _points.Clear();
        _velocityBuffer.Clear();
        _widthBuffer.Clear();
        _pressureBuffer.Clear();
        _isActive = true;
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _noiseSeed = ResolveDeterministicNoiseSeed(point);
        _lastResampledPointCount = 0;
        _lastEffectiveTaperBaseDip = Math.Max(0.0, _config.TaperLengthPx);
        _lastTimestamp = input.TimestampTicks > 0
            ? input.TimestampTicks
            : Stopwatch.GetTimestamp();
        _lastRawTimestamp = _lastTimestamp;
        _lastRawPos = point;
        _hasRawPoint = true;
        _lastInkFlow = 1.0;
        _lastStrokeDirection = new Vector(1, 0);
        MarkGeometryDirty();
        _positionFilter.Reset();
        _pressureFilter.Reset();
        _strokeNoisePhase = 0;
        _inkWetness = Math.Clamp(_config.InitialInkWetness, 0.0, 1.0);
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;

        _smoothedWidth = ClampWidth(_baseSize * 0.5);
        _smoothedPos = _positionFilter.Filter(point, 1.0 / 120.0);
        double nibAngle = _config.BrushAngleDegrees * Math.PI / 180.0;
        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth, 0, 0, 0, 0, _strokeNoisePhase, _inkWetness, nibAngle, 1.0));
        if (input.HasPressure)
        {
            _pressureBuffer.Enqueue(Math.Clamp(input.Pressure, 0, 1));
        }
    }

    public void OnMove(BrushInputSample input)
    {
        bool telemetryEnabled = IsMoveTelemetryEnabled();
        long telemetryStartTicks = 0;
        long telemetryStartAllocBytes = 0;
        if (telemetryEnabled)
        {
            telemetryStartTicks = Stopwatch.GetTimestamp();
            telemetryStartAllocBytes = GC.GetAllocatedBytesForCurrentThread();
        }

        try
        {
            if (!_isActive) return;
            var point = input.Position;

        // 数据验证：检查 NaN/Infinity
            if (double.IsNaN(point.X) || double.IsNaN(point.Y) ||
                double.IsInfinity(point.X) || double.IsInfinity(point.Y))
            {
                return;
            }

        var rawNow = input.TimestampTicks > 0
            ? input.TimestampTicks
            : Stopwatch.GetTimestamp();
        double rawDtMs = (rawNow - _lastRawTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (rawDtMs < 1) rawDtMs = 1;
        double dtSeconds = rawDtMs / 1000.0;
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
        var filteredPoint = _positionFilter.Filter(point, dtSeconds);
        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - posAlpha) + filteredPoint.X * posAlpha,
            _smoothedPos.Y * (1 - posAlpha) + filteredPoint.Y * posAlpha
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

        var now = input.TimestampTicks > 0
            ? input.TimestampTicks
            : Stopwatch.GetTimestamp();
        double dtMs = (now - _lastTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (dtMs < 1) dtMs = 1;

        double velocity = dist / dtMs;

        // v10: 跟踪速度范围用于归一化
        _minVelocity = Math.Min(_minVelocity, velocity);
        _maxVelocity = Math.Max(_maxVelocity, velocity);

        double smoothVelocity = PushAndAverage(_velocityBuffer, _config.VelocitySmoothWindow, velocity);
        double resolvedPressure = input.HasPressure ? Math.Clamp(input.Pressure, 0, 1) : 0.5;
        resolvedPressure = _pressureFilter.Filter(resolvedPressure, dtSeconds);
        double smoothedPressure = PushAndAverage(
            _pressureBuffer,
            _config.PressureSmoothWindow,
            resolvedPressure);
        double targetWidth = CalculateTargetWidth(smoothVelocity, _pointCount, smoothedPressure, input.HasPressure);

        double brushAngle = ResolveEffectiveBrushAngle(input);
        double orientationStrength = ResolveOrientationStrength(input);

        // 轻微各向异性：模拟毛笔扁平笔锋（宽度层）
        if (dist > 0.001)
        {
            var dir = _smoothedPos - lastPt.Position;
            if (dir.LengthSquared > 0.0001)
            {
                double angle = Math.Atan2(dir.Y, dir.X);
                double anisotropy = _config.AnisotropyStrength * orientationStrength;
                double angleFactor = 1.0 - (anisotropy * Math.Cos(2 * (angle - brushAngle)));
                angleFactor = Math.Clamp(angleFactor, 0.9, 1.1);
                targetWidth = ClampWidth(targetWidth * angleFactor);
            }
        }

        // v11: 顿笔逻辑 - 低速时累积墨水扩散
        double speedFloor = Math.Max(_config.SpeedFloorPxPerMs, _config.MinVelocityClamp);
        double normalizedSpeed = Math.Clamp((smoothVelocity - speedFloor) /
                                            Math.Max(0.001, _config.VelocityThreshold - speedFloor), 0, 1);
        UpdateWetness(smoothedPressure, normalizedSpeed, dtSeconds);
        double absorption = Math.Clamp(_config.PaperAbsorption, 0.0, 1.0);
        bool inStartSuppressionWindow = _pointCount < Math.Max(0, _config.StartBurstSuppressPoints);
        double turnAttenuation = 1.0;
        double turnSharpness = 0.0;
        if (dist > 0.001)
        {
            var moveDir = _smoothedPos - lastPt.Position;
            if (moveDir.LengthSquared > 0.0001)
            {
                moveDir.Normalize();
                var lastDir = _lastStrokeDirection;
                if (lastDir.LengthSquared > 0.0001)
                {
                    lastDir.Normalize();
                    double turnAngle = Math.Abs(Vector.AngleBetween(lastDir, moveDir));
                    double turnNorm = Math.Clamp(turnAngle / 120.0, 0.0, 1.0);
                    turnSharpness = turnNorm;
                    // Reduce accumulation at sharper turns to avoid local blobs.
                    turnAttenuation = Lerp(1.0, 0.8, turnNorm);
                }
            }
        }
        double overlapAttenuation = ResolveOverlapAttenuation(_smoothedPos, targetWidth);

        if (normalizedSpeed < _config.DunBiSpeedThreshold)
        {
            // 低速时累积宽度（墨水扩散）
            double wetnessFactor = 0.65 + (_inkWetness * 0.7);
            double absorptionFactor = 1.0 - (absorption * 0.35);
            double accumulationRate = (_config.DunBiSpreadRate * wetnessFactor * absorptionFactor * turnAttenuation * overlapAttenuation) / Math.Max(velocity, Math.Max(_config.SpeedFloorPxPerMs, 0.08));
            double deltaTime = dtMs / 1000.0; // 转换为秒
            _accumulatedWidth += accumulationRate * deltaTime;

            // 限制最大累积
            double maxAccumulation = targetWidth * (_config.DunBiMaxAccumulation - 1.0);
            maxAccumulation *= turnAttenuation * overlapAttenuation;
            if (inStartSuppressionWindow)
            {
                double startCap = Math.Clamp(_config.StartBurstAccumulationCap, 0.0, 1.0);
                maxAccumulation *= startCap;
            }
            _accumulatedWidth = Math.Min(_accumulatedWidth, maxAccumulation);
        }
        else
        {
            // 高速时衰减累积
            _accumulatedWidth *= (1.0 - (_config.DunBiDecayRate * (1.0 + absorption * 0.4)));
        }

        // 应用累积宽度
        double effectiveWidth = targetWidth + (_accumulatedWidth * (0.6 + (_inkWetness * 0.8)));
        if (inStartSuppressionWindow && normalizedSpeed < 0.3)
        {
            double startWidthCap = ClampWidth(_baseSize * Math.Max(0.6, _config.StartBurstMaxWidthFactor));
            effectiveWidth = Math.Min(effectiveWidth, startWidthCap);
        }
        if (turnSharpness > 0.2)
        {
            double cornerGrowthCap = Lerp(_baseSize * 0.48, _baseSize * 0.24, turnSharpness);
            effectiveWidth = Math.Min(effectiveWidth, _smoothedWidth + cornerGrowthCap);
        }
        if (overlapAttenuation < 0.92)
        {
            double overlapStrength = Math.Clamp((0.92 - overlapAttenuation) / 0.24, 0.0, 1.0);
            double overlapGrowthCap = Lerp(_baseSize * 0.24, _baseSize * 0.12, overlapStrength);
            effectiveWidth = Math.Min(effectiveWidth, targetWidth + overlapGrowthCap);
        }
        if (normalizedSpeed < 0.22)
        {
            double lowSpeedMax = ClampWidth(_baseSize * Math.Max(1.0, _config.LowSpeedWidthMaxFactor));
            effectiveWidth = Math.Min(effectiveWidth, lowSpeedMax);
        }

        targetWidth = PushAndAverage(_widthBuffer, _config.PressureSmoothWindow, effectiveWidth);
        targetWidth = ClampWidth(targetWidth);

        double lowPassSpeedNorm = Math.Clamp(
            smoothVelocity / Math.Max(_config.WidthLowPassSpeedReference, 0.001),
            0,
            1);
        double maxStepDelta = Lerp(_baseSize * 0.28, _baseSize * 0.78, lowPassSpeedNorm);
        if (input.HasPressure)
        {
            double pressureDelta = Math.Abs((Math.Clamp(smoothedPressure, 0, 1) - 0.5) * 2.0);
            maxStepDelta *= 1.0 + (pressureDelta * 0.45);
        }
        maxStepDelta = Math.Clamp(maxStepDelta, 0.9, _baseSize * 1.1);
        double desiredDelta = targetWidth - _smoothedWidth;
        targetWidth = _smoothedWidth + Math.Clamp(desiredDelta, -maxStepDelta, maxStepDelta);

        double dynamicWidthAlpha = Lerp(_config.WidthLowPassMaxAlpha, _config.WidthLowPassMinAlpha, lowPassSpeedNorm);
        dynamicWidthAlpha = Math.Clamp(dynamicWidthAlpha, 0.45, 0.95);
        double widthAlpha = Math.Clamp((_config.WidthSmoothing * 0.35) + (dynamicWidthAlpha * 0.65), 0.45, 0.96);
        _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));
        if (input.HasPressure)
        {
            double centeredPressure = MapPressureSigned(Math.Clamp(smoothedPressure, 0, 1), 0.04, 1.12);
            double pressureBoost = centeredPressure * _config.RealPressureWidthScale * 0.32;
            pressureBoost = Math.Clamp(pressureBoost, -0.18, 0.24);
            double pressureAdjustedWidth = ClampWidth(_smoothedWidth * (1.0 + pressureBoost));
            double pressureBlend = Math.Clamp(_config.RealPressureWidthInfluence * 0.24, 0.04, 0.27);
            _smoothedWidth = Lerp(_smoothedWidth, pressureAdjustedWidth, pressureBlend);
        }
        _smoothedWidth = ClampWidth(_smoothedWidth);

        // 数据验证：检查宽度有效性
            if (double.IsNaN(_smoothedWidth) || double.IsInfinity(_smoothedWidth) || _smoothedWidth <= 0)
            {
                return;
            }

            _strokeNoisePhase += dist / Math.Max(_baseSize * 0.18, 0.2);
            UpdateStrokeDirection(lastPt.Position, _smoothedPos);
            _points.Add(new StrokePoint(
                _smoothedPos,
                _smoothedWidth,
                smoothVelocity,
                0,
                0,
                _accumulatedWidth,
                _strokeNoisePhase,
                _inkWetness,
                brushAngle,
                orientationStrength));
            TrimRawPointsIfNeeded();
            UpdateInkFlow();
            MarkGeometryDirty();
            _lastTimestamp = now;
            _pointCount++;
        }
        finally
        {
            if (telemetryEnabled)
            {
                long allocDelta = GC.GetAllocatedBytesForCurrentThread() - telemetryStartAllocBytes;
                double dtMs = (Stopwatch.GetTimestamp() - telemetryStartTicks) * 1000.0 / Stopwatch.Frequency;
                _moveTelemetry.Record(
                    dtMs,
                    Math.Max(0, allocDelta),
                    _points.Count,
                    _lastResampledPointCount,
                    _lastEffectiveTaperBaseDip,
                    _config.PresetName,
                    _config.RenderModeTag);
            }
        }
    }

    public void OnUp(BrushInputSample input)
    {
        if (!_isActive) return;
        var point = input.Position;

        var last = _points.Last();
        var dir = point - last.Position;
        if (dir.Length > 0.1) dir.Normalize();
        else dir = new Vector(1, 0);

        var extension = _baseSize * 0.2;
        var endPos = point + dir * extension;
        UpdateStrokeDirection(last.Position, endPos);
        _strokeNoisePhase += (endPos - last.Position).Length / Math.Max(_baseSize * 0.2, 0.2);

        double tailFactor = _config.EndTaperStyle == TaperCapStyle.Exposed
            ? Math.Max(0.05, _config.TaperMinWidthFactor * 0.18)
            : Math.Max(0.08, _config.TaperMinWidthFactor * 0.28);
        var minWidth = Math.Clamp(_baseSize * tailFactor, Math.Max(0.14, _baseSize * 0.015), _baseSize * _config.MaxStrokeWidthMultiplier);
        _points.Add(new StrokePoint(
            endPos,
            minWidth,
            0,
            0,
            1,
            0,
            _strokeNoisePhase,
            _inkWetness,
            last.NibAngleRadians,
            last.NibStrength));
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
        _pressureBuffer.Clear();
        _isActive = false;
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _lastInkFlow = 1.0;
        _lastResampledPointCount = 0;
        _lastEffectiveTaperBaseDip = 0.0;
        _hasRawPoint = false;
        _positionFilter.Reset();
        _pressureFilter.Reset();
        _strokeNoisePhase = 0;
        _inkWetness = Math.Clamp(_config.InitialInkWetness, 0.0, 1.0);
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;
        MarkGeometryDirty();
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var core = _isActive ? GetPreviewCoreGeometry() : GetLastCoreGeometry();
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

    internal List<StrokePointData>? GetLastResampledStrokePointsForDiagnostics()
    {
        if (_points.Count < 2)
        {
            return null;
        }

        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2)
        {
            return null;
        }

        var result = new List<StrokePointData>(samples.Count);
        foreach (var sample in samples)
        {
            result.Add(new StrokePointData(sample.Position, sample.Width));
        }
        return result;
    }

    internal bool TryGetMoveTelemetrySnapshotForDiagnostics(out BrushMoveTelemetrySnapshot snapshot)
    {
        return _moveTelemetry.TryGetLastSnapshot(out snapshot);
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
        double wetnessSum = 0;

        foreach (var point in _points)
        {
            speedSum += Math.Clamp(point.Speed / maxSpeed, 0, 1);
            accumulationSum += point.AccumulatedWidth;
            wetnessSum += point.Wetness;
        }

        double avgSpeed = speedSum / _points.Count;
        double avgAccumulation = accumulationSum / Math.Max(_baseSize, 0.001);
        double avgWetness = wetnessSum / Math.Max(1, _points.Count);
        double flow = 1.0 - avgSpeed;
        flow += Math.Clamp(avgAccumulation * 0.35, 0, 0.4);
        flow += Math.Clamp((avgWetness - 0.5) * 0.22, -0.12, 0.16);
        _lastInkFlow = Math.Clamp(flow, 0.25, 1.0);
    }

    private void UpdateWetness(double pressure, double normalizedSpeed, double dtSeconds)
    {
        double response = Math.Clamp(_config.WetnessResponse, 0.02, 0.9);
        double evaporation = Math.Clamp(_config.WetnessEvaporationPerSecond, 0.0, 2.0);
        double pressureFactor = Math.Clamp(_config.WetnessPressureInfluence, 0.0, 1.0);
        double slowBoost = Math.Clamp(_config.WetnessSlowSpeedBoost, 0.0, 1.0);

        double target = 0.35 + (pressure * pressureFactor) + ((1.0 - normalizedSpeed) * slowBoost);
        target = Math.Clamp(target, 0.15, 1.0);

        _inkWetness = (_inkWetness * (1.0 - response)) + (target * response);
        _inkWetness *= (1.0 - (evaporation * Math.Clamp(dtSeconds, 0.0, 0.2)));
        _inkWetness = Math.Clamp(_inkWetness, 0.08, 1.0);
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

    private double ResolveEffectiveBrushAngle(BrushInputSample input)
    {
        double fallback = _config.BrushAngleDegrees * Math.PI / 180.0;
        if (!_config.EnableOrientationAnisotropy)
        {
            return fallback;
        }

        double? orientationAngle = null;
        if (input.AzimuthRadians.HasValue)
        {
            orientationAngle = input.AzimuthRadians.Value + (_config.OrientationAngleOffsetDegrees * Math.PI / 180.0);
        }
        else if (input.HasTiltOrientation)
        {
            orientationAngle = Math.Atan2(input.TiltYRadians!.Value, input.TiltXRadians!.Value);
        }

        if (!orientationAngle.HasValue)
        {
            return fallback;
        }

        double mix = Math.Clamp(_config.OrientationAnisotropyMix, 0, 1);
        return LerpAngle(fallback, NormalizeAngle(orientationAngle.Value), mix);
    }

    private double ResolveOrientationStrength(BrushInputSample input)
    {
        if (!_config.EnableOrientationAnisotropy || !input.HasAnyOrientation)
        {
            return 1.0;
        }

        double minStrength = Math.Max(_config.OrientationStrengthMin, 0.05);
        double maxStrength = Math.Max(_config.OrientationStrengthMax, minStrength);
        if (!input.AltitudeRadians.HasValue)
        {
            return Lerp(minStrength, maxStrength, 0.4);
        }

        double altitude = Math.Clamp(input.AltitudeRadians.Value, 0.0, Math.PI * 0.5);
        double tiltFactor = 1.0 - (altitude / (Math.PI * 0.5));
        return Lerp(minStrength, maxStrength, tiltFactor);
    }

    private static double LerpAngle(double start, double end, double t)
    {
        double delta = NormalizeAngle(end - start);
        if (delta > Math.PI)
        {
            delta -= Math.PI * 2.0;
        }
        return NormalizeAngle(start + (delta * t));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle <= -Math.PI)
        {
            angle += Math.PI * 2.0;
        }
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2.0;
        }
        return angle;
    }

    private double CalculateTargetWidth(double velocity, int pointIndex, double pressure, bool hasPressure)
    {
        // 起笔阶段：逐渐增加速度影响
        double velocityInfluence = 1.0;
        if (pointIndex < _config.StartVelocityRampUpPoints)
        {
            velocityInfluence = (double)pointIndex / Math.Max(1, _config.StartVelocityRampUpPoints);
            velocityInfluence = velocityInfluence * velocityInfluence * (3.0 - (2.0 * velocityInfluence));
        }

        // 限制最小速度
        double speedFloor = Math.Max(_config.SpeedFloorPxPerMs, _config.MinVelocityClamp);
        double clampedVelocity = Math.Max(velocity, speedFloor);

        // v10: 使用更温和的曲线（二次方）
        double velocityScale = Math.Max(_config.VelocityThreshold, 0.001);
        var t = Math.Min(clampedVelocity / velocityScale, 1.0);
        var factor = 1.0 - (t * t);
        factor = Lerp(1.0, factor, _config.VelocityWidthFactor);
        factor = Math.Clamp(factor, 0.0, 1.0);

        // 起笔插值
        var baseFactor = 0.8;
        var finalFactor = baseFactor * (1.0 - velocityInfluence) + factor * velocityInfluence;
        double gamma = Math.Clamp(_config.WidthGamma, 0.55, 2.4);
        double gammaAdjustedFactor = Math.Pow(Math.Clamp(finalFactor, 0.0, 1.0), 1.0 / gamma);

        var range = _config.MaxWidthFactor - _config.MinWidthFactor;
        var width = _baseSize * (_config.MinWidthFactor + (range * gammaAdjustedFactor));
        if (hasPressure)
        {
            var centered = MapPressureSigned(Math.Clamp(pressure, 0, 1), 0.05, 1.16);
            var pressureScale = 1.0 + (centered * _config.RealPressureWidthScale);
            var pressureWidth = ClampWidth(width * Math.Clamp(pressureScale, 0.72, 1.58));
            width = Lerp(width, pressureWidth, Math.Clamp(_config.RealPressureWidthInfluence, 0, 1));
        }
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

    private static double MapPressureSigned(double pressure, double deadZone, double gamma)
    {
        double centered = (pressure - 0.5) * 2.0;
        double abs = Math.Abs(centered);
        if (abs <= deadZone)
        {
            return 0.0;
        }

        double normalized = (abs - deadZone) / (1.0 - deadZone);
        double curved = Math.Pow(Math.Clamp(normalized, 0.0, 1.0), gamma);
        return Math.Sign(centered) * curved;
    }

    private double ResolveOverlapAttenuation(WpfPoint currentPosition, double targetWidth)
    {
        if (_points.Count < 12)
        {
            return 1.0;
        }

        double searchRadius = Math.Clamp(targetWidth * 0.72, _baseSize * 0.42, _baseSize * 1.25);
        double searchRadiusSq = searchRadius * searchRadius;
        int hits = 0;
        int scanned = 0;

        // Skip near neighbors to avoid attenuating normal continuous segments.
        for (int i = _points.Count - 8; i >= 0 && scanned < 56; i -= 2)
        {
            scanned++;
            var p = _points[i].Position;
            double dx = currentPosition.X - p.X;
            double dy = currentPosition.Y - p.Y;
            double distSq = (dx * dx) + (dy * dy);
            if (distSq <= searchRadiusSq)
            {
                hits++;
            }
        }

        if (hits <= 0)
        {
            return 1.0;
        }

        // Stronger attenuation when revisiting the same area repeatedly.
        double overlapNorm = Math.Clamp(hits / 3.0, 0.0, 1.0);
        return Lerp(1.0, 0.66, overlapNorm);
    }

    private void TrimRawPointsIfNeeded()
    {
        int maxRawPoints = Math.Max(256, _config.MaxRawPointCount);
        if (_points.Count <= maxRawPoints)
        {
            return;
        }

        int keepTail = Math.Max(64, (int)Math.Round(maxRawPoints * 0.62));
        int tailStart = Math.Max(1, _points.Count - keepTail);
        var compacted = new List<StrokePoint>(maxRawPoints);
        compacted.Add(_points[0]);

        for (int i = 1; i < tailStart; i += 2)
        {
            compacted.Add(_points[i]);
        }

        for (int i = tailStart; i < _points.Count; i++)
        {
            compacted.Add(_points[i]);
        }

        while (compacted.Count > maxRawPoints && compacted.Count > 2)
        {
            compacted.RemoveAt(1);
        }

        _points.Clear();
        _points.AddRange(compacted);
    }

    private double ResolveDeterministicNoiseSeed(WpfPoint startPoint)
    {
        uint hash = 2166136261u;
        hash = Fnv1a(hash, Quantize(startPoint.X, 1000.0));
        hash = Fnv1a(hash, Quantize(startPoint.Y, 1000.0));
        hash = Fnv1a(hash, Quantize(_baseSize, 1000.0));
        hash = Fnv1a(hash, _color.A << 24 | _color.R << 16 | _color.G << 8 | _color.B);
        hash = Fnv1a(hash, Quantize(_config.BrushAngleDegrees, 100.0));
        hash = Fnv1a(hash, Quantize(_config.ArcLengthResampleStepPx, 1000.0));
        int seed = unchecked((int)hash);
        if (seed == 0)
        {
            seed = 17;
        }

        return (Math.Abs(seed) % 100000) / 97.0;
    }

    private static uint Fnv1a(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }

        return hash;
    }

    private static int Quantize(double value, double scale)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Round(value * scale);
    }

    private bool IsMoveTelemetryEnabled()
    {
#if !DEBUG
        return false;
#else
        return _config.EnableDebugMoveTelemetry || BrushMoveTelemetryFlag;
#endif
    }

    private static bool ResolveTelemetryFlagFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("CTOOLKIT_BRUSH_TELEMETRY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BrushMoveTelemetry
    {
        private const int Capacity = 256;
        private readonly double[] _dtMs = new double[Capacity];
        private readonly long[] _allocBytes = new long[Capacity];
        private readonly int[] _rawPoints = new int[Capacity];
        private readonly int[] _resampledPoints = new int[Capacity];
        private readonly double[] _effectiveTaperBaseDip = new double[Capacity];
        private readonly double[] _scratchDtMs = new double[Capacity];
        private readonly long[] _scratchAllocBytes = new long[Capacity];
        private readonly int[] _scratchRawPoints = new int[Capacity];
        private readonly int[] _scratchResampledPoints = new int[Capacity];
        private readonly double[] _scratchEffectiveTaperBaseDip = new double[Capacity];
        private int _count;
        private int _index;
        private long _sequence;
        private BrushMoveTelemetrySnapshot _lastSnapshot;
        private bool _hasSnapshot;

        public void Record(
            double dtMs,
            long allocBytes,
            int rawPoints,
            int resampledPoints,
            double effectiveTaperBaseDip,
            string presetName,
            string modeTag)
        {
            _dtMs[_index] = Math.Max(0.0, dtMs);
            _allocBytes[_index] = Math.Max(0, allocBytes);
            _rawPoints[_index] = Math.Max(0, rawPoints);
            _resampledPoints[_index] = Math.Max(0, resampledPoints);
            _effectiveTaperBaseDip[_index] = Math.Max(0.0, effectiveTaperBaseDip);
            _index = (_index + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _sequence++;

            if (_count < 16 || (_sequence % 64) != 0)
            {
                return;
            }

            EmitSnapshot(presetName, modeTag);
        }

        private void EmitSnapshot(string presetName, string modeTag)
        {
            int count = _count;
            if (count <= 0)
            {
                return;
            }

            double dtSum = 0.0;
            double dtMax = 0.0;
            long allocSum = 0;
            long allocMax = 0;
            long rawSum = 0;
            int rawMax = 0;
            long resampledSum = 0;
            int resampledMax = 0;
            double effectiveBaseSum = 0.0;
            double effectiveBaseMin = double.MaxValue;
            double effectiveBaseMax = 0.0;

            for (int i = 0; i < count; i++)
            {
                double dt = _dtMs[i];
                long alloc = _allocBytes[i];
                int raw = _rawPoints[i];
                int resampled = _resampledPoints[i];
                double effectiveBase = _effectiveTaperBaseDip[i];

                dtSum += dt;
                if (dt > dtMax)
                {
                    dtMax = dt;
                }

                allocSum += alloc;
                if (alloc > allocMax)
                {
                    allocMax = alloc;
                }

                rawSum += raw;
                if (raw > rawMax)
                {
                    rawMax = raw;
                }

                resampledSum += resampled;
                if (resampled > resampledMax)
                {
                    resampledMax = resampled;
                }
                effectiveBaseSum += effectiveBase;
                if (effectiveBase < effectiveBaseMin)
                {
                    effectiveBaseMin = effectiveBase;
                }
                if (effectiveBase > effectiveBaseMax)
                {
                    effectiveBaseMax = effectiveBase;
                }

                _scratchDtMs[i] = dt;
                _scratchAllocBytes[i] = alloc;
                _scratchRawPoints[i] = raw;
                _scratchResampledPoints[i] = resampled;
                _scratchEffectiveTaperBaseDip[i] = effectiveBase;
            }

            double dtAvg = dtSum / count;
            double allocAvg = (double)allocSum / count;
            double rawAvg = (double)rawSum / count;
            double resampledAvg = (double)resampledSum / count;
            double effectiveBaseAvg = effectiveBaseSum / count;
            if (effectiveBaseMin == double.MaxValue)
            {
                effectiveBaseMin = 0.0;
            }

            double dtP95 = PercentileInPlace(_scratchDtMs, count, 0.95);
            double allocP95 = PercentileInPlace(_scratchAllocBytes, count, 0.95);
            double rawP95 = PercentileInPlace(_scratchRawPoints, count, 0.95);
            double resampledP95 = PercentileInPlace(_scratchResampledPoints, count, 0.95);
            double effectiveBaseP95 = PercentileInPlace(_scratchEffectiveTaperBaseDip, count, 0.95);

            _lastSnapshot = new BrushMoveTelemetrySnapshot(
                presetName,
                modeTag,
                dtAvg,
                dtP95,
                dtMax,
                allocAvg,
                allocP95,
                allocMax,
                rawAvg,
                rawP95,
                rawMax,
                resampledAvg,
                resampledP95,
                resampledMax,
                effectiveBaseAvg,
                effectiveBaseP95,
                effectiveBaseMin,
                effectiveBaseMax);
            _hasSnapshot = true;

            Debug.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[BrushMoveTelemetry] preset={presetName} mode={modeTag} " +
                    $"dt_ms(avg/p95/max)={dtAvg:F3}/{dtP95:F3}/{dtMax:F3} " +
                    $"alloc_bytes(avg/p95/max)={allocAvg:F0}/{allocP95:F0}/{allocMax} " +
                    $"points_raw(avg/p95/max)={rawAvg:F1}/{rawP95:F1}/{rawMax} " +
                    $"points_resampled(avg/p95/max)={resampledAvg:F1}/{resampledP95:F1}/{resampledMax} " +
                    $"taper_base_dip(avg/p95/min/max)={effectiveBaseAvg:F2}/{effectiveBaseP95:F2}/{effectiveBaseMin:F2}/{effectiveBaseMax:F2}"));
        }

        public bool TryGetLastSnapshot(out BrushMoveTelemetrySnapshot snapshot)
        {
            snapshot = _lastSnapshot;
            return _hasSnapshot;
        }

        private static double PercentileInPlace(double[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }

        private static double PercentileInPlace(long[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }

        private static double PercentileInPlace(int[] values, int count, double q)
        {
            if (count <= 0)
            {
                return 0.0;
            }

            Array.Sort(values, 0, count);
            int idx = Math.Clamp((int)Math.Ceiling((count - 1) * q), 0, count - 1);
            return values[idx];
        }
    }
}
