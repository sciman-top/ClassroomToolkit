namespace ClassroomToolkit.App.Paint;

internal static class StylusSampleTimestampStateUpdater
{
    internal static void Reset(ref StylusSampleTimestampState state)
    {
        state = StylusSampleTimestampState.Default;
    }

    internal static void Remember(
        ref StylusSampleTimestampState state,
        long timestampTicks)
    {
        if (timestampTicks <= 0)
        {
            return;
        }

        state = new StylusSampleTimestampState(
            HasTimestamp: true,
            LastTimestampTicks: timestampTicks);
    }
}
