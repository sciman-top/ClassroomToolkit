namespace ClassroomToolkit.App.Windowing;

internal enum FloatingTopmostDriftReason
{
    None = 0,
    ToolbarDrift = 1,
    RollCallDrift = 2,
    LauncherDrift = 3,
    NoDrift = 4
}

internal readonly record struct FloatingTopmostDriftDecision(
    bool HasDrift,
    FloatingTopmostDriftReason Reason);

internal enum FloatingTopmostForceEnforceReason
{
    None = 0,
    DisabledByDesign = 1
}

internal readonly record struct FloatingTopmostForceEnforceDecision(
    bool ShouldForceEnforce,
    FloatingTopmostForceEnforceReason Reason);

internal static class FloatingTopmostDriftPolicy
{
    internal static FloatingTopmostDriftDecision ResolveDrift(ToolbarInteractionRetouchSnapshot snapshot)
    {
        if (snapshot.ToolbarVisible && !snapshot.ToolbarTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.ToolbarDrift);
        }

        if (snapshot.RollCallVisible && !snapshot.RollCallTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.RollCallDrift);
        }

        if (snapshot.LauncherVisible && !snapshot.LauncherTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.LauncherDrift);
        }

        return new FloatingTopmostDriftDecision(
            HasDrift: false,
            Reason: FloatingTopmostDriftReason.NoDrift);
    }

    internal static bool HasDrift(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return ResolveDrift(snapshot).HasDrift;
    }

    internal static FloatingTopmostForceEnforceDecision ResolveForceEnforce(ToolbarInteractionRetouchSnapshot snapshot)
    {
        _ = snapshot;
        return new FloatingTopmostForceEnforceDecision(
            ShouldForceEnforce: false,
            Reason: FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    internal static bool ShouldForceEnforce(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return ResolveForceEnforce(snapshot).ShouldForceEnforce;
    }
}
