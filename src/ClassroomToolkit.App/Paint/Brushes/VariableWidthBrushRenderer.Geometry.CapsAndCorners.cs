using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Paint.Brushes;

public partial class VariableWidthBrushRenderer
{
    private void BuildStrokePathV10(StreamGeometryContext ctx, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, List<StrokePoint> samples)
    {
        ctx.BeginFigure(leftEdge[0], true, true);

        AddBezierPath(ctx, leftEdge);

        var endCap = BuildCapData(samples, true);
        AddCapV13(ctx, leftEdge.Last(), rightEdge.Last(), endCap);

        var rightEdgeReversed = rightEdge.AsEnumerable().Reverse().ToList();
        AddBezierPath(ctx, rightEdgeReversed);

        var startCap = BuildCapData(samples, false);
        AddCapV13(ctx, rightEdge[0], leftEdge[0], startCap);
    }

    private CapData BuildCapData(List<StrokePoint> samples, bool isEnd)
    {
        int count = samples.Count;
        if (count < 2)
        {
            return new CapData(samples[0].Position, ClampWidth(samples[0].Width), 0);
        }

        int lastIndex = count - 1;
        int prevIndex = Math.Max(0, lastIndex - 1);

        WpfPoint basePoint = isEnd ? samples[lastIndex].Position : samples[0].Position;
        WpfPoint refPoint = isEnd ? samples[prevIndex].Position : samples[1].Position;

        var dir = isEnd ? (basePoint - refPoint) : (refPoint - basePoint);
        if (dir.LengthSquared < 0.0001)
        {
            dir = new Vector(1, 0);
        }
        else
        {
            dir.Normalize();
        }

        var normal = new Vector(-dir.Y, dir.X);
        double brushAngle = _config.BrushAngleDegrees * Math.PI / 180.0;
        var brushDir = new Vector(Math.Cos(brushAngle), Math.Sin(brushAngle));
        double dot = Math.Clamp(Vector.Multiply(dir, brushDir), -1.0, 1.0);
        double angleDiff = Math.Acos(dot);
        double skewSign = Math.Sign((dir.X * brushDir.Y) - (dir.Y * brushDir.X));
        double skew = Math.Sin(angleDiff) * ClampWidth(samples[isEnd ? lastIndex : 0].Width) * 0.32 * skewSign;

        double width = Math.Clamp(
            isEnd ? samples[lastIndex].Width : samples[0].Width,
            Math.Max(0.14, _baseSize * 0.015),
            _baseSize * _config.MaxStrokeWidthMultiplier);
        double baseForTip = isEnd ? width : Math.Min(_baseSize * 0.8, width * 0.95);

        double tipLen = ClampTipLength(baseForTip);
        double dryFactor = Math.Clamp(1.0 - _lastInkFlow, 0, 1);
        tipLen *= Lerp(0.9, 1.25, dryFactor);
        if (!isEnd)
        {
            double capShrink = Math.Clamp(1.0 - _config.StartCapLength, 0.6, 1.0);
            tipLen *= 0.8 * capShrink;
            double segmentLen = (refPoint - basePoint).Length;
            double maxTip = Math.Max(baseForTip * 0.18, segmentLen * 0.5);
            tipLen = Math.Min(tipLen, maxTip);
        }
        var tipPoint = isEnd ? basePoint + dir * tipLen : basePoint - dir * tipLen;
        tipPoint += normal * skew;

        double dropRate = ComputePressureDropRate(samples, isEnd);
        return new CapData(tipPoint, width, dropRate);
    }

    private static double ClampTipLength(double width)
    {
        double minLen = width * 0.3;
        double maxLen = width * 1.2;
        double desired = width * 0.9;
        return Math.Clamp(desired, minLen, maxLen);
    }

    private static double ComputePressureDropRate(List<StrokePoint> samples, bool isEnd)
    {
        int count = samples.Count;
        if (count < 3) return 0;

        int window = Math.Max(2, count / 10);

        if (isEnd)
        {
            int prevIndex = Math.Max(0, count - 1 - window);
            double dp = Math.Max(samples[^1].Progress - samples[prevIndex].Progress, 0.001);
            double drop = Math.Max(0, samples[prevIndex].Width - samples[^1].Width);
            return drop / dp;
        }

        int nextIndex = Math.Min(count - 1, window);
        double dpStart = Math.Max(samples[nextIndex].Progress - samples[0].Progress, 0.001);
        double dropStart = Math.Max(0, samples[0].Width - samples[nextIndex].Width);
        return (dropStart / dpStart) * 0.45;
    }

    private void AddCapV13(StreamGeometryContext ctx, WpfPoint from, WpfPoint to, CapData cap)
    {
        double sharpThreshold = _baseSize * 0.2;
        double dropThreshold = Lerp(2.4, 3.2, _lastInkFlow);

        double normalizedDrop = cap.PressureDropRate / Math.Max(_baseSize, 0.001);
        bool useSharp = cap.Width < sharpThreshold && normalizedDrop > dropThreshold;

        if (useSharp)
        {
            ctx.LineTo(cap.TipPoint, true, true);
            ctx.LineTo(to, true, true);
            return;
        }

        AddRoundedCapArc(ctx, from, to, cap.TipPoint);
    }

    private static void AddRoundedCapArc(StreamGeometryContext ctx, WpfPoint from, WpfPoint to, WpfPoint tip)
    {
        double chord = (to - from).Length;
        if (chord < 0.1)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        var mid = new WpfPoint((from.X + to.X) * 0.5, (from.Y + to.Y) * 0.5);
        var chordVec = to - from;
        var normal = new Vector(-chordVec.Y, chordVec.X);
        if (normal.LengthSquared < 0.0001)
        {
            ctx.LineTo(to, true, true);
            return;
        }

        normal.Normalize();
        var tipVec = tip - mid;
        double h = Math.Abs(Vector.Multiply(tipVec, normal));
        h = Math.Max(h, chord * 0.15);

        double radius = (h / 2.0) + (chord * chord / (8.0 * h));
        radius = Math.Max(radius, chord * 0.5);
        radius = Math.Min(radius, chord * 3.0);

        double side = Vector.Multiply(tipVec, normal);
        var sweep = side >= 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

        ctx.ArcTo(to, new WpfSize(radius, radius), 0, false, sweep, true, true);
    }

    private void AddCornerReinforcement(List<WpfPoint> edge, WpfPoint center, Vector normalPrev, Vector normalNext, double width)
    {
        var bisector = normalPrev + normalNext;
        if (bisector.LengthSquared < 0.0001)
        {
            bisector = normalPrev;
        }

        if (bisector.LengthSquared < 0.0001)
        {
            return;
        }

        bisector.Normalize();

        double baseOffset = _baseSize * 0.1;
        double minOffset = _baseSize * 0.05;
        double maxOffset = _baseSize * 0.35;
        double offset = Math.Clamp(baseOffset, minOffset, maxOffset);
        offset = Math.Min(offset, width * 0.45);

        if (offset < 0.1) return;

        var point = center + bisector * offset;
        if (edge.Count == 0 || (edge.Last() - point).Length > 0.1)
        {
            edge.Add(point);
        }
    }

    private static void AddCornerArc(List<WpfPoint> edge, WpfPoint center, Vector startNormal, Vector endNormal, double radius, bool clockwise)
    {
        if (startNormal.LengthSquared < 0.0001 || endNormal.LengthSquared < 0.0001)
        {
            edge.Add(center + startNormal * radius);
            edge.Add(center + endNormal * radius);
            return;
        }

        startNormal.Normalize();
        endNormal.Normalize();

        double startAngle = Math.Atan2(startNormal.Y, startNormal.X);
        double endAngle = Math.Atan2(endNormal.Y, endNormal.X);
        double delta = endAngle - startAngle;

        if (clockwise)
        {
            if (delta > 0) delta -= Math.PI * 2;
        }
        else
        {
            if (delta < 0) delta += Math.PI * 2;
        }

        int segments = CornerArcSegments;
        var startPoint = center + startNormal * radius;
        if (edge.Count == 0 || (edge.Last() - startPoint).Length > 0.1)
        {
            edge.Add(startPoint);
        }

        for (int i = 1; i < segments; i++)
        {
            double t = i / (double)segments;
            double angle = startAngle + delta * t;
            edge.Add(new WpfPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }

        var endPoint = center + endNormal * radius;
        edge.Add(endPoint);
    }

    private static Vector GetNormalFromVector(Vector dir, Vector fallback)
    {
        if (dir.LengthSquared < 0.0001)
        {
            if (fallback.LengthSquared < 0.0001) return new Vector(0, 1);
            return fallback;
        }

        dir.Normalize();
        return new Vector(-dir.Y, dir.X);
    }

    private static double ResolveEllipticalNibRadius(
        double baseRadius,
        Vector normal,
        double nibAngleRadians,
        double nibStrength)
    {
        double safeBase = Math.Max(baseRadius, 0.08);
        if (normal.LengthSquared < 0.0001)
        {
            return safeBase;
        }

        normal.Normalize();
        double strength = Math.Clamp(nibStrength, 0.2, 2.0);
        double major = safeBase * Math.Clamp(1.0 + (0.55 * strength), 1.0, 2.35);
        double minor = safeBase * Math.Clamp(1.0 - (0.32 * strength), 0.42, 1.0);

        double c = Math.Cos(nibAngleRadians);
        double s = Math.Sin(nibAngleRadians);
        double u = (normal.X * c) + (normal.Y * s);
        double v = (-normal.X * s) + (normal.Y * c);
        double denom = ((u * u) / Math.Max(major * major, 1e-6))
                     + ((v * v) / Math.Max(minor * minor, 1e-6));
        if (denom < 1e-6)
        {
            return safeBase;
        }

        double radius = 1.0 / Math.Sqrt(denom);
        return Math.Clamp(radius, safeBase * 0.45, safeBase * 2.4);
    }
}
