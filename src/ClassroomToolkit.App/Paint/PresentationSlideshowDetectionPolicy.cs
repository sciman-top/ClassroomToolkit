using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationSlideshowDetectionPolicy
{
    internal static bool IsSlideshow(
        PresentationTarget target,
        PresentationClassifier classifier,
        Func<IntPtr, bool> isFullscreenWindow,
        PresentationType? expectedType = null)
    {
        if (!target.IsValid || target.Info == null)
        {
            return false;
        }

        if (classifier.IsSlideshowWindow(target.Info))
        {
            return true;
        }

        if (!isFullscreenWindow(target.Handle))
        {
            return false;
        }

        var type = expectedType ?? classifier.Classify(target.Info);
        return PresentationFullscreenWindowAdmissionPolicy.ShouldTreatAsPresentationFullscreen(
            targetIsValid: true,
            targetHasInfo: true,
            isFullscreen: true,
            classifiesAsSlideshow: false,
            classifiesAsOffice: type == PresentationType.Office,
            classifiesAsDedicatedWpsRuntime: type == PresentationType.Wps
                && WpsPresentationRuntimePolicy.IsDedicatedSlideshowRuntime(target.Info.ProcessName));
    }
}
