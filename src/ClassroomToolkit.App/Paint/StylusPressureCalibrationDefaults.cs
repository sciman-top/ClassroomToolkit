namespace ClassroomToolkit.App.Paint;

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
