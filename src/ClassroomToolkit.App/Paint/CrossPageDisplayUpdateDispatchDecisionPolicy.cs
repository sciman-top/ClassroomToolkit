namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayUpdateDispatchDecisionPolicy
{
    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchSnapshot snapshot,
        double elapsedMs,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        return CrossPageDisplayUpdateThrottlePolicy.Resolve(
            updatePending: snapshot.Pending,
            photoPanning: snapshot.Panning,
            crossPageDragging: snapshot.Dragging,
            inkOperationActive: snapshot.InkOperationActive,
            elapsedMs: elapsedMs,
            draggingMinIntervalMs: draggingMinIntervalMs,
            normalMinIntervalMs: normalMinIntervalMs);
    }
}
