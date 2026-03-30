namespace ClassroomToolkit.App.Windowing;

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

internal static class ZOrderApplyReentryPolicy
{
    internal static ZOrderApplyReentryDecision Resolve(
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
        return Resolve(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder).ShouldAcceptRequest;
    }
}
