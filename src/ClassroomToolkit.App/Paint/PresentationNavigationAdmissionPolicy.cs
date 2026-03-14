namespace ClassroomToolkit.App.Paint;

internal static class PresentationNavigationAdmissionPolicy
{
    internal static bool ShouldAttempt(
        bool allowChannel,
        bool boardActive,
        bool targetIsValid,
        bool targetHasInfo,
        bool targetIsSlideshow,
        bool allowBackground,
        bool targetForeground)
    {
        if (!allowChannel || boardActive)
        {
            return false;
        }

        if (!targetIsValid || !targetHasInfo || !targetIsSlideshow)
        {
            return false;
        }

        if (!allowBackground && !targetForeground)
        {
            return false;
        }

        return true;
    }
}
