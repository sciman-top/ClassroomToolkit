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
