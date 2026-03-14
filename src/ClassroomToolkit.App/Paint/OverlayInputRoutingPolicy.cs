namespace ClassroomToolkit.App.Paint;

internal enum OverlayWheelInputRoute
{
    Ignore = 0,
    ConsumeForBoard = 1,
    HandlePhoto = 2,
    RoutePresentation = 3
}

internal enum OverlayKeyInputRoute
{
    Ignore = 0,
    Consume = 1,
    RoutePresentation = 2
}

internal static class OverlayInputRoutingPolicy
{
    internal static OverlayWheelInputRoute ResolveWheelRoute(
        bool boardActive,
        bool photoModeActive,
        bool canRoutePresentationInput,
        bool presentationChannelEnabled)
    {
        if (boardActive)
        {
            return OverlayWheelInputRoute.ConsumeForBoard;
        }

        if (photoModeActive)
        {
            return OverlayWheelInputRoute.HandlePhoto;
        }

        if (!canRoutePresentationInput || !presentationChannelEnabled)
        {
            return OverlayWheelInputRoute.Ignore;
        }

        return OverlayWheelInputRoute.RoutePresentation;
    }

    internal static OverlayKeyInputRoute ResolveKeyRoute(
        bool photoLoading,
        bool photoKeyHandled,
        bool photoOrBoardActive,
        bool canRoutePresentationInput)
    {
        if (photoLoading)
        {
            return OverlayKeyInputRoute.Consume;
        }

        if (photoKeyHandled)
        {
            return OverlayKeyInputRoute.Consume;
        }

        if (photoOrBoardActive || !canRoutePresentationInput)
        {
            return OverlayKeyInputRoute.Ignore;
        }

        return OverlayKeyInputRoute.RoutePresentation;
    }
}
