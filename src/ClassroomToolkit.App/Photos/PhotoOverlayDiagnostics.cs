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

        try
        {
            Directory.CreateDirectory(logsDirectory);
            var logFilePath = Path.Combine(logsDirectory, LatestLogFileName);
            var sessionHeader = PhotoOverlayDiagnosticsPolicy.FormatSessionStartMessage();
            lock (FileWriteLock)
            {
                File.WriteAllText(logFilePath, sessionHeader + Environment.NewLine);
                _logFilePath = logFilePath;
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 诊断目录不可写时自禁用文件落盘，保留 Debug 输出，不影响启动与照片热路径。
            _logFilePath = null;
            Debug.WriteLine($"[PhotoOverlayDiagnostics] initialize failed: {ex.Message}");
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

        try
        {
            lock (FileWriteLock)
            {
                File.AppendAllText(logFilePath, formattedMessage + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // 日志文件被占用（如用户用编辑器打开）时自禁用，避免错误打断照片叠加层热路径。
            _logFilePath = null;
        }
    }
}
