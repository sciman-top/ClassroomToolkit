using System;

namespace ClassroomToolkit.App.Paint;

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
