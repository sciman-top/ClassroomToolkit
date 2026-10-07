using
ClassroomToolkit.App.Photos;
using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal enum ImageManagerActivationReason
{
    None = 0,
    NotTopmostTarget = 1,
    AlreadyActive = 2,
    BlockedByToolbar = 3,
    BlockedByRollCall = 4,
    BlockedByLauncher = 5
}

internal readonly record struct ImageManagerActivationDecision(
    bool ShouldActivate,
    ImageManagerActivationReason Reason);

internal static class ImageManagerActivationPolicy
{
    internal static ImageManagerActivationDecision Resolve(
        bool imageManagerTopmost,
        bool imageManagerActive,
        bool toolbarActive,
        bool rollCallActive,
        bool launcherActive)
    {
        if (!imageManagerTopmost)
        {
            return new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: ImageManagerActivationReason.NotTopmostTarget);
        }

        if (imageManagerActive)
        {
            return new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: ImageManagerActivationReason.AlreadyActive);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: false,
                LauncherActive: launcherActive));
        return guardDecision.IsBlocked
            ? new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => ImageManagerActivationReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => ImageManagerActivationReason.BlockedByRollCall,
                    FloatingActivationGuardReason.LauncherActive => ImageManagerActivationReason.BlockedByLauncher,
                    _ => ImageManagerActivationReason.BlockedByToolbar
                })
            : new ImageManagerActivationDecision(
                ShouldActivate: true,
                Reason: ImageManagerActivationReason.None);
    }

    internal static bool ShouldActivate(
        bool imageManagerTopmost,
        bool imageManagerActive,
        bool toolbarActive,
        bool rollCallActive,
        bool launcherActive)
    {
        return Resolve(
            imageManagerTopmost,
            imageManagerActive,
            toolbarActive,
            rollCallActive,
            launcherActive).ShouldActivate;
    }
}

internal readonly record struct ImageManagerStateChangeContext(
    bool ImageManagerExists,
    WindowState ImageManagerWindowState,
    bool OverlayVisible,
    WindowState OverlayWindowState)
{
    internal bool ImageManagerMinimized => ImageManagerWindowState == WindowState.Minimized;
    internal bool OverlayMinimized => OverlayWindowState == WindowState.Minimized;
}

internal readonly record struct ImageManagerStateChangeDecision(
    bool NormalizeOverlayWindowState,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class ImageManagerStateChangePolicy
{
    internal static ImageManagerStateChangeDecision Resolve(ImageManagerStateChangeContext context)
    {
        return Resolve(
            imageManagerExists: context.ImageManagerExists,
            imageManagerMinimized: context.ImageManagerMinimized,
            overlayVisible: context.OverlayVisible,
            overlayMinimized: context.OverlayMinimized);
    }

    internal static ImageManagerStateChangeDecision Resolve(
        bool imageManagerExists,
        bool imageManagerMinimized,
        bool overlayVisible,
        bool overlayMinimized)
    {
        var shouldRecoverOverlay = imageManagerExists
            && imageManagerMinimized
            && overlayVisible
            && overlayMinimized;

        return new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: shouldRecoverOverlay,
            RequestZOrderApply: shouldRecoverOverlay,
            ForceEnforceZOrder: shouldRecoverOverlay);
    }
}

internal static class ImageManagerStateChangeSurfaceDecisionPolicy
{
    internal static SurfaceZOrderDecision Resolve(ImageManagerStateChangeDecision decision)
    {
        return ImageManagerSurfaceDecisionFactory.NoTouch(
            requestZOrderApply: decision.RequestZOrderApply,
            forceEnforceZOrder: decision.ForceEnforceZOrder);
    }
}

internal enum ImageManagerStateChangeNormalizationExecutionKind
{
    None = 0,
    Scheduled = 1,
    ImmediateFallback = 2
}

internal readonly record struct ImageManagerStateChangeTransitionExecutionResult(
    ImageManagerStateChangeNormalizationExecutionKind NormalizationExecution,
    bool AppliedSurfaceDecision);

internal static class ImageManagerStateChangeTransitionCoordinator
{
    internal static ImageManagerStateChangeTransitionExecutionResult Apply(
        ImageManagerStateChangeDecision decision,
        Action applyOverlayNormalization,
        Func<Action, bool> tryScheduleOverlayNormalization,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(applyOverlayNormalization);
        ArgumentNullException.ThrowIfNull(tryScheduleOverlayNormalization);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        if (!decision.NormalizeOverlayWindowState)
        {
            return new ImageManagerStateChangeTransitionExecutionResult(
                ImageManagerStateChangeNormalizationExecutionKind.None,
                AppliedSurfaceDecision: false);
        }

        var scheduled = false;
        scheduled = SafeActionExecutionExecutor.TryExecute(
            () => tryScheduleOverlayNormalization(applyOverlayNormalization),
            fallback: false);

        if (!scheduled)
        {
            _ = SafeActionExecutionExecutor.TryExecute(applyOverlayNormalization);
        }

        var appliedSurfaceDecision = false;
        if (ImageManagerStateChangeSurfaceApplyPolicy.ShouldApply(
                decision.RequestZOrderApply,
                decision.ForceEnforceZOrder))
        {
            appliedSurfaceDecision = SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    applySurfaceDecision(ImageManagerStateChangeSurfaceDecisionPolicy.Resolve(decision));
                    return true;
                },
                fallback: false);
        }

        return new ImageManagerStateChangeTransitionExecutionResult(
            scheduled
                ? ImageManagerStateChangeNormalizationExecutionKind.Scheduled
                : ImageManagerStateChangeNormalizationExecutionKind.ImmediateFallback,
            AppliedSurfaceDecision: appliedSurfaceDecision);
    }
}

internal static class ImageManagerSurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision TouchImageManager(bool forceEnforceZOrder)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.ImageManager,
            RequestZOrderApply: true,
            ForceEnforceZOrder: forceEnforceZOrder);
    }

    internal static SurfaceZOrderDecision NoTouch(bool requestZOrderApply, bool forceEnforceZOrder)
    {
        var decision = ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply);
        return decision with { ForceEnforceZOrder = forceEnforceZOrder };
    }
}

internal enum ImageManagerSurfaceTransitionKind
{
    Open = 0,
    Activated = 1,
    Closed = 2,
    StateChanged = 3
}

internal static class ImageManagerSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        ImageManagerSurfaceTransitionKind kind,
        bool overlayVisible)
    {
        return kind switch
        {
            ImageManagerSurfaceTransitionKind.Open => ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: false),
            ImageManagerSurfaceTransitionKind.Activated => ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerActivated(
                    overlayVisible)),
            ImageManagerSurfaceTransitionKind.Closed => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: true,
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerClosed(
                    overlayVisible)),
            ImageManagerSurfaceTransitionKind.StateChanged => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: overlayVisible,
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerStateChanged(
                    overlayVisible)),
            _ => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: false,
                forceEnforceZOrder: false)
        };
    }
}

internal enum ImageManagerTopmostReason
{
    None = 0,
    ImageManagerHidden = 1,
    FrontSurfaceMismatch = 2
}

internal readonly record struct ImageManagerTopmostDecision(
    bool ShouldApply,
    ImageManagerTopmostReason Reason);

internal static class ImageManagerTopmostPolicy
{
    internal static ImageManagerTopmostDecision Resolve(bool imageManagerVisible, ZOrderSurface frontSurface)
    {
        if (!imageManagerVisible)
        {
            return new ImageManagerTopmostDecision(
                ShouldApply: false,
                Reason: ImageManagerTopmostReason.ImageManagerHidden);
        }

        return frontSurface == ZOrderSurface.ImageManager
            ? new ImageManagerTopmostDecision(
                ShouldApply: true,
                Reason: ImageManagerTopmostReason.None)
            : new ImageManagerTopmostDecision(
                ShouldApply: false,
                Reason: ImageManagerTopmostReason.FrontSurfaceMismatch);
    }

    internal static bool ShouldApply(bool imageManagerVisible, ZOrderSurface frontSurface)
    {
        return Resolve(imageManagerVisible, frontSurface).ShouldApply;
    }
}

internal readonly record struct ImageManagerVisibilityCloseContext(
    bool ImageManagerVisible,
    bool OwnerAlreadyOverlay);

internal readonly record struct ImageManagerVisibilityOpenContext(
    bool OverlayVisible,
    bool ImageManagerVisible,
    WindowState ImageManagerWindowState);

internal static class ImageManagerVisibilitySurfaceDecisionPolicy
{
    internal static SurfaceZOrderDecision ResolveOpen(ImageManagerVisibilityTransitionPlan plan)
    {
        if (plan.TouchImageManagerSurface)
        {
            return ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: plan.ForceEnforceZOrder);
        }

        return ImageManagerSurfaceDecisionFactory.NoTouch(
            requestZOrderApply: plan.RequestZOrderApply,
            forceEnforceZOrder: plan.ForceEnforceZOrder);
    }
}

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
