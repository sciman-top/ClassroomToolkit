namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageInputResumeExecutionPlan(
    CrossPageInputResumeAction Action,
    bool ShouldClearPendingBrushState,
    bool ShouldUpdateBrushAfterContinuation);
