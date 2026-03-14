using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

internal static class OverlayWindowStyleBitsPolicy
{
    internal readonly record struct StyleMask(int SetMask, int ClearMask);

    internal static StyleMask Resolve(bool inputPassthroughEnabled, bool focusBlocked)
    {
        var setMask = 0;
        var clearMask = 0;

        if (inputPassthroughEnabled)
        {
            setMask |= WindowStyleBitMasks.WsExTransparent;
        }
        else
        {
            clearMask |= WindowStyleBitMasks.WsExTransparent;
        }

        if (focusBlocked)
        {
            setMask |= WindowStyleBitMasks.WsExNoActivate;
        }
        else
        {
            clearMask |= WindowStyleBitMasks.WsExNoActivate;
        }

        return new StyleMask(setMask, clearMask);
    }
}
