namespace ClassroomToolkit.App.Paint;

internal static class CrossPageRequestAdmissionReasonPolicy
{
    internal static string ResolveDiagnosticTag(CrossPageRequestAdmissionReason reason)
    {
        return reason switch
        {
            CrossPageRequestAdmissionReason.CrossPageInactive => "crosspage-inactive",
            CrossPageRequestAdmissionReason.PhotoLoading => "photo-loading",
            CrossPageRequestAdmissionReason.BackgroundNotReady => "background-not-ready",
            CrossPageRequestAdmissionReason.OverlayNotVisible => "overlay-not-visible",
            CrossPageRequestAdmissionReason.OverlayMinimized => "overlay-minimized",
            CrossPageRequestAdmissionReason.ViewportUnavailable => "viewport-unavailable",
            _ => "admitted"
        };
    }
}
