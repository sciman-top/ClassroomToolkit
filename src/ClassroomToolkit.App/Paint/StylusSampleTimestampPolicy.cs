namespace ClassroomToolkit.App.Paint;

internal static class StylusSampleTimestampPolicy
{
    internal static long ResolveBatchSpanTicks(
        long stopwatchFrequency,
        long nowTicks,
        int sampleCount,
        in StylusSampleTimestampState state)
    {
        return StylusBatchTimingPolicy.ResolveSpanTicks(
            stopwatchFrequency,
            nowTicks,
            sampleCount,
            hasPreviousTimestamp: state.HasTimestamp,
            lastTimestampTicks: state.LastTimestampTicks);
    }

    internal static long EnsureMonotonicTimestamp(
        long timestampTicks,
        in StylusSampleTimestampState state)
    {
        if (!state.HasTimestamp)
        {
            return timestampTicks;
        }

        if (timestampTicks <= state.LastTimestampTicks)
        {
            return state.LastTimestampTicks + 1;
        }

        return timestampTicks;
    }
}
