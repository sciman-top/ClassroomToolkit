using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint.Brushes;

public class MarkerBrushRenderer : IBrushRenderer
{
    private const double SpeedWidthFactor = 0.1;
    private const double MinWidthFactor = 0.85;
    private const double PositionSmoothing = 0.75;
    private const double WidthSmoothing = 0.4;
    private const double MinMoveDistance = 0.5;
    private const double VelocityDecay = 0.985;
    private const double SplineTargetSpacing = 1.8;
    private const double MinSampleSpacing = 0.15;
    private const double StartTaperFactor = 0.8;
    private const double EndTaperFactor = 0.75;
    private const double TaperLengthRatio = 0.1;
    private const double MaxTaperLengthFactor = 2.8;
    private const bool DebugDrawCenters = false;
    private static readonly WpfPen DebugCenterPen = CreateFrozenPen(Colors.LimeGreen, 1);

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

    public bool IsActive => _isActive;

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
            _smoothedPos.X * (1 - PositionSmoothing) + point.X * PositionSmoothing,
            _smoothedPos.Y * (1 - PositionSmoothing) + point.Y * PositionSmoothing
        );

        var lastPt = _points[_points.Count - 1].Position;
        var dist = (_smoothedPos - lastPt).Length;
        if (dist < MinMoveDistance) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;
        _velocityPeak = Math.Max(_velocityPeak * VelocityDecay, velocity);
        double normSpeed = Math.Clamp(velocity / (_velocityPeak + 0.0001), 0, 1);

        double targetWidth = _baseSize * (1.0 - (SpeedWidthFactor * normSpeed));
        double minWidth = _baseSize * MinWidthFactor;
        targetWidth = Math.Max(targetWidth, minWidth);

        _smoothedWidth = Lerp(_smoothedWidth, targetWidth, WidthSmoothing);
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

#pragma warning disable CS0162
        if (DebugDrawCenters && _points.Count > 1)
        {
            for (int i = 1; i < _points.Count; i++)
            {
                dc.DrawLine(DebugCenterPen, _points[i - 1].Position, _points[i].Position);
            }
        }
#pragma warning restore CS0162
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
        var pen = new WpfPen(System.Windows.Media.Brushes.Black, 1)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        for (int i = 0; i < samples.Count - 1; i++)
        {
            var p0 = samples[i];
            var p1 = samples[i + 1];
            if ((p1.Position - p0.Position).Length < 0.01)
            {
                continue;
            }
            double width = Math.Max(p0.Width, p1.Width);
            pen.Thickness = Math.Max(width, 0.1);
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

    private static WpfPen CreateFrozenPen(WpfColor color, double thickness)
    {
        var pen = new WpfPen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
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
            int steps = Math.Max(1, (int)Math.Ceiling(length / SplineTargetSpacing));
            int startStep = i == 0 ? 0 : 1;
            for (int s = startStep; s <= steps; s++)
            {
                double t = steps == 0 ? 0 : s / (double)steps;
                var pos = CatmullRomCentripetal(p0.Position, p1.Position, p2.Position, p3.Position, t);
                double width = Lerp(p1.Width, p2.Width, t);
                if (samples.Count > 0 && (pos - samples[^1].Position).Length < MinSampleSpacing)
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
        double maxTaper = _baseSize * MaxTaperLengthFactor;
        double taperLength = Math.Min(total * TaperLengthRatio, maxTaper);
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
                width *= Lerp(StartTaperFactor, 1.0, t);
            }
            if (endDist < taperLength)
            {
                double t = Smoothstep(endDist / taperLength);
                width *= Lerp(EndTaperFactor, 1.0, t);
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

    private static double Smoothstep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }
}
