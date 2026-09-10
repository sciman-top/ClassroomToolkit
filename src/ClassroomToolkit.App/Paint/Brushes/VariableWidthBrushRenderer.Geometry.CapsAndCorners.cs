using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
{
    private void BuildStrokePathV10(
        StreamGeometryContext ctx,
        List<WpfPoint> leftEdge,
        List<WpfPoint> rightEdge,
        List<StrokePoint> samples,
        bool includeStartCap,
        bool includeEndCap)
    {
        ctx.BeginFigure(leftEdge[0], true, true);

        AddBezierPath(ctx, leftEdge);

        if (includeEndCap)
        {
            var endCap = BuildCapData(samples, true);
            AddCapV13(ctx, leftEdge.Last(), rightEdge.Last(), endCap, isEnd: true);
        }
        else
        {
            ctx.LineTo(rightEdge.Last(), true, true);
        }

        var rightEdgeReversed = rightEdge.AsEnumerable().Reverse().ToList();
        AddBezierPath(ctx, rightEdgeReversed);

        if (includeStartCap)
        {
            var startCap = BuildCapData(samples, false);
            AddCapV13(ctx, rightEdge[0], leftEdge[0], startCap, isEnd: false);
        }
        else
        {
            ctx.LineTo(leftEdge[0], true, true);
        }
    }

    private CapData BuildCapData(List<StrokePoint> samples, bool isEnd)
    {
        int count = samples.Count;
        if (count < 2)
        {
            var nib = ResolveEndpointNibState(samples, isEnd);
            return new CapData(
                samples[0].Position,
                ClampWidth(samples[0].Width),
                0,
                nib.AngleRadians,
                nib.Strength);
        }

        int lastIndex = count - 1;
        int prevIndex = Math.Max(0, lastIndex - 1);

        WpfPoint basePoint = isEnd ? samples[lastIndex].Position : samples[0].Position;
        WpfPoint refPoint = isEnd ? samples[prevIndex].Position : samples[1].Position;

        var dir = isEnd ? (basePoint - refPoint) : (refPoint - basePoint);
        if (dir.LengthSquared < 0.0001)
        {
            dir = _lastStrokeDirection;
            if (dir.LengthSquared < 0.0001)
            {
                dir = new Vector(1, 0);
            }
            else
            {
                dir.Normalize();
            }
        }
        else
        {
            dir.Normalize();
        }

        var normal = new Vector(-dir.Y, dir.X);
        var nibState = ResolveEndpointNibState(samples, isEnd);
        double brushAngle = nibState.AngleRadians;
        var brushDir = new Vector(Math.Cos(brushAngle), Math.Sin(brushAngle));
        double dot = Math.Clamp(Vector.Multiply(dir, brushDir), -1.0, 1.0);
        double angleDiff = Math.Acos(dot);
        double skewSign = Math.Sign((dir.X * brushDir.Y) - (dir.Y * brushDir.X));
        double nibStrengthFactor = Lerp(
            0.72,
            1.28,
            Math.Clamp((nibState.Strength - 0.2) / 1.8, 0.0, 1.0));
        double skew = Math.Sin(angleDiff)
                    * ClampWidth(samples[isEnd ? lastIndex : 0].Width)
                    * 0.32
                    * nibStrengthFactor
                    * skewSign;

        double width = Math.Clamp(
            isEnd ? samples[lastIndex].Width : samples[0].Width,
            Math.Max(0.14, _baseSize * 0.015),
            _baseSize * _config.MaxStrokeWidthMultiplier);
        double baseForTip = isEnd ? width : Math.Min(_baseSize * 0.8, width * 0.95);

        double tipLen = ClampTipLength(baseForTip);
        bool exposedTaper = (isEnd ? _config.EndTaperStyle : _config.StartTaperStyle) == TaperCapStyle.Exposed;
        if (exposedTaper)
        {
            // 收锋宽度已经接近零时，若仍用“端点宽度”决定外延，最终轮廓会
            // 退化为圆钝的小圆帽。尖锋长度应参考笔画肩部宽度，但保持在
            // 配置收锋长度的合理范围内，避免短笔画长尾或投影下的过度外延。
            double shoulderWidth = samples.Max(sample => ClampWidth(sample.Width));
            double configuredTaperLength = Math.Clamp(
                _config.TaperLengthPx * Math.Clamp(_config.TaperLenScale, 0.6, 2.0),
                2.0,
                Math.Max(8.0, _baseSize * 6.0));
            double exposedTipLength = Math.Min(
                configuredTaperLength * 0.32,
                shoulderWidth * 0.72);
            tipLen = Math.Max(tipLen, exposedTipLength);
            tipLen = Math.Clamp(
                tipLen,
                Math.Max(1.5, _baseSize * 0.18),
                Math.Max(2.0, shoulderWidth * 1.05));
        }
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
        return new CapData(tipPoint, width, dropRate, nibState.AngleRadians, nibState.Strength);
    }

    private (double AngleRadians, double Strength) ResolveEndpointNibState(
        List<StrokePoint> samples,
        bool isEnd)
    {
        int endpointIndex = isEnd ? samples.Count - 1 : 0;
        double fallbackAngle = _config.BrushAngleDegrees * Math.PI / 180.0;
        double endpointAngle = samples[endpointIndex].NibAngleRadians;
        if (!double.IsFinite(endpointAngle))
        {
            endpointAngle = fallbackAngle;
        }

        int window = Math.Clamp(4 + (samples.Count / 20), 3, 12);
        double sumX = 0.0;
        double sumY = 0.0;
        double strengthSum = 0.0;
        double weightSum = 0.0;
        for (int offset = 0; offset < window; offset++)
        {
            int index = isEnd
                ? Math.Max(0, endpointIndex - offset)
                : Math.Min(samples.Count - 1, endpointIndex + offset);
            double angle = samples[index].NibAngleRadians;
            if (!double.IsFinite(angle))
            {
                angle = endpointAngle;
            }

            // 端点权重更高，但仍保留一个小窗口，避免最后一个抖动样本
            // 单独决定端帽偏转；cos/sin 平均跨越 ±π 接缝不跳变。
            double recency = 1.0 - (offset / (double)Math.Max(1, window - 1));
            double weight = 1.0 + (recency * 1.5);
            sumX += Math.Cos(angle) * weight;
            sumY += Math.Sin(angle) * weight;
            strengthSum += Math.Clamp(samples[index].NibStrength, 0.2, 2.0) * weight;
            weightSum += weight;
        }

        double angleMagnitude = Math.Sqrt((sumX * sumX) + (sumY * sumY));
        double averagedAngle = angleMagnitude > 1e-6
            ? Math.Atan2(sumY, sumX)
            : endpointAngle;
        double strength = weightSum > 1e-6
            ? strengthSum / weightSum
            : Math.Clamp(samples[endpointIndex].NibStrength, 0.2, 2.0);
        return (NormalizeAngle(averagedAngle), Math.Clamp(strength, 0.2, 2.0));
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

    private void AddCapV13(
        StreamGeometryContext ctx,
        WpfPoint from,
        WpfPoint to,
        CapData cap,
        bool isEnd)
    {
        double sharpThreshold = _baseSize * Lerp(
            0.23,
            0.17,
            Math.Clamp((cap.NibStrength - 0.2) / 1.8, 0.0, 1.0));
        double dropThreshold = Lerp(2.4, 3.2, _lastInkFlow);

        double normalizedDrop = cap.PressureDropRate / Math.Max(_baseSize, 0.001);
        bool exposedTaper = (isEnd ? _config.EndTaperStyle : _config.StartTaperStyle) == TaperCapStyle.Exposed;
        bool useSharp = exposedTaper || (cap.Width < sharpThreshold && normalizedDrop > dropThreshold);

        if (useSharp)
        {
            AddExposedTip(ctx, from, to, cap.TipPoint);
            return;
        }

        AddRoundedCapArc(ctx, from, to, cap.TipPoint);
    }

    private static void AddExposedTip(
        StreamGeometryContext ctx,
        WpfPoint from,
        WpfPoint to,
        WpfPoint tip)
    {
        var firstVector = tip - from;
        var secondVector = to - tip;
        if (firstVector.LengthSquared < 0.01 || secondVector.LengthSquared < 0.01)
        {
            ctx.LineTo(tip, true, true);
            ctx.LineTo(to, true, true);
            return;
        }

        // 两段 Bezier 都以真实 tip 为端点：外轮廓经过同一个尖点，
        // 同时保留一点曲率，避免收锋看起来像生硬的三角形。
        var firstControl = from + (firstVector * 0.78);
        var firstTipControl = tip - (firstVector * 0.14);
        var secondTipControl = tip + (secondVector * 0.14);
        var secondControl = to - (secondVector * 0.78);
        ctx.BezierTo(firstControl, firstTipControl, tip, true, true);
        ctx.BezierTo(secondTipControl, secondControl, to, true, true);
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

        if (!double.IsFinite(nibAngleRadians))
        {
            nibAngleRadians = 0.0;
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
