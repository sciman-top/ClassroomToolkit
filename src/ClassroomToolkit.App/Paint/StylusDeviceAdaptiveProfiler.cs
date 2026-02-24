using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.App.Paint;

internal enum StylusSampleRateTier
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

internal readonly record struct StylusAdaptiveProfile(
    StylusPressureDeviceProfile PressureProfile,
    StylusSampleRateTier SampleRateTier,
    double MarkerPressureMultiplier,
    double MarkerMoveDistanceMultiplier,
    double MarkerWidthSmoothingDelta,
    double CalligraphyPressureInfluenceMultiplier,
    double CalligraphyPressureScaleMultiplier,
    double CalligraphyPositionAlphaScale,
    int PredictionHorizonMs);

internal sealed class StylusDeviceAdaptiveProfiler
{
    private readonly Queue<double> _intervalMs = new();
    private long _lastTimestamp;
    private StylusPressureDeviceProfile _lastPressureProfile = StylusPressureDeviceProfile.Unknown;
    private StylusSampleRateTier _lastRateTier = StylusSampleRateTier.Unknown;

    public StylusAdaptiveProfile CurrentProfile { get; private set; } = ResolveProfile(
        StylusPressureDeviceProfile.Unknown,
        StylusSampleRateTier.Unknown);

    public void Reset()
    {
        _intervalMs.Clear();
        _lastTimestamp = 0;
        _lastPressureProfile = StylusPressureDeviceProfile.Unknown;
        _lastRateTier = StylusSampleRateTier.Unknown;
        CurrentProfile = ResolveProfile(_lastPressureProfile, _lastRateTier);
    }

    public void Seed(
        StylusPressureDeviceProfile pressureProfile,
        StylusSampleRateTier sampleRateTier,
        int? predictionHorizonMs = null)
    {
        _lastPressureProfile = pressureProfile;
        _lastRateTier = sampleRateTier;
        CurrentProfile = ResolveProfile(_lastPressureProfile, _lastRateTier);
        if (predictionHorizonMs.HasValue)
        {
            int ms = Math.Clamp(predictionHorizonMs.Value, 4, 18);
            CurrentProfile = CurrentProfile with { PredictionHorizonMs = ms };
        }
    }

    public bool Observe(long timestampTicks, StylusPressureDeviceProfile pressureProfile)
    {
        if (timestampTicks > 0 && _lastTimestamp > 0)
        {
            double dtMs = (timestampTicks - _lastTimestamp) * 1000.0 / Math.Max(System.Diagnostics.Stopwatch.Frequency, 1);
            if (dtMs is > 0.2 and < 100.0)
            {
                _intervalMs.Enqueue(dtMs);
                while (_intervalMs.Count > 64)
                {
                    _intervalMs.Dequeue();
                }
            }
        }
        _lastTimestamp = timestampTicks;

        var rateTier = ResolveSampleRateTier();
        if (pressureProfile == _lastPressureProfile && rateTier == _lastRateTier)
        {
            return false;
        }

        _lastPressureProfile = pressureProfile;
        _lastRateTier = rateTier;
        CurrentProfile = ResolveProfile(_lastPressureProfile, _lastRateTier);
        return true;
    }

    public static void ApplyToMarkerConfig(MarkerBrushConfig config, StylusAdaptiveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.PressureWidthFactor = Math.Clamp(config.PressureWidthFactor * profile.MarkerPressureMultiplier, 0.02, 0.55);
        config.MinMoveDistance = Math.Clamp(config.MinMoveDistance * profile.MarkerMoveDistanceMultiplier, 0.2, 1.5);
        config.WidthSmoothing = Math.Clamp(config.WidthSmoothing + profile.MarkerWidthSmoothingDelta, 0.05, 0.75);
    }

    public static void ApplyToCalligraphyConfig(BrushPhysicsConfig config, StylusAdaptiveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.RealPressureWidthInfluence = Math.Clamp(
            config.RealPressureWidthInfluence * profile.CalligraphyPressureInfluenceMultiplier, 0.18, 0.95);
        config.RealPressureWidthScale = Math.Clamp(
            config.RealPressureWidthScale * profile.CalligraphyPressureScaleMultiplier, 0.1, 0.7);
        config.PositionSmoothingMinAlpha = Math.Clamp(config.PositionSmoothingMinAlpha * profile.CalligraphyPositionAlphaScale, 0.18, 0.95);
        config.PositionSmoothingMaxAlpha = Math.Clamp(config.PositionSmoothingMaxAlpha * profile.CalligraphyPositionAlphaScale, 0.28, 0.98);
    }

    private StylusSampleRateTier ResolveSampleRateTier()
    {
        if (_intervalMs.Count < 8)
        {
            return StylusSampleRateTier.Unknown;
        }

        double avgMs = 0;
        foreach (var dt in _intervalMs)
        {
            avgMs += dt;
        }
        avgMs /= _intervalMs.Count;
        if (avgMs <= 0)
        {
            return StylusSampleRateTier.Unknown;
        }

        double hz = 1000.0 / avgMs;
        if (hz >= 150)
        {
            return StylusSampleRateTier.High;
        }
        if (hz >= 90)
        {
            return StylusSampleRateTier.Medium;
        }
        return StylusSampleRateTier.Low;
    }

    private static StylusAdaptiveProfile ResolveProfile(
        StylusPressureDeviceProfile pressureProfile,
        StylusSampleRateTier sampleRateTier)
    {
        double markerPressure = 1.0;
        double markerMove = 1.0;
        double markerWidthDelta = 0.0;
        double calligraphyPressureInfluence = 1.0;
        double calligraphyPressureScale = 1.0;
        double calligraphyPosAlphaScale = 1.0;
        int predictionHorizonMs = 8;

        switch (pressureProfile)
        {
            case StylusPressureDeviceProfile.EndpointPseudo:
            case StylusPressureDeviceProfile.LowRange:
                markerPressure = 0.92;
                calligraphyPressureInfluence = 0.9;
                calligraphyPressureScale = 0.88;
                predictionHorizonMs = 10;
                break;
            case StylusPressureDeviceProfile.Continuous:
                markerPressure = 1.06;
                calligraphyPressureInfluence = 1.08;
                calligraphyPressureScale = 1.06;
                break;
        }

        switch (sampleRateTier)
        {
            case StylusSampleRateTier.Low:
                markerMove = 0.84;
                markerWidthDelta = 0.07;
                calligraphyPosAlphaScale = 0.9;
                predictionHorizonMs += 4;
                break;
            case StylusSampleRateTier.Medium:
                markerMove = 0.95;
                markerWidthDelta = 0.03;
                calligraphyPosAlphaScale = 0.97;
                predictionHorizonMs += 2;
                break;
            case StylusSampleRateTier.High:
                markerMove = 1.02;
                markerWidthDelta = -0.02;
                calligraphyPosAlphaScale = 1.03;
                predictionHorizonMs = Math.Max(6, predictionHorizonMs - 1);
                break;
        }

        return new StylusAdaptiveProfile(
            pressureProfile,
            sampleRateTier,
            markerPressure,
            markerMove,
            markerWidthDelta,
            calligraphyPressureInfluence,
            calligraphyPressureScale,
            calligraphyPosAlphaScale,
            predictionHorizonMs);
    }
}
