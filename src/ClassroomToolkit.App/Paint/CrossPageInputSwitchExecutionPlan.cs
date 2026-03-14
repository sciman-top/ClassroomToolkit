namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageInputSwitchExecutionPlan(
    bool ShouldSwitch,
    bool ShouldResolveBrushContinuation,
    bool DeferCrossPageDisplayUpdate);
