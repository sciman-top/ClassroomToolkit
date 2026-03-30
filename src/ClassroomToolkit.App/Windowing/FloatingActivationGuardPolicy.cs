namespace ClassroomToolkit.App.Windowing;

internal enum FloatingActivationGuardReason
{
    None = 0,
    ToolbarActive = 1,
    RollCallActive = 2,
    ImageManagerActive = 3,
    LauncherActive = 4
}

internal readonly record struct FloatingActivationGuardDecision(
    bool IsBlocked,
    FloatingActivationGuardReason Reason);

internal static class FloatingActivationGuardPolicy
{
    internal static FloatingActivationGuardDecision Resolve(FloatingUtilityActivitySnapshot snapshot)
    {
        if (snapshot.ToolbarActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.ToolbarActive);
        }

        if (snapshot.RollCallActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.RollCallActive);
        }

        if (snapshot.ImageManagerActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.ImageManagerActive);
        }

        if (snapshot.LauncherActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.LauncherActive);
        }

        return new FloatingActivationGuardDecision(
            IsBlocked: false,
            Reason: FloatingActivationGuardReason.None);
    }

    internal static bool IsBlockedByUtilityWindows(FloatingUtilityActivitySnapshot snapshot)
    {
        return Resolve(snapshot).IsBlocked;
    }

    internal static bool IsBlockedByUtilityWindows(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        return IsBlockedByUtilityWindows(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
    }
}
