using System;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.App.Ink;

public sealed record PresentationSlideInfo(string FilePath, string DisplayName, int SlideIndex);

public static class PresentationSlideResolver
{
    public static PresentationSlideInfo? TryResolvePowerPoint()
    {
        try
        {
            if (CLSIDFromProgID("PowerPoint.Application", out var clsid) != 0)
            {
                return null;
            }
            var app = GetActiveComObject(clsid);
            if (app == null)
            {
                return null;
            }
            dynamic ppt = app!;
            if (ppt.SlideShowWindows == null || ppt.SlideShowWindows.Count < 1)
            {
                return null;
            }
            dynamic window = ppt.SlideShowWindows[1];
            object? slideObj = window.View?.Slide;
            if (slideObj == null)
            {
                return null;
            }
            dynamic slide = slideObj;
            string filePath = ppt.ActivePresentation?.FullName ?? string.Empty;
            string name = ppt.ActivePresentation?.Name ?? string.Empty;
            int slideIndex = (int)slide.SlideIndex;
            if (slideIndex <= 0)
            {
                return null;
            }
            return new PresentationSlideInfo(filePath, name, slideIndex);
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidOleVariantTypeException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static object? GetActiveComObject(Guid clsid)
    {
        try
        {
            var hr = GetActiveObject(ref clsid, IntPtr.Zero, out var result);
            if (hr != 0)
            {
                return null;
            }
            return result;
        }
        catch (InvalidOleVariantTypeException)
        {
            return null;
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, out object? result);
}
