namespace ClassroomToolkit.App.Paint;

internal static class OverlayWindowStyleApplyPolicy
{
    internal static bool ShouldApply(
        bool inputPassthroughEnabled,
        bool focusBlocked,
        bool? lastInputPassthroughEnabled,
        bool? lastFocusBlocked)
    {
        return lastInputPassthroughEnabled != inputPassthroughEnabled
            || lastFocusBlocked != focusBlocked;
    }
}
