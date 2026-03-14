namespace ClassroomToolkit.App.Windowing;

internal static class WindowActivationDiagnosticsPolicy
{
    internal static string FormatExecutionSkipMessage(WindowActivationExecutionReason reason)
    {
        return $"[WindowActivation] skip reason={WindowActivationExecutionReasonPolicy.ResolveTag(reason)}";
    }
}
