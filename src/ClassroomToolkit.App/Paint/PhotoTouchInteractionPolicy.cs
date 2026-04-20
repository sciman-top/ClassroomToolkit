using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoTouchInteractionPolicy
{
    internal static bool ShouldUseSingleTouchPan(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount)
    {
        return photoModeActive
            && !boardActive
            && !inkOperationActive
            && activeTouchCount == 1;
    }

    internal static bool ShouldUseManipulationZoom(int activeTouchCount)
    {
        return activeTouchCount >= 2;
    }

    internal static bool ShouldIgnorePromotedTouchStylus(TabletDeviceType tabletDeviceType)
    {
        return tabletDeviceType == TabletDeviceType.Touch;
    }
}
