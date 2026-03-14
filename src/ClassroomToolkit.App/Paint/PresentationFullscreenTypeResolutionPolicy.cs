namespace ClassroomToolkit.App.Paint;

internal static class PresentationFullscreenTypeResolutionPolicy
{
    internal static PresentationType Resolve(
        bool wpsFullscreen,
        bool officeFullscreen,
        PresentationType currentPresentationType)
    {
        if (wpsFullscreen && !officeFullscreen)
        {
            return PresentationType.Wps;
        }

        if (officeFullscreen && !wpsFullscreen)
        {
            return PresentationType.Office;
        }

        if (wpsFullscreen && officeFullscreen && currentPresentationType != PresentationType.None)
        {
            return currentPresentationType;
        }

        return PresentationType.None;
    }
}
