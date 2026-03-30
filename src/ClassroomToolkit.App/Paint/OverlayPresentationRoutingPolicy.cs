using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Paint;

internal static class OverlayPresentationRoutingPolicy
{
    internal static bool CanRouteFromAuxWindow(
        UiNavigationMode navigationMode,
        bool photoModeActive,
        bool boardActive)
    {
        if (photoModeActive || boardActive)
        {
            return false;
        }

        return UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode);
    }

    internal static bool CanRouteFromOverlay(
        UiNavigationMode navigationMode,
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inputPassthroughEnabled)
    {
        if (photoModeActive || boardActive)
        {
            return false;
        }

        if (!UiSessionPresentationInputPolicy.AllowsPresentationInput(navigationMode))
        {
            return false;
        }

        if (mode == PaintToolMode.Cursor && inputPassthroughEnabled)
        {
            return false;
        }

        return true;
    }
}
