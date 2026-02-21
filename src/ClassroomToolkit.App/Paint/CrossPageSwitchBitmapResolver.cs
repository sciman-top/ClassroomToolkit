using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageSwitchBitmapResolver
{
    internal static TBitmap? ResolveForInteractiveSwitch<TBitmap>(
        bool interactiveSwitch,
        TBitmap? preloadedBitmap,
        Func<TBitmap?> loadBitmap)
        where TBitmap : class
    {
        if (interactiveSwitch && preloadedBitmap != null)
        {
            return preloadedBitmap;
        }

        return loadBitmap();
    }
}
