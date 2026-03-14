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
        bool interactionActive = false)
    {
        if (hasResolvedTargetFrame)
        {
            return new CrossPageNeighborPageFrameDecision(
                HoldCurrentFrame: false,
                CollapseSlot: false);
        }

        if (hasCurrentFrame && interactionActive && !slotPageChanged)
        {
            // Keep same-page slot stable during active interaction to avoid transient flicker.
            // For slot remap (different page), never keep old page frame on the new slot.
            return new CrossPageNeighborPageFrameDecision(
                HoldCurrentFrame: true,
                CollapseSlot: false);
        }

        return new CrossPageNeighborPageFrameDecision(
            HoldCurrentFrame: false,
            CollapseSlot: true);
    }
}
