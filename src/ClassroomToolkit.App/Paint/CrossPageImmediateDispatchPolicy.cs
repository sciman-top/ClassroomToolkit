namespace ClassroomToolkit.App.Paint;

internal static class CrossPageImmediateDispatchPolicy
{
    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchDecision decision,
        CrossPageUpdateDispatchSuffix suffix)
    {
        if (suffix != CrossPageUpdateDispatchSuffix.Immediate)
        {
            return decision;
        }

        if (decision.Mode != CrossPageDisplayUpdateDispatchMode.Delayed)
        {
            return decision;
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: 0);
    }
}
