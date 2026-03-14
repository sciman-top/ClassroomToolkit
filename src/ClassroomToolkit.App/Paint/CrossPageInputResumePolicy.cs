namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputResumePolicy
{
    internal static CrossPageInputResumeExecutionPlan Resolve(
        bool switchedPage,
        PaintToolMode mode,
        bool strokeInProgress,
        bool isErasing,
        bool replayCurrentInput,
        bool hasPendingBrushSeed,
        bool pendingSeedEqualsInput)
    {
        if (!switchedPage)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.None,
                ShouldClearPendingBrushState: false,
                ShouldUpdateBrushAfterContinuation: false);
        }

        if (mode == PaintToolMode.Brush && !strokeInProgress)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.BeginBrushContinuation,
                ShouldClearPendingBrushState: true,
                ShouldUpdateBrushAfterContinuation: replayCurrentInput || hasPendingBrushSeed || !pendingSeedEqualsInput);
        }

        if (mode == PaintToolMode.Eraser && !isErasing)
        {
            return new CrossPageInputResumeExecutionPlan(
                CrossPageInputResumeAction.BeginEraser,
                ShouldClearPendingBrushState: false,
                ShouldUpdateBrushAfterContinuation: false);
        }

        return new CrossPageInputResumeExecutionPlan(
            CrossPageInputResumeAction.None,
            ShouldClearPendingBrushState: false,
            ShouldUpdateBrushAfterContinuation: false);
    }
}
