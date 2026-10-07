using
ClassroomToolkit.Domain;

namespace ClassroomToolkit.App.Windowing;

internal static class DialogShowDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string dialogName, string message)
    {
        return $"[DialogShow] failed dialog={dialogName} msg={message}";
    }
}

internal static class DispatcherBeginInvokeDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[Dispatcher][BeginInvoke] failed op={operation} ex={exceptionType} msg={message}";
    }
}

internal static class LifecycleSafeExecutionDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string phase, string operation, string exceptionType, string message)
    {
        return $"[LifecycleSafeExecution] failed phase={phase} op={operation} ex={exceptionType} msg={message}";
    }
}

internal static class WindowingExceptionFilterPolicy
{
    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}
