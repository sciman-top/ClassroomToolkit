using System;
using System.Windows.Input;
using ClassroomToolkit.App.Windowing;

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
        ArgumentNullException.ThrowIfNull(tryHandlePhotoKey);
        ArgumentNullException.ThrowIfNull(forwardPresentationKey);

        if (!overlayVisible)
        {
            return false;
        }

        var photoHandled = SafeActionExecutionExecutor.TryExecute(
            () => tryHandlePhotoKey(key),
            fallback: false);

        if (photoHandled)
        {
            return true;
        }

        if (!AuxWindowNavigationRoutingPolicy.ShouldForwardPresentation(canRoutePresentationInput, key))
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                forwardPresentationKey(key);
                return true;
            },
            fallback: false);
    }
}
