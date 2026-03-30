namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionDirectRepairDispatchAdmissionReason
{
    None = 0,
    AlreadyQueued = 1
}

internal readonly record struct ToolbarInteractionDirectRepairDispatchAdmissionDecision(
    bool ShouldDispatch,
    ToolbarInteractionDirectRepairDispatchAdmissionReason Reason);

internal static class ToolbarInteractionDirectRepairDispatchAdmissionPolicy
{
    internal static ToolbarInteractionDirectRepairDispatchAdmissionDecision Resolve(bool alreadyQueued)
    {
        if (alreadyQueued)
        {
            return new ToolbarInteractionDirectRepairDispatchAdmissionDecision(
                ShouldDispatch: false,
                Reason: ToolbarInteractionDirectRepairDispatchAdmissionReason.AlreadyQueued);
        }

        return new ToolbarInteractionDirectRepairDispatchAdmissionDecision(
            ShouldDispatch: true,
            Reason: ToolbarInteractionDirectRepairDispatchAdmissionReason.None);
    }
}
