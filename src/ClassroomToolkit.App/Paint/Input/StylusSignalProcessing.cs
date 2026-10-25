using ClassroomToolkit.App.Paint.Brushes;
using System.Collections.Generic;
using System.Windows.Input;
using System;
using WpfPoint
=
System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal sealed class OneEuroFilter
{
    private readonly double _minCutoff;
    private readonly double _beta;
    private readonly double _derivativeCutoff;
    private bool _initialized;
    private double _lastValue;
    private double _lastDerivative;

    public OneEuroFilter(double minCutoff, double beta, double derivativeCutoff = 1.0)
    {
        _minCutoff = Math.Max(0.001, minCutoff);
        _beta = Math.Max(0.0, beta);
        _derivativeCutoff = Math.Max(0.001, derivativeCutoff);
    }

    public void Reset()
    {
        _initialized = false;
        _lastValue = 0;
        _lastDerivative = 0;
    }

    public double Filter(double value, double dtSeconds)
    {
        if (!double.IsFinite(value))
        {
            return _initialized ? _lastValue : 0.0;
        }

        var dt = Math.Clamp(dtSeconds, 1.0 / 480.0, 0.25);
        if (!_initialized)
        {
            _initialized = true;
            _lastValue = value;
            _lastDerivative = 0;
            return value;
        }

        var derivative = (value - _lastValue) / Math.Max(dt, 1e-6);
        var derivativeAlpha = ComputeAlpha(_derivativeCutoff, dt);
        _lastDerivative = Lerp(_lastDerivative, derivative, derivativeAlpha);

        var cutoff = _minCutoff + (_beta * Math.Abs(_lastDerivative));
        var alpha = ComputeAlpha(cutoff, dt);
        _lastValue = Lerp(_lastValue, value, alpha);
        return _lastValue;
    }

    private static double ComputeAlpha(double cutoff, double dtSeconds)
    {
        var tau = 1.0 / (2.0 * Math.PI * Math.Max(cutoff, 0.001));
        return 1.0 / (1.0 + (tau / Math.Max(dtSeconds, 1e-6)));
    }

    private static double Lerp(double from, double to, double t)
    {
        return from + ((to - from) * Math.Clamp(t, 0.0, 1.0));
    }
}

internal sealed class OneEuroPointFilter
{
    private readonly OneEuroFilter _xFilter;
    private readonly OneEuroFilter _yFilter;

    public OneEuroPointFilter(double minCutoff, double beta, double derivativeCutoff = 1.0)
    {
        _xFilter = new OneEuroFilter(minCutoff, beta, derivativeCutoff);
        _yFilter = new OneEuroFilter(minCutoff, beta, derivativeCutoff);
    }

    public void Reset()
    {
        _xFilter.Reset();
        _yFilter.Reset();
    }

    public WpfPoint Filter(WpfPoint value, double dtSeconds)
    {
        return new WpfPoint(
            _xFilter.Filter(value.X, dtSeconds),
            _yFilter.Filter(value.Y, dtSeconds));
    }
}

internal static class StylusAdaptiveProfilingDefaults
{
    internal const int SeedPredictionHorizonMinMs = 4;
    internal const int SeedPredictionHorizonMaxMs = 18;
    internal const double ObserveIntervalMinMs = 0.2;
    internal const double ObserveIntervalMaxMs = 100.0;
    internal const int ObserveIntervalWindowSize = 64;
    internal const int ResolveRateMinSamples = 8;
    internal const double HighSampleRateHzThreshold = 150.0;
    internal const double MediumSampleRateHzThreshold = 90.0;
    internal const int LowRatePredictionHorizonDeltaMs = 4;
    internal const int MediumRatePredictionHorizonDeltaMs = 2;
    internal const int HighRatePredictionHorizonDeltaMs = 1;
    internal const int HighRatePredictionHorizonMinMs = 6;
}

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
            int ms = Math.Clamp(
                predictionHorizonMs.Value,
                StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMinMs,
                StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMaxMs);
            CurrentProfile = CurrentProfile with { PredictionHorizonMs = ms };
        }
    }

    public bool Observe(long timestampTicks, StylusPressureDeviceProfile pressureProfile)
    {
        if (timestampTicks > 0 && _lastTimestamp > 0)
        {
            double dtMs = (timestampTicks - _lastTimestamp) * 1000.0 / Math.Max(System.Diagnostics.Stopwatch.Frequency, 1);
            if (dtMs is > StylusAdaptiveProfilingDefaults.ObserveIntervalMinMs and < StylusAdaptiveProfilingDefaults.ObserveIntervalMaxMs)
            {
                _intervalMs.Enqueue(dtMs);
                while (_intervalMs.Count > StylusAdaptiveProfilingDefaults.ObserveIntervalWindowSize)
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
        if (_intervalMs.Count < StylusAdaptiveProfilingDefaults.ResolveRateMinSamples)
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
        if (hz >= StylusAdaptiveProfilingDefaults.HighSampleRateHzThreshold)
        {
            return StylusSampleRateTier.High;
        }
        if (hz >= StylusAdaptiveProfilingDefaults.MediumSampleRateHzThreshold)
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
                predictionHorizonMs += StylusAdaptiveProfilingDefaults.LowRatePredictionHorizonDeltaMs;
                break;
            case StylusSampleRateTier.Medium:
                markerMove = 0.95;
                markerWidthDelta = 0.03;
                calligraphyPosAlphaScale = 0.97;
                predictionHorizonMs += StylusAdaptiveProfilingDefaults.MediumRatePredictionHorizonDeltaMs;
                break;
            case StylusSampleRateTier.High:
                markerMove = 1.02;
                markerWidthDelta = -0.02;
                calligraphyPosAlphaScale = 1.03;
                predictionHorizonMs = Math.Max(
                    StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonMinMs,
                    predictionHorizonMs - StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonDeltaMs);
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

internal readonly record struct StylusOrientationSample(
    double? AzimuthRadians,
    double? AltitudeRadians,
    double? TiltXRadians,
    double? TiltYRadians)
{
    public bool HasAny => AzimuthRadians.HasValue || (TiltXRadians.HasValue && TiltYRadians.HasValue);
}

internal static class StylusOrientationResolver
{
    public static StylusOrientationSample Resolve(StylusPoint stylusPoint)
    {
        double? azimuth = ResolveCircularRadians(stylusPoint, StylusPointProperties.AzimuthOrientation);
        double? altitude = ResolveLinearRadians(stylusPoint, StylusPointProperties.AltitudeOrientation, Math.PI * 0.5);
        double? tiltX = ResolveSignedRadians(stylusPoint, StylusPointProperties.XTiltOrientation, Math.PI * 0.5);
        double? tiltY = ResolveSignedRadians(stylusPoint, StylusPointProperties.YTiltOrientation, Math.PI * 0.5);

        return new StylusOrientationSample(azimuth, altitude, tiltX, tiltY);
    }

    private static double? ResolveCircularRadians(StylusPoint stylusPoint, StylusPointProperty property)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var range = max - min;
        if (range < 1)
        {
            return null;
        }

        var normalized = Math.Clamp((raw - min) / range, 0.0, 1.0);
        return normalized * Math.PI * 2.0;
    }

    private static double? ResolveLinearRadians(StylusPoint stylusPoint, StylusPointProperty property, double maxRadians)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var range = max - min;
        if (range < 1)
        {
            return null;
        }

        var normalized = Math.Clamp((raw - min) / range, 0.0, 1.0);
        return normalized * maxRadians;
    }

    private static double? ResolveSignedRadians(StylusPoint stylusPoint, StylusPointProperty property, double maxRadians)
    {
        if (!TryGetRawWithRange(stylusPoint, property, out var raw, out var min, out var max))
        {
            return null;
        }

        var halfRange = (max - min) * 0.5;
        if (halfRange < 1)
        {
            return null;
        }

        var mid = min + halfRange;
        var signed = Math.Clamp((raw - mid) / halfRange, -1.0, 1.0);
        return signed * maxRadians;
    }

    private static bool TryGetRawWithRange(
        StylusPoint stylusPoint,
        StylusPointProperty property,
        out double raw,
        out double min,
        out double max)
    {
        raw = 0;
        min = 0;
        max = 0;

        var description = stylusPoint.Description;
        if (!description.HasProperty(property))
        {
            return false;
        }

        // 触控采样热路径：每个样本解析 4 个方向属性，此处不得引入闭包/委托分配
        // （原 TryInvoke(lambda) 每属性一次闭包+委托堆分配）。
        double resolvedRaw;
        double resolvedMin;
        double resolvedMax;
        try
        {
            resolvedRaw = stylusPoint.GetPropertyValue(property);
            var info = description.GetPropertyInfo(property);
            resolvedMin = info.Minimum;
            resolvedMax = info.Maximum;
        }
        catch (Exception ex) when (ClassroomToolkit.App.Windowing.WindowingDiagnosticsPolicies.IsNonFatal(ex))
        {
            return false;
        }

        if (!double.IsFinite(resolvedRaw) || !double.IsFinite(resolvedMin) || !double.IsFinite(resolvedMax))
        {
            return false;
        }

        raw = resolvedRaw;
        min = resolvedMin;
        max = resolvedMax;
        return true;
    }
}

internal static class StylusPressureAnalysisDefaults
{
    internal const int WindowSize = 28;
    internal const int MinSamplesForProfile = 12;
    internal const double EndpointPseudoRatioThreshold = 0.82;
    internal const double LowRangeThreshold = 0.07;
    internal const double ContinuousRangeThreshold = 0.18;
    internal const int EndpointDistinctMax = 3;
    internal const int LowRangeDistinctMax = 4;
    internal const int ContinuousDistinctMin = 7;
    internal const double BucketScale = 100.0;
    internal const double EndpointRatioUpperBoundForContinuous = 0.7;
    internal const double GammaMin = 0.55;
    internal const double GammaMax = 1.8;
}

internal static class StylusPressureCalibrationDefaults
{
    internal const int BinCount = 64;
    internal const int MinSamplesForQuantiles = 20;
    internal const double SeedLowQuantileMax = 0.95;
    internal const double SeedRangeMinWidth = 0.01;
    internal const double NormalizationEpsilon = 1e-5;
    internal const double EmaAlpha = 0.03;
    internal const double LowQuantile = 0.04;
    internal const double HighQuantile = 0.96;
    internal const double MinEffectiveRange = 0.04;
}

internal sealed class StylusPressureCurveCalibrator
{
    private const int BinCount = StylusPressureCalibrationDefaults.BinCount;
    private readonly int[] _hist = new int[BinCount];
    private int _samples;
    private double _emaMin = 1.0;
    private double _emaMax;
    private bool _hasSeedRange;
    private double _seedLow;
    private double _seedHigh = 1.0;

    public void Reset()
    {
        Array.Clear(_hist);
        _samples = 0;
        _emaMin = 1.0;
        _emaMax = 0.0;
        _hasSeedRange = false;
        _seedLow = 0.0;
        _seedHigh = 1.0;
    }

    public void SeedRange(double lowQuantile, double highQuantile)
    {
        var low = Math.Clamp(lowQuantile, 0.0, StylusPressureCalibrationDefaults.SeedLowQuantileMax);
        var high = Math.Clamp(highQuantile, low + StylusPressureCalibrationDefaults.SeedRangeMinWidth, 1.0);
        _seedLow = low;
        _seedHigh = high;
        _hasSeedRange = true;
    }

    public bool TryExportRange(out double lowQuantile, out double highQuantile)
    {
        if (_samples < StylusPressureCalibrationDefaults.MinSamplesForQuantiles && !_hasSeedRange)
        {
            lowQuantile = 0.0;
            highQuantile = 1.0;
            return false;
        }

        if (_samples < StylusPressureCalibrationDefaults.MinSamplesForQuantiles && _hasSeedRange)
        {
            lowQuantile = _seedLow;
            highQuantile = _seedHigh;
            return true;
        }

        lowQuantile = ResolveQuantile(StylusPressureCalibrationDefaults.LowQuantile);
        highQuantile = ResolveQuantile(StylusPressureCalibrationDefaults.HighQuantile);
        return highQuantile - lowQuantile >= StylusPressureCalibrationDefaults.SeedRangeMinWidth;
    }

    public double Calibrate(double pressure, StylusPressureDeviceProfile profile)
    {
        var value = Math.Clamp(pressure, 0.0, 1.0);
        if (profile != StylusPressureDeviceProfile.Continuous)
        {
            return value;
        }

        int index = Math.Clamp((int)Math.Round(value * (BinCount - 1)), 0, BinCount - 1);
        _hist[index]++;
        _samples++;

        _emaMin = Math.Min(
            _emaMin * (1.0 - StylusPressureCalibrationDefaults.EmaAlpha) + value * StylusPressureCalibrationDefaults.EmaAlpha,
            value);
        _emaMax = Math.Max(
            _emaMax * (1.0 - StylusPressureCalibrationDefaults.EmaAlpha) + value * StylusPressureCalibrationDefaults.EmaAlpha,
            value);

        if (_samples < StylusPressureCalibrationDefaults.MinSamplesForQuantiles)
        {
            if (_hasSeedRange)
            {
                var seeded = Math.Clamp(value, _seedLow, _seedHigh);
                var seededNormalized = (seeded - _seedLow)
                    / Math.Max(_seedHigh - _seedLow, StylusPressureCalibrationDefaults.NormalizationEpsilon);
                return Math.Clamp(seededNormalized, 0.0, 1.0);
            }
            return value;
        }

        var lowQ = ResolveQuantile(StylusPressureCalibrationDefaults.LowQuantile);
        var highQ = ResolveQuantile(StylusPressureCalibrationDefaults.HighQuantile);
        if (highQ - lowQ < StylusPressureCalibrationDefaults.MinEffectiveRange)
        {
            lowQ = Math.Min(lowQ, _emaMin);
            highQ = Math.Max(highQ, _emaMax);
        }

        var clamped = Math.Clamp(value, lowQ, highQ);
        var normalized = (clamped - lowQ)
            / Math.Max(highQ - lowQ, StylusPressureCalibrationDefaults.NormalizationEpsilon);
        return Math.Clamp(normalized, 0.0, 1.0);
    }

    private double ResolveQuantile(double q)
    {
        if (_samples <= 0)
        {
            return 0.0;
        }

        int target = (int)Math.Ceiling(_samples * Math.Clamp(q, 0.0, 1.0));
        int cumulative = 0;
        for (int i = 0; i < BinCount; i++)
        {
            cumulative += _hist[i];
            if (cumulative >= target)
            {
                return i / (double)(BinCount - 1);
            }
        }
        return 1.0;
    }
}

internal enum StylusPressureDeviceProfile
{
    Unknown = 0,
    Continuous = 1,
    LowRange = 2,
    EndpointPseudo = 3
}

internal sealed class StylusPressureSignalAnalyzer
{
    private const int WindowSize = StylusPressureAnalysisDefaults.WindowSize;
    private const int MinSamplesForProfile = StylusPressureAnalysisDefaults.MinSamplesForProfile;
    private const double EndpointPseudoRatioThreshold = StylusPressureAnalysisDefaults.EndpointPseudoRatioThreshold;
    private const double LowRangeThreshold = StylusPressureAnalysisDefaults.LowRangeThreshold;
    private const double ContinuousRangeThreshold = StylusPressureAnalysisDefaults.ContinuousRangeThreshold;
    private const int EndpointDistinctMax = StylusPressureAnalysisDefaults.EndpointDistinctMax;
    private const int LowRangeDistinctMax = StylusPressureAnalysisDefaults.LowRangeDistinctMax;
    private const int ContinuousDistinctMin = StylusPressureAnalysisDefaults.ContinuousDistinctMin;

    private readonly Queue<double> _samples = new();
    private readonly Queue<bool> _endpointFlags = new();
    private readonly HashSet<int> _distinctPressureBuckets = new();
    private int _endpointCount;

    public StylusPressureDeviceProfile Profile { get; private set; } = StylusPressureDeviceProfile.Unknown;

    public void Reset()
    {
        _samples.Clear();
        _endpointFlags.Clear();
        _distinctPressureBuckets.Clear();
        _endpointCount = 0;
        Profile = StylusPressureDeviceProfile.Unknown;
    }

    public bool TryResolve(
        double rawPressure,
        double lowThreshold,
        double highThreshold,
        double gamma,
        out double resolvedPressure)
    {
        resolvedPressure = 0.0;
        if (!double.IsFinite(rawPressure))
        {
            return false;
        }

        var clamped = Math.Clamp(rawPressure, 0.0, 1.0);
        var low = Math.Clamp(lowThreshold, 0.0, 0.49);
        var high = Math.Clamp(highThreshold, low + 0.001, 1.0);
        bool endpoint = clamped <= low || clamped >= high;

        PushSample(clamped, endpoint);
        UpdateProfile();

        if (Profile == StylusPressureDeviceProfile.EndpointPseudo || Profile == StylusPressureDeviceProfile.LowRange)
        {
            return false;
        }
        if (endpoint)
        {
            return false;
        }

        resolvedPressure = ApplyGammaCurve(clamped, gamma);
        return true;
    }

    private void PushSample(double pressure, bool endpoint)
    {
        _samples.Enqueue(pressure);
        _endpointFlags.Enqueue(endpoint);
        if (endpoint)
        {
            _endpointCount++;
        }

        while (_samples.Count > WindowSize)
        {
            _samples.Dequeue();
            bool removedEndpoint = _endpointFlags.Dequeue();
            if (removedEndpoint)
            {
                _endpointCount--;
            }
        }
    }

    private void UpdateProfile()
    {
        if (_samples.Count < MinSamplesForProfile)
        {
            Profile = StylusPressureDeviceProfile.Unknown;
            return;
        }

        double min = 1.0;
        double max = 0.0;
        // 每触控样本调用：复用容器避免逐样本 HashSet 堆分配。
        _distinctPressureBuckets.Clear();
        foreach (var value in _samples)
        {
            if (value < min)
            {
                min = value;
            }
            if (value > max)
            {
                max = value;
            }
            _distinctPressureBuckets.Add((int)Math.Round(value * StylusPressureAnalysisDefaults.BucketScale));
        }

        double range = max - min;
        int distinctCount = _distinctPressureBuckets.Count;
        double endpointRatio = _samples.Count == 0 ? 0 : (double)_endpointCount / _samples.Count;

        if (endpointRatio >= EndpointPseudoRatioThreshold && distinctCount <= EndpointDistinctMax)
        {
            Profile = StylusPressureDeviceProfile.EndpointPseudo;
            return;
        }

        if (range <= LowRangeThreshold && distinctCount <= LowRangeDistinctMax)
        {
            Profile = StylusPressureDeviceProfile.LowRange;
            return;
        }

        if (range >= ContinuousRangeThreshold
            && distinctCount >= ContinuousDistinctMin
            && endpointRatio < StylusPressureAnalysisDefaults.EndpointRatioUpperBoundForContinuous)
        {
            Profile = StylusPressureDeviceProfile.Continuous;
            return;
        }

        Profile = StylusPressureDeviceProfile.Unknown;
    }

    private static double ApplyGammaCurve(double pressure, double gamma)
    {
        double g = double.IsFinite(gamma)
            ? Math.Clamp(gamma, StylusPressureAnalysisDefaults.GammaMin, StylusPressureAnalysisDefaults.GammaMax)
            : 1.0;
        return Math.Clamp(Math.Pow(pressure, g), 0.0, 1.0);
    }
}
