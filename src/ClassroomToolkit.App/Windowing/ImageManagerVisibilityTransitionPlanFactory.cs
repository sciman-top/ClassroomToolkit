using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerVisibilityTransitionPlanFactory
{
    internal static ImageManagerVisibilityTransitionPlan CreateOpen(
        bool overlayVisible,
        bool imageManagerVisible,
        WindowState imageManagerWindowState)
    {
        return new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: overlayVisible,
            ShowWindow: !imageManagerVisible,
            NormalizeWindowState: imageManagerWindowState == WindowState.Minimized,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: overlayVisible,
            TouchImageManagerSurface: true);
    }

    internal static ImageManagerVisibilityTransitionPlan CreateCloseForPhotoSelection(
        bool imageManagerVisible,
        bool ownerAlreadyOverlay)
    {
        return new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: false,
            ShowWindow: false,
            NormalizeWindowState: false,
            DetachOwnerBeforeClose: imageManagerVisible && ownerAlreadyOverlay,
            CloseWindow: imageManagerVisible,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false,
            TouchImageManagerSurface: false);
    }
}
