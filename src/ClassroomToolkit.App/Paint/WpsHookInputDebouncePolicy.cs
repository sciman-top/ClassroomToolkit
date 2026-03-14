using System;

namespace ClassroomToolkit.App.Paint;

internal static class WpsHookInputDebouncePolicy
{
    internal static bool IsRecent(
        DateTime lastHookInputUtc,
        DateTime nowUtc,
        int debounceMs)
    {
        if (lastHookInputUtc == PresentationRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        return (nowUtc - lastHookInputUtc).TotalMilliseconds < debounceMs;
    }
}
