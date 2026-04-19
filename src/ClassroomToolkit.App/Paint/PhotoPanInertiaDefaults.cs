namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanInertiaDefaults
{
    internal const int MouseTickIntervalMs = 16;
    internal const double MouseDecelerationDipPerMs2 = 0.0022;
    internal const double MouseStopSpeedDipPerMs = 0.012;
    internal const double MouseFrameElapsedMinMs = 1.0;
    internal const double MouseFrameElapsedMaxMs = 34.0;
    internal const double MouseMaxDurationMs = 1100.0;
    internal const double MouseMaxTranslationPerFrameDip = 150.0;
    internal const double MouseMinReleaseSpeedDipPerMs = 0.06;
    internal const double MouseMaxReleaseSpeedDipPerMs = 4.4;
    internal const double MouseMinVelocitySampleDistanceDip = 0.9;
    internal const double MouseMaxVelocitySampleAgeMs = 140;
    internal const double MouseMinVelocitySampleIntervalMs = 6;
    internal const double MouseVelocitySampleWindowMs = 120;
    internal const double MouseVelocitySampleHistoryMaxAgeMs = 220;
    internal const int MouseVelocitySampleCapacity = 12;
    internal const double MouseVelocityRecentWeightGain = 0.75;
    internal const double TouchMinVelocitySampleDistanceDip = 0.55;
    internal const double TouchMaxVelocitySampleAgeMs = 220;
    internal const double TouchVelocitySampleWindowMs = 170;
    internal const double TouchVelocityRecentWeightGain = 1.0;
    internal const double GestureTranslationDecelerationDipPerMs2 = 0.0034;
    internal const double GestureCrossPageTranslationDecelerationDipPerMs2 = 0.0029;
}
