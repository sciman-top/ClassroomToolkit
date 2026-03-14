namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborInkFramePolicy
{
    internal static CrossPageNeighborInkFrameDecision Resolve(
        bool slotPageChanged,
        bool hasCurrentInkFrame,
        bool holdInkReplacement,
        bool usedPreservedInkFrame,
        bool hasResolvedInkBitmap)
    {
        var keepExistingFrame = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged,
            hasCurrentInkFrame);
        var preserveCurrentUntilReplacement = slotPageChanged && hasCurrentInkFrame && !hasResolvedInkBitmap;
        var preservePreservedUntilReplacement = usedPreservedInkFrame && !hasResolvedInkBitmap;
        var clearCurrentFrame = !keepExistingFrame
            && !holdInkReplacement
            && !preserveCurrentUntilReplacement
            && !preservePreservedUntilReplacement;
        var allowResolvedInkReplacement = hasResolvedInkBitmap
            && !holdInkReplacement
            && CrossPageNeighborInkReplacementPolicy.ShouldReplace(
                slotPageChanged,
                hasCurrentInkFrame,
                usedPreservedInkFrame);
        var keepVisible = hasCurrentInkFrame || usedPreservedInkFrame || allowResolvedInkReplacement || holdInkReplacement;

        return new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: clearCurrentFrame,
            AllowResolvedInkReplacement: allowResolvedInkReplacement,
            KeepVisible: keepVisible);
    }
}
