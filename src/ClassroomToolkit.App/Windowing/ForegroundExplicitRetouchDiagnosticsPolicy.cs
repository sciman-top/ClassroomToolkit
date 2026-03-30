namespace ClassroomToolkit.App.Windowing;

internal static class ForegroundExplicitRetouchDiagnosticsPolicy
{
    internal static string FormatThrottleSkipMessage(
        ZOrderSurface surface,
        ForegroundExplicitRetouchThrottleReason reason)
    {
        return
            $"[ExplicitForeground][Throttle] skip surface={surface} reason={ForegroundExplicitRetouchThrottleReasonPolicy.ResolveTag(reason)}";
    }
}
