namespace ClassroomToolkit.App.Paint;

internal enum CrossPageInteractiveInkSlotRemapAction
{
    KeepCurrentFrame = 0,
    UsePreservedFrame = 1,
    ClearCurrentFrame = 2
}

internal static class CrossPageInteractiveInkSlotRemapPolicy
{
    internal static CrossPageInteractiveInkSlotRemapAction Resolve(
        bool slotPageChanged,
        bool hasResolvedInkBitmap,
        bool hasCurrentInkFrame,
        bool hasPreservedInkFrame,
        bool inkOperationActive)
    {
        if (!slotPageChanged || hasResolvedInkBitmap)
        {
            return CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
        }

        // During active ink mutation, preserved slot frames can belong to the previous
        // page mapping and cause one-frame cross-page ghost flashes on remap.
        if (inkOperationActive)
        {
            return hasCurrentInkFrame
                ? CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame
                : CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
        }

        if (hasPreservedInkFrame)
        {
            return CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame;
        }

        return hasCurrentInkFrame
            ? CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame
            : CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame;
    }
}
