using System;

namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestAdmissionDecisionFactory
{
    internal static ZOrderRequestAdmissionDecision Reject(
        DateTime lastRequestUtc,
        bool lastForceEnforceZOrder,
        ZOrderRequestAdmissionReason reason = ZOrderRequestAdmissionReason.ReentryBlocked)
    {
        return new ZOrderRequestAdmissionDecision(
            ShouldQueue: false,
            LastRequestUtc: lastRequestUtc,
            LastForceEnforceZOrder: lastForceEnforceZOrder,
            Reason: reason);
    }

    internal static ZOrderRequestAdmissionDecision FromDedup(ZOrderRequestBurstDedupDecision dedup)
    {
        return new ZOrderRequestAdmissionDecision(
            ShouldQueue: dedup.ShouldQueue,
            LastRequestUtc: dedup.LastRequestUtc,
            LastForceEnforceZOrder: dedup.LastForceEnforceZOrder,
            Reason: dedup.Reason);
    }
}
