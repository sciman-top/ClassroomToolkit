namespace ClassroomToolkit.App.Paint;

internal readonly record struct StylusDownExecutionPlan(
    StylusDownExecutionAction Action,
    bool ShouldResetTimestampState,
    bool ShouldMarkHandled);
