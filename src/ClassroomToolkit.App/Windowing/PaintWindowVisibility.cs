using
System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PaintWindowVisibilityHideContext(
    bool OverlayVisible,
    bool ToolbarVisible);

internal readonly record struct PaintWindowVisibilityShowContext(
    bool OverlayVisible,
    bool ToolbarExists,
    bool ToolbarOwnerAlreadyOverlay);

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

internal static class PaintWindowCreationPolicy
{
    internal static bool ShouldEnsureWindows(
        bool hasOverlayWindow,
        bool hasToolbarWindow)
    {
        return !hasOverlayWindow || !hasToolbarWindow;
    }
}

internal static class PaintWindowEnsureSkipPolicy
{
    internal static bool ShouldSkip(
        bool hasOverlayWindow,
        bool hasToolbarWindow,
        bool eventsWired,
        bool shouldWireOverlayLifecycle,
        bool shouldWireToolbarLifecycle)
    {
        return hasOverlayWindow
               && hasToolbarWindow
               && eventsWired
               && !shouldWireOverlayLifecycle
               && !shouldWireToolbarLifecycle;
    }
}

internal readonly record struct PaintVisibilityTransitionPlan(
    bool ShowOverlay,
    bool HideOverlay,
    bool SyncFloatingOwnersVisible,
    bool CaptureToolbarPosition,
    bool NormalizeToolbarWindowState,
    bool ShowToolbar,
    bool TouchPhotoFullscreenSurface,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class PaintVisibilityTransitionPolicy
{
    internal static PaintVisibilityTransitionPlan ResolveEnsureOverlayVisible(bool overlayVisible)
    {
        return new PaintVisibilityTransitionPlan(
            ShowOverlay: !overlayVisible,
            HideOverlay: false,
            SyncFloatingOwnersVisible: true,
            CaptureToolbarPosition: false,
            NormalizeToolbarWindowState: false,
            ShowToolbar: false,
            TouchPhotoFullscreenSurface: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
    }

    internal static PaintVisibilityTransitionPlan ResolvePaintToggle(bool overlayVisible)
    {
        if (overlayVisible)
        {
            return new PaintVisibilityTransitionPlan(
                ShowOverlay: false,
                HideOverlay: true,
                SyncFloatingOwnersVisible: false,
                CaptureToolbarPosition: true,
                NormalizeToolbarWindowState: false,
                ShowToolbar: false,
                TouchPhotoFullscreenSurface: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false);
        }

        return new PaintVisibilityTransitionPlan(
            ShowOverlay: true,
            HideOverlay: false,
            SyncFloatingOwnersVisible: true,
            CaptureToolbarPosition: false,
            NormalizeToolbarWindowState: false,
            ShowToolbar: false,
            TouchPhotoFullscreenSurface: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
    }

    internal static PaintVisibilityTransitionPlan ResolvePhotoModeChange(
        bool photoModeActive,
        WindowState toolbarWindowState)
    {
        if (photoModeActive)
        {
            return new PaintVisibilityTransitionPlan(
                ShowOverlay: false,
                HideOverlay: false,
                SyncFloatingOwnersVisible: false,
                CaptureToolbarPosition: false,
                NormalizeToolbarWindowState: toolbarWindowState == WindowState.Minimized,
                ShowToolbar: true,
                TouchPhotoFullscreenSurface: true,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false);
        }

        return new PaintVisibilityTransitionPlan(
            ShowOverlay: false,
            HideOverlay: false,
            SyncFloatingOwnersVisible: true,
            CaptureToolbarPosition: false,
            NormalizeToolbarWindowState: false,
            ShowToolbar: false,
            TouchPhotoFullscreenSurface: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
    }
}
