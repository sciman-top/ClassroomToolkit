namespace ClassroomToolkit.App.Windowing;

internal static class LifecycleSafeExecutionDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string phase, string operation, string exceptionType, string message)
    {
        return $"[LifecycleSafeExecution] failed phase={phase} op={operation} ex={exceptionType} msg={message}";
    }
}
