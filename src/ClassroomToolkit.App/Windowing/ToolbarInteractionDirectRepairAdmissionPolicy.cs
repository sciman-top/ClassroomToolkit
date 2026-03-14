namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionDirectRepairAdmissionReason
{
    None = 0,
    ZOrderApplying = 1,
    ZOrderQueued = 2
}

internal readonly record struct ToolbarInteractionDirectRepairAdmissionDecision(
    bool ShouldApply,
    ToolbarInteractionDirectRepairAdmissionReason Reason);

internal static class ToolbarInteractionDirectRepairAdmissionPolicy
{
    internal static ToolbarInteractionDirectRepairAdmissionDecision Resolve(
        bool zOrderApplying,
        bool zOrderQueued)
    {
        if (zOrderApplying)
        {
            return new ToolbarInteractionDirectRepairAdmissionDecision(
                ShouldApply: false,
                Reason: ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying);
        }

        if (zOrderQueued)
        {
            return new ToolbarInteractionDirectRepairAdmissionDecision(
                ShouldApply: false,
                Reason: ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued);
        }

        return new ToolbarInteractionDirectRepairAdmissionDecision(
            ShouldApply: true,
            Reason: ToolbarInteractionDirectRepairAdmissionReason.None);
    }
}
