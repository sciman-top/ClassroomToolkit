namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestDiagnosticsPolicy
{
    internal static string FormatQueuedMessage(
        ZOrderRequestAdmissionReason reason,
        bool forceEnforceZOrder)
    {
        return $"[ZOrderRequest] queued reason={ZOrderRequestAdmissionReasonPolicy.ResolveTag(reason)} force={forceEnforceZOrder}";
    }

    internal static string FormatSkipMessage(
        ZOrderRequestAdmissionReason reason,
        bool forceEnforceZOrder,
        bool applyQueued,
        bool zOrderApplying)
    {
        return
            $"[ZOrderRequest] skip reason={ZOrderRequestAdmissionReasonPolicy.ResolveTag(reason)} force={forceEnforceZOrder} queued={applyQueued} applying={zOrderApplying}";
    }

    internal static string FormatQueueDispatchFailedRollbackMessage(bool forceEnforceZOrder)
    {
        return $"[ZOrderRequest] rollback reason=queue-dispatch-failed force={forceEnforceZOrder}";
    }
}
