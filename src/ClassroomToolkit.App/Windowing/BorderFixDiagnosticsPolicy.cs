namespace ClassroomToolkit.App.Windowing;

internal static class BorderFixDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string phase, string target, string exceptionType, string message)
    {
        return $"[BorderFix] failed phase={phase} target={target} ex={exceptionType} msg={message}";
    }
}
