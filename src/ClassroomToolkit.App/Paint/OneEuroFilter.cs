using System;
using WpfPoint = System.Windows.Point;

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
