using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ClassroomToolkit.App.Paint.Brushes;

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private struct PointData
    {
        public Point Point;
        public long Timestamp;
        public double Width;

        public PointData(Point point, long timestamp, double width)
        {
            Point = point;
            Timestamp = timestamp;
            Width = width;
        }
    }

    private readonly List<PointData> _points = new();
    private Color _color;
    private double _baseSize;
    private double _opacity;
    private bool _isActive;
    private long _lastTimestamp;
    
    // Configuration for the brush dynamics
    private const double MinWidthFactor = 0.2; // Fast strokes are 20% of base size
    private const double MaxWidthFactor = 1.2; // Slow strokes are 120% of base size
    private const double VelocityThreshold = 2.0; // Velocity scaling factor

    public bool IsActive => _isActive;

    public void Initialize(Color color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _opacity = opacity / 255.0; // Normalize to 0-1
    }

    public void OnDown(Point point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        
        // Initial point has default width
        _points.Add(new PointData(point, _lastTimestamp, _baseSize));
    }

    public void OnMove(Point point)
    {
        if (!_isActive) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        
        // Avoid division by zero or extremely small time intervals
        if (dt < 1) dt = 1;

        var lastPoint = _points.Last().Point;
        var distance = (point - lastPoint).Length;
        var velocity = distance / dt;

        // Calculate dynamic width based on velocity
        // Higher velocity -> Thinner line
        // Lower velocity -> Thicker line
        
        var targetWidth = CalculateWidth(velocity);
        
        // Smooth the width transition to avoid jagged edges
        var prevWidth = _points.Last().Width;
        var smoothedWidth = Lerp(prevWidth, targetWidth, 0.3); // 0.3 smoothing factor

        _points.Add(new PointData(point, now, smoothedWidth));
        _lastTimestamp = now;
    }

    public void OnUp(Point point)
    {
        if (!_isActive) return;
        
        // Add final point with tapering
        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        _points.Add(new PointData(point, now, 0.1)); // Taper to tip
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
        // Simple inverse relationship: fast = thin, slow = thick
        // Map velocity (approx 0 to 5 px/ms) to factor
        
        var factor = Math.Max(MinWidthFactor, Math.Min(MaxWidthFactor, 1.0 - (velocity / VelocityThreshold)));
        
        // If velocity is very low (start/stop), boost width slightly for ink bleed effect
        if (velocity < 0.1) factor = MaxWidthFactor;

        return _baseSize * factor;
    }

    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // We will construct a mesh (strip of triangles) representing the stroke
            
            // 1. Calculate left and right boundary points
            var leftPoints = new List<Point>();
            var rightPoints = new List<Point>();

            for (int i = 0; i < _points.Count - 1; i++)
            {
                var p1 = _points[i];
                var p2 = _points[i + 1];

                var dir = p2.Point - p1.Point;
                if (dir.LengthSquared < 0.0001) continue;
                dir.Normalize();

                var normal = new Vector(-dir.Y, dir.X);
                
                var w1 = p1.Width / 2.0;
                var w2 = p2.Width / 2.0;

                if (i == 0)
                {
                    // Start cap
                    leftPoints.Add(p1.Point + normal * w1);
                    rightPoints.Add(p1.Point - normal * w1);
                }

                leftPoints.Add(p2.Point + normal * w2);
                rightPoints.Add(p2.Point - normal * w2);
            }

            // 2. Build the path
            // Start at the first left point
            if (leftPoints.Count == 0) return null;

            ctx.BeginFigure(leftPoints[0], true, true);

            // Go down the left side (using Bezier or PolyLine? Let's use PolyLine for performance first, or Bezier if needed)
            // For smoother look, we should use PolyQuadraticBezier or similar, but let's try straight lines for the hull first.
            // With high sample rate, straight lines are fine.
            
            for (int i = 1; i < leftPoints.Count; i++)
            {
                ctx.LineTo(leftPoints[i], true, true);
            }

            // Cross over to the end of right side (which is the last point, reversed)
            // The tip is effectively the line between last left and last right.
            
            // Come back up the right side
            for (int i = rightPoints.Count - 1; i >= 0; i--)
            {
                ctx.LineTo(rightPoints[i], true, true);
            }
            
            // Close the figure (connect back to start)
        }

        return geometry;
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }
}
