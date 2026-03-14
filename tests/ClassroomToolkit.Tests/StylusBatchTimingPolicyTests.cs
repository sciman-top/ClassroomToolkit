using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusBatchTimingPolicyTests
{
    [Fact]
    public void ResolveSpanTicks_ShouldUseFallback_WhenSampleCountIsZero()
    {
        var span = StylusBatchTimingPolicy.ResolveSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 0,
            sampleCount: 0,
            hasPreviousTimestamp: false,
            lastTimestampTicks: 0);

        span.Should().Be(4); // 1000 / 240 -> floor 4
    }

    [Fact]
    public void ResolveSpanTicks_ShouldClampObservedSpan_WhenPreviousTimestampExists()
    {
        var span = StylusBatchTimingPolicy.ResolveSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 3000,
            sampleCount: 10,
            hasPreviousTimestamp: true,
            lastTimestampTicks: 1000);

        span.Should().Be(220); // maxPerSample=22, maxSpan=220
    }
}
