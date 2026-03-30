namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderApplyReentryReasonPolicy
{
    internal static string ResolveTag(ZOrderApplyReentryReason reason)
    {
        return reason switch
        {
            ZOrderApplyReentryReason.NotApplying => "not-applying",
            ZOrderApplyReentryReason.ForcedDuringApplying => "forced-during-applying",
            ZOrderApplyReentryReason.FollowUpSlotAvailable => "follow-up-slot-available",
            ZOrderApplyReentryReason.ApplyingAndQueued => "applying-and-queued",
            _ => "none"
        };
    }
}
