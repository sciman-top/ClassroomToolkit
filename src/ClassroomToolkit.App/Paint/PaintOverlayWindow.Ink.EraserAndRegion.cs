using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void BeginEraser(WpfPoint position)
    {
        PushHistory();
        _isErasing = true;
        _lastEraserPoint = position;
        ApplyEraserAt(position);
    }

    private void UpdateEraser(WpfPoint position)
    {
        if (!_isErasing || _lastEraserPoint == null)
        {
            return;
        }
        var last = _lastEraserPoint.Value;
        var distance = (position - last).Length;
        var threshold = Math.Max(
            InkGeometryDefaults.EraserMoveThresholdMinDip,
            _eraserSize * InkGeometryDefaults.EraserMoveThresholdScale);
        if (distance < threshold)
        {
            return;
        }
        var geometry = BuildEraserGeometry(last, position);
        if (geometry != null)
        {
            EraseGeometry(geometry);
        }
        _lastEraserPoint = position;
    }

    private void EndEraser(WpfPoint position)
    {
        if (!_isErasing)
        {
            return;
        }
        if (_lastEraserPoint == null || (_lastEraserPoint.Value - position).Length < InkGeometryDefaults.EraserTapDistanceThresholdDip)
        {
            ApplyEraserAt(position);
        }
        _isErasing = false;
        _lastEraserPoint = null;
        NotifyInkStateChanged(updateActiveSnapshot: true);
        var photoInkModeActive = IsPhotoInkModeActive();
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                photoInkModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
    }

    private void BeginRegionSelection(WpfPoint position)
    {
        PushHistory();
        _regionStart = position;
        if (_regionRect == null)
        {
            _regionRect = new WpfRectangle
            {
                Stroke = new SolidColorBrush(MediaColor.FromArgb(200, 255, 200, 60)),
                StrokeThickness = InkInputRuntimeDefaults.RegionSelectionStrokeThicknessDip,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Fill = new SolidColorBrush(MediaColor.FromArgb(30, 255, 200, 60)),
                IsHitTestVisible = false
            };
            PreviewCanvas.Children.Add(_regionRect);
        }
        UpdateSelectionRect(_regionRect, _regionStart, position);
        _isRegionSelecting = true;
    }

    private void UpdateRegionSelection(WpfPoint position)
    {
        if (_isRegionSelecting && _regionRect != null)
        {
            UpdateSelectionRect(_regionRect, _regionStart, position);
        }
    }

    private void EndRegionSelection(WpfPoint position)
    {
        if (!_isRegionSelecting)
        {
            return;
        }
        _isRegionSelecting = false;
        var region = BuildRegionRect(_regionStart, position);
        ClearRegionSelection();
        if (region.Width > InkInputRuntimeDefaults.RegionEraseMinSideDip
            && region.Height > InkInputRuntimeDefaults.RegionEraseMinSideDip)
        {
            var changed = EraseRectAcrossVisibleCrossPages(region);
            if (changed)
            {
                NotifyInkStateChanged(updateActiveSnapshot: true);
            }
        }
    }

    private bool EraseRectAcrossVisibleCrossPages(Rect region)
    {
        if (!CrossPageRegionErasePolicy.ShouldUseCrossPageErase(
                IsPhotoInkModeActive(),
                IsCrossPageDisplayActive()))
        {
            return EraseRect(region);
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        var pages = ResolveCrossPagePagesIntersectingRegion(region, currentPage);
        if (pages.Count == 0)
        {
            return EraseRect(region);
        }

        var anyChanged = false;
        var batchOrder = CrossPageRegionEraseOrderPolicy.ResolveBatchOrder(
            pages,
            currentPage);
        foreach (var targetPage in batchOrder)
        {
            if (!TryNavigateCrossPageForRegionErase(targetPage))
            {
                continue;
            }
            var pageChanged = EraseRect(region);
            anyChanged |= pageChanged;
            if (!pageChanged)
            {
                continue;
            }
            NotifyInkStateChanged(
                updateActiveSnapshot: true,
                notifyContext: false,
                syncCrossPageVisual: false);
        }

        if (anyChanged)
        {
            PrimeVisibleNeighborInkSlots();
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.RegionEraseCrossPage);
        }
        return anyChanged;
    }

    private bool TryNavigateCrossPageForRegionErase(int targetPage)
    {
        if (!CrossPageRegionErasePolicy.CanNavigateForRegionErase(
                IsPhotoInkModeActive(),
                IsCrossPageDisplayActive(),
                targetPage))
        {
            return false;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        if (currentPage == targetPage)
        {
            return true;
        }

        if (PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return false;
        }

        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var offset = CrossPageInputNavigation.ComputePageOffset(
            currentPage,
            targetPage,
            page => GetScaledHeightForPage(page, normalizedWidthDip));

        SaveCurrentPageOnNavigate(
            forceBackground: false,
            persistToSidecar: false,
            finalizeActiveOperation: false);

        var navigationPlan = CrossPageRegionEraseNavigationPolicy.Resolve();

        NavigateToPage(
            targetPage,
            _photoTranslate.Y + offset,
            interactiveSwitch: navigationPlan.InteractiveSwitch,
            deferCrossPageDisplayUpdate: navigationPlan.DeferCrossPageDisplayUpdate);

        return GetCurrentPageIndexForCrossPage() == targetPage;
    }

    private List<int> ResolveCrossPagePagesIntersectingRegion(Rect region, int currentPage)
    {
        var pages = new HashSet<int>();
        if (PhotoBackground.Source is BitmapSource currentBitmap
            && TryBuildImageScreenRect(currentBitmap, _photoContentTransform, out var currentRect)
            && currentRect.IntersectsWith(region))
        {
            pages.Add(currentPage);
        }

        foreach (var img in _neighborPageImages)
        {
            if (img.Visibility != Visibility.Visible || img.Source is not BitmapSource bitmap)
            {
                continue;
            }
            if (!int.TryParse(img.Uid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageIndex))
            {
                continue;
            }
            if (!TryBuildImageScreenRect(bitmap, img.RenderTransform, out var rect))
            {
                continue;
            }
            if (rect.IntersectsWith(region))
            {
                pages.Add(pageIndex);
            }
        }

        return pages.Where(page => page > 0).ToList();
    }
}
