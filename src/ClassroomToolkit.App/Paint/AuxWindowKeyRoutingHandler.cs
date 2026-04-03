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
        Func<Key, bool> tryForwardPresentationKey)
    {
        ArgumentNullException.ThrowIfNull(tryHandlePhotoKey);
        ArgumentNullException.ThrowIfNull(tryForwardPresentationKey);

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
            () => tryForwardPresentationKey(key),
            fallback: false);
    }
}
