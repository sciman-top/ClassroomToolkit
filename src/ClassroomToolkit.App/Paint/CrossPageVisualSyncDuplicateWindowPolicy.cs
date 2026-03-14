namespace ClassroomToolkit.App.Paint;

internal static class CrossPageVisualSyncDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.VisualSyncMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        var bothVisualSync = currentRequest.Kind == CrossPageUpdateSourceKind.VisualSync
            && previousRequest.Kind == CrossPageUpdateSourceKind.VisualSync;
        var bothReplaySource = CrossPageUpdateReplayPolicy.IsReplayBaseSource(currentRequest.BaseSource)
            && CrossPageUpdateReplayPolicy.IsReplayBaseSource(previousRequest.BaseSource);
        if (bothReplaySource)
        {
            // Replay is the recovery path for skipped/pending updates.
            // Deduplicating replay requests can leave previous page visuals stale until next interaction.
            return false;
        }

        if (!bothVisualSync)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageVisualSyncDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }

    internal static bool ShouldSkip(
        string source,
        string? lastSource,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.VisualSyncMs)
    {
        var currentRequest = CrossPageUpdateRequestContextFactory.Create(source);
        var previousRequest = string.IsNullOrWhiteSpace(lastSource)
            ? (CrossPageUpdateRequestContext?)null
            : CrossPageUpdateRequestContextFactory.Create(lastSource);
        return ShouldSkip(
            currentRequest,
            previousRequest,
            nowUtc,
            lastRequestedUtc,
            duplicateWindowMs);
    }
}
