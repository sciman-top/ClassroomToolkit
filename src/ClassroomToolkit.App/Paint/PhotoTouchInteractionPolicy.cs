using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoTouchInteractionPolicy
{
    internal static bool ShouldUseManipulation(int activeTouchCount)
    {
        // WPF only starts a manipulation when the originating TouchDown is
        // left unhandled.  A one-finger manipulation is the canonical pan
        // path; a second finger upgrades the same stream to pinch/translate.
        return activeTouchCount >= 1;
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
