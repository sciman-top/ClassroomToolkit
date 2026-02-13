using System;
using System.Diagnostics;

namespace ClassroomToolkit.App.Photos;

public static class PhotoNavigationDiagnostics
{
    // Enable via env var: CTK_PHOTO_NAV_TRACE=1
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("CTK_PHOTO_NAV_TRACE"),
        "1",
        StringComparison.Ordinal);

    public static bool IsEnabled => Enabled;

    public static void Log(string category, string message)
    {
        if (!Enabled)
        {
            return;
        }

        Debug.WriteLine($"[PhotoNav][{category}] {DateTime.Now:HH:mm:ss.fff} {message}");
    }
}
