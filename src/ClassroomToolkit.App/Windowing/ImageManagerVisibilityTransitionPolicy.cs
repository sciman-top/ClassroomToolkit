using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerVisibilityTransitionPlan(
    bool SyncOwnersToOverlay,
    bool ShowWindow,
    bool NormalizeWindowState,
    bool DetachOwnerBeforeClose,
    bool CloseWindow,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder,
    bool TouchImageManagerSurface);

internal static class ImageManagerVisibilityTransitionPolicy
{
    internal static ImageManagerVisibilityTransitionPlan ResolveOpen(
        ImageManagerVisibilityOpenContext context)
    {
        return ResolveOpen(
            context.OverlayVisible,
            context.ImageManagerVisible,
            context.ImageManagerWindowState);
    }

    internal static ImageManagerVisibilityTransitionPlan ResolveOpen(
        bool overlayVisible,
        bool imageManagerVisible,
        WindowState imageManagerWindowState)
    {
        return ImageManagerVisibilityTransitionPlanFactory.CreateOpen(
            overlayVisible,
            imageManagerVisible,
            imageManagerWindowState);
    }

    internal static ImageManagerVisibilityTransitionPlan ResolveCloseForPhotoSelection(
        ImageManagerVisibilityCloseContext context)
    {
        return ResolveCloseForPhotoSelection(
            context.ImageManagerVisible,
            context.OwnerAlreadyOverlay);
    }

    internal static ImageManagerVisibilityTransitionPlan ResolveCloseForPhotoSelection(
        bool imageManagerVisible,
        bool ownerAlreadyOverlay)
    {
        return ImageManagerVisibilityTransitionPlanFactory.CreateCloseForPhotoSelection(
            imageManagerVisible,
            ownerAlreadyOverlay);
    }
}
