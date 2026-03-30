namespace ClassroomToolkit.App.Windowing;

internal static class ForegroundExplicitRetouchThrottleReasonPolicy
{
    internal static string ResolveTag(ForegroundExplicitRetouchThrottleReason reason)
    {
        return reason switch
        {
            ForegroundExplicitRetouchThrottleReason.Throttled => "throttled",
            _ => "allow"
        };
    }
}
