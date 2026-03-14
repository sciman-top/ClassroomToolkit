namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanDragActivationPolicy
{
    internal static bool ShouldActivateCrossPageDrag(
        bool crossPageDisplayActive,
        double deltaYDip,
        double thresholdDip = PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip)
    {
        return crossPageDisplayActive
            && System.Math.Abs(deltaYDip) > thresholdDip;
    }
}
