namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchAdmissionReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionRetouchAdmissionReason reason)
    {
        return reason switch
        {
            ToolbarInteractionRetouchAdmissionReason.ReentryBlocked => "reentry-blocked",
            _ => "accepted"
        };
    }
}
