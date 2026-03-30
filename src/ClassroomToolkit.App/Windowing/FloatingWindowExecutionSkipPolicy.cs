namespace ClassroomToolkit.App.Windowing;

internal enum FloatingWindowExecutionSkipReason
{
    None = 0,
    EnforceZOrder = 1,
    ActivationIntent = 2,
    OwnerBindingIntent = 3,
    NoExecutionIntent = 4
}

internal readonly record struct FloatingWindowExecutionSkipDecision(
    bool ShouldExecute,
    FloatingWindowExecutionSkipReason Reason);

internal static class FloatingWindowExecutionSkipPolicy
{
    internal static FloatingWindowExecutionSkipDecision Resolve(FloatingWindowExecutionPlan plan)
    {
        if (plan.TopmostExecutionPlan.EnforceZOrder)
        {
            return new FloatingWindowExecutionSkipDecision(
                ShouldExecute: true,
                Reason: FloatingWindowExecutionSkipReason.EnforceZOrder);
        }

        var activationIntent = FloatingWindowExecutionIntentPolicy.ResolveActivationIntent(plan.ActivationPlan);
        if (activationIntent.HasIntent)
        {
            return new FloatingWindowExecutionSkipDecision(
                ShouldExecute: true,
                Reason: FloatingWindowExecutionSkipReason.ActivationIntent);
        }

        var ownerIntent = FloatingWindowExecutionIntentPolicy.ResolveOwnerBindingIntent(plan.OwnerPlan);
        if (ownerIntent.HasIntent)
        {
            return new FloatingWindowExecutionSkipDecision(
                ShouldExecute: true,
                Reason: FloatingWindowExecutionSkipReason.OwnerBindingIntent);
        }

        return new FloatingWindowExecutionSkipDecision(
            ShouldExecute: false,
            Reason: FloatingWindowExecutionSkipReason.NoExecutionIntent);
    }

    internal static bool ShouldExecute(FloatingWindowExecutionPlan plan)
    {
        return Resolve(plan).ShouldExecute;
    }
}
