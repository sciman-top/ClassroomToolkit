using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
