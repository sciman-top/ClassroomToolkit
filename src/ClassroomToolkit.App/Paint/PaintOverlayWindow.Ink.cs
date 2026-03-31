using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    private void UpdateBrushPrediction(BrushInputSample input)
    {
        if (!_lastBrushInputSample.HasValue)
        {
            _lastBrushInputSample = input;
            return;
        }

        var previous = _lastBrushInputSample.Value;
        var dtMs = (input.TimestampTicks - previous.TimestampTicks) * 1000.0 / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            _lastBrushInputSample = input;
            return;
        }

        var dtSeconds = dtMs / 1000.0;
        var v = (input.Position - previous.Position) / Math.Max(dtSeconds, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        _lastBrushVelocityDipPerSec = new Vector(
            (_lastBrushVelocityDipPerSec.X * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (v.X * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor),
            (_lastBrushVelocityDipPerSec.Y * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (v.Y * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor));
        _lastBrushInputSample = input;
    }

    private void RenderBrushPreview()
    {
        if (_activeRenderer == null)
        {
            return;
        }

        _visualHost.UpdateVisual(dc =>
        {
            _activeRenderer.Render(dc);
            if (TryResolvePredictedBrushSegment(
                    out var p0,
                    out var p1,
                    out var p2,
                    out var w0,
                    out var w1,
                    out var w2))
            {
                var previewColor = EffectiveBrushColor();
                DrawPredictedBrushSegment(dc, previewColor, p0, p1, p2, w0, w1, w2);
            }
        });
    }

    private bool TryResolvePredictedBrushSegment(
        out WpfPoint p0,
        out WpfPoint p1,
        out WpfPoint p2,
        out double w0,
        out double w1,
        out double w2)
    {
        p0 = new WpfPoint();
        p1 = new WpfPoint();
        p2 = new WpfPoint();
        w0 = Math.Max(
            BrushPredictionPreviewDefaults.InitialBaseWidthMinDip,
            _brushSize * BrushPredictionPreviewDefaults.InitialBaseWidthFactor);
        w1 = Math.Max(BrushPredictionPreviewDefaults.MinMidWidthDip, w0 * BrushPredictionPreviewDefaults.MidWidthRatio);
        w2 = Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w1 * BrushPredictionPreviewDefaults.InitialTipWidthRatio);

        if (!_strokeInProgress || !_lastBrushInputSample.HasValue)
        {
            return false;
        }

        var speed = _lastBrushVelocityDipPerSec.Length;
        if (speed < BrushPredictionPreviewDefaults.MinSpeedDipPerSec)
        {
            return false;
        }

        double horizonMs = Math.Clamp(_brushPredictionHorizonMs, InkPredictionDefaults.HorizonMinMs, InkPredictionDefaults.HorizonMaxMs);
        var damping = Math.Clamp(
            1.0 - (speed / BrushPredictionPreviewDefaults.DampingSpeedReference),
            BrushPredictionPreviewDefaults.DampingMin,
            1.0);
        var lead1 = _lastBrushVelocityDipPerSec
            * ((horizonMs * BrushPredictionPreviewDefaults.FirstLeadHorizonRatio) / 1000.0)
            * damping;
        var lead2 = _lastBrushVelocityDipPerSec
            * ((horizonMs * BrushPredictionPreviewDefaults.SecondLeadHorizonRatio) / 1000.0)
            * damping;

        if (lead1.Length > BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio)
        {
            lead1 *= (BrushPredictionMaxDistanceDip * BrushPredictionPreviewDefaults.FirstLeadDistanceRatio) / lead1.Length;
        }
        if (lead2.Length > BrushPredictionMaxDistanceDip)
        {
            lead2 *= BrushPredictionMaxDistanceDip / lead2.Length;
        }

        var origin = _lastBrushInputSample.Value.Position;
        p0 = origin;
        p1 = origin + lead1;
        p2 = origin + lead2;
        double speedFactor = Math.Clamp(
            (speed - BrushPredictionPreviewDefaults.MinSpeedDipPerSec) / BrushPredictionPreviewDefaults.SpeedFactorRange,
            0.0,
            1.0);
        var baseWidth = Math.Max(
            BrushPredictionPreviewDefaults.MinBaseWidthDip,
            _brushSize * (BrushPredictionPreviewDefaults.BaseWidthFactor + speedFactor * BrushPredictionPreviewDefaults.SpeedWidthGainFactor));
        w0 = baseWidth;
        w1 = Math.Max(BrushPredictionPreviewDefaults.MinMidWidthDip, baseWidth * BrushPredictionPreviewDefaults.MidWidthRatio);
        w2 = Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, baseWidth * BrushPredictionPreviewDefaults.TipWidthRatio);
        return true;
    }

    private static void DrawPredictedBrushSegment(
        DrawingContext dc,
        MediaColor color,
        WpfPoint p0,
        WpfPoint p1,
        WpfPoint p2,
        double w0,
        double w1,
        double w2)
    {
        byte a0 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.PrimaryAlphaMultiplier,
            InkPredictionDefaults.PrimaryAlphaMin,
            InkPredictionDefaults.PrimaryAlphaMax);
        byte a1 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.SecondaryAlphaMultiplier,
            InkPredictionDefaults.SecondaryAlphaMin,
            InkPredictionDefaults.SecondaryAlphaMax);
        byte a2 = (byte)Math.Clamp(
            color.A * BrushPredictionPreviewDefaults.TipAlphaMultiplier,
            InkPredictionDefaults.TipAlphaMin,
            InkPredictionDefaults.TipAlphaMax);

        var c0 = MediaColor.FromArgb(a0, color.R, color.G, color.B);
        var c1 = MediaColor.FromArgb(a1, color.R, color.G, color.B);
        var c2 = MediaColor.FromArgb(a2, color.R, color.G, color.B);

        var pen0 = new MediaPen(new SolidColorBrush(c0), w0)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (pen0.CanFreeze) pen0.Freeze();
        dc.DrawLine(pen0, p0, p1);

        var pen1 = new MediaPen(new SolidColorBrush(c1), w1)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (pen1.CanFreeze) pen1.Freeze();
        dc.DrawLine(pen1, p1, p2);

        var tipBrush = new SolidColorBrush(c2);
        if (tipBrush.CanFreeze) tipBrush.Freeze();
        dc.DrawEllipse(
            tipBrush,
            null,
            p2,
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio),
            Math.Max(BrushPredictionPreviewDefaults.MinTipWidthDip, w2 * BrushPredictionPreviewDefaults.TipRadiusRatio));
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

    private void MarkCurrentInkPageLoaded(IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }
        MarkInkPageLoaded(_currentDocumentPath, _currentPageIndex, strokes);
    }

    private void MarkInkPageLoaded(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }
        _inkDirtyPages.MarkLoaded(sourcePath, pageIndex, ComputeInkHash(strokes));
        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }
        ClearInkWalSnapshot(sourcePath, pageIndex);
    }

    private void MarkCurrentInkPageModified()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }
        var hash = ComputeInkHash(_inkStrokes);
        _inkDirtyPages.MarkModified(_currentDocumentPath, _currentPageIndex, hash);
        TrackInkWalSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes, hash);
        SyncSessionInkDirtyFlag();
    }

    private bool MarkInkPagePersistedIfUnchanged(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var persisted = _inkDirtyPages.MarkPersistedIfUnchanged(sourcePath, pageIndex, hash);
        if (persisted)
        {
            ClearInkWalSnapshot(sourcePath, pageIndex);
        }
        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }
        return persisted;
    }

    private void MarkInkPageModified(string sourcePath, int pageIndex, string hash, IReadOnlyList<InkStrokeData>? walSnapshot = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        _inkDirtyPages.MarkModified(sourcePath, pageIndex, hash);
        if (walSnapshot != null)
        {
            TrackInkWalSnapshot(sourcePath, pageIndex, walSnapshot, hash);
        }
        if (IsCurrentInkPage(sourcePath, pageIndex))
        {
            SyncSessionInkDirtyFlag();
        }
    }

    private bool IsCurrentInkPage(string sourcePath, int pageIndex)
    {
        return pageIndex == _currentPageIndex
            && !string.IsNullOrWhiteSpace(_currentDocumentPath)
            && string.Equals(sourcePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncSessionInkDirtyFlag()
    {
        var isDirty = IsCurrentPageDirty();
        var current = _sessionCoordinator.CurrentState;
        if (current.InkDirty == isDirty)
        {
            return;
        }

        DispatchSessionEvent(isDirty
            ? new MarkInkDirtyEvent()
            : new MarkInkSavedEvent());
    }

    private void TrackInkWalSnapshot(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes, string hash)
    {
        if (!InkPersistenceTogglePolicy.ShouldTrackWal(_inkSaveEnabled))
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(
            () => _inkWal.Upsert(sourcePath, pageIndex, CloneInkStrokes(strokes), hash));
    }

    private void ClearInkWalSnapshot(string sourcePath, int pageIndex)
    {
        _ = SafeActionExecutionExecutor.TryExecute(
            () => _inkWal.Remove(sourcePath, pageIndex));
    }

    private void RecoverInkWalForDirectory(string sourcePath)
    {
        if (!InkPersistenceTogglePolicy.ShouldRecoverWal(_inkSaveEnabled)
            || string.IsNullOrWhiteSpace(sourcePath)
            || _inkPersistence == null)
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var directoryPath = System.IO.Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return;
                }
                var recovered = _inkWal.RecoverDirectory(directoryPath, _inkPersistence, ComputeInkHash);
                if (recovered > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[InkWAL] Recovered {recovered} pending pages in {directoryPath}");
                }
            },
            ex => System.Diagnostics.Debug.WriteLine($"[InkWAL] Recover failed: {ex.Message}"));
    }

    private bool WasPageModifiedInSession(string sourcePath, int pageIndex)
    {
        return _inkDirtyPages.WasModifiedInSession(sourcePath, pageIndex);
    }

    private IEnumerable<string> EnumerateSessionModifiedSourcesInDirectory(string directoryPath)
    {
        return _inkDirtyPages.EnumerateSessionModifiedSourcesInDirectory(directoryPath);
    }

    private static string ComputeInkHash(IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return "empty";
        }

        var builder = new StringBuilder(strokes.Count * 64);
        foreach (var stroke in strokes)
        {
            builder.Append(stroke.Type).Append('|')
                .Append(stroke.BrushStyle).Append('|')
                .Append(stroke.ColorHex).Append('|')
                .Append(stroke.Opacity).Append('|')
                .Append(stroke.BrushSize.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceWidth.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceHeight.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.GeometryPath ?? string.Empty).Append('|')
                .Append(stroke.Ribbons.Count).Append('|');
            foreach (var ribbon in stroke.Ribbons)
            {
                builder.Append(ribbon.GeometryPath ?? string.Empty).Append('@')
                    .Append(ribbon.Opacity.ToString("G17", CultureInfo.InvariantCulture)).Append('@')
                    .Append(ribbon.RibbonT.ToString("G17", CultureInfo.InvariantCulture)).Append(';');
            }
            builder.Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
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
