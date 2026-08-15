namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingOwnerExecutionPlan(
    FloatingOwnerBindingAction ToolbarAction,
    FloatingOwnerBindingAction RollCallAction,
    FloatingOwnerBindingAction ImageManagerAction);

internal static class FloatingOwnerExecutionPlanPolicy
{
    internal static FloatingOwnerExecutionPlan Resolve(FloatingOwnerRuntimeSnapshot snapshot)
    {
        return Resolve(
            snapshot.OverlayVisible,
            snapshot.ToolbarOwnerAlreadyOverlay,
            snapshot.RollCallOwnerAlreadyOverlay,
            snapshot.ImageManagerOwnerAlreadyOverlay);
    }

    internal static FloatingOwnerExecutionPlan Resolve(
        bool overlayVisible,
        bool toolbarOwnerAlreadyOverlay,
        bool rollCallOwnerAlreadyOverlay,
        bool imageManagerOwnerAlreadyOverlay)
    {
        return new FloatingOwnerExecutionPlan(
            ToolbarAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                toolbarOwnerAlreadyOverlay),
            RollCallAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                rollCallOwnerAlreadyOverlay),
            ImageManagerAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                imageManagerOwnerAlreadyOverlay));
    }
}
