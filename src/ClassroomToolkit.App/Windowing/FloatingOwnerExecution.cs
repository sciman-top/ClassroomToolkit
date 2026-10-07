using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingOwnerBindingContext(
    bool OverlayVisible,
    bool OwnerAlreadyOverlay);

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

internal readonly record struct FloatingOwnerExecutionPlan(
    FloatingOwnerBindingAction ToolbarAction,
    FloatingOwnerBindingAction RollCallAction,
    FloatingOwnerBindingAction ImageManagerAction);

internal static class FloatingOwnerExecutionPlanPolicy
{
    internal static FloatingOwnerExecutionPlan Resolve(FloatingOwnerRuntimeSnapshot snapshot)
    {
        return Resolve(
            snapshot.OverlayVisible,
            snapshot.ToolbarOwnerAlreadyOverlay,
            snapshot.RollCallOwnerAlreadyOverlay,
            snapshot.ImageManagerOwnerAlreadyOverlay);
    }

    internal static FloatingOwnerExecutionPlan Resolve(
        bool overlayVisible,
        bool toolbarOwnerAlreadyOverlay,
        bool rollCallOwnerAlreadyOverlay,
        bool imageManagerOwnerAlreadyOverlay)
    {
        return new FloatingOwnerExecutionPlan(
            ToolbarAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                toolbarOwnerAlreadyOverlay),
            RollCallAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                rollCallOwnerAlreadyOverlay),
            ImageManagerAction: FloatingOwnerBindingPolicy.Resolve(
                overlayVisible,
                imageManagerOwnerAlreadyOverlay));
    }
}

internal static class FloatingOwnerExecutionExecutor
{
    internal static void Apply(
        FloatingOwnerExecutionPlan plan,
        Window? overlayOwner,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? imageManagerWindow)
    {
        Apply(
            plan,
            overlayOwner,
            toolbarWindow,
            rollCallWindow,
            imageManagerWindow,
            (target, owner, action) => WindowOwnerBindingExecutor.TryApply(target, owner, action));
    }

    internal static void Apply<TTarget, TOwner>(
        FloatingOwnerExecutionPlan plan,
        TOwner? overlayOwner,
        TTarget? toolbarWindow,
        TTarget? rollCallWindow,
        TTarget? imageManagerWindow,
        Func<TTarget?, TOwner?, FloatingOwnerBindingAction, bool> applyAction)
        where TTarget : class
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(applyAction);

        TryApply(toolbarWindow, overlayOwner, plan.ToolbarAction, applyAction);
        TryApply(rollCallWindow, overlayOwner, plan.RollCallAction, applyAction);
        TryApply(imageManagerWindow, overlayOwner, plan.ImageManagerAction, applyAction);
    }

    private static void TryApply<TTarget, TOwner>(
        TTarget? target,
        TOwner? overlayOwner,
        FloatingOwnerBindingAction action,
        Func<TTarget?, TOwner?, FloatingOwnerBindingAction, bool> applyAction)
        where TTarget : class
        where TOwner : class
    {
        _ = SafeActionExecutionExecutor.TryExecute(() => applyAction(target, overlayOwner, action));
    }
}

internal static class FloatingSingleOwnerExecutionExecutor
{
    internal static bool Apply(
        FloatingOwnerBindingAction action,
        Window? child,
        Window? overlayOwner)
    {
        return Apply(
            action,
            child,
            overlayOwner,
            (target, owner, ownerAction) => WindowOwnerBindingExecutor.TryApply(target, owner, ownerAction));
    }

    internal static bool Apply<TWindow>(
        FloatingOwnerBindingAction action,
        TWindow? child,
        TWindow? overlayOwner,
        Func<TWindow?, TWindow?, FloatingOwnerBindingAction, bool> applyAction)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(applyAction);

        return SafeActionExecutionExecutor.TryExecute(
            () => applyAction(child, overlayOwner, action),
            fallback: false);
    }
}
