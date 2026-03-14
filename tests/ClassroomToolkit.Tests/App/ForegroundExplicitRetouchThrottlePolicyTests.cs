using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundExplicitRetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnAllow_WhenIntervalDisabled()
    {
        var decision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            lastRetouchUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 0);

        decision.ShouldAllowRetouch.Should().BeTrue();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnAllow_WhenNoPreviousRetouch()
    {
        var decision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 120);

        decision.ShouldAllowRetouch.Should().BeTrue();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnThrottled_WhenWithinThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            lastRetouchUtc: now.AddMilliseconds(-50),
            nowUtc: now,
            minimumIntervalMs: 120);

        decision.ShouldAllowRetouch.Should().BeFalse();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.Throttled);
    }

    [Fact]
    public void Resolve_ShouldReturnAllow_WhenOutsideThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            lastRetouchUtc: now.AddMilliseconds(-180),
            nowUtc: now,
            minimumIntervalMs: 120);

        decision.ShouldAllowRetouch.Should().BeTrue();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.None);
    }

    [Fact]
    public void Resolve_RuntimeStateOverload_ShouldUseLastRetouchUtc()
    {
        var now = DateTime.UtcNow;
        var state = new ExplicitForegroundRetouchRuntimeState(
            LastRetouchUtc: now.AddMilliseconds(-50));

        var decision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            state,
            now,
            minimumIntervalMs: 120);

        decision.ShouldAllowRetouch.Should().BeFalse();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.Throttled);
    }

    [Fact]
    public void ShouldAllowRetouch_ShouldMapResolveDecision()
    {
        var allowed = ForegroundExplicitRetouchThrottlePolicy.ShouldAllowRetouch(
            lastRetouchUtc: DateTime.UtcNow.AddMilliseconds(-180),
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 120);

        allowed.Should().BeTrue();
    }
}
