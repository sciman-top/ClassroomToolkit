using System;

namespace ClassroomToolkit.App.Photos;

internal static class PhotoUnifiedTransformChangePolicy
{
    internal static bool HasChanged(
        bool unifiedTransformEnabled,
        double currentScaleX,
        double currentScaleY,
        double currentTranslateX,
        double currentTranslateY,
        double nextScaleX,
        double nextScaleY,
        double nextTranslateX,
        double nextTranslateY,
        double epsilon)
    {
        if (!unifiedTransformEnabled)
        {
            return true;
        }

        return !AreClose(currentScaleX, nextScaleX, epsilon)
               || !AreClose(currentScaleY, nextScaleY, epsilon)
               || !AreClose(currentTranslateX, nextTranslateX, epsilon)
               || !AreClose(currentTranslateY, nextTranslateY, epsilon);
    }

    private static bool AreClose(double left, double right, double epsilon)
    {
        return Math.Abs(left - right) < epsilon;
    }
}
