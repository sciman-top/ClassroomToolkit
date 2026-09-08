using System;
using System.Collections.Generic;
using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
{
    private List<StrokePoint> BuildCenterlineSamplesFinal()
    {
        return BuildCenterlineSamplesFinal(_points, previewFastPath: false, globalStartLength: 0.0, globalTotalLength: ComputePolylineLength(_points));
    }

    private List<StrokePoint> BuildCenterlineSamplesFinal(IReadOnlyList<StrokePoint> sourcePoints, bool previewFastPath)
    {
        return BuildCenterlineSamplesFinal(
            sourcePoints,
            previewFastPath,
            globalStartLength: 0.0,
            globalTotalLength: ComputePolylineLength(sourcePoints));
    }

    private List<StrokePoint> BuildCenterlineSamplesFinal(
        IReadOnlyList<StrokePoint> sourcePoints,
        bool previewFastPath,
        double globalStartLength,
        double globalTotalLength)
    {
        var samples = new List<StrokePoint>();
        if (sourcePoints.Count == 0)
        {
            _lastResampledPointCount = 0;
            return samples;
        }
        if (sourcePoints.Count == 1)
        {
            var p = sourcePoints[0];
            samples.Add(new StrokePoint(
                p.Position,
                ClampWidth(p.Width),
                0,
                0,
                0,
                p.AccumulatedWidth,
                p.NoisePhase,
                p.Wetness,
                p.NibAngleRadians,
                p.NibStrength));
            _lastResampledPointCount = samples.Count;
            return samples;
        }

        double sourceLength = 0;
        for (int i = 1; i < sourcePoints.Count; i++)
        {
            sourceLength += (sourcePoints[i].Position - sourcePoints[i - 1].Position).Length;
        }
        double totalLength = Math.Max(globalTotalLength, globalStartLength + sourceLength);

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        for (int i = 0; i < sourcePoints.Count; i++)
        {
            if (sourcePoints[i].Speed > maxSpeed)
            {
                maxSpeed = sourcePoints[i].Speed;
            }
        }
        double accumulatedLength = Math.Max(0.0, globalStartLength);

        for (int i = 0; i < sourcePoints.Count - 1; i++)
        {
            var p0 = sourcePoints[Math.Max(i - 1, 0)];
            var p1 = sourcePoints[i];
            var p2 = sourcePoints[i + 1];
            var p3 = sourcePoints[Math.Min(i + 2, sourcePoints.Count - 1)];

            int upsampleSteps = ResolveUpsampleSteps(p0, p1, p2, p3, previewFastPath);
            int startStep = (i == 0) ? 0 : 1;
            for (int step = startStep; step <= upsampleSteps; step++)
            {
                double t = step / (double)upsampleSteps;
                // 位置使用 centripetal spline，降低不均匀点距、急转和回折时的过冲；
                // 宽度/速度/湿度等标量只做有界插值，不能因 spline overshoot 产生
                // 负宽度、虚假高速或湿度越界。
                var pos = CentripetalCatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);

                double speed = InterpolateBounded(p1.Speed, p2.Speed, t);
                double accumulatedWidth = InterpolateBounded(p1.AccumulatedWidth, p2.AccumulatedWidth, t);
                double width = InterpolateBounded(p1.Width, p2.Width, t);
                double noisePhase = InterpolateBounded(p1.NoisePhase, p2.NoisePhase, t);
                double wetness = InterpolateBounded(p1.Wetness, p2.Wetness, t);
                double nibAngle = LerpAngle(p1.NibAngleRadians, p2.NibAngleRadians, t);
                double nibStrength = Lerp(p1.NibStrength, p2.NibStrength, t);

                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples[^1].Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }

                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);
                double normSpeed = Math.Clamp(speed / maxSpeed, 0, 1);

                double targetWidth = ClampWidth(width);
                double currentWidth = targetWidth;
                if (samples.Count > 0)
                {
                    currentWidth = samples[^1].Width * 0.52 + targetWidth * 0.48;
                }

                // 开启 post-resample taper 时，端点宽度只在
                // ApplyEndpointTaper 中统一计算；避免插值前后重复收锋。
                if (_config.SimulateEndTaper && !_config.EnableEndpointTaperPostResample)
                {
                    currentWidth *= ResolveEndProgressTaperFactor(progress);
                }

                currentWidth = ClampWidth(currentWidth);
                samples.Add(new StrokePoint(
                    pos,
                    currentWidth,
                    speed,
                    normSpeed,
                    progress,
                    accumulatedWidth,
                    noisePhase,
                    Math.Clamp(wetness, 0.0, 1.0),
                    nibAngle,
                    Math.Clamp(nibStrength, 0.2, 2.0)));
            }
        }

        var resampled = ResampleByArcLength(samples, previewFastPath);
        if (_config.EnableEndpointTaperPostResample)
        {
            ApplyEndpointTaper(resampled, globalStartLength, totalLength);
        }
        _lastResampledPointCount = resampled.Count;
        return resampled;
    }

    private List<StrokePoint> ResampleByArcLength(List<StrokePoint> source, bool previewFastPath)
    {
        if (source.Count < 2)
        {
            return source;
        }

        double step = _config.ArcLengthResampleStepPx;
        if (previewFastPath)
        {
            step *= PreviewFastArcLengthStepFactor;
        }
        step = Math.Clamp(step, 0.6, 6.0);
        int maxPoints = Math.Max(2, _config.MaxResampledPointCount);
        if (previewFastPath)
        {
            maxPoints = Math.Min(maxPoints, PreviewFastMaxResampledPointCount);
        }
        var cumulative = new double[source.Count];
        double totalLength = 0.0;
        cumulative[0] = 0.0;
        for (int i = 1; i < source.Count; i++)
        {
            totalLength += (source[i].Position - source[i - 1].Position).Length;
            cumulative[i] = totalLength;
        }

        if (totalLength <= 0.001)
        {
            return new List<StrokePoint> { source[0], source[^1] };
        }

        int targetCount = (int)Math.Ceiling(totalLength / step) + 1;
        targetCount = Math.Clamp(targetCount, 2, maxPoints);
        double distanceStep = totalLength / Math.Max(1, targetCount - 1);
        var result = new List<StrokePoint>(targetCount);
        int segmentIndex = 1;

        for (int i = 0; i < targetCount; i++)
        {
            double distance = i == targetCount - 1 ? totalLength : distanceStep * i;
            while (segmentIndex < cumulative.Length - 1 && cumulative[segmentIndex] < distance)
            {
                segmentIndex++;
            }

            int prevIndex = Math.Max(0, segmentIndex - 1);
            double startDistance = cumulative[prevIndex];
            double endDistance = cumulative[segmentIndex];
            double segmentLength = Math.Max(endDistance - startDistance, 0.000001);
            double t = Math.Clamp((distance - startDistance) / segmentLength, 0.0, 1.0);
            result.Add(InterpolateStrokePoint(source[prevIndex], source[segmentIndex], t));
        }

        return result;
    }

    private static StrokePoint InterpolateStrokePoint(StrokePoint a, StrokePoint b, double t)
    {
        return new StrokePoint(
            new WpfPoint(
                Lerp(a.Position.X, b.Position.X, t),
                Lerp(a.Position.Y, b.Position.Y, t)),
            Lerp(a.Width, b.Width, t),
            Lerp(a.Speed, b.Speed, t),
            Lerp(a.NormalizedSpeed, b.NormalizedSpeed, t),
            Lerp(a.Progress, b.Progress, t),
            Lerp(a.AccumulatedWidth, b.AccumulatedWidth, t),
            Lerp(a.NoisePhase, b.NoisePhase, t),
            Lerp(a.Wetness, b.Wetness, t),
            LerpAngle(a.NibAngleRadians, b.NibAngleRadians, t),
            Lerp(a.NibStrength, b.NibStrength, t));
    }

    private void ApplyEndpointTaper(List<StrokePoint> samples, double globalStartLength, double globalTotalLength)
    {
        if (samples.Count < 2)
        {
            return;
        }

        double taperAutoScaleK = Math.Clamp(_config.TaperRadiusScaleK, 0.5, 6.0);
        const double taperMinLenDip = 2.0;
        double taperMaxLenDip = Math.Max(64.0, _baseSize * 6.0);
        double taperLength = Math.Clamp(_config.TaperLengthPx, taperMinLenDip, taperMaxLenDip);
        if (taperLength <= 0.001)
        {
            return;
        }

        double strength = Math.Clamp(_config.TaperStrength, 0.0, 1.0);
        if (strength <= 0.001)
        {
            return;
        }

        var cumulative = new double[samples.Count];
        cumulative[0] = Math.Max(0.0, globalStartLength);
        for (int i = 1; i < samples.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + (samples[i].Position - samples[i - 1].Position).Length;
        }

        double totalLength = Math.Max(cumulative[^1], globalTotalLength);
        if (totalLength <= 0.001)
        {
            return;
        }

        // 收锋长度应由画笔尺寸决定，而不是由本笔速度造成的瞬时最大宽度
        // 决定；否则同一支笔在快慢两种书写速度下会得到不同的基础收锋长。
        double maxRadius = Math.Max(_baseSize * 0.5, _config.MinStrokeWidthPx * 0.5);
        double taperLenScale = Math.Clamp(_config.TaperLenScale, 0.6, 2.0);
        double taperAutoScaled = (taperLength * taperLenScale) + (taperAutoScaleK * maxRadius);
        taperLength = Math.Clamp(taperAutoScaled, taperMinLenDip, taperMaxLenDip);
        _lastEffectiveTaperBaseDip = taperLength;

        // 收锋长度随离笔速度缩放：慢收变钝、快甩出长锋（起点藏锋不受影响）。
        double endVelocityFactor = Lerp(
            Math.Min(_config.TaperLengthVelocityMinFactor, _config.TaperLengthVelocityMaxFactor),
            Math.Max(_config.TaperLengthVelocityMinFactor, _config.TaperLengthVelocityMaxFactor),
            Math.Clamp(_releaseSpeedNorm, 0.0, 1.0));
        double endTaperLength = Math.Clamp(taperLength * endVelocityFactor, taperMinLenDip, taperMaxLenDip);

        bool isShortStroke = totalLength < (2.0 * taperLength);
        bool isDotLikeStroke = totalLength <= Math.Max(2.0, _baseSize * 0.85);
        double effectiveTaperLength = isShortStroke
            ? Math.Max(totalLength * 0.5, 0.5)
            : taperLength;
        double effectiveEndTaperLength = isShortStroke
            ? effectiveTaperLength
            : endTaperLength;
        LastEffectiveEndTaperLengthDip = effectiveEndTaperLength;
        double effectiveStrength = isDotLikeStroke
            ? Math.Min(strength, 0.72)
            : strength;

        double minTipWidth = Math.Max(0.14, _baseSize * 0.015);
        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            double startDist = cumulative[i];
            double endDist = totalLength - cumulative[i];
            double width = sample.Width;
            double startFactor = 1.0;
            double endFactor = 1.0;

            if (startDist <= effectiveTaperLength)
            {
                double t = Math.Clamp(startDist / effectiveTaperLength, 0.0, 1.0);
                startFactor = ResolveTaperFactor(_config.StartTaperStyle, t, effectiveStrength, _config.TaperEasePower);
            }

            if (endDist <= effectiveEndTaperLength)
            {
                double t = Math.Clamp(endDist / effectiveEndTaperLength, 0.0, 1.0);
                endFactor = ResolveTaperFactor(_config.EndTaperStyle, t, effectiveStrength, _config.TaperEasePower);
            }

            if (isDotLikeStroke)
            {
                double arcT = Math.Clamp(cumulative[i] / Math.Max(totalLength, 0.0001), 0.0, 1.0);
                double headWeight = Math.Clamp(arcT / 0.6, 0.0, 1.0);
                headWeight = headWeight * headWeight * (3.0 - (2.0 * headWeight));
                double headMixCap = Math.Clamp(_config.DotLikeHeadMixCap, 0.1, 0.7);
                // 点画的起笔应保留着纸宽度；短笔画取消真实端点外延后，
                // 不能再用过强的 head taper 把首段压成中段以下。
                double headMix = Lerp(0.10, headMixCap, headWeight);
                startFactor = Lerp(1.0, startFactor, headMix);

                double tailWeight = Math.Clamp((arcT - 0.45) / 0.55, 0.0, 1.0);
                tailWeight = tailWeight * tailWeight * (3.0 - (2.0 * tailWeight));
                double tailSharpMin = Math.Clamp(_config.DotLikeTailSharpMin, 0.8, 0.98);
                double tailSharp = Lerp(1.0, tailSharpMin, tailWeight);
                endFactor = Math.Clamp(endFactor * tailSharp, 0.02, 1.0);
            }

            double combinedFactor = isShortStroke
                ? Math.Min(startFactor, endFactor)
                : (startFactor * endFactor);
            if (_config.SimulateEndTaper)
            {
                // 与端点距离 taper 在同一最终轮廓阶段合成，
                // 这样采样密度变化不会重复压缩收锋宽度。
                combinedFactor *= ResolveEndProgressTaperFactor(
                    Math.Clamp(cumulative[i] / Math.Max(totalLength, 0.0001), 0.0, 1.0));
            }
            if (isDotLikeStroke)
            {
                double arcT = Math.Clamp(cumulative[i] / Math.Max(totalLength, 0.0001), 0.0, 1.0);
                double centerWeight = 1.0 - Math.Abs((arcT * 2.0) - 1.0);
                centerWeight = Math.Clamp(centerWeight, 0.0, 1.0);
                centerWeight = centerWeight * centerWeight * (3.0 - (2.0 * centerWeight));
                double bodyFloor = Lerp(0.22, 0.72, centerWeight);
                combinedFactor = Math.Max(combinedFactor, bodyFloor);
            }
            width *= combinedFactor;
            width = Math.Clamp(width, minTipWidth, _baseSize * _config.MaxStrokeWidthMultiplier);
            samples[i] = new StrokePoint(
                sample.Position,
                width,
                sample.Speed,
                sample.NormalizedSpeed,
                sample.Progress,
                sample.AccumulatedWidth,
                sample.NoisePhase,
                sample.Wetness,
                sample.NibAngleRadians,
                sample.NibStrength);
        }
    }

    private double ResolveEndProgressTaperFactor(double progress)
    {
        double startProgress = Math.Clamp(_config.EndTaperStartProgress, 0.0, 0.99);
        if (progress <= startProgress)
        {
            return 1.0;
        }

        double taperProgress = Math.Clamp(
            (progress - startProgress) / Math.Max(0.01, 1.0 - startProgress),
            0.0,
            1.0);
        double taperCurve = 1.0 - (taperProgress * taperProgress);
        double taperFactor = _config.TaperMinWidthFactor
                           + ((1.0 - _config.TaperMinWidthFactor) * taperCurve);
        if (progress > 0.85)
        {
            double tailT = Math.Clamp((progress - 0.85) / 0.15, 0.0, 1.0);
            taperFactor *= Lerp(1.0, 0.84, tailT);
        }

        return Math.Clamp(taperFactor, 0.02, 1.0);
    }

    private static double ComputePolylineLength(IReadOnlyList<StrokePoint> points, int endExclusive = -1)
    {
        int end = endExclusive < 0 ? points.Count : Math.Min(endExclusive, points.Count);
        double length = 0.0;
        for (int i = 1; i < end; i++)
        {
            length += (points[i].Position - points[i - 1].Position).Length;
        }

        return length;
    }

    private static double ResolveTaperFactor(TaperCapStyle style, double normalizedDistance, double strength, double easePower)
    {
        double smooth = normalizedDistance * normalizedDistance * (3.0 - (2.0 * normalizedDistance));
        if (Math.Abs(easePower - 1.0) > 0.0001)
        {
            smooth = Math.Pow(smooth, Math.Max(0.05, easePower));
        }

        switch (style)
        {
            case TaperCapStyle.Exposed:
                {
                    double edge = Math.Max(0.02, 1.0 - strength);
                    double exposedCurve = smooth * smooth;
                    return Lerp(edge, 1.0, exposedCurve);
                }
            case TaperCapStyle.Hidden:
            default:
                {
                    double edge = Math.Max(0.06, 1.0 - (strength * 0.55));
                    return Lerp(edge, 1.0, smooth);
                }
        }
    }

    private int ResolveUpsampleSteps(StrokePoint p0, StrokePoint p1, StrokePoint p2, StrokePoint p3, bool previewFastPath = false)
    {
        int minSteps = Math.Clamp(_config.MinUpsampleSteps, 1, 24);
        int maxSteps = Math.Clamp(_config.MaxUpsampleSteps, minSteps, 32);
        if (previewFastPath)
        {
            maxSteps = Math.Min(maxSteps, PreviewFastMaxUpsampleSteps);
        }
        double targetSpacing = Math.Max(_config.UpsampleTargetSpacing, 0.2);
        if (previewFastPath)
        {
            targetSpacing *= PreviewFastUpsampleSpacingFactor;
        }

        double segmentLength = (p2.Position - p1.Position).Length;
        int stepsByLength = (int)Math.Ceiling(segmentLength / targetSpacing);
        stepsByLength = Math.Clamp(stepsByLength, minSteps, maxSteps);

        double curvature = ResolveCurvatureFactor(
            p0.Position,
            p1.Position,
            p2.Position,
            p3.Position,
            _config.UpsampleCurvatureReferenceDegrees);
        double boost = 1.0 + (Math.Max(_config.UpsampleCurvatureBoost, 0.0) * curvature);
        int boostedSteps = (int)Math.Ceiling(stepsByLength * boost);
        return Math.Clamp(boostedSteps, minSteps, maxSteps);
    }

    private static double ResolveCurvatureFactor(
        WpfPoint p0,
        WpfPoint p1,
        WpfPoint p2,
        WpfPoint p3,
        double referenceDegrees)
    {
        double angle01 = ResolveCornerAngleDegrees(p1 - p0, p2 - p1);
        double angle12 = ResolveCornerAngleDegrees(p2 - p1, p3 - p2);
        double maxAngle = Math.Max(angle01, angle12);
        double reference = Math.Clamp(referenceDegrees, 12.0, 170.0);
        return Math.Clamp(maxAngle / reference, 0.0, 1.0);
    }

    private static double ResolveCornerAngleDegrees(Vector a, Vector b)
    {
        if (a.LengthSquared < 0.0001 || b.LengthSquared < 0.0001)
        {
            return 0.0;
        }

        a.Normalize();
        b.Normalize();
        return Math.Abs(Vector.AngleBetween(a, b));
    }
}
