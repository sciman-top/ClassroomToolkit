using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private WpfPoint ResolvePhotoZoomAnchor()
    {
        if (_photoFullscreen)
        {
            // Fullscreen HWND bounds are applied in physical pixels.  Prefer
            // the monitor's DIP extent here so a transient WPF work-area size
            // cannot move the zoom anchor above the real screen center.
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            if (monitor.Width > PhotoTransformViewportDefaults.MinUsableViewportDip
                && monitor.Height > PhotoTransformViewportDefaults.MinUsableViewportDip)
            {
                return PhotoZoomAnchorPolicy.ResolveViewportCenter(monitor.Width, monitor.Height);
            }
        }

        var viewportWidth = OverlayRoot.ActualWidth;
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }

        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportWidth = ActualWidth;
            viewportHeight = ActualHeight;
        }

        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
            viewportHeight = monitor.Height;
        }

        return PhotoZoomAnchorPolicy.ResolveViewportCenter(viewportWidth, viewportHeight);
    }

    private WpfPoint ToPhotoSpace(WpfPoint point)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive()))
        {
            return point;
        }

        var inverse = GetPhotoInverseMatrix();
        return inverse.Transform(point);
    }

    private Geometry? ToPhotoGeometry(Geometry geometry)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive())
            || geometry == null)
        {
            return geometry;
        }

        var inverse = GetPhotoInverseMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }

        return flattened;
    }

    private Geometry? ToScreenGeometry(Geometry geometry)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive())
            || geometry == null)
        {
            return geometry;
        }

        return PhotoInkCoordinateMapper.ToScreenGeometry(
            geometry,
            _photoPageScale.ScaleX,
            _photoPageScale.ScaleY,
            _photoScale.ScaleX,
            _photoScale.ScaleY,
            _photoTranslate.X,
            _photoTranslate.Y);
    }

    private Matrix GetPhotoMatrix()
    {
        return PhotoInkCoordinateMapper.CreateForwardMatrix(
            _photoPageScale.ScaleX,
            _photoPageScale.ScaleY,
            _photoScale.ScaleX,
            _photoScale.ScaleY,
            _photoTranslate.X,
            _photoTranslate.Y);
    }

    private Matrix GetPhotoInverseMatrix()
    {
        if (PhotoInkCoordinateMapper.TryCreateInverseMatrix(
                _photoPageScale.ScaleX,
                _photoPageScale.ScaleY,
                _photoScale.ScaleX,
                _photoScale.ScaleY,
                _photoTranslate.X,
                _photoTranslate.Y,
                out var inverse,
                PhotoTransformMathDefaults.InverseScaleEpsilon))
        {
            _lastValidPhotoInverseMatrix = inverse;
            return inverse;
        }

        return _lastValidPhotoInverseMatrix;
    }
}
