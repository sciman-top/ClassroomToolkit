namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborBitmapResolvePolicy
{
    internal static bool ShouldAllowSynchronousResolve(
        bool interactionActive,
        bool slotPageChanged)
    {
        // During active pan/gesture with slot remap, synchronous decode/render can
        // block input handling and cause visible lag. Prefer cache-only frames.
        if (interactionActive && slotPageChanged)
        {
            return false;
        }

        return true;
    }
}
