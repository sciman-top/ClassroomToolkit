namespace ClassroomToolkit.App.Paint;

internal static class PresentationFullscreenWindowAdmissionPolicy
{
    internal static bool ShouldTreatAsPresentationFullscreen(
        bool targetIsValid,
        bool targetHasInfo,
        bool isFullscreen,
        bool classifiesAsSlideshow,
        bool classifiesAsOffice,
        bool classifiesAsDedicatedWpsRuntime)
    {
        if (!targetIsValid || !targetHasInfo || !isFullscreen)
        {
            return false;
        }

        if (classifiesAsSlideshow)
        {
            return true;
        }

        // Office slideshow may switch runtime classes in pen/annotation mode.
        if (classifiesAsOffice)
        {
            return true;
        }

        // Newer WPS builds host slideshow in a dedicated wpp/wppt runtime whose
        // top-level window may expose only generic Qt classes.
        return classifiesAsDedicatedWpsRuntime;
    }
}
