namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractivePageReplacementPolicy
{
    internal static bool ShouldReplace(
        bool hasResolvedTargetFrame,
        bool interactionActive,
        bool slotPageChanged,
        bool hasCurrentFrame)
    {
        if (!hasResolvedTargetFrame)
        {
            return false;
        }

        // During interaction, keep same-page slot stable and move by transform only.
        // This avoids replacing with a different bitmap instance and reduces flash.
        if (interactionActive && !slotPageChanged && hasCurrentFrame)
        {
            return false;
        }

        return true;
    }
}
