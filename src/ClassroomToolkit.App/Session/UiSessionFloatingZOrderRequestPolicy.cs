using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Session;

internal static class UiSessionFloatingZOrderRequestPolicy
{
    public static bool TryResolveForOverlayTopmost(
        bool topmostRequired,
        out FloatingZOrderRequest request)
    {
        if (!topmostRequired)
        {
            request = default;
            return false;
        }

        request = new FloatingZOrderRequest(ForceEnforceZOrder: true);
        return true;
    }

    public static bool TryResolveForWidgetVisibility(
        UiSessionWidgetVisibility visibility,
        out FloatingZOrderRequest request)
    {
        if (!UiSessionWidgetVisibilityEffectPolicy.ShouldRequestFloatingZOrder(visibility))
        {
            request = default;
            return false;
        }

        request = new FloatingZOrderRequest(ForceEnforceZOrder: true);
        return true;
    }
}
