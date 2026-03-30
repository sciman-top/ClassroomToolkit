namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestReentryReasonPolicy
{
    internal static ZOrderRequestAdmissionReason ResolveAdmissionReason(ZOrderApplyReentryReason reason)
    {
        return reason switch
        {
            ZOrderApplyReentryReason.ApplyingAndQueued => ZOrderRequestAdmissionReason.ReentryApplyingAndQueued,
            _ => ZOrderRequestAdmissionReason.ReentryBlocked
        };
    }
}
