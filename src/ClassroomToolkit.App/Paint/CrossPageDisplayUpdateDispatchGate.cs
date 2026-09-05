namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayUpdateDispatchSnapshot(
    bool Pending,
    bool Panning,
    bool Dragging,
    bool InkOperationActive)
{
    internal static string FormatDiagnosticsTag(CrossPageDisplayUpdateDispatchSnapshot snapshot)
    {
        return $"pending={snapshot.Pending} panning={snapshot.Panning} dragging={snapshot.Dragging}";
    }
}

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

internal static class CrossPageDisplayUpdateRunFailureReplayPolicy
{
    internal static CrossPageReplayQueueDecision Resolve(string source)
    {
        var context = CrossPageUpdateRequestContextFactory.Create(source);
        return context.Kind switch
        {
            CrossPageUpdateSourceKind.VisualSync => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageUpdateSourceKind.Interaction => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}
