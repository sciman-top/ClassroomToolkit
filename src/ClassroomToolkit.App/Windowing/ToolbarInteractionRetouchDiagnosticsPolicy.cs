namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchDiagnosticsPolicy
{
    internal static string FormatDecisionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchDecisionReason reason)
    {
        return
            $"[ToolbarRetouch][Decision] skip trigger={trigger} reason={ToolbarInteractionRetouchDecisionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatActivationSuppressionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionActivationSuppressionReason reason)
    {
        return
            $"[ToolbarRetouch][Suppression] skip trigger={trigger} reason={ToolbarInteractionActivationSuppressionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatAdmissionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchAdmissionReason reason,
        bool forceEnforceZOrder)
    {
        return
            $"[ToolbarRetouch][Admission] skip trigger={trigger} reason={ToolbarInteractionRetouchAdmissionReasonPolicy.ResolveTag(reason)} force={forceEnforceZOrder}";
    }

    internal static string FormatThrottleSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchThrottleReason reason,
        int minimumIntervalMs)
    {
        return
            $"[ToolbarRetouch][Throttle] skip trigger={trigger} reason={ToolbarInteractionRetouchThrottleReasonPolicy.ResolveTag(reason)} minIntervalMs={minimumIntervalMs}";
    }

    internal static string FormatExecutionPlanMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchExecutionPlan plan)
    {
        return
            $"[ToolbarRetouch][Execute] trigger={trigger} directRepair={plan.ApplyDirectDriftRepair} requestZOrder={plan.RequestZOrderApply} force={plan.ForceEnforceZOrder}";
    }

    internal static string FormatDirectRepairAdmissionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionDirectRepairAdmissionReason reason)
    {
        return
            $"[ToolbarRetouch][DirectRepair] skip trigger={trigger} reason={ToolbarInteractionDirectRepairAdmissionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatDirectRepairDispatchMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchDispatchMode mode)
    {
        return
            $"[ToolbarRetouch][DirectRepair] dispatch trigger={trigger} mode={mode}";
    }

    internal static string FormatDirectRepairDispatchAdmissionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionDirectRepairDispatchAdmissionReason reason)
    {
        return
            $"[ToolbarRetouch][DirectRepair] dispatch-skip trigger={trigger} reason={ToolbarInteractionDirectRepairDispatchAdmissionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatDirectRepairDispatchFailureMessage(
        ToolbarInteractionRetouchTrigger trigger,
        string exceptionType,
        string message)
    {
        return
            $"[ToolbarRetouch][DirectRepair] dispatch-failed trigger={trigger} ex={exceptionType} msg={message}";
    }

    internal static string FormatRuntimeResetMessage(ToolbarInteractionRetouchRuntimeResetReason reason)
    {
        return
            $"[ToolbarRetouch][RuntimeReset] reason={ToolbarInteractionRetouchRuntimeResetReasonPolicy.ResolveTag(reason)}";
    }
}
