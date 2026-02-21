using System;
using System.Windows;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Photos;

/// <summary>
/// Ink overlay integration for ImageManagerWindow.
/// Handles "显示笔迹" checkbox and thumbnail ink composite rendering.
/// </summary>
public partial class ImageManagerWindow
{
    private InkPersistenceService? _inkPersistence;
    private InkStrokeRenderer? _inkRenderer;
    public event Action<bool>? ShowInkOverlayChanged;

    /// <summary>
    /// Inject persistence service for checking ink availability per file.
    /// </summary>
    public void SetInkPersistenceService(InkPersistenceService persistence)
    {
        _inkPersistence = persistence;
        _inkRenderer = new InkStrokeRenderer();
    }

    /// <summary>
    /// Handle "显示笔迹" checkbox changed.
    /// When checked, re-scan the current directory to overlay ink on thumbnails.
    /// </summary>
    private void OnShowInkChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }
        ShowInkOverlayChanged?.Invoke(ViewModel.ShowInkOverlay);
        if (!string.IsNullOrWhiteSpace(ViewModel.CurrentFolder))
        {
            StartLoadImages(ViewModel.CurrentFolder);
        }
    }

    /// <summary>
    /// Check whether the given file path has associated ink data via sidecar.
    /// </summary>
    internal bool HasInkForFile(string filePath)
    {
        return _inkPersistence?.HasInkForFile(filePath) ?? false;
    }
}
