namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionDirectRepairDispatchAdmissionReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionDirectRepairDispatchAdmissionReason reason)
    {
        return reason switch
        {
            ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued => "already-queued",
            _ => "none"
        };
    }
}
