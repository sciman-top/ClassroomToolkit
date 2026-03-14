namespace ClassroomToolkit.App.Settings;

public static class AppFlags
{
    public static bool UseSqliteBusinessStore { get; } = ReadFlag("CTOOLKIT_USE_SQLITE_BUSINESS_STORE", false);
    public static bool EnableExperimentalSqliteBackend { get; } = ReadFlag("CTOOLKIT_ENABLE_EXPERIMENTAL_SQLITE_BACKEND", false);
    public static bool UseGpuInkRenderer { get; } = ReadFlag("CTOOLKIT_USE_GPU_INK_RENDERER", false);
    public static bool UseApplicationUseCases { get; } = ReadFlag("CTOOLKIT_USE_APPLICATION_USECASES", true);
    public static bool UseApplicationPhotoFlow { get; } = ReadFlag("CTOOLKIT_USE_APPLICATION_PHOTO_FLOW", true);
    public static bool UseApplicationPaintFlow { get; } = ReadFlag("CTOOLKIT_USE_APPLICATION_PAINT_FLOW", false);
    public static bool UseApplicationPresentationFlow { get; } = ReadFlag("CTOOLKIT_USE_APPLICATION_PRESENTATION_FLOW", true);

    private static bool ReadFlag(string key, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        raw = raw.Trim();
        return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
