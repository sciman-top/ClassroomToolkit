namespace ClassroomToolkit.App.Windowing;

internal enum FloatingActivationIntentReason
{
    None = 0,
    OverlayActivationRequested = 1,
    ImageManagerActivationRequested = 2
}

internal readonly record struct FloatingActivationIntentDecision(
    bool HasIntent,
    FloatingActivationIntentReason Reason);

internal enum FloatingOwnerBindingIntentReason
{
    None = 0,
    ToolbarOwnerBindingRequested = 1,
    RollCallOwnerBindingRequested = 2,
    ImageManagerOwnerBindingRequested = 3
}

internal readonly record struct FloatingOwnerBindingIntentDecision(
    bool HasIntent,
    FloatingOwnerBindingIntentReason Reason);

internal static class FloatingWindowExecutionIntentPolicy
{
    internal static FloatingActivationIntentDecision ResolveActivationIntent(FloatingWindowActivationPlan activationPlan)
    {
        if (activationPlan.ActivateOverlay)
        {
            return new FloatingActivationIntentDecision(
                HasIntent: true,
                Reason: FloatingActivationIntentReason.OverlayActivationRequested);
        }

        if (activationPlan.ActivateImageManager)
        {
            return new FloatingActivationIntentDecision(
                HasIntent: true,
                Reason: FloatingActivationIntentReason.ImageManagerActivationRequested);
        }

        return new FloatingActivationIntentDecision(
            HasIntent: false,
            Reason: FloatingActivationIntentReason.None);
    }

    internal static bool HasActivationIntent(FloatingWindowActivationPlan activationPlan)
    {
        return ResolveActivationIntent(activationPlan).HasIntent;
    }

    internal static FloatingOwnerBindingIntentDecision ResolveOwnerBindingIntent(FloatingOwnerExecutionPlan ownerPlan)
    {
        if (ownerPlan.ToolbarAction != FloatingOwnerBindingAction.None)
        {
            return new FloatingOwnerBindingIntentDecision(
                HasIntent: true,
                Reason: FloatingOwnerBindingIntentReason.ToolbarOwnerBindingRequested);
        }

        if (ownerPlan.RollCallAction != FloatingOwnerBindingAction.None)
        {
            return new FloatingOwnerBindingIntentDecision(
                HasIntent: true,
                Reason: FloatingOwnerBindingIntentReason.RollCallOwnerBindingRequested);
        }

        if (ownerPlan.ImageManagerAction != FloatingOwnerBindingAction.None)
        {
            return new FloatingOwnerBindingIntentDecision(
                HasIntent: true,
                Reason: FloatingOwnerBindingIntentReason.ImageManagerOwnerBindingRequested);
        }

        return new FloatingOwnerBindingIntentDecision(
            HasIntent: false,
            Reason: FloatingOwnerBindingIntentReason.None);
    }

    internal static bool HasOwnerBindingIntent(FloatingOwnerExecutionPlan ownerPlan)
    {
        return ResolveOwnerBindingIntent(ownerPlan).HasIntent;
    }
}
