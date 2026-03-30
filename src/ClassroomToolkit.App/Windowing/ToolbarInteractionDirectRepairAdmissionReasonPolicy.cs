namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionDirectRepairAdmissionReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionDirectRepairAdmissionReason reason)
    {
        return reason switch
        {
            ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying => "zorder-applying",
            ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued => "zorder-queued",
            _ => "none"
        };
    }
}
