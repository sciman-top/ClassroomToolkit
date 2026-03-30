using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractivePinLifetimePolicy
{
    internal static bool ShouldReleasePin(
        DateTime holdUntilUtc,
        DateTime nowUtc,
        bool interactionActive)
    {
        if (holdUntilUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        if (interactionActive)
        {
            return false;
        }

        return nowUtc > holdUntilUtc;
    }
}
