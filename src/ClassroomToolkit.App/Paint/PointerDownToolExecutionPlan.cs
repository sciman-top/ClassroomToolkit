namespace ClassroomToolkit.App.Paint;

internal readonly record struct PointerDownToolExecutionPlan(
    PointerDownToolAction Action,
    bool ShouldCapturePointer);
