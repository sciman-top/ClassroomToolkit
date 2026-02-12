namespace ClassroomToolkit.Services.Presentation;

public interface IForegroundWindowController
{
    bool IsForeground(IntPtr hwnd);

    bool EnsureForeground(IntPtr hwnd);
}
