namespace ClassroomToolkit.App.Paint;

internal static class StylusBatchTimingPolicy
{
    internal static long ResolveSpanTicks(
        long stopwatchFrequency,
        long nowTicks,
        int sampleCount,
        bool hasPreviousTimestamp,
        long lastTimestampTicks)
    {
        if (sampleCount <= 0)
        {
            return Math.Max(1, stopwatchFrequency / StylusBatchTimingDefaults.FallbackHzWhenEmpty);
        }

        long minPerSampleTicks = Math.Max(1, stopwatchFrequency / StylusBatchTimingDefaults.MinPerSampleHz);
        long maxPerSampleTicks = Math.Max(minPerSampleTicks, stopwatchFrequency / StylusBatchTimingDefaults.MaxPerSampleHz);
        long minSpanTicks = minPerSampleTicks * sampleCount;
        long maxSpanTicks = maxPerSampleTicks * sampleCount;
        long fallbackSpanTicks = Math.Max(
            minSpanTicks,
            (stopwatchFrequency / StylusBatchTimingDefaults.FallbackSpanHz) * sampleCount);

        if (!hasPreviousTimestamp)
        {
            return fallbackSpanTicks;
        }

        long observedSpan = nowTicks - lastTimestampTicks;
        if (observedSpan <= 0)
        {
            return fallbackSpanTicks;
        }

        return Math.Clamp(observedSpan, minSpanTicks, maxSpanTicks);
    }
}
