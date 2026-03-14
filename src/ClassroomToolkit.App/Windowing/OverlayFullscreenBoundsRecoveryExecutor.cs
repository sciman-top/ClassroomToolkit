namespace ClassroomToolkit.App.Windowing;

internal static class OverlayFullscreenBoundsRecoveryExecutor
{
    internal static void Apply(
        bool shouldRecover,
        Action<bool> normalizeWindowState,
        Action applyImmediateBounds,
        Action applyDeferredBounds)
    {
        if (!shouldRecover)
        {
            return;
        }

        normalizeWindowState(true);
        applyImmediateBounds();
        applyDeferredBounds();
    }
}
