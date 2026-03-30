using System;

namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestStateUpdater
{
    internal static void Apply(
        ref ZOrderRequestRuntimeState state,
        ZOrderRequestAdmissionDecision admission)
    {
        state = new ZOrderRequestRuntimeState(
            admission.LastRequestUtc,
            admission.LastForceEnforceZOrder);
    }

    internal static void Apply(
        ref DateTime lastRequestUtc,
        ref bool lastForceEnforceZOrder,
        ZOrderRequestAdmissionDecision admission)
    {
        lastRequestUtc = admission.LastRequestUtc;
        lastForceEnforceZOrder = admission.LastForceEnforceZOrder;
    }
}
