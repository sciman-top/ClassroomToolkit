using System;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Photos;

internal sealed record ImageManagerWindowCallbacks(
    Action<IReadOnlyList<string>, int> ImageSelected,
    EventHandler StateChanged,
    EventHandler Activated,
    Action Closed,
    Action ResetPhotoTransform,
    Action<bool> PhotoShowInkOverlayChanged);

/// <summary>
/// Owns the ImageManager window instance, its event connections, and its settings lifecycle.
/// MainWindow retains cross-window photo presentation and z-order coordination.
/// </summary>
internal sealed class ImageManagerWindowCoordinator
{
    private readonly IImageManagerWindowFactory _factory;
    private readonly AppSettings _settings;
    private readonly Func<bool> _saveSettings;
    private readonly ImageManagerWindowCallbacks _callbacks;
    private ImageManagerWindow? _window;

    internal ImageManagerWindow? CurrentWindow => _window;

    internal ImageManagerWindowCoordinator(
        IImageManagerWindowFactory factory,
        AppSettings settings,
        Func<bool> saveSettings,
        ImageManagerWindowCallbacks callbacks)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    internal ImageManagerWindow EnsureWindow()
    {
        if (_window != null)
        {
            return _window;
        }

        var window = _factory.Create(_settings.PhotoFavoriteFolders, _settings.PhotoRecentFolders);
        _window = window;
        window.ApplyLayoutSettings(_settings);
        window.ViewModel.ShowInkOverlay = _settings.PhotoShowInkOverlay;
        Subscribe(window);
        return window;
    }

    internal void Close()
    {
        _window?.Close();
    }

    private void Subscribe(ImageManagerWindow window)
    {
        window.ImageSelected += _callbacks.ImageSelected;
        window.FavoritesChanged += OnFavoritesChanged;
        window.RecentsChanged += OnRecentsChanged;
        window.LeftPanelLayoutChanged += OnLeftPanelLayoutChanged;
        window.LayoutDefaultsRequested += OnLayoutDefaultsRequested;
        window.PhotoTransformDefaultsRequested += OnPhotoTransformDefaultsRequested;
        window.ShowInkOverlayChanged += OnShowInkOverlayChanged;
        window.StateChanged += _callbacks.StateChanged;
        window.Activated += _callbacks.Activated;
        window.Closed += OnWindowClosed;
    }

    private void Unsubscribe(ImageManagerWindow window)
    {
        window.ImageSelected -= _callbacks.ImageSelected;
        window.FavoritesChanged -= OnFavoritesChanged;
        window.RecentsChanged -= OnRecentsChanged;
        window.LeftPanelLayoutChanged -= OnLeftPanelLayoutChanged;
        window.LayoutDefaultsRequested -= OnLayoutDefaultsRequested;
        window.PhotoTransformDefaultsRequested -= OnPhotoTransformDefaultsRequested;
        window.ShowInkOverlayChanged -= OnShowInkOverlayChanged;
        window.StateChanged -= _callbacks.StateChanged;
        window.Activated -= _callbacks.Activated;
        window.Closed -= OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        var closedWindow = _window;
        if (closedWindow == null)
        {
            return;
        }

        CompleteClose(closedWindow);
    }

    internal void CompleteClose(ImageManagerWindow closedWindow)
    {
        ArgumentNullException.ThrowIfNull(closedWindow);
        if (!ReferenceEquals(_window, closedWindow))
        {
            return;
        }

        closedWindow.CaptureLayoutSettings(_settings);
        Unsubscribe(closedWindow);
        _saveSettings();
        _window = null;
        _callbacks.Closed();
    }

    private void OnFavoritesChanged(IReadOnlyList<string> favorites)
    {
        _settings.PhotoFavoriteFolders = favorites.ToList();
        _saveSettings();
    }

    private void OnRecentsChanged(IReadOnlyList<string> recents)
    {
        _settings.PhotoRecentFolders = recents.ToList();
        _saveSettings();
    }

    private void OnLeftPanelLayoutChanged(double ratio, int width)
    {
        _settings.PhotoManagerLeftPanelRatio = ratio;
        _settings.PhotoManagerLeftPanelWidth = width;
        _saveSettings();
    }

    private void OnLayoutDefaultsRequested()
    {
        var defaults = new AppSettings();
        _settings.PhotoManagerWindowWidth = 0;
        _settings.PhotoManagerWindowHeight = 0;
        _settings.PhotoManagerLeftPanelRatio = defaults.PhotoManagerLeftPanelRatio;
        _settings.PhotoManagerLeftPanelWidth = 0;
        _settings.PhotoManagerThumbnailSize = defaults.PhotoManagerThumbnailSize;
        _settings.PhotoManagerListMode = defaults.PhotoManagerListMode;
        _saveSettings();
    }

    private void OnPhotoTransformDefaultsRequested()
    {
        var defaults = new AppSettings();
        _settings.PhotoUnifiedTransformEnabled = defaults.PhotoUnifiedTransformEnabled;
        _settings.PhotoUnifiedScaleX = defaults.PhotoUnifiedScaleX;
        _settings.PhotoUnifiedScaleY = defaults.PhotoUnifiedScaleY;
        _settings.PhotoUnifiedTranslateX = defaults.PhotoUnifiedTranslateX;
        _settings.PhotoUnifiedTranslateY = defaults.PhotoUnifiedTranslateY;
        _saveSettings();
        _callbacks.ResetPhotoTransform();
    }

    private void OnShowInkOverlayChanged(bool enabled)
    {
        if (!PhotoOverlayPolicies.ShouldApply(_settings.PhotoShowInkOverlay, enabled))
        {
            return;
        }

        _settings.PhotoShowInkOverlay = enabled;
        _saveSettings();
        _callbacks.PhotoShowInkOverlayChanged(enabled);
    }
}
