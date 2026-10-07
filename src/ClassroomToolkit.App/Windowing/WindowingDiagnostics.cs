using
ClassroomToolkit.Domain;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowingDiagnosticsPolicies
{
    internal static string FormatFailureMessageDialogShowDiagnostics(string dialogName, string message)
    {
        return $"[DialogShow] failed dialog={dialogName} msg={message}";
    }

    internal static string FormatFailureMessageDispatcherBeginInvokeDiagnostics(string operation, string exceptionType, string message)
    {
        return $"[Dispatcher][BeginInvoke] failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatFailureMessageLifecycleSafeExecutionDiagnostics(string phase, string operation, string exceptionType, string message)
    {
        return $"[LifecycleSafeExecution] failed phase={phase} op={operation} ex={exceptionType} msg={message}";
    }

    internal static bool IsNonFatal(Exception ex) => DomainExceptionFilterPolicy.IsNonFatal(ex);
}
