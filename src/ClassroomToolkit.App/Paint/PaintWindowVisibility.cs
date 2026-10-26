using ClassroomToolkit.App.Windowing;
using
System.Windows;

namespace ClassroomToolkit.App.Paint;

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

internal enum PaintVisibilityTransition
{
    /// <summary>Overlay is hidden: show it, sync owners, request one z-order apply.</summary>
    EnsureOverlayVisible = 0,

    /// <summary>Overlay is visible: capture the toolbar position before hiding.</summary>
    ToggleHidePaint = 1,

    /// <summary>Overlay is hidden: show it and sync owners on toggle.</summary>
    ToggleShowPaint = 2,

    /// <summary>Entering photo teaching: normalise a minimized toolbar and touch the photo surface.</summary>
    EnterPhotoMode = 3,

    /// <summary>Leaving photo teaching: sync owners without touching the photo surface.</summary>
    ExitPhotoMode = 4
}

internal static class PaintWindowVisibilityPolicies
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

    internal static bool ShouldEnsureWindows(
        bool hasOverlayWindow,
        bool hasToolbarWindow)
    {
        return !hasOverlayWindow || !hasToolbarWindow;
    }

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

    internal static PaintVisibilityTransition ResolvePaintToggle(bool overlayVisible)
    {
        return overlayVisible
            ? PaintVisibilityTransition.ToggleHidePaint
            : PaintVisibilityTransition.ToggleShowPaint;
    }

    internal static PaintVisibilityTransition ResolvePhotoModeChange(
        bool photoModeActive,
        WindowState toolbarWindowState)
    {
        return photoModeActive
            ? PaintVisibilityTransition.EnterPhotoMode
            : PaintVisibilityTransition.ExitPhotoMode;
    }

    internal static bool ShouldNormalizeToolbarWindowState(
        PaintVisibilityTransition transition,
        WindowState toolbarWindowState)
    {
        return transition == PaintVisibilityTransition.EnterPhotoMode
            && toolbarWindowState == WindowState.Minimized;
    }
}
