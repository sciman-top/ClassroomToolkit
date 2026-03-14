using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkPanCompensationPolicy
{
    internal static Vector Resolve(
        bool photoInkModeActive,
        double currentTranslateX,
        double currentTranslateY,
        double lastRedrawTranslateX,
        double lastRedrawTranslateY)
    {
        if (!photoInkModeActive)
        {
            return new Vector(0, 0);
        }

        return new Vector(
            currentTranslateX - lastRedrawTranslateX,
            currentTranslateY - lastRedrawTranslateY);
    }
}
