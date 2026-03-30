namespace ClassroomToolkit.App.Paint;

internal static class CrossPageViewportBoundsPolicy
{
    internal static double ResolveSlackDip(double viewportHeight)
    {
        return Math.Max(
            CrossPageViewportBoundsDefaults.ClampSlackMinDip,
            viewportHeight * CrossPageViewportBoundsDefaults.ClampSlackViewportRatio);
    }

    internal static bool IsTranslateClamped(double originalY, double clampedY)
    {
        return Math.Abs(originalY - clampedY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
    }
}
