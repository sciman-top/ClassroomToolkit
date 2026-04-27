using System;
using System.Collections.Generic;
using System.Linq;
using WpfPoint = System.Windows.Point;
using WpfVector = System.Windows.Vector;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkStrokeQualityReport(
    double JitterStdDev,
    double WidthStdDev,
    double CornerScore,
    double CompositeScore);

internal static class InkStrokeQualityMetrics
{
    public static InkStrokeQualityReport Analyze(
        IReadOnlyList<WpfPoint> rawPoints,
        IReadOnlyList<WpfPoint> smoothedPoints,
        IReadOnlyList<double>? widths = null)
    {
        if (rawPoints == null || smoothedPoints == null || rawPoints.Count < 2 || smoothedPoints.Count < 2)
        {
            return new InkStrokeQualityReport(0, 0, 0, 0);
        }

        var baseline = (rawPoints[0], rawPoints[^1]);
        var rawJitter = ComputeDistanceStdDev(rawPoints, baseline);
        var smoothJitter = ComputeDistanceStdDev(smoothedPoints, baseline);
        var jitterImprovement = rawJitter <= 0.001 ? 0 : Math.Clamp((rawJitter - smoothJitter) / rawJitter, -1, 1);

        double widthStdDev = 0;
        if (widths != null && widths.Count > 2)
        {
            widthStdDev = ComputeStdDev(widths);
        }

        double cornerScore = ComputeCornerScore(smoothedPoints);
        double composite = Math.Clamp((jitterImprovement * 0.45) + ((1.0 - Math.Min(widthStdDev / 10.0, 1.0)) * 0.2) + (cornerScore * 0.35), 0, 1);
        return new InkStrokeQualityReport(smoothJitter, widthStdDev, cornerScore, composite);
    }

    public static double ComputeDistanceStdDev(IReadOnlyList<WpfPoint> points, (WpfPoint Start, WpfPoint End) baseline)
    {
        if (points.Count == 0)
        {
            return 0;
        }
        var distances = points.Select(point => DistanceToLine(point, baseline.Start, baseline.End)).ToList();
        return ComputeStdDev(distances);
    }

    private static double ComputeCornerScore(IReadOnlyList<WpfPoint> points)
    {
        if (points.Count < 5)
        {
            return 0.5;
        }

        var angles = new List<double>(points.Count - 2);
        for (int i = 1; i < points.Count - 1; i++)
        {
            var a = points[i] - points[i - 1];
            var b = points[i + 1] - points[i];
            if (a.LengthSquared < 0.0001 || b.LengthSquared < 0.0001)
            {
                continue;
            }
            angles.Add(Math.Abs(WpfVector.AngleBetween(a, b)));
        }
        if (angles.Count == 0)
        {
            return 0.5;
        }

        double p85 = Percentile(angles, 0.85);
        return Math.Clamp(p85 / 120.0, 0.0, 1.0);
    }

    private static double ComputeStdDev(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        var avg = values.Average();
        var variance = values.Select(value => (value - avg) * (value - avg)).Average();
        return Math.Sqrt(variance);
    }

    private static double DistanceToLine(WpfPoint p, WpfPoint a, WpfPoint b)
    {
        var ab = b - a;
        var abLenSq = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (abLenSq < 0.000001)
        {
            return (p - a).Length;
        }
        var ap = p - a;
        var t = ((ap.X * ab.X) + (ap.Y * ab.Y)) / abLenSq;
        var projection = new WpfPoint(a.X + ab.X * t, a.Y + ab.Y * t);
        return (p - projection).Length;
    }

    private static double Percentile(List<double> values, double q)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        var sorted = values.OrderBy(v => v).ToArray();
        var pos = Math.Clamp(q, 0, 1) * (sorted.Length - 1);
        int i = (int)Math.Floor(pos);
        int j = Math.Min(sorted.Length - 1, i + 1);
        double t = pos - i;
        return sorted[i] + ((sorted[j] - sorted[i]) * t);
    }
}
