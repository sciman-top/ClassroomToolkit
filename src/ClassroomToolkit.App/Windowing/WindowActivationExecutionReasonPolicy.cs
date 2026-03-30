namespace ClassroomToolkit.App.Windowing;

internal static class WindowActivationExecutionReasonPolicy
{
    internal static string ResolveTag(WindowActivationExecutionReason reason)
    {
        return reason switch
        {
            WindowActivationExecutionReason.ExecutionNotRequested => "execution-not-requested",
            WindowActivationExecutionReason.TargetMissing => "target-missing",
            WindowActivationExecutionReason.Executed => "executed",
            _ => "none"
        };
    }
}
