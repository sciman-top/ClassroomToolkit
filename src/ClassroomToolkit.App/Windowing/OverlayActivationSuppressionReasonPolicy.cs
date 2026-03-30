namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivationSuppressionReasonPolicy
{
    internal static string ResolveTag(OverlayActivationSuppressionReason reason)
    {
        return reason switch
        {
            OverlayActivationSuppressionReason.SuppressionRequested => "suppression-requested",
            _ => "not-suppressed"
        };
    }
}
