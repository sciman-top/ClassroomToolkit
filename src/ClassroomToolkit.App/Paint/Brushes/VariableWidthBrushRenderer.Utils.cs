using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

public partial class VariableWidthBrushRenderer
{
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

    private void SimplifyPointsRdp(double epsilon)
    {
        if (_points.Count < 3 || epsilon <= 0)
        {
            return;
        }

        int count = _points.Count;
        var keep = new bool[count];
        var anchors = new List<int> { 0, 1, count - 2, count - 1 };
        double cornerThreshold = Math.Clamp(_config.RdpCornerPreserveAngleDegrees, 12.0, 160.0);
        for (int i = 1; i < count - 1; i++)
        {
            if (IsCornerCandidate(i, cornerThreshold))
            {
                anchors.Add(i);
            }
        }
        anchors = anchors.Where(index => index >= 0 && index < count).Distinct().OrderBy(index => index).ToList();
        foreach (var anchor in anchors)
        {
            keep[anchor] = true;
        }

        double epsSq = epsilon * epsilon;
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            RdpRecursive(anchors[i], anchors[i + 1], epsSq, keep);
        }

        var simplified = new List<StrokePoint>();
        for (int i = 0; i < count; i++)
        {
            if (keep[i])
            {
                simplified.Add(_points[i]);
            }
        }

        if (simplified.Count >= 2)
        {
            _points.Clear();
            _points.AddRange(simplified);
        }
    }

    private bool IsCornerCandidate(int index, double thresholdDegrees)
    {
        if (index <= 0 || index >= _points.Count - 1)
        {
            return false;
        }

        var prev = _points[index - 1].Position;
        var curr = _points[index].Position;
        var next = _points[index + 1].Position;
        var a = curr - prev;
        var b = next - curr;
        if (a.LengthSquared < 0.0001 || b.LengthSquared < 0.0001)
        {
            return false;
        }

        a.Normalize();
        b.Normalize();
        var angle = Math.Abs(Vector.AngleBetween(a, b));
        if (angle < 1.0)
        {
            return false;
        }

        // Smaller interior angle should be preserved to avoid over-rounding corners.
        return angle >= thresholdDegrees;
    }

    private void RdpRecursive(int start, int end, double epsSq, bool[] keep)
    {
        if (end <= start + 1)
        {
            return;
        }

        var a = _points[start].Position;
        var b = _points[end].Position;
        double maxDistSq = 0;
        int maxIndex = -1;

        for (int i = start + 1; i < end; i++)
        {
            var p = _points[i].Position;
            double distSq = DistanceToSegmentSquared(p, a, b);
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
                maxIndex = i;
            }
        }

        if (maxIndex >= 0 && maxDistSq > epsSq)
        {
            keep[maxIndex] = true;
            RdpRecursive(start, maxIndex, epsSq, keep);
            RdpRecursive(maxIndex, end, epsSq, keep);
        }
    }

    private static double DistanceToSegmentSquared(WpfPoint p, WpfPoint a, WpfPoint b)
    {
        var ab = b - a;
        double abLenSq = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (abLenSq < 0.000001)
        {
            var ap = p - a;
            return (ap.X * ap.X) + (ap.Y * ap.Y);
        }

        var ap2 = p - a;
        double t = ((ap2.X * ab.X) + (ap2.Y * ab.Y)) / abLenSq;
        t = Math.Clamp(t, 0, 1);
        var proj = new WpfPoint(a.X + (ab.X * t), a.Y + (ab.Y * t));
        var diff = p - proj;
        return (diff.X * diff.X) + (diff.Y * diff.Y);
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
}
