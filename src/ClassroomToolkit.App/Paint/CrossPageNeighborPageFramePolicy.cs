namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageNeighborPageFrameDecision(
    bool HoldCurrentFrame,
    bool CollapseSlot);

internal static class CrossPageNeighborPageFramePolicy
{
    internal static CrossPageNeighborPageFrameDecision Resolve(
        bool slotPageChanged,
        bool hasCurrentFrame,
        bool hasResolvedTargetFrame,
        bool interactionActive = false,
        bool preferHoldCurrentFrameOnSlotRemap = false)
    {
        if (hasResolvedTargetFrame)
        {
            return new CrossPageNeighborPageFrameDecision(
                HoldCurrentFrame: false,
                CollapseSlot: false);
        }

        if (hasCurrentFrame && interactionActive)
        {
            // Keep same-page slot stable during active interaction to avoid transient flicker.
            // During zoom-out seam expansion, temporarily holding the current slot frame also
            // avoids blank flashes when the remapped target page bitmap is still prefetching.
            // Callers must opt-in for slot remap continuity explicitly.
            if (!slotPageChanged || preferHoldCurrentFrameOnSlotRemap)
            {
                return new CrossPageNeighborPageFrameDecision(
                    HoldCurrentFrame: true,
                    CollapseSlot: false);
            }
        }

        return new CrossPageNeighborPageFrameDecision(
            HoldCurrentFrame: false,
            CollapseSlot: true);
    }
}
