using System;

namespace ClassroomToolkit.App.Paint;

internal static class WpsHookAuxWindowSuppressionPolicy
{
    internal static bool ShouldSuppressNavigation(
        bool foregroundOwnedByCurrentProcess,
        IntPtr foregroundWindow,
        IntPtr overlayWindow)
    {
        if (!foregroundOwnedByCurrentProcess || foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        // Keep remote clicker navigation available when overlay itself is foreground.
        return foregroundWindow != overlayWindow;
    }
}
