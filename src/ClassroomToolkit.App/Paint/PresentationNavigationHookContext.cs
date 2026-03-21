namespace ClassroomToolkit.App.Paint;

internal readonly record struct PresentationNavigationHookContext(
    bool SuppressWheelFromRecentInkInput,
    bool TargetValid,
    bool Passthrough,
    bool InterceptSource,
    bool SuppressedAsDebounced);
