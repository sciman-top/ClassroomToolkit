using System;
using System.Collections.Generic;
using System.Linq;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void CaptureStrokeContext()
    {
        _ = _pendingInkContextCheck;
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
        if (!IsPhotoInkModeActive() || PhotoBackground.Source is not System.Windows.Media.Imaging.BitmapSource bitmap)
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
