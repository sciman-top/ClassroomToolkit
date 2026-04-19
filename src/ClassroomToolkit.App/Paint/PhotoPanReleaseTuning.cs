namespace ClassroomToolkit.App.Paint;

internal enum PhotoPanPointerKind
{
    Mouse,
    Stylus,
    Touch
}

internal readonly record struct PhotoPanReleaseTuning(
    double DecelerationDipPerMs2,
    double StopSpeedDipPerMs,
    double MinReleaseSpeedDipPerMs,
    double MaxReleaseSpeedDipPerMs,
    double MaxDurationMs,
    double MaxTranslationPerFrameDip);
