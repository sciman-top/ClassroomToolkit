using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
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
            var prevHeight = GetScaledHeightForPage(pageIndex, normalizedWidthDip);
            if (prevHeight <= 0)
            {
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
            var nextHeight = GetScaledHeightForPage(pageIndex, normalizedWidthDip);
            if (nextHeight <= 0)
            {
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
            ScheduleNeighborImagePrefetch(currentPage - 1);
            ScheduleNeighborImagePrefetch(currentPage + 1);
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
        if (_photoDocumentIsPdf)
        {
            return GetScaledPdfPageHeight(pageIndex);
        }
        return GetScaledPageHeight(GetPageBitmap(pageIndex), normalizedWidthDip);
    }

    private void RenderNeighborPages(List<(int PageIndex, double Top)> neighborPages)
    {
        if (neighborPages.Count == 0)
        {
            ClearNeighborPages();
            return;
        }
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
            var bitmap = GetNeighborPageBitmapForRender(pageIndex);
            var img = _neighborPageImages[i];
            img.Source = bitmap;
            img.Visibility = bitmap != null ? Visibility.Visible : Visibility.Collapsed;
            var inkImg = _neighborInkImages[i];
            if (bitmap != null && !_photoPanning && !_crossPageDragging)
            {
                var inkBitmap = TryGetNeighborInkBitmap(pageIndex, bitmap);
                inkImg.Source = inkBitmap;
                inkImg.Visibility = inkBitmap != null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                inkImg.Source = null;
                inkImg.Visibility = Visibility.Collapsed;
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
                // Apply same transform as current page
                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(_photoScale.ScaleX * pageScaleRatio, _photoScale.ScaleY * pageScaleRatio));
                transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));
                img.RenderTransform = transform;
                inkImg.RenderTransform = transform;
            }
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
            var height = GetScaledHeightForPage(i, normalizedWidthDip);
            if (height > 0)
            {
                totalHeightAbove += height;
            }
        }
        double totalHeightBelow = 0;
        for (int i = currentPage + 1; i <= totalPages; i++)
        {
            var height = GetScaledHeightForPage(i, normalizedWidthDip);
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

    private void RequestCrossPageDisplayUpdate()
    {
        if (_crossPageUpdatePending)
        {
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
                return;
            }
            _lastCrossPageUpdateUtc = DateTime.UtcNow;
            UpdateCrossPageDisplay();
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _crossPageUpdatePending = false;
        }
    }

        private BitmapSource? TryGetNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap)
    {
        if (!_inkCacheEnabled || pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
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
            _neighborInkCache.Remove(cacheKey);
            return null;
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var entry) && ReferenceEquals(entry.Strokes, strokes))
        {
            return entry.Bitmap;
        }
        ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
        return null;
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
                _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(currentStrokes, bitmap);
                RequestCrossPageDisplayUpdate();
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
}
