using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Paint;

internal enum StylusPressureDeviceProfile
{
    Unknown = 0,
    Continuous = 1,
    LowRange = 2,
    EndpointPseudo = 3
}

internal sealed class StylusPressureSignalAnalyzer
{
    private const int WindowSize = 28;
    private const int MinSamplesForProfile = 12;
    private const double EndpointPseudoRatioThreshold = 0.82;
    private const double LowRangeThreshold = 0.07;
    private const double ContinuousRangeThreshold = 0.18;
    private const int EndpointDistinctMax = 3;
    private const int LowRangeDistinctMax = 4;
    private const int ContinuousDistinctMin = 7;

    private readonly Queue<double> _samples = new();
    private readonly Queue<bool> _endpointFlags = new();
    private int _endpointCount;

    public StylusPressureDeviceProfile Profile { get; private set; } = StylusPressureDeviceProfile.Unknown;

    public void Reset()
    {
        _samples.Clear();
        _endpointFlags.Clear();
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
        var buckets = new HashSet<int>();
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
            buckets.Add((int)Math.Round(value * 100.0));
        }

        double range = max - min;
        int distinctCount = buckets.Count;
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

        if (range >= ContinuousRangeThreshold && distinctCount >= ContinuousDistinctMin && endpointRatio < 0.7)
        {
            Profile = StylusPressureDeviceProfile.Continuous;
            return;
        }

        Profile = StylusPressureDeviceProfile.Unknown;
    }

    private static double ApplyGammaCurve(double pressure, double gamma)
    {
        double g = double.IsFinite(gamma) ? Math.Clamp(gamma, 0.55, 1.8) : 1.0;
        return Math.Clamp(Math.Pow(pressure, g), 0.0, 1.0);
    }
}
