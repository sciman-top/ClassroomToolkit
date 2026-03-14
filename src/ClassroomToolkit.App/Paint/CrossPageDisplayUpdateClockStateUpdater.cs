namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayUpdateClockStateUpdater
{
    internal static void MarkUpdated(
        ref CrossPageDisplayUpdateClockState state,
        DateTime nowUtc)
    {
        state = new CrossPageDisplayUpdateClockState(nowUtc);
    }
}
