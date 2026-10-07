using
ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SessionFloatingWidgetVisibilityDecision(
    bool AnyVisibilityChanged,
    bool AnyWidgetBecameVisible,
    SessionFloatingWidgetVisibilityReason Reason);

internal enum SessionFloatingWidgetVisibilityReason
{
    None = 0,
    RollCallBecameVisible = 1,
    LauncherBecameVisible = 2,
    ToolbarBecameVisible = 3,
    VisibilityChangedButNoWidgetBecameVisible = 4
}

internal static class SessionFloatingWidgetVisibilityPolicy
{
    internal static SessionFloatingWidgetVisibilityDecision Resolve(
        UiSessionState previous,
        UiSessionState current)
    {
        var rollCallChanged = previous.RollCallVisible != current.RollCallVisible;
        var launcherChanged = previous.LauncherVisible != current.LauncherVisible;
        var toolbarChanged = previous.ToolbarVisible != current.ToolbarVisible;
        var anyChanged = rollCallChanged || launcherChanged || toolbarChanged;

        var anyBecameVisible =
            (!previous.RollCallVisible && current.RollCallVisible)
            || (!previous.LauncherVisible && current.LauncherVisible)
            || (!previous.ToolbarVisible && current.ToolbarVisible);

        var reason = !anyChanged
            ? SessionFloatingWidgetVisibilityReason.None
            : !previous.RollCallVisible && current.RollCallVisible
                ? SessionFloatingWidgetVisibilityReason.RollCallBecameVisible
                : !previous.LauncherVisible && current.LauncherVisible
                    ? SessionFloatingWidgetVisibilityReason.LauncherBecameVisible
                    : !previous.ToolbarVisible && current.ToolbarVisible
                        ? SessionFloatingWidgetVisibilityReason.ToolbarBecameVisible
                        : SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible;

        return new SessionFloatingWidgetVisibilityDecision(
            AnyVisibilityChanged: anyChanged,
            AnyWidgetBecameVisible: anyBecameVisible,
            Reason: reason);
    }
}

internal enum SessionTransitionApplyGateReason
{
    None = 0,
    NoZOrderAction = 1,
    TouchSurfaceRequested = 2,
    ZOrderApplyRequested = 3,
    ForceEnforceRequested = 4
}

internal readonly record struct SessionTransitionApplyGateDecision(
    bool ShouldApply,
    SessionTransitionApplyGateReason Reason);

internal static class SessionTransitionApplyGatePolicy
{
    internal static SessionTransitionApplyGateDecision Resolve(SurfaceZOrderDecision decision)
    {
        if (decision.ShouldTouchSurface)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.TouchSurfaceRequested);
        }

        if (decision.RequestZOrderApply)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.ZOrderApplyRequested);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.ForceEnforceRequested);
        }

        return new SessionTransitionApplyGateDecision(
            ShouldApply: false,
            Reason: SessionTransitionApplyGateReason.NoZOrderAction);
    }

    internal static bool ShouldApply(SurfaceZOrderDecision decision)
    {
        return Resolve(decision).ShouldApply;
    }
}

internal readonly record struct SessionTransitionApplyDecision(
    bool RequestZOrderApply,
    bool ForceEnforceZOrder,
    SessionTransitionApplyReason Reason);

internal enum SessionTransitionApplyReason
{
    None = 0,
    EnsureFloatingRequested = 1,
    SceneChanged = 2,
    WidgetBecameVisible = 3,
    NoApplyRequested = 4,
    WidgetVisibilityChangedButNoWidgetBecameVisible = 5
}

internal static class SessionTransitionApplyPolicy
{
    internal static SessionTransitionApplyDecision Resolve(
        bool shouldEnsureFloating,
        bool overlayTopmostRequired,
        bool sceneChanged,
        SessionFloatingWidgetVisibilityDecision widgetVisibility)
    {
        var reason = shouldEnsureFloating
            ? SessionTransitionApplyReason.EnsureFloatingRequested
            : sceneChanged
                ? SessionTransitionApplyReason.SceneChanged
                : widgetVisibility.AnyWidgetBecameVisible
                    ? SessionTransitionApplyReason.WidgetBecameVisible
                    : widgetVisibility.AnyVisibilityChanged
                        ? SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible
                    : SessionTransitionApplyReason.NoApplyRequested;
        return new SessionTransitionApplyDecision(
            RequestZOrderApply: shouldEnsureFloating || sceneChanged || widgetVisibility.AnyWidgetBecameVisible,
            ForceEnforceZOrder: shouldEnsureFloating
                || widgetVisibility.AnyWidgetBecameVisible
                || (sceneChanged && overlayTopmostRequired),
            Reason: reason);
    }
}

internal static class SessionTransitionDecisionFactory
{
    internal static SurfaceZOrderDecision Create(
        SessionTransitionSurfaceDecision surfaceDecision,
        SessionTransitionApplyDecision applyDecision)
    {
        var decision = surfaceDecision.ShouldTouchSurface
            ? ForegroundSurfaceDecisionFactory.Touch(surfaceDecision.Surface)
            : ForegroundSurfaceDecisionFactory.NoTouch(applyDecision.RequestZOrderApply);

        return decision with
        {
            RequestZOrderApply = applyDecision.RequestZOrderApply,
            ForceEnforceZOrder = applyDecision.ForceEnforceZOrder
        };
    }
}

internal static class SessionTransitionDiagnosticsPolicy
{
    internal static string FormatAdmissionSkipMessage(
        long transitionId,
        SessionTransitionAdmissionReason reason)
    {
        return $"[UiSession][Admission] skip #{transitionId} reason={reason}";
    }

    internal static string FormatApplyGateSkipMessage(
        long transitionId,
        SessionTransitionApplyGateReason reason)
    {
        return $"[UiSession][ApplyGate] skip #{transitionId} reason={reason}";
    }

    internal static string FormatDuplicateResetMessage(SessionTransitionDuplicateResetReason reason)
    {
        return $"[UiSession][DuplicateReset] reason={reason}";
    }

    internal static string FormatWindowingReasonMessage(
        long transitionId,
        SessionTransitionWindowingReason reason)
    {
        return $"[UiSession][Windowing] #{transitionId} reason={reason}";
    }

    internal static string FormatWidgetVisibilityReasonMessage(
        long transitionId,
        SessionFloatingWidgetVisibilityReason reason)
    {
        return $"[UiSession][WidgetVisibility] #{transitionId} reason={reason}";
    }

    internal static string FormatApplyReasonMessage(
        long transitionId,
        SessionTransitionApplyReason reason)
    {
        return $"[UiSession][Apply] #{transitionId} reason={reason}";
    }

    internal static string FormatSurfaceReasonMessage(
        long transitionId,
        SessionTransitionSurfaceReason reason)
    {
        return $"[UiSession][Surface] #{transitionId} reason={reason}";
    }
}

internal enum SessionTransitionDuplicateReason
{
    None = 0,
    TransitionAdvanced = 1,
    DuplicateTransitionId = 2,
    RegressedTransitionId = 3
}

internal readonly record struct SessionTransitionDuplicateDecision(
    bool ShouldApply,
    SessionTransitionDuplicateReason Reason);

internal static class SessionTransitionDuplicatePolicy
{
    internal static SessionTransitionDuplicateDecision Resolve(long lastAppliedTransitionId, long currentTransitionId)
    {
        if (currentTransitionId > lastAppliedTransitionId)
        {
            return new SessionTransitionDuplicateDecision(
                ShouldApply: true,
                Reason: SessionTransitionDuplicateReason.TransitionAdvanced);
        }

        if (currentTransitionId == lastAppliedTransitionId)
        {
            return new SessionTransitionDuplicateDecision(
                ShouldApply: false,
                Reason: SessionTransitionDuplicateReason.DuplicateTransitionId);
        }

        return new SessionTransitionDuplicateDecision(
            ShouldApply: false,
            Reason: SessionTransitionDuplicateReason.RegressedTransitionId);
    }

    internal static bool ShouldApply(long lastAppliedTransitionId, long currentTransitionId)
    {
        return Resolve(lastAppliedTransitionId, currentTransitionId).ShouldApply;
    }
}

internal enum SessionTransitionDuplicateResetReason
{
    None = 0,
    OverlayNotRewired = 1,
    NoAppliedTransition = 2,
    ResetRequired = 3
}

internal readonly record struct SessionTransitionDuplicateResetDecision(
    bool ShouldReset,
    SessionTransitionDuplicateResetReason Reason);

internal static class SessionTransitionDuplicateResetPolicy
{
    internal static SessionTransitionDuplicateResetDecision Resolve(
        bool overlayWindowRewired,
        long lastAppliedTransitionId)
    {
        if (!overlayWindowRewired)
        {
            return new SessionTransitionDuplicateResetDecision(
                ShouldReset: false,
                Reason: SessionTransitionDuplicateResetReason.OverlayNotRewired);
        }

        if (lastAppliedTransitionId <= 0)
        {
            return new SessionTransitionDuplicateResetDecision(
                ShouldReset: false,
                Reason: SessionTransitionDuplicateResetReason.NoAppliedTransition);
        }

        return new SessionTransitionDuplicateResetDecision(
            ShouldReset: true,
            Reason: SessionTransitionDuplicateResetReason.ResetRequired);
    }

    internal static bool ShouldReset(
        bool overlayWindowRewired,
        long lastAppliedTransitionId)
    {
        return Resolve(overlayWindowRewired, lastAppliedTransitionId).ShouldReset;
    }
}

internal static class SessionTransitionDuplicateStateUpdater
{
    internal static void Reset(ref long lastAppliedTransitionId)
    {
        lastAppliedTransitionId = 0;
    }

    internal static void MarkApplied(
        ref long lastAppliedTransitionId,
        long currentTransitionId)
    {
        if (currentTransitionId > lastAppliedTransitionId)
        {
            lastAppliedTransitionId = currentTransitionId;
        }
    }
}

internal enum SessionTransitionAdmissionReason
{
    None = 0,
    NoStateChange = 1,
    DuplicateTransitionId = 2,
    RegressedTransitionId = 3
}

internal readonly record struct SessionTransitionAdmissionDecision(
    bool ShouldProcess,
    SessionTransitionAdmissionReason Reason);

internal static class SessionTransitionEventAdmissionPolicy
{
    internal static SessionTransitionAdmissionDecision Resolve(
        bool hasStateChange,
        long lastAppliedTransitionId,
        long currentTransitionId)
    {
        if (!hasStateChange)
        {
            return new SessionTransitionAdmissionDecision(
                ShouldProcess: false,
                Reason: SessionTransitionAdmissionReason.NoStateChange);
        }

        var duplicateDecision = SessionTransitionDuplicatePolicy.Resolve(
            lastAppliedTransitionId,
            currentTransitionId);
        if (!duplicateDecision.ShouldApply)
        {
            return new SessionTransitionAdmissionDecision(
                ShouldProcess: false,
                Reason: duplicateDecision.Reason == SessionTransitionDuplicateReason.RegressedTransitionId
                    ? SessionTransitionAdmissionReason.RegressedTransitionId
                    : SessionTransitionAdmissionReason.DuplicateTransitionId);
        }

        return new SessionTransitionAdmissionDecision(
            ShouldProcess: true,
            Reason: SessionTransitionAdmissionReason.None);
    }

    internal static bool ShouldProcess(
        bool hasStateChange,
        long lastAppliedTransitionId,
        long currentTransitionId)
    {
        return Resolve(
            hasStateChange,
            lastAppliedTransitionId,
            currentTransitionId).ShouldProcess;
    }
}

internal readonly record struct SessionTransitionSurfaceDecision(
    bool ShouldTouchSurface,
    ZOrderSurface Surface,
    SessionTransitionSurfaceReason Reason);

internal enum SessionTransitionSurfaceReason
{
    None = 0,
    SurfaceRetouchRequested = 1,
    NoSurfaceRetouchRequested = 2
}

internal static class SessionTransitionSurfacePolicy
{
    internal static SessionTransitionSurfaceDecision Resolve(UiSessionState previous, UiSessionState current)
    {
        var shouldTouch = SessionTransitionZOrderPolicy.ShouldRetouchSurface(previous, current);
        var surface = shouldTouch
            ? UiSceneSurfaceMapper.Map(current.Scene)
            : ZOrderSurface.None;
        var reason = shouldTouch
            ? SessionTransitionSurfaceReason.SurfaceRetouchRequested
            : SessionTransitionSurfaceReason.NoSurfaceRetouchRequested;
        return new SessionTransitionSurfaceDecision(shouldTouch, surface, reason);
    }
}

internal enum SessionTransitionWindowingReason
{
    None = 0,
    EnsureFloatingRequested = 1,
    SceneChanged = 2,
    WidgetBecameVisible = 3,
    NoApplyRequested = 4,
    WidgetVisibilityChangedButNoWidgetBecameVisible = 5
}

internal readonly record struct SessionTransitionWindowingDecision(
    SurfaceZOrderDecision ZOrderDecision,
    SessionTransitionWindowingReason Reason,
    SessionFloatingWidgetVisibilityReason WidgetVisibilityReason,
    SessionTransitionApplyReason ApplyReason,
    SessionTransitionSurfaceReason SurfaceReason);

internal static class SessionTransitionWindowingPolicy
{
    internal static SessionTransitionWindowingDecision ResolveDecision(UiSessionTransition transition)
    {
        var surfaceDecision = SessionTransitionSurfacePolicy.Resolve(
            transition.Previous,
            transition.Current);
        var floatingDecision = FloatingTopmostRetouchPolicy.Resolve(transition);
        var sceneChanged = transition.Previous.Scene != transition.Current.Scene;
        var widgetVisibility = SessionFloatingWidgetVisibilityPolicy.Resolve(
            transition.Previous,
            transition.Current);
        var applyDecision = SessionTransitionApplyPolicy.Resolve(
            floatingDecision.ShouldEnsureFloatingOnTransition,
            transition.Current.OverlayTopmostRequired,
            sceneChanged,
            widgetVisibility);

        var reason = applyDecision.Reason switch
        {
            SessionTransitionApplyReason.EnsureFloatingRequested => SessionTransitionWindowingReason.EnsureFloatingRequested,
            SessionTransitionApplyReason.SceneChanged => SessionTransitionWindowingReason.SceneChanged,
            SessionTransitionApplyReason.WidgetBecameVisible => SessionTransitionWindowingReason.WidgetBecameVisible,
            SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible => SessionTransitionWindowingReason.WidgetVisibilityChangedButNoWidgetBecameVisible,
            _ => SessionTransitionWindowingReason.NoApplyRequested
        };

        return new SessionTransitionWindowingDecision(
            ZOrderDecision: SessionTransitionDecisionFactory.Create(surfaceDecision, applyDecision),
            Reason: reason,
            WidgetVisibilityReason: widgetVisibility.Reason,
            ApplyReason: applyDecision.Reason,
            SurfaceReason: surfaceDecision.Reason);
    }

    internal static SurfaceZOrderDecision Resolve(UiSessionTransition transition)
    {
        return ResolveDecision(transition).ZOrderDecision;
    }
}

internal enum SessionTransitionZOrderRetouchReason
{
    None = 0,
    SceneChangedToSurface = 1,
    SceneUnchanged = 2,
    SceneChangedToNoneSurface = 3
}

internal readonly record struct SessionTransitionZOrderRetouchDecision(
    bool ShouldRetouchSurface,
    SessionTransitionZOrderRetouchReason Reason);

internal static class SessionTransitionZOrderPolicy
{
    internal static SessionTransitionZOrderRetouchDecision Resolve(UiSessionState previous, UiSessionState current)
    {
        if (previous.Scene == current.Scene)
        {
            return new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: false,
                Reason: SessionTransitionZOrderRetouchReason.SceneUnchanged);
        }

        return UiSceneSurfaceMapper.Map(current.Scene) != ZOrderSurface.None
            ? new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: true,
                Reason: SessionTransitionZOrderRetouchReason.SceneChangedToSurface)
            : new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: false,
                Reason: SessionTransitionZOrderRetouchReason.SceneChangedToNoneSurface);
    }

    internal static bool ShouldRetouchSurface(UiSessionState previous, UiSessionState current)
    {
        return Resolve(previous, current).ShouldRetouchSurface;
    }
}
