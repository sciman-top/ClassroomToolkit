namespace ClassroomToolkit.App.Windowing;

internal static class FloatingOwnerBindingReasonPolicy
{
    internal static string ResolveTag(FloatingOwnerBindingReason reason)
    {
        return reason switch
        {
            FloatingOwnerBindingReason.AttachWhenOverlayVisible => "attach-when-overlay-visible",
            FloatingOwnerBindingReason.DetachWhenOverlayHidden => "detach-when-overlay-hidden",
            FloatingOwnerBindingReason.AlreadyAligned => "already-aligned",
            _ => "none"
        };
    }
}
