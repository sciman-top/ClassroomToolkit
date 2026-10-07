using System;

namespace ClassroomToolkit.App.Photos;

public sealed record FolderItem(string Path)
{
    public override string ToString()
    {
        return Path;
    }
}

internal static class PhotoOverlayPolicies
{
    internal static bool ShouldFocusOverlay(bool photoModeActive)
    {
        return photoModeActive;
    }

    internal static bool ShouldSyncOwners(bool touchPhotoFullscreenSurface)
    {
        return !touchPhotoFullscreenSurface;
    }

    internal static bool ShouldApply(bool currentEnabled, bool nextEnabled)
    {
        return currentEnabled != nextEnabled;
    }

    internal static string FormatSessionStartMessage()
    {
        var timestamp = PhotoNavigationDiagnosticsTimestampPolicy.Format(DateTime.Now);
        return $"[PhotoOverlay][session-start] {timestamp} trace session initialized";
    }

    internal static string FormatMessage(string eventName, string message)
    {
        var timestamp = PhotoNavigationDiagnosticsTimestampPolicy.Format(DateTime.Now);
        return $"[PhotoOverlay][{eventName}] {timestamp} {message}";
    }

    internal static bool ShouldReuseCache(
        DateTime nowUtc,
        DateTime cachedUtc,
        TimeSpan ttl)
    {
        return nowUtc - cachedUtc < ttl;
    }

    internal static bool ShouldSkipMissProbe(
        DateTime nowUtc,
        DateTime lastMissProbeUtc,
        TimeSpan probeInterval)
    {
        if (lastMissProbeUtc == DateTime.MinValue)
        {
            return false;
        }

        return nowUtc - lastMissProbeUtc < probeInterval;
    }

    internal static bool HasChanged(
        bool unifiedTransformEnabled,
        double currentScaleX,
        double currentScaleY,
        double currentTranslateX,
        double currentTranslateY,
        double nextScaleX,
        double nextScaleY,
        double nextTranslateX,
        double nextTranslateY,
        double epsilon)
    {
        if (!unifiedTransformEnabled)
        {
            return true;
        }

        return !AreClose(currentScaleX, nextScaleX, epsilon)
               || !AreClose(currentScaleY, nextScaleY, epsilon)
               || !AreClose(currentTranslateX, nextTranslateX, epsilon)
               || !AreClose(currentTranslateY, nextTranslateY, epsilon);
    }

    private static bool AreClose(double left, double right, double epsilon)
    {
        return Math.Abs(left - right) < epsilon;
    }
}
