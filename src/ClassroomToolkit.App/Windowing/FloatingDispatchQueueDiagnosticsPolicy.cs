namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchQueueDiagnosticsPolicy
{
    internal static string FormatRequestDecisionMessage(
        FloatingDispatchQueueAction action,
        FloatingDispatchQueueReason reason,
        bool forceEnforceZOrder)
    {
        return $"[FloatingDispatchQueue] action={action} reason={FloatingDispatchQueueReasonPolicy.ResolveTag(reason)} force={forceEnforceZOrder}";
    }

    internal static string FormatQueueDispatchFailureExceptionMessage(string exceptionType, string message)
    {
        return $"[FloatingDispatchQueue] dispatch-failed ex={exceptionType} msg={message}";
    }
}
