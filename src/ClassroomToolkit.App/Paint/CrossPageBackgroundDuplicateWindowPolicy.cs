namespace ClassroomToolkit.App.Paint;

internal static class CrossPageBackgroundDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.BackgroundRefreshMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        if (currentRequest.Kind != CrossPageUpdateSourceKind.BackgroundRefresh
            || previousRequest.Kind != CrossPageUpdateSourceKind.BackgroundRefresh)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageBackgroundDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }
}
