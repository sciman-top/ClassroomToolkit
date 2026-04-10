using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
}

