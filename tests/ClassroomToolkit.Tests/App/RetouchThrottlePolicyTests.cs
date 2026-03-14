using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnIntervalDisabled_WhenIntervalDisabled()
    {
        var decision = RetouchThrottlePolicy.Resolve(
            DateTime.UtcNow,
            DateTime.UtcNow,
            minimumIntervalMs: 0);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.IntervalDisabled);
    }

    [Fact]
    public void Resolve_ShouldReturnFirstRetouch_WhenNoPreviousRetouch()
    {
        var decision = RetouchThrottlePolicy.Resolve(
            WindowDedupDefaults.UnsetTimestampUtc,
            DateTime.UtcNow,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.FirstRetouch);
    }

    [Fact]
    public void Resolve_ShouldReturnWithinThrottleWindow_WhenWithinWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = RetouchThrottlePolicy.Resolve(
            nowUtc.AddMilliseconds(-40),
            nowUtc,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeFalse();
        decision.Reason.Should().Be(RetouchThrottleReason.WithinThrottleWindow);
    }

    [Fact]
    public void Resolve_ShouldReturnOutsideThrottleWindow_WhenOutsideWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = RetouchThrottlePolicy.Resolve(
            nowUtc.AddMilliseconds(-180),
            nowUtc,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.OutsideThrottleWindow);
    }

    [Fact]
    public void ShouldAllow_ShouldMapResolveDecision()
    {
        RetouchThrottlePolicy.ShouldAllow(
                DateTime.UtcNow.AddMilliseconds(-180),
                DateTime.UtcNow,
                minimumIntervalMs: 120)
            .Should()
            .BeTrue();
    }
}
