namespace ClassroomToolkit.App.Paint;

internal static class WpsWheelRoutingPolicy
{
    internal static bool ShouldBypassDirectSend(
        bool hookActive,
        bool hookInterceptWheel,
        bool hookBlockOnly,
        bool isWpsForeground)
    {
        return hookActive
            && hookInterceptWheel
            && hookBlockOnly
            && isWpsForeground;
    }
}
