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

        if (hasCurrentFrame && (slotPageChanged || interactionActive))
        {
            // Target page bitmap is temporarily unavailable: preserve current slot frame
            // to avoid one-frame page disappear/flicker during cross-page interactions.
            return new CrossPageNeighborPageFrameDecision(
                HoldCurrentFrame: true,
                CollapseSlot: false);
        }

        return new CrossPageNeighborPageFrameDecision(
            HoldCurrentFrame: false,
            CollapseSlot: true);
    }
}
