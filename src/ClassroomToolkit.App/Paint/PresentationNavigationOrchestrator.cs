namespace ClassroomToolkit.App.Paint;

internal enum PresentationNavigationBlockReason
{
    None = 0,
    WheelSuppressedByRecentInkInput = 1,
    TargetInvalid = 2,
    RawPassthroughWithoutIntercept = 3,
    Debounced = 4
}

internal readonly record struct PresentationNavigationOrchestratorResult(
    bool ShouldDispatch,
    int DirectionCode,
    PresentationNavigationBlockReason BlockReason);

internal static class PresentationNavigationOrchestrator
{
    internal static PresentationNavigationOrchestratorResult ResolveHook(
        PresentationNavigationIntent intent,
        bool suppressWheelFromRecentInkInput,
        bool targetValid,
        bool passthrough,
        bool interceptSource,
        bool suppressedAsDebounced)
    {
        if (intent.IsWheelSource && suppressWheelFromRecentInkInput)
        {
            return new PresentationNavigationOrchestratorResult(
                ShouldDispatch: false,
                DirectionCode: 0,
                BlockReason: PresentationNavigationBlockReason.WheelSuppressedByRecentInkInput);
        }

        if (!targetValid)
        {
            return new PresentationNavigationOrchestratorResult(
                ShouldDispatch: false,
                DirectionCode: 0,
                BlockReason: PresentationNavigationBlockReason.TargetInvalid);
        }

        if (passthrough && !interceptSource)
        {
            return new PresentationNavigationOrchestratorResult(
                ShouldDispatch: false,
                DirectionCode: 0,
                BlockReason: PresentationNavigationBlockReason.RawPassthroughWithoutIntercept);
        }

        if (suppressedAsDebounced)
        {
            return new PresentationNavigationOrchestratorResult(
                ShouldDispatch: false,
                DirectionCode: 0,
                BlockReason: PresentationNavigationBlockReason.Debounced);
        }

        return new PresentationNavigationOrchestratorResult(
            ShouldDispatch: true,
            DirectionCode: intent.Direction > 0 ? 1 : -1,
            BlockReason: PresentationNavigationBlockReason.None);
    }
}
