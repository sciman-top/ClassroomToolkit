namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborInkFramePolicy
{
    internal static CrossPageNeighborInkFrameDecision Resolve(
        bool slotPageChanged,
        bool hasCurrentInkFrame,
        bool hasTargetInkStrokes,
        bool holdInkReplacement,
        bool usedPreservedInkFrame,
        bool hasResolvedInkBitmap)
    {
        if (!hasTargetInkStrokes && !hasResolvedInkBitmap)
        {
            var retainCurrentFrame = holdInkReplacement
                || usedPreservedInkFrame
                || (hasCurrentInkFrame && !slotPageChanged);
            return new CrossPageNeighborInkFrameDecision(
                ClearCurrentFrame: !retainCurrentFrame,
                AllowResolvedInkReplacement: false,
                KeepVisible: retainCurrentFrame);
        }

        var keepExistingFrame = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged,
            hasCurrentInkFrame);
        var preservePreservedUntilReplacement = usedPreservedInkFrame && !hasResolvedInkBitmap;
        var clearCurrentFrame = !keepExistingFrame
            && !holdInkReplacement
            && !preservePreservedUntilReplacement;
        var allowResolvedInkReplacement = hasResolvedInkBitmap
            && !holdInkReplacement
            && CrossPageNeighborInkReplacementPolicy.ShouldReplace(
                slotPageChanged,
                hasCurrentInkFrame,
                usedPreservedInkFrame);
        var keepVisible = keepExistingFrame
            || preservePreservedUntilReplacement
            || allowResolvedInkReplacement
            || holdInkReplacement;

        return new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: clearCurrentFrame,
            AllowResolvedInkReplacement: allowResolvedInkReplacement,
            KeepVisible: keepVisible);
    }

    internal static bool ShouldClearWhenUnresolved(
        CrossPageNeighborInkFrameDecision decision,
        bool hasResolvedInkBitmap)
    {
        return decision.ClearCurrentFrame && !hasResolvedInkBitmap;
    }
}
