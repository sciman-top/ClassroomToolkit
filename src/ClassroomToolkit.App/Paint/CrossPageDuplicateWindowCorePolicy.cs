using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDuplicateWindowCorePolicy
{
    internal static bool TryGetLastRequest(
        CrossPageUpdateRequestContext? lastRequest,
        DateTime lastRequestedUtc,
        out CrossPageUpdateRequestContext value)
    {
        if (lastRequestedUtc != CrossPageRuntimeDefaults.UnsetTimestampUtc
            && lastRequest.HasValue)
        {
            value = lastRequest.Value;
            return true;
        }

        value = default;
        return false;
    }

    internal static bool HasSameBaseSource(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext lastRequest)
    {
        return string.Equals(currentRequest.BaseSource, lastRequest.BaseSource, StringComparison.Ordinal);
    }

    internal static bool IsWithinWindow(
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int intervalMs)
    {
        return (nowUtc - lastRequestedUtc).TotalMilliseconds < intervalMs;
    }
}
