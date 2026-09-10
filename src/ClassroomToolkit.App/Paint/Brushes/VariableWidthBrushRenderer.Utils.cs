using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
{
    private static WpfPoint CentripetalCatmullRomPoint(WpfPoint p0, WpfPoint p1, WpfPoint p2, WpfPoint p3, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        if (t <= 0.0) return p1;
        if (t >= 1.0) return p2;

        double t0 = 0.0;
        double t1 = t0 + Math.Sqrt(Math.Max((p1 - p0).Length, 0.0));
        double t2 = t1 + Math.Sqrt(Math.Max((p2 - p1).Length, 0.0));
        double t3 = t2 + Math.Sqrt(Math.Max((p3 - p2).Length, 0.0));
        if (t1 - t0 < 1e-6 || t2 - t1 < 1e-6 || t3 - t2 < 1e-6)
        {
            return new WpfPoint(Lerp(p1.X, p2.X, t), Lerp(p1.Y, p2.Y, t));
        }

        double u = Lerp(t1, t2, t);
        var a1 = InterpolatePoint(p0, p1, t0, t1, u);
        var a2 = InterpolatePoint(p1, p2, t1, t2, u);
        var a3 = InterpolatePoint(p2, p3, t2, t3, u);
        var b1 = InterpolatePoint(a1, a2, t0, t2, u);
        var b2 = InterpolatePoint(a2, a3, t1, t3, u);
        return InterpolatePoint(b1, b2, t1, t2, u);
    }

    private static WpfPoint InterpolatePoint(WpfPoint a, WpfPoint b, double ta, double tb, double t)
    {
        double denominator = tb - ta;
        if (Math.Abs(denominator) < 1e-6)
        {
            return a;
        }

        double amount = Math.Clamp((t - ta) / denominator, 0.0, 1.0);
        return new WpfPoint(Lerp(a.X, b.X, amount), Lerp(a.Y, b.Y, amount));
    }

    private static double InterpolateBounded(double a, double b, double t)
    {
        double value = Lerp(a, b, Math.Clamp(t, 0.0, 1.0));
        return Math.Clamp(value, Math.Min(a, b), Math.Max(a, b));
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
        var anchorMask = new bool[count];
        var cumulativeLengths = BuildCumulativePointLengths();
        MarkProtectedEndpointBand(anchorMask, epsilon);
        double cornerThreshold = Math.Clamp(_config.RdpCornerPreserveAngleDegrees, 12.0, 160.0);
        for (int i = 1; i < count - 1; i++)
        {
            if (IsCornerCandidate(i, cornerThreshold))
            {
                MarkAnchorWithProtectionBand(anchorMask, i);
            }
        }
        MarkDynamicAttributeAnchors(anchorMask, cumulativeLengths, epsilon);

        var anchors = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            if (anchorMask[i])
            {
                anchors.Add(i);
            }
        }
        foreach (var anchor in anchors)
        {
            keep[anchor] = true;
        }

        double epsSq = epsilon * epsilon;
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            RdpRecursive(anchors[i], anchors[i + 1], epsSq, keep, cumulativeLengths);
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
            InvalidatePolylineLengthCache();
        }

        void MarkProtectedEndpointBand(bool[] mask, double protectionEpsilon)
        {
            int band = Math.Clamp(
                1 + (int)Math.Ceiling(protectionEpsilon / Math.Max(_baseSize * 0.45, 1.0)),
                2,
                6);
            for (int i = 0; i <= band; i++)
            {
                MarkAnchor(mask, i);
                MarkAnchor(mask, count - 1 - i);
            }
        }

        static void MarkAnchor(bool[] mask, int index)
        {
            if ((uint)index < (uint)mask.Length)
            {
                mask[index] = true;
            }
        }

        void MarkAnchorWithProtectionBand(bool[] mask, int index)
        {
            MarkAnchor(mask, index - 1);
            MarkAnchor(mask, index);
            MarkAnchor(mask, index + 1);
        }
    }

    private double[] BuildCumulativePointLengths()
    {
        var cumulative = new double[_points.Count];
        for (int i = 1; i < _points.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + (_points[i].Position - _points[i - 1].Position).Length;
        }

        return cumulative;
    }

    private void MarkDynamicAttributeAnchors(
        bool[] anchorMask,
        double[] cumulativeLengths,
        double epsilon)
    {
        if (_points.Count < 3)
        {
            return;
        }

        double localThreshold = Math.Max(epsilon * 0.82, _baseSize * 0.045);
        for (int i = 1; i < _points.Count - 1; i++)
        {
            double localError = ResolveDynamicAttributeError(i, i - 1, i + 1, cumulativeLengths);
            if (localError > localThreshold)
            {
                // Keep a one-point protection band around the transition. This
                // preserves a pressure/wetness/nib change through later arc-length
                // resampling instead of leaving only one isolated spike.
                MarkAnchorWithProtectionBand(anchorMask, i);
            }
        }

        void MarkAnchorWithProtectionBand(bool[] mask, int index)
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                int candidate = index + offset;
                if ((uint)candidate < (uint)mask.Length)
                {
                    mask[candidate] = true;
                }
            }
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

    private void RdpRecursive(
        int start,
        int end,
        double epsSq,
        bool[] keep,
        double[] cumulativeLengths)
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
            double dynamicError = ResolveDynamicAttributeError(i, start, end, cumulativeLengths);
            double scoreSq = Math.Max(distSq, dynamicError * dynamicError);
            if (scoreSq > maxDistSq)
            {
                maxDistSq = scoreSq;
                maxIndex = i;
            }
        }

        if (maxIndex >= 0 && maxDistSq > epsSq)
        {
            keep[maxIndex] = true;
            RdpRecursive(start, maxIndex, epsSq, keep, cumulativeLengths);
            RdpRecursive(maxIndex, end, epsSq, keep, cumulativeLengths);
        }
    }

    private double ResolveDynamicAttributeError(
        int index,
        int start,
        int end,
        double[] cumulativeLengths)
    {
        if (index <= start || index >= end || (uint)end >= (uint)cumulativeLengths.Length)
        {
            return 0.0;
        }

        double span = cumulativeLengths[end] - cumulativeLengths[start];
        double t = span > 0.0001
            ? Math.Clamp((cumulativeLengths[index] - cumulativeLengths[start]) / span, 0.0, 1.0)
            : (index - start) / (double)Math.Max(1, end - start);

        var startPoint = _points[start];
        var currentPoint = _points[index];
        var endPoint = _points[end];
        double maxSpeed = Math.Max(_maxVelocity, 0.001);

        double widthError = Math.Abs(currentPoint.Width - Lerp(startPoint.Width, endPoint.Width, t));
        double speedError = Math.Abs(currentPoint.Speed - Lerp(startPoint.Speed, endPoint.Speed, t))
            / maxSpeed * _baseSize * 0.72;
        double accumulationError = Math.Abs(
                currentPoint.AccumulatedWidth
                - Lerp(startPoint.AccumulatedWidth, endPoint.AccumulatedWidth, t))
            / Math.Max(_baseSize, 0.001) * _baseSize * 0.58;
        double wetnessError = Math.Abs(
                currentPoint.Wetness
                - Lerp(startPoint.Wetness, endPoint.Wetness, t))
            * _baseSize * 0.72;
        double angleError = Math.Abs(NormalizeAngle(
                currentPoint.NibAngleRadians
                - LerpAngle(startPoint.NibAngleRadians, endPoint.NibAngleRadians, t)))
            / Math.PI * _baseSize * 0.7;
        double strengthError = Math.Abs(
                currentPoint.NibStrength
                - Lerp(startPoint.NibStrength, endPoint.NibStrength, t))
            * _baseSize * 0.45;

        return Math.Max(
            Math.Max(widthError, speedError),
            Math.Max(
                Math.Max(accumulationError, wetnessError),
                Math.Max(angleError, strengthError)));
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
    private static void FilterLoops(List<WpfPoint> edge)
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
