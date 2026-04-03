using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkRedrawTelemetryPolicyTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("on")]
    [InlineData("yes")]
    [InlineData("enabled")]
    [InlineData(" enabled ")]
    public void IsEnabledValue_ShouldReturnTrue_ForTruthyValues(string raw)
    {
        InkRedrawTelemetryPolicy.IsEnabledValue(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("off")]
    [InlineData("no")]
    [InlineData("disabled")]
    public void IsEnabledValue_ShouldReturnFalse_ForFalsyValues(string? raw)
    {
        InkRedrawTelemetryPolicy.IsEnabledValue(raw).Should().BeFalse();
    }

    [Fact]
    public void AppendSample_ShouldTrimWindowToConfiguredSize()
    {
        var samples = new Queue<double>();
        InkRedrawTelemetryPolicy.AppendSample(samples, 1, windowSize: 3);
        InkRedrawTelemetryPolicy.AppendSample(samples, 2, windowSize: 3);
        InkRedrawTelemetryPolicy.AppendSample(samples, 3, windowSize: 3);
        InkRedrawTelemetryPolicy.AppendSample(samples, 4, windowSize: 3);

        samples.Should().Equal(new[] { 2.0, 3.0, 4.0 });
    }

    [Fact]
    public void Percentile_ShouldReturnP50AndP95FromSortedSamples()
    {
        var samples = new[] { 5.0, 1.0, 3.0, 2.0, 4.0 };

        var p50 = InkRedrawTelemetryPolicy.Percentile(samples, 0.5);
        var p95 = InkRedrawTelemetryPolicy.Percentile(samples, 0.95);

        p50.Should().Be(3.0);
        p95.Should().Be(4.0);
    }

    [Fact]
    public void ShouldEmitLog_ShouldRespectStrideAndInterval()
    {
        var now = DateTime.UtcNow;
        var recent = now.AddSeconds(-5);

        var blocked = InkRedrawTelemetryPolicy.ShouldEmitLog(
            sampleCount: 5,
            nowUtc: now,
            lastLogUtc: recent,
            minSampleStride: 40,
            minIntervalSeconds: 30);
        blocked.Should().BeFalse();

        var strideHit = InkRedrawTelemetryPolicy.ShouldEmitLog(
            sampleCount: 40,
            nowUtc: now,
            lastLogUtc: recent,
            minSampleStride: 40,
            minIntervalSeconds: 30);
        strideHit.Should().BeTrue();

        var intervalHit = InkRedrawTelemetryPolicy.ShouldEmitLog(
            sampleCount: 5,
            nowUtc: now,
            lastLogUtc: now.AddSeconds(-31),
            minSampleStride: 40,
            minIntervalSeconds: 30);
        intervalHit.Should().BeTrue();
    }
}
