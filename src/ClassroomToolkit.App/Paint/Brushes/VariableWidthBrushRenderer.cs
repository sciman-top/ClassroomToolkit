using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private struct PointData
    {
        public WpfPoint Point;
        public long Timestamp;
        public double Width;

        public PointData(WpfPoint point, long timestamp, double width)
        {
            Point = point;
            Timestamp = timestamp;
            Width = width;
        }
    }

    private readonly List<PointData> _points = new();
    private WpfColor _color;
    private double _baseSize;
    private double _opacity;
    private bool _isActive;
    private long _lastTimestamp;
    
    // Configuration for the brush dynamics
    private const double MinWidthFactor = 0.25; 
    private const double MaxWidthFactor = 1.1; 
    private const double VelocityThreshold = 1.5;
    private const double MinDistanceThreshold = 2.0; // Filter out points closer than 2px

    public bool IsActive => _isActive;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _opacity = 1.0; // Use color's alpha channel directly
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        
        // Start with a thinner width for entry
        _points.Add(new PointData(point, _lastTimestamp, _baseSize * 0.5));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        var lastData = _points.Last();
        var dist = (point - lastData.Point).Length;

        // Input filtering: ignore tiny movements to reduce jitter
        if (dist < MinDistanceThreshold) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        var velocity = dist / dt;
        var targetWidth = CalculateWidth(velocity);
        
        // Width smoothing (Low-pass filter)
        // alpha determines how much weight the new width has. Lower = smoother changes.
        var alpha = 0.4; 
        var smoothedWidth = lastData.Width + (targetWidth - lastData.Width) * alpha;

        _points.Add(new PointData(point, now, smoothedWidth));
        _lastTimestamp = now;
    }

    public void OnUp(WpfPoint point)
    {
        if (!_isActive) return;
        
        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        // End with a sharp taper
        _points.Add(new PointData(point, now, 0.0));
        _isActive = false;
    }

    public void Reset()
    {
        _points.Clear();
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var geometry = GenerateGeometry();
        if (geometry != null)
        {
            var brush = new SolidColorBrush(_color) { Opacity = _opacity };
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
        }
    }

    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geo = GenerateGeometry();
        if (geo != null)
        {
            geo.Freeze();
        }
        return geo;
    }

    private double CalculateWidth(double velocity)
    {
        // Sigmoid-like easing for more natural pressure simulation
        // Fast (high velocity) -> Thin
        // Slow (low velocity) -> Thick
        
        var normalizedVel = Math.Min(velocity / VelocityThreshold, 1.0);
        
        // Easing function: 1 - t^2 (starts slow, drops fast) or Cosine
        var ease = 1.0 - (normalizedVel * normalizedVel); 
        
        var range = MaxWidthFactor - MinWidthFactor;
        return _baseSize * (MinWidthFactor + (range * ease));
    }

    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;

        // If very few points, just return a simple line geometry to avoid processing overhead
        if (_points.Count < 4)
        {
            return GenerateSimpleGeometry();
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();

            // We use the "Midpoint Quadratic Bezier" technique.
            // Curve passes through midpoints of segments, using the points as control points.
            
            // Start P0
            AddRibbonPoints(_points[0].Point, CalculateNormal(_points[0].Point, _points[1].Point), _points[0].Width, leftEdge, rightEdge);

            for (int i = 0; i < _points.Count - 1; i++)
            {
                var p0 = _points[i];
                var p1 = _points[i+1];

                // For the last segment, we just go to the end point
                if (i == _points.Count - 2)
                {
                     // Treat as line for the very last bit
                     AddRibbonPoints(p1.Point, CalculateNormal(p0.Point, p1.Point), p1.Width, leftEdge, rightEdge);
                     continue;
                }
                
                // For internal segments, we draw a Quad Bezier from Mid(i) to Mid(i+1) using P(i+1) as control?
                // Standard algorithm:
                // From: Mid(P_i, P_{i+1}) 
                // To: Mid(P_{i+1}, P_{i+2})
                // Control: P_{i+1}
                
                var p2 = _points[i+2];

                var start = (i == 0) ? p0.Point : Mid(p0.Point, p1.Point);
                var end = Mid(p1.Point, p2.Point);
                var control = p1.Point;

                var startWidth = (i == 0) ? p0.Width : (p0.Width + p1.Width) / 2.0;
                var endWidth = (p1.Width + p2.Width) / 2.0;
                var controlWidth = p1.Width;

                // Discretize this Bezier curve to generate variable width ribbon
                TessellateBezier(start, control, end, startWidth, controlWidth, endWidth, leftEdge, rightEdge);
            }
            
            // Construct the closed shape
            if (leftEdge.Count > 0)
            {
                ctx.BeginFigure(leftEdge[0], true, true);
                
                // Trace Left Edge
                for (int i = 1; i < leftEdge.Count; i++)
                {
                    ctx.LineTo(leftEdge[i], true, true);
                }

                // Trace Right Edge (Backwards)
                for (int i = rightEdge.Count - 1; i >= 0; i--)
                {
                    ctx.LineTo(rightEdge[i], true, true);
                }
            }
        }
        
        return geometry;
    }

    private void TessellateBezier(WpfPoint start, WpfPoint control, WpfPoint end, double wStart, double wControl, double wEnd, List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        // Number of steps depends on curvature/length, but fixed count is usually fine for handwriting strokes
        const int steps = 8; 

        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            
            // Quadratic Bezier Formula: B(t) = (1-t)^2 P0 + 2(1-t)t P1 + t^2 P2
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            double x = uu * start.X + 2 * u * t * control.X + tt * end.X;
            double y = uu * start.Y + 2 * u * t * control.Y + tt * end.Y;
            var pos = new WpfPoint(x, y);

            // Derivative for Tangent: B'(t) = 2(1-t)(P1-P0) + 2t(P2-P1)
            double tx = 2 * u * (control.X - start.X) + 2 * t * (end.X - control.X);
            double ty = 2 * u * (control.Y - start.Y) + 2 * t * (end.Y - control.Y);
            
            // Normal is (-y, x)
            var normal = new Vector(-ty, tx);
            if (normal.LengthSquared > 0.000001) normal.Normalize();

            // Interpolate width
            // This is an approximation. Ideally we calculate width at 't' based on arc length, 
            // but linear t interpolation is sufficient for short segments.
            double w = uu * wStart + 2 * u * t * wControl + tt * wEnd;

            AddRibbonPoints(pos, normal, w, lefts, rights);
        }
    }

    private void AddRibbonPoints(WpfPoint center, Vector normal, double width, List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        var offset = normal * (width * 0.5);
        lefts.Add(center + offset);
        rights.Add(center - offset);
    }

    private Geometry GenerateSimpleGeometry()
    {
        // Fallback for very short strokes (dots, tiny dashes)
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (_points.Count > 0)
            {
                var p = _points[0];
                var r = p.Width / 2.0;
                ctx.BeginFigure(new WpfPoint(p.Point.X - r, p.Point.Y - r), true, true);
                ctx.LineTo(new WpfPoint(p.Point.X + r, p.Point.Y - r), true, true);
                ctx.LineTo(new WpfPoint(p.Point.X + r, p.Point.Y + r), true, true);
                ctx.LineTo(new WpfPoint(p.Point.X - r, p.Point.Y + r), true, true);
            }
        }
        return geometry;
    }

    private static WpfPoint Mid(WpfPoint a, WpfPoint b)
    {
        return new WpfPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
    }

    private static Vector CalculateNormal(WpfPoint p1, WpfPoint p2)
    {
        var dir = p2 - p1;
        if (dir.LengthSquared < 0.000001) return new Vector(0, 1);
        dir.Normalize();
        return new Vector(-dir.Y, dir.X);
    }
}