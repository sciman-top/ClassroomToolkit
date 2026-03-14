namespace ClassroomToolkit.App.Windowing;

internal static class DialogShowDiagnosticsPolicy
{
    internal static string FormatFailureMessage(string dialogName, string message)
    {
        return $"[DialogShow] failed dialog={dialogName} msg={message}";
    }
}
