using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkPanCompensationGeometryPolicy
{
    private const double NonZeroOffsetEpsilon = 0.0001;

    internal static bool ShouldApplyCompensation(
        bool photoInkModeActive,
        Transform? rasterRenderTransform,
        TranslateTransform panCompensation)
    {
        if (!photoInkModeActive || !ReferenceEquals(rasterRenderTransform, panCompensation))
        {
            return false;
        }

        return Math.Abs(panCompensation.X) > NonZeroOffsetEpsilon
            || Math.Abs(panCompensation.Y) > NonZeroOffsetEpsilon;
    }

    internal static Geometry AdjustToRasterSpace(
        Geometry geometry,
        double panCompensationX,
        double panCompensationY)
    {
        var clone = geometry.Clone();
        clone.Transform = new TranslateTransform(-panCompensationX, -panCompensationY);
        var adjusted = clone.GetFlattenedPathGeometry();
        if (adjusted.CanFreeze)
        {
            adjusted.Freeze();
        }

        return adjusted;
    }
}
