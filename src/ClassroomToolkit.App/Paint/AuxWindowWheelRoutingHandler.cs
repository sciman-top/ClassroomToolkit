using System;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

internal static class AuxWindowWheelRoutingHandler
{
    internal static bool TryHandle(
        int delta,
        bool overlayVisible,
        bool canRoutePresentationInput,
        Func<int, bool> tryForwardPresentationWheel)
    {
        ArgumentNullException.ThrowIfNull(tryForwardPresentationWheel);

        if (!overlayVisible || !canRoutePresentationInput || delta == 0)
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () => tryForwardPresentationWheel(delta),
            fallback: false);
    }
}
