namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkPanRedrawPolicy
{
    internal static bool ShouldRequest(
        bool photoInkModeActive,
        double currentTranslateX,
        double currentTranslateY,
        double lastRedrawTranslateX,
        double lastRedrawTranslateY,
        double thresholdDip = InkRuntimeTimingDefaults.PhotoPanRedrawThresholdDip)
    {
        return photoInkModeActive
            && PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
                lastRedrawTranslateX,
                lastRedrawTranslateY,
                currentTranslateX,
                currentTranslateY,
                thresholdDip);
    }
}
