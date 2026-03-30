namespace ClassroomToolkit.App.Paint;

internal readonly record struct StylusSampleTimestampState(
    bool HasTimestamp,
    long LastTimestampTicks)
{
    internal static StylusSampleTimestampState Default => new(
        HasTimestamp: false,
        LastTimestampTicks: 0);
}
