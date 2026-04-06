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

        // During active ink mutation, prefer preserved frame for the target page when available.
        // This avoids clear-then-fill flashes on seam crossing while keeping ownership by page uid.
        if (inkOperationActive)
        {
            if (hasPreservedInkFrame)
            {
                return CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame;
            }

            // Slot remapped without a preserved target frame: keeping current frame would
            // display old-page ink on the new slot (flash + jitter during seam crossing).
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
