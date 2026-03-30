namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayRunGateDecision(
    bool ShouldRun,
    string? AbortReason);

internal static class CrossPageDisplayRunGatePolicy
{
    internal static CrossPageDisplayRunGateDecision Resolve(bool crossPageDisplayActive)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageDisplayRunGateDecision(
                ShouldRun: false,
                AbortReason: CrossPageDeferredDiagnosticReason.Inactive);
        }

        return new CrossPageDisplayRunGateDecision(
            ShouldRun: true,
            AbortReason: null);
    }
}
