using
System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionRetouchSnapshot(
    bool OverlayVisible,
    bool PhotoModeActive,
    bool WhiteboardActive,
    bool ToolbarVisible,
    bool ToolbarTopmost,
    bool RollCallVisible,
    bool RollCallTopmost,
    bool LauncherVisible,
    bool LauncherTopmost);

internal static class ToolbarInteractionRetouchIntervalDefaults
{
    internal const int DefaultMs = 120;
    internal const int InteractiveMs = 220;
}

internal static class ToolbarInteractionRetouchIntervalPolicy
{
    internal static int ResolveMs(
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchTrigger trigger,
        int defaultMs = ToolbarInteractionRetouchIntervalDefaults.DefaultMs,
        int interactiveMs = ToolbarInteractionRetouchIntervalDefaults.InteractiveMs)
    {
        if (trigger == ToolbarInteractionRetouchTrigger.PreviewMouseDown)
        {
            return defaultMs;
        }

        return InteractiveSceneIntervalPolicy.ResolveMs(
            snapshot.OverlayVisible,
            snapshot.PhotoModeActive,
            snapshot.WhiteboardActive,
            defaultMs,
            interactiveMs);
    }
}

internal enum ToolbarInteractionRetouchRuntimeResetReason
{
    None = 0,
    OverlayClosed = 1,
    ToolbarClosed = 2,
    PaintHidden = 3,
    RequestExit = 4
}

internal readonly record struct ToolbarInteractionRetouchRuntimeState(
    DateTime LastRetouchUtc,
    DateTime LastPreviewMouseDownUtc)
{
    internal static ToolbarInteractionRetouchRuntimeState Default => new(
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
        LastPreviewMouseDownUtc: WindowDedupDefaults.UnsetTimestampUtc);
}

internal static class ToolbarInteractionRetouchStateUpdater
{
    internal static void MarkPreviewMouseDown(
        ref ToolbarInteractionRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            LastPreviewMouseDownUtc = nowUtc
        };
    }

    internal static void MarkRetouched(
        ref ToolbarInteractionRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = state with
        {
            LastRetouchUtc = nowUtc
        };
    }

    internal static void Reset(ref ToolbarInteractionRetouchRuntimeState state)
    {
        state = ToolbarInteractionRetouchRuntimeState.Default;
    }
}

internal enum ToolbarInteractionRetouchDecisionReason
{
    None = 0,
    PreviewMouseDown = 1,
    SceneNotInteractive = 2,
    NoTopmostDrift = 3
}

internal readonly record struct ToolbarInteractionRetouchDecision(
    bool ShouldRetouch,
    bool ForceEnforceZOrder,
    ToolbarInteractionRetouchDecisionReason Reason);

internal enum ToolbarInteractionRetouchTrigger
{
    Activated = 0,
    PreviewMouseDown = 1
}

internal static class ToolbarInteractionRetouchDecisionPolicy
{
    internal static ToolbarInteractionRetouchDecision Resolve(
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchTrigger trigger)
    {
        var interactiveScene = InteractiveSceneIntervalPolicy.IsInteractiveScene(
            snapshot.OverlayVisible,
            snapshot.PhotoModeActive,
            snapshot.WhiteboardActive);
        if (!interactiveScene)
        {
            return new ToolbarInteractionRetouchDecision(
                ShouldRetouch: false,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.SceneNotInteractive);
        }

        if (trigger == ToolbarInteractionRetouchTrigger.PreviewMouseDown)
        {
            if (!snapshot.LauncherVisible)
            {
                return new ToolbarInteractionRetouchDecision(
                    ShouldRetouch: false,
                    ForceEnforceZOrder: false,
                    Reason: ToolbarInteractionRetouchDecisionReason.PreviewMouseDown);
            }

            return new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: true,
                Reason: ToolbarInteractionRetouchDecisionReason.None);
        }

        var driftDecision = FloatingTopmostDriftPolicy.ResolveDrift(snapshot);
        if (!driftDecision.HasDrift)
        {
            if (snapshot.LauncherVisible)
            {
                return new ToolbarInteractionRetouchDecision(
                    ShouldRetouch: true,
                    ForceEnforceZOrder: true,
                    Reason: ToolbarInteractionRetouchDecisionReason.None);
            }

            return new ToolbarInteractionRetouchDecision(
                ShouldRetouch: false,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.NoTopmostDrift);
        }

        var forceEnforceDecision = FloatingTopmostDriftPolicy.ResolveForceEnforce(snapshot);

        return new ToolbarInteractionRetouchDecision(
            ShouldRetouch: true,
            ForceEnforceZOrder: forceEnforceDecision.ShouldForceEnforce || ForegroundZOrderRetouchPolicy.ShouldForceOnToolbarInteraction(
                snapshot.OverlayVisible,
                snapshot.PhotoModeActive,
                snapshot.WhiteboardActive),
            Reason: ToolbarInteractionRetouchDecisionReason.None);
    }
}

internal enum ToolbarInteractionRetouchDispatchMode
{
    Immediate = 0,
    Background = 1
}

internal static class ToolbarInteractionRetouchDispatchPolicy
{
    internal static ToolbarInteractionRetouchDispatchMode Resolve(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchExecutionPlan executionPlan)
    {
        if (!executionPlan.ApplyDirectDriftRepair)
        {
            return ToolbarInteractionRetouchDispatchMode.Immediate;
        }

        var interactiveScene = InteractiveSceneIntervalPolicy.IsInteractiveScene(
            snapshot.OverlayVisible,
            snapshot.PhotoModeActive,
            snapshot.WhiteboardActive);
        var launcherDrift = snapshot.LauncherVisible && !snapshot.LauncherTopmost;
        if (trigger == ToolbarInteractionRetouchTrigger.Activated && interactiveScene && launcherDrift)
        {
            return ToolbarInteractionRetouchDispatchMode.Immediate;
        }

        if (trigger == ToolbarInteractionRetouchTrigger.Activated && interactiveScene)
        {
            return ToolbarInteractionRetouchDispatchMode.Background;
        }

        return ToolbarInteractionRetouchDispatchMode.Immediate;
    }
}

internal readonly record struct ToolbarInteractionRetouchExecutionPlan(
    bool ApplyDirectDriftRepair,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class ToolbarInteractionRetouchExecutionPlanPolicy
{
    internal static ToolbarInteractionRetouchExecutionPlan Resolve(
        ToolbarInteractionRetouchDecision decision)
    {
        if (!decision.ShouldRetouch)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: decision.ForceEnforceZOrder);
        }

        return new ToolbarInteractionRetouchExecutionPlan(
            ApplyDirectDriftRepair: true,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);
    }
}

internal static class ToolbarInteractionRetouchDiagnosticsPolicy
{
    internal static string FormatDecisionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchDecisionReason reason)
    {
        return
            $"[ToolbarRetouch][Decision] skip trigger={trigger} reason={reason}";
    }

    internal static string FormatActivationSuppressionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionActivationSuppressionReason reason)
    {
        return
            $"[ToolbarRetouch][Suppression] skip trigger={trigger} reason={reason}";
    }

    internal static string FormatAdmissionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ZOrderApplyReentryReason reason,
        bool forceEnforceZOrder)
    {
        return
            $"[ToolbarRetouch][Admission] skip trigger={trigger} reason={reason} force={forceEnforceZOrder}";
    }

    internal static string FormatThrottleSkipMessage(
        ToolbarInteractionRetouchTrigger trigger,
        RetouchThrottleReason reason,
        int minimumIntervalMs)
    {
        return
            $"[ToolbarRetouch][Throttle] skip trigger={trigger} reason={reason} minIntervalMs={minimumIntervalMs}";
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
            $"[ToolbarRetouch][DirectRepair] skip trigger={trigger} reason={reason}";
    }

    internal static string FormatDirectRepairDispatchMessage(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchDispatchMode mode)
    {
        return
            $"[ToolbarRetouch][DirectRepair] dispatch trigger={trigger} mode={mode}";
    }

    internal static string FormatDirectRepairDispatchAdmissionSkipMessage(
        ToolbarInteractionRetouchTrigger trigger)
    {
        return
            $"[ToolbarRetouch][DirectRepair] dispatch-skip trigger={trigger} reason=AlreadyQueued";
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
            $"[ToolbarRetouch][RuntimeReset] reason={reason}";
    }
}

internal enum ToolbarInteractionDirectRepairAdmissionReason
{
    None = 0,
    ZOrderApplying = 1,
    ZOrderQueued = 2
}

internal readonly record struct ToolbarInteractionDirectRepairAdmissionDecision(
    bool ShouldApply,
    ToolbarInteractionDirectRepairAdmissionReason Reason);

internal static class ToolbarInteractionDirectRepairAdmissionPolicy
{
    internal static ToolbarInteractionDirectRepairAdmissionDecision Resolve(
        bool zOrderApplying,
        bool zOrderQueued)
    {
        if (zOrderApplying)
        {
            return new ToolbarInteractionDirectRepairAdmissionDecision(
                ShouldApply: false,
                Reason: ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying);
        }

        if (zOrderQueued)
        {
            return new ToolbarInteractionDirectRepairAdmissionDecision(
                ShouldApply: false,
                Reason: ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued);
        }

        return new ToolbarInteractionDirectRepairAdmissionDecision(
            ShouldApply: true,
            Reason: ToolbarInteractionDirectRepairAdmissionReason.None);
    }
}

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

        if (isBackgroundQueued())
        {
            requestRerun();
            return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundDispatchRejected;
        }

        if (!tryMarkBackgroundQueued())
        {
            requestRerun();
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
            clearBackgroundQueued();
            clearRerun();
            return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduleFailed;
        }

        return ToolbarInteractionDirectRepairExecutionOutcome.BackgroundScheduled;
    }
}

internal enum ToolbarInteractionActivationSuppressionReason
{
    None = 0,
    NonActivatedTrigger = 1,
    PreviewTimestampUnset = 2,
    OutsideSuppressionWindow = 3,
    LauncherOnlyDrift = 4,
    PreviewAlreadyRetouched = 5
}

internal readonly record struct ToolbarInteractionActivationSuppressionDecision(
    bool ShouldSuppress,
    ToolbarInteractionActivationSuppressionReason Reason);

internal static class ToolbarInteractionActivationSuppressionPolicy
{
    internal static ToolbarInteractionActivationSuppressionDecision Resolve(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        DateTime lastPreviewMouseDownUtc,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int launcherOnlySuppressionMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewSuppressionMs)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger);
        }

        if (lastPreviewMouseDownUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset);
        }

        var elapsedMs = (nowUtc - lastPreviewMouseDownUtc).TotalMilliseconds;
        if (elapsedMs < 0 || elapsedMs > launcherOnlySuppressionMs)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow);
        }

        var retouchElapsedMs = (nowUtc - lastRetouchUtc).TotalMilliseconds;
        var previewAlreadyRetouched = lastRetouchUtc != WindowDedupDefaults.UnsetTimestampUtc
                                      && lastRetouchUtc >= lastPreviewMouseDownUtc
                                      && retouchElapsedMs >= 0
                                      && retouchElapsedMs <= launcherOnlySuppressionMs;
        if (previewAlreadyRetouched)
        {
            return new ToolbarInteractionActivationSuppressionDecision(
                ShouldSuppress: true,
                Reason: ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched);
        }

        return new ToolbarInteractionActivationSuppressionDecision(
            ShouldSuppress: false,
            Reason: ToolbarInteractionActivationSuppressionReason.None);
    }

    internal static bool ShouldSuppress(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        DateTime lastPreviewMouseDownUtc,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int launcherOnlySuppressionMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewSuppressionMs)
    {
        return Resolve(
            trigger,
            snapshot,
            lastPreviewMouseDownUtc,
            lastRetouchUtc,
            nowUtc,
            launcherOnlySuppressionMs).ShouldSuppress;
    }
}

internal static class ToolbarInteractionActivationSuppressionDefaults
{
    internal const int LauncherOnlyAfterPreviewSuppressionMs = 90;
    internal const int LauncherOnlyAfterPreviewInteractiveSuppressionMs = 130;
}
