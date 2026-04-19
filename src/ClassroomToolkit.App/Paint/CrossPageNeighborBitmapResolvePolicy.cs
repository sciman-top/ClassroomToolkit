namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborBitmapResolvePolicy
{
    internal static bool ShouldAllowSynchronousResolve(
        bool interactionActive,
        bool slotPageChanged)
    {
        if (!interactionActive)
        {
            return true;
        }

        // During active interaction, only allow sync resolve for unchanged slots.
        // Slot remap should stay async to avoid decode/render spikes and ghost flashes.
        return !slotPageChanged;
    }
}
