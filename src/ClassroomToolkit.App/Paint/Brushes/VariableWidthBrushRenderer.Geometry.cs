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

    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geometry = GenerateGeometry();
        if (geometry != null)
        {
            geometry.Freeze();
        }
        return geometry;
    }

    public IReadOnlyList<RibbonGeometry>? GetLastRibbonGeometries()
    {
        EnsureGeometryCache();
        return _cachedRibbons == null || _cachedRibbons.Count == 0 ? null : _cachedRibbons;
    }

    public Geometry? GetLastCoreGeometry()
    {
        EnsureGeometryCache();
        return _cachedCoreGeometry;
    }

    public IReadOnlyList<InkBloomGeometry>? GetInkBloomGeometries()
    {
        EnsureGeometryCache();
        if (_cachedBlooms == null)
        {
            _cachedBlooms = BuildInkBloomGeometries();
        }
        return _cachedBlooms.Count == 0 ? null : _cachedBlooms;
    }

    public double GetRibbonOpacity(double ribbonT)
    {
        double baseOpacity = Lerp(1.0, 0.38, ribbonT);
        double inkOpacity = Lerp(0.55, 1.0, _lastInkFlow);
        return Math.Clamp(baseOpacity * inkOpacity, 0.18, 1.0);
    }

    private Geometry? GenerateGeometry()
    {
        if (_points.Count < 2) return null;
        var samples = BuildCenterlineSamplesFinal();
        if (samples.Count < 2) return null;

        var geometries = BuildRibbonGeometries(samples);
        if (geometries.Count == 0) return null;
        if (geometries.Count == 1) return geometries[0].Geometry;

        var group = new GeometryGroup
        {
            FillRule = FillRule.Nonzero
        };
        foreach (var item in geometries)
        {
            group.Children.Add(item.Geometry);
        }
        return group;
    }

    private List<RibbonGeometry> BuildRibbonGeometries(List<StrokePoint> samples)
    {
        var result = new List<RibbonGeometry>();
        int ribbonCount = ResolveRibbonCount();
        if (ribbonCount <= 1)
        {
            var single = BuildRibbonGeometry(samples, 0, 0);
            if (single != null) result.Add(new RibbonGeometry(single, 0));
            return result;
        }

        double centerIndex = (ribbonCount - 1) * 0.5;
        for (int i = 0; i < ribbonCount; i++)
        {
            double ribbonT = centerIndex > 0 ? Math.Abs(i - centerIndex) / centerIndex : 0;
            var ribbonSamples = BuildRibbonSamples(samples, i, ribbonCount);
            var ribbonGeometry = BuildRibbonGeometry(ribbonSamples, ribbonT, i * 17.7);
            if (ribbonGeometry != null)
            {
                result.Add(new RibbonGeometry(ribbonGeometry, ribbonT));
            }
        }

        return result;
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
            double spread = 1.0 + (accumulation * 0.35);

            var ellipse = new EllipseGeometry(point.Position, normalRadius * spread, tangentRadius * spread);
            double angle = Math.Atan2(normal.Y, normal.X) * 180.0 / Math.PI;
            ellipse.Transform = new RotateTransform(angle, point.Position.X, point.Position.Y);
            ellipse.Freeze();

            double strength = 1.0 - Math.Clamp(normSpeed / Math.Max(_config.DunBiSpeedThreshold, 0.001), 0, 1);
            double opacity = _config.InkBloomOpacity * strength * Math.Clamp(_lastInkFlow + 0.15, 0.4, 1.0);
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
        _cachedBlooms = null;
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
            result.Add(new StrokePoint(pos, scaledWidth, sample.Speed, sample.NormalizedSpeed, sample.Progress, sample.AccumulatedWidth));
        }

        return result;
    }

    private List<StrokePoint> BuildCenterlineSamplesFinal()
    {
        var samples = new List<StrokePoint>();
        if (_points.Count == 0) return samples;
        if (_points.Count == 1)
        {
            var p = _points[0];
            samples.Add(new StrokePoint(p.Position, ClampWidth(p.Width), 0, 0, 0, p.AccumulatedWidth));
            return samples;
        }

        double totalLength = 0;
        for (int i = 1; i < _points.Count; i++)
        {
            totalLength += (_points[i].Position - _points[i - 1].Position).Length;
        }

        double maxSpeed = Math.Max(_maxVelocity, 0.001);
        double accumulatedLength = 0;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[Math.Max(i - 1, 0)];
            var p1 = _points[i];
            var p2 = _points[i + 1];
            var p3 = _points[Math.Min(i + 2, _points.Count - 1)];

            int startStep = (i == 0) ? 0 : 1;
            for (int step = startStep; step <= UpsampleSteps; step++)
            {
                double t = step / (double)UpsampleSteps;
                var pos = CatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);

                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);
                double width = CatmullRomValue(p0.Width, p1.Width, p2.Width, p3.Width, t);

                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }

                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);
                double normSpeed = Math.Clamp(speed / maxSpeed, 0, 1);

                double targetWidth = ClampWidth(width);
                double currentWidth = targetWidth;
                if (samples.Count > 0)
                {
                    currentWidth = samples[^1].Width * 0.6 + targetWidth * 0.4;
                }

                if (_config.SimulateEndTaper && progress > _config.EndTaperStartProgress)
                {
                    double taperProgress = Math.Clamp(
                        (progress - _config.EndTaperStartProgress) / (1.0 - _config.EndTaperStartProgress),
                        0.0, 1.0);
                    double taperCurve = 1.0 - (taperProgress * taperProgress);
                    double taperFactor = _config.TaperMinWidthFactor + (1.0 - _config.TaperMinWidthFactor) * taperCurve;
                    currentWidth *= taperFactor;
                }

                currentWidth = ClampWidth(currentWidth);
                samples.Add(new StrokePoint(pos, currentWidth, speed, normSpeed, progress, accumulatedWidth));
            }
        }

        return samples;
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

            int startStep = (i == 0) ? 0 : 1;
            for (int step = startStep; step <= UpsampleSteps; step++)
            {
                double t = step / (double)UpsampleSteps;
                var pos = CatmullRomPoint(p0.Position, p1.Position, p2.Position, p3.Position, t);

                double speed = CatmullRomValue(p0.Speed, p1.Speed, p2.Speed, p3.Speed, t);
                double accumulatedWidth = CatmullRomValue(p0.AccumulatedWidth, p1.AccumulatedWidth, p2.AccumulatedWidth, p3.AccumulatedWidth, t);

                if (i > 0 || step > 0)
                {
                    double segmentLength = (pos - (samples.Count > 0 ? samples.Last().Position : p1.Position)).Length;
                    accumulatedLength += segmentLength;
                }
                double progress = totalLength > 0 ? accumulatedLength / totalLength : 0;
                progress = Math.Clamp(progress, 0, 1);

                double normalizedSpeed = Math.Clamp((speed - _minVelocity) / velocityRange, 0, 1);
                double width = CalculateWidthV10(p1.Width, normalizedSpeed, progress);

                samples.Add(new StrokePoint(pos, width, speed, normalizedSpeed, progress, accumulatedWidth));
            }
        }

        SmoothWidthsV10(samples);

        return samples;
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
                samples[i].AccumulatedWidth
            );
        }
    }

    private void BuildRibbonEdgesV10(List<StrokePoint> samples, List<WpfPoint> leftEdge, List<WpfPoint> rightEdge, double noiseSeedOffset, double ribbonT)
    {
        if (samples.Count < 2) return;

        Vector lastNormal = new Vector(0, 1);
        double dryFactor = Math.Clamp(1.0 - _lastInkFlow, 0, 1);
        double edgeNoiseScale = Lerp(0.35, 1.0, ribbonT) * Lerp(0.75, 1.35, dryFactor);
        double fiberNoiseScale = Lerp(0.45, 1.0, ribbonT) * Lerp(0.85, 1.2, dryFactor);
        double flyingWhiteThreshold = Math.Clamp(_config.FlyingWhiteThreshold - (dryFactor * 0.16), 0.55, 0.95);
        double flyingWhiteIntensity = _config.FlyingWhiteNoiseIntensity * Lerp(0.8, 1.6, dryFactor);

        for (int i = 0; i < samples.Count; i++)
        {
            var pos = samples[i].Position;
            var width = ClampWidth(samples[i].Width);
            var speed = samples[i].NormalizedSpeed;
            var progress = samples[i].Progress;
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
            double phase = _noiseSeed + noiseSeedOffset + i * 0.45 + (progress * 3.2) + ((pos.X + pos.Y) * 0.01);

            if (speed > flyingWhiteThreshold && progress < _config.FlyingWhiteNoiseReductionProgress)
            {
                double frequency = _config.FlyingWhiteNoiseFrequency;
                double smoothNoise = FractalNoise(phase, frequency);

                double inkDryBoost = Lerp(1.0, 1.4, dryFactor);
                double noiseAmplitude = _baseSize * flyingWhiteIntensity * speed * inkDryBoost * edgeNoiseScale;

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
            double fiberAttenuation = 1.0 - (accumulation * 0.6);
            double fiberAmplitude = width * _config.FiberNoiseIntensity * fiberFactor * fiberAttenuation * fiberNoiseScale;
            edgeNoise += fiberNoise * fiberAmplitude * cornerAttenuation;

            var halfWidth = width * 0.5;
            var leftOffset = halfWidth + edgeNoise;
            var rightOffset = halfWidth - edgeNoise;

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

        double width = ClampWidth(isEnd ? samples[lastIndex].Width : samples[0].Width);
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

    private double ComputePressureDropRate(List<StrokePoint> samples, bool isEnd)
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
}
