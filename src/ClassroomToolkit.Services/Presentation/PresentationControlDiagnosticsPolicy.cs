namespace ClassroomToolkit.Services.Presentation;

internal static class PresentationControlDiagnosticsPolicy
{
    internal static string FormatSendFailureMessage(
        string operation,
        PresentationCommand command,
        IntPtr target,
        string exceptionType,
        string message)
    {
        return $"[PresentationControl] send-failed op={operation} cmd={command} target=0x{target.ToInt64():X} ex={exceptionType} msg={message}";
    }
}
