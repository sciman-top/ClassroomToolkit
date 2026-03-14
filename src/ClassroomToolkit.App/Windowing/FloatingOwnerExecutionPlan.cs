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
            ToolbarAction: FloatingOwnerExecutionActionResolver.Resolve(
                overlayVisible,
                toolbarOwnerAlreadyOverlay),
            RollCallAction: FloatingOwnerExecutionActionResolver.Resolve(
                overlayVisible,
                rollCallOwnerAlreadyOverlay),
            ImageManagerAction: FloatingOwnerExecutionActionResolver.Resolve(
                overlayVisible,
                imageManagerOwnerAlreadyOverlay));
    }
}
