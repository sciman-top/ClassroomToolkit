using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _imageListScrollViewer = FindDescendant<ScrollViewer>(ImageList);
        _ = InitializeTreeAsync(_lifecycleCancellation.Token);
        InitializeDefaultFolder();
        EnterInitialMaximizedState();
        ApplyAdaptiveLayout();
        UpdateWindowStateToggleButton();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        BeginClose();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompleteClose();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        TrackRestoredWindowSize();
        ApplyAdaptiveLayout();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        ApplyAdaptiveLayout();
        UpdateWindowStateToggleButton();
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        RemoveMinimizeButton();
    }

    public void SetKeyboardNavigationSuppressed(bool suppressed)
    {
        _suppressKeyboardNavigation = suppressed;
    }

    public void ApplyLayoutSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.PhotoManagerWindowWidth > 0)
        {
            Width = settings.PhotoManagerWindowWidth;
            _restoredWindowWidth = settings.PhotoManagerWindowWidth;
        }

        if (settings.PhotoManagerWindowHeight > 0)
        {
            Height = settings.PhotoManagerWindowHeight;
            _restoredWindowHeight = settings.PhotoManagerWindowHeight;
        }

        _preferredLeftRatio = NormalizeLeftRatio(settings.PhotoManagerLeftPanelRatio, DefaultLeftRatio);
        _preferredLeftPanelWidth = Math.Max(0, settings.PhotoManagerLeftPanelWidth);
        if (ThumbnailSizeSlider != null)
        {
            ThumbnailSizeSlider.Value = NormalizeThumbnailSize(settings.PhotoManagerThumbnailSize, DefaultThumbnailSize);
        }

        SetViewMode(settings.PhotoManagerListMode);
    }

    public void CaptureLayoutSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        UpdatePreferredLeftLayoutFromCurrent();
        var width = WindowState == WindowState.Normal
            ? (ActualWidth > 0 ? ActualWidth : Width)
            : _restoredWindowWidth;
        var height = WindowState == WindowState.Normal
            ? (ActualHeight > 0 ? ActualHeight : Height)
            : _restoredWindowHeight;
        settings.PhotoManagerWindowWidth = (int)Math.Round(width);
        settings.PhotoManagerWindowHeight = (int)Math.Round(height);
        settings.PhotoManagerLeftPanelRatio = _preferredLeftRatio;
        settings.PhotoManagerLeftPanelWidth = _preferredLeftPanelWidth;
        settings.PhotoManagerThumbnailSize = NormalizeThumbnailSize(ThumbnailSizeSlider?.Value ?? DefaultThumbnailSize, DefaultThumbnailSize);
        settings.PhotoManagerListMode = ViewModel.ListMode;
    }

    private void BeginClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _lifecycleCancellation.Cancel();
        UpdatePreferredLeftLayoutFromCurrent();
        SafeActionExecutionExecutor.TryExecute(
            () => LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth),
            ex => Debug.WriteLine($"ImageManager: close layout callback failed: {ex.Message}"));
        _thumbnailCts?.Cancel();
    }

    private void CompleteClose()
    {
        if (_closeCompleted)
        {
            return;
        }

        _closeCompleted = true;
        Loaded -= OnWindowLoaded;
        PreviewKeyDown -= OnPreviewKeyDown;
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
        SizeChanged -= OnWindowSizeChanged;
        StateChanged -= OnWindowStateChanged;
        SourceInitialized -= OnWindowSourceInitialized;
        _thumbnailRefreshDebounceTimer.Stop();
        _thumbnailRefreshDebounceTimer.Tick -= OnThumbnailRefreshDebounceTick;
        _multiSelectLongPressTimer.Stop();
        _multiSelectLongPressTimer.Tick -= OnMultiSelectLongPressTick;
        _thumbnailBackgroundQueueTimer.Stop();
        _thumbnailBackgroundQueueTimer.Tick -= OnThumbnailBackgroundQueueTick;
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        ResetThumbnailPendingQueue();
        lock (_thumbnailCacheLock)
        {
            _thumbnailCache.Clear();
            _thumbnailCacheLru.Clear();
        }

        try
        {
            _thumbnailSemaphore.Dispose();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: thumbnail semaphore dispose failed: {ex.Message}");
        }

        _lifecycleCancellation.Dispose();
    }

    private void TryReleaseThumbnailSemaphore()
    {
        try
        {
            _thumbnailSemaphore.Release();
        }
        catch (ObjectDisposedException)
        {
            // Expected when background workers race with window shutdown.
        }
    }
}
