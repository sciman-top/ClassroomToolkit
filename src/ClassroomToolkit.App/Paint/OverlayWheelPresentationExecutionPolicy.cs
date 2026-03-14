namespace ClassroomToolkit.App.Paint;

internal static class OverlayWheelPresentationExecutionPolicy
{
    internal static OverlayWheelPresentationExecutionAction Resolve(
        bool hookActive,
        bool hookInterceptWheel,
        bool hookBlockOnly,
        bool isWpsForeground,
        bool hookRecentlyFired,
        int wheelDelta)
    {
        if (WpsWheelRoutingPolicy.ShouldBypassDirectSend(
                hookActive,
                hookInterceptWheel,
                hookBlockOnly,
                isWpsForeground))
        {
            return OverlayWheelPresentationExecutionAction.None;
        }

        if (hookRecentlyFired)
        {
            return OverlayWheelPresentationExecutionAction.None;
        }

        return wheelDelta < 0
            ? OverlayWheelPresentationExecutionAction.SendNext
            : OverlayWheelPresentationExecutionAction.SendPrevious;
    }
}
