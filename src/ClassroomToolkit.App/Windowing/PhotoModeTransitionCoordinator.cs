using System;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoModeTransitionExecutionResult(
    bool UpdatedImageManagerKeyboardSuppression,
    bool NormalizedToolbarWindowState,
    bool ShowedToolbarWindow,
    bool SyncedOwners,
    bool AppliedSurfaceDecision);

internal static class PhotoModeTransitionCoordinator
{
    internal static PhotoModeTransitionExecutionResult Apply(
        bool active,
        PaintVisibilityTransitionPlan transitionPlan,
        Action<bool> setImageManagerKeyboardNavigationSuppressed,
        Action normalizeToolbarWindowState,
        Action showToolbarWindow,
        Action syncOwners,
        Action applyPhotoModeSurfaceTransition)
    {
        ArgumentNullException.ThrowIfNull(setImageManagerKeyboardNavigationSuppressed);
        ArgumentNullException.ThrowIfNull(normalizeToolbarWindowState);
        ArgumentNullException.ThrowIfNull(showToolbarWindow);
        ArgumentNullException.ThrowIfNull(syncOwners);
        ArgumentNullException.ThrowIfNull(applyPhotoModeSurfaceTransition);

        setImageManagerKeyboardNavigationSuppressed(active);
        normalizeToolbarWindowState();

        if (transitionPlan.ShowToolbar)
        {
            showToolbarWindow();
        }

        if (PhotoModeOwnerSyncPolicy.ShouldSyncOwners(transitionPlan.TouchPhotoFullscreenSurface))
        {
            syncOwners();
        }

        var appliedSurfaceDecision = false;
        if (transitionPlan.RequestZOrderApply)
        {
            applyPhotoModeSurfaceTransition();
            appliedSurfaceDecision = true;
        }

        return new PhotoModeTransitionExecutionResult(
            UpdatedImageManagerKeyboardSuppression: true,
            NormalizedToolbarWindowState: transitionPlan.NormalizeToolbarWindowState,
            ShowedToolbarWindow: transitionPlan.ShowToolbar,
            SyncedOwners: PhotoModeOwnerSyncPolicy.ShouldSyncOwners(transitionPlan.TouchPhotoFullscreenSurface),
            AppliedSurfaceDecision: appliedSurfaceDecision);
    }
}
