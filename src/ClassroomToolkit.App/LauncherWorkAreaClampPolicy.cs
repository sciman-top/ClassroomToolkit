using System.Windows;

namespace ClassroomToolkit.App;

internal static class LauncherWorkAreaClampPolicy
{
    internal static System.Windows.Point Resolve(
        double left,
        double top,
        double width,
        double height,
        Rect workArea)
    {
        var resolvedLeft = left;
        var resolvedTop = top;

        if (resolvedLeft < workArea.Left)
        {
            resolvedLeft = workArea.Left;
        }

        if (resolvedTop < workArea.Top)
        {
            resolvedTop = workArea.Top;
        }

        if (resolvedLeft + width > workArea.Right)
        {
            resolvedLeft = workArea.Right - width;
        }

        if (resolvedTop + height > workArea.Bottom)
        {
            resolvedTop = workArea.Bottom - height;
        }

        return new System.Windows.Point(resolvedLeft, resolvedTop);
    }
}
