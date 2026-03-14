namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanRefreshGatePolicy
{
    internal static bool ShouldRefresh(
        double beforeTranslateX,
        double beforeTranslateY,
        double afterTranslateX,
        double afterTranslateY,
        double epsilonDip = CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip)
    {
        return System.Math.Abs(afterTranslateX - beforeTranslateX) > epsilonDip
            || System.Math.Abs(afterTranslateY - beforeTranslateY) > epsilonDip;
    }
}
