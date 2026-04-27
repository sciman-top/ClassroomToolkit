using System.Windows;

namespace ClassroomToolkit.App.Helpers;

internal static class WindowPlacementHelper
{
    public static void EnsureVisible(Window window)
    {
        if (window == null)
        {
            return;
        }
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;

        if (window.Left < left)
        {
            window.Left = left;
        }
        if (window.Top < top)
        {
            window.Top = top;
        }
        if (window.Left + window.Width > right)
        {
            window.Left = right - window.Width;
        }
        if (window.Top + window.Height > bottom)
        {
            window.Top = bottom - window.Height;
        }
    }

    public static void CenterOnVirtualScreen(Window window)
    {
        if (window == null)
        {
            return;
        }
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var width = SystemParameters.VirtualScreenWidth;
        var height = SystemParameters.VirtualScreenHeight;

        window.Left = left + Math.Max(0, (width - window.Width) / 2);
        window.Top = top + Math.Max(0, (height - window.Height) / 2);
        EnsureVisible(window);
    }
}
