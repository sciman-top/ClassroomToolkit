namespace ClassroomToolkit.App.Paint;

internal enum CrossPageInteractiveSwitchRefreshMode
{
    DeferredByInput,
    ImmediateDirect,
    ImmediateScheduled
}

internal static class CrossPageInteractiveSwitchRefreshPolicy
{
    internal static CrossPageInteractiveSwitchRefreshMode Resolve(
        PaintToolMode mode,
        bool deferCrossPageDisplayUpdate)
    {
        if (deferCrossPageDisplayUpdate)
        {
            return CrossPageInteractiveSwitchRefreshMode.DeferredByInput;
        }

        return mode == PaintToolMode.Brush
            ? CrossPageInteractiveSwitchRefreshMode.ImmediateDirect
            : CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled;
    }
}
