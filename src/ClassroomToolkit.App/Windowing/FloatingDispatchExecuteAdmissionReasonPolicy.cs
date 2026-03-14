namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchExecuteAdmissionReasonPolicy
{
    internal static string ResolveTag(FloatingDispatchExecuteAdmissionReason reason)
    {
        return reason switch
        {
            FloatingDispatchExecuteAdmissionReason.ApplyQueued => "apply-queued",
            FloatingDispatchExecuteAdmissionReason.NotQueued => "not-queued",
            _ => "none"
        };
    }
}
