using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusSampleTimestampPolicyTests
{
    [Fact]
    public void ResolveBatchSpanTicks_ShouldUseFallback_WhenStateHasNoTimestamp()
    {
        var span = StylusSampleTimestampPolicy.ResolveBatchSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 0,
            sampleCount: 0,
            state: StylusSampleTimestampState.Default);

        span.Should().Be(4);
    }

    [Fact]
    public void ResolveBatchSpanTicks_ShouldUseObservedSpan_WhenStateHasTimestamp()
    {
        var span = StylusSampleTimestampPolicy.ResolveBatchSpanTicks(
            stopwatchFrequency: 1000,
            nowTicks: 3000,
            sampleCount: 10,
            state: new StylusSampleTimestampState(
                HasTimestamp: true,
                LastTimestampTicks: 1000));

        span.Should().Be(220);
    }

    [Fact]
    public void EnsureMonotonicTimestamp_ShouldAdvance_WhenTimestampNotGreaterThanPrevious()
    {
        var timestamp = StylusSampleTimestampPolicy.EnsureMonotonicTimestamp(
            timestampTicks: 500,
            state: new StylusSampleTimestampState(
                HasTimestamp: true,
                LastTimestampTicks: 500));

        timestamp.Should().Be(501);
    }
}
