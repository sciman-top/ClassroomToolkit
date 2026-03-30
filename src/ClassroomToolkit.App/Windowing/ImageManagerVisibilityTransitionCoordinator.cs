using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerVisibilityTransitionExecutionResult(
    bool AppliedOwnerSync,
    bool ShowRequested,
    bool NormalizeRequested,
    bool AppliedSurfaceDecision,
    bool DetachedOwner,
    bool CloseRequested);

internal static class ImageManagerVisibilityTransitionCoordinator
{
    internal static ImageManagerVisibilityTransitionExecutionResult ApplyOpen(
        ImageManagerVisibilityTransitionPlan plan,
        Action syncOwnersToOverlay,
        Action showWindow,
        Action normalizeWindowState,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(syncOwnersToOverlay);
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(normalizeWindowState);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        if (plan.SyncOwnersToOverlay)
        {
            syncOwnersToOverlay();
        }

        if (plan.ShowWindow)
        {
            showWindow();
        }

        normalizeWindowState();

        var appliedSurfaceDecision = false;
        if (ClassroomToolkit.App.Photos.ImageManagerOpenSurfaceApplyPolicy.ShouldApply(
                plan.TouchImageManagerSurface,
                plan.RequestZOrderApply))
        {
            applySurfaceDecision(ImageManagerVisibilitySurfaceDecisionPolicy.ResolveOpen(plan));
            appliedSurfaceDecision = true;
        }

        return new ImageManagerVisibilityTransitionExecutionResult(
            AppliedOwnerSync: plan.SyncOwnersToOverlay,
            ShowRequested: plan.ShowWindow,
            NormalizeRequested: plan.NormalizeWindowState,
            AppliedSurfaceDecision: appliedSurfaceDecision,
            DetachedOwner: false,
            CloseRequested: false);
    }

    internal static ImageManagerVisibilityTransitionExecutionResult ApplyCloseForPhotoSelection(
        ImageManagerVisibilityTransitionPlan plan,
        Action detachOwner,
        Action closeWindow)
    {
        ArgumentNullException.ThrowIfNull(detachOwner);
        ArgumentNullException.ThrowIfNull(closeWindow);

        if (plan.DetachOwnerBeforeClose)
        {
            detachOwner();
        }

        if (plan.CloseWindow)
        {
            closeWindow();
        }

        return new ImageManagerVisibilityTransitionExecutionResult(
            AppliedOwnerSync: false,
            ShowRequested: false,
            NormalizeRequested: false,
            AppliedSurfaceDecision: false,
            DetachedOwner: plan.DetachOwnerBeforeClose,
            CloseRequested: plan.CloseWindow);
    }
}
