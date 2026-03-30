namespace ClassroomToolkit.App.Windowing;

internal static class FloatingWindowExecutionSkipReasonPolicy
{
    internal static string ResolveTag(FloatingWindowExecutionSkipReason reason)
    {
        return reason switch
        {
            FloatingWindowExecutionSkipReason.EnforceZOrder => "enforce-zorder",
            FloatingWindowExecutionSkipReason.ActivationIntent => "activation-intent",
            FloatingWindowExecutionSkipReason.OwnerBindingIntent => "owner-binding-intent",
            FloatingWindowExecutionSkipReason.NoExecutionIntent => "no-execution-intent",
            _ => "execute"
        };
    }
}
