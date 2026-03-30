namespace ClassroomToolkit.App.Paint;

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
