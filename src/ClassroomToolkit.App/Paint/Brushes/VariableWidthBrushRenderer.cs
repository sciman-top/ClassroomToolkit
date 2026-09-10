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

internal partial class VariableWidthBrushRenderer : IBrushRenderer
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
    private readonly SlidingAverageWindow _velocityAverage = new();
    private readonly SlidingAverageWindow _widthAverage = new();
    private readonly SlidingAverageWindow _pressureAverage = new();
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
    private double _lastPressure = 0.5;
    private bool _hasPressureSample;

    // v10: 用于速度归一化的范围跟踪
    private double _minVelocity = double.MaxValue;
    private double _maxVelocity = double.MinValue;

    // v11: 顿笔墨水累积状态
    private double _accumulatedWidth;
    private double _lastInkFlow = 1.0;
    private Vector _lastStrokeDirection = new Vector(1, 0);
    private double _releaseSpeedNorm;
    private StrokeWetnessSummary _lastStrokeWetness = new(0.62, 0.62, 0.62);
    private bool _cacheDirty = true;
    private List<RibbonGeometry>? _cachedRibbons;
    private Geometry? _cachedCoreGeometry;
    private Geometry? _cachedPreviewGeometry;
    private Geometry? _previewBaseGeometry;
    private int _previewBasePointCount;
    // 尾部起点长度随基座刷新一次性缓存；总长在追加点上增量维护，结构性变更后置无效。
    private double _previewTailStartGlobalLength;
    private double _previewBaseGlobalTotalLength;
    private int _previewGeometryVersion = -1;
    private WpfPoint _previewCachedRawPosition;
    private bool _previewCachedRawPositionValid;
    private double _previewPolylineTotalLength;
    private bool _previewPolylineLengthValid;
    private readonly List<StrokePoint> _previewSliceBuffer = new();
    private int _geometryVersion;
    private int _lastResampledPointCount;
    private double _lastEffectiveTaperBaseDip;

    private readonly BrushPhysicsConfig _config;
    private readonly BrushMoveTelemetry _moveTelemetry = new BrushMoveTelemetry();
    private SolidColorBrush? _cachedRenderBrush;
    private int _cachedRenderColorKey = int.MinValue;

    public bool IsActive => _isActive;
    public int GeometryVersion => _geometryVersion;
    public double LastInkFlow => _lastInkFlow;
    public Vector LastStrokeDirection => _lastStrokeDirection;
    public int LastResampledPointCount => _lastResampledPointCount;
    public double LastEffectiveEndTaperLengthDip { get; private set; }

    /// <summary>单笔湿感摘要：起笔/收笔/最低含水量，供提交后 Ink mask 做分层纹理。</summary>
    internal readonly record struct StrokeWetnessSummary(double Start, double End, double Min);

    internal readonly record struct BrushPredictionState(
        double Width,
        double Pressure,
        double Wetness,
        double NibAngleRadians,
        double NibStrength,
        bool HasPressure);

    internal StrokeWetnessSummary LastStrokeWetnessSummary => _lastStrokeWetness;

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
        _lastPressure = 0.5;
        _cachedRenderBrush = null;
        _cachedRenderColorKey = int.MinValue;
    }

    public void OnDown(BrushInputSample input)
    {
        var point = input.Position;
        _points.Clear();
        _velocityAverage.Reset();
        _widthAverage.Reset();
        _pressureAverage.Reset();
        _isActive = true;
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _releaseSpeedNorm = 0;
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
        bool hasFinitePressure = input.HasPressure && double.IsFinite(input.Pressure);
        _hasPressureSample = hasFinitePressure;
        MarkGeometryDirty();
        _positionFilter.Reset();
        _pressureFilter.Reset();
        _strokeNoisePhase = 0;
        _inkWetness = Math.Clamp(_config.InitialInkWetness, 0.0, 1.0);
        _lastStrokeWetness = new StrokeWetnessSummary(_inkWetness, _inkWetness, _inkWetness);
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;
        _previewTailStartGlobalLength = 0.0;
        _previewBaseGlobalTotalLength = 0.0;
        _previewPolylineTotalLength = 0.0;
        _previewPolylineLengthValid = true;
        _previewSliceBuffer.Clear();

        _smoothedWidth = ClampWidth(_baseSize * 0.5);
        _lastPressure = hasFinitePressure
            ? Math.Clamp(input.Pressure, 0.0, 1.0)
            : 0.5;
        _smoothedPos = _positionFilter.Filter(point, 1.0 / 120.0);
        double nibAngle = ResolveEffectiveBrushAngle(input);
        double nibStrength = ResolveOrientationStrength(input);
        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth, 0, 0, 0, 0, _strokeNoisePhase, _inkWetness, nibAngle, nibStrength));
        TrackAppendedPointLength();
        if (hasFinitePressure)
        {
            _pressureAverage.Push(Math.Clamp(input.Pressure, 0, 1), _config.PressureSmoothWindow);
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

            bool hasFinitePressure = input.HasPressure && double.IsFinite(input.Pressure);
            if (hasFinitePressure)
            {
                _lastPressure = Math.Clamp(input.Pressure, 0.0, 1.0);
                _hasPressureSample = true;
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
            double followBoost = Math.Clamp((rawSpeed - 1.1) / 2.4, 0, 1);
            if (rawDtMs > 9.0)
            {
                followBoost = Math.Max(followBoost, Math.Clamp((rawDtMs - 9.0) / 24.0, 0, 1));
            }
            posAlpha = Lerp(posAlpha, 0.97, followBoost);
            var filteredPoint = _positionFilter.Filter(point, dtSeconds);
            _smoothedPos = new WpfPoint(
                _smoothedPos.X * (1 - posAlpha) + filteredPoint.X * posAlpha,
                _smoothedPos.Y * (1 - posAlpha) + filteredPoint.Y * posAlpha
            );

            var lastPt = _points[^1];
            var dist = (_smoothedPos - lastPt.Position).Length;

            // 去噪：忽略过小的移动（阈值随当前宽度调整）
            double minDistFactor = 0.15;
            if (_config.EnableAdaptiveSampling)
            {
                double speedNorm = Math.Clamp(rawSpeed / Math.Max(_config.AdaptiveSamplingSpeedReference, 0.001), 0, 1);
                minDistFactor = Lerp(_config.AdaptiveSamplingMinFactor, _config.AdaptiveSamplingMaxFactor, speedNorm);
                // Fast handwriting prefers continuity over aggressive thinning.
                minDistFactor *= Lerp(1.0, 0.62, speedNorm);
                if (rawDtMs > 9.0)
                {
                    minDistFactor *= Lerp(1.0, 0.82, Math.Clamp((rawDtMs - 9.0) / 22.0, 0, 1));
                }
            }
            double minDist = Math.Clamp(_smoothedWidth * minDistFactor, 0.4, 2.6);
            if (dist < minDist) return;

            // 数据验证：异常跳变保护（掉帧时允许更大的连续位移，避免断线）
            double maxPointJumpDistance = ResolveDynamicMaxPointJumpDistance(rawDtMs);
            if (dist > maxPointJumpDistance)
            {
                if (rawDtMs <= 6.0)
                {
                    return;
                }

                var jumpDirection = _smoothedPos - lastPt.Position;
                if (jumpDirection.LengthSquared < 0.0001)
                {
                    return;
                }

                jumpDirection.Normalize();
                _smoothedPos = lastPt.Position + (jumpDirection * maxPointJumpDistance);
                dist = maxPointJumpDistance;
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

            double smoothVelocity = _velocityAverage.Push(velocity, _config.VelocitySmoothWindow);
            // 收锋速度：仅统计被接受的采样点（去抖窗口之后），慢速微步会被距离过滤自然聚合。
            _releaseSpeedNorm = Lerp(
                _releaseSpeedNorm,
                Math.Clamp(smoothVelocity / Math.Max(_config.VelocityThreshold, 0.001), 0, 1),
                0.45);
            double resolvedPressure = hasFinitePressure
                ? Math.Clamp(input.Pressure, 0, 1)
                : (_hasPressureSample ? _lastPressure : 0.5);
            resolvedPressure = _pressureFilter.Filter(resolvedPressure, dtSeconds);
            double smoothedPressure = _pressureAverage.Push(
                resolvedPressure,
                _config.PressureSmoothWindow);
            double targetWidth = CalculateTargetWidth(smoothVelocity, _pointCount);

            // 倾斜→宽度基线（默认关闭）：笔杆越压平笔画越宽，模拟扁锋着纸面。
            if (_config.TiltWidthInfluence > 0.0
                && input.AltitudeRadians is double altitudeInput
                && double.IsFinite(altitudeInput))
            {
                double altitude = Math.Clamp(altitudeInput, 0.0, Math.PI * 0.5);
                double tiltFactor = 1.0 - (altitude / (Math.PI * 0.5));
                targetWidth = ClampWidth(targetWidth * (1.0 + (_config.TiltWidthInfluence * tiltFactor)));
            }

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
            _lastStrokeWetness = new StrokeWetnessSummary(
                _lastStrokeWetness.Start,
                _inkWetness,
                Math.Min(_lastStrokeWetness.Min, _inkWetness));
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
                double cornerGrowthCapMax = _baseSize * Math.Clamp(_config.CornerGrowthCapMaxFactor, 0.1, 1.0);
                double cornerGrowthCap = Lerp(cornerGrowthCapMax, _baseSize * 0.24, turnSharpness);
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

            targetWidth = _widthAverage.Push(effectiveWidth, _config.PressureSmoothWindow);
            targetWidth = ClampWidth(targetWidth);

            double lowPassSpeedNorm = Math.Clamp(
                smoothVelocity / Math.Max(_config.WidthLowPassSpeedReference, 0.001),
                0,
                1);
            double maxStepDelta = Lerp(_baseSize * 0.28, _baseSize * 0.78, lowPassSpeedNorm);
            maxStepDelta = Math.Clamp(maxStepDelta, 0.9, _baseSize * 1.1);
            double desiredDelta = targetWidth - _smoothedWidth;
            targetWidth = _smoothedWidth + Math.Clamp(desiredDelta, -maxStepDelta, maxStepDelta);

            double dynamicWidthAlpha = Lerp(_config.WidthLowPassMaxAlpha, _config.WidthLowPassMinAlpha, lowPassSpeedNorm);
            dynamicWidthAlpha = Math.Clamp(dynamicWidthAlpha, 0.45, 0.95);
            double widthAlpha = Math.Clamp((_config.WidthSmoothing * 0.35) + (dynamicWidthAlpha * 0.65), 0.45, 0.96);
            _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));
            // 压力只在这一处进入直接宽度曲线。速度、顿笔和低通先确定
            // 基线；有真压感时再一次性向压力曲线靠拢，避免同一信号在
            // 目标宽度、步长和末端修正中重复放大。湿度仍是独立的材料
            // 状态，其压力影响只用于计算含水量，不属于直接压力宽度通路。
            if (hasFinitePressure)
            {
                double pressurePrimaryBlend = Math.Clamp(_config.PressurePrimaryWidthBlend, 0.0, 1.0);
                double pressureTargetWidth;
                double pressureBlend;
                if (pressurePrimaryBlend > 0.0)
                {
                    pressureTargetWidth = CalculatePressureTargetWidth(smoothedPressure);
                    pressureBlend = pressurePrimaryBlend;
                }
                else
                {
                    // 保留旧预设的温和压力手感；它仍然是同一个最终阶段，
                    // 只是没有启用压力主曲线。
                    double centeredPressure = MapPressureSigned(Math.Clamp(smoothedPressure, 0, 1), 0.04, 1.12);
                    double pressureBoost = centeredPressure * _config.RealPressureWidthScale * 0.32;
                    pressureBoost = Math.Clamp(pressureBoost, -0.18, 0.24);
                    pressureTargetWidth = ClampWidth(_smoothedWidth * (1.0 + pressureBoost));
                    pressureBlend = Math.Clamp(_config.RealPressureWidthInfluence * 0.24, 0.04, 0.27);
                }

                _smoothedWidth = Lerp(_smoothedWidth, pressureTargetWidth, pressureBlend);
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
            TrackAppendedPointLength();
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
        else dir = _lastStrokeDirection;
        if (dir.LengthSquared < 0.0001)
        {
            dir = new Vector(1, 0);
        }
        else
        {
            dir.Normalize();
        }

        // 保留真实抬笔位置。笔锋的外延只由唯一的 cap builder 负责，
        // 避免 raw endpoint、采样 taper 和 cap 三处重复外延/收锋。
        var endPos = point;
        UpdateStrokeDirection(last.Position, endPos);
        _strokeNoisePhase += (endPos - last.Position).Length / Math.Max(_baseSize * 0.2, 0.2);

        double tailFactor = _config.EndTaperStyle == TaperCapStyle.Exposed
            ? Math.Max(0.05, _config.TaperMinWidthFactor * 0.18)
            : Math.Max(0.08, _config.TaperMinWidthFactor * 0.28);
        var minWidth = Math.Clamp(_baseSize * tailFactor, Math.Max(0.14, _baseSize * 0.015), _baseSize * _config.MaxStrokeWidthMultiplier);
        double nibAngle = input.HasAnyOrientation
            ? ResolveEffectiveBrushAngle(input)
            : last.NibAngleRadians;
        double nibStrength = input.HasAnyOrientation
            ? ResolveOrientationStrength(input)
            : last.NibStrength;
        _points.Add(new StrokePoint(
            endPos,
            minWidth,
            0,
            0,
            1,
            0,
            _strokeNoisePhase,
            _inkWetness,
            nibAngle,
            nibStrength));
        TrackAppendedPointLength();
        if (_config.EnableRdpSimplify)
        {
            double epsilon = Math.Max(_baseSize * _config.RdpEpsilonFactor, _config.RdpMinEpsilon);
            SimplifyPointsRdp(epsilon);
        }
        UpdateInkFlow();
        MarkGeometryDirty();
        WriteWidthProfileCsvIfEnabled();
        _isActive = false;
    }

    public void Reset()
    {
        _points.Clear();
        _velocityAverage.Reset();
        _widthAverage.Reset();
        _pressureAverage.Reset();
        _isActive = false;
        _pointCount = 0;
        _minVelocity = double.MaxValue;
        _maxVelocity = double.MinValue;
        _accumulatedWidth = 0; // v11: 重置累积宽度
        _releaseSpeedNorm = 0;
        _lastInkFlow = 1.0;
        _lastResampledPointCount = 0;
        _lastEffectiveTaperBaseDip = 0.0;
        LastEffectiveEndTaperLengthDip = 0.0;
        _hasRawPoint = false;
        _positionFilter.Reset();
        _pressureFilter.Reset();
        _strokeNoisePhase = 0;
        _inkWetness = Math.Clamp(_config.InitialInkWetness, 0.0, 1.0);
        _lastPressure = 0.5;
        _hasPressureSample = false;
        _lastStrokeWetness = new StrokeWetnessSummary(_inkWetness, _inkWetness, _inkWetness);
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;
        _previewTailStartGlobalLength = 0.0;
        _previewBaseGlobalTotalLength = 0.0;
        _previewPolylineTotalLength = 0.0;
        _previewPolylineLengthValid = true;
        _previewSliceBuffer.Clear();
        MarkGeometryDirty();
    }

    public void Render(DrawingContext dc)
    {
        ArgumentNullException.ThrowIfNull(dc);

        if (_points.Count < 2) return;

        var core = _isActive ? GetPreviewCoreGeometry() : GetLastCoreGeometry();
        if (core == null)
        {
            return;
        }
        var brush = GetCachedRenderBrush(_color);
        dc.DrawGeometry(brush, null, core);
    }

    private SolidColorBrush GetCachedRenderBrush(WpfColor color)
    {
        int key = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        if (_cachedRenderBrush != null && _cachedRenderColorKey == key)
        {
            return _cachedRenderBrush;
        }

        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        _cachedRenderBrush = brush;
        _cachedRenderColorKey = key;
        return brush;
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
        // InkFlow 只是纹理/干湿启发量：长笔画均匀抽样即可，避免每 move 全量 O(n) 扫描。
        int stride = (int)Math.Ceiling(_points.Count / 256.0);
        int sampledCount = 0;
        double speedSum = 0;
        double accumulationSum = 0;
        double wetnessSum = 0;

        for (int i = 0; i < _points.Count; i += stride)
        {
            var point = _points[i];
            speedSum += Math.Clamp(point.Speed / maxSpeed, 0, 1);
            accumulationSum += point.AccumulatedWidth;
            wetnessSum += point.Wetness;
            sampledCount++;
        }

        double avgSpeed = speedSum / sampledCount;
        double avgAccumulation = accumulationSum /
            Math.Max(1, sampledCount) /
            Math.Max(_baseSize, 0.001);
        double avgWetness = wetnessSum / Math.Max(1, sampledCount);
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
        double fallback = double.IsFinite(_config.BrushAngleDegrees)
            ? _config.BrushAngleDegrees * Math.PI / 180.0
            : -Math.PI * 0.25;
        if (!double.IsFinite(fallback))
        {
            fallback = -Math.PI * 0.25;
        }

        if (!_config.EnableOrientationAnisotropy)
        {
            return fallback;
        }

        double? orientationAngle = null;
        if (input.AzimuthRadians is double azimuth && double.IsFinite(azimuth))
        {
            double offset = double.IsFinite(_config.OrientationAngleOffsetDegrees)
                ? _config.OrientationAngleOffsetDegrees * Math.PI / 180.0
                : 0.0;
            double candidate = azimuth + offset;
            if (double.IsFinite(candidate))
            {
                orientationAngle = candidate;
            }
        }
        else if (input.HasTiltOrientation
            && double.IsFinite(input.TiltXRadians!.Value)
            && double.IsFinite(input.TiltYRadians!.Value))
        {
            double candidate = Math.Atan2(input.TiltYRadians.Value, input.TiltXRadians.Value);
            if (double.IsFinite(candidate))
            {
                orientationAngle = candidate;
            }
        }

        if (!orientationAngle.HasValue || !double.IsFinite(orientationAngle.Value))
        {
            return fallback;
        }

        double mix = Math.Clamp(_config.OrientationAnisotropyMix, 0, 1);
        return LerpAngle(fallback, NormalizeAngle(orientationAngle.Value), mix);
    }

    private double ResolveOrientationStrength(BrushInputSample input)
    {
        bool hasFiniteAzimuth = input.AzimuthRadians is double azimuth
            && double.IsFinite(azimuth);
        bool hasFiniteTilt = input.HasTiltOrientation
            && double.IsFinite(input.TiltXRadians!.Value)
            && double.IsFinite(input.TiltYRadians!.Value);
        if (!_config.EnableOrientationAnisotropy || (!hasFiniteAzimuth && !hasFiniteTilt))
        {
            return 1.0;
        }

        double minStrength = Math.Max(_config.OrientationStrengthMin, 0.05);
        double maxStrength = Math.Max(_config.OrientationStrengthMax, minStrength);
        if (!input.AltitudeRadians.HasValue || !double.IsFinite(input.AltitudeRadians.Value))
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
        if (!double.IsFinite(angle))
        {
            return 0.0;
        }

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

    /// <summary>
    /// 压感主宽度曲线：压力 [0,1] 经 gamma 映射到 [MinWidthFactor, MaxWidthFactor]×baseSize，
    /// 与速度主曲线共用同一个输出区间，保证混合后仍受 ClampWidth 约束。
    /// </summary>
    private double CalculatePressureTargetWidth(double pressure)
    {
        double clamped = Math.Clamp(pressure, 0.0, 1.0);
        double gamma = Math.Clamp(_config.WidthGamma, 0.55, 2.4);
        double curved = Math.Pow(clamped, 1.0 / gamma);
        double range = _config.MaxWidthFactor - _config.MinWidthFactor;
        double width = _baseSize * (_config.MinWidthFactor + (range * curved));
        return ClampWidth(width);
    }

    private double CalculateTargetWidth(double velocity, int pointIndex)
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
        return ClampWidth(width);
    }

    private double ClampWidth(double width)
    {
        double minWidth = _config.MinStrokeWidthPx;
        double maxWidth = _baseSize * _config.MaxStrokeWidthMultiplier;
        if (maxWidth < minWidth) maxWidth = minWidth;
        return Math.Clamp(width, minWidth, maxWidth);
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

    private double ResolveDynamicMaxPointJumpDistance(double rawDtMs)
    {
        double baseLimit = Math.Max(_config.MaxPointJumpDistance, _baseSize * 5.0);
        double safeDtMs = Math.Clamp(rawDtMs, 1.0, 80.0);
        double followSpeedDipPerMs = Math.Max(2.4, _baseSize * 0.24);
        double expandedByDt = safeDtMs * followSpeedDipPerMs;
        double hardCap = Math.Max(baseLimit * 2.4, _baseSize * 20.0);
        return Math.Clamp(Math.Max(baseLimit, expandedByDt), baseLimit, hardCap);
    }

    /// <summary>
    /// 原始点裁剪的滞后触发块：超过上限后不逐点全量重排（旧实现每 move O(n)），
    /// 而是再多积累 chunk 个点才裁一次、一次裁回上限，摊销后每次追加点 O(1)。
    /// </summary>
    internal const int RawPointTrimChunkMin = 64;

    internal static int ResolveRawPointTrimChunk(int maxRawPoints)
    {
        return Math.Max(RawPointTrimChunkMin, Math.Max(1, maxRawPoints / 8));
    }

    internal static int ResolveRawPointTrimTriggerCount(int maxRawPoints)
    {
        return Math.Max(256, maxRawPoints) + ResolveRawPointTrimChunk(maxRawPoints);
    }

    private void TrimRawPointsIfNeeded()
    {
        int maxRawPoints = Math.Max(256, _config.MaxRawPointCount);
        if (_points.Count <= ResolveRawPointTrimTriggerCount(maxRawPoints))
        {
            return;
        }

        int keepTail = Math.Max(64, (int)Math.Round(maxRawPoints * 0.62));
        int tailStart = Math.Max(1, _points.Count - keepTail);
        int headTarget = Math.Max(2, maxRawPoints - keepTail);
        var headCandidates = new List<(int Index, double Importance)>(Math.Max(0, tailStart - 2));
        for (int i = 1; i < tailStart - 1; i++)
        {
            headCandidates.Add((i, ResolveDecimationImportance(i)));
        }

        var selectedHead = new SortedSet<int> { 0, Math.Max(0, tailStart - 1) };
        int protectedBudget = Math.Max(0, headTarget - selectedHead.Count);
        foreach (var candidate in headCandidates
            .OrderByDescending(item => item.Importance)
            .ThenBy(item => item.Index)
            .Take(protectedBudget))
        {
            selectedHead.Add(candidate.Index);
        }

        // Fill the remaining head budget at even arc positions. Important corners,
        // width changes and speed changes are already selected above.
        int remaining = headTarget - selectedHead.Count;
        for (int slot = 1; slot <= remaining; slot++)
        {
            int index = (int)Math.Round(slot * (tailStart - 1) / (double)(remaining + 1));
            if ((uint)index < (uint)tailStart)
            {
                selectedHead.Add(index);
            }
        }

        var compacted = new List<StrokePoint>(maxRawPoints);
        foreach (int index in selectedHead)
        {
            compacted.Add(_points[index]);
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
        InvalidatePolylineLengthCache();
    }

    private double ResolveDecimationImportance(int index)
    {
        if (index <= 0 || index >= _points.Count - 1)
        {
            return double.MaxValue;
        }

        var previous = _points[index - 1];
        var current = _points[index];
        var next = _points[index + 1];
        var incoming = current.Position - previous.Position;
        var outgoing = next.Position - current.Position;
        double corner = ResolveCornerAngleDegrees(incoming, outgoing) / 180.0;
        double widthDelta = Math.Abs(current.Width - ((previous.Width + next.Width) * 0.5)) /
            Math.Max(_baseSize, 0.001);
        double speedDelta = Math.Abs(current.Speed - ((previous.Speed + next.Speed) * 0.5)) /
            Math.Max(_maxVelocity, 0.001);
        double accumulationDelta = Math.Abs(current.AccumulatedWidth -
            ((previous.AccumulatedWidth + next.AccumulatedWidth) * 0.5)) /
            Math.Max(_baseSize, 0.001);

        return (corner * 2.2) + (widthDelta * 1.4) + (speedDelta * 0.7) + (accumulationDelta * 0.8);
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

    private sealed class SlidingAverageWindow
    {
        private const int MaxCapacity = 64;
        private readonly double[] _values = new double[MaxCapacity];
        private double _sum;
        private int _start;
        private int _count;

        public void Reset()
        {
            _start = 0;
            _count = 0;
            _sum = 0;
        }

        public double Push(double value, int windowSize)
        {
            int limit = Math.Clamp(windowSize, 1, MaxCapacity);
            if (_count == MaxCapacity)
            {
                _sum -= _values[_start];
                _start = (_start + 1) % MaxCapacity;
                _count--;
            }

            int writeIndex = (_start + _count) % MaxCapacity;
            _values[writeIndex] = value;
            _count++;
            _sum += value;

            while (_count > limit)
            {
                _sum -= _values[_start];
                _start = (_start + 1) % MaxCapacity;
                _count--;
            }

            if (_count == 0)
            {
                return 0;
            }

            // 增量和的浮点漂移在（≤64 个同量级值）窗口内可忽略。
            return _sum / _count;
        }
    }
}
