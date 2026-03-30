using System;

namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionDirectRepairExecutionOutcome
{
    ImmediateApplied = 0,
    BackgroundScheduled = 1,
    BackgroundDispatchRejected = 2,
    BackgroundMarkQueuedFailed = 3,
    BackgroundScheduleFailed = 4
}

internal static class ToolbarInteractionDirectRepairExecutionCoordinator
{
    internal static ToolbarInteractionDirectRepairExecutionOutcome Apply(
        ToolbarInteractionRetouchDispatchMode dispatchMode,
        Func<bool> isBackgroundQueued,
        Func<bool> tryMarkBackgroundQueued,
        Action clearBackgroundQueued,
        Action requestRerun,
        Func<bool> tryConsumeRerun,
        Action clearRerun,
        Action applyDirectRepair,
        Func<Action, bool> tryScheduleBackground)
    {
        ArgumentNullException.ThrowIfNull(isBackgroundQueued);
        ArgumentNullException.ThrowIfNull(tryMarkBackgroundQueued);
        ArgumentNullException.ThrowIfNull(clearBackgroundQueued);
        ArgumentNullException.ThrowIfNull(requestRerun);
        ArgumentNullException.ThrowIfNull(tryConsumeRerun);
        ArgumentNullException.ThrowIfNull(clearRerun);
        ArgumentNullException.ThrowIfNull(applyDirectRepair);
        ArgumentNullException.ThrowIfNull(tryScheduleBackground);

        if (dispatchMode != ToolbarInteractionRetouchDispatchMode.Background)
        {
            applyDirectRepair();
            return ToolbarInteractionDirectRepairExecutionOutcome.ImmediateApplied;
        }

        var dispatchAdmission = ToolbarInteractionDirectRepairDispatchAdmissionPolicy.Resolve(
            alreadyQueued: isBackgroundQueued());
        if (!dispatchAdmission.ShouldDispatch)
        {
            ApplyFailurePlan(
                requestRerun,
                clearBackgroundQueued,
                clearRerun,
                ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveAdmissionRejected());
            return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected;
        }

        if (!tryMarkBackgroundQueued())
        {
            ApplyFailurePlan(
                requestRerun,
                clearBackgroundQueued,
                clearRerun,
                ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveMarkQueuedFailed());
            return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundMarkQueuedFailed;
        }

        var scheduled = tryScheduleBackground(
            () =>
            {
                try
                {
                    applyDirectRepair();
                    if (tryConsumeRerun())
                    {
                        applyDirectRepair();
                    }
                }
                finally
                {
                    clearBackgroundQueued();
                }
            });
        if (!scheduled)
        {
            ApplyFailurePlan(
                requestRerun,
                clearBackgroundQueued,
                clearRerun,
                ToolbarInteractionDirectRepairDispatchFailurePlanPolicy.ResolveScheduleFailed());
            return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed;
        }

        return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduled;
    }

    private static void ApplyFailurePlan(
        Action requestRerun,
        Action clearBackgroundQueued,
        Action clearRerun,
        ToolbarInteractionDirectRepairDispatchFailurePlan failurePlan)
    {
        if (failurePlan.ShouldRequestRerun)
        {
            requestRerun();
        }

        if (failurePlan.ShouldClearQueuedState)
        {
            clearBackgroundQueued();
        }

        if (failurePlan.ShouldClearRerunState)
        {
            clearRerun();
        }
    }
}
