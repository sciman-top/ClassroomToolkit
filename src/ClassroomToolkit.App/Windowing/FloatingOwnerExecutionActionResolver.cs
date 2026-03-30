namespace ClassroomToolkit.App.Windowing;

internal static class FloatingOwnerExecutionActionResolver
{
    internal static FloatingOwnerBindingAction Resolve(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return Resolve(new FloatingOwnerBindingContext(overlayVisible, ownerAlreadyOverlay));
    }

    internal static FloatingOwnerBindingAction Resolve(FloatingOwnerBindingContext context)
    {
        return FloatingOwnerBindingPolicy.Resolve(context);
    }
}
