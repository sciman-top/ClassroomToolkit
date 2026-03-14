namespace ClassroomToolkit.App.Paint;

internal static class PhotoViewportStepPolicy
{
    internal const double OverlapRatio = 0.12;
    internal const double MinStepDip = 24.0;

    internal static double ResolveStep(double viewportHeight)
    {
        return Math.Max(MinStepDip, viewportHeight * (1.0 - OverlapRatio));
    }
}
