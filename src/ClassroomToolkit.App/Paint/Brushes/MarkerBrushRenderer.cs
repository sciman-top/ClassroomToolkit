using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public sealed class MarkerBrushConfig
{
    public double SpeedWidthFactor { get; set; }
    public double PressureWidthFactor { get; set; }
    public double MinWidthFactor { get; set; }
    public double PositionSmoothing { get; set; }
    public double WidthSmoothing { get; set; }
    public double MinMoveDistance { get; set; }
    public double VelocityDecay { get; set; }
    public double SplineTargetSpacing { get; set; }
    public double MinSampleSpacing { get; set; }
    public double StartTaperFactor { get; set; }
    public double EndTaperFactor { get; set; }
    public double TaperLengthRatio { get; set; }
    public double MaxTaperLengthFactor { get; set; }
    public double RibbonEndCapScale { get; set; }
    public double RibbonJoinScale { get; set; }
    public double RibbonQuadOverlap { get; set; }
    public double RibbonMiterLimit { get; set; }
    public double RibbonNormalSmoothing { get; set; }
    public int RibbonCapSegments { get; set; }
    public double CurvatureSmoothingMin { get; set; }
    public double CurvatureSmoothingMax { get; set; }
    public double CurvatureReferenceDegrees { get; set; }

    public static MarkerBrushConfig Smooth => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.004,
        PressureWidthFactor = 0.065,
        MinWidthFactor = 0.992,
        PositionSmoothing = 0.56,
        WidthSmoothing = 0.16,
        MinMoveDistance = 0.34,
        VelocityDecay = 0.999,
        SplineTargetSpacing = 1.3,
        MinSampleSpacing = 0.09,
        StartTaperFactor = 0.98,
        EndTaperFactor = 0.94,
        TaperLengthRatio = 0.03,
        MaxTaperLengthFactor = 1.7,
        RibbonEndCapScale = 1.05,
        RibbonJoinScale = 1.02,
        RibbonQuadOverlap = 1.07,
        RibbonMiterLimit = 1.8,
        RibbonNormalSmoothing = 0.45,
        RibbonCapSegments = 20,
        CurvatureSmoothingMin = 0.14,
        CurvatureSmoothingMax = 0.46,
        CurvatureReferenceDegrees = 70.0
    };

    public static MarkerBrushConfig Balanced => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.038,
        PressureWidthFactor = 0.085,
        MinWidthFactor = 0.915,
        PositionSmoothing = 0.72,
        WidthSmoothing = 0.32,
        MinMoveDistance = 0.5,
        VelocityDecay = 0.988,
        SplineTargetSpacing = 2.2,
        MinSampleSpacing = 0.22,
        StartTaperFactor = 0.84,
        EndTaperFactor = 0.7,
        TaperLengthRatio = 0.09,
        MaxTaperLengthFactor = 2.7,
        RibbonEndCapScale = 0.9,
        RibbonJoinScale = 0.9,
        RibbonQuadOverlap = 1.01,
        RibbonMiterLimit = 2.8,
        RibbonNormalSmoothing = 0.78,
        RibbonCapSegments = 10,
        CurvatureSmoothingMin = 0.08,
        CurvatureSmoothingMax = 0.34,
        CurvatureReferenceDegrees = 62.0
    };

    public static MarkerBrushConfig Sharp => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.078,
        PressureWidthFactor = 0.13,
        MinWidthFactor = 0.84,
        PositionSmoothing = 0.92,
        WidthSmoothing = 0.6,
        MinMoveDistance = 0.8,
        VelocityDecay = 0.97,
        SplineTargetSpacing = 3.4,
        MinSampleSpacing = 0.48,
        StartTaperFactor = 0.6,
        EndTaperFactor = 0.42,
        TaperLengthRatio = 0.18,
        MaxTaperLengthFactor = 3.6,
        RibbonEndCapScale = 0.82,
        RibbonJoinScale = 0.82,
        RibbonQuadOverlap = 0.96,
        RibbonMiterLimit = 4.2,
        RibbonNormalSmoothing = 0.93,
        RibbonCapSegments = 6,
        CurvatureSmoothingMin = 0.04,
        CurvatureSmoothingMax = 0.22,
        CurvatureReferenceDegrees = 54.0
    };
}

public enum MarkerRenderMode
{
    SegmentUnion = 0,
    Ribbon
}

public class MarkerBrushRenderer : IBrushRenderer
{
    private const int PreviewTailPointWindow = 56;
    private const int PreviewBaseRefreshStride = 14;

    private readonly MarkerBrushConfig _config;
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
    private long _lastTimestampTicks;
    private double _smoothedWidth;
    private WpfPoint _smoothedPos;
    private double _velocityPeak;
    private readonly OneEuroPointFilter _positionFilter = new OneEuroPointFilter(1.2, 0.06, 1.0);
    private readonly OneEuroFilter _widthFilter = new OneEuroFilter(1.5, 0.03, 1.0);
    private readonly MarkerRenderMode _renderMode;
    private int _geometryVersion;
    private bool _cacheDirty = true;
    private Geometry? _cachedGeometry;
    private Geometry? _cachedPreviewGeometry;
    private int _previewGeometryVersion = -1;
    private Geometry? _previewBaseGeometry;
    private int _previewBasePointCount;
    private SolidColorBrush? _cachedRenderBrush;
    private int _cachedRenderColorKey = int.MinValue;
    private readonly List<WpfPoint> _ribbonLeftBuffer = new();
    private readonly List<WpfPoint> _ribbonRightBuffer = new();

    public bool IsActive => _isActive;
    public int GeometryVersion => _geometryVersion;
    public MarkerRenderMode RenderMode => _renderMode;

    public MarkerBrushRenderer()
        : this(MarkerRenderMode.SegmentUnion, MarkerBrushConfig.Smooth)
    {
    }

    public MarkerBrushRenderer(MarkerRenderMode renderMode)
        : this(renderMode, MarkerBrushConfig.Smooth)
    {
    }

    public MarkerBrushRenderer(MarkerRenderMode renderMode, MarkerBrushConfig config)
    {
        _renderMode = renderMode;
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = baseSize;
        _smoothedPos = new WpfPoint(0, 0);
        _velocityPeak = 0.001;
        _cachedRenderBrush = null;
        _cachedRenderColorKey = int.MinValue;
    }

    public void OnDown(BrushInputSample input)
    {
        var point = input.Position;
        _points.Clear();
        _isActive = true;
        _lastTimestampTicks = input.TimestampTicks > 0
            ? input.TimestampTicks
            : Stopwatch.GetTimestamp();
        _smoothedWidth = _baseSize;
        _smoothedPos = point;
        _velocityPeak = 0.001;
        _positionFilter.Reset();
        _widthFilter.Reset();
        _smoothedPos = _positionFilter.Filter(point, 1.0 / 120.0);
        _smoothedWidth = _widthFilter.Filter(_baseSize, 1.0 / 120.0);
        _points.Add(new MarkerPoint(point, _smoothedWidth));
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;
        MarkGeometryDirty();
    }

    public void OnMove(BrushInputSample input)
    {
        if (!_isActive) return;
        var point = input.Position;

        var now = input.TimestampTicks > 0
            ? input.TimestampTicks
            : Stopwatch.GetTimestamp();
        var dt = (now - _lastTimestampTicks) * 1000.0 / Stopwatch.Frequency;
        if (dt < 1) dt = 1;
        double dtSeconds = dt / 1000.0;

        _smoothedPos = _positionFilter.Filter(point, dtSeconds);
        var lastPt = _points[_points.Count - 1].Position;
        double rawDist = (point - lastPt).Length;
        double rawSpeed = rawDist / dt;
        double followBoost = Math.Clamp((rawSpeed - 1.0) / 2.2, 0, 1);
        if (dt > 9.0)
        {
            followBoost = Math.Max(followBoost, Math.Clamp((dt - 9.0) / 20.0, 0, 1));
        }
        if (followBoost > 0.001)
        {
            _smoothedPos = new WpfPoint(
                Lerp(_smoothedPos.X, point.X, followBoost * 0.58),
                Lerp(_smoothedPos.Y, point.Y, followBoost * 0.58));
        }

        var dist = (_smoothedPos - lastPt).Length;
        double minMoveDistance = Lerp(_config.MinMoveDistance, _config.MinMoveDistance * 0.45, followBoost);
        minMoveDistance = Math.Clamp(minMoveDistance, 0.18, _config.MinMoveDistance);
        if (dist < minMoveDistance) return;

        double velocity = dist / dt;
        _velocityPeak = Math.Max(_velocityPeak * _config.VelocityDecay, velocity);
        double normSpeed = Math.Clamp(velocity / (_velocityPeak + 0.0001), 0, 1);

        double targetWidth = _baseSize * (1.0 - (_config.SpeedWidthFactor * normSpeed));
        double minWidth = _baseSize * _config.MinWidthFactor;
        targetWidth = Math.Max(targetWidth, minWidth);
        if (input.HasPressure)
        {
            double centeredPressure = MapPressureCentered(Math.Clamp(input.Pressure, 0, 1));
            double pressureScale = 1.0 + (centeredPressure * _config.PressureWidthFactor);
            targetWidth *= Math.Clamp(pressureScale, 0.84, 1.2);
        }

        _smoothedWidth = Lerp(_smoothedWidth, targetWidth, _config.WidthSmoothing);
        _smoothedWidth = _widthFilter.Filter(_smoothedWidth, dtSeconds);
        _points.Add(new MarkerPoint(_smoothedPos, _smoothedWidth));
        _lastTimestampTicks = now;
        MarkGeometryDirty();
    }

    public void OnUp(BrushInputSample input)
    {
        if (!_isActive) return;
        var point = input.Position;
        var last = _points[_points.Count - 1].Position;
        if ((point - last).Length > 0.1)
        {
            _points.Add(new MarkerPoint(point, _smoothedWidth));
            MarkGeometryDirty();
        }
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        var geometry = _isActive
            ? GetPreviewGeometry()
            : GetLastStrokeGeometry();
        if (geometry == null)
        {
            return;
        }
        dc.DrawGeometry(GetCachedRenderBrush(), null, geometry);
    }

    public Geometry? GetLastStrokeGeometry()
    {
        if (!_cacheDirty)
        {
            return _cachedGeometry;
        }
        _cachedGeometry = BuildStrokeGeometryCore();
        _cacheDirty = false;
        return _cachedGeometry;
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
        _previewBaseGeometry = null;
        _previewBasePointCount = 0;
        MarkGeometryDirty();
    }

    private WpfColor GetMarkerColor()
    {
        byte alpha = Math.Min(_color.A, (byte)0xE6);
        return WpfColor.FromArgb(alpha, _color.R, _color.G, _color.B);
    }

    private void MarkGeometryDirty()
    {
        _cacheDirty = true;
        _cachedGeometry = null;
        _cachedPreviewGeometry = null;
        _previewGeometryVersion = -1;
        _geometryVersion++;
    }

    private Geometry? GetPreviewGeometry()
    {
        if (_cachedPreviewGeometry != null && _previewGeometryVersion == _geometryVersion)
        {
            return _cachedPreviewGeometry;
        }

        Geometry? preview;
        if (_points.Count <= PreviewTailPointWindow + 4)
        {
            _previewBaseGeometry = null;
            _previewBasePointCount = 0;
            preview = BuildSegmentGeometryFromPoints(_points);
        }
        else
        {
            int basePointCount = Math.Max(2, _points.Count - PreviewTailPointWindow);
            bool shouldRefreshBase = _previewBaseGeometry == null
                || _previewBasePointCount <= 0
                || basePointCount < _previewBasePointCount
                || (basePointCount - _previewBasePointCount) >= PreviewBaseRefreshStride;

            if (shouldRefreshBase)
            {
                _previewBasePointCount = basePointCount;
                _previewBaseGeometry = BuildSegmentGeometryForRange(0, _previewBasePointCount);
                if (_previewBaseGeometry?.CanFreeze == true)
                {
                    _previewBaseGeometry.Freeze();
                }
            }

            int tailStart = Math.Max(0, _previewBasePointCount - 3);
            var tailGeometry = BuildSegmentGeometryForRange(tailStart, _points.Count);
            if (_previewBaseGeometry != null && tailGeometry != null)
            {
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                group.Children.Add(_previewBaseGeometry);
                group.Children.Add(tailGeometry);
                preview = group;
            }
            else
            {
                preview = tailGeometry ?? _previewBaseGeometry;
            }
        }

        if (preview?.CanFreeze == true)
        {
            preview.Freeze();
        }
        _cachedPreviewGeometry = preview;
        _previewGeometryVersion = _geometryVersion;
        return _cachedPreviewGeometry;
    }

    private Geometry? BuildStrokeGeometryCore()
    {
        return _renderMode == MarkerRenderMode.Ribbon
            ? BuildRibbonGeometry()
            : BuildSegmentGeometry();
    }

    private Geometry? BuildSegmentGeometry()
    {
        return BuildSegmentGeometryFromPoints(_points);
    }

    private Geometry? BuildSegmentGeometryForRange(int startInclusive, int endExclusive)
    {
        int start = Math.Clamp(startInclusive, 0, _points.Count);
        int end = Math.Clamp(endExclusive, start, _points.Count);
        int count = end - start;
        if (count <= 0)
        {
            return null;
        }

        var source = _points.GetRange(start, count);
        return BuildSegmentGeometryFromPoints(source);
    }

    private Geometry? BuildSegmentGeometryFromPoints(IReadOnlyList<MarkerPoint> sourcePoints)
    {
        if (sourcePoints.Count == 0)
        {
            return null;
        }
        if (sourcePoints.Count == 1)
        {
            var single = sourcePoints[0];
            var circle = new EllipseGeometry(single.Position, single.Width * 0.5, single.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }

        var samples = BuildSmoothSamples(sourcePoints);
        if (samples.Count < 2)
        {
            var point = sourcePoints[0];
            var circle = new EllipseGeometry(point.Position, point.Width * 0.5, point.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int i = 0; i < samples.Count - 1; i++)
        {
            var p0 = samples[i];
            var p1 = samples[i + 1];
            if ((p1.Position - p0.Position).Length < 0.01)
            {
                continue;
            }
            double width = Math.Max(p0.Width, p1.Width);
            var pen = new WpfPen(System.Windows.Media.Brushes.Black, Math.Max(width, 0.1))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            var segment = new StreamGeometry();
            using (var ctx = segment.Open())
            {
                ctx.BeginFigure(p0.Position, isFilled: false, isClosed: false);
                ctx.LineTo(p1.Position, isStroked: true, isSmoothJoin: true);
            }
            var widened = segment.GetWidenedPathGeometry(pen);
            if (widened.CanFreeze) widened.Freeze();
            group.Children.Add(widened);
        }

        if (samples.Count == 0)
        {
            return null;
        }

        if (samples.Count == 1)
        {
            var single = samples[0];
            var circle = new EllipseGeometry(single.Position, single.Width * 0.5, single.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }

        if (group.Children.Count == 0)
        {
            var single = samples[0];
            var circle = new EllipseGeometry(single.Position, single.Width * 0.5, single.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }

        for (int i = 1; i < samples.Count - 1; i++)
        {
            double width = samples[i].Width;
            width = Math.Max(width, samples[i - 1].Width);
            width = Math.Max(width, samples[i + 1].Width);
            double radius = Math.Max(width * 0.5, 0.1);
            var join = new EllipseGeometry(samples[i].Position, radius, radius);
            if (join.CanFreeze) join.Freeze();
            group.Children.Add(join);
        }

        if (group.CanFreeze) group.Freeze();
        return group;
    }

    private Geometry? BuildRibbonGeometry()
    {
        return BuildRibbonGeometryFromPoints(_points);
    }

    private Geometry? BuildRibbonGeometryFromPoints(IReadOnlyList<MarkerPoint> sourcePoints)
    {
        var samples = BuildSmoothSamples(sourcePoints);
        if (samples.Count == 0)
        {
            return null;
        }
        if (samples.Count < 2)
        {
            var single = samples[0];
            var circle = new EllipseGeometry(single.Position, single.Width * 0.5, single.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }
        SmoothSamplePositionsAdaptive(samples);

        _ribbonLeftBuffer.Clear();
        _ribbonRightBuffer.Clear();
        _ribbonLeftBuffer.EnsureCapacity(samples.Count);
        _ribbonRightBuffer.EnsureCapacity(samples.Count);
        var left = _ribbonLeftBuffer;
        var right = _ribbonRightBuffer;
        var lastNormal = new Vector(0, 0);

        for (int i = 0; i < samples.Count; i++)
        {
            var prev = i > 0 ? samples[i - 1].Position : samples[i].Position;
            var next = i < samples.Count - 1 ? samples[i + 1].Position : samples[i].Position;
            var tangent = next - prev;
            if (tangent.LengthSquared < 0.0001)
            {
                tangent = i > 0 ? samples[i].Position - samples[i - 1].Position : new Vector(1, 0);
            }
            if (tangent.LengthSquared < 0.0001)
            {
                tangent = new Vector(1, 0);
            }
            tangent.Normalize();

            var normal = new Vector(-tangent.Y, tangent.X);
            if (lastNormal.LengthSquared > 0.0001 && Vector.Multiply(lastNormal, normal) < 0)
            {
                normal = -normal;
            }
            if (lastNormal.LengthSquared > 0.0001)
            {
                normal = LerpVector(lastNormal, normal, _config.RibbonNormalSmoothing);
            }
            if (normal.LengthSquared < 0.0001)
            {
                normal = lastNormal.LengthSquared > 0.0001 ? lastNormal : new Vector(0, -1);
            }
            normal.Normalize();
            lastNormal = normal;

            double width = samples[i].Width;
            if (i == 0 || i == samples.Count - 1)
            {
                width *= _config.RibbonEndCapScale;
            }
            else
            {
                width = Math.Max(width, samples[i - 1].Width);
                width = Math.Max(width, samples[i + 1].Width);
                width *= _config.RibbonJoinScale;
            }

            double halfWidth = Math.Max(width * 0.5, 0.1) * _config.RibbonQuadOverlap;
            left.Add(samples[i].Position + (normal * halfWidth));
            right.Add(samples[i].Position - (normal * halfWidth));
        }

        ClampRibbonMiters(samples, left, right);

        bool clockwise = true;
        if (left.Count > 1)
        {
            clockwise = IsQuadClockwise(left[0], left[1], right[1], right[0]);
        }

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < samples.Count - 1; i++)
            {
                ctx.BeginFigure(left[i], isFilled: true, isClosed: true);
                ctx.LineTo(left[i + 1], isStroked: true, isSmoothJoin: true);
                ctx.LineTo(right[i + 1], isStroked: true, isSmoothJoin: true);
                ctx.LineTo(right[i], isStroked: true, isSmoothJoin: true);
            }

            for (int i = 0; i < samples.Count; i++)
            {
                double radius = (left[i] - right[i]).Length * 0.5;
                if (radius < 0.1)
                {
                    continue;
                }
                AppendCircleFigure(ctx, samples[i].Position, radius, clockwise);
            }
        }

        if (geometry.CanFreeze) geometry.Freeze();
        return geometry;
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + ((end - start) * t);
    }

    private static double MapPressureCentered(double pressure)
    {
        double centered = (pressure - 0.5) * 2.0;
        double abs = Math.Abs(centered);
        const double deadZone = 0.14;
        if (abs <= deadZone)
        {
            return 0.0;
        }

        double normalized = (abs - deadZone) / (1.0 - deadZone);
        double curved = Math.Pow(normalized, 1.28);
        return Math.Sign(centered) * Math.Clamp(curved, 0.0, 1.0);
    }

    private List<MarkerPoint> BuildSmoothSamples(IReadOnlyList<MarkerPoint> sourcePoints)
    {
        var samples = new List<MarkerPoint>();
        if (sourcePoints.Count == 0)
        {
            return samples;
        }
        if (sourcePoints.Count == 1)
        {
            samples.Add(sourcePoints[0]);
            return samples;
        }

        for (int i = 0; i < sourcePoints.Count - 1; i++)
        {
            var p0 = sourcePoints[Math.Max(i - 1, 0)];
            var p1 = sourcePoints[i];
            var p2 = sourcePoints[i + 1];
            var p3 = sourcePoints[Math.Min(i + 2, sourcePoints.Count - 1)];

            var segment = p2.Position - p1.Position;
            double length = segment.Length;
            int steps = Math.Max(1, (int)Math.Ceiling(length / _config.SplineTargetSpacing));
            int startStep = i == 0 ? 0 : 1;
            for (int s = startStep; s <= steps; s++)
            {
                double t = steps == 0 ? 0 : s / (double)steps;
                var pos = CatmullRomCentripetal(p0.Position, p1.Position, p2.Position, p3.Position, t);
                double width = Lerp(p1.Width, p2.Width, t);
                if (samples.Count > 0 && (pos - samples[^1].Position).Length < _config.MinSampleSpacing)
                {
                    continue;
                }
                samples.Add(new MarkerPoint(pos, width));
            }
        }

        SmoothSampleWidths(samples);
        ApplyTaper(samples);
        return samples;
    }

    private SolidColorBrush GetCachedRenderBrush()
    {
        var color = GetMarkerColor();
        int key = color.A << 24 | color.R << 16 | color.G << 8 | color.B;
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

    private static void SmoothSampleWidths(List<MarkerPoint> samples)
    {
        if (samples.Count < 3)
        {
            return;
        }
        var widths = new double[samples.Count];
        widths[0] = samples[0].Width;
        widths[^1] = samples[^1].Width;
        for (int i = 1; i < samples.Count - 1; i++)
        {
            widths[i] = (samples[i - 1].Width * 0.25) + (samples[i].Width * 0.5) + (samples[i + 1].Width * 0.25);
        }
        for (int i = 0; i < samples.Count; i++)
        {
            samples[i] = new MarkerPoint(samples[i].Position, widths[i]);
        }
    }

    private void SmoothSamplePositionsAdaptive(List<MarkerPoint> samples)
    {
        if (samples.Count < 3)
        {
            return;
        }
        var positions = new WpfPoint[samples.Count];
        positions[0] = samples[0].Position;
        positions[^1] = samples[^1].Position;
        for (int i = 1; i < samples.Count - 1; i++)
        {
            var prev = samples[i - 1].Position;
            var curr = samples[i].Position;
            var next = samples[i + 1].Position;
            var a = curr - prev;
            var b = next - curr;
            double angle = 0;
            if (a.LengthSquared > 0.0001 && b.LengthSquared > 0.0001)
            {
                angle = Math.Abs(Vector.AngleBetween(a, b));
            }
            double reference = Math.Clamp(_config.CurvatureReferenceDegrees, 24.0, 175.0);
            double cornerFactor = Math.Clamp(angle / reference, 0, 1);
            double smoothing = Lerp(_config.CurvatureSmoothingMax, _config.CurvatureSmoothingMin, cornerFactor);
            smoothing = Math.Clamp(smoothing, 0.02, 0.75);
            double inv = 1.0 - smoothing;
            double avgX = (prev.X + next.X) * 0.5;
            double avgY = (prev.Y + next.Y) * 0.5;
            positions[i] = new WpfPoint(
                (curr.X * inv) + (avgX * smoothing),
                (curr.Y * inv) + (avgY * smoothing));
        }
        for (int i = 0; i < samples.Count; i++)
        {
            samples[i] = new MarkerPoint(positions[i], samples[i].Width);
        }
    }

    private void ApplyTaper(List<MarkerPoint> samples)
    {
        if (samples.Count < 2)
        {
            return;
        }
        double total = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            total += (samples[i].Position - samples[i - 1].Position).Length;
        }
        if (total <= 0.01)
        {
            return;
        }
        double maxTaper = _baseSize * _config.MaxTaperLengthFactor;
        double taperLength = Math.Min(total * _config.TaperLengthRatio, maxTaper);
        if (taperLength <= 0.1)
        {
            return;
        }

        double accumulated = 0;
        var distances = new double[samples.Count];
        distances[0] = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            accumulated += (samples[i].Position - samples[i - 1].Position).Length;
            distances[i] = accumulated;
        }

        for (int i = 0; i < samples.Count; i++)
        {
            double startDist = distances[i];
            double endDist = total - distances[i];
            double width = samples[i].Width;

            if (startDist < taperLength)
            {
                double t = Smoothstep(startDist / taperLength);
                width *= Lerp(_config.StartTaperFactor, 1.0, t);
            }
            if (endDist < taperLength)
            {
                double t = Smoothstep(endDist / taperLength);
                width *= Lerp(_config.EndTaperFactor, 1.0, t);
            }
            samples[i] = new MarkerPoint(samples[i].Position, width);
        }
    }

    private static WpfPoint CatmullRomCentripetal(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3, double t)
    {
        const double alpha = 0.5;
        double t0 = 0;
        double t1 = t0 + Math.Pow((p1 - p0).Length, alpha);
        double t2 = t1 + Math.Pow((p2 - p1).Length, alpha);
        double t3 = t2 + Math.Pow((p3 - p2).Length, alpha);

        if (t1 - t0 < 0.0001) t1 = t0 + 0.0001;
        if (t2 - t1 < 0.0001) t2 = t1 + 0.0001;
        if (t3 - t2 < 0.0001) t3 = t2 + 0.0001;

        double tt = t1 + (t2 - t1) * t;

        var a1 = LerpPoint(p0, p1, (tt - t0) / (t1 - t0));
        var a2 = LerpPoint(p1, p2, (tt - t1) / (t2 - t1));
        var a3 = LerpPoint(p2, p3, (tt - t2) / (t3 - t2));

        var b1 = LerpPoint(a1, a2, (tt - t0) / (t2 - t0));
        var b2 = LerpPoint(a2, a3, (tt - t1) / (t3 - t1));

        return LerpPoint(b1, b2, (tt - t1) / (t2 - t1));
    }

    private static WpfPoint LerpPoint(WpfPoint start, WpfPoint end, double t)
    {
        return new WpfPoint(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }

    private static Vector LerpVector(Vector start, Vector end, double t)
    {
        return new Vector(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }

    private void AppendCircleFigure(StreamGeometryContext ctx, WpfPoint center, double radius, bool clockwise)
    {
        int segments = Math.Max(6, _config.RibbonCapSegments * 2);
        double step = (Math.PI * 2.0 / segments) * (clockwise ? 1 : -1);

        var start = new WpfPoint(center.X + radius, center.Y);
        ctx.BeginFigure(start, isFilled: true, isClosed: true);
        for (int i = 1; i <= segments; i++)
        {
            double angle = step * i;
            var p = new WpfPoint(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));
            ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
        }
    }

    private static bool IsQuadClockwise(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3)
    {
        double area = 0;
        area += (p0.X * p1.Y) - (p1.X * p0.Y);
        area += (p1.X * p2.Y) - (p2.X * p1.Y);
        area += (p2.X * p3.Y) - (p3.X * p2.Y);
        area += (p3.X * p0.Y) - (p0.X * p3.Y);
        return area > 0;
    }

    private void ClampRibbonMiters(List<MarkerPoint> samples, List<WpfPoint> left, List<WpfPoint> right)
    {
        if (samples.Count < 3)
        {
            return;
        }

        for (int i = 1; i < samples.Count - 1; i++)
        {
            var pPrev = samples[i - 1].Position;
            var pCurr = samples[i].Position;
            var pNext = samples[i + 1].Position;

            var tPrev = pCurr - pPrev;
            var tNext = pNext - pCurr;
            if (tPrev.LengthSquared < 0.0001 || tNext.LengthSquared < 0.0001)
            {
                continue;
            }

            tPrev.Normalize();
            tNext.Normalize();
            var avg = tPrev + tNext;
            if (avg.LengthSquared < 0.0001)
            {
                continue;
            }
            avg.Normalize();

            double width = samples[i].Width;
            width = Math.Max(width, samples[i - 1].Width);
            width = Math.Max(width, samples[i + 1].Width);
            width *= _config.RibbonJoinScale;

            double halfWidth = Math.Max(width * 0.5, 0.1);
            double angle = Math.Acos(Math.Clamp(Vector.Multiply(-tPrev, tNext), -1.0, 1.0));
            double sinHalf = Math.Sin(angle * 0.5);
            if (sinHalf < 0.001)
            {
                continue;
            }
            double miterLength = halfWidth / sinHalf;
            double maxMiter = halfWidth * _config.RibbonMiterLimit;
            if (miterLength <= maxMiter)
            {
                continue;
            }

            double scale = maxMiter / miterLength;
            var normal = new Vector(-avg.Y, avg.X);
            if (normal.LengthSquared < 0.0001)
            {
                continue;
            }
            normal.Normalize();
            left[i] = pCurr + (normal * (halfWidth * scale));
            right[i] = pCurr - (normal * (halfWidth * scale));
        }
    }

    private static double Smoothstep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }
}
