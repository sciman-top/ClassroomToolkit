namespace ClassroomToolkit.App.Paint;

internal static class StylusBatchDispatchPolicy
{
    internal const int MinStepTicks = 1;
    internal const int MinSampleCountForDivision = 1;
    internal const int MinBatchOffsetSamples = 0;

    internal static long ResolveStepTicks(long spanTicks, int sampleCount)
    {
        return Math.Max(MinStepTicks, spanTicks / Math.Max(MinSampleCountForDivision, sampleCount));
    }

    internal static long ResolveBatchStartTicks(long nowTicks, long stepTicks, int sampleCount)
    {
        return nowTicks - (stepTicks * Math.Max(MinBatchOffsetSamples, sampleCount - 1));
    }
}
