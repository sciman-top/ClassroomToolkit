namespace ClassroomToolkit.App.Paint;

internal static class WpsFullscreenExitPolicy
{
    internal static bool ShouldTreatAsActiveFullscreen(
        bool hasFullscreenCandidate,
        PresentationType foregroundType,
        bool foregroundIsFullscreen,
        bool foregroundOwnedByCurrentProcess)
    {
        if (!hasFullscreenCandidate)
        {
            return false;
        }

        if (foregroundOwnedByCurrentProcess)
        {
            return true;
        }

        // WPS may leave a background fullscreen candidate alive briefly after exit.
        // If foreground already returned to a non-fullscreen WPS window, treat slideshow as ended.
        if (foregroundType == PresentationType.Wps && !foregroundIsFullscreen)
        {
            return false;
        }

        return true;
    }
}
