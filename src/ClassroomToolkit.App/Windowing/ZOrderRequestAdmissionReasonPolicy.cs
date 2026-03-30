namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestAdmissionReasonPolicy
{
    internal static string ResolveTag(ZOrderRequestAdmissionReason reason)
    {
        return reason switch
        {
            ZOrderRequestAdmissionReason.ReentryBlocked => "reentry-blocked",
            ZOrderRequestAdmissionReason.DedupSameForceWithinWindow => "dedup-same-force",
            ZOrderRequestAdmissionReason.DedupWeakerAfterForceWithinWindow => "dedup-weaker-after-force",
            ZOrderRequestAdmissionReason.QueuedNoHistory => "queued-no-history",
            ZOrderRequestAdmissionReason.QueuedDedupDisabled => "queued-dedup-disabled",
            ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow => "queued-force-escalation",
            ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow => "queued-outside-window",
            ZOrderRequestAdmissionReason.ReentryApplyingAndQueued => "reentry-applying-and-queued",
            _ => "queued"
        };
    }
}
