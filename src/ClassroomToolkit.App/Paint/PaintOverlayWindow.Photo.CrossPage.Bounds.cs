using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
            if (img.Visibility != Visibility.Visible)
            {
                continue;
            }
            if (img.Tag is not double baseTop)
            {
                continue;
            }
            var pageTransform = EnsureNeighborTransform(img);
            pageTransform.Translate.X = _photoTranslate.X;
            pageTransform.Translate.Y = _photoTranslate.Y + baseTop;
            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (inkImg.Visibility != Visibility.Visible)
                {
                    continue;
                }
                var inkTag = ResolveNeighborInkSlotTag(inkImg.Tag, baseTop);
                var inkTransform = EnsureNeighborTransform(inkImg);
                inkTransform.Translate.X = _photoTranslate.X - (inkTag.HorizontalOffsetDip * inkTransform.Scale.ScaleX);
                inkTransform.Translate.Y = _photoTranslate.Y + inkTag.BaseTop;
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
}
