using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class AuxWindowNavigationRoutingPolicy
{
    internal static bool ShouldForwardPresentation(bool canRoutePresentationInput, Key key)
    {
        if (!canRoutePresentationInput)
        {
            return false;
        }

        return PresentationKeyCommandPolicy.TryMap(key, out _);
    }
}
