namespace ClassroomToolkit.App.Paint;

internal readonly record struct StylusUpExecutionPlan(
    StylusUpExecutionAction Action,
    bool ShouldMarkHandled);
