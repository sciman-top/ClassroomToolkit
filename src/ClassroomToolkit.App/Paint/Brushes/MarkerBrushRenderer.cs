using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public sealed class MarkerBrushConfig
{
    public double SpeedWidthFactor { get; set; }
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

    public static MarkerBrushConfig Smooth => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.006,
        MinWidthFactor = 0.99,
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
        RibbonCapSegments = 20
    };

    public static MarkerBrushConfig Balanced => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.07,
        MinWidthFactor = 0.86,
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
        RibbonCapSegments = 10
    };

    public static MarkerBrushConfig Sharp => new MarkerBrushConfig
    {
        SpeedWidthFactor = 0.14,
        MinWidthFactor = 0.72,
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
        RibbonCapSegments = 6
    };
}

public enum MarkerRenderMode
{
    SegmentUnion = 0,
    Ribbon
}

public class MarkerBrushRenderer : IBrushRenderer
{
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
    private long _lastTimestamp;
    private double _smoothedWidth;
    private WpfPoint _smoothedPos;
    private double _velocityPeak;
    private readonly MarkerRenderMode _renderMode;

    public bool IsActive => _isActive;
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
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        _smoothedWidth = _baseSize;
        _smoothedPos = point;
        _velocityPeak = 0.001;
        _points.Add(new MarkerPoint(point, _smoothedWidth));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - _config.PositionSmoothing) + point.X * _config.PositionSmoothing,
            _smoothedPos.Y * (1 - _config.PositionSmoothing) + point.Y * _config.PositionSmoothing
        );

        var lastPt = _points[_points.Count - 1].Position;
        var dist = (_smoothedPos - lastPt).Length;
        if (dist < _config.MinMoveDistance) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;
        _velocityPeak = Math.Max(_velocityPeak * _config.VelocityDecay, velocity);
        double normSpeed = Math.Clamp(velocity / (_velocityPeak + 0.0001), 0, 1);

        double targetWidth = _baseSize * (1.0 - (_config.SpeedWidthFactor * normSpeed));
        double minWidth = _baseSize * _config.MinWidthFactor;
        targetWidth = Math.Max(targetWidth, minWidth);

        _smoothedWidth = Lerp(_smoothedWidth, targetWidth, _config.WidthSmoothing);
        _points.Add(new MarkerPoint(_smoothedPos, _smoothedWidth));
        _lastTimestamp = now;
    }

    public void OnUp(WpfPoint point)
    {
        if (!_isActive) return;
        var last = _points[_points.Count - 1].Position;
        if ((point - last).Length > 0.1)
        {
            _points.Add(new MarkerPoint(point, _smoothedWidth));
        }
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        var geometry = BuildStrokeGeometry();
        if (geometry == null)
        {
            return;
        }
        var brush = new SolidColorBrush(GetMarkerColor());
        brush.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    public Geometry? GetLastStrokeGeometry()
    {
        return BuildStrokeGeometry();
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

    private Geometry? BuildStrokeGeometry()
    {
        return _renderMode == MarkerRenderMode.Ribbon
            ? BuildRibbonGeometry()
            : BuildSegmentGeometry();
    }

    private Geometry? BuildSegmentGeometry()
    {
        if (_points.Count == 0)
        {
            return null;
        }
        if (_points.Count == 1)
        {
            var single = _points[0];
            var circle = new EllipseGeometry(single.Position, single.Width * 0.5, single.Width * 0.5);
            if (circle.CanFreeze) circle.Freeze();
            return circle;
        }

        var samples = BuildSmoothSamples();
        if (samples.Count < 2)
        {
            var point = _points[0];
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
        var samples = BuildSmoothSamples();
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
        SmoothSamplePositions(samples);

        var left = new List<WpfPoint>(samples.Count);
        var right = new List<WpfPoint>(samples.Count);
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

    private List<MarkerPoint> BuildSmoothSamples()
    {
        var samples = new List<MarkerPoint>();
        if (_points.Count == 0)
        {
            return samples;
        }
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

    private static void SmoothSamplePositions(List<MarkerPoint> samples)
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
            positions[i] = new WpfPoint(
                (prev.X + (curr.X * 2.0) + next.X) * 0.25,
                (prev.Y + (curr.Y * 2.0) + next.Y) * 0.25);
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
