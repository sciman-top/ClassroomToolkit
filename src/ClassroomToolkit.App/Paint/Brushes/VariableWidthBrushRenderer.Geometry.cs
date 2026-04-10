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
    private const int PreviewTailPointWindow = 56;
    private const int PreviewBaseRefreshStride = 14;
    private const int PreviewFastMaxUpsampleSteps = 6;
    private const int PreviewFastMaxResampledPointCount = 1200;
    private const double PreviewFastUpsampleSpacingFactor = 1.28;
    private const double PreviewFastArcLengthStepFactor = 1.35;

    public sealed class RibbonGeometry
    {
        public RibbonGeometry(Geometry geometry, double ribbonT)
        {
            Geometry = geometry;
            RibbonT = ribbonT;
        }

        public Geometry Geometry { get; }
        public double RibbonT { get; }
    }

    public sealed class InkBloomGeometry
    {
        public InkBloomGeometry(Geometry geometry, double opacity)
        {
            Geometry = geometry;
            Opacity = opacity;
        }

        public Geometry Geometry { get; }
        public double Opacity { get; }
    }

    private struct CapData
    {
        public WpfPoint TipPoint;
        public double Width;
        public double PressureDropRate;

        public CapData(WpfPoint tipPoint, double width, double pressureDropRate)
        {
            TipPoint = tipPoint;
            Width = width;
            PressureDropRate = pressureDropRate;
        }
    }

    private List<InkBloomGeometry> BuildInkBloomGeometries()
    {
        var result = new List<InkBloomGeometry>();
        if (!_config.InkBloomEnabled || _points.Count < 2)
        {
            return result;
        }

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        double minSpacing = Math.Max(_baseSize * _config.InkBloomMinSpacingFactor, 1.5);
        WpfPoint? lastPos = null;

        for (int i = 0; i < _points.Count; i++)
        {
            var point = _points[i];
            double normSpeed = Math.Clamp(point.Speed / maxSpeed, 0, 1);
            if (normSpeed > _config.DunBiSpeedThreshold)
            {
                continue;
            }

            if (lastPos.HasValue && (point.Position - lastPos.Value).Length < minSpacing)
            {
                continue;
            }

            var direction = ResolvePointDirection(i);
            var normal = new Vector(-direction.Y, direction.X);
            double normalRadius = Math.Max(point.Width * _config.InkBloomRadiusFactor, _baseSize * 0.25);
            double tangentRadius = normalRadius * _config.InkBloomTangentFactor;
            double accumulation = Math.Clamp(point.AccumulatedWidth / Math.Max(_baseSize, 0.001), 0, 1);
            double wetness = Math.Clamp(point.Wetness, 0.0, 1.0);
            double absorption = Math.Clamp(_config.PaperAbsorption, 0.0, 1.0);
            double spread = 1.0 + (accumulation * (0.25 + wetness * 0.22)) * (1.0 - absorption * 0.28);

            var ellipse = new EllipseGeometry(point.Position, normalRadius * spread, tangentRadius * spread);
            double angle = Math.Atan2(normal.Y, normal.X) * 180.0 / Math.PI;
            ellipse.Transform = new RotateTransform(angle, point.Position.X, point.Position.Y);
            ellipse.Freeze();

            double strength = 1.0 - Math.Clamp(normSpeed / Math.Max(_config.DunBiSpeedThreshold, 0.001), 0, 1);
            double opacity = _config.InkBloomOpacity * strength * Math.Clamp(_lastInkFlow + 0.15, 0.4, 1.0);
            opacity *= 0.78 + wetness * 0.35;
            opacity = Math.Clamp(opacity, 0.05, 0.6);

            result.Add(new InkBloomGeometry(ellipse, opacity));
            lastPos = point.Position;

            if (result.Count >= _config.InkBloomMaxCount)
            {
                break;
            }
        }

        return result;
    }

    private void EnsureGeometryCache()
    {
        if (!_cacheDirty)
        {
            return;
        }

        _cacheDirty = false;
        _cachedRibbons = null;
        _cachedCoreGeometry = null;
        _cachedPreviewGeometry = null;
        _cachedBlooms = null;

        if (_points.Count < 2)
        {
            return;
        }

        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2)
        {
            return;
        }

        _cachedRibbons = BuildRibbonGeometries(samples);
        if (_cachedRibbons != null && _cachedRibbons.Count > 0)
        {
            var group = new GeometryGroup
            {
                FillRule = FillRule.Nonzero
            };
            foreach (var ribbon in _cachedRibbons)
            {
                group.Children.Add(ribbon.Geometry);
            }
            group.Freeze();
            _cachedCoreGeometry = group;
        }
    }

    private void MarkGeometryDirty()
    {
        _cacheDirty = true;
        _cachedRibbons = null;
        _cachedCoreGeometry = null;
        _cachedPreviewGeometry = null;
        _cachedBlooms = null;
        _geometryVersion++;
    }

    private Geometry? BuildRibbonGeometry(List<StrokePoint> samples, double ribbonT, double noiseSeedOffset)
    {
        if (samples.Count < 2) return null;

        var geometry = new StreamGeometry
        {
            FillRule = FillRule.Nonzero
        };

        using (var ctx = geometry.Open())
        {
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();
            BuildRibbonEdgesV10(samples, leftEdge, rightEdge, noiseSeedOffset, ribbonT);

            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            if (leftEdge.Count > 1 && rightEdge.Count > 1)
            {
                BuildStrokePathV10(ctx, leftEdge, rightEdge, samples);
            }
            else
            {
                return null;
            }
        }

        return geometry;
    }

    private List<StrokePoint> BuildRibbonSamples(List<StrokePoint> baseSamples, int ribbonIndex, int ribbonCount)
    {
        var result = new List<StrokePoint>(baseSamples.Count);
        if (baseSamples.Count == 0) return result;

        double centerIndex = (ribbonCount - 1) * 0.5;
        double offsetIndex = ribbonIndex - centerIndex;
        double ribbonT = centerIndex > 0 ? Math.Abs(offsetIndex) / centerIndex : 0;
        double ribbonPhase = _noiseSeed + (ribbonIndex + 1) * 31.7;

        Vector lastNormal = new Vector(0, 1);

        for (int i = 0; i < baseSamples.Count; i++)
        {
            var sample = baseSamples[i];

            Vector dir;
            if (i == 0) dir = baseSamples[i + 1].Position - sample.Position;
            else if (i == baseSamples.Count - 1) dir = sample.Position - baseSamples[i - 1].Position;
            else dir = baseSamples[i + 1].Position - baseSamples[i - 1].Position;

            if (dir.LengthSquared > 0.0001) dir.Normalize();
            var normal = GetNormalFromVector(dir, lastNormal);
            lastNormal = normal;

            double offsetStep = sample.Width * _config.MultiRibbonOffsetFactor;
            double offsetNoise = FractalNoise(ribbonPhase + (i * 0.18), 0.35) * (sample.Width * _config.MultiRibbonOffsetJitter);
            double offset = (offsetIndex * offsetStep) + offsetNoise;

            double widthScale = 1.0 - (ribbonT * _config.MultiRibbonWidthFalloff);
            double widthNoise = FractalNoise(ribbonPhase + (i * 0.12), 0.55) * _config.MultiRibbonWidthJitter;
            double scaledWidth = ClampWidth(sample.Width * Math.Clamp(widthScale + widthNoise, 0.6, 1.2));

            var pos = sample.Position + normal * offset;
            result.Add(new StrokePoint(
                pos,
                scaledWidth,
                sample.Speed,
                sample.NormalizedSpeed,
                sample.Progress,
                sample.AccumulatedWidth,
                sample.NoisePhase,
                sample.Wetness,
                sample.NibAngleRadians,
                sample.NibStrength));
        }

        return result;
    }

    private void BuildRibbonEdgesV10(List<StrokePoint> samples, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, double noiseSeedOffset, double ribbonT)
    {
        if (samples.Count < 2) return;

        Vector lastNormal = new Vector(0, 1);
        double wetFlow = Math.Clamp(_inkWetness, 0.0, 1.0);
        double absorption = Math.Clamp(_config.PaperAbsorption, 0.0, 1.0);
        double dryFactor = Math.Clamp(1.0 - ((_lastInkFlow * 0.72) + (wetFlow * 0.28)), 0, 1);
        double edgeNoiseScale = Lerp(0.35, 1.0, ribbonT) * Lerp(0.75, 1.35, dryFactor);
        double fiberNoiseScale = Lerp(0.45, 1.0, ribbonT) * Lerp(0.85, 1.2, dryFactor);
        double flyingWhiteThreshold = Math.Clamp(_config.FlyingWhiteThreshold - (dryFactor * 0.16), 0.55, 0.95);
        double flyingWhiteIntensity = _config.FlyingWhiteNoiseIntensity * Lerp(0.8, 1.6, dryFactor);

        for (int i = 0; i < samples.Count; i++)
        {
            var pos = samples[i].Position;
            var width = Math.Clamp(
                samples[i].Width,
                Math.Max(0.14, _baseSize * 0.015),
                _baseSize * _config.MaxStrokeWidthMultiplier);
            var speed = samples[i].NormalizedSpeed;
            var progress = samples[i].Progress;
            double widthNorm = Math.Clamp(width / Math.Max(_baseSize, 0.001), 0.0, 3.0);
            double slowNorm = Math.Clamp((0.42 - speed) / 0.42, 0.0, 1.0);
            double wideNorm = Math.Clamp((widthNorm - 1.05) / 0.9, 0.0, 1.0);
            double slowWideAttenuation = Lerp(1.0, 0.58, slowNorm * wideNorm);
            double lowSpeedNoiseScale = Lerp(0.52, 1.0, Math.Clamp(speed / 0.35, 0.0, 1.0));
            double cornerSharpness = 0.0;
            Vector cornerNormal = lastNormal;

            if (i > 0 && i < samples.Count - 1)
            {
                var dirPrevCorner = pos - samples[i - 1].Position;
                var dirNextCorner = samples[i + 1].Position - pos;
                if (dirPrevCorner.LengthSquared > 0.0001 && dirNextCorner.LengthSquared > 0.0001)
                {
                    dirPrevCorner.Normalize();
                    dirNextCorner.Normalize();
                    double cornerAngle = Math.Abs(Vector.AngleBetween(dirPrevCorner, dirNextCorner));
                    if (cornerAngle < CornerAngleThreshold && cornerAngle > CornerMinAngle)
                    {
                        cornerSharpness = Math.Clamp(
                            (CornerAngleThreshold - cornerAngle) / (CornerAngleThreshold - CornerMinAngle),
                            0.0, 1.0);
                        var bisector = dirPrevCorner + dirNextCorner;
                        if (bisector.LengthSquared > 0.0001)
                        {
                            bisector.Normalize();
                            cornerNormal = new Vector(-bisector.Y, bisector.X);
                        }
                    }
                }
            }
            double cornerAttenuation = 1.0 - (cornerSharpness * 0.6);

            double directionNoise = Math.Sin(i * DirectionNoiseFrequency) * width * DirectionNoiseAmplitude;
            directionNoise *= (1.0 - progress) * cornerAttenuation;
            directionNoise *= lowSpeedNoiseScale;
            width = ClampWidth(width + directionNoise);

            Vector dir;
            if (i == 0) dir = samples[i + 1].Position - pos;
            else if (i == samples.Count - 1) dir = pos - samples[i - 1].Position;
            else dir = samples[i + 1].Position - samples[i - 1].Position;

            if (dir.LengthSquared > 0.0001)
                dir.Normalize();

            var normal = GetNormalFromVector(dir, lastNormal);
            if (cornerSharpness > 0 && cornerNormal.LengthSquared > 0.0001)
            {
                double t = cornerSharpness * 0.6;
                normal = new Vector(
                    (normal.X * (1.0 - t)) + (cornerNormal.X * t),
                    (normal.Y * (1.0 - t)) + (cornerNormal.Y * t)
                );
                if (normal.LengthSquared > 0.0001)
                {
                    normal.Normalize();
                }
                if (i > 0)
                {
                    width = ClampWidth(Lerp(width, samples[i - 1].Width, cornerSharpness * 0.35));
                }
            }
            lastNormal = normal;

            double edgeNoise = 0;
            double phase = _noiseSeed + noiseSeedOffset + (samples[i].NoisePhase * 0.9) + (progress * 1.4);

            if (speed > flyingWhiteThreshold && progress < _config.FlyingWhiteNoiseReductionProgress)
            {
                double frequency = _config.FlyingWhiteNoiseFrequency;
                double smoothNoise = FractalNoise(phase, frequency);

                double inkDryBoost = Lerp(1.0, 1.4, dryFactor + absorption * 0.12);
                double noiseAmplitude = _baseSize * flyingWhiteIntensity * speed * inkDryBoost * edgeNoiseScale;
                noiseAmplitude *= slowWideAttenuation;
                noiseAmplitude *= lowSpeedNoiseScale;

                double noiseReduction = 1.0;
                if (progress > _config.FlyingWhiteNoiseReductionProgress)
                {
                    double t = Math.Clamp(
                        (progress - _config.FlyingWhiteNoiseReductionProgress) /
                        (1.0 - _config.FlyingWhiteNoiseReductionProgress), 0, 1);
                    noiseReduction = 1.0 - t;
                }

                edgeNoise = smoothNoise * noiseAmplitude * noiseReduction * cornerAttenuation;

                double maxNoise = width * Lerp(0.05, 0.11, dryFactor);
                edgeNoise = Math.Clamp(edgeNoise, -maxNoise, maxNoise);
            }

            double fiberNoise = FractalNoise(phase, _config.FiberNoiseFrequency);
            double fiberFactor = 0.2 + (speed * 0.8);
            double accumulation = Math.Clamp(samples[i].AccumulatedWidth / (_baseSize * 0.8), 0, 1);
            double wetness = Math.Clamp(samples[i].Wetness, 0.0, 1.0);
            double fiberAttenuation = (1.0 - (accumulation * 0.6)) * (0.86 + absorption * 0.3) * (1.04 - wetness * 0.22);
            double fiberAmplitude = width * _config.FiberNoiseIntensity * fiberFactor * fiberAttenuation * fiberNoiseScale;
            fiberAmplitude *= slowWideAttenuation;
            fiberAmplitude *= lowSpeedNoiseScale;
            edgeNoise += fiberNoise * fiberAmplitude * cornerAttenuation;

            var halfWidth = width * 0.5;
            double nibRadius = ResolveEllipticalNibRadius(
                halfWidth,
                normal,
                samples[i].NibAngleRadians,
                samples[i].NibStrength);
            var leftOffset = nibRadius + edgeNoise;
            var rightOffset = nibRadius - edgeNoise;

            leftOffset = Math.Max(leftOffset, width * 0.1);
            rightOffset = Math.Max(rightOffset, width * 0.1);

            var leftPoint = pos + normal * leftOffset;
            var rightPoint = pos - normal * rightOffset;

            bool handledCorner = false;
            if (i > 0 && i < samples.Count - 1)
            {
                var dirPrev = pos - samples[i - 1].Position;
                var dirNext = samples[i + 1].Position - pos;

                if (dirPrev.LengthSquared > 0.0001 && dirNext.LengthSquared > 0.0001)
                {
                    dirPrev.Normalize();
                    dirNext.Normalize();

                    double angle = Math.Abs(Vector.AngleBetween(dirPrev, dirNext));
                    if (angle < CornerAngleThreshold && angle > CornerMinAngle)
                    {
                        double cross = (dirPrev.X * dirNext.Y) - (dirPrev.Y * dirNext.X);
                        var normalPrev = GetNormalFromVector(dirPrev, normal);
                        var normalNext = GetNormalFromVector(dirNext, normal);

                        bool allowCornerPatch = speed < 0.35 && angle < 80 && angle > 12;

                        if (allowCornerPatch && cross > 0.0001)
                        {
                            AddCornerReinforcement(leftEdge, pos, normalPrev, normalNext, width);
                            AddCornerArc(leftEdge, pos, normalPrev, normalNext, halfWidth, false);
                            rightEdge.Add(rightPoint);
                            handledCorner = true;
                        }
                        else if (allowCornerPatch && cross < -0.0001)
                        {
                            AddCornerReinforcement(rightEdge, pos, -normalPrev, -normalNext, width);
                            AddCornerArc(rightEdge, pos, -normalPrev, -normalNext, halfWidth, true);
                            leftEdge.Add(leftPoint);
                            handledCorner = true;
                        }
                    }
                }
            }

            if (!handledCorner)
            {
                leftEdge.Add(leftPoint);
                rightEdge.Add(rightPoint);
            }
        }
    }

}
