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
            return new CrossPageNeighborInkFrameDecision(
                ClearCurrentFrame: true,
                AllowResolvedInkReplacement: false,
                KeepVisible: false);
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
}
