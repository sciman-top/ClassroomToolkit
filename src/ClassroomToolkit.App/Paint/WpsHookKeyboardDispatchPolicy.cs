namespace ClassroomToolkit.App.Paint;

internal static class WpsHookKeyboardDispatchPolicy
{
    internal static bool ShouldDispatchFromHook(
        string source,
        PaintToolMode mode,
        bool targetForeground,
        bool foregroundOwnedByCurrentProcess)
    {
        if (!string.Equals(source, "keyboard", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mode != PaintToolMode.Cursor)
        {
            return true;
        }

        // Cursor mode should prefer native keyboard handling when slideshow
        // already owns foreground. Keep hook dispatch only when our overlay
        // process currently owns foreground.
        if (targetForeground && !foregroundOwnedByCurrentProcess)
        {
            return false;
        }

        return true;
    }
}
