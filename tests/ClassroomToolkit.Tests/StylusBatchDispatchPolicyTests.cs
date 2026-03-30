using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusBatchDispatchPolicyTests
{
    [Fact]
    public void ResolveStepTicks_ShouldGuardAgainstZeroSampleCount()
    {
        StylusBatchDispatchPolicy.ResolveStepTicks(spanTicks: 100, sampleCount: 0).Should().Be(100);
    }

    [Fact]
    public void ResolveBatchStartTicks_ShouldBackdateBySegmentCount()
    {
        var start = StylusBatchDispatchPolicy.ResolveBatchStartTicks(
            nowTicks: 1000,
            stepTicks: 10,
            sampleCount: 4);

        start.Should().Be(970);
    }
}
