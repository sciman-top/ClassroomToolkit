using System;
using System.Diagnostics;
using System.IO;

namespace ClassroomToolkit.App.Photos;

internal static class PhotoOverlayDiagnostics
{
    // Disabled by default; set CTK_PHOTO_OVERLAY_TRACE=1 to enable diagnostics.
    private static readonly object FileWriteLock = new();
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("CTK_PHOTO_OVERLAY_TRACE"),
        "1",
        StringComparison.Ordinal);
    private static string? _logFilePath;

    internal const string LatestLogFileName = "photo-overlay-latest.log";

    internal static bool IsEnabled => Enabled;

    internal static void InitializeSession(string logsDirectory)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(logsDirectory))
        {
            return;
        }

        Directory.CreateDirectory(logsDirectory);
        var logFilePath = Path.Combine(logsDirectory, LatestLogFileName);
        var sessionHeader = PhotoOverlayDiagnosticsPolicy.FormatSessionStartMessage();
        lock (FileWriteLock)
        {
            File.WriteAllText(logFilePath, sessionHeader + Environment.NewLine);
            _logFilePath = logFilePath;
        }
    }

    internal static void Log(string eventName, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var formattedMessage = PhotoOverlayDiagnosticsPolicy.FormatMessage(eventName, message);
        Debug.WriteLine(formattedMessage);
        var logFilePath = _logFilePath;
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return;
        }

        lock (FileWriteLock)
        {
            File.AppendAllText(logFilePath, formattedMessage + Environment.NewLine);
        }
    }
}
