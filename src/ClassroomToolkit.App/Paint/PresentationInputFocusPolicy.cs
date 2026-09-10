namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Defines the small set of application windows that may act as the source of
/// a background presentation-navigation request.  Process ownership alone is
/// intentionally insufficient: settings dialogs, roll-call windows and text
/// editors can all belong to this process while still owning unrelated input.
/// </summary>
internal static class PresentationInputFocusPolicy
{
    internal static bool IsAuthorizedForeground(
        IntPtr foregroundWindow,
        IntPtr overlayWindow,
        IntPtr toolbarWindow)
    {
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        return foregroundWindow == overlayWindow
               || foregroundWindow == toolbarWindow;
    }
}
