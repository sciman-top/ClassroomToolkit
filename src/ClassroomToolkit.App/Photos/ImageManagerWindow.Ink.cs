using System;
using System.Collections.Generic;
using System.Windows;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Photos;

/// <summary>
/// Ink overlay integration for ImageManagerWindow.
/// Handles "显示笔迹" checkbox and thumbnail ink composite rendering.
/// </summary>
public partial class ImageManagerWindow
{
    private static readonly TimeSpan InkCleanupThrottleWindow = TimeSpan.FromSeconds(30);
    private InkPersistenceService? _inkPersistence;
    private InkExportService? _inkExport;
    private InkStrokeRenderer? _inkRenderer;
    private readonly Dictionary<string, DateTime> _inkCleanupRunTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _inkCleanupLock = new();
    public event Action<bool>? ShowInkOverlayChanged;

    /// <summary>
    /// Inject persistence service for checking ink availability per file.
    /// </summary>
    public void SetInkPersistenceService(InkPersistenceService persistence, InkExportService? export = null)
    {
        _inkPersistence = persistence;
        _inkExport = export ?? new InkExportService(persistence);
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

    private ImageManagerInkCleanupSummary CleanupOrphanInkArtifacts(string folder)
    {
        if (_inkPersistence == null || _inkExport == null)
        {
            return new ImageManagerInkCleanupSummary(0, 0);
        }

        if (!TryBeginInkCleanup(folder))
        {
            return new ImageManagerInkCleanupSummary(0, 0);
        }

        return ImageManagerInkCleanupExecutor.Cleanup(
            folder,
            _inkPersistence.CleanupOrphanSidecarsInDirectory,
            _inkExport.CleanupOrphanCompositeOutputsInDirectory);
    }

    private bool TryBeginInkCleanup(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        lock (_inkCleanupLock)
        {
            var now = DateTime.UtcNow;
            CompactInkCleanupRunTimes(now);
            if (_inkCleanupRunTimes.TryGetValue(folder, out var lastRun)
                && now - lastRun < InkCleanupThrottleWindow)
            {
                return false;
            }

            _inkCleanupRunTimes[folder] = now;
            return true;
        }
    }

    private void CompactInkCleanupRunTimes(DateTime now)
    {
        if (_inkCleanupRunTimes.Count < 256)
        {
            return;
        }

        var expireBefore = now - TimeSpan.FromMinutes(10);
        List<string>? staleKeys = null;
        foreach (var pair in _inkCleanupRunTimes)
        {
            if (pair.Value < expireBefore)
            {
                staleKeys ??= new List<string>();
                staleKeys.Add(pair.Key);
            }
        }

        if (staleKeys == null)
        {
            return;
        }

        foreach (var staleKey in staleKeys)
        {
            _inkCleanupRunTimes.Remove(staleKey);
        }
    }
}
