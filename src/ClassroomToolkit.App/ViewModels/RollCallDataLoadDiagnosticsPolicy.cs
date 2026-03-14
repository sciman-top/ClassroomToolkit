namespace ClassroomToolkit.App.ViewModels;

internal static class RollCallDataLoadDiagnosticsPolicy
{
    internal static string FormatFileWriteTimeReadFailure(string path, string exceptionType, string message)
    {
        return $"[RollCallDataLoad] file-write-time-read-failed path={path} ex={exceptionType} msg={message}";
    }

    internal static string FormatPreloadConsumeFailure(string path, string exceptionType, string message)
    {
        return $"[RollCallDataLoad] preload-consume-failed path={path} ex={exceptionType} msg={message}";
    }
}
