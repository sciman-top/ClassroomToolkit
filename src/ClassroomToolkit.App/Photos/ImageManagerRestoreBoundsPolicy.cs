using System;
using System.Windows;

namespace ClassroomToolkit.App.Photos;

internal readonly record struct ImageManagerRestoreBoundsPlan(
    double Width,
    double Height,
    double Left,
    double Top);

internal static class ImageManagerRestoreBoundsPolicy
{
    internal const double WorkAreaWidthRatio = 0.92;
    internal const double WorkAreaHeightRatio = 0.90;

    internal static ImageManagerRestoreBoundsPlan Resolve(
        double restoredWidth,
        double restoredHeight,
        double defaultWidth,
        double defaultHeight,
        double minWidth,
        double minHeight,
        Rect workArea)
    {
        var safeDefaultWidth = Math.Max(1, defaultWidth);
        var safeDefaultHeight = Math.Max(1, defaultHeight);
        var safeMinWidth = Math.Max(1, minWidth);
        var safeMinHeight = Math.Max(1, minHeight);

        // Restore should return to a manageable, teaching-friendly window size.
        // Keep smaller user-resized bounds, but clamp oversized history to default size.
        var maxRestoreWidth = Math.Max(safeMinWidth, safeDefaultWidth);
        var maxRestoreHeight = Math.Max(safeMinHeight, safeDefaultHeight);
        var desiredWidth = Math.Clamp(restoredWidth, safeMinWidth, maxRestoreWidth);
        var desiredHeight = Math.Clamp(restoredHeight, safeMinHeight, maxRestoreHeight);

        if (workArea.Width > 0 && workArea.Height > 0)
        {
            var maxWidth = Math.Max(safeMinWidth, workArea.Width * WorkAreaWidthRatio);
            var maxHeight = Math.Max(safeMinHeight, workArea.Height * WorkAreaHeightRatio);
            desiredWidth = Math.Clamp(desiredWidth, safeMinWidth, maxWidth);
            desiredHeight = Math.Clamp(desiredHeight, safeMinHeight, maxHeight);

            var left = workArea.Left + (workArea.Width - desiredWidth) * 0.5;
            var top = workArea.Top + (workArea.Height - desiredHeight) * 0.5;
            return new ImageManagerRestoreBoundsPlan(
                Width: desiredWidth,
                Height: desiredHeight,
                Left: left,
                Top: top);
        }

        return new ImageManagerRestoreBoundsPlan(
            Width: Math.Max(safeMinWidth, desiredWidth),
            Height: Math.Max(safeMinHeight, desiredHeight),
            Left: double.NaN,
            Top: double.NaN);
    }
}
