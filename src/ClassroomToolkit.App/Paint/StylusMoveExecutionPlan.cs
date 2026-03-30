namespace ClassroomToolkit.App.Paint;

internal readonly record struct StylusMoveExecutionPlan(
    StylusMoveExecutionAction Action,
    bool ShouldMarkHandled);
