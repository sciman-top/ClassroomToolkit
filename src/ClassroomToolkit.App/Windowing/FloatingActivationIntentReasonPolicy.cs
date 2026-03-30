namespace ClassroomToolkit.App.Windowing;

internal static class FloatingActivationIntentReasonPolicy
{
    internal static string ResolveTag(FloatingActivationIntentReason reason)
    {
        return reason switch
        {
            FloatingActivationIntentReason.OverlayActivationRequested => "overlay-activation-requested",
            FloatingActivationIntentReason.ImageManagerActivationRequested => "image-manager-activation-requested",
            _ => "none"
        };
    }
}
