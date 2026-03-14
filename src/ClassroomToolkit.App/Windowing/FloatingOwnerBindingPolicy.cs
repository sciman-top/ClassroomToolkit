namespace ClassroomToolkit.App.Windowing;

internal enum FloatingOwnerBindingAction
{
    None,
    AttachOverlay,
    DetachOverlay
}

internal enum FloatingOwnerBindingReason
{
    None = 0,
    AttachWhenOverlayVisible = 1,
    DetachWhenOverlayHidden = 2,
    AlreadyAligned = 3
}

internal readonly record struct FloatingOwnerBindingDecision(
    FloatingOwnerBindingAction Action,
    FloatingOwnerBindingReason Reason);

internal static class FloatingOwnerBindingPolicy
{
    internal static FloatingOwnerBindingAction Resolve(FloatingOwnerBindingContext context)
    {
        return ResolveDecision(
            context.OverlayVisible,
            context.OwnerAlreadyOverlay).Action;
    }

    internal static FloatingOwnerBindingDecision ResolveDecision(FloatingOwnerBindingContext context)
    {
        return ResolveDecision(
            context.OverlayVisible,
            context.OwnerAlreadyOverlay);
    }

    internal static FloatingOwnerBindingAction Resolve(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return ResolveDecision(overlayVisible, ownerAlreadyOverlay).Action;
    }

    internal static FloatingOwnerBindingDecision ResolveDecision(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        if (overlayVisible && !ownerAlreadyOverlay)
        {
            return new FloatingOwnerBindingDecision(
                Action: FloatingOwnerBindingAction.AttachOverlay,
                Reason: FloatingOwnerBindingReason.AttachWhenOverlayVisible);
        }

        if (!overlayVisible && ownerAlreadyOverlay)
        {
            return new FloatingOwnerBindingDecision(
                Action: FloatingOwnerBindingAction.DetachOverlay,
                Reason: FloatingOwnerBindingReason.DetachWhenOverlayHidden);
        }

        return new FloatingOwnerBindingDecision(
            Action: FloatingOwnerBindingAction.None,
            Reason: FloatingOwnerBindingReason.AlreadyAligned);
    }

    internal static bool ShouldAttachOverlayOwner(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return Resolve(overlayVisible, ownerAlreadyOverlay) == FloatingOwnerBindingAction.AttachOverlay;
    }

    internal static bool ShouldDetachOverlayOwner(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return Resolve(overlayVisible, ownerAlreadyOverlay) == FloatingOwnerBindingAction.DetachOverlay;
    }
}
