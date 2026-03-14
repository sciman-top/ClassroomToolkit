namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoManipulationDeltaExecutionPlan(
    bool ShouldApplyTranslation,
    bool ShouldLogPanTelemetry,
    bool ShouldRequestCrossPageUpdate);
