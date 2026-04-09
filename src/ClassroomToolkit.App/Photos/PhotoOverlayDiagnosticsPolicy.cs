using System;

namespace ClassroomToolkit.App.Photos;

internal static class PhotoOverlayDiagnosticsPolicy
{
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
}
