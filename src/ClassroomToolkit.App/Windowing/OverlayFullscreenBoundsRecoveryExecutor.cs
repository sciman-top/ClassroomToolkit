using System;

namespace ClassroomToolkit.App.Windowing;

internal static class OverlayFullscreenBoundsRecoveryExecutor
{
    internal static void Apply(
        bool shouldRecover,
        Action<bool> normalizeWindowState,
        Action applyImmediateBounds,
        Action applyDeferredBounds)
    {
        ArgumentNullException.ThrowIfNull(normalizeWindowState);
        ArgumentNullException.ThrowIfNull(applyImmediateBounds);
        ArgumentNullException.ThrowIfNull(applyDeferredBounds);

        if (!shouldRecover)
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(() => normalizeWindowState(true));
        _ = SafeActionExecutionExecutor.TryExecute(applyImmediateBounds);
        _ = SafeActionExecutionExecutor.TryExecute(applyDeferredBounds);
    }
}
