using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class RetouchThrottlePolicyTests
{
    [Fact]
    public void ResolveMs_ReturnsInteractiveWindow_WhenPhotoOrWhiteboardActive()
    {
        WindowingDedupPolicies
            .ResolveMs(
                overlayVisible: true,
                photoModeActive: true,
                whiteboardActive: false,
                FloatingInteractiveDedupIntervalDefaults.DefaultMs,
                FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
            .Should()
            .Be(FloatingInteractiveDedupIntervalDefaults.InteractiveMs);

        WindowingDedupPolicies
            .ResolveMs(
                overlayVisible: true,
                photoModeActive: false,
                whiteboardActive: true,
                FloatingInteractiveDedupIntervalDefaults.DefaultMs,
                FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
            .Should()
            .Be(FloatingInteractiveDedupIntervalDefaults.InteractiveMs);
    }

    [Fact]
    public void ResolveMs_ReturnsDefaultWindow_WhenOverlayNotInteractive()
    {
        WindowingDedupPolicies
            .ResolveMs(
                overlayVisible: false,
                photoModeActive: true,
                whiteboardActive: true,
                FloatingInteractiveDedupIntervalDefaults.DefaultMs,
                FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
            .Should()
            .Be(FloatingInteractiveDedupIntervalDefaults.DefaultMs);
    }

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

}

public sealed class ForegroundExplicitRetouchThrottlePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnAllow_WhenIntervalDisabled()
    {
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            lastRetouchUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 0);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.IntervalDisabled);
    }

    [Fact]
    public void Resolve_ShouldReturnAllow_WhenNoPreviousRetouch()
    {
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: DateTime.UtcNow,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.FirstRetouch);
    }

    [Fact]
    public void Resolve_ShouldReturnThrottled_WhenWithinThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            lastRetouchUtc: now.AddMilliseconds(-50),
            nowUtc: now,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeFalse();
        decision.Reason.Should().Be(RetouchThrottleReason.WithinThrottleWindow);
    }

    [Fact]
    public void Resolve_ShouldReturnAllow_WhenOutsideThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = WindowingDedupPolicies.ResolveRetouchThrottle(
            lastRetouchUtc: now.AddMilliseconds(-180),
            nowUtc: now,
            minimumIntervalMs: 120);

        decision.ShouldAllow.Should().BeTrue();
        decision.Reason.Should().Be(RetouchThrottleReason.OutsideThrottleWindow);
    }

}
