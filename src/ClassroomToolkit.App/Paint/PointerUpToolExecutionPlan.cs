namespace ClassroomToolkit.App.Paint;

internal readonly record struct PointerUpToolExecutionPlan(
    PointerUpToolAction Action,
    bool ShouldRefreshAdaptiveRenderer);
