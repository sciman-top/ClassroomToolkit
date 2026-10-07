using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestBurstThresholds
{
    internal const int RequestDedupMs = 12;
    internal const int InteractiveRequestDedupMs = 56;
}

internal readonly record struct ZOrderRequestRuntimeState(
    DateTime LastRequestUtc,
    bool LastForceEnforceZOrder)
{
    internal static ZOrderRequestRuntimeState Default => new(
        WindowDedupDefaults.UnsetTimestampUtc,
        LastForceEnforceZOrder: false);
}

internal readonly record struct ZOrderRequestBurstDedupDecision(
    bool ShouldQueue,
    bool LastForceEnforceZOrder,
    DateTime LastRequestUtc,
    ZOrderRequestAdmissionReason Reason);

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

internal static class ZOrderApplyGuardStateUpdater
{
    internal static bool TryEnter(ref bool applying)
    {
        if (applying)
        {
            return false;
        }

        applying = true;
        return true;
    }

    internal static void Exit(ref bool applying)
    {
        applying = false;
    }
}

internal enum ZOrderApplyReentryReason
{
    None = 0,
    NotApplying = 1,
    ForcedDuringApplying = 2,
    FollowUpSlotAvailable = 3,
    ApplyingAndQueued = 4
}

internal readonly record struct ZOrderApplyReentryDecision(
    bool ShouldAcceptRequest,
    ZOrderApplyReentryReason Reason);

public enum ZOrderSurface
{
    None,
    PresentationFullscreen,
    PhotoFullscreen,
    Whiteboard,
    ImageManager
}

internal static class ZOrderRequestPolicies
{
    internal static ZOrderRequestBurstDedupDecision ResolveBurstDedup(
        DateTime lastRequestUtc,
        bool lastForceEnforceZOrder,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int minIntervalMs = ZOrderRequestBurstThresholds.RequestDedupMs)
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

    internal static ZOrderRequestAdmissionDecision ResolveAdmission(
        bool zOrderApplying,
        bool applyQueued,
        ZOrderRequestRuntimeState state,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int dedupIntervalMs = ZOrderRequestBurstThresholds.RequestDedupMs)
    {
        return ResolveAdmission(
            zOrderApplying,
            applyQueued,
            state.LastRequestUtc,
            state.LastForceEnforceZOrder,
            nowUtc,
            forceEnforceZOrder,
            dedupIntervalMs);
    }

    internal static ZOrderRequestAdmissionDecision ResolveAdmission(
        bool zOrderApplying,
        bool applyQueued,
        DateTime lastRequestUtc,
        bool lastForceEnforceZOrder,
        DateTime nowUtc,
        bool forceEnforceZOrder,
        int dedupIntervalMs = ZOrderRequestBurstThresholds.RequestDedupMs)
    {
        var reentryDecision = ZOrderRequestPolicies.ResolveZOrderApplyReentry(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder);
        if (!reentryDecision.ShouldAcceptRequest)
        {
            var reason = reentryDecision.Reason == ZOrderApplyReentryReason.ApplyingAndQueued
                ? ZOrderRequestAdmissionReason.ReentryApplyingAndQueued
                : ZOrderRequestAdmissionReason.ReentryBlocked;
            return new ZOrderRequestAdmissionDecision(
                ShouldQueue: false,
                LastRequestUtc: lastRequestUtc,
                LastForceEnforceZOrder: lastForceEnforceZOrder,
                Reason: reason);
        }

        var dedup = ZOrderRequestPolicies.ResolveBurstDedup(
            lastRequestUtc,
            lastForceEnforceZOrder,
            nowUtc,
            forceEnforceZOrder,
            dedupIntervalMs);
        return new ZOrderRequestAdmissionDecision(
            ShouldQueue: dedup.ShouldQueue,
            LastRequestUtc: dedup.LastRequestUtc,
            LastForceEnforceZOrder: dedup.LastForceEnforceZOrder,
            Reason: dedup.Reason);
    }

    internal static ZOrderApplyReentryDecision ResolveZOrderApplyReentry(
        bool zOrderApplying,
        bool applyQueued,
        bool forceEnforceZOrder)
    {
        if (!zOrderApplying)
        {
            return new ZOrderApplyReentryDecision(
                ShouldAcceptRequest: true,
                Reason: ZOrderApplyReentryReason.NotApplying);
        }

        if (forceEnforceZOrder)
        {
            return new ZOrderApplyReentryDecision(
                ShouldAcceptRequest: true,
                Reason: ZOrderApplyReentryReason.ForcedDuringApplying);
        }

        // During an in-flight apply pass, keep at most one queued follow-up request.
        return !applyQueued
            ? new ZOrderApplyReentryDecision(
                ShouldAcceptRequest: true,
                Reason: ZOrderApplyReentryReason.FollowUpSlotAvailable)
            : new ZOrderApplyReentryDecision(
                ShouldAcceptRequest: false,
                Reason: ZOrderApplyReentryReason.ApplyingAndQueued);
    }

    internal static bool ShouldAcceptRequest(
        bool zOrderApplying,
        bool applyQueued,
        bool forceEnforceZOrder)
    {
        return ResolveZOrderApplyReentry(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder).ShouldAcceptRequest;
    }
}
