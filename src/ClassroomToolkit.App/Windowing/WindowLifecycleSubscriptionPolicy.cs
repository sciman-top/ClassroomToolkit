namespace ClassroomToolkit.App.Windowing;

internal enum WindowLifecycleSubscriptionReason
{
    None = 0,
    CurrentWindowMissing = 1,
    SameWindowInstance = 2,
    WindowInstanceChanged = 3
}

internal readonly record struct WindowLifecycleSubscriptionDecision(
    bool ShouldWire,
    WindowLifecycleSubscriptionReason Reason);

internal static class WindowLifecycleSubscriptionPolicy
{
    internal static WindowLifecycleSubscriptionDecision Resolve(object? previousWindow, object? currentWindow)
    {
        if (currentWindow == null)
        {
            return new WindowLifecycleSubscriptionDecision(
                ShouldWire: false,
                Reason: WindowLifecycleSubscriptionReason.CurrentWindowMissing);
        }

        return ReferenceEquals(previousWindow, currentWindow)
            ? new WindowLifecycleSubscriptionDecision(
                ShouldWire: false,
                Reason: WindowLifecycleSubscriptionReason.SameWindowInstance)
            : new WindowLifecycleSubscriptionDecision(
                ShouldWire: true,
                Reason: WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    internal static bool ShouldWire(object? previousWindow, object? currentWindow)
    {
        return Resolve(previousWindow, currentWindow).ShouldWire;
    }
}
