using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var normalizedWidthDip = currentBitmap != null
            ? GetCrossPageNormalizedWidthDip(currentBitmap)
            : 0;
        var currentPageHeight = currentBitmap != null
            ? GetScaledPageHeight(currentBitmap, normalizedWidthDip)
            : 0;
        if (CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
                totalPages,
                hasCurrentBitmap: currentBitmap != null,
                currentPageHeight))
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
        var preservedInkFrames = new Dictionary<string, (BitmapSource Bitmap, double HorizontalOffsetDip)>(StringComparer.Ordinal);
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
                    preservedInkFrames[inkImg.Uid] = (
                        inkBitmap,
                        ResolveNeighborInkHorizontalOffsetDip(inkImg.Tag));
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
            var runtimeInkPageCleared = IsRuntimeInkPageClearedForCrossPageIndex(pageIndex);
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
            var baseTop = inkImg.Tag is NeighborInkSlotTag currentInkTag ? currentInkTag.BaseTop : (img.Tag is double taggedTop ? taggedTop : top - _photoTranslate.Y);
            var pageScaleRatio = 1.0;
            var currentInkOffsetDip = ResolveNeighborInkHorizontalOffsetDip(inkImg.Tag);
            if (bitmap != null)
            {
                baseTop = top - _photoTranslate.Y;
                if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
                {
                    var pageWidthDip = GetBitmapDisplayWidthInDip(bitmap);
                    if (pageWidthDip > 0)
                    {
                        pageScaleRatio = normalizedWidthDip / pageWidthDip;
                    }
                }
                img.Tag = baseTop;
                SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
                img.Uid = pageUid;
                inkImg.Uid = pageUid;
                ApplyNeighborSharedTransform(img, inkImg, pageScaleRatio, baseTop);
            }
            if (runtimeInkPageCleared)
            {
                TryAssignFrameSource(inkImg, null);
                SetNeighborInkSlotTag(inkImg, baseTop, 0);
                inkImg.Visibility = Visibility.Collapsed;
                if (bitmap != null) ApplyNeighborSharedTransform(img, inkImg, pageScaleRatio, baseTop);
                continue;
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
                inkOperationActive: IsInkOperationActive(),
                slotPageChanged: slotPageChanged);
            var usedPreservedInkFrame = false;
            if (bitmap != null)
            {
                if (inkImg.Source == null
                    && preservedInkFrames.TryGetValue(pageUid, out var preservedInkEntry))
                {
                    TryAssignFrameSource(inkImg, preservedInkEntry.Bitmap);
                    currentInkOffsetDip = preservedInkEntry.HorizontalOffsetDip;
                    SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
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
                        currentInkOffsetDip = 0;
                    }
                    if (inkBitmap != null
                        && !holdInkReplacement
                        && (frameDecision.AllowResolvedInkReplacement
                            || !ReferenceEquals(inkImg.Source, inkBitmap)))
                    {
                        TryAssignFrameSource(inkImg, inkBitmap);
                        if (TryGetNeighborInkCacheEntry(pageIndex, out var resolvedEntry)
                            && ReferenceEquals(resolvedEntry.Bitmap, inkBitmap))
                        {
                            currentInkOffsetDip = resolvedEntry.HorizontalOffsetDip;
                        }
                    }
                    SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
                    inkImg.Visibility = frameDecision.KeepVisible && inkImg.Source != null
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else
                {
                    if (inkImg.Source == null)
                    {
                        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
                        if (!string.IsNullOrWhiteSpace(cacheKey)
                            && _neighborInkCache.TryGetValue(cacheKey, out var cachedEntry))
                        {
                            TryAssignFrameSource(inkImg, cachedEntry.Bitmap);
                            currentInkOffsetDip = cachedEntry.HorizontalOffsetDip;
                            SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
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
                        if (interactiveInkBitmap != null
                            && TryGetNeighborInkCacheEntry(pageIndex, out var interactiveEntry)
                            && ReferenceEquals(interactiveEntry.Bitmap, interactiveInkBitmap))
                        {
                            currentInkOffsetDip = interactiveEntry.HorizontalOffsetDip;
                            SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
                        }
                    }
                    else if (slotPageChanged)
                    {
                        var hasPreservedInkFrame = preservedInkFrames.TryGetValue(pageUid, out var remapPreservedInkEntry);
                        var remapAction = CrossPageInteractiveInkSlotRemapPolicy.Resolve(
                            slotPageChanged: true,
                            hasResolvedInkBitmap: interactiveInkBitmap != null,
                            hasCurrentInkFrame: inkImg.Source != null,
                            hasPreservedInkFrame: hasPreservedInkFrame,
                            inkOperationActive: IsInkOperationActive());
                        if (remapAction == CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame && hasPreservedInkFrame)
                        {
                            usedPreservedInkFrame = true;
                            TryAssignFrameSource(inkImg, remapPreservedInkEntry.Bitmap);
                            currentInkOffsetDip = remapPreservedInkEntry.HorizontalOffsetDip;
                            SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
                        }
                        else if (remapAction == CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame)
                        {
                            // Slot remapped to a different page and no preserved target frame available.
                            // Clear old-page ink to avoid cross-page ghost duplication.
                            TryAssignFrameSource(inkImg, null);
                            currentInkOffsetDip = 0;
                            SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
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
                        currentInkOffsetDip = 0;
                        SetNeighborInkSlotTag(inkImg, baseTop, currentInkOffsetDip);
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
                ApplyNeighborSharedTransform(img, inkImg, pageScaleRatio, baseTop);
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
                    SetNeighborInkSlotTag(inkImg, baseTop, 0);
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
}
