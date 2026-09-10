using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClassroomToolkit.Interop;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct DpiSuggestedBounds(
    int Left,
    int Top,
    int Right,
    int Bottom);

internal static class DpiSuggestedBoundsInterop
{
    internal static bool TryCopy(
        IntPtr lParam,
        out DpiSuggestedBounds suggestedBounds)
    {
        suggestedBounds = default;
        if (lParam == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.NativeRect nativeRect;
        try
        {
            nativeRect = Marshal.PtrToStructure<NativeMethods.NativeRect>(lParam);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PaintOverlay] WM_DPICHANGED RECT read failed: {ex.GetType().Name} - {ex.Message}");
            return false;
        }

        var width = (long)nativeRect.Right - nativeRect.Left;
        var height = (long)nativeRect.Bottom - nativeRect.Top;
        if (width <= 0
            || width > int.MaxValue
            || height <= 0
            || height > int.MaxValue)
        {
            return false;
        }

        suggestedBounds = new DpiSuggestedBounds(
            nativeRect.Left,
            nativeRect.Top,
            nativeRect.Right,
            nativeRect.Bottom);
        return true;
    }
}
