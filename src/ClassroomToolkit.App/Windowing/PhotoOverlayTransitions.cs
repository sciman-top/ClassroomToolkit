using
ClassroomToolkit.App.Paint;
using
ClassroomToolkit.App.Photos;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoCloseTransitionContext(
    bool OverlayVisible);

internal readonly record struct PhotoCloseTransitionPlan(
    bool SyncFloatingOwnersVisible,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class PhotoCloseTransitionPolicy
{
    internal static PhotoCloseTransitionPlan Resolve(PhotoCloseTransitionContext context)
    {
        return Resolve(context.OverlayVisible);
    }

    internal static PhotoCloseTransitionPlan Resolve(bool overlayVisible)
    {
        return new PhotoCloseTransitionPlan(
            SyncFloatingOwnersVisible: false,
            RequestZOrderApply: overlayVisible,
            ForceEnforceZOrder: overlayVisible);
    }
}

internal static class PhotoCloseOwnerDetachmentPolicy
{
    internal static bool ShouldDetachOwners(bool syncFloatingOwnersVisible)
    {
        return !syncFloatingOwnersVisible;
    }
}

internal readonly record struct PhotoModeSurfaceTransitionContext(
    bool PhotoModeActive,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder,
    bool OverlayVisible);

internal enum PhotoModeSurfaceTransitionKind
{
    PhotoModeChanged = 0,
    PresentationFullscreenDetected = 1
}

internal static class PhotoModeSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        PhotoModeSurfaceTransitionKind kind,
        PhotoModeSurfaceTransitionContext context)
    {
        return Resolve(
            kind,
            context.PhotoModeActive,
            context.RequestZOrderApply,
            context.ForceEnforceZOrder,
            context.OverlayVisible);
    }

    internal static SurfaceZOrderDecision Resolve(
        PhotoModeSurfaceTransitionKind kind,
        bool photoModeActive,
        bool requestZOrderApply,
        bool forceEnforceZOrder,
        bool overlayVisible)
    {
        return kind switch
        {
            PhotoModeSurfaceTransitionKind.PhotoModeChanged => ResolvePhotoModeChanged(
                photoModeActive,
                requestZOrderApply,
                forceEnforceZOrder),
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected => ResolvePresentationFullscreenDetected(
                overlayVisible),
            _ => ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false)
        };
    }

    private static SurfaceZOrderDecision ResolvePhotoModeChanged(
        bool photoModeActive,
        bool requestZOrderApply,
        bool forceEnforceZOrder)
    {
        var baseForce = ForegroundZOrderRetouchPolicy.ShouldForceOnPhotoModeChanged(photoModeActive);

        return new SurfaceZOrderDecision(
            ShouldTouchSurface: requestZOrderApply && photoModeActive,
            Surface: photoModeActive ? ZOrderSurface.PhotoFullscreen : ZOrderSurface.None,
            RequestZOrderApply: requestZOrderApply,
            ForceEnforceZOrder: forceEnforceZOrder || baseForce);
    }

    private static SurfaceZOrderDecision ResolvePresentationFullscreenDetected(bool overlayVisible)
    {
        var noTouch = ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: overlayVisible);
        return noTouch with
        {
            ForceEnforceZOrder = ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(
                overlayVisible)
        };
    }
}

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

internal static class PhotoSelectionPreparationDefaults
{
    internal const int PresentationForegroundSuppressionMs = 800;
}

internal readonly record struct PhotoSelectionPreparationPlan(
    bool CloseImageManager,
    bool DisableWhiteboard,
    bool SuppressPresentationForeground,
    int PresentationForegroundSuppressionMs);

internal static class PhotoSelectionPreparationPolicy
{
    internal static PhotoSelectionPreparationPlan Resolve(
        bool imageManagerVisible,
        bool whiteboardActive)
    {
        return new PhotoSelectionPreparationPlan(
            CloseImageManager: imageManagerVisible,
            DisableWhiteboard: whiteboardActive,
            SuppressPresentationForeground: true,
            PresentationForegroundSuppressionMs: PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs);
    }
}

internal enum PhotoCursorModeFocusRequestReason
{
    None = 0,
    PhotoModeInactive = 1,
    ToolModeNotCursor = 2,
    FocusRequested = 3
}

internal readonly record struct PhotoCursorModeFocusRequestDecision(
    bool ShouldRequestFocus,
    PhotoCursorModeFocusRequestReason Reason);

internal static class PhotoCursorModeFocusRequestPolicy
{
    internal static PhotoCursorModeFocusRequestDecision Resolve(bool photoModeActive, PaintToolMode mode)
    {
        if (!photoModeActive)
        {
            return new PhotoCursorModeFocusRequestDecision(
                ShouldRequestFocus: false,
                Reason: PhotoCursorModeFocusRequestReason.PhotoModeInactive);
        }

        if (mode != PaintToolMode.Cursor)
        {
            return new PhotoCursorModeFocusRequestDecision(
                ShouldRequestFocus: false,
                Reason: PhotoCursorModeFocusRequestReason.ToolModeNotCursor);
        }

        return new PhotoCursorModeFocusRequestDecision(
            ShouldRequestFocus: true,
            Reason: PhotoCursorModeFocusRequestReason.FocusRequested);
    }

    internal static bool ShouldRequestFocus(bool photoModeActive, PaintToolMode mode)
    {
        return Resolve(photoModeActive, mode).ShouldRequestFocus;
    }
}

internal readonly record struct PhotoOverlayEntryPlan(
    bool UpdateSequence,
    bool UpdateInkVisibility,
    bool SuppressNextOverlayActivatedApply,
    bool EnterPhotoMode,
    bool TouchPhotoSurface,
    bool FocusOverlay);

internal static class PhotoOverlayEntryPolicy
{
    internal static PhotoOverlayEntryPlan Resolve(bool hasPath)
    {
        if (!hasPath)
        {
            return new PhotoOverlayEntryPlan(
                UpdateSequence: true,
                UpdateInkVisibility: false,
                SuppressNextOverlayActivatedApply: false,
                EnterPhotoMode: false,
                TouchPhotoSurface: false,
                FocusOverlay: false);
        }

        return new PhotoOverlayEntryPlan(
            UpdateSequence: true,
            UpdateInkVisibility: true,
            SuppressNextOverlayActivatedApply: true,
            EnterPhotoMode: true,
            TouchPhotoSurface: true,
            FocusOverlay: true);
    }
}

internal static class PhotoOverlayEntrySurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(bool touchPhotoSurface)
    {
        return PhotoOverlayEntrySurfaceDecisionFactory.Resolve(touchPhotoSurface);
    }
}

internal static class PhotoOverlayEntrySurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision Resolve(bool touchPhotoSurface)
    {
        if (!touchPhotoSurface)
        {
            return ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false);
        }

        var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.PhotoFullscreen);
        return decision with { RequestZOrderApply = false };
    }
}

internal readonly record struct PhotoOverlayReentryPlan(
    bool NormalizeWindowState,
    bool ActivateOverlay,
    bool ReturnEarly);

internal static class PhotoOverlayReentryPolicy
{
    public static PhotoOverlayReentryPlan Resolve(
        bool windowMinimized,
        bool photoModeActive,
        bool sameSourcePath)
    {
        var returnEarly = photoModeActive && sameSourcePath;
        return new PhotoOverlayReentryPlan(
            NormalizeWindowState: windowMinimized,
            ActivateOverlay: returnEarly,
            ReturnEarly: returnEarly);
    }
}
