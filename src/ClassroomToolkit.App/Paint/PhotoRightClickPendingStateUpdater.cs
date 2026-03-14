using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoRightClickPendingStateUpdater
{
    internal static void Arm(
        ref bool pending,
        ref WpfPoint start,
        WpfPoint point)
    {
        pending = true;
        start = point;
    }

    internal static void Clear(ref bool pending)
    {
        pending = false;
    }

    internal static void UpdateByMove(
        ref bool pending,
        WpfPoint start,
        WpfPoint current)
    {
        if (!pending)
        {
            return;
        }

        var delta = current - start;
        if (PhotoRightClickContextMenuPolicy.ShouldCancelPendingByMove(delta))
        {
            pending = false;
        }
    }
}
