namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoPanInertiaTuning(
    double MouseDecelerationDipPerMs2,
    double MouseStopSpeedDipPerMs,
    double MouseMinReleaseSpeedDipPerMs,
    double MouseMaxReleaseSpeedDipPerMs,
    double MouseMaxDurationMs,
    double MouseMaxTranslationPerFrameDip,
    double GestureTranslationDecelerationDipPerMs2,
    double GestureCrossPageTranslationDecelerationDipPerMs2)
{
    internal static PhotoPanInertiaTuning Default => new(
        PhotoPanInertiaDefaults.MouseDecelerationDipPerMs2,
        PhotoPanInertiaDefaults.MouseStopSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMinReleaseSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs,
        PhotoPanInertiaDefaults.MouseMaxDurationMs,
        PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip,
        PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2,
        PhotoPanInertiaDefaults.GestureCrossPageTranslationDecelerationDipPerMs2);
}

internal static class PhotoPanInertiaProfilePolicy
{
    private static readonly PhotoPanInertiaTuning Sensitive = new(
        MouseDecelerationDipPerMs2: 0.0026,
        MouseStopSpeedDipPerMs: 0.013,
        MouseMinReleaseSpeedDipPerMs: 0.045,
        MouseMaxReleaseSpeedDipPerMs: 5.2,
        MouseMaxDurationMs: 980.0,
        MouseMaxTranslationPerFrameDip: 175.0,
        GestureTranslationDecelerationDipPerMs2: 0.0040,
        GestureCrossPageTranslationDecelerationDipPerMs2: 0.0034);

    private static readonly PhotoPanInertiaTuning Heavy = new(
        MouseDecelerationDipPerMs2: 0.0016,
        MouseStopSpeedDipPerMs: 0.009,
        MouseMinReleaseSpeedDipPerMs: 0.05,
        MouseMaxReleaseSpeedDipPerMs: 5.0,
        MouseMaxDurationMs: 1500.0,
        MouseMaxTranslationPerFrameDip: 190.0,
        GestureTranslationDecelerationDipPerMs2: 0.0026,
        GestureCrossPageTranslationDecelerationDipPerMs2: 0.0022);

    internal static PhotoPanInertiaTuning Resolve(string? profile)
    {
        return PhotoInertiaProfileDefaults.Normalize(profile) switch
        {
            PhotoInertiaProfileDefaults.Sensitive => Sensitive,
            PhotoInertiaProfileDefaults.Heavy => Heavy,
            _ => PhotoPanInertiaTuning.Default
        };
    }
}
