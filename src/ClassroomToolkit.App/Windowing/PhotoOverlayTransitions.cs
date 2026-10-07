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

        if (PhotoOverlayPolicies.ShouldSyncOwners(transitionPlan.TouchPhotoFullscreenSurface))
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
            SyncedOwners: PhotoOverlayPolicies.ShouldSyncOwners(transitionPlan.TouchPhotoFullscreenSurface),
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

internal readonly record struct PhotoOverlayEntryPlan(
    bool UpdateSequence,
    bool UpdateInkVisibility,
    bool SuppressNextOverlayActivatedApply,
    bool EnterPhotoMode,
    bool TouchPhotoSurface,
    bool FocusOverlay);

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

internal static class PhotoOverlayTransitionsPolicies
{
    internal static PhotoCloseTransitionPlan ResolvePhotoCloseTransition(PhotoCloseTransitionContext context)
    {
        return ResolvePhotoCloseTransition(context.OverlayVisible);
    }

    internal static PhotoCloseTransitionPlan ResolvePhotoCloseTransition(bool overlayVisible)
    {
        return new PhotoCloseTransitionPlan(
            SyncFloatingOwnersVisible: false,
            RequestZOrderApply: overlayVisible,
            ForceEnforceZOrder: overlayVisible);
    }

    internal static bool ShouldDetachOwners(bool syncFloatingOwnersVisible)
    {
        return !syncFloatingOwnersVisible;
    }

    internal static SurfaceZOrderDecision ResolvePhotoModeSurfaceTransition(
        PhotoModeSurfaceTransitionKind kind,
        PhotoModeSurfaceTransitionContext context)
    {
        return ResolvePhotoModeSurfaceTransition(
            kind,
            context.PhotoModeActive,
            context.RequestZOrderApply,
            context.ForceEnforceZOrder,
            context.OverlayVisible);
    }

    internal static SurfaceZOrderDecision ResolvePhotoModeSurfaceTransition(
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

    internal static PhotoSelectionPreparationPlan ResolvePhotoSelectionPreparation(
        bool imageManagerVisible,
        bool whiteboardActive)
    {
        return new PhotoSelectionPreparationPlan(
            CloseImageManager: imageManagerVisible,
            DisableWhiteboard: whiteboardActive,
            SuppressPresentationForeground: true,
            PresentationForegroundSuppressionMs: PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs);
    }

    internal static PhotoCursorModeFocusRequestDecision ResolvePhotoCursorModeFocusRequest(bool photoModeActive, PaintToolMode mode)
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
        return ResolvePhotoCursorModeFocusRequest(photoModeActive, mode).ShouldRequestFocus;
    }

    internal static PhotoOverlayEntryPlan ResolvePhotoOverlayEntry(bool hasPath)
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

    internal static SurfaceZOrderDecision ResolvePhotoOverlayEntrySurfaceTransition(bool touchPhotoSurface)
    {
        return PhotoOverlayEntrySurfaceDecisionFactory.Resolve(touchPhotoSurface);
    }

    public static PhotoOverlayReentryPlan ResolvePhotoOverlayReentry(
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
