using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

internal sealed class PresentationForegroundController : IForegroundWindowController
{
    public bool IsForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.IsForeground(hwnd);
    }

    public bool EnsureForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.EnsureForeground(hwnd);
    }
}
