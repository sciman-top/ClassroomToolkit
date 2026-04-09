using System;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
}