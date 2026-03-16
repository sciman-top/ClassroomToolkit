using System.IO;
using System.Windows;

namespace ClassroomToolkit.App.Settings;

internal readonly record struct UiDefaultsBootstrapOptimizationResult(
    bool ShouldPersist,
    bool InkPathOptimized,
    bool LauncherPositionReset,
    bool PaintToolbarPositionReset,
    bool RollCallFontOptimized);

internal static class UiDefaultsBootstrapOptimizationPolicy
{
    private const string LegacyInkPhotoRootPath = @"D:\ClassroomToolkit\Ink\Photos";
    private const int LegacyLauncherPosition = 120;
    private const int LegacyPaintToolbarPosition = 260;
    private const int LegacyRollCallIdFont = 48;
    private const int LegacyRollCallNameFont = 60;
    private const int LegacyRollCallTimerFont = 56;

    internal static UiDefaultsBootstrapOptimizationResult Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.UiDefaultsOptimized)
        {
            return new UiDefaultsBootstrapOptimizationResult(
                ShouldPersist: false,
                InkPathOptimized: false,
                LauncherPositionReset: false,
                PaintToolbarPositionReset: false,
                RollCallFontOptimized: false);
        }

        var inkPathOptimized = false;
        var launcherPositionReset = false;
        var paintToolbarPositionReset = false;
        var rollCallFontOptimized = false;

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

        if (settings.RollCallIdFontSize == LegacyRollCallIdFont
            && settings.RollCallNameFontSize == LegacyRollCallNameFont
            && settings.RollCallTimerFontSize == LegacyRollCallTimerFont)
        {
            var adaptiveFonts = ResolveAdaptiveRollCallFonts(SystemParameters.PrimaryScreenHeight);
            if (settings.RollCallIdFontSize != adaptiveFonts.IdFont
                || settings.RollCallNameFontSize != adaptiveFonts.NameFont
                || settings.RollCallTimerFontSize != adaptiveFonts.TimerFont)
            {
                settings.RollCallIdFontSize = adaptiveFonts.IdFont;
                settings.RollCallNameFontSize = adaptiveFonts.NameFont;
                settings.RollCallTimerFontSize = adaptiveFonts.TimerFont;
                rollCallFontOptimized = true;
            }
        }

        settings.UiDefaultsOptimized = true;
        return new UiDefaultsBootstrapOptimizationResult(
            ShouldPersist: true,
            InkPathOptimized: inkPathOptimized,
            LauncherPositionReset: launcherPositionReset,
            PaintToolbarPositionReset: paintToolbarPositionReset,
            RollCallFontOptimized: rollCallFontOptimized);
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

    private static (int IdFont, int NameFont, int TimerFont) ResolveAdaptiveRollCallFonts(double screenHeight)
    {
        const double referenceHeight = 1080.0;
        var safeHeight = screenHeight <= 0 ? referenceHeight : screenHeight;
        var scale = Math.Clamp(safeHeight / referenceHeight, 0.9, 1.2);

        var idFont = ClampEvenInt(LegacyRollCallIdFont * scale, min: 42, max: 62);
        var nameFont = ClampEvenInt(LegacyRollCallNameFont * scale, min: 52, max: 78);
        var timerFont = ClampEvenInt(LegacyRollCallTimerFont * scale, min: 48, max: 72);
        return (idFont, nameFont, timerFont);
    }

    private static int ClampEvenInt(double value, int min, int max)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if ((rounded & 1) != 0)
        {
            rounded += 1;
        }

        return Math.Clamp(rounded, min, max);
    }
}
