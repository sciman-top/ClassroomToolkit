using System;
using System.Collections.Generic;
using System.Linq;
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
        EnsureImageManagerWindow();
        var imageManagerWindow = _imageManagerWindow;
        if (imageManagerWindow == null)
        {
            return;
        }

        var openContext = new ImageManagerVisibilityOpenContext(
            OverlayVisible: IsOverlayVisibleForWindowing(),
            ImageManagerVisible: imageManagerWindow.IsVisible,
            ImageManagerWindowState: imageManagerWindow.WindowState);
        var openPlan = ImageManagerVisibilityTransitionPolicy.ResolveOpen(openContext);
        ApplyImageManagerOpenTransition(openPlan);
    }

    private void EnsureImageManagerWindow()
    {
        if (_imageManagerWindow != null)
        {
            return;
        }

        _imageManagerWindow = _imageManagerWindowFactory.Create(
            _settings.PhotoFavoriteFolders,
            _settings.PhotoRecentFolders);
        _imageManagerWindow.ApplyLayoutSettings(_settings);
        _imageManagerWindow.ViewModel.ShowInkOverlay = _settings.PhotoShowInkOverlay;
        WireImageManagerWindow(_imageManagerWindow);
    }

    private void WireImageManagerWindow(ImageManagerWindow imageManagerWindow)
    {
        imageManagerWindow.ImageSelected += OnImageSelected;
        imageManagerWindow.FavoritesChanged += OnPhotoFavoritesChanged;
        imageManagerWindow.RecentsChanged += OnPhotoRecentsChanged;
        imageManagerWindow.LeftPanelLayoutChanged += OnImageManagerLeftPanelLayoutChanged;
        imageManagerWindow.LayoutDefaultsRequested += OnImageManagerLayoutDefaultsRequested;
        imageManagerWindow.PhotoTransformDefaultsRequested += OnImageManagerPhotoTransformDefaultsRequested;
        imageManagerWindow.ShowInkOverlayChanged += OnImageManagerShowInkOverlayChanged;
        imageManagerWindow.StateChanged += OnImageManagerStateChanged;
        imageManagerWindow.Activated += OnImageManagerWindowActivated;
        imageManagerWindow.Closed += OnImageManagerWindowClosed;
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
        var decision = ImageManagerStateChangePolicy.Resolve(context);
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

    private void OnImageManagerWindowClosed(object? sender, EventArgs e)
    {
        var closedWindow = _imageManagerWindow;
        if (closedWindow != null)
        {
            CleanupClosedImageManagerWindow(closedWindow);
        }

        _imageManagerWindow = null;
        ApplyImageManagerSurfaceTransition(ImageManagerSurfaceTransitionKind.Closed);
    }

    private void CleanupClosedImageManagerWindow(ImageManagerWindow closedWindow)
    {
        closedWindow.CaptureLayoutSettings(_settings);
        closedWindow.ImageSelected -= OnImageSelected;
        closedWindow.FavoritesChanged -= OnPhotoFavoritesChanged;
        closedWindow.RecentsChanged -= OnPhotoRecentsChanged;
        closedWindow.LeftPanelLayoutChanged -= OnImageManagerLeftPanelLayoutChanged;
        closedWindow.LayoutDefaultsRequested -= OnImageManagerLayoutDefaultsRequested;
        closedWindow.PhotoTransformDefaultsRequested -= OnImageManagerPhotoTransformDefaultsRequested;
        closedWindow.ShowInkOverlayChanged -= OnImageManagerShowInkOverlayChanged;
        closedWindow.StateChanged -= OnImageManagerStateChanged;
        closedWindow.Activated -= OnImageManagerWindowActivated;
        closedWindow.Closed -= OnImageManagerWindowClosed;
        SaveSettings();
    }

    private void OnImageManagerActivated()
    {
        ApplyImageManagerSurfaceTransition(ImageManagerSurfaceTransitionKind.Activated);
    }

    private void OnPhotoFavoritesChanged(IReadOnlyList<string> favorites)
    {
        _settings.PhotoFavoriteFolders = favorites.ToList();
        SaveSettings();
    }

    private void OnPhotoRecentsChanged(IReadOnlyList<string> recents)
    {
        _settings.PhotoRecentFolders = recents.ToList();
        SaveSettings();
    }

    private void OnImageManagerLeftPanelLayoutChanged(double ratio, int width)
    {
        _settings.PhotoManagerLeftPanelRatio = ratio;
        _settings.PhotoManagerLeftPanelWidth = width;
        SaveSettings();
    }

    private void OnImageManagerLayoutDefaultsRequested()
    {
        var defaults = new AppSettings();
        _settings.PhotoManagerWindowWidth = 0;
        _settings.PhotoManagerWindowHeight = 0;
        _settings.PhotoManagerLeftPanelRatio = defaults.PhotoManagerLeftPanelRatio;
        _settings.PhotoManagerLeftPanelWidth = 0;
        _settings.PhotoManagerThumbnailSize = defaults.PhotoManagerThumbnailSize;
        _settings.PhotoManagerListMode = defaults.PhotoManagerListMode;
        SaveSettings();
    }

    private void OnImageManagerPhotoTransformDefaultsRequested()
    {
        var defaults = new AppSettings();
        _settings.PhotoUnifiedTransformEnabled = defaults.PhotoUnifiedTransformEnabled;
        _settings.PhotoUnifiedScaleX = defaults.PhotoUnifiedScaleX;
        _settings.PhotoUnifiedScaleY = defaults.PhotoUnifiedScaleY;
        _settings.PhotoUnifiedTranslateX = defaults.PhotoUnifiedTranslateX;
        _settings.PhotoUnifiedTranslateY = defaults.PhotoUnifiedTranslateY;
        SaveSettings();
        _overlayWindow?.ResetPhotoTransformToDefault();
    }

    private void OnImageManagerShowInkOverlayChanged(bool enabled)
    {
        if (!PhotoShowInkOverlayChangePolicy.ShouldApply(_settings.PhotoShowInkOverlay, enabled))
        {
            return;
        }

        _settings.PhotoShowInkOverlay = enabled;
        SaveSettings();
        _overlayWindow?.UpdateInkShowEnabled(enabled);
    }
}
