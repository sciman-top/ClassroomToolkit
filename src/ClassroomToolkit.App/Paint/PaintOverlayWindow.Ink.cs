using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using MediaColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool ApplyInkErase(Geometry geometry)
    {
        if (_inkStrokes.Count == 0 || geometry == null)
        {
            return false;
        }
        var photoInkModeActive = IsPhotoInkModeActive();
        var erasePrimary = photoInkModeActive ? ToPhotoGeometry(geometry) : geometry;
        var eraseFallback = photoInkModeActive ? geometry : null;
        if (erasePrimary == null)
        {
            return false;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var geometryPathChanged = false;
            var bloomGeometryChanged = false;
            var ribbonGeometryChanged = false;
            var updatedPath = ExcludeGeometryWithFallback(stroke.GeometryPath, erasePrimary, eraseFallback);
            if (!InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, updatedPath, out var strokeRemoved))
            {
                strokeRemoved = false;
            }
            else if (strokeRemoved)
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }
            else
            {
                geometryPathChanged = true;
            }

            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometryWithFallback(bloom.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(bloomUpdated))
                    {
                        stroke.Blooms.RemoveAt(j);
                        bloomGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(bloomUpdated, bloom.GeometryPath, StringComparison.Ordinal))
                    {
                        bloom.GeometryPath = bloomUpdated;
                        bloomGeometryChanged = true;
                    }
                }
            }
            if (stroke.Ribbons.Count > 0)
            {
                bool ribbonsChanged = false;
                for (int j = stroke.Ribbons.Count - 1; j >= 0; j--)
                {
                    var ribbon = stroke.Ribbons[j];
                    var ribbonUpdated = ExcludeGeometryWithFallback(ribbon.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(ribbonUpdated))
                    {
                        stroke.Ribbons.RemoveAt(j);
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(ribbonUpdated, ribbon.GeometryPath, StringComparison.Ordinal))
                    {
                        ribbon.GeometryPath = ribbonUpdated;
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                    }
                }
                if (ribbonsChanged)
                {
                    stroke.CachedRibbonGeometries = null;
                }
            }
            if (InkEraseStrokeChangePolicy.ShouldMarkStrokeChanged(
                    geometryPathChanged,
                    bloomGeometryChanged,
                    ribbonGeometryChanged))
            {
                changed = true;
            }
        }
        return changed;
    }

    private static string? ExcludeGeometryWithFallback(string geometryPath, Geometry primaryEraser, Geometry? fallbackEraser)
    {
        var primary = ExcludeGeometry(geometryPath, primaryEraser);
        if (fallbackEraser == null)
        {
            return primary;
        }
        if (primary == null || string.Equals(primary, geometryPath, StringComparison.Ordinal))
        {
            return ExcludeGeometry(geometryPath, fallbackEraser);
        }
        return primary;
    }

    private void CaptureStrokeContext()
    {
    }

    private void CommitStroke(InkStrokeData stroke)
    {
        _inkStrokes.Add(stroke);
        NotifyInkStateChanged(updateActiveSnapshot: true, notifyContext: false);
    }

    private void UpdateActiveCacheSnapshot()
    {
        if (!_inkCacheEnabled)
        {
            return;
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        var cacheKey = _currentCacheKey;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            InvalidateNeighborInkCache(cacheKey);
            return;
        }
        _photoCache.Set(cacheKey, strokes);
        InvalidateNeighborInkCache(cacheKey);
    }

    private List<InkStrokeData> CloneCommittedInkStrokes()
    {
        return CloneInkStrokes(_inkStrokes);
    }

    private void SetInkContextDirty()
    {
        _pendingInkContextCheck = true;
        _refreshOrchestrator?.RequestRefresh("ink-dirty");
    }

    private void SetInkCacheDirty()
    {
        MarkCurrentInkPageModified();
        ScheduleSidecarAutoSave();
    }

    private bool IsCurrentPageDirty()
    {
        return _inkDirtyPages.IsDirty(_currentDocumentPath, _currentPageIndex);
    }

    private bool IsRuntimeInkPageExplicitlyCleared(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        if (!_inkDirtyPages.TryGetRuntimeState(sourcePath, pageIndex, out _, out var runtimeHash, out _))
        {
            return false;
        }

        return string.Equals(runtimeHash, "empty", StringComparison.Ordinal);
    }

    private bool IsInkOperationActive()
    {
        return _strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting;
    }

    private bool IsPhotoInkModeActive()
    {
        return PhotoInkModePolicy.IsActive(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive());
    }

    private void NotifyInkStateChanged(
        bool updateActiveSnapshot,
        bool notifyContext = true,
        bool syncCrossPageVisual = true)
    {
        if (updateActiveSnapshot)
        {
            MarkInkStrokeVersionDirty();
            UpdateActiveCacheSnapshot();
        }
        SetInkCacheDirty();
        if (syncCrossPageVisual && !_suppressCrossPageVisualSync)
        {
            ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger.InkStateChanged);
        }
        if (notifyContext)
        {
            SetInkContextDirty();
        }
    }

    private void ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger trigger)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var elapsedMs = _crossPageInkVisualSyncState.LastSyncUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc
            ? double.MaxValue
            : (nowUtc - _crossPageInkVisualSyncState.LastSyncUtc).TotalMilliseconds;
        if (CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
                trigger,
                _crossPageInkVisualSyncState.LastTrigger,
                interactionActive: IsCrossPageInteractionActive(),
                elapsedMs))
        {
            return;
        }

        var decision = CrossPageInkVisualSyncPolicy.Resolve(
            IsPhotoInkModeActive(),
            IsCrossPageDisplayActive(),
            trigger);
        if (!decision.ShouldRequestCrossPageUpdate)
        {
            return;
        }

        if (decision.ShouldPrimeVisibleNeighborSlots)
        {
            PrimeVisibleNeighborInkSlots();
        }

        var source = trigger == CrossPageInkVisualSyncTrigger.InkRedrawCompleted
            ? CrossPageUpdateSources.InkRedrawCompleted
            : CrossPageUpdateSources.InkStateChanged;
        if (_crossPageDisplayUpdateState.Pending
            && CrossPageUpdateReplayPolicy.ShouldQueueReplay(CrossPageUpdateSourceKind.VisualSync))
        {
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                CrossPageReplayQueueDecisionFactory.VisualSync());
        }
        RequestCrossPageDisplayUpdate(source);
        CrossPageInkVisualSyncStateUpdater.MarkApplied(
            ref _crossPageInkVisualSyncState,
            nowUtc,
            trigger);
    }

    private bool TryGetCurrentPhotoReferenceSize(out double width, out double height)
    {
        width = 0;
        height = 0;
        if (!IsPhotoInkModeActive() || PhotoBackground.Source is not BitmapSource bitmap)
        {
            return false;
        }

        width = GetBitmapDisplayWidthInDip(bitmap);
        height = GetBitmapDisplayHeightInDip(bitmap);
        return width > InkInputRuntimeDefaults.PhotoReferenceSizeMinDip
            && height > InkInputRuntimeDefaults.PhotoReferenceSizeMinDip;
    }

    private void MarkInkInput()
    {
        _lastInkInputUtc = GetCurrentUtcTimestamp();
        _inkDiagnostics?.OnInkInput();
        UpdateInkMonitorInterval();
    }

    private bool ShouldDeferInkContext()
    {
        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            return true;
        }
        if (_lastInkInputUtc == InkRuntimeTimingDefaults.UnsetTimestampUtc)
        {
            return false;
        }
        return (GetCurrentUtcTimestamp() - _lastInkInputUtc).TotalMilliseconds < InkInputCooldownMs;
    }

    private void UpdateInkMonitorInterval()
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var idle = _lastInkInputUtc != InkRuntimeTimingDefaults.UnsetTimestampUtc
                   && (nowUtc - _lastInkInputUtc).TotalMilliseconds > InkIdleThresholdMs;

        var targetMs = idle ? InkMonitorIdleIntervalMs : InkMonitorActiveIntervalMs;
        var currentMs = _inkMonitor.Interval.TotalMilliseconds;
        if (Math.Abs(currentMs - targetMs) < 1)
        {
            return;
        }
        _inkMonitor.Interval = TimeSpan.FromMilliseconds(targetMs);
    }
}
