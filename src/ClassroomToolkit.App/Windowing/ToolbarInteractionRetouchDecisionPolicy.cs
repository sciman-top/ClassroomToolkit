namespace ClassroomToolkit.App.Windowing;

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
        var interactiveScene = ToolbarInteractionTopmostRetouchPolicy.ShouldRetouch(
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
