using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal enum FloatingTopmostRetouchReason
{
    None = 0,
    OverlayTopmostNotRising = 1,
    OverlayTopmostBecameRequired = 2
}

internal readonly record struct FloatingTopmostRetouchDecision(
    bool ShouldEnsureFloatingOnTransition,
    FloatingTopmostRetouchReason Reason);

internal static class FloatingTopmostRetouchPolicy
{
    internal static FloatingTopmostRetouchDecision Resolve(UiSessionTransition transition)
    {
        var shouldEnsure = transition.Previous.OverlayTopmostRequired != transition.Current.OverlayTopmostRequired
            && transition.Current.OverlayTopmostRequired;
        return shouldEnsure
            ? new FloatingTopmostRetouchDecision(
                ShouldEnsureFloatingOnTransition: true,
                Reason: FloatingTopmostRetouchReason.OverlayTopmostBecameRequired)
            : new FloatingTopmostRetouchDecision(
                ShouldEnsureFloatingOnTransition: false,
                Reason: FloatingTopmostRetouchReason.OverlayTopmostNotRising);
    }

    internal static bool ShouldEnsureFloatingOnTransition(UiSessionTransition transition)
    {
        return Resolve(transition).ShouldEnsureFloatingOnTransition;
    }
}
