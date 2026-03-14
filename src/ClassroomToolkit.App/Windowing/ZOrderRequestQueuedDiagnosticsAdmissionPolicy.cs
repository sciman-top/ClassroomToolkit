namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestQueuedDiagnosticsAdmissionPolicy
{
    internal static bool ShouldLog(ZOrderRequestAdmissionReason reason)
    {
        return reason == ZOrderRequestAdmissionReason.QueuedForceEscalationWithinWindow;
    }
}
