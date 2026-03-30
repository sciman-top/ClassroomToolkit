namespace ClassroomToolkit.App.Windowing;

internal static class FloatingSingleOwnerExecutionPolicy
{
    public static FloatingOwnerBindingAction Resolve(bool childExists, FloatingOwnerBindingContext context)
    {
        if (!childExists)
        {
            return FloatingOwnerBindingAction.None;
        }

        return FloatingOwnerBindingPolicy.Resolve(context);
    }

    public static FloatingOwnerBindingAction Resolve(bool childExists, bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return Resolve(
            childExists,
            new FloatingOwnerBindingContext(
                overlayVisible,
                ownerAlreadyOverlay));
    }
}
