namespace ClassroomToolkit.App.Paint;

internal static class CrossPageUpdateRequestStateUpdater
{
    internal static void ApplyAcceptedRequest(
        ref CrossPageUpdateRequestRuntimeState state,
        CrossPageUpdateRequestContext request,
        DateTime nowUtc)
    {
        state = new CrossPageUpdateRequestRuntimeState(
            LastRequest: request,
            LastRequestUtc: nowUtc);
    }

    internal static void ApplyAcceptedRequest(
        ref CrossPageUpdateRequestContext? lastRequest,
        ref DateTime lastRequestUtc,
        CrossPageUpdateRequestContext request,
        DateTime nowUtc)
    {
        lastRequest = request;
        lastRequestUtc = nowUtc;
    }
}
