namespace ClassroomToolkit.App.Paint;

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
