using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowPlacementHelper
{
    /// <summary>
    /// 窗口矩形是否仍与虚拟屏（全部显示器的并集，DIP 空间）相交。
    /// 用于多显示器下保护保存的副屏坐标不被主屏工作区钳制覆盖。
    /// </summary>
    public static bool IntersectsWithVirtualScreen(double left, double top, double width, double height)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        return left + width > virtualLeft
            && top + height > virtualTop
            && left < virtualRight
            && top < virtualBottom;
    }

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
