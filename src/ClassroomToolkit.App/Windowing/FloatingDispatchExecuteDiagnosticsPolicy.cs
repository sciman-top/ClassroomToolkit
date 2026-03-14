namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchExecuteDiagnosticsPolicy
{
    internal static string FormatSkipMessage(FloatingDispatchExecuteAdmissionReason reason)
    {
        return $"[FloatingDispatchQueue][Execute] skip reason={FloatingDispatchExecuteAdmissionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatFailureMessage(string exceptionType, string message)
    {
        return $"[FloatingDispatchQueue][Execute] failed ex={exceptionType} msg={message}";
    }
}
