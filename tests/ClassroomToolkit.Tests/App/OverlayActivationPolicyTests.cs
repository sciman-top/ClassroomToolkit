using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class OverlayActivationPolicyTests
{
    [Fact]
    public void Resolve_ShouldActivate_WhenOverlayNeedsActivationAndNoUtilityWindowIsActive()
    {
        var decision = OverlayActivationPolicy.Resolve(
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
        var decision = OverlayActivationPolicy.Resolve(
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
        var decision = OverlayActivationPolicy.Resolve(
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
        var decision = OverlayActivationPolicy.Resolve(
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
        var decision = OverlayActivationPolicy.Resolve(
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
        var decision = OverlayActivationPolicy.Resolve(
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
        OverlayActivationPolicy.ShouldActivate(
            overlayVisible: true,
            overlayShouldActivate: true,
            overlayActive: false,
            toolbarActive: false,
            imageManagerActive: false,
            rollCallActive: false,
            launcherActive: false).Should().BeTrue();
    }
}
