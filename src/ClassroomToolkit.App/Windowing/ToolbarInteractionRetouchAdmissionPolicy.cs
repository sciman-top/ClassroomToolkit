namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchAdmissionReason
{
    None = 0,
    ReentryBlocked = 1
}

internal readonly record struct ToolbarInteractionRetouchAdmissionDecision(
    bool ShouldRequest,
    ToolbarInteractionRetouchAdmissionReason Reason);

internal static class ToolbarInteractionRetouchAdmissionPolicy
{
    internal static ToolbarInteractionRetouchAdmissionDecision Resolve(
        bool zOrderApplying,
        bool applyQueued,
        bool forceEnforceZOrder)
    {
        var reentryDecision = ZOrderApplyReentryPolicy.Resolve(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder);
        return reentryDecision.ShouldAcceptRequest
            ? new ToolbarInteractionRetouchAdmissionDecision(
                ShouldRequest: true,
                Reason: ToolbarInteractionRetouchAdmissionReason.None)
            : new ToolbarInteractionRetouchAdmissionDecision(
                ShouldRequest: false,
                Reason: ToolbarInteractionRetouchAdmissionReason.ReentryBlocked);
    }

    internal static bool ShouldRequest(
        bool zOrderApplying,
        bool applyQueued,
        bool forceEnforceZOrder)
    {
        return Resolve(
            zOrderApplying,
            applyQueued,
            forceEnforceZOrder).ShouldRequest;
    }
}
