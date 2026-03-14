namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractionDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.InteractionMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        if (currentRequest.Kind != CrossPageUpdateSourceKind.Interaction
            || previousRequest.Kind != CrossPageUpdateSourceKind.Interaction)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }
}
