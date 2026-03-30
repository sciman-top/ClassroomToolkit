using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ZOrderRequestBurstDedupDecision(
    bool ShouldQueue,
    bool LastForceEnforceZOrder,
    DateTime LastRequestUtc,
    ZOrderRequestAdmissionReason Reason);

internal static class ZOrderRequestBurstDedupPolicy
{
    internal static ZOrderRequestBurstDedupDecision Resolve(
        DateTime lastRequestUtc,
        bool lastForceEnforceZOrder,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int minIntervalMs = ZOrderRequestBurstDedupDefaults.MinIntervalMs)
    {
        if (minIntervalMs <= 0 || lastRequestUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            var reason = minIntervalMs <= 0
                ? ZOrderRequestAdmissionReason.QueuedDedupDisabled
                : ZOrderRequestAdmissionReason.QueuedNoHistory;
            return new ZOrderRequestBurstDedupDecision(
                ShouldQueue: true,
                LastForceEnforceZOrder: forceEnforceZOrder,
                LastRequestUtc: nowUtc,
                Reason: reason);
        }

        var elapsedMs = (nowUtc - lastRequestUtc).TotalMilliseconds;
        var sameForceFlag = forceEnforceZOrder == lastForceEnforceZOrder;
        if (sameForceFlag && elapsedMs < minIntervalMs)
        {
            return new ZOrderRequestBurstDedupDecision(
                ShouldQueue: false,
                LastForceEnforceZOrder: lastForceEnforceZOrder,
                LastRequestUtc: lastRequestUtc,
                Reason: ZOrderRequestAdmissionReason.DedupSameForceWithinWindow);
        }

        // If a stronger force=true request was just accepted, a weaker force=false request
        // within the same burst window is redundant and should be dropped.
        if (elapsedMs < minIntervalMs && lastForceEnforceZOrder && !forceEnforceZOrder)
        {
            return new ZOrderRequestBurstDedupDecision(
                ShouldQueue: false,
                LastForceEnforceZOrder: lastForceEnforceZOrder,
                LastRequestUtc: lastRequestUtc,
                Reason: ZOrderRequestAdmissionReason.DedupWeakerAfterForceWithinWindow);
        }

        var queuedReason = elapsedMs < minIntervalMs
            ? ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow
            : ZOrderRequestAdmissionReason.QueuedOutsideDedupWindow;
        return new ZOrderRequestBurstDedupDecision(
            ShouldQueue: true,
            LastForceEnforceZOrder: forceEnforceZOrder,
            LastRequestUtc: nowUtc,
            Reason: queuedReason);
    }
}
