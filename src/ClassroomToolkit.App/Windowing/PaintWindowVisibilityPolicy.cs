namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PaintWindowShowPlan(
    bool ShowOverlay,
    FloatingOwnerBindingAction ToolbarOwnerAction,
    bool ShowToolbar,
    bool EnsureToolbarVisible,
    bool RestoreToolbarMode,
    bool RestorePresentationFocus);

internal readonly record struct PaintWindowHidePlan(
    bool HideOverlay,
    bool HideToolbar);

internal static class PaintWindowVisibilityPolicy
{
    internal static PaintWindowShowPlan ResolveShow(PaintWindowVisibilityShowContext context)
    {
        return ResolveShow(
            context.OverlayVisible,
            context.ToolbarExists,
            context.ToolbarOwnerAlreadyOverlay);
    }

    internal static PaintWindowShowPlan ResolveShow(
        bool overlayVisible,
        bool toolbarExists,
        bool toolbarOwnerAlreadyOverlay)
    {
        return new PaintWindowShowPlan(
            ShowOverlay: !overlayVisible,
            ToolbarOwnerAction: toolbarExists
                ? FloatingOwnerBindingPolicy.Resolve(
                    new FloatingOwnerBindingContext(
                        OverlayVisible: true,
                        OwnerAlreadyOverlay: toolbarOwnerAlreadyOverlay))
                : FloatingOwnerBindingAction.None,
            ShowToolbar: toolbarExists,
            EnsureToolbarVisible: toolbarExists,
            RestoreToolbarMode: toolbarExists,
            RestorePresentationFocus: true);
    }

    internal static PaintWindowHidePlan ResolveHide(PaintWindowVisibilityHideContext context)
    {
        return ResolveHide(context.OverlayVisible, context.ToolbarVisible);
    }

    internal static PaintWindowHidePlan ResolveHide(bool overlayVisible, bool toolbarVisible)
    {
        return new PaintWindowHidePlan(
            HideOverlay: overlayVisible,
            HideToolbar: toolbarVisible);
    }
}
