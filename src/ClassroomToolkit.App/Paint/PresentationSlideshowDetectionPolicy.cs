namespace ClassroomToolkit.App.Paint;

internal static class PresentationSlideshowDetectionPolicy
{
    internal static bool IsSlideshow(
        PresentationTarget target,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }

        if (classifier.IsSlideshowWindow(target.Info))
        {
            return true;
        }

        return isFullscreenWindow(target.Handle);
    }
}
