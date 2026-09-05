namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayUpdateClockState(
    DateTime LastUpdateUtc)
{
    internal static CrossPageDisplayUpdateClockState Default => new(
        LastUpdateUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}

internal static class CrossPageDisplayUpdateClockStateUpdater
{
    internal static void MarkUpdated(
        ref CrossPageDisplayUpdateClockState state,
        DateTime nowUtc)
    {
        state = new CrossPageDisplayUpdateClockState(nowUtc);
    }
}
