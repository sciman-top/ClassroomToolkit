using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint.Brushes;

public partial class VariableWidthBrushRenderer
{
    private List<StrokePoint> BuildCenterlineSamplesFinal()
    {
        return BuildCenterlineSamplesFinal(_points, previewFastPath: false);
    }

    private List<StrokePoint> BuildCenterlineSamplesFinal(IReadOnlyList<StrokePoint> sourcePoints, bool previewFastPath)
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

        double totalLength = 0;
        for (int i = 1; i < sourcePoints.Count; i++)
        {
            totalLength += (sourcePoints[i].Position - sourcePoints[i - 1].Position).Length;
        }

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        for (int i = 0; i < sourcePoints.Count; i++)
        {
            if (sourcePoints[i].Speed > maxSpeed)
            {
                maxSpeed = sourcePoints[i].Speed;
            }
        }
        double accumulatedLength = 0;

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
                var pos = CatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);

                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);
                double width = CatmullRomValue(p0.Width, p1.Width, p2.Width, p3.Width, t);
                double noisePhase = CatmullRomValue(p0.NoisePhase, p1.NoisePhase, p2.NoisePhase, p3.NoisePhase, t);
                double wetness = CatmullRomValue(p0.Wetness, p1.Wetness, p2.Wetness, p3.Wetness, t);
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

                if (_config.SimulateEndTaper && progress > _config.EndTaperStartProgress)
                {
                    double taperProgress = Math.Clamp(
                        (progress - _config.EndTaperStartProgress) / (1.0 - _config.EndTaperStartProgress),
                        0.0, 1.0);
                    double taperCurve = 1.0 - (taperProgress * taperProgress);
                    double taperFactor = _config.TaperMinWidthFactor + (1.0 - _config.TaperMinWidthFactor) * taperCurve;
                    if (progress > 0.85)
                    {
                        double tailT = Math.Clamp((progress - 0.85) / 0.15, 0.0, 1.0);
                        taperFactor *= Lerp(1.0, 0.84, tailT);
                    }
                    currentWidth *= taperFactor;
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
            ApplyEndpointTaper(resampled);
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

    private void ApplyEndpointTaper(List<StrokePoint> samples)
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
        cumulative[0] = 0.0;
        for (int i = 1; i < samples.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + (samples[i].Position - samples[i - 1].Position).Length;
        }

        double totalLength = cumulative[^1];
        if (totalLength <= 0.001)
        {
            return;
        }

        double maxRadius = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            maxRadius = Math.Max(maxRadius, samples[i].Width * 0.5);
        }
        double taperLenScale = Math.Clamp(_config.TaperLenScale, 0.6, 2.0);
        double taperAutoScaled = (taperLength * taperLenScale) + (taperAutoScaleK * maxRadius);
        taperLength = Math.Clamp(taperAutoScaled, taperMinLenDip, taperMaxLenDip);
        _lastEffectiveTaperBaseDip = taperLength;

        bool isShortStroke = totalLength < (2.0 * taperLength);
        bool isDotLikeStroke = totalLength <= Math.Max(2.0, _baseSize * 0.85);
        double effectiveTaperLength = isShortStroke
            ? Math.Max(totalLength * 0.5, 0.5)
            : taperLength;
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
                startFactor = ResolveTaperFactor(_config.StartTaperStyle, t, effectiveStrength);
            }

            if (endDist <= effectiveTaperLength)
            {
                double t = Math.Clamp(endDist / effectiveTaperLength, 0.0, 1.0);
                endFactor = ResolveTaperFactor(_config.EndTaperStyle, t, effectiveStrength);
            }

            if (isDotLikeStroke)
            {
                double arcT = Math.Clamp(cumulative[i] / Math.Max(totalLength, 0.0001), 0.0, 1.0);
                double headWeight = Math.Clamp(arcT / 0.6, 0.0, 1.0);
                headWeight = headWeight * headWeight * (3.0 - (2.0 * headWeight));
                double headMixCap = Math.Clamp(_config.DotLikeHeadMixCap, 0.1, 0.7);
                double headMix = Lerp(0.22, headMixCap, headWeight);
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

    private static double ResolveTaperFactor(TaperCapStyle style, double normalizedDistance, double strength)
    {
        double smooth = normalizedDistance * normalizedDistance * (3.0 - (2.0 * normalizedDistance));
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

    private List<StrokePoint> BuildCenterlineSamplesV10()
    {
        var samples = new List<StrokePoint>();
        if (_points.Count == 0) return samples;
        if (_points.Count == 1)
        {
            samples.Add(_points[0]);
            return samples;
        }

        double totalLength = 0;
        for (int i = 1; i < _points.Count; i++)
        {
            totalLength += (_points[i].Position - _points[i - 1].Position).Length;
        }

        double velocityRange = _maxVelocity - _minVelocity;
        if (velocityRange < 0.001) velocityRange = 1.0;

        double accumulatedLength = 0;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[Math.Max(i - 1, 0)];
            var p1 = _points[i];
            var p2 = _points[i + 1];
            var p3 = _points[Math.Min(i + 2, _points.Count - 1)];

            int upsampleSteps = ResolveUpsampleSteps(p0, p1, p2, p3);
            int startStep = (i == 0) ? 0 : 1;
            for (int step = startStep; step <= upsampleSteps; step++)
            {
                double t = step / (double)upsampleSteps;
                var pos = CatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);

                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);
                double noisePhase = CatmullRomValue(p0.NoisePhase, p1.NoisePhase, p2.NoisePhase, p3.NoisePhase, t);
                double wetness = CatmullRomValue(p0.Wetness, p1.Wetness, p2.Wetness, p3.Wetness, t);
                double nibAngle = LerpAngle(p1.NibAngleRadians, p2.NibAngleRadians, t);
                double nibStrength = Lerp(p1.NibStrength, p2.NibStrength, t);

                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }
                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);

                double normalizedSpeed = Math.Clamp((speed - _minVelocity) / velocityRange, 0, 1);
                double width = CalculateWidthV10(p1.Width, normalizedSpeed, progress);

                samples.Add(new StrokePoint(
                    pos,
                    width,
                    speed,
                    normalizedSpeed,
                    progress,
                    accumulatedWidth,
                    noisePhase,
                    Math.Clamp(wetness, 0.0, 1.0),
                    nibAngle,
                    Math.Clamp(nibStrength, 0.2, 2.0)));
            }
        }

        SmoothWidthsV10(samples);

        return samples;
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

    private double CalculateWidthV10(double baseWidth, double normalizedSpeed, double progress)
    {
        double velocityFactor = 1.0 - (_config.VelocityWidthFactor * normalizedSpeed);
        velocityFactor = Math.Clamp(velocityFactor, 0.2, 1.0);

        double blendedVelocityFactor = velocityFactor;

        if (progress > _config.EndVelocityDecoupleStart)
        {
            double taperZoneProgress = Math.Clamp(
                (progress - _config.EndVelocityDecoupleStart) / (1.0 - _config.EndVelocityDecoupleStart),
                0.0, 1.0
            );

            blendedVelocityFactor = Lerp(velocityFactor, 1.0, taperZoneProgress);
        }

        double targetWidth = baseWidth * blendedVelocityFactor;

        if (progress > _config.EndTaperStartProgress)
        {
            double taperProgress = Math.Clamp(
                (progress - _config.EndTaperStartProgress) / (1.0 - _config.EndTaperStartProgress),
                0.0, 1.0
            );

            double taperCurve = 1.0 - (taperProgress * taperProgress);
            double taperFactor = _config.TaperMinWidthFactor + (1.0 - _config.TaperMinWidthFactor) * taperCurve;
            if (progress > 0.85)
            {
                double tailT = Math.Clamp((progress - 0.85) / 0.15, 0.0, 1.0);
                taperFactor *= Lerp(1.0, 0.84, tailT);
            }
            targetWidth *= taperFactor;

            double minWidth = _baseSize * _config.TaperMinWidthFactor;
            targetWidth = Math.Max(targetWidth, minWidth);
        }

        return ClampWidth(targetWidth);
    }

    private void SmoothWidthsV10(List<StrokePoint> samples)
    {
        if (samples.Count < 2) return;

        for (int i = 1; i < samples.Count; i++)
        {
            double smoothed = samples[i - 1].Width * 0.65 + samples[i].Width * 0.35;
            samples[i] = new StrokePoint(
                samples[i].Position,
                ClampWidth(smoothed),
                samples[i].Speed,
                samples[i].NormalizedSpeed,
                samples[i].Progress,
                samples[i].AccumulatedWidth,
                samples[i].NoisePhase,
                samples[i].Wetness,
                samples[i].NibAngleRadians,
                samples[i].NibStrength
            );
        }
    }
}
