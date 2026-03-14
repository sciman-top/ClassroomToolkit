namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionDirectRepairDispatchFailurePlan(
    bool ShouldRequestRerun,
    bool ShouldClearQueuedState,
    bool ShouldClearRerunState);

internal static class ToolbarInteractionDirectRepairDispatchFailurePlanPolicy
{
    internal static ToolbarInteractionDirectRepairDispatchFailurePlan ResolveAdmissionRejected()
    {
        return new ToolbarInteractionDirectRepairDispatchFailurePlan(
            ShouldRequestRerun: true,
            ShouldClearQueuedState: false,
            ShouldClearRerunState: false);
    }

    internal static ToolbarInteractionDirectRepairDispatchFailurePlan ResolveMarkQueuedFailed()
    {
        return new ToolbarInteractionDirectRepairDispatchFailurePlan(
            ShouldRequestRerun: true,
            ShouldClearQueuedState: false,
            ShouldClearRerunState: false);
    }

    internal static ToolbarInteractionDirectRepairDispatchFailurePlan ResolveScheduleFailed()
    {
        return new ToolbarInteractionDirectRepairDispatchFailurePlan(
            ShouldRequestRerun: false,
            ShouldClearQueuedState: true,
            ShouldClearRerunState: true);
    }
}
