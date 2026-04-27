using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void TrySeedNeighborFrameForInteractiveSwitch(
        int previousPage,
        BitmapSource? previousPageBitmap)
    {
        if (!IsCrossPageDisplayActive() || previousPage <= 0)
        {
            return;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        if (previousPage == currentPage)
        {
            return;
        }

        if (_neighborPagesCanvas == null || PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return;
        }

        var neighborBitmap = previousPageBitmap ?? GetNeighborPageBitmapForRender(previousPage, allowSynchronousResolve: true);
        if (neighborBitmap == null)
        {
            return;
        }

        if (!_photoDocumentIsPdf)
        {
            _neighborImageCache[previousPage] = neighborBitmap;
        }

        while (_neighborPageImages.Count < 1)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }

        while (_neighborInkImages.Count < 1)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }

        var pageUid = previousPage.ToString(CultureInfo.InvariantCulture);
        var slotIndex = ResolveNeighborSlotIndexForInteractiveSeed(pageUid);
        while (_neighborPageImages.Count <= slotIndex)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }

        while (_neighborInkImages.Count <= slotIndex)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }
        ReapplyPhotoRenderQualityModeForDynamicSurfaces();

        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var baseTop = CrossPageInputNavigation.ComputePageOffset(
            currentPage,
            previousPage,
            page => GetScaledHeightForPage(page, normalizedWidthDip));
        var pageScaleRatio = 1.0;
        if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
        {
            var pageWidth = GetBitmapDisplayWidthInDip(neighborBitmap);
            if (pageWidth > 0)
            {
                pageScaleRatio = normalizedWidthDip / pageWidth;
            }
        }

        var pageImg = _neighborPageImages[slotIndex];
        if (CrossPageFrameSourceAssignmentPolicy.ShouldAssign(pageImg.Source, neighborBitmap))
        {
            pageImg.Source = neighborBitmap;
        }

        pageImg.Visibility = Visibility.Visible;
        pageImg.Uid = pageUid;
        pageImg.Tag = baseTop;

        var inkBitmap = ResolveNeighborInkBitmap(previousPage, neighborBitmap, allowDeferredRender: false);

        if (inkBitmap == null)
        {
            RequestDeferredNeighborInkRender(previousPage, neighborBitmap);
        }

        var inkImgFirst = _neighborInkImages[slotIndex];
        var targetInkBitmap = _inkShowEnabled ? inkBitmap : null;
        var currentInkOffsetDip = 0.0;
        var slotPageChanged = !string.Equals(inkImgFirst.Uid, pageUid, StringComparison.Ordinal);
        var shouldReplaceSeedFrame = CrossPageInteractiveSeedInkFramePolicy.ShouldReplaceFrame(
            _inkShowEnabled,
            hasCurrentFrame: inkImgFirst.Source != null,
            hasResolvedTargetFrame: targetInkBitmap != null,
            slotPageChanged: slotPageChanged);
        if (shouldReplaceSeedFrame
            && CrossPageFrameSourceAssignmentPolicy.ShouldAssign(
                inkImgFirst.Source,
                targetInkBitmap))
        {
            inkImgFirst.Source = targetInkBitmap;
            if (targetInkBitmap != null
                && TryGetNeighborInkCacheEntry(previousPage, out var cachedEntry)
                && ReferenceEquals(cachedEntry.Bitmap, targetInkBitmap))
            {
                currentInkOffsetDip = cachedEntry.HorizontalOffsetDip;
            }
        }

        inkImgFirst.Visibility = inkImgFirst.Source != null ? Visibility.Visible : Visibility.Collapsed;
        inkImgFirst.Uid = pageUid;
        SetNeighborInkSlotTag(inkImgFirst, baseTop, currentInkOffsetDip);
        ApplyNeighborSharedTransform(pageImg, inkImgFirst, pageScaleRatio, baseTop);

        _neighborPagesCanvas.Visibility = Visibility.Visible;
        _lastNeighborPagesNonEmptyUtc = GetCurrentUtcTimestamp();
        var visibleNeighborCount = _neighborPageImages.Count(img => img.Visibility == Visibility.Visible);
        var holdMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(visibleNeighborCount, _mode);
        if (holdMs > 0)
        {
            _interactiveSwitchPinnedNeighborPage = previousPage;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = GetCurrentUtcTimestamp().AddMilliseconds(holdMs);
        }
        else
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }

        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "seed",
            "navigate-interactive",
            $"page={previousPage} ink={(inkImgFirst.Source != null)} holdMs={holdMs}");
    }

    private int ResolveNeighborSlotIndexForInteractiveSeed(string pageUid)
    {
        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            if (string.Equals(_neighborPageImages[i].Uid, pageUid, StringComparison.Ordinal))
            {
                return i;
            }
        }

        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            if (_neighborPageImages[i].Visibility != Visibility.Visible)
            {
                return i;
            }
        }

        return _neighborPageImages.Count;
    }
}
