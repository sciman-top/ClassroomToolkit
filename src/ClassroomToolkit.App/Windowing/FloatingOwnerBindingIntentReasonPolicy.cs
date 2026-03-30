namespace ClassroomToolkit.App.Windowing;

internal static class FloatingOwnerBindingIntentReasonPolicy
{
    internal static string ResolveTag(FloatingOwnerBindingIntentReason reason)
    {
        return reason switch
        {
            FloatingOwnerBindingIntentReason.ToolbarOwnerBindingRequested => "toolbar-owner-binding-requested",
            FloatingOwnerBindingIntentReason.RollCallOwnerBindingRequested => "rollcall-owner-binding-requested",
            FloatingOwnerBindingIntentReason.ImageManagerOwnerBindingRequested => "image-manager-owner-binding-requested",
            _ => "none"
        };
    }
}
