namespace ClassroomToolkit.App.Windowing;

internal static class DispatcherBeginInvokeDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[Dispatcher][BeginInvoke] failed op={operation} ex={exceptionType} msg={message}";
    }
}
