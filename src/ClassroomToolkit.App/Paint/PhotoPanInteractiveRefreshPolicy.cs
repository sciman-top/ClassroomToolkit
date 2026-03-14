namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanInteractiveRefreshPolicy
{
    internal static bool ShouldRefresh(
        double lastRefreshTranslateX,
        double lastRefreshTranslateY,
        double currentTranslateX,
        double currentTranslateY,
        double thresholdDip = 2.0)
    {
        return System.Math.Abs(currentTranslateX - lastRefreshTranslateX) >= thresholdDip
            || System.Math.Abs(currentTranslateY - lastRefreshTranslateY) >= thresholdDip;
    }
}
