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
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{    private void UpdateCrossPageDisplay()
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
            ClearNeighborPages();
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            ClearNeighborPages();
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
            ClearNeighborPages();
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
        if (Volatile.Read(ref _overlayClosed) != 0)
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", source, "overlay-closed");
            return;
        }

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
            var lifecycleToken = _overlayLifecycleCancellation.Token;
            _ = SafeTaskRunner.Run(
                "PaintOverlayWindow.CrossPageDisplayUpdate.Delayed",
                async cancellationToken =>
                {
                    var delayOutcome = await TryAwaitCrossPageDelayedDispatchAsync(
                        delay,
                        cancellationToken).ConfigureAwait(false);
                    if (!delayOutcome.ShouldContinue)
                    {
                        if (string.IsNullOrWhiteSpace(delayOutcome.FailureDetail))
                        {
                            HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
                                request.Kind,
                                source,
                                mode: "delayed-canceled",
                                emitAbortDiagnostics: false,
                                abortDetail: "delayed-canceled-dispatch-unavailable");
                            return;
                        }

                        var detail = delayOutcome.FailureDetail;
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
                        HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
                            request.Kind,
                            source,
                            mode: "delayed",
                            emitAbortDiagnostics: false,
                            abortDetail: "delayed-dispatch-unavailable");
                    }
                },
                lifecycleToken,
                onError: ex =>
                {
                    HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
                        request.Kind,
                        source,
                        mode: "delayed-task-fault",
                        emitAbortDiagnostics: false,
                        abortDetail: "delayed-task-fault-dispatch-unavailable");
                    Debug.WriteLine(
                        $"[CrossPage] delayed-dispatch task fault ex={ex.GetType().Name} msg={ex.Message} source={source}");
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

    private static async Task<(bool ShouldContinue, string? FailureDetail)> TryAwaitCrossPageDelayedDispatchAsync(
        int delayMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return (ShouldContinue: true, FailureDetail: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (ShouldContinue: false, FailureDetail: null);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return (
                ShouldContinue: false,
                FailureDetail: CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatDelayFailureDetail(
                    ex.GetType().Name));
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

    private void HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(
        CrossPageUpdateSourceKind kind,
        string source,
        string mode,
        bool emitAbortDiagnostics,
        string abortDetail)
    {
        var scheduled = TryBeginInvoke(() =>
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                kind,
                source,
                mode,
                emitAbortDiagnostics);
        }, DispatcherPriority.Background);
        if (scheduled)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            CrossPageDisplayUpdatePendingStateUpdater.MarkPendingCleared(ref _crossPageDisplayUpdateState);
            HandleCrossPageDisplayUpdateDispatchFailure(
                kind,
                source,
                mode,
                emitAbortDiagnostics);
            return;
        }

        _inkDiagnostics?.OnCrossPageUpdateEvent("defer-abort", source, abortDetail);
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
        _ = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                UpdateCrossPageDisplay();
                TryFlushCrossPageReplay();
                return true;
            },
            fallback: false,
            onFailure: ex =>
            {
                var replayQueueDecision = CrossPageDisplayUpdateRunFailureReplayPolicy.Resolve(source);
                CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                    ref _crossPageReplayState,
                    replayQueueDecision);
                _inkDiagnostics?.OnCrossPageUpdateEvent(
                    "recover",
                    source,
                    $"run-failed ex={ex.GetType().Name}");
            });
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


}


