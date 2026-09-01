using System.IO;

namespace ClassroomToolkit.App.Settings;

internal readonly record struct UiDefaultsBootstrapOptimizationResult(
    bool ShouldPersist,
    bool InkPathOptimized,
    bool LauncherPositionReset,
    bool PaintToolbarPositionReset);

internal static class UiDefaultsBootstrapOptimizationPolicy
{
    internal const int CurrentVersion = 1;

    private const string LegacyInkPhotoRootPath = @"D:\ClassroomToolkit\Ink\Photos";
    private const int LegacyLauncherPosition = 120;
    private const int LegacyPaintToolbarPosition = 260;

    internal static UiDefaultsBootstrapOptimizationResult Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.UiDefaultsVersion >= CurrentVersion)
        {
            return new UiDefaultsBootstrapOptimizationResult(
                ShouldPersist: false,
                InkPathOptimized: false,
                LauncherPositionReset: false,
                PaintToolbarPositionReset: false);
        }

        var inkPathOptimized = false;
        var launcherPositionReset = false;
        var paintToolbarPositionReset = false;

        if (ShouldOptimizeInkPhotoRootPath(settings.InkPhotoRootPath))
        {
            var optimizedPath = AppSettings.ResolveDefaultInkPhotoRootPath();
            if (!string.Equals(settings.InkPhotoRootPath, optimizedPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.InkPhotoRootPath = optimizedPath;
                inkPathOptimized = true;
            }
        }

        if (settings.LauncherX == LegacyLauncherPosition && settings.LauncherY == LegacyLauncherPosition)
        {
            settings.LauncherX = AppSettings.UnsetPosition;
            settings.LauncherY = AppSettings.UnsetPosition;
            launcherPositionReset = true;
        }

        if (settings.LauncherBubbleX == LegacyLauncherPosition && settings.LauncherBubbleY == LegacyLauncherPosition)
        {
            settings.LauncherBubbleX = AppSettings.UnsetPosition;
            settings.LauncherBubbleY = AppSettings.UnsetPosition;
            launcherPositionReset = true;
        }

        if (settings.PaintToolbarX == LegacyPaintToolbarPosition && settings.PaintToolbarY == LegacyPaintToolbarPosition)
        {
            settings.PaintToolbarX = AppSettings.UnsetPosition;
            settings.PaintToolbarY = AppSettings.UnsetPosition;
            paintToolbarPositionReset = true;
        }

        settings.UiDefaultsVersion = CurrentVersion;
        return new UiDefaultsBootstrapOptimizationResult(
            ShouldPersist: true,
            InkPathOptimized: inkPathOptimized,
            LauncherPositionReset: launcherPositionReset,
            PaintToolbarPositionReset: paintToolbarPositionReset);
    }

    private static bool ShouldOptimizeInkPhotoRootPath(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return true;
        }

        var normalized = currentPath.Trim();
        if (!normalized.Equals(LegacyInkPhotoRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return !Directory.Exists(normalized);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return true;
        }
    }

}
