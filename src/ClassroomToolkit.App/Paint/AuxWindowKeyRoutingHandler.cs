using System;
using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

internal static class AuxWindowKeyRoutingHandler
{
    internal static bool TryHandle(
        Key key,
        bool overlayVisible,
        Func<Key, bool> tryHandlePhotoKey,
        bool canRoutePresentationInput,
        Action<Key> forwardPresentationKey)
    {
        if (!overlayVisible)
        {
            return false;
        }

        if (tryHandlePhotoKey(key))
        {
            return true;
        }

        if (!AuxWindowNavigationRoutingPolicy.ShouldForwardPresentation(canRoutePresentationInput, key))
        {
            return false;
        }

        forwardPresentationKey(key);
        return true;
    }
}
