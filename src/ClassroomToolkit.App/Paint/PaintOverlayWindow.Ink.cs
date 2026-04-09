using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using MediaColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void BeginBrushStroke(BrushInputSample input)
    {
        var position = input.Position;
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _strokeInProgress = true;
        _activeBrushStrokeUsesCrossPageContinuation = false;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(input);
        _lastBrushInputSample = input;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        RenderBrushPreview();
        _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
        _lastCalligraphyPreviewPoint = position;
    }

    private void BeginBrushStrokeContinuation(BrushInputSample input, bool renderInitialPreview)
    {
        var position = input.Position;
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }

        _strokeInProgress = true;
        _activeBrushStrokeUsesCrossPageContinuation = true;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(input);
        _lastBrushInputSample = input;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        if (renderInitialPreview)
        {
            RenderBrushPreview();
        }
        _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
        _lastCalligraphyPreviewPoint = position;
    }

    private void UpdateBrushStroke(BrushInputSample input)
    {
        if (!TryUpdateBrushStrokeGeometry(input))
        {
            return;
        }
        FlushBrushStrokePreview(input);
    }

    private bool TryUpdateBrushStrokeGeometry(BrushInputSample input)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return false;
        }

        var versionBeforeMove = _activeRenderer.GeometryVersion;
        _activeRenderer.OnMove(input);
        // Keep seam-bridge continuation input aligned with the latest accepted move sample.
        _lastBrushInputSample = input;
        return _activeRenderer.GeometryVersion != versionBeforeMove;
    }

    private void FlushBrushStrokePreview(BrushInputSample input)
    {
        var position = input.Position;
        if (_brushStyle == PaintBrushStyle.Calligraphy && ShouldThrottleCalligraphyPreview(position))
        {
            return;
        }

        UpdateBrushPrediction(input);
        RenderBrushPreview();
    }

    private void EndBrushStroke(BrushInputSample input)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        var position = input.Position;
        _activeRenderer.OnUp(input);
        var geometry = _activeRenderer.GetLastStrokeGeometry();
        if (geometry != null)
        {
            CommitGeometryFill(geometry, EffectiveBrushColor());
            RecordBrushStroke(geometry);
        }
        _activeRenderer.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        var usedCrossPageContinuation = _activeBrushStrokeUsesCrossPageContinuation;
        _activeBrushStrokeUsesCrossPageContinuation = false;
        _lastBrushInputSample = null;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        _lastCalligraphyPreviewPoint = null;
        var photoInkModeActive = IsPhotoInkModeActive();
        if (!_suppressImmediatePhotoInkRedraw
            && PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                photoInkModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform,
                usedCrossPageContinuation))
        {
            RequestInkRedraw();
        }
        SetInkContextDirty();
    }

    private void BeginBrushStroke(WpfPoint position)
    {
        BeginBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private void UpdateBrushStroke(WpfPoint position)
    {
        UpdateBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private void EndBrushStroke(WpfPoint position)
    {
        EndBrushStroke(BrushInputSample.CreatePointer(position));
    }

    private bool ShouldThrottleCalligraphyPreview(WpfPoint position)
    {
        if (_lastCalligraphyPreviewPoint.HasValue)
        {
            var delta = position - _lastCalligraphyPreviewPoint.Value;
            if (delta.Length >= _calligraphyPreviewMinDistance)
            {
                _lastCalligraphyPreviewPoint = position;
                _lastCalligraphyPreviewUtc = GetCurrentUtcTimestamp();
                return false;
            }
        }
        var nowUtc = GetCurrentUtcTimestamp();
        if ((nowUtc - _lastCalligraphyPreviewUtc).TotalMilliseconds >= CalligraphyPreviewMinIntervalMs)
        {
            _lastCalligraphyPreviewUtc = nowUtc;
            _lastCalligraphyPreviewPoint = position;
            return false;
        }
        return true;
    }

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

    private void BeginShape(WpfPoint position)
    {
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        if (_shapeType == PaintShapeType.Triangle)
        {
            BeginTriangleShape(position);
            return;
        }

        PushHistory();
        CaptureStrokeContext();
        _shapeStart = position;
        EnsureActiveShapePreview();
        if (_activeShape == null)
        {
            return;
        }
        UpdateShape(_activeShape!, _shapeType, _shapeStart, position);
        _isDrawingShape = true;
    }

    private void UpdateShapePreview(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        if (_shapeType == PaintShapeType.Triangle)
        {
            if (_triangleFirstEdgeCommitted)
            {
                if (_activeShape is WpfPath path)
                {
                    path.Data = BuildTriangleInteractivePreviewGeometry(_trianglePoint1, _trianglePoint2, position);
                }
                return;
            }
            UpdateShape(_activeShape, PaintShapeType.Triangle, _trianglePoint1, position);
            return;
        }
        UpdateShape(_activeShape, _shapeType, _shapeStart, position);
    }

    private void EndShape(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        if (_shapeType == PaintShapeType.Triangle)
        {
            EndTriangleShape(position);
            return;
        }
        var geometry = BuildShapeGeometry(_shapeType, _shapeStart, position);
        CommitShapeGeometry(geometry, _shapeType);
        ClearShapePreview();
        var photoInkModeActive = IsPhotoInkModeActive();
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                photoInkModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
        NotifyInkStateChanged(updateActiveSnapshot: false);
    }
    
    private void ClearShapePreview()
    {
        if (_activeShape != null)
        {
            PreviewCanvas.Children.Remove(_activeShape);
            _activeShape = null;
        }
        _isDrawingShape = false;
        ResetTriangleState();
    }

    private void EnsureActiveShapePreview()
    {
        if (_activeShape == null)
        {
            _activeShape = CreateShape(_shapeType);
            if (_activeShape == null)
            {
                return;
            }
            ApplyShapeStyle(_activeShape);
            PreviewCanvas.Children.Add(_activeShape);
        }
    }

    private void CommitShapeGeometry(Geometry? geometry, PaintShapeType shapeType)
    {
        if (geometry == null)
        {
            return;
        }

        if (shapeType == PaintShapeType.RectangleFill
            || shapeType == PaintShapeType.Arrow
            || shapeType == PaintShapeType.DashedArrow)
        {
            CommitGeometryFill(geometry, EffectiveBrushColor());
            RecordBrushStroke(geometry);
            return;
        }

        var pen = BuildShapePen();
        CommitGeometryStroke(geometry, pen);
        RecordShapeStroke(geometry, pen);
    }

    private void BeginTriangleShape(WpfPoint position)
    {
        if (!_triangleAnchorSet)
        {
            PushHistory();
            CaptureStrokeContext();
            _trianglePoint1 = position;
            _triangleAnchorSet = true;
        }

        EnsureActiveShapePreview();
        if (_activeShape is WpfPath path)
        {
            if (_triangleFirstEdgeCommitted)
            {
                path.Data = BuildTriangleInteractivePreviewGeometry(_trianglePoint1, _trianglePoint2, position);
            }
            else
            {
                path.Data = BuildTrianglePreviewGeometry(_trianglePoint1, position);
            }
        }
        _isDrawingShape = true;
    }

    private void EndTriangleShape(WpfPoint position)
    {
        const double triangleTapThresholdDip = 2.5;
        if (!_triangleAnchorSet)
        {
            return;
        }

        if (!_triangleFirstEdgeCommitted)
        {
            if ((_trianglePoint1 - position).Length < triangleTapThresholdDip)
            {
                // First tap only establishes anchor; do not commit a degenerate first edge.
                _isDrawingShape = false;
                if (_activeShape is WpfPath path)
                {
                    path.Data = BuildTrianglePreviewGeometry(_trianglePoint1, _trianglePoint1);
                }
                return;
            }

            _trianglePoint2 = position;
            _triangleFirstEdgeCommitted = true;
            _isDrawingShape = false;
            if (_activeShape is WpfPath previewPath)
            {
                previewPath.Data = BuildTrianglePreviewGeometry(_trianglePoint1, _trianglePoint2);
            }
            return;
        }

        var triangle = BuildTriangleGeometry(_trianglePoint1, _trianglePoint2, position);
        CommitShapeGeometry(triangle, PaintShapeType.Triangle);
        ClearShapePreview();
        ResetTriangleState();
        var photoInkModeActive = IsPhotoInkModeActive();
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                photoInkModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
        NotifyInkStateChanged(updateActiveSnapshot: false);
    }

    private void ResetTriangleState()
    {
        _triangleAnchorSet = false;
        _triangleFirstEdgeCommitted = false;
        _trianglePoint1 = new WpfPoint();
        _trianglePoint2 = new WpfPoint();
    }

    private bool HasPendingTriangleDraft()
    {
        if (_shapeType != PaintShapeType.Triangle)
        {
            return false;
        }

        return _triangleAnchorSet
               || _triangleFirstEdgeCommitted
               || _isDrawingShape
               || _activeShape != null;
    }

    private void CancelPendingTriangleDraft(string reason)
    {
        if (!HasPendingTriangleDraft())
        {
            return;
        }

        Debug.WriteLine($"[TriangleDraft] canceled: {reason}");
        ClearShapePreview();
    }

    private void HideEraserPreview()
    {
        // Eraser live preview is currently disabled.
    }
    
     private void ApplyEraserAt(WpfPoint position)
    {
        var radius = Math.Max(InkGeometryDefaults.MinEraserRadiusDip, _eraserSize * 0.5);
        var geometry = new EllipseGeometry(position, radius, radius);
        EraseGeometry(geometry);
    }

    private bool EraseRect(Rect region)
    {
        if (IsPhotoInkModeActive())
        {
            return EraseGeometry(new RectangleGeometry(region));
        }
        var eraseGeometry = new RectangleGeometry(region);
        var changed = ApplyInkErase(eraseGeometry);
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return changed || !_inkRecordEnabled;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new Int32Rect(
            (int)Math.Floor(region.X * dpi.DpiScaleX),
            (int)Math.Floor(region.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(region.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(region.Height * dpi.DpiScaleY));
        rect = IntersectRects(rect, new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return changed || !_inkRecordEnabled;
        }
        var stride = rect.Width * 4;
        var clear = new byte[stride * rect.Height];
        _rasterSurface.WritePixels(rect, clear, stride, 0);
        _hasDrawing = true;
        return changed || !_inkRecordEnabled;
    }

    private void ClearRegionSelection()
    {
        if (_regionRect != null)
        {
            PreviewCanvas.Children.Remove(_regionRect);
            _regionRect = null;
        }
        _isRegionSelecting = false;
    }

    private bool ShouldRecordRuntimeInkStroke()
    {
        // Cross-page photo/PDF writing requires per-page vector strokes to survive page switches.
        // Keep runtime stroke recording in photo ink mode even when replay/history recording is disabled.
        return _inkRecordEnabled || IsPhotoInkModeActive();
    }

    private void RecordBrushStroke(Geometry geometry)
    {
        if (!ShouldRecordRuntimeInkStroke() || geometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = _brushStyle,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            CalligraphyRenderMode = _calligraphyRenderMode,
            CalligraphyInkBloomEnabled = _calligraphyInkBloomEnabled,
            CalligraphySealEnabled = _calligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = _calligraphyOverlayOpacityThreshold
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        var photoInkModeActive = IsPhotoInkModeActive();
        if (_brushStyle == PaintBrushStyle.Calligraphy && _activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
        {
            var core = calligraphyRenderer.GetLastCoreGeometry();
            var strokeGeometry = core ?? geometry;
            if (!CalligraphySinglePassCompositeEnabled)
            {
                var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
                if (ribbons != null && ribbons.Count > 0)
                {
                    foreach (var ribbon in ribbons)
                    {
                        var ribbonGeometry = photoInkModeActive ? ToPhotoGeometry(ribbon.Geometry) : ribbon.Geometry;
                        if (ribbonGeometry == null)
                        {
                            continue;
                        }
                        stroke.Ribbons.Add(new InkRibbonData
                        {
                            GeometryPath = InkGeometrySerializer.Serialize(ribbonGeometry),
                            Opacity = calligraphyRenderer.GetRibbonOpacity(ribbon.RibbonT),
                            RibbonT = ribbon.RibbonT
                        });
                    }
                }
            }
            var storeGeometry = photoInkModeActive ? ToPhotoGeometry(strokeGeometry) : strokeGeometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
            stroke.InkFlow = calligraphyRenderer.LastInkFlow;
            stroke.StrokeDirectionX = calligraphyRenderer.LastStrokeDirection.X;
            stroke.StrokeDirectionY = calligraphyRenderer.LastStrokeDirection.Y;
            if (!CalligraphySinglePassCompositeEnabled)
            {
                var blooms = calligraphyRenderer.GetInkBloomGeometries();
                if (blooms != null)
                {
                    foreach (var bloom in blooms)
                    {
                        var bloomGeometry = photoInkModeActive ? ToPhotoGeometry(bloom.Geometry) : bloom.Geometry;
                        if (bloomGeometry == null)
                        {
                            continue;
                        }
                        stroke.Blooms.Add(new InkBloomData
                        {
                            GeometryPath = InkGeometrySerializer.Serialize(bloomGeometry),
                            Opacity = bloom.Opacity
                        });
                    }
                }
            }
        }
        else
        {
            var storeGeometry = photoInkModeActive ? ToPhotoGeometry(geometry) : geometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        stroke.MaskSeed = ComputeDeterministicMaskSeed(stroke);
        CommitStroke(stroke);
    }

    private void RecordShapeStroke(Geometry geometry, MediaPen pen)
    {
        if (!ShouldRecordRuntimeInkStroke() || geometry == null || pen == null)
        {
            return;
        }
        var widened = geometry.GetWidenedPathGeometry(pen);
        if (widened == null || widened.Bounds.IsEmpty)
        {
            return;
        }
        var storeGeometry = IsPhotoInkModeActive() ? ToPhotoGeometry(widened) : widened;
        if (storeGeometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Shape,
            BrushStyle = PaintBrushStyle.StandardRibbon,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            CalligraphyRenderMode = CalligraphyRenderMode.Clarity,
            GeometryPath = InkGeometrySerializer.Serialize(storeGeometry)
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        stroke.MaskSeed = ComputeDeterministicMaskSeed(stroke);
        CommitStroke(stroke);
    }

    private static int ComputeDeterministicMaskSeed(InkStrokeData stroke)
    {
        uint hash = 2166136261u;
        hash = Fnv1a(hash, stroke.Type);
        hash = Fnv1a(hash, stroke.BrushStyle);
        hash = Fnv1a(hash, stroke.CalligraphyRenderMode);
        hash = Fnv1a(hash, stroke.ColorHex);
        hash = Fnv1a(hash, Quantize(stroke.BrushSize, 1000.0));
        hash = Fnv1a(hash, stroke.GeometryPath);
        int seed = unchecked((int)hash);
        return seed == 0 ? 17 : seed;
    }

    private static uint Fnv1a(uint hash, PaintBrushStyle value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, InkStrokeType value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, CalligraphyRenderMode value)
    {
        return Fnv1a(hash, (int)value);
    }

    private static uint Fnv1a(uint hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Fnv1a(hash, 0);
        }

        unchecked
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
        }
        return hash;
    }

    private static uint Fnv1a(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
        return hash;
    }

    private static int Quantize(double value, double scale)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Round(value * scale);
    }

    private bool ApplyInkErase(Geometry geometry)
    {
        if (_inkStrokes.Count == 0 || geometry == null)
        {
            return false;
        }
        var photoInkModeActive = IsPhotoInkModeActive();
        var erasePrimary = photoInkModeActive ? ToPhotoGeometry(geometry) : geometry;
        var eraseFallback = photoInkModeActive ? geometry : null;
        if (erasePrimary == null)
        {
            return false;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var geometryPathChanged = false;
            var bloomGeometryChanged = false;
            var ribbonGeometryChanged = false;
            var updatedPath = ExcludeGeometryWithFallback(stroke.GeometryPath, erasePrimary, eraseFallback);
            if (!InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, updatedPath, out var strokeRemoved))
            {
                strokeRemoved = false;
            }
            else if (strokeRemoved)
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }
            else
            {
                geometryPathChanged = true;
            }

            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometryWithFallback(bloom.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(bloomUpdated))
                    {
                        stroke.Blooms.RemoveAt(j);
                        bloomGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(bloomUpdated, bloom.GeometryPath, StringComparison.Ordinal))
                    {
                        bloom.GeometryPath = bloomUpdated;
                        bloomGeometryChanged = true;
                    }
                }
            }
            if (stroke.Ribbons.Count > 0)
            {
                bool ribbonsChanged = false;
                for (int j = stroke.Ribbons.Count - 1; j >= 0; j--)
                {
                    var ribbon = stroke.Ribbons[j];
                    var ribbonUpdated = ExcludeGeometryWithFallback(ribbon.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(ribbonUpdated))
                    {
                        stroke.Ribbons.RemoveAt(j);
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                        continue;
                    }
                    if (!string.Equals(ribbonUpdated, ribbon.GeometryPath, StringComparison.Ordinal))
                    {
                        ribbon.GeometryPath = ribbonUpdated;
                        ribbonsChanged = true;
                        ribbonGeometryChanged = true;
                        changed = true;
                    }
                }
                if (ribbonsChanged)
                {
                    stroke.CachedRibbonGeometries = null;
                }
            }
            if (InkEraseStrokeChangePolicy.ShouldMarkStrokeChanged(
                    geometryPathChanged,
                    bloomGeometryChanged,
                    ribbonGeometryChanged))
            {
                changed = true;
            }
        }
        return changed;
    }

    private static string? ExcludeGeometryWithFallback(string geometryPath, Geometry primaryEraser, Geometry? fallbackEraser)
    {
        var primary = ExcludeGeometry(geometryPath, primaryEraser);
        if (fallbackEraser == null)
        {
            return primary;
        }
        if (primary == null || string.Equals(primary, geometryPath, StringComparison.Ordinal))
        {
            return ExcludeGeometry(geometryPath, fallbackEraser);
        }
        return primary;
    }

    private void CaptureStrokeContext()
    {
    }

    private void CommitStroke(InkStrokeData stroke)
    {
        _inkStrokes.Add(stroke);
        NotifyInkStateChanged(updateActiveSnapshot: true, notifyContext: false);
    }

    private void UpdateActiveCacheSnapshot()
    {
        if (!_inkCacheEnabled)
        {
            return;
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        var cacheKey = _currentCacheKey;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            InvalidateNeighborInkCache(cacheKey);
            return;
        }
        _photoCache.Set(cacheKey, strokes);
        InvalidateNeighborInkCache(cacheKey);
    }

    private List<InkStrokeData> CloneCommittedInkStrokes()
    {
        return CloneInkStrokes(_inkStrokes);
    }

    private void SetInkContextDirty()
    {
        _pendingInkContextCheck = true;
        _refreshOrchestrator?.RequestRefresh("ink-dirty");
    }

    private void SetInkCacheDirty()
    {
        MarkCurrentInkPageModified();
        ScheduleSidecarAutoSave();
    }

    private bool IsCurrentPageDirty()
    {
        return _inkDirtyPages.IsDirty(_currentDocumentPath, _currentPageIndex);
    }

    private bool IsRuntimeInkPageExplicitlyCleared(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        if (!_inkDirtyPages.TryGetRuntimeState(sourcePath, pageIndex, out _, out var runtimeHash, out _))
        {
            return false;
        }

        return string.Equals(runtimeHash, "empty", StringComparison.Ordinal);
    }

    private bool IsInkOperationActive()
    {
        return _strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting;
    }

    private bool IsPhotoInkModeActive()
    {
        return PhotoInkModePolicy.IsActive(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive());
    }

    private void NotifyInkStateChanged(
        bool updateActiveSnapshot,
        bool notifyContext = true,
        bool syncCrossPageVisual = true)
    {
        if (updateActiveSnapshot)
        {
            MarkInkStrokeVersionDirty();
            UpdateActiveCacheSnapshot();
        }
        SetInkCacheDirty();
        if (syncCrossPageVisual && !_suppressCrossPageVisualSync)
        {
            ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger.InkStateChanged);
        }
        if (notifyContext)
        {
            SetInkContextDirty();
        }
    }

    private void ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger trigger)
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var elapsedMs = _crossPageInkVisualSyncState.LastSyncUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc
            ? double.MaxValue
            : (nowUtc - _crossPageInkVisualSyncState.LastSyncUtc).TotalMilliseconds;
        if (CrossPageInkVisualSyncDedupPolicy.ShouldSkip(
                trigger,
                _crossPageInkVisualSyncState.LastTrigger,
                interactionActive: IsCrossPageInteractionActive(),
                elapsedMs))
        {
            return;
        }

        var decision = CrossPageInkVisualSyncPolicy.Resolve(
            IsPhotoInkModeActive(),
            IsCrossPageDisplayActive(),
            trigger);
        if (!decision.ShouldRequestCrossPageUpdate)
        {
            return;
        }

        if (decision.ShouldPrimeVisibleNeighborSlots)
        {
            PrimeVisibleNeighborInkSlots();
        }

        var source = trigger == CrossPageInkVisualSyncTrigger.InkRedrawCompleted
            ? CrossPageUpdateSources.InkRedrawCompleted
            : CrossPageUpdateSources.InkStateChanged;
        if (_crossPageDisplayUpdateState.Pending
            && CrossPageUpdateReplayPolicy.ShouldQueueReplay(CrossPageUpdateSourceKind.VisualSync))
        {
            CrossPageReplayPendingStateUpdater.ApplyQueueDecision(
                ref _crossPageReplayState,
                CrossPageReplayQueueDecisionFactory.VisualSync());
        }
        RequestCrossPageDisplayUpdate(source);
        CrossPageInkVisualSyncStateUpdater.MarkApplied(
            ref _crossPageInkVisualSyncState,
            nowUtc,
            trigger);
    }

    private bool TryGetCurrentPhotoReferenceSize(out double width, out double height)
    {
        width = 0;
        height = 0;
        if (!IsPhotoInkModeActive() || PhotoBackground.Source is not BitmapSource bitmap)
        {
            return false;
        }

        width = GetBitmapDisplayWidthInDip(bitmap);
        height = GetBitmapDisplayHeightInDip(bitmap);
        return width > InkInputRuntimeDefaults.PhotoReferenceSizeMinDip
            && height > InkInputRuntimeDefaults.PhotoReferenceSizeMinDip;
    }

    private void MarkInkInput()
    {
        _lastInkInputUtc = GetCurrentUtcTimestamp();
        _inkDiagnostics?.OnInkInput();
        UpdateInkMonitorInterval();
    }

    private bool ShouldDeferInkContext()
    {
        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            return true;
        }
        if (_lastInkInputUtc == InkRuntimeTimingDefaults.UnsetTimestampUtc)
        {
            return false;
        }
        return (GetCurrentUtcTimestamp() - _lastInkInputUtc).TotalMilliseconds < InkInputCooldownMs;
    }

    private void UpdateInkMonitorInterval()
    {
        var nowUtc = GetCurrentUtcTimestamp();
        var idle = _lastInkInputUtc != InkRuntimeTimingDefaults.UnsetTimestampUtc
                   && (nowUtc - _lastInkInputUtc).TotalMilliseconds > InkIdleThresholdMs;

        var targetMs = idle ? InkMonitorIdleIntervalMs : InkMonitorActiveIntervalMs;
        var currentMs = _inkMonitor.Interval.TotalMilliseconds;
        if (Math.Abs(currentMs - targetMs) < 1)
        {
            return;
        }
        _inkMonitor.Interval = TimeSpan.FromMilliseconds(targetMs);
    }
}
