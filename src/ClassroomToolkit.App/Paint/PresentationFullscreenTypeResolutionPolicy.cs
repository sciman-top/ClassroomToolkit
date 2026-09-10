using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationFullscreenTypeResolutionPolicy
{
    internal static PresentationType Resolve(
        bool wpsFullscreen,
        bool officeFullscreen,
        PresentationType currentPresentationType,
        PresentationType foregroundType = PresentationType.None,
        bool foregroundIsFullscreen = false)
    {
        // When both applications have a fullscreen candidate, the fresh
        // foreground window is the only safe discriminator.  A cached current
        // type may belong to the previous monitor or slideshow session.
        if (foregroundIsFullscreen
            && foregroundType == PresentationType.Wps
            && wpsFullscreen)
        {
            return PresentationType.Wps;
        }

        if (foregroundIsFullscreen
            && foregroundType == PresentationType.Office
            && officeFullscreen)
        {
            return PresentationType.Office;
        }

        if (wpsFullscreen && !officeFullscreen)
        {
            return PresentationType.Wps;
        }

        if (officeFullscreen && !wpsFullscreen)
        {
            return PresentationType.Office;
        }

        if (wpsFullscreen
            && officeFullscreen
            && currentPresentationType is PresentationType.Wps or PresentationType.Office)
        {
            return currentPresentationType;
        }

        return PresentationType.None;
    }
}
