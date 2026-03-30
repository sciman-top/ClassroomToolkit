using System;

namespace ClassroomToolkit.App.Windowing;

internal enum ZOrderRequestAdmissionReason
{
    None = 0,
    ReentryBlocked = 1,
    DedupSameForceWithinWindow = 2,
    DedupWeakerAfterForceWithinWindow = 3,
    QueuedNoHistory = 4,
    QueuedDedupDisabled = 5,
    QueuedForceEscalationWithinWindow = 6,
    QueuedOutsideDedupWindow = 7,
    ReentryApplyingAndQueued = 8
}

internal readonly record struct ZOrderRequestAdmissionDecision(
    bool ShouldQueue,
    DateTime LastRequestUtc,
    bool LastForceEnforceZOrder,
    ZOrderRequestAdmissionReason Reason);

internal static class ZOrderRequestAdmissionPolicy
{
    internal static ZOrderRequestAdmissionDecision Resolve(
        bool zOrderApplying,
        bool applyQueued,
        ZOrderRequestRuntimeState state,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int dedupIntervalMs = ZOrderRequestBurstThresholds.RequestDedupMs)
    {
        return Resolve(
            zOrderApplying,
            applyQueued,
            state.LastRequestUtc,
            state.LastForceEnforceZOrder,
            nowUtc,
            forceEnforceZOrder,
            dedupIntervalMs);
    }

    internal static ZOrderRequestAdmissionDecision Resolve(
        bool zOrderApplying,
        bool applyQueued,
        DateTime lastRequestUtc,
        bool lastForceEnforceZOrder,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int dedupIntervalMs = ZOrderRequestBurstThresholds.RequestDedupMs)
    {
        var reentryDecision = ZOrderApplyReentryPolicy.Resolve(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder);
        if (!reentryDecision.ShouldAcceptRequest)
        {
            return ZOrderRequestAdmissionDecisionFactory.Reject(
                lastRequestUtc,
                lastForceEnforceZOrder,
                ZOrderRequestReentryReasonPolicy.ResolveAdmissionReason(reentryDecision.Reason));
        }

        var dedup = ZOrderRequestBurstDedupPolicy.Resolve(
            lastRequestUtc,
            lastForceEnforceZOrder,
            nowUtc,
            forceEnforceZOrder,
            dedupIntervalMs);
        return ZOrderRequestAdmissionDecisionFactory.FromDedup(dedup);
    }
}
