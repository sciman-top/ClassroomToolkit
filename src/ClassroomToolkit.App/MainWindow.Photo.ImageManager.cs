using System;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class MainWindow
{
    private void OnOpenPhotoTeaching()
    {
        var imageManagerWindow = _imageManagerWindowCoordinator.EnsureWindow();

        var openContext = new ImageManagerVisibilityOpenContext(
            OverlayVisible: IsOverlayVisibleForWindowing(),
            ImageManagerVisible: imageManagerWindow.IsVisible,
            ImageManagerWindowState: imageManagerWindow.WindowState);
        var openPlan = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilityTransition(openContext);
        ApplyImageManagerOpenTransition(openPlan);
    }

    private void ApplyImageManagerOpenTransition(ImageManagerVisibilityTransitionPlan plan)
    {
        var imageManagerWindow = _imageManagerWindow;
        if (imageManagerWindow == null)
        {
            return;
        }

        ImageManagerVisibilityTransitionCoordinator.ApplyOpen(
            plan,
            () => SyncFloatingWindowOwners(overlayVisible: true),
            () => ExecuteLifecycleSafe("photo-image-manager-open", "show-image-manager-window", imageManagerWindow.Show),
            () => WindowStateNormalizationExecutor.Apply(imageManagerWindow, plan.NormalizeWindowState),
            ApplySurfaceZOrderDecision);
    }

    private void OnImageManagerStateChanged(object? sender, EventArgs e)
    {
        var context = CaptureImageManagerStateChangeContext();
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerStateChange(context);
        ApplyImageManagerStateChangeTransition(decision);
    }

    private ImageManagerStateChangeContext CaptureImageManagerStateChangeContext()
    {
        return new ImageManagerStateChangeContext(
            ImageManagerExists: _imageManagerWindow != null,
            ImageManagerWindowState: _imageManagerWindow?.WindowState ?? WindowState.Normal,
            OverlayVisible: IsOverlayVisibleForWindowing(),
            OverlayWindowState: _overlayWindow?.WindowState ?? WindowState.Normal);
    }

    private void ApplyImageManagerStateChangeTransition(ImageManagerStateChangeDecision decision)
    {
        ImageManagerStateChangeTransitionCoordinator.Apply(
            decision,
            () => WindowStateNormalizationExecutor.Apply(_overlayWindow, decision.NormalizeOverlayWindowState),
            action => TryBeginInvoke(
                action,
                DispatcherPriority.Background,
                "ApplyImageManagerStateChangeTransition.NormalizeOverlay"),
            ApplySurfaceZOrderDecision);
    }

    private void OnImageManagerWindowActivated(object? sender, EventArgs e)
    {
        OnImageManagerActivated();
    }

    private void OnImageManagerActivated()
    {
        ApplyImageManagerSurfaceTransition(ImageManagerSurfaceTransitionKind.Activated);
    }

}
