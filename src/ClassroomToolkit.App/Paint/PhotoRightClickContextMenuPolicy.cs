using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoRightClickContextMenuPolicy
{
    internal static bool ShouldArmPending(
        bool photoModeActive,
        bool photoFullscreen,
        PaintToolMode mode)
    {
        return photoModeActive && photoFullscreen && mode == PaintToolMode.Cursor;
    }

    internal static bool ShouldCancelPendingByMove(
        Vector delta,
        double thresholdDip = PhotoRightClickContextMenuDefaults.CancelMoveThresholdDip)
    {
        var threshold = Math.Max(PhotoRightClickContextMenuDefaults.MinThresholdDip, thresholdDip);
        return delta.Length > threshold;
    }

    internal static bool ShouldShowContextMenuOnUp(
        bool rightClickPending,
        bool photoModeActive,
        bool photoFullscreen,
        PaintToolMode mode)
    {
        return rightClickPending && ShouldArmPending(photoModeActive, photoFullscreen, mode);
    }
}
