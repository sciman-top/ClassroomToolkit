using System;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Services.Presentation;

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
