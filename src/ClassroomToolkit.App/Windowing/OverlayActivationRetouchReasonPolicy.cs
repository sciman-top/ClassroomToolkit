namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivationRetouchReasonPolicy
{
    internal static string ResolveTag(OverlayActivationRetouchReason reason)
    {
        return reason switch
        {
            OverlayActivationRetouchReason.NoApplyRequest => "no-apply-request",
            OverlayActivationRetouchReason.Throttled => "throttled",
            OverlayActivationRetouchReason.Forced => "forced",
            _ => "apply"
        };
    }
}
