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
    private const int CrossPagePostInputRefreshDelayMs = 420;
    private const int NeighborPagesClearGraceMs = 180;
    private const int InteractiveSwitchInkCacheLimit = 3;

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
        _lastNeighborPagesNonEmptyUtc = DateTime.MinValue;
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
        _interactiveSwitchInkCache.Clear();
        _neighborPageHeightCache.Clear();
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
        var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: _crossPageDisplayEnabled);
        if (bitmap == null && _crossPageDisplayEnabled)
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

    private BitmapSource? GetNeighborPageBitmapForRender(int pageIndex)
    {
        if (!_photoDocumentIsPdf)
        {
            return GetPageBitmap(pageIndex);
        }

        // In cross-page mode, a cache miss can leave a large blank gap between pages.
        // Fallback to direct render once so visible neighbors are always drawable.
        if (TryGetCachedPdfPageBitmap(pageIndex, out var cached))
        {
            return cached;
        }
        return GetPdfPageBitmap(pageIndex);
    }
    
    private void ScheduleNeighborImagePrefetch(int pageIndex)
    {
        if (!_photoModeActive || _photoDocumentIsPdf)
        {
            return;
        }
        if (!_crossPageDisplayEnabled && (_photoPanning || _crossPageDragging))
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
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!_photoModeActive || _photoDocumentIsPdf || _crossPageDragging)
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
    }

    private double GetCrossPageNormalizedWidthDip(BitmapSource? fallback = null)
    {
        if (!_crossPageDisplayEnabled || _photoDocumentIsPdf)
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
        if (!_crossPageDisplayEnabled || _photoDocumentIsPdf)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var source = bitmap ?? PhotoBackground.Source as BitmapSource;
        if (source == null)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(source);
        var pageWidthDip = GetBitmapDisplayWidthInDip(source);
        if (normalizedWidthDip <= 0 || pageWidthDip <= 0)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var ratio = normalizedWidthDip / pageWidthDip;
        _photoPageScale.ScaleX = ratio;
        _photoPageScale.ScaleY = ratio;
    }

    private void UpdateCrossPageDisplay()
    {
        if (!_crossPageDisplayEnabled || !_photoModeActive)
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
        const double visibilityMargin = 2.0;
        var visiblePages = new List<(int PageIndex, double Top)>
        {
            (currentPage, currentTop)
        };

        var prevTop = currentTop;
        for (int pageIndex = currentPage - 1; pageIndex >= 1; pageIndex--)
        {
            var prevHeight = GetNeighborPageHeightWithFallback(pageIndex, normalizedWidthDip, currentPageHeight, "prev");
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
            var nextHeight = GetNeighborPageHeightWithFallback(pageIndex, normalizedWidthDip, currentPageHeight, "next");
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
                .Where(p => !TryGetCachedPdfPageBitmap(p, out _))
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
        var neighborPages = visiblePages.Where(p => p.PageIndex != currentPage).ToList();
        if (_crossPageDragging && _crossPageTranslateClamped && neighborPages.Count == 0)
        {
            return;
        }
        RenderNeighborPages(neighborPages);
    }

    private double GetScaledHeightForPage(int pageIndex, double normalizedWidthDip = 0)
    {
        if (pageIndex <= 0)
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
        if (_neighborPageHeightCache.TryGetValue(pageIndex, out var cachedHeight) && cachedHeight > 0)
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

    private double GetNeighborPageHeightWithFallback(int pageIndex, double normalizedWidthDip, double preferredHeight, string direction)
    {
        var height = GetScaledHeightForPage(pageIndex, normalizedWidthDip);
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
        if (neighborPages.Count == 0)
        {
            var elapsedSinceNonEmptyMs = (DateTime.UtcNow - _lastNeighborPagesNonEmptyUtc).TotalMilliseconds;
            var hasVisibleNeighborFrame = _neighborPageImages.Any(img => img.Visibility == Visibility.Visible && img.Source != null);
            if (hasVisibleNeighborFrame
                && !_photoPanning
                && !_crossPageDragging
                && _lastNeighborPagesNonEmptyUtc != DateTime.MinValue
                && elapsedSinceNonEmptyMs < NeighborPagesClearGraceMs)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("hold", "neighbor-pages", $"reason=grace elapsedMs={elapsedSinceNonEmptyMs:F0}");
                return;
            }
            ClearNeighborPages();
            return;
        }
        _lastNeighborPagesNonEmptyUtc = DateTime.UtcNow;
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
            BitmapSource? bitmap = GetNeighborPageBitmapForRender(pageIndex);
            var img = _neighborPageImages[i];
            var slotPageChanged = !string.Equals(img.Uid, pageUid, StringComparison.Ordinal);
            if (bitmap != null)
            {
                img.Source = bitmap;
                img.Visibility = Visibility.Visible;
            }
            else if (slotPageChanged)
            {
                img.Source = null;
                img.Visibility = Visibility.Collapsed;
                missingPageBitmapCount++;
                _inkDiagnostics?.OnCrossPageUpdateEvent("miss", "neighbor-bitmap", $"page={pageIndex} slot=reused");
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
            var inkImg = _neighborInkImages[i];
            if (bitmap != null)
            {
                if (!_photoPanning && !_crossPageDragging)
                {
                    var inkBitmap = TryGetNeighborInkBitmap(pageIndex, bitmap);
                    if (inkBitmap != null)
                    {
                        inkImg.Source = inkBitmap;
                    }
                    else if (slotPageChanged)
                    {
                        // Slot is reused by another page: stale ink bitmap would be rendered
                        // with the new page transform and appear scaled/shifted.
                        inkImg.Source = null;
                    }
                    // Keep previous frame until replacement is ready to avoid one-frame flashing.
                    inkImg.Visibility = inkImg.Source != null ? Visibility.Visible : Visibility.Collapsed;
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
                            inkImg.Source = cachedEntry.Bitmap;
                        }
                    }
                    if (slotPageChanged && inkImg.Source == null)
                    {
                        inkImg.Source = null;
                    }
                    inkImg.Visibility = inkImg.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            else
            {
                if (slotPageChanged)
                {
                    inkImg.Source = null;
                }
                inkImg.Visibility = inkImg.Source != null ? Visibility.Visible : Visibility.Collapsed;
            }
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
                // Apply same transform as current page
                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(_photoScale.ScaleX * pageScaleRatio, _photoScale.ScaleY * pageScaleRatio));
                transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));
                img.RenderTransform = transform;
                inkImg.RenderTransform = transform;
            }
        }

        if (missingPageBitmapCount > 0)
        {
            ScheduleCrossPageDisplayUpdateForMissingNeighborPages(missingPageBitmapCount);
        }
    }

    private void UpdateNeighborTransformsForPan()
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
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
        if (!_crossPageDisplayEnabled)
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
        if (!_crossPageDisplayEnabled)
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
            var middle = (viewportHeight - pageHeight) * 0.5;
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
        if (!_crossPageDisplayEnabled || !_photoModeActive)
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
        _crossPageTranslateClamped = Math.Abs(originalY - _photoTranslate.Y) > 0.5;
    }

    private bool TryGetCrossPageBounds(
        BitmapSource currentBitmap,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY,
        out double normalizedWidthDip,
        bool includeSlack = true)
    {
        minX = maxX = minY = maxY = 0;
        normalizedWidthDip = 0;

        if (!_crossPageDisplayEnabled || !_photoModeActive)
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
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        if (currentPageHeight <= 0)
        {
            return false;
        }

        double totalHeightAbove = 0;
        for (int i = 1; i < currentPage; i++)
        {
            var height = GetNeighborPageHeightWithFallback(i, normalizedWidthDip, currentPageHeight, "bounds-above");
            if (height > 0)
            {
                totalHeightAbove += height;
            }
        }
        double totalHeightBelow = 0;
        for (int i = currentPage + 1; i <= totalPages; i++)
        {
            var height = GetNeighborPageHeightWithFallback(i, normalizedWidthDip, currentPageHeight, "bounds-below");
            if (height > 0)
            {
                totalHeightBelow += height;
            }
        }

        maxY = totalHeightAbove;
        minY = -(currentPageHeight + totalHeightBelow - viewportHeight);

        if (includeSlack)
        {
            var slack = Math.Max(32.0, viewportHeight * 0.5);
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
            var middle = (minY + maxY) * 0.5;
            minY = middle;
            maxY = middle;
        }

        if (normalizedWidthDip > 0)
        {
            var scaledWidth = normalizedWidthDip * _photoScale.ScaleX;
            var horizontalSlack = Math.Max(48.0, viewportWidth * 0.08);
            if (scaledWidth <= viewportWidth)
            {
                var centerX = (viewportWidth - scaledWidth) * 0.5;
                minX = centerX - horizontalSlack;
                maxX = centerX + horizontalSlack;
            }
            else
            {
                minX = (viewportWidth - scaledWidth) - horizontalSlack;
                maxX = horizontalSlack;
            }
        }
        else
        {
            minX = -1_000_000;
            maxX = 1_000_000;
        }

        return true;
    }

    private void RequestCrossPageDisplayUpdate(string source = "unspecified")
    {
        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "request",
            source,
            $"pending={_crossPageUpdatePending} panning={_photoPanning} dragging={_crossPageDragging}");
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("crosspage-update-enter");
        }
        if (_crossPageUpdatePending)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, "pending");
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("crosspage-update-skip", "pending");
            }
            return;
        }
        var nowUtc = DateTime.UtcNow;
        var throttleActive = _photoPanning || _crossPageDragging;
        var elapsedMs = (nowUtc - _lastCrossPageUpdateUtc).TotalMilliseconds;
        if (throttleActive && elapsedMs < CrossPageUpdateMinIntervalMs)
        {
            _crossPageUpdatePending = true;
            var token = Interlocked.Increment(ref _crossPageUpdateToken);
            var delay = Math.Max(1, (int)Math.Ceiling(CrossPageUpdateMinIntervalMs - elapsedMs));
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _crossPageUpdateToken)
                    {
                        return;
                    }
                    _crossPageUpdatePending = false;
                    if (!_photoModeActive || !_crossPageDisplayEnabled)
                    {
                        return;
                    }
                    _lastCrossPageUpdateUtc = DateTime.UtcNow;
                    _inkDiagnostics?.OnCrossPageUpdateEvent("run", source, "mode=delayed");
                    if (IsCrossPageFirstInputTraceActive())
                    {
                        MarkCrossPageFirstInputStage("crosspage-update-run", "delayed");
                    }
                    UpdateCrossPageDisplay();
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    _crossPageUpdatePending = false;
                }
            });
            return;
        }
        _crossPageUpdatePending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            _crossPageUpdatePending = false;
            if (!_photoModeActive || !_crossPageDisplayEnabled)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("abort", source, "inactive");
                return;
            }
            _lastCrossPageUpdateUtc = DateTime.UtcNow;
            _inkDiagnostics?.OnCrossPageUpdateEvent("run", source, "mode=direct");
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("crosspage-update-run", "direct");
            }
            UpdateCrossPageDisplay();
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _crossPageUpdatePending = false;
        }
    }

        private BitmapSource? TryGetNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap)
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
        ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
        if (_neighborInkCache.TryGetValue(cacheKey, out var staleEntry))
        {
            return staleEntry.Bitmap;
        }
        return null;
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
                if (!_photoModeActive || !_crossPageDisplayEnabled)
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
                    ScheduleCrossPageDisplayUpdateAfterInputSettles("neighbor-sidecar", singlePerPointerUp: true);
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
                if (!_photoModeActive || !_crossPageDisplayEnabled)
                {
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
                _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(pageIndex, currentStrokes, bitmap);
                TrimNeighborInkCache(pageIndex);
                // Prefer in-place slot replacement to avoid a full cross-page refresh flash.
                if (TryApplyNeighborInkBitmapToVisibleSlot(pageIndex, bitmap))
                {
                    _inkDiagnostics?.OnCrossPageUpdateEvent("apply", "neighbor-render", $"page={pageIndex}");
                }
                else
                {
                    ScheduleCrossPageDisplayUpdateAfterInputSettles("neighbor-render");
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
        string source = "post-input",
        bool singlePerPointerUp = false,
        int? delayOverrideMs = null)
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent("defer-skip", source, "inactive");
            return;
        }

        if (IsInkOperationActive())
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent("defer-skip", source, "ink-active");
            return;
        }

        var targetDelayMs = delayOverrideMs.GetValueOrDefault(CrossPagePostInputRefreshDelayMs);
        if (targetDelayMs <= 0)
        {
            targetDelayMs = CrossPagePostInputRefreshDelayMs;
        }

        var elapsedMs = (DateTime.UtcNow - _lastCrossPagePointerUpUtc).TotalMilliseconds;
        if (elapsedMs >= targetDelayMs || _lastCrossPagePointerUpUtc == DateTime.MinValue)
        {
            if (singlePerPointerUp && !TryAcquirePostInputRefreshSlot(out var seqImmediate))
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("defer-skip", source, $"already-refreshed seq={seqImmediate}");
                return;
            }
            RequestCrossPageDisplayUpdate($"{source}-immediate");
            return;
        }

        var delay = Math.Max(1, (int)Math.Ceiling(targetDelayMs - elapsedMs));
        _inkDiagnostics?.OnCrossPageUpdateEvent("defer-schedule", source, $"delayMs={delay}");
        var token = Interlocked.Increment(ref _crossPagePostInputRefreshToken);
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            TryBeginInvoke(() =>
            {
                if (token != _crossPagePostInputRefreshToken)
                {
                    return;
                }
                if (!_photoModeActive || !_crossPageDisplayEnabled || IsInkOperationActive())
                {
                    _inkDiagnostics?.OnCrossPageUpdateEvent("defer-abort", source, "inactive-or-ink-active");
                    return;
                }
                if (singlePerPointerUp && !TryAcquirePostInputRefreshSlot(out var seqDelayed))
                {
                    _inkDiagnostics?.OnCrossPageUpdateEvent("defer-skip", source, $"already-refreshed seq={seqDelayed}");
                    return;
                }
                RequestCrossPageDisplayUpdate($"{source}-delayed");
            }, DispatcherPriority.Background);
        });
    }

    private void ApplyCrossPagePointerUpFastRefresh(string source = "pointer-up-fast")
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        // Fast path: keep current/neighbor transforms coherent immediately.
        UpdateNeighborTransformsForPan();
        _inkDiagnostics?.OnCrossPageUpdateEvent("run", source, "mode=fast-current");
    }

    private bool TryAcquirePostInputRefreshSlot(out long pointerUpSequence)
    {
        pointerUpSequence = Interlocked.Read(ref _crossPagePointerUpSequence);
        if (_lastCrossPagePointerUpUtc == DateTime.MinValue)
        {
            return true;
        }

        while (true)
        {
            var appliedSequence = Interlocked.Read(ref _crossPagePostInputRefreshAppliedSequence);
            if (appliedSequence == pointerUpSequence)
            {
                return false;
            }

            var exchanged = Interlocked.CompareExchange(
                ref _crossPagePostInputRefreshAppliedSequence,
                pointerUpSequence,
                appliedSequence);
            if (exchanged == appliedSequence)
            {
                return true;
            }
        }
    }

    private bool TryApplyNeighborInkBitmapToVisibleSlot(int pageIndex, BitmapSource inkBitmap)
    {
        if (_neighborInkImages.Count == 0)
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

            inkImg.Source = inkBitmap;
            inkImg.Visibility = Visibility.Visible;
            return true;
        }

        return false;
    }

    private void ScheduleCrossPageDisplayUpdateForMissingNeighborPages(int missingCount)
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled || missingCount <= 0)
        {
            return;
        }

        var elapsedMs = (DateTime.UtcNow - _lastCrossPageMissingBitmapRefreshUtc).TotalMilliseconds;
        if (elapsedMs < 140)
        {
            return;
        }

        _lastCrossPageMissingBitmapRefreshUtc = DateTime.UtcNow;
        _inkDiagnostics?.OnCrossPageUpdateEvent("defer-schedule", "neighbor-missing", $"count={missingCount}");
        var token = Interlocked.Increment(ref _crossPageMissingBitmapRefreshToken);
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(120).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            TryBeginInvoke(() =>
            {
                if (token != _crossPageMissingBitmapRefreshToken)
                {
                    return;
                }
                if (!_photoModeActive || !_crossPageDisplayEnabled)
                {
                    return;
                }
                RequestCrossPageDisplayUpdate("neighbor-missing-delayed");
            }, DispatcherPriority.Background);
        });
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
        _interactiveSwitchInkCache.Remove(cacheKey);
    }

    private void RememberInteractiveSwitchInkBitmap(string cacheKey, int pageIndex, BitmapSource bitmap)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || pageIndex <= 0)
        {
            return;
        }
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            return;
        }

        _interactiveSwitchInkCache[cacheKey] = new InkBitmapCacheEntry(pageIndex, strokes, bitmap);
        TrimInteractiveSwitchInkCache(pageIndex);
    }

    private void TrimInteractiveSwitchInkCache(int anchorPageIndex)
    {
        if (_interactiveSwitchInkCache.Count <= InteractiveSwitchInkCacheLimit)
        {
            return;
        }

        var keysToRemove = _interactiveSwitchInkCache
            .OrderBy(pair => Math.Abs(pair.Value.PageIndex - anchorPageIndex))
            .Skip(InteractiveSwitchInkCacheLimit)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _interactiveSwitchInkCache.Remove(key);
        }
    }

    private int GetNeighborPrefetchRadius()
    {
        if (!_crossPageDisplayEnabled || _photoDocumentIsPdf)
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
