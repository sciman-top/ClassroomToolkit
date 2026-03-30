namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDiagnosticsPolicy
{
    internal static string FormatAdmissionSkipMessage(
        long transitionId,
        SessionTransitionAdmissionReason reason)
    {
        return $"[UiSession][Admission] skip #{transitionId} reason={SessionTransitionAdmissionReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatApplyGateSkipMessage(
        long transitionId,
        SessionTransitionApplyGateReason reason)
    {
        return $"[UiSession][ApplyGate] skip #{transitionId} reason={SessionTransitionApplyGateReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatDuplicateResetMessage(SessionTransitionDuplicateResetReason reason)
    {
        return $"[UiSession][DuplicateReset] reason={SessionTransitionDuplicateResetReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatWindowingReasonMessage(
        long transitionId,
        SessionTransitionWindowingReason reason)
    {
        return $"[UiSession][Windowing] #{transitionId} reason={SessionTransitionWindowingReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatWidgetVisibilityReasonMessage(
        long transitionId,
        SessionFloatingWidgetVisibilityReason reason)
    {
        return $"[UiSession][WidgetVisibility] #{transitionId} reason={SessionFloatingWidgetVisibilityReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatApplyReasonMessage(
        long transitionId,
        SessionTransitionApplyReason reason)
    {
        return $"[UiSession][Apply] #{transitionId} reason={SessionTransitionApplyReasonPolicy.ResolveTag(reason)}";
    }

    internal static string FormatSurfaceReasonMessage(
        long transitionId,
        SessionTransitionSurfaceReason reason)
    {
        return $"[UiSession][Surface] #{transitionId} reason={SessionTransitionSurfaceReasonPolicy.ResolveTag(reason)}";
    }
}
