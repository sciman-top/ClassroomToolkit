namespace ClassroomToolkit.App.Windowing;

internal static class FloatingActivationExecutionReasonPolicy
{
    internal static string ResolveTag(FloatingActivationExecutionReason reason)
    {
        return reason switch
        {
            FloatingActivationExecutionReason.TargetMissing => "target-missing",
            FloatingActivationExecutionReason.ActivationNotRequested => "activation-not-requested",
            _ => "none"
        };
    }
}
