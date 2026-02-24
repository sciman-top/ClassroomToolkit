using System;

namespace ClassroomToolkit.App.Paint;

internal sealed class StylusPressureCurveCalibrator
{
    private const int BinCount = 64;
    private readonly int[] _hist = new int[BinCount];
    private int _samples;
    private double _emaMin = 1.0;
    private double _emaMax = 0.0;
    private bool _hasSeedRange;
    private double _seedLow = 0.0;
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
        var low = Math.Clamp(lowQuantile, 0.0, 0.95);
        var high = Math.Clamp(highQuantile, low + 0.01, 1.0);
        _seedLow = low;
        _seedHigh = high;
        _hasSeedRange = true;
    }

    public bool TryExportRange(out double lowQuantile, out double highQuantile)
    {
        if (_samples < 20 && !_hasSeedRange)
        {
            lowQuantile = 0.0;
            highQuantile = 1.0;
            return false;
        }

        if (_samples < 20 && _hasSeedRange)
        {
            lowQuantile = _seedLow;
            highQuantile = _seedHigh;
            return true;
        }

        lowQuantile = ResolveQuantile(0.04);
        highQuantile = ResolveQuantile(0.96);
        return highQuantile - lowQuantile >= 0.01;
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

        const double emaAlpha = 0.03;
        _emaMin = Math.Min(_emaMin * (1.0 - emaAlpha) + value * emaAlpha, value);
        _emaMax = Math.Max(_emaMax * (1.0 - emaAlpha) + value * emaAlpha, value);

        if (_samples < 20)
        {
            if (_hasSeedRange)
            {
                var seeded = Math.Clamp(value, _seedLow, _seedHigh);
                var seededNormalized = (seeded - _seedLow) / Math.Max(_seedHigh - _seedLow, 1e-5);
                return Math.Clamp(seededNormalized, 0.0, 1.0);
            }
            return value;
        }

        var lowQ = ResolveQuantile(0.04);
        var highQ = ResolveQuantile(0.96);
        if (highQ - lowQ < 0.04)
        {
            lowQ = Math.Min(lowQ, _emaMin);
            highQ = Math.Max(highQ, _emaMax);
        }

        var clamped = Math.Clamp(value, lowQ, highQ);
        var normalized = (clamped - lowQ) / Math.Max(highQ - lowQ, 1e-5);
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
