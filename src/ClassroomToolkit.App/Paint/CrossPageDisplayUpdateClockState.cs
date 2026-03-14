namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayUpdateClockState(
    DateTime LastUpdateUtc)
{
    internal static CrossPageDisplayUpdateClockState Default => new(
        LastUpdateUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc);
}
