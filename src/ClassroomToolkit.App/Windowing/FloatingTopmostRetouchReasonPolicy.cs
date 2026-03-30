namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostRetouchReasonPolicy
{
    internal static string ResolveTag(FloatingTopmostRetouchReason reason)
    {
        return reason switch
        {
            FloatingTopmostRetouchReason.OverlayTopmostNotRising => "overlay-topmost-not-rising",
            FloatingTopmostRetouchReason.OverlayTopmostBecameRequired => "overlay-topmost-became-required",
            _ => "none"
        };
    }
}
