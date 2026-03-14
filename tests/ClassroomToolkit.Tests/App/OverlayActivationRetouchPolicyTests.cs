using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationRetouchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoApplyRequest_WhenRequestApplyIsFalse()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
            decision,
            DateTime.UtcNow,
            DateTime.UtcNow,
            minimumIntervalMs: 100);

        retouchDecision.ShouldApply.Should().BeFalse();
        retouchDecision.Reason.Should().Be(OverlayActivationRetouchReason.NoApplyRequest);
        retouchDecision.ShouldUpdateLastRetouchUtc.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnForced_WhenForceEnforceZOrderIsTrue()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true);

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
            decision,
            DateTime.UtcNow,
            DateTime.UtcNow,
            minimumIntervalMs: 100);

        retouchDecision.ShouldApply.Should().BeTrue();
        retouchDecision.Reason.Should().Be(OverlayActivationRetouchReason.Forced);
        retouchDecision.ShouldUpdateLastRetouchUtc.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRespectThrottle_WhenNotForce()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
            decision,
            nowUtc,
            nowUtc.AddMilliseconds(50),
            minimumIntervalMs: 100);

        retouchDecision.ShouldApply.Should().BeFalse();
        retouchDecision.Reason.Should().Be(OverlayActivationRetouchReason.Throttled);
    }

    [Fact]
    public void Resolve_ShouldUpdateLastRetouchUtc_WhenThrottleAllows()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
            decision,
            nowUtc.AddMilliseconds(-200),
            nowUtc,
            minimumIntervalMs: 100);

        retouchDecision.ShouldApply.Should().BeTrue();
        retouchDecision.Reason.Should().Be(OverlayActivationRetouchReason.None);
        retouchDecision.ShouldUpdateLastRetouchUtc.Should().BeTrue();
    }

    [Fact]
    public void ShouldUpdateLastRetouchUtc_ShouldReturnFalse_WhenForced()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true);

        OverlayActivationRetouchPolicy
            .ShouldUpdateLastRetouchUtc(decision, shouldApply: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        OverlayActivationRetouchPolicy.ShouldApply(
                decision,
                DateTime.UtcNow.AddMilliseconds(-200),
                DateTime.UtcNow,
                minimumIntervalMs: 100)
            .Should()
            .BeTrue();
    }
}
