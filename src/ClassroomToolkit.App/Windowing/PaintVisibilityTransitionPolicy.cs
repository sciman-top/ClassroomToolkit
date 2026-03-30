using System.Windows;

namespace ClassroomToolkit.App.Windowing;

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
