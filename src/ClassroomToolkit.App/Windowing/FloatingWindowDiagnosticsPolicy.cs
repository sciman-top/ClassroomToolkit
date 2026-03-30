namespace ClassroomToolkit.App.Windowing;

internal static class FloatingWindowDiagnosticsPolicy
{
    internal static string FormatExecutionSkipMessage(FloatingWindowExecutionSkipReason reason)
    {
        return
            $"[FloatingWindow][Execution] skip reason={FloatingWindowExecutionSkipReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatActivationSkipMessage(
        string targetName,
        FloatingActivationExecutionReason reason)
    {
        return
            $"[FloatingWindow][Activation] skip target={targetName} reason={FloatingActivationExecutionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatActivationAttemptFailedMessage(string targetName)
    {
        return $"[FloatingWindow][Activation] attempt-failed target={targetName}";
    }
}
