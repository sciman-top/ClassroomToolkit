using ClassroomToolkit.Interop.Presentation;
using System;

namespace ClassroomToolkit.Services.Presentation;

public interface IForegroundWindowController
{
    bool IsForeground(IntPtr hwnd);

    bool EnsureForeground(IntPtr hwnd);
}

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

public interface IPresentationWindowValidator
{
    bool IsWindowValid(IntPtr hwnd);
}

public sealed class Win32PresentationWindowValidator : IPresentationWindowValidator
{
    public bool IsWindowValid(IntPtr hwnd)
    {
        return PresentationWindowFocus.IsWindowValid(hwnd);
    }
}
