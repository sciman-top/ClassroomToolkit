using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class RetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnIntervalDisabled_WhenIntervalDisabled()
    {
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            DateTime.UtcNow,
            DateTime.UtcNow,
            minimumIntervalMs: 0);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.IntervalDisabled);
    }

    [Fact]
    public void Resolve_ShouldReturnFirstRetouch_WhenNoPreviousRetouch()
    {
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
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
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
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
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            nowUtc.AddMilliseconds(-180),
            nowUtc,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.OutsideThrottleWindow);
    }

    [Fact]
    public void ShouldAllow_ShouldMapResolveDecision()
    {
        WindowingDedupPolicies.ShouldAllow(
                DateTime.UtcNow.AddMilliseconds(-180),
                DateTime.UtcNow,
                minimumIntervalMs: 120)
            .Should()
            .BeTrue();
    }
}

public sealed class ForegroundExplicitRetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnAllow_WhenIntervalDisabled()
    {
        var decision = WindowingDedupPolicies.ResolveForegroundExplicitRetouchThrottle(
            lastRetouchUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 0);

        decision.ShouldAllowRetouch.Should().BeTrue();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnAllow_WhenNoPreviousRetouch()
    {
        var decision = WindowingDedupPolicies.ResolveForegroundExplicitRetouchThrottle(
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
        var decision = WindowingDedupPolicies.ResolveForegroundExplicitRetouchThrottle(
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
        var decision = WindowingDedupPolicies.ResolveForegroundExplicitRetouchThrottle(
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

        var decision = WindowingDedupPolicies.ResolveForegroundExplicitRetouchThrottle(
            state,
            now,
            minimumIntervalMs: 120);

        decision.ShouldAllowRetouch.Should().BeFalse();
        decision.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.Throttled);
    }

    [Fact]
    public void ShouldAllowRetouch_ShouldMapResolveDecision()
    {
        var allowed = WindowingDedupPolicies.ShouldAllowRetouch(
            lastRetouchUtc: DateTime.UtcNow.AddMilliseconds(-180),
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 120);

        allowed.Should().BeTrue();
    }
}
