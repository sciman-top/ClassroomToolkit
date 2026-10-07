using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class OverlayActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldActivate_WhenOverlayNeedsActivationAndNoUtilityWindowIsActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeTrue();
        decision.Reason.Should().Be(OverlayActivationReason.None);
    }

    [Fact]
    public void Resolve_ShouldNotActivate_WhenToolbarIsActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: true,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationReason.BlockedByToolbar);
    }

    [Fact]
    public void Resolve_ShouldNotActivate_WhenOverlayAlreadyActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: true,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationReason.OverlayAlreadyActive);
    }

    [Fact]
    public void Resolve_ShouldNotActivate_WhenImageManagerIsActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: true,
            rollCallActive: false,
            launcherActive: false);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationReason.BlockedByImageManager);
    }

    [Fact]
    public void Resolve_ShouldNotActivate_WhenRollCallWindowIsActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: true,
            launcherActive: false);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationReason.BlockedByRollCall);
    }

    [Fact]
    public void Resolve_ShouldNotActivate_WhenLauncherIsActive()
    {
        var decision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: true);

        decision.ShouldActivate.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationReason.BlockedByLauncher);
    }

    [Fact]
    public void ShouldActivate_ShouldMapResolveDecision()
    {
        OverlayActivationPolicies.ShouldActivateOverlayActivation(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: false).Should().BeTrue();
    }
}

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

        var retouchDecision = OverlayActivationPolicies.ResolveRetouch(
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

        var retouchDecision = OverlayActivationPolicies.ResolveRetouch(
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

        var retouchDecision = OverlayActivationPolicies.ResolveRetouch(
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

        var retouchDecision = OverlayActivationPolicies.ResolveRetouch(
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

        OverlayActivationPolicies.ShouldUpdateLastRetouchUtc(decision, shouldApply: true)
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

        OverlayActivationPolicies.ShouldApply(
                decision,
                DateTime.UtcNow.AddMilliseconds(-200),
                DateTime.UtcNow,
                minimumIntervalMs: 100)
            .Should()
            .BeTrue();
    }
}

public sealed class OverlayActivationSuppressionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSuppressionRequested_WhenSuppressionFlagIsTrue()
    {
        var decision = OverlayActivationPolicies.ResolveSuppression(true);

        decision.ShouldSuppress.Should().BeTrue();
        decision.Reason.Should().Be(OverlayActivationSuppressionReason.SuppressionRequested);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenSuppressionFlagIsFalse()
    {
        var decision = OverlayActivationPolicies.ResolveSuppression(false);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationSuppressionReason.None);
    }

    [Fact]
    public void ShouldSuppress_ShouldMapResolveDecision()
    {
        var suppress = OverlayActivationPolicies.ShouldSuppress(true);

        suppress.Should().BeTrue();
    }
}

public sealed class OverlayActivationSurfacePolicyTests
{
    [Theory]
    [InlineData(true, ZOrderSurface.PhotoFullscreen, true)]
    [InlineData(true, ZOrderSurface.Whiteboard, true)]
    [InlineData(true, ZOrderSurface.PresentationFullscreen, false)]
    [InlineData(false, ZOrderSurface.PhotoFullscreen, false)]
    public void Resolve_ShouldMatchExpected(bool overlayVisible, ZOrderSurface surface, bool expected)
    {
        var decision = OverlayActivationPolicies.ResolveSurface(overlayVisible, surface);
        decision.ShouldActivate.Should().Be(expected);
    }

    [Fact]
    public void ShouldActivate_ShouldMapResolveDecision()
    {
        OverlayActivationPolicies.ShouldActivateSurface(true, ZOrderSurface.Whiteboard).Should().BeTrue();
    }
}

public sealed class OverlayTopmostEnforcePolicyTests
{
    [Fact]
    public void ResolveForPhotoFullscreen_ShouldReturnFalse_WhenOverlayAlreadyTopmost()
    {
        OverlayActivationPolicies.ResolveForPhotoFullscreen(overlayCurrentlyTopmost: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ResolveForPhotoFullscreen_ShouldReturnTrue_WhenOverlayNotTopmost()
    {
        OverlayActivationPolicies.ResolveForPhotoFullscreen(overlayCurrentlyTopmost: false)
            .Should()
            .BeTrue();
    }
}
