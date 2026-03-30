namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayUpdateDispatchSnapshotPolicy
{
    internal static CrossPageDisplayUpdateDispatchSnapshot Resolve(
        bool pending,
        bool panning,
        bool dragging,
        bool inkOperationActive)
    {
        return new CrossPageDisplayUpdateDispatchSnapshot(
            Pending: pending,
            Panning: panning,
            Dragging: dragging,
            InkOperationActive: inkOperationActive);
    }
}
