namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivationDiagnosticsPolicy
{
    internal static string FormatRetouchSkipMessage(OverlayActivationRetouchReason reason)
    {
        return $"[OverlayActivation][Retouch] skip reason={OverlayActivationRetouchReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatSuppressionMessage(OverlayActivationSuppressionReason reason)
    {
        return $"[OverlayActivation][Suppression] apply reason={OverlayActivationSuppressionReasonPolicy.ResolveTag(reason)}";
    }
}
