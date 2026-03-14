namespace ClassroomToolkit.App.Paint;

internal static class CalligraphyRenderingDefaults
{
    internal const double SealStrokeWidthFactor = 0.08;
    internal const double DegradeAreaThreshold = 160000.0;
    internal const int DegradeLayerThreshold = 22;
    internal const int MaxRibbonLayersNormal = 18;
    internal const int MaxRibbonLayersDegraded = 8;
    internal const int MaxBloomLayersNormal = 10;
    internal const int MaxBloomLayersDegraded = 4;
    internal const int AdaptiveLevelMax = 2;
    internal const double AdaptiveHighCostMs = 6.4;
    internal const double AdaptiveLowCostMs = 3.8;
    internal const double AdaptiveCostEmaAlpha = 0.2;
    internal const int AdaptiveAreaThresholdStep = 25000;
    internal const int AdaptiveLayerThresholdStep = 4;
}
