using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool IsCrossPageInteractionActive()
    {
        return CrossPageInteractionActivityPolicy.IsActive(
            _photoPanning || _photoManipulating,
            _crossPageDragging,
            IsInkOperationActive());
    }

    private bool IsCrossPagePanOrDragActive()
    {
        return CrossPageInteractionActivityPolicy.IsActive(
            _photoPanning || _photoManipulating,
            _crossPageDragging,
            inkOperationActive: false);
    }

    private bool IsCrossPageDisplayActive()
    {
        return PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled());
    }

    private bool IsCrossPageDisplaySettingEnabled()
    {
        return _crossPageDisplayEnabled;
    }

    private bool IsCrossPageImageSequenceActive()
    {
        return IsCrossPageDisplayActive() && !_photoDocumentIsPdf;
    }

    private void ClearNeighborPages()
    {
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Children.Clear();
        _neighborPagesCanvas.Visibility = Visibility.Collapsed;
        _neighborPageImages.Clear();
        _neighborInkImages.Clear();
        _lastNeighborPagesNonEmptyUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        _interactiveSwitchPinnedNeighborPage = 0;
        _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    }

    private void ClearNeighborInkVisuals(bool clearSlotIdentity = false, int preservePageIndex = 0)
    {
        var preserveUid = preservePageIndex > 0
            ? preservePageIndex.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        for (var i = 0; i < _neighborInkImages.Count; i++)
        {
            var inkImg = _neighborInkImages[i];
            if (!string.IsNullOrWhiteSpace(preserveUid)
                && string.Equals(inkImg.Uid, preserveUid, StringComparison.Ordinal))
            {
                inkImg.Visibility = inkImg.Source != null ? Visibility.Visible : Visibility.Collapsed;
                continue;
            }
            TryAssignFrameSource(inkImg, null, forceAssign: true);
            inkImg.Visibility = Visibility.Collapsed;
            if (clearSlotIdentity)
            {
                inkImg.Uid = string.Empty;
                inkImg.Tag = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(preserveUid))
        {
            _interactiveSwitchPinnedNeighborPage = preservePageIndex;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = GetCurrentUtcTimestamp()
                .AddMilliseconds(CrossPageRuntimeDefaults.NeighborPagesClearGraceMs);
        }
        else
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
        _neighborPageHeightCache.Clear();
        InvalidateCrossPageBoundsCache();
    }

    private int GetTotalPageCount()
    {
        if (_photoDocumentIsPdf)
        {
            return _pdfPageCount;
        }
        return _photoSequencePaths.Count;
    }

    private int GetCurrentPageIndexForCrossPage()
    {
        if (_photoDocumentIsPdf)
        {
            return _currentPageIndex;
        }
        return _photoSequenceIndex >= 0 ? _photoSequenceIndex + 1 : 1;
    }

    private BitmapSource? GetPageBitmap(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return GetPdfPageBitmap(pageIndex);
        }
        // For image sequence, pageIndex is 1-based
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return null;
        }
        if (_neighborImageCache.TryGetValue(pageIndex, out var cached))
        {
            return cached;
        }
        var path = _photoSequencePaths[arrayIndex];
        var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: IsCrossPageImageSequenceActive());
        if (bitmap == null && IsCrossPageImageSequenceActive())
        {
            // Fallback: when downsample decode fails transiently, retry full decode once.
            // This prioritizes continuity over memory in cross-page seam rendering.
            bitmap = TryLoadBitmapSource(path, downsampleToMonitor: false);
            if (bitmap != null)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("recover", "neighbor-bitmap-load", $"page={pageIndex} mode=fullres");
            }
        }
        if (bitmap != null)
        {
            _neighborImageCache[pageIndex] = bitmap;
            // Limit cache size
            if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
            {
                var keysToRemove = _neighborImageCache.Keys
                    .OrderBy(k => Math.Abs(k - pageIndex))
                    .Skip(NeighborPageCacheLimit)
                    .ToList();
                foreach (var k in keysToRemove)
                {
                    _neighborImageCache.Remove(k);
                }
            }
        }
        return bitmap;
    }

    private BitmapSource? GetNeighborPageBitmapForRender(int pageIndex, bool allowSynchronousResolve)
    {
        if (!_photoDocumentIsPdf)
        {
            if (!allowSynchronousResolve)
            {
                return _neighborImageCache.TryGetValue(pageIndex, out var cachedImage)
                    ? cachedImage
                    : null;
            }

            return GetPageBitmap(pageIndex);
        }

        if (TryGetCachedPdfPageBitmap(
                pageIndex,
                out var cached,
                tryEnterTimeoutMs: allowSynchronousResolve ? PhotoDocumentRuntimeDefaults.PdfCacheTryEnterTimeoutMs : 0))
        {
            return cached;
        }

        if (!allowSynchronousResolve)
        {
            return null;
        }

        // In cross-page mode, a cache miss can leave a large blank gap between pages.
        // Fallback to direct render once so visible neighbors are always drawable.
        return GetPdfPageBitmap(pageIndex);
    }
    
    private void ScheduleNeighborImagePrefetch(int pageIndex)
    {
        var interactionActiveForPrefetch = IsCrossPagePanOrDragActive();
        if (!CrossPageNeighborPrefetchGatePolicy.ShouldSchedule(
                _photoModeActive,
                _photoDocumentIsPdf,
                IsCrossPageDisplaySettingEnabled(),
                interactionActiveForPrefetch))
        {
            return;
        }
        if (_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count)
        {
            return;
        }
        if (_neighborImageCache.ContainsKey(pageIndex))
        {
            return;
        }
        if (!_neighborImagePrefetchPending.Add(pageIndex))
        {
            return;
        }
        var scheduled = TryBeginInvoke(() =>
        {
            try
            {
                if (!CrossPageNeighborPrefetchGatePolicy.ShouldRunPrefetch(
                        _photoModeActive,
                        _photoDocumentIsPdf,
                        IsCrossPageDisplaySettingEnabled(),
                        IsCrossPagePanOrDragActive()))
                {
                    return;
                }
                if (_neighborImageCache.ContainsKey(pageIndex))
                {
                    return;
                }
                var path = _photoSequencePaths[pageIndex - 1];
                var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: true);
                if (bitmap == null)
                {
                    return;
                }
                _neighborImageCache[pageIndex] = bitmap;
                if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
                {
                    var keysToRemove = _neighborImageCache.Keys
                        .OrderBy(k => Math.Abs(k - pageIndex))
                        .Skip(NeighborPageCacheLimit)
                        .ToList();
                    foreach (var k in keysToRemove)
                    {
                        _neighborImageCache.Remove(k);
                    }
                }
            }
            finally
            {
                _neighborImagePrefetchPending.Remove(pageIndex);
            }
        }, DispatcherPriority.Background);
        if (!scheduled)
        {
            _neighborImagePrefetchPending.Remove(pageIndex);
            _inkDiagnostics?.OnCrossPageUpdateEvent("defer-abort", "neighbor-prefetch", "dispatch-failed");
        }
    }

    private double GetScaledPageHeight(BitmapSource? bitmap, double normalizedWidthDip = 0)
    {
        if (bitmap == null)
        {
            return 0;
        }
        var imageHeight = GetBitmapDisplayHeightInDip(bitmap);
        if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
        {
            var imageWidth = GetBitmapDisplayWidthInDip(bitmap);
            if (imageWidth > 0)
            {
                imageHeight *= normalizedWidthDip / imageWidth;
            }
        }
        return imageHeight * _photoScale.ScaleY;
    }

    private double GetBitmapDisplayWidthInDip(BitmapSource bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width > 0 && height > 0)
        {
            if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
            {
                return height;
            }
            return width;
        }
        var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
        var fallbackWidth = bitmap.PixelWidth * 96.0 / dpiX;
        if (bitmap is BitmapImage rotated && (rotated.Rotation == Rotation.Rotate90 || rotated.Rotation == Rotation.Rotate270))
        {
            var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
            return bitmap.PixelHeight * 96.0 / dpiY;
        }
        return fallbackWidth;
    }

    private double GetBitmapDisplayHeightInDip(BitmapSource bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width > 0 && height > 0)
        {
            if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
            {
                return width;
            }
            return height;
        }
        var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        var fallbackHeight = bitmap.PixelHeight * 96.0 / dpiY;
        if (bitmap is BitmapImage rotated && (rotated.Rotation == Rotation.Rotate90 || rotated.Rotation == Rotation.Rotate270))
        {
            var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
            return bitmap.PixelWidth * 96.0 / dpiX;
        }
        return fallbackHeight;
    }

    private void ResetCrossPageNormalizedWidth()
    {
        _crossPageNormalizedWidthDip = 0;
        InvalidateCrossPageBoundsCache();
    }

    private void InvalidateCrossPageBoundsCache()
    {
        _crossPageBoundsCacheValid = false;
        _crossPageBoundsCacheUpdatedUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    }

    private bool TryResolveCachedCrossPageBounds(
        bool includeSlack,
        int currentPage,
        int totalPages,
        double viewportWidth,
        double viewportHeight,
        double normalizedWidthDip,
        bool preferCachedDuringInteraction,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY)
    {
        minX = maxX = minY = maxY = 0;
        if (!preferCachedDuringInteraction || !_crossPageBoundsCacheValid)
        {
            return false;
        }

        if (_crossPageBoundsCacheUpdatedUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var nowUtc = GetCurrentUtcTimestamp();
        if ((nowUtc - _crossPageBoundsCacheUpdatedUtc).TotalMilliseconds > CrossPageBoundsCacheDefaults.InteractiveReuseMaxAgeMs)
        {
            return false;
        }

        const double viewportEpsilon = 0.5;
        if (_crossPageBoundsCacheIncludeSlack != includeSlack
            || _crossPageBoundsCacheCurrentPage != currentPage
            || _crossPageBoundsCacheTotalPages != totalPages
            || Math.Abs(_crossPageBoundsCacheViewportWidth - viewportWidth) > viewportEpsilon
            || Math.Abs(_crossPageBoundsCacheViewportHeight - viewportHeight) > viewportEpsilon
            || Math.Abs(_crossPageBoundsCacheNormalizedWidthDip - normalizedWidthDip) > CrossPageBoundsCacheDefaults.KeyEpsilon
            || Math.Abs(_crossPageBoundsCacheScaleX - _photoScale.ScaleX) > CrossPageBoundsCacheDefaults.KeyEpsilon
            || Math.Abs(_crossPageBoundsCacheScaleY - _photoScale.ScaleY) > CrossPageBoundsCacheDefaults.KeyEpsilon)
        {
            return false;
        }

        minX = _crossPageBoundsCacheMinX;
        maxX = _crossPageBoundsCacheMaxX;
        minY = _crossPageBoundsCacheMinY;
        maxY = _crossPageBoundsCacheMaxY;
        return true;
    }

    private double GetCrossPageNormalizedWidthDip(BitmapSource? fallback = null)
    {
        if (!IsCrossPageImageSequenceActive())
        {
            return 0;
        }
        if (_crossPageNormalizedWidthDip > 1)
        {
            return _crossPageNormalizedWidthDip;
        }
        var bitmap = fallback ?? PhotoBackground.Source as BitmapSource;
        if (bitmap == null)
        {
            return 0;
        }
        var widthDip = GetBitmapDisplayWidthInDip(bitmap);
        if (widthDip <= 1)
        {
            return 0;
        }
        _crossPageNormalizedWidthDip = widthDip;
        return _crossPageNormalizedWidthDip;
    }

    private void UpdateCurrentPageWidthNormalization(BitmapSource? bitmap = null)
    {
        EnsurePhotoTransformsWritable();
        if (!IsCrossPageImageSequenceActive())
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var source = bitmap ?? PhotoBackground.Source as BitmapSource;
        if (source == null)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(source);
        var pageWidthDip = GetBitmapDisplayWidthInDip(source);
        if (normalizedWidthDip <= 0 || pageWidthDip <= 0)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var ratio = normalizedWidthDip / pageWidthDip;
        _photoPageScale.ScaleX = ratio;
        _photoPageScale.ScaleY = ratio;
        UpdatePhotoInkClip();
    }

    private void UpdateCrossPageDisplay()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        var interactionActive = IsCrossPageInteractionActive();
        var allowSynchronousHeightResolve = CrossPageNeighborHeightResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive,
            _photoDocumentIsPdf);
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        if (currentPageHeight <= 0)
        {
            return;
        }
        _neighborPageHeightCache[currentPage] = currentPageHeight;
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;

        // Dynamically collect all pages intersecting viewport to avoid missing strips
        // when zoomed out or when page heights vary significantly.
        var visibilityMargin = CrossPageViewportBoundsDefaults.VisibilityMarginDip;
        var visiblePages = new List<(int PageIndex, double Top)>
        {
            (currentPage, currentTop)
        };

        var prevTop = currentTop;
        for (int pageIndex = currentPage - 1; pageIndex >= 1; pageIndex--)
        {
            var prevHeight = GetNeighborPageHeightWithFallback(
                pageIndex,
                normalizedWidthDip,
                currentPageHeight,
                "prev",
                allowSynchronousResolve: allowSynchronousHeightResolve);
            if (prevHeight <= 0)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("abort", "neighbor-height", $"page={pageIndex} dir=prev");
                break;
            }
            prevTop -= prevHeight;
            var prevBottom = prevTop + prevHeight;
            if (prevBottom < -visibilityMargin)
            {
                break;
            }
            visiblePages.Insert(0, (pageIndex, prevTop));
        }

        var nextTop = currentBottom;
        for (int pageIndex = currentPage + 1; pageIndex <= totalPages; pageIndex++)
        {
            if (nextTop > viewportHeight + visibilityMargin)
            {
                break;
            }
            visiblePages.Add((pageIndex, nextTop));
            var nextHeight = GetNeighborPageHeightWithFallback(
                pageIndex,
                normalizedWidthDip,
                currentPageHeight,
                "next",
                allowSynchronousResolve: allowSynchronousHeightResolve);
            if (nextHeight <= 0)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("abort", "neighbor-height", $"page={pageIndex} dir=next");
                break;
            }
            nextTop += nextHeight;
        }
        if (_photoDocumentIsPdf)
        {
            var missingPages = visiblePages
                .Where(p => p.PageIndex != currentPage)
                .Select(p => p.PageIndex)
                .Distinct()
                .Where(p => !TryGetCachedPdfPageBitmap(p, out _, tryEnterTimeoutMs: 0))
                .ToList();
            if (missingPages.Count > 0)
            {
                SchedulePdfVisiblePrefetch(missingPages);
            }
            _pdfPinnedPages.Clear();
            foreach (var page in visiblePages.Select(p => p.PageIndex).Distinct())
            {
                _pdfPinnedPages.Add(page);
            }
        }
        if (!_photoDocumentIsPdf)
        {
            var prefetchRadius = GetNeighborPrefetchRadius();
            for (int delta = 1; delta <= prefetchRadius; delta++)
            {
                ScheduleNeighborImagePrefetch(currentPage - delta);
                ScheduleNeighborImagePrefetch(currentPage + delta);
            }
        }
        // Render neighbor pages
        var neighborPages = CrossPageNeighborPageDedupPolicy.Resolve(
            visiblePages.Where(p => p.PageIndex != currentPage).ToList());
        if (_crossPageDragging && _crossPageTranslateClamped && neighborPages.Count == 0)
        {
            return;
        }
        RenderNeighborPages(neighborPages);
    }

    private double GetScaledHeightForPage(
        int pageIndex,
        double normalizedWidthDip = 0,
        bool allowSynchronousResolve = true)
    {
        if (pageIndex <= 0)
        {
            return 0;
        }

        if (_neighborPageHeightCache.TryGetValue(pageIndex, out var cachedHeight) && cachedHeight > 0)
        {
            if (!_photoDocumentIsPdf && !allowSynchronousResolve)
            {
                return cachedHeight;
            }
        }

        if (!_photoDocumentIsPdf && !allowSynchronousResolve)
        {
            return 0;
        }

        var height = _photoDocumentIsPdf
            ? GetScaledPdfPageHeight(pageIndex)
            : GetScaledPageHeight(GetPageBitmap(pageIndex), normalizedWidthDip);
        if (height > 0)
        {
            _neighborPageHeightCache[pageIndex] = height;
            return height;
        }
        if (_neighborPageHeightCache.TryGetValue(pageIndex, out cachedHeight) && cachedHeight > 0)
        {
            return cachedHeight;
        }

        if (PhotoBackground.Source is BitmapSource currentBitmap)
        {
            var normalizedWidth = normalizedWidthDip > 0 ? normalizedWidthDip : GetCrossPageNormalizedWidthDip(currentBitmap);
            var currentHeight = GetScaledPageHeight(currentBitmap, normalizedWidth);
            if (currentHeight > 0)
            {
                _neighborPageHeightCache[pageIndex] = currentHeight;
                return currentHeight;
            }
        }
        return 0;
    }

    private double GetScaledHeightForInteractiveSwitchClamp(
        int pageIndex,
        double normalizedWidthDip,
        double fallbackHeight)
    {
        if (pageIndex <= 0)
        {
            return fallbackHeight;
        }

        if (_neighborPageHeightCache.TryGetValue(pageIndex, out var cachedHeight) && cachedHeight > 0)
        {
            return cachedHeight;
        }

        if (pageIndex == GetCurrentPageIndexForCrossPage() && PhotoBackground.Source is BitmapSource currentBitmap)
        {
            var currentHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
            if (currentHeight > 0)
            {
                _neighborPageHeightCache[pageIndex] = currentHeight;
                return currentHeight;
            }
        }

        if (_photoDocumentIsPdf)
        {
            if (TryGetCachedPdfPageBitmap(pageIndex, out var cachedPdfBitmap, tryEnterTimeoutMs: 0) && cachedPdfBitmap != null)
            {
                var pdfHeight = GetScaledPageHeight(cachedPdfBitmap, normalizedWidthDip);
                if (pdfHeight > 0)
                {
                    _neighborPageHeightCache[pageIndex] = pdfHeight;
                    return pdfHeight;
                }
            }
        }
        else if (_neighborImageCache.TryGetValue(pageIndex, out var cachedImageBitmap))
        {
            var imageHeight = GetScaledPageHeight(cachedImageBitmap, normalizedWidthDip);
            if (imageHeight > 0)
            {
                _neighborPageHeightCache[pageIndex] = imageHeight;
                return imageHeight;
            }
        }

        return fallbackHeight;
    }

    private double GetNeighborPageHeightWithFallback(
        int pageIndex,
        double normalizedWidthDip,
        double preferredHeight,
        string direction,
        bool allowSynchronousResolve = true)
    {
        var height = GetScaledHeightForPage(
            pageIndex,
            normalizedWidthDip,
            allowSynchronousResolve: allowSynchronousResolve);
        if (height > 0)
        {
            return height;
        }

        if (preferredHeight > 0)
        {
            _neighborPageHeightCache[pageIndex] = preferredHeight;
            _inkDiagnostics?.OnCrossPageUpdateEvent("fallback", "neighbor-height", $"page={pageIndex} dir={direction} source=current");
            return preferredHeight;
        }

        var nearest = _neighborPageHeightCache
            .Where(p => p.Value > 0)
            .OrderBy(p => Math.Abs(p.Key - pageIndex))
            .Select(p => p.Value)
            .FirstOrDefault();
        if (nearest > 0)
        {
            _neighborPageHeightCache[pageIndex] = nearest;
            _inkDiagnostics?.OnCrossPageUpdateEvent("fallback", "neighbor-height", $"page={pageIndex} dir={direction} source=nearest");
            return nearest;
        }

        return 0;
    }

    private void RenderNeighborPages(List<(int PageIndex, double Top)> neighborPages)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var interactionActive = IsCrossPageInteractionActive();
        if (CrossPageInteractivePinLifetimePolicy.ShouldReleasePin(
                _interactiveSwitchPinnedNeighborInkHoldUntilUtc,
                nowUtc,
                interactionActive))
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }

        if (neighborPages.Count == 0)
        {
            var nowForClearUtc = GetCurrentUtcTimestamp();
            var interactionActiveForClear = IsCrossPageInteractionActive();
            var elapsedSinceNonEmptyMs = (nowForClearUtc - _lastNeighborPagesNonEmptyUtc).TotalMilliseconds;
            var hasVisibleNeighborFrame = _neighborPageImages.Any(img => img.Visibility == Visibility.Visible && img.Source != null);
            if (CrossPageNeighborPagesClearPolicy.ShouldKeepFrames(
                hasVisibleNeighborFrame,
                interactionActiveForClear,
                _lastNeighborPagesNonEmptyUtc,
                nowForClearUtc,
                CrossPageRuntimeDefaults.NeighborPagesClearGraceMs))
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("hold", "neighbor-pages", $"reason=grace elapsedMs={elapsedSinceNonEmptyMs:F0}");
                return;
            }
            ClearNeighborPages();
            return;
        }
        _lastNeighborPagesNonEmptyUtc = GetCurrentUtcTimestamp();
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Visibility = Visibility.Visible;
        var normalizedWidthDip = 0.0;
        if (!_photoDocumentIsPdf && PhotoBackground.Source is BitmapSource currentBitmap)
        {
            normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        }
        var missingPageBitmapCount = 0;
        // Ensure we have enough Image elements
        while (_neighborPageImages.Count < neighborPages.Count)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }
        while (_neighborInkImages.Count < neighborPages.Count)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }

        // Preserve currently visible frames by page uid so slot reordering does not
        // temporarily blank a page's bitmap/ink for one frame.
        var preservedPageFrames = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        var preservedInkFrames = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        for (int i = 0; i < _neighborPageImages.Count; i++)
        {
            var pageImg = _neighborPageImages[i];
            if (pageImg.Visibility == Visibility.Visible
                && pageImg.Source is BitmapSource pageBitmap
                && !string.IsNullOrWhiteSpace(pageImg.Uid)
                && !preservedPageFrames.ContainsKey(pageImg.Uid))
            {
                preservedPageFrames[pageImg.Uid] = pageBitmap;
            }

            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (inkImg.Visibility == Visibility.Visible
                    && inkImg.Source is BitmapSource inkBitmap
                    && !string.IsNullOrWhiteSpace(inkImg.Uid)
                    && !preservedInkFrames.ContainsKey(inkImg.Uid))
                {
                    preservedInkFrames[inkImg.Uid] = inkBitmap;
                }
            }
        }
        // Hide excess images
        for (int i = neighborPages.Count; i < _neighborPageImages.Count; i++)
        {
            _neighborPageImages[i].Visibility = Visibility.Collapsed;
            if (i < _neighborInkImages.Count)
            {
                _neighborInkImages[i].Visibility = Visibility.Collapsed;
            }
        }
        // Update visible neighbor page images
        for (int i = 0; i < neighborPages.Count; i++)
        {
            var (pageIndex, top) = neighborPages[i];
            var pageUid = pageIndex.ToString(CultureInfo.InvariantCulture);
            var img = _neighborPageImages[i];
            var slotPageChanged = !string.Equals(img.Uid, pageUid, StringComparison.Ordinal);
            var hasCurrentFrame = img.Source is BitmapSource;
            var allowSynchronousResolve = CrossPageNeighborBitmapResolvePolicy.ShouldAllowSynchronousResolve(
                interactionActive: interactionActive,
                slotPageChanged: slotPageChanged);
            BitmapSource? bitmap;
            if (interactionActive && !slotPageChanged && img.Source is BitmapSource visibleBitmap)
            {
                // During drag/gesture pan, prefer transform-only movement for already visible slots.
                bitmap = visibleBitmap;
            }
            else
            {
                bitmap = GetNeighborPageBitmapForRender(pageIndex, allowSynchronousResolve);
            }
            if (bitmap == null && !_photoDocumentIsPdf && interactionActive && slotPageChanged)
            {
                ScheduleNeighborImagePrefetch(pageIndex);
            }
            if (bitmap == null
                && slotPageChanged
                && preservedPageFrames.TryGetValue(pageUid, out var preservedPageBitmap))
            {
                bitmap = preservedPageBitmap;
            }
            var pageFrameDecision = CrossPageNeighborPageFramePolicy.Resolve(
                slotPageChanged,
                hasCurrentFrame,
                hasResolvedTargetFrame: bitmap != null,
                interactionActive: interactionActive);
            var heldCurrentSlotFrame = false;
            var shouldReplacePageFrame = CrossPageInteractivePageReplacementPolicy.ShouldReplace(
                hasResolvedTargetFrame: bitmap != null,
                interactionActive,
                slotPageChanged,
                hasCurrentFrame);
            if (shouldReplacePageFrame && bitmap != null)
            {
                TryAssignFrameSource(img, bitmap);
                img.Visibility = Visibility.Visible;
            }
            else if (img.Source is BitmapSource keepBitmap
                     && CrossPageInteractivePageReplacementPolicy.ShouldReuseCurrentFrame(
                         shouldReplacePageFrame,
                         slotPageChanged,
                         hasCurrentFrame))
            {
                bitmap = keepBitmap;
                img.Visibility = Visibility.Visible;
            }
            else if (pageFrameDecision.HoldCurrentFrame && img.Source is BitmapSource currentFrame)
            {
                bitmap = currentFrame;
                img.Visibility = Visibility.Visible;
                heldCurrentSlotFrame = true;
                missingPageBitmapCount++;
                _inkDiagnostics?.OnCrossPageUpdateEvent("hold", "neighbor-bitmap", $"page={pageIndex} mode=slot-hold");
            }
            else if (pageFrameDecision.CollapseSlot)
            {
                TryAssignFrameSource(img, null);
                img.Visibility = Visibility.Collapsed;
                missingPageBitmapCount++;
                var slotTag = slotPageChanged ? "reused" : "same";
                _inkDiagnostics?.OnCrossPageUpdateEvent("miss", "neighbor-bitmap", $"page={pageIndex} slot={slotTag}");
            }
            else if (img.Source is BitmapSource heldBitmap)
            {
                // Keep previous frame for the same page when decode/cache misses transiently.
                // This avoids one-frame black flicker in cross-page seams.
                bitmap = heldBitmap;
                img.Visibility = Visibility.Visible;
                _inkDiagnostics?.OnCrossPageUpdateEvent("hold", "neighbor-bitmap", $"page={pageIndex}");
            }
            else
            {
                img.Visibility = Visibility.Collapsed;
                missingPageBitmapCount++;
                _inkDiagnostics?.OnCrossPageUpdateEvent("miss", "neighbor-bitmap", $"page={pageIndex} slot=same");
            }
            if (heldCurrentSlotFrame)
            {
                // Keep existing page/ink transforms and uid until target page frame arrives.
                continue;
            }
            var inkImg = _neighborInkImages[i];
            if (bitmap != null)
            {
                var baseTop = top - _photoTranslate.Y;
                var pageScaleRatio = 1.0;
                if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
                {
                    var pageWidthDip = GetBitmapDisplayWidthInDip(bitmap);
                    if (pageWidthDip > 0)
                    {
                        pageScaleRatio = normalizedWidthDip / pageWidthDip;
                    }
                }
                img.Tag = baseTop;
                inkImg.Tag = baseTop;
                img.Uid = pageUid;
                inkImg.Uid = pageUid;
                // Bind slot identity/transform before ink bitmap replacement so async neighbor-ink
                // apply always targets the correct page slot without one-frame old-position flashes.
                ApplyNeighborSharedTransform(img, inkImg, pageScaleRatio, baseTop);
            }
            var baseHoldInkReplacement = CrossPageInteractiveInkFrameHoldPolicy.ShouldHoldReplacement(
                pageIndex,
                _interactiveSwitchPinnedNeighborPage,
                _interactiveSwitchPinnedNeighborInkHoldUntilUtc,
                nowUtc,
                hasCurrentInkFrame: inkImg.Source != null);
            var holdInkReplacement = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
                baseHoldInkReplacement,
                interactionActive,
                hasCurrentInkFrame: inkImg.Source != null,
                inkOperationActive: IsInkOperationActive());
            var usedPreservedInkFrame = false;
            if (bitmap != null)
            {
                if (inkImg.Source == null
                    && preservedInkFrames.TryGetValue(pageUid, out var preservedInkBitmap))
                {
                    TryAssignFrameSource(inkImg, preservedInkBitmap);
                    usedPreservedInkFrame = true;
                }

                if (!interactionActive)
                {
                    var inkBitmap = TryGetNeighborInkBitmap(pageIndex, bitmap);
                    var hasTargetInkStrokes = inkBitmap != null || HasNeighborInkStrokes(pageIndex);
                    var frameDecision = CrossPageNeighborInkFramePolicy.Resolve(
                        slotPageChanged,
                        hasCurrentInkFrame: inkImg.Source != null,
                        hasTargetInkStrokes,
                        holdInkReplacement,
                        usedPreservedInkFrame,
                        hasResolvedInkBitmap: inkBitmap != null);
                    if (CrossPageNeighborInkFramePolicy.ShouldClearWhenUnresolved(
                            frameDecision,
                            hasResolvedInkBitmap: inkBitmap != null))
                    {
                        // Slot remapped to a page without resolved target ink:
                        // clear stale old-page frame immediately to avoid ghost duplication.
                        TryAssignFrameSource(inkImg, null);
                    }
                    if (inkBitmap != null
                        && !holdInkReplacement
                        && (frameDecision.AllowResolvedInkReplacement
                            || !ReferenceEquals(inkImg.Source, inkBitmap)))
                    {
                        TryAssignFrameSource(inkImg, inkBitmap);
                    }
                    // Keep previous frame until replacement is ready to avoid one-frame flashing.
                    inkImg.Visibility = frameDecision.KeepVisible && inkImg.Source != null
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else
                {
                    // Keep previously rendered neighbor ink visible while dragging.
                    // This avoids the "ink disappears during pan" flicker without
                    // introducing expensive re-render work on each move event.
                    if (inkImg.Source == null)
                    {
                        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
                        if (!string.IsNullOrWhiteSpace(cacheKey)
                            && _neighborInkCache.TryGetValue(cacheKey, out var cachedEntry))
                        {
                            TryAssignFrameSource(inkImg, cachedEntry.Bitmap);
                        }
                    }
                    // In interactive cross-page drawing/erase, keep neighbor ink in sync
                    // so updates appear immediately without requiring zoom/pan.
                    var interactiveInkBitmap = ResolveNeighborInkBitmap(
                        pageIndex,
                        bitmap,
                        allowDeferredRender: false);
                    var shouldReplaceInteractiveInk = CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
                        hasResolvedInkBitmap: interactiveInkBitmap != null,
                        holdInkReplacement: holdInkReplacement,
                        hasCurrentInkFrame: inkImg.Source != null,
                        slotPageChanged: slotPageChanged);
                    if (shouldReplaceInteractiveInk)
                    {
                        TryAssignFrameSource(inkImg, interactiveInkBitmap);
                    }
                    else if (slotPageChanged)
                    {
                        var hasPreservedInkFrame = preservedInkFrames.TryGetValue(pageUid, out var remapPreservedInkBitmap);
                        var remapAction = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
                            slotPageChanged: true,
                            hasResolvedInkBitmap: interactiveInkBitmap != null,
                            hasCurrentInkFrame: inkImg.Source != null,
                            hasPreservedInkFrame: hasPreservedInkFrame,
                            inkOperationActive: IsInkOperationActive());
                        if (remapAction == CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame && hasPreservedInkFrame)
                        {
                            usedPreservedInkFrame = true;
                            TryAssignFrameSource(inkImg, remapPreservedInkBitmap);
                        }
                        else if (remapAction == CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame)
                        {
                            // Slot remapped to a different page and no preserved target frame available.
                            // Clear old-page ink to avoid cross-page ghost duplication.
                            TryAssignFrameSource(inkImg, null);
                        }
                        if (interactiveInkBitmap == null)
                        {
                            RequestDeferredNeighborInkRender(pageIndex, bitmap);
                        }
                    }
                    else if (CrossPageInteractiveInkClearPolicy.ShouldClearCurrentFrame(
                                 holdInkReplacement,
                                 HasNeighborInkStrokes(pageIndex),
                                 IsInkOperationActive(),
                                 interactionActive))
                    {
                        TryAssignFrameSource(inkImg, null);
                    }
                    else if (slotPageChanged && interactiveInkBitmap == null)
                    {
                        RequestDeferredNeighborInkRender(pageIndex, bitmap);
                    }
                    var interactionDecision = CrossPageNeighborInkFramePolicy.Resolve(
                        slotPageChanged,
                        hasCurrentInkFrame: inkImg.Source != null,
                        hasTargetInkStrokes: interactiveInkBitmap != null || HasNeighborInkStrokes(pageIndex),
                        holdInkReplacement,
                        usedPreservedInkFrame,
                        hasResolvedInkBitmap: interactiveInkBitmap != null);
                    inkImg.Visibility = interactionDecision.KeepVisible && inkImg.Source != null
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
            else
            {
                var noBitmapDecision = CrossPageNeighborInkFramePolicy.Resolve(
                    slotPageChanged,
                    hasCurrentInkFrame: inkImg.Source != null,
                    hasTargetInkStrokes: false,
                    holdInkReplacement,
                    usedPreservedInkFrame: false,
                    hasResolvedInkBitmap: false);
                if (noBitmapDecision.ClearCurrentFrame)
                {
                    TryAssignFrameSource(inkImg, null);
                }
                inkImg.Visibility = noBitmapDecision.KeepVisible && inkImg.Source != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        if (missingPageBitmapCount > 0)
        {
            ScheduleCrossPageDisplayUpdateForMissingNeighborPages(missingPageBitmapCount);
        }
    }

    private void UpdateNeighborTransformsForPan()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        if (_neighborPageImages.Count == 0 || _neighborInkImages.Count == 0)
        {
            return;
        }
        for (int i = 0; i < _neighborPageImages.Count; i++)
        {
            var img = _neighborPageImages[i];
            if (img.Visibility != Visibility.Visible || img.RenderTransform is not TransformGroup group)
            {
                continue;
            }
            if (img.Tag is not double baseTop || group.Children.Count < 2)
            {
                continue;
            }
            if (group.Children[1] is TranslateTransform translate)
            {
                translate.X = _photoTranslate.X;
                translate.Y = _photoTranslate.Y + baseTop;
            }
            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (inkImg.Visibility != Visibility.Visible || inkImg.RenderTransform is not TransformGroup inkGroup)
                {
                    continue;
                }
                if (inkGroup.Children.Count < 2)
                {
                    continue;
                }
                if (inkGroup.Children[1] is TranslateTransform inkTranslate)
                {
                    inkTranslate.X = _photoTranslate.X;
                    inkTranslate.Y = _photoTranslate.Y + baseTop;
                }
            }
        }
    }

    private void FinalizeCurrentPageFromScroll()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var viewportCenter = viewportHeight / 2;
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;

        // Determine which page contains the viewport center
        int newCurrentPage = currentPage;
        double newTranslateY = currentTop;

        if (currentTop > viewportCenter && currentPage > 1)
        {
            // Previous page is at center
            newCurrentPage = currentPage - 1;
            var newHeight = GetScaledHeightForPage(newCurrentPage, normalizedWidthDip);
            newTranslateY = currentTop - newHeight;
        }
        else if (currentBottom < viewportCenter && currentPage < totalPages)
        {
            // Next page is at center
            newCurrentPage = currentPage + 1;
            var newHeight = GetScaledHeightForPage(newCurrentPage, normalizedWidthDip);
            newTranslateY = currentTop + currentPageHeight;
        }

        if (newCurrentPage != currentPage)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            NavigateToPage(newCurrentPage, newTranslateY);
        }
        else
        {
            UpdateCrossPageDisplay();
        }
    }

    private void SyncCurrentPageToViewportCenter()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        for (int i = 0; i < 8; i++)
        {
            var beforePage = GetCurrentPageIndexForCrossPage();
            FinalizeCurrentPageFromScroll();
            if (beforePage == GetCurrentPageIndexForCrossPage())
            {
                break;
            }
        }
    }

    private void ClampSinglePageTranslateY(double viewportHeight)
    {
        var bitmap = PhotoBackground.Source as BitmapSource;
        if (bitmap == null)
        {
            return;
        }
        var pageHeight = GetScaledPageHeight(bitmap);
        double minY;
        double maxY;
        if (pageHeight <= viewportHeight)
        {
            var middle = (viewportHeight - pageHeight) * CrossPageViewportBoundsDefaults.CenterRatio;
            minY = middle;
            maxY = middle;
        }
        else
        {
            minY = viewportHeight - pageHeight;
            maxY = 0;
        }
        _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
    }

    private void ApplyCrossPageBoundaryLimits(bool includeSlack = true)
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null
            || !TryGetCrossPageBounds(currentBitmap, out _, out _, out var minY, out var maxY, out _, includeSlack))
        {
            return;
        }

        var originalY = _photoTranslate.Y;
        _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
        _crossPageTranslateClamped = CrossPageViewportBoundsPolicy.IsTranslateClamped(originalY, _photoTranslate.Y);
        UpdatePhotoInkClip();
    }

    private bool TryGetCrossPageBounds(
        BitmapSource currentBitmap,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY,
        out double normalizedWidthDip,
        bool includeSlack = true,
        bool preferCachedDuringInteraction = false)
    {
        minX = maxX = minY = maxY = 0;
        normalizedWidthDip = 0;

        if (!IsCrossPageDisplayActive())
        {
            return false;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return false;
        }

        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var viewportWidth = OverlayRoot.ActualWidth;
        if (viewportWidth <= 0)
        {
            viewportWidth = ActualWidth;
        }
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return false;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var allowSynchronousHeightResolve = CrossPageNeighborHeightResolvePolicy.ShouldAllowSynchronousResolve(
            interactionActive: preferCachedDuringInteraction,
            photoDocumentIsPdf: _photoDocumentIsPdf);
        if (TryResolveCachedCrossPageBounds(
                includeSlack,
                currentPage,
                totalPages,
                viewportWidth,
                viewportHeight,
                normalizedWidthDip,
                preferCachedDuringInteraction,
                out minX,
                out maxX,
                out minY,
                out maxY))
        {
            return true;
        }
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        if (currentPageHeight <= 0)
        {
            return false;
        }

        double totalHeightAbove = 0;
        for (int i = 1; i < currentPage; i++)
        {
            var height = GetNeighborPageHeightWithFallback(
                i,
                normalizedWidthDip,
                currentPageHeight,
                "bounds-above",
                allowSynchronousResolve: allowSynchronousHeightResolve);
            if (height > 0)
            {
                totalHeightAbove += height;
            }
        }
        double totalHeightBelow = 0;
        for (int i = currentPage + 1; i <= totalPages; i++)
        {
            var height = GetNeighborPageHeightWithFallback(
                i,
                normalizedWidthDip,
                currentPageHeight,
                "bounds-below",
                allowSynchronousResolve: allowSynchronousHeightResolve);
            if (height > 0)
            {
                totalHeightBelow += height;
            }
        }

        maxY = totalHeightAbove;
        minY = -(currentPageHeight + totalHeightBelow - viewportHeight);

        if (includeSlack)
        {
            var slack = CrossPageViewportBoundsPolicy.ResolveSlackDip(viewportHeight);
            if (currentPage > 1)
            {
                maxY += slack;
            }
            if (currentPage < totalPages)
            {
                minY -= slack;
            }
        }
        if (minY > maxY)
        {
            var middle = (minY + maxY) * CrossPageViewportBoundsDefaults.CenterRatio;
            minY = middle;
            maxY = middle;
        }

        if (normalizedWidthDip > 0)
        {
            var scaledWidth = normalizedWidthDip * _photoScale.ScaleX;
            var xRange = PhotoHorizontalPanRangePolicy.Resolve(
                viewportWidth,
                scaledWidth,
                includeSlack);
            minX = xRange.MinX;
            maxX = xRange.MaxX;
        }
        else
        {
            minX = -1_000_000;
            maxX = 1_000_000;
        }

        _crossPageBoundsCacheValid = true;
        _crossPageBoundsCacheIncludeSlack = includeSlack;
        _crossPageBoundsCacheCurrentPage = currentPage;
        _crossPageBoundsCacheTotalPages = totalPages;
        _crossPageBoundsCacheViewportWidth = viewportWidth;
        _crossPageBoundsCacheViewportHeight = viewportHeight;
        _crossPageBoundsCacheNormalizedWidthDip = normalizedWidthDip;
        _crossPageBoundsCacheScaleX = _photoScale.ScaleX;
        _crossPageBoundsCacheScaleY = _photoScale.ScaleY;
        _crossPageBoundsCacheMinX = minX;
        _crossPageBoundsCacheMaxX = maxX;
        _crossPageBoundsCacheMinY = minY;
        _crossPageBoundsCacheMaxY = maxY;
        _crossPageBoundsCacheUpdatedUtc = GetCurrentUtcTimestamp();

        return true;
    }

    private void RequestCrossPageDisplayUpdate(string source = CrossPageUpdateSources.Unspecified)
    {
        var admissionDecision = CrossPageRequestAdmissionPolicy.Resolve(
            crossPageDisplayActive: IsCrossPageDisplayActive(),
            photoLoading: _photoLoading,
            hasPhotoBackgroundSource: PhotoBackground.Source != null,
            overlayVisible: IsVisible,
            overlayMinimized: WindowState == WindowState.Minimized,
            hasUsableViewport: OverlayRoot.ActualWidth > 0 && OverlayRoot.ActualHeight > 0);
        if (!admissionDecision.ShouldAdmit)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent(
                "skip",
                source,
                CrossPageRequestAdmissionReasonPolicy.ResolveDiagnosticTag(admissionDecision.Reason));
            return;
        }

        var request = CrossPageUpdateRequestContextFactory.Create(source);
        source = request.Source;
        var nowUtc = GetCurrentUtcTimestamp();
        var duplicateDecision = CrossPageDuplicateWindowPolicy.Resolve(
                request,
                _crossPageUpdateRequestState,
                nowUtc);
        if (duplicateDecision.ShouldSkip)
        {
            var replayQueueDecision = CrossPageDuplicateSkipReplayQueuePolicy.Resolve(
                duplicateDecision,
                request.Kind,
                source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                replayQueueDecision);
            var reason = CrossPageDuplicateWindowReasonPolicy.ResolveDiagnosticTag(duplicateDecision.Reason);
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, reason);
            return;
        }

        CrossPageUpdateRequestStateUpdater.ApplyAcceptedRequest(
            ref _crossPageUpdateRequestState,
            request,
            nowUtc);
        var dispatchSnapshot = CrossPageDisplayUpdateDispatchSnapshotPolicy.Resolve(
            pending: _crossPageDisplayUpdateState.Pending,
            panning: _photoPanning || _photoManipulating,
            dragging: _crossPageDragging,
            inkOperationActive: IsInkOperationActive());
        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "request",
            source,
            CrossPageDisplayUpdateDispatchSnapshot.FormatDiagnosticsTag(dispatchSnapshot));
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("crosspage-update-enter");
        }
        var elapsedMs = (nowUtc - _crossPageDisplayUpdateClockState.LastUpdateUtc).TotalMilliseconds;
        var dispatchDecision = CrossPageDisplayUpdateDispatchDecisionPolicy.Resolve(
            dispatchSnapshot,
            elapsedMs,
            draggingMinIntervalMs: CrossPageRuntimeDefaults.DraggingUpdateMinIntervalMs,
            normalMinIntervalMs: CrossPageRuntimeDefaults.UpdateMinIntervalMs);
        var sourceSuffix = CrossPageUpdateSourceParser.Parse(source).Suffix;
        dispatchDecision = CrossPageImmediateDispatchPolicy.Resolve(dispatchDecision, sourceSuffix);
        dispatchDecision = CrossPagePendingTakeoverPolicy.Resolve(
            dispatchDecision,
            sourceSuffix,
            _crossPageDisplayUpdateState,
            nowUtc);
        if (dispatchDecision.Mode == CrossPageDisplayUpdateDispatchMode.SkipPending)
        {
            var replayQueueDecision = CrossPageReplayQueuePolicy.Resolve(request.Kind, source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                replayQueueDecision);
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, "pending");
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("crosspage-update-skip", "pending");
            }
            return;
        }
        if (dispatchDecision.Mode == CrossPageDisplayUpdateDispatchMode.Delayed)
        {
            var token = CrossPageDisplayUpdatePendingStateUpdater.MarkDelayedScheduled(
                ref _crossPageDisplayUpdateState,
                nowUtc);
            var delay = dispatchDecision.DelayMs;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var detail = CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatDelayFailureDetail(
                        ex.GetType().Name);
                    var scheduledRecovery = TryBeginInvoke(() =>
                    {
                        RecoverCrossPageDelayedDispatchFailure(
                            request.Kind,
                            source,
                            token,
                            detail);
                    }, DispatcherPriority.Background);
                    var recoveryDecision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
                        recoveryDispatchScheduled: scheduledRecovery,
                        dispatcherCheckAccess: Dispatcher.CheckAccess(),
                        dispatcherShutdownStarted: Dispatcher.HasShutdownStarted,
                        dispatcherShutdownFinished: Dispatcher.HasShutdownFinished);
                    if (recoveryDecision.ShouldRecoverInline)
                    {
                        var tokenMatched = CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                            _crossPageDisplayUpdateState,
                            token);
                        RecoverCrossPageDelayedDispatchFailure(
                            request.Kind,
                            source,
                            token,
                            CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatInlineRecoveryDetail(
                                tokenMatched));
                    }
                    return;
                }
                var scheduled = TryBeginInvoke(() =>
                {
                    if (!CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                            _crossPageDisplayUpdateState,
                            token))
                    {
                        return;
                    }
                    CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
                    ExecuteCrossPageDisplayUpdateRun(
                        source,
                        mode: "delayed",
                        emitAbortDiagnostics: false);
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
                    HandleCrossPageDisplayUpdateDispatchFailure(
                        request.Kind,
                        source,
                        mode: "delayed",
                        emitAbortDiagnostics: false);
                }
            });
            return;
        }
        CrossPageDisplayUpdatePendingStateUpdater.MarkDirectScheduled(ref _crossPageDisplayUpdateState, nowUtc);
        var directScheduled = TryBeginInvoke(() =>
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            ExecuteCrossPageDisplayUpdateRun(
                source,
                mode: "direct",
                emitAbortDiagnostics: true);
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                request.Kind,
                source,
                mode: "direct",
                emitAbortDiagnostics: true);
        }
    }

    private void RecoverCrossPageDelayedDispatchFailure(
        CrossPageUpdateSourceKind kind,
        string source,
        int token,
        string detail)
    {
        if (!CrossPageDisplayUpdatePendingStateUpdater.IsTokenMatched(
                _crossPageDisplayUpdateState,
                token))
        {
            return;
        }

        CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
        var replayQueueDecision = CrossPageReplayQueuePolicy.Resolve(kind, source);
        CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
            ref _crossPageReplayState,
            replayQueueDecision);
        _inkDiagnostics?.OnCrossPageUpdateEvent("recover", source, detail);
    }

    private void HandleCrossPageDisplayUpdateDispatchFailure(
        CrossPageUpdateSourceKind kind,
        string source,
        string mode,
        bool emitAbortDiagnostics)
    {
        _ = CrossPageDisplayUpdateDispatchFailureCoordinator.Apply(
            ref _crossPageReplayState,
            kind,
            source,
            mode,
            emitAbortDiagnostics,
            executeCrossPageDisplayUpdateRun: ExecuteCrossPageDisplayUpdateRun,
            emitDiagnostics: (action, eventSource, detail) => _inkDiagnostics?.OnCrossPageUpdateEvent(action, eventSource, detail),
            flushCrossPageReplay: TryFlushCrossPageReplay,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished);
    }

    private void ExecuteCrossPageDisplayUpdateRun(
        string source,
        string mode,
        bool emitAbortDiagnostics)
    {
        var runGate = CrossPageDisplayRunGatePolicy.Resolve(IsCrossPageDisplayActive());
        if (!runGate.ShouldRun)
        {
            if (emitAbortDiagnostics)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent(
                    "abort",
                    source,
                    runGate.AbortReason ?? CrossPageDeferredDiagnosticReason.Inactive);
            }
            return;
        }

        CrossPageDisplayUpdateClockStateUpdater.MarkUpdated(
            ref _crossPageDisplayUpdateClockState,
            GetCurrentUtcTimestamp());
        _inkDiagnostics?.OnCrossPageUpdateEvent("run", source, $"mode={mode}");
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("crosspage-update-run", mode);
        }
        try
        {
            UpdateCrossPageDisplay();
            TryFlushCrossPageReplay();
        }
        catch (Exception ex)
        {
            var replayQueueDecision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(source);
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                replayQueueDecision);
            _inkDiagnostics?.OnCrossPageUpdateEvent(
                "recover",
                source,
                $"run-failed ex={ex.GetType().Name}");
        }
    }

    private void ApplyNeighborSharedTransform(
        WpfImage pageImage,
        WpfImage inkImage,
        double pageScaleRatio,
        double baseTop)
    {
        if (pageImage.RenderTransform is TransformGroup existing
            && existing.Children.Count >= 2
            && existing.Children[0] is ScaleTransform existingScale
            && existing.Children[1] is TranslateTransform existingTranslate)
        {
            existingScale.ScaleX = _photoScale.ScaleX * pageScaleRatio;
            existingScale.ScaleY = _photoScale.ScaleY * pageScaleRatio;
            existingTranslate.X = _photoTranslate.X;
            existingTranslate.Y = _photoTranslate.Y + baseTop;
            if (!ReferenceEquals(inkImage.RenderTransform, existing))
            {
                inkImage.RenderTransform = existing;
            }
            return;
        }

        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(_photoScale.ScaleX * pageScaleRatio, _photoScale.ScaleY * pageScaleRatio));
        transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));
        pageImage.RenderTransform = transform;
        inkImage.RenderTransform = transform;
    }

    private void TryFlushCrossPageReplay()
    {
        var flushPlan = CrossPageReplayFlushCoordinator.Resolve(
            replayState: _crossPageReplayState,
            crossPageUpdatePending: _crossPageDisplayUpdateState.Pending,
            photoModeActive: _photoModeActive,
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            interactionActive: IsCrossPageInteractionActive());
        if (!flushPlan.ShouldFlush || !flushPlan.HasDispatchTarget)
        {
            return;
        }

        TryScheduleCrossPageReplayDispatch(flushPlan.DispatchTarget);
    }

    private bool HasCrossPageReplayPending()
    {
        return CrossPageReplayPendingStateUpdater.HasPending(_crossPageReplayState);
    }

    private void TryFlushCrossPageReplayAfterPointerUp()
    {
        // Pointer-up is the earliest stable point where interaction can end.
        // Triggering replay flush here avoids "old page refreshes only after next pan".
        TryFlushCrossPageReplay();
    }

    private void TryScheduleCrossPageReplayDispatch(CrossPageReplayDispatchTarget target)
    {
        _ = CrossPageReplayDispatchCoordinator.Apply(
            ref _crossPageReplayState,
            target,
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate,
            tryBeginInvoke: TryBeginInvoke,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished);
    }

    private void ResetCrossPageReplayState()
    {
        CrossPageReplayPendingStateUpdater.Reset(
            ref _crossPageReplayState);
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
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            ScheduleNeighborInkSidecarLoad(cacheKey, pageIndex);
        }
        if (!_photoCache.TryGet(cacheKey, out strokes) || strokes.Count == 0)
        {
            if (_neighborInkCache.TryGetValue(cacheKey, out var cachedEntry))
            {
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
                var bitmap = _inkStrokeRenderer.RenderPage(
                    page,
                    pageBitmap.PixelWidth,
                    pageBitmap.PixelHeight,
                    pageBitmap.DpiX,
                    pageBitmap.DpiY);
                if (!_inkShowEnabled || !_inkCacheEnabled)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(pageIndex, currentStrokes, bitmap);
                TrimNeighborInkCache(pageIndex);
                // Prefer in-place slot replacement to avoid a full cross-page refresh flash.
                if (TryApplyNeighborInkBitmapToVisibleSlot(pageIndex, bitmap))
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

    private bool TryApplyNeighborInkBitmapToVisibleSlot(int pageIndex, BitmapSource inkBitmap)
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
            inkImg.Visibility = Visibility.Visible;
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
            TryApplyNeighborInkBitmapToVisibleSlot(pageIndex, entry.Bitmap);
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
        var bitmap = _inkStrokeRenderer.RenderPage(
            page,
            pageBitmap.PixelWidth,
            pageBitmap.PixelHeight,
            pageBitmap.DpiX,
            pageBitmap.DpiY);
        _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(pageIndex, strokes, bitmap);
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










