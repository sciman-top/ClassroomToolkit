namespace ClassroomToolkit.App.Windowing;

internal enum FloatingActivationExecutionReason
{
    None = 0,
    TargetMissing = 1,
    ActivationNotRequested = 2
}

internal readonly record struct FloatingActivationExecutionDecision(
    bool ShouldActivate,
    FloatingActivationExecutionReason Reason);

internal static class FloatingActivationExecutionPolicy
{
    internal static FloatingActivationExecutionDecision Resolve<TWindow>(TWindow? target, bool shouldActivate)
        where TWindow : class
    {
        if (target == null)
        {
            return new FloatingActivationExecutionDecision(
                ShouldActivate: false,
                Reason: FloatingActivationExecutionReason.TargetMissing);
        }

        return shouldActivate
            ? new FloatingActivationExecutionDecision(
                ShouldActivate: true,
                Reason: FloatingActivationExecutionReason.None)
            : new FloatingActivationExecutionDecision(
                ShouldActivate: false,
                Reason: FloatingActivationExecutionReason.ActivationNotRequested);
    }

    internal static bool ShouldActivate<TWindow>(TWindow? target, bool shouldActivate)
        where TWindow : class
    {
        return Resolve(target, shouldActivate).ShouldActivate;
    }
}
