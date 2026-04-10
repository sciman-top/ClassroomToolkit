using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using IoPath = System.IO.Path;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool TryGetNeighborInkCacheEntry(int pageIndex, out InkBitmapCacheEntry entry)
    {
        entry = default;
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        return !string.IsNullOrWhiteSpace(cacheKey)
            && _neighborInkCache.TryGetValue(cacheKey, out entry);
    }

    private InkBitmapCacheEntry BuildNeighborInkCacheEntry(
        InkPageData page,
        BitmapSource pageBitmap,
        List<InkStrokeData> strokes)
    {
        var renderPlan = ResolveNeighborInkRenderSurfacePlan(pageBitmap, strokes);
        var bitmap = _inkStrokeRenderer.RenderPage(
            page,
            renderPlan.PixelWidth,
            renderPlan.PixelHeight,
            pageBitmap.DpiX,
            pageBitmap.DpiY,
            horizontalOffsetDip: renderPlan.HorizontalOffsetDip);
        return new InkBitmapCacheEntry(page.PageIndex, strokes, bitmap, renderPlan.HorizontalOffsetDip);
    }

    private CrossPageNeighborInkRenderSurfacePlan ResolveNeighborInkRenderSurfacePlan(
        BitmapSource pageBitmap,
        IReadOnlyList<InkStrokeData> strokes)
    {
        if (pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return new CrossPageNeighborInkRenderSurfacePlan(0, 0, 0);
        }

        if (!TryResolveNeighborInkHorizontalBounds(strokes, out var minX, out var maxX))
        {
            return new CrossPageNeighborInkRenderSurfacePlan(pageBitmap.PixelWidth, pageBitmap.PixelHeight, 0);
        }

        var pageWidthDip = GetBitmapDisplayWidthInDip(pageBitmap);
        return CrossPageNeighborInkRenderSurfacePolicy.Resolve(
            pagePixelWidth: pageBitmap.PixelWidth,
            pagePixelHeight: pageBitmap.PixelHeight,
            dpiX: pageBitmap.DpiX,
            pageWidthDip: pageWidthDip,
            minStrokeXDip: minX,
            maxStrokeXDip: maxX);
    }

    private static bool TryResolveNeighborInkHorizontalBounds(
        IReadOnlyList<InkStrokeData> strokes,
        out double minX,
        out double maxX)
    {
        minX = double.PositiveInfinity;
        maxX = double.NegativeInfinity;
        var hasBounds = false;

        for (var i = 0; i < strokes.Count; i++)
        {
            var stroke = strokes[i];
            var geometry = stroke.CachedGeometry;
            if (geometry == null)
            {
                geometry = InkGeometrySerializer.Deserialize(stroke.GeometryPath);
                if (geometry == null)
                {
                    continue;
                }

                if (geometry.CanFreeze)
                {
                    geometry.Freeze();
                }

                stroke.CachedGeometry = geometry;
            }

            var bounds = geometry.Bounds;
            if (bounds.IsEmpty)
            {
                continue;
            }

            hasBounds = true;
            minX = Math.Min(minX, bounds.Left);
            maxX = Math.Max(maxX, bounds.Right);
        }

        return hasBounds && maxX > minX;
    }

    private BitmapSource? ResolveNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap, bool allowDeferredRender)
    {
        if (!_inkShowEnabled || !_inkCacheEnabled || pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return null;
        }
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return null;
        }
        if (TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, knownCacheKey: cacheKey)) return null;
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            ScheduleNeighborInkSidecarLoad(cacheKey, pageIndex);
        }
        if (!_photoCache.TryGet(cacheKey, out strokes) || strokes.Count == 0)
        {
            if (_neighborInkCache.TryGetValue(cacheKey, out var cachedEntry))
            {
                if (TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, knownCacheKey: cacheKey)) return null;
                return cachedEntry.Bitmap;
            }
            return null;
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var entry) && ReferenceEquals(entry.Strokes, strokes))
        {
            return entry.Bitmap;
        }
        TryPrimeNeighborInkCache(pageIndex, pageBitmap);
        if (_neighborInkCache.TryGetValue(cacheKey, out var primedEntry) && ReferenceEquals(primedEntry.Strokes, strokes))
        {
            return primedEntry.Bitmap;
        }
        if (!allowDeferredRender)
        {
            // Interactive seam switching should prioritize continuity for the previous page.
            // When cache misses the latest stroke snapshot, build neighbor ink frame now
            // instead of returning null/stale frame and waiting for a deferred pass.
            var sourcePath = _currentDocumentPath;
            if (!_photoDocumentIsPdf)
            {
                var arrayIndex = pageIndex - 1;
                if (arrayIndex >= 0 && arrayIndex < _photoSequencePaths.Count)
                {
                    sourcePath = _photoSequencePaths[arrayIndex];
                }
            }

            var page = new InkPageData
            {
                PageIndex = pageIndex,
                DocumentName = IoPath.GetFileNameWithoutExtension(sourcePath),
                SourcePath = sourcePath,
                Strokes = strokes
            };
            var rebuiltEntry = BuildNeighborInkCacheEntry(page, pageBitmap, strokes);
            _neighborInkCache[cacheKey] = rebuiltEntry;
            TrimNeighborInkCache(pageIndex);
            return rebuiltEntry.Bitmap;
        }
        if (allowDeferredRender)
        {
            ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var staleEntry))
        {
            return staleEntry.Bitmap;
        }
        return null;
    }

    private bool TryResolveRuntimeInkPageForCrossPageIndex(int pageIndex, out string sourcePath, out int runtimePageIndex)
    {
        sourcePath = string.Empty;
        runtimePageIndex = 0;
        if (pageIndex <= 0)
        {
            return false;
        }

        if (_photoDocumentIsPdf)
        {
            if (string.IsNullOrWhiteSpace(_currentDocumentPath))
            {
                return false;
            }

            sourcePath = _currentDocumentPath;
            runtimePageIndex = pageIndex;
            return true;
        }

        var sequenceIndex = pageIndex - 1;
        if (sequenceIndex < 0 || sequenceIndex >= _photoSequencePaths.Count)
        {
            return false;
        }

        sourcePath = _photoSequencePaths[sequenceIndex];
        runtimePageIndex = 1;
        return !string.IsNullOrWhiteSpace(sourcePath);
    }

    private bool IsRuntimeInkPageClearedForCrossPageIndex(int pageIndex)
    {
        return TryResolveRuntimeInkPageForCrossPageIndex(pageIndex, out var sourcePath, out var runtimePageIndex)
            && IsRuntimeInkPageExplicitlyCleared(sourcePath, runtimePageIndex);
    }

    private bool TryEnforceRuntimeEmptyGuardForCrossPageIndex(int pageIndex, int visibleNeighborSlotIndex = -1, string? knownCacheKey = null)
    {
        if (!IsRuntimeInkPageClearedForCrossPageIndex(pageIndex)) return false;
        var cacheKey = !string.IsNullOrWhiteSpace(knownCacheKey) ? knownCacheKey : BuildNeighborInkCacheKey(pageIndex);
        if (!string.IsNullOrWhiteSpace(cacheKey)) { _photoCache.Remove(cacheKey); _neighborInkCache.Remove(cacheKey); }
        if (visibleNeighborSlotIndex >= 0 && visibleNeighborSlotIndex < _neighborInkImages.Count) { TryAssignFrameSource(_neighborInkImages[visibleNeighborSlotIndex], null); _neighborInkImages[visibleNeighborSlotIndex].Visibility = Visibility.Collapsed; }
        return true;
    }

    private bool HasNeighborInkStrokes(int pageIndex)
    {
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        return !string.IsNullOrWhiteSpace(cacheKey)
            && _photoCache.TryGet(cacheKey, out var strokes)
            && strokes.Count > 0;
    }

    private BitmapSource? TryGetNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap)
    {
        return ResolveNeighborInkBitmap(pageIndex, pageBitmap, allowDeferredRender: true);
    }

    private void RequestDeferredNeighborInkRender(int pageIndex, BitmapSource pageBitmap)
    {
        if (!_inkShowEnabled || !_inkCacheEnabled || pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return;
        }
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            return;
        }

        ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
    }

    private void ScheduleNeighborInkSidecarLoad(string cacheKey, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        if (_neighborInkSidecarLoadPending.Contains(cacheKey))
        {
            return;
        }
        _neighborInkSidecarLoadPending.Add(cacheKey);
        var scheduled = TryBeginInvoke(() =>
        {
            try
            {
                if (!IsCrossPageDisplayActive() || !_inkShowEnabled || !_inkCacheEnabled)
                {
                    return;
                }
                var expectedCacheKey = BuildNeighborInkCacheKey(pageIndex);
                if (CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(cacheKey, expectedCacheKey))
                {
                    _inkDiagnostics?.OnCrossPageUpdateEvent("skip", CrossPageUpdateSources.NeighborSidecar, "stale-cache-key");
                    return;
                }
                if (TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, knownCacheKey: cacheKey)) return;
                if (_photoCache.TryGet(cacheKey, out var existed) && existed.Count > 0)
                {
                    return;
                }
                if (TryLoadNeighborInkFromSidecarIntoCache(pageIndex))
                {
                    // De-dup against pointer-up refresh for the same switch cycle.
                    ScheduleCrossPageDisplayUpdateAfterInputSettles(
                        CrossPageUpdateSources.NeighborSidecar,
                        singlePerPointerUp: true);
                }
            }
            finally
            {
                _neighborInkSidecarLoadPending.Remove(cacheKey);
            }
        }, DispatcherPriority.Background);
        if (!scheduled)
        {
            _neighborInkSidecarLoadPending.Remove(cacheKey);
        }
    }

    private void ScheduleNeighborInkRender(
        string cacheKey,
        int pageIndex,
        BitmapSource pageBitmap,
        List<InkStrokeData> strokes)
    {
        if (_neighborInkRenderPending.Contains(cacheKey))
        {
            return;
        }
        _neighborInkRenderPending.Add(cacheKey);
        var scheduled = TryBeginInvoke(() =>
        {
            try
            {
                if (!IsCrossPageDisplayActive() || !_inkShowEnabled || !_inkCacheEnabled)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                var expectedCacheKey = BuildNeighborInkCacheKey(pageIndex);
                if (CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(cacheKey, expectedCacheKey))
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                if (!_photoCache.TryGet(cacheKey, out var currentStrokes) || currentStrokes.Count == 0)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                if (_neighborInkCache.TryGetValue(cacheKey, out var existing) && ReferenceEquals(existing.Strokes, currentStrokes))
                {
                    return;
                }
                var page = new InkPageData
                {
                    PageIndex = pageIndex,
                    DocumentName = _currentDocumentName,
                    SourcePath = _currentDocumentPath,
                    Strokes = currentStrokes
                };
                var cacheEntry = BuildNeighborInkCacheEntry(page, pageBitmap, currentStrokes);
                if (!_inkShowEnabled || !_inkCacheEnabled)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                _neighborInkCache[cacheKey] = cacheEntry;
                TrimNeighborInkCache(pageIndex);
                // Prefer in-place slot replacement to avoid a full cross-page refresh flash.
                if (TryApplyNeighborInkBitmapToVisibleSlot(pageIndex, cacheEntry.Bitmap, cacheEntry.HorizontalOffsetDip))
                {
                    _inkDiagnostics?.OnCrossPageUpdateEvent("apply", CrossPageUpdateSources.NeighborRender, $"page={pageIndex}");
                }
                else
                {
                    ScheduleCrossPageDisplayUpdateAfterInputSettles(CrossPageUpdateSources.NeighborRender);
                }
            }
            finally
            {
                _neighborInkRenderPending.Remove(cacheKey);
            }
        }, DispatcherPriority.Background);
        if (!scheduled)
        {
            _neighborInkRenderPending.Remove(cacheKey);
        }
    }

    private void ScheduleCrossPageDisplayUpdateAfterInputSettles(
        string source = CrossPageUpdateSources.PostInput,
        bool singlePerPointerUp = false,
        int? delayOverrideMs = null)
    {
        _ = CrossPageDeferredRefreshCoordinator.ScheduleAsync(
            source: source,
            singlePerPointerUp: singlePerPointerUp,
            delayOverrideMs: delayOverrideMs,
            configuredDelayMs: _photoPostInputRefreshDelayMs,
            lastPointerUpUtc: _lastCrossPagePointerUpUtc,
            getCurrentUtcTimestamp: GetCurrentUtcTimestamp,
            isCrossPageDisplayActive: IsCrossPageDisplayActive,
            isCrossPageInteractionActive: IsCrossPageInteractionActive,
            tryAcquirePostInputRefreshSlot: TryAcquirePostInputRefreshSlot,
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate,
            tryBeginInvoke: TryBeginInvoke,
            delayAsync: static delay => System.Threading.Tasks.Task.Delay(delay),
            incrementRefreshToken: () => Interlocked.Increment(ref _crossPagePostInputRefreshToken),
            readRefreshToken: () => _crossPagePostInputRefreshToken,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished,
            diagnostics: (action, eventSource, detail) => _inkDiagnostics?.OnCrossPageUpdateEvent(action, eventSource, detail));
    }

    private void ApplyCrossPagePointerUpFastRefresh(
        string source = CrossPageUpdateSources.PointerUpFast,
        bool requestImmediateRefresh = false)
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }

        // Fast path: keep current/neighbor transforms coherent immediately.
        UpdateNeighborTransformsForPan();
        if (requestImmediateRefresh)
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(source));
        }

        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "run",
            source,
            requestImmediateRefresh ? "mode=fast-current+immediate" : "mode=fast-current");
    }

    private bool TryAcquirePostInputRefreshSlot(out long pointerUpSequence)
    {
        pointerUpSequence = Interlocked.Read(ref _crossPagePointerUpSequence);
        var result = CrossPagePostInputRefreshSlotCoordinator.TryAcquire(
            pointerUpSequence: pointerUpSequence,
            lastPointerUpUtc: _lastCrossPagePointerUpUtc,
            readAppliedSequence: () => Interlocked.Read(ref _crossPagePostInputRefreshAppliedSequence),
            compareExchangeAppliedSequence: (nextValue, comparand) => Interlocked.CompareExchange(
                ref _crossPagePostInputRefreshAppliedSequence,
                nextValue,
                comparand));
        return result.Acquired;
    }

    private void HideNeighborSlotForPage(int pageIndex)
    {
        if (!IsCrossPageDisplayActive() || pageIndex <= 0)
        {
            return;
        }
        var pageUid = pageIndex.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i < _neighborPageImages.Count; i++)
        {
            var pageImg = _neighborPageImages[i];
            if (string.Equals(pageImg.Uid, pageUid, StringComparison.Ordinal))
            {
                pageImg.Visibility = Visibility.Collapsed;
            }

            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (string.Equals(inkImg.Uid, pageUid, StringComparison.Ordinal))
                {
                    inkImg.Visibility = Visibility.Collapsed;
                }
            }
        }
        if (_interactiveSwitchPinnedNeighborPage == pageIndex)
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }
    }

    private bool TryApplyNeighborInkBitmapToVisibleSlot(int pageIndex, BitmapSource inkBitmap, double horizontalOffsetDip = 0)
    {
        if (!_inkShowEnabled || !_inkCacheEnabled || _neighborInkImages.Count == 0)
        {
            return false;
        }

        var pageUid = pageIndex.ToString(CultureInfo.InvariantCulture);
        for (var i = 0; i < _neighborInkImages.Count; i++)
        {
            var inkImg = _neighborInkImages[i];
            if (!string.Equals(inkImg.Uid, pageUid, StringComparison.Ordinal))
            {
                continue;
            }
            if (i >= _neighborPageImages.Count)
            {
                continue;
            }
            if (_neighborPageImages[i].Visibility != Visibility.Visible)
            {
                continue;
            }

            TryAssignFrameSource(inkImg, inkBitmap);
            var baseTop = _neighborPageImages[i].Tag is double taggedTop ? taggedTop : 0;
            SetNeighborInkSlotTag(inkImg, baseTop, horizontalOffsetDip);
            inkImg.Visibility = Visibility.Visible;
            UpdateNeighborTransformsForPan();
            return true;
        }

        return false;
    }

    private static bool TryAssignFrameSource(WpfImage target, ImageSource? source, bool forceAssign = false)
    {
        if (!CrossPageFrameSourceAssignmentPolicy.ShouldAssign(target.Source, source, forceAssign))
        {
            return false;
        }

        target.Source = source;
        return true;
    }

    private void PrimeVisibleNeighborInkSlots()
    {
        if (!IsCrossPageDisplayActive() || !_inkShowEnabled || !_inkCacheEnabled)
        {
            return;
        }

        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            var pageImg = _neighborPageImages[i];
            if (pageImg.Visibility != Visibility.Visible || pageImg.Source is not BitmapSource pageBitmap)
            {
                continue;
            }
            if (!int.TryParse(pageImg.Uid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageIndex) || pageIndex <= 0)
            {
                continue;
            }
            if (TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, visibleNeighborSlotIndex: i)) { continue; }

            TryPrimeNeighborInkCache(pageIndex, pageBitmap);
            var cacheKey = BuildNeighborInkCacheKey(pageIndex);
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                continue;
            }
            if (!_neighborInkCache.TryGetValue(cacheKey, out var entry) || entry.Bitmap == null)
            {
                continue;
            }
            TryApplyNeighborInkBitmapToVisibleSlot(pageIndex, entry.Bitmap, entry.HorizontalOffsetDip);
        }
    }

    private void ScheduleCrossPageDisplayUpdateForMissingNeighborPages(int missingCount)
    {
        _ = CrossPageMissingNeighborRefreshCoordinator.ScheduleAsync(
            missingCount: missingCount,
            photoModeActive: _photoModeActive,
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            interactionActive: IsCrossPageInteractionActive(),
            lastScheduledUtc: _lastCrossPageMissingBitmapRefreshUtc,
            nowUtc: GetCurrentUtcTimestamp(),
            isCrossPageDisplayActive: IsCrossPageDisplayActive,
            updateLastScheduledUtc: value => _lastCrossPageMissingBitmapRefreshUtc = value,
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate,
            tryBeginInvoke: TryBeginInvoke,
            delayAsync: static delay => System.Threading.Tasks.Task.Delay(delay),
            incrementRefreshToken: () => Interlocked.Increment(ref _crossPageMissingBitmapRefreshToken),
            readRefreshToken: () => _crossPageMissingBitmapRefreshToken,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished,
            diagnostics: (action, source, detail) => _inkDiagnostics?.OnCrossPageUpdateEvent(action, source, detail));
    }

    private void TryPrimeNeighborInkCache(int pageIndex, BitmapSource pageBitmap)
    {
        if (!_inkShowEnabled || !_inkCacheEnabled)
        {
            return;
        }
        if (pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return;
        }

        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            return;
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var existing) && ReferenceEquals(existing.Strokes, strokes))
        {
            return;
        }

        var sourcePath = _currentDocumentPath;
        if (!_photoDocumentIsPdf)
        {
            var arrayIndex = pageIndex - 1;
            if (arrayIndex >= 0 && arrayIndex < _photoSequencePaths.Count)
            {
                sourcePath = _photoSequencePaths[arrayIndex];
            }
        }

        var page = new InkPageData
        {
            PageIndex = pageIndex,
            DocumentName = IoPath.GetFileNameWithoutExtension(sourcePath),
            SourcePath = sourcePath,
            Strokes = strokes
        };
        var cacheEntry = BuildNeighborInkCacheEntry(page, pageBitmap, strokes);
        _neighborInkCache[cacheKey] = cacheEntry;
        TrimNeighborInkCache(pageIndex);
    }

    private void TrimNeighborInkCache(int anchorPageIndex)
    {
        if (_neighborInkCache.Count <= NeighborInkCacheLimit)
        {
            return;
        }

        var keysToRemove = _neighborInkCache
            .OrderBy(pair => Math.Abs(pair.Value.PageIndex - anchorPageIndex))
            .Skip(NeighborInkCacheLimit)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _neighborInkCache.Remove(key);
        }
    }

    private void InvalidateNeighborInkCache(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        _neighborInkCache.Remove(cacheKey);
    }

    private int GetNeighborPrefetchRadius()
    {
        if (!IsCrossPageImageSequenceActive())
        {
            return CrossPageNeighborPrefetchRadiusMin;
        }

        var zoom = Math.Max(0.1, Math.Min(Math.Abs(_photoScale.ScaleX), Math.Abs(_photoScale.ScaleY)));
        var radius = zoom switch
        {
            <= 0.75 => CrossPageNeighborPrefetchRadiusDefault + 1,
            <= 1.40 => CrossPageNeighborPrefetchRadiusDefault,
            _ => CrossPageNeighborPrefetchRadiusDefault - 1
        };

        return Math.Clamp(radius, CrossPageNeighborPrefetchRadiusMin, _neighborPrefetchRadiusMaxSetting);
    }
}
