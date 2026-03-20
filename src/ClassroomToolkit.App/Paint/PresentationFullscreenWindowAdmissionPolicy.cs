namespace ClassroomToolkit.App.Paint;

internal static class PresentationFullscreenWindowAdmissionPolicy
{
    internal static bool ShouldTreatAsPresentationFullscreen(
        bool targetIsValid,
        bool targetHasInfo,
        bool isFullscreen,
        bool classifiesAsSlideshow,
        bool classifiesAsOffice)
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
        // Keep WPS strict to avoid matching unrelated fullscreen windows.
        return classifiesAsOffice;
    }
}
