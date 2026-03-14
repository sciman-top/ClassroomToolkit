using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerBindingPolicyTests
{
    [Fact]
    public void ResolveDecision_ShouldReturnAttachReason_WhenOverlayVisible_AndOwnerNotBound()
    {
        var decision = FloatingOwnerBindingPolicy.ResolveDecision(
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        decision.Action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        decision.Reason.Should().Be(FloatingOwnerBindingReason.AttachWhenOverlayVisible);
    }

    [Fact]
    public void ShouldAttachOverlayOwner_ShouldReturnTrue_WhenOverlayVisible_AndOwnerNotBound()
    {
        var shouldAttach = FloatingOwnerBindingPolicy.ShouldAttachOverlayOwner(
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        shouldAttach.Should().BeTrue();
    }

    [Fact]
    public void ShouldAttachOverlayOwner_ShouldReturnFalse_WhenOwnerAlreadyBound()
    {
        var shouldAttach = FloatingOwnerBindingPolicy.ShouldAttachOverlayOwner(
            overlayVisible: true,
            ownerAlreadyOverlay: true);

        shouldAttach.Should().BeFalse();
    }

    [Fact]
    public void ShouldDetachOverlayOwner_ShouldReturnTrue_WhenOverlayHidden_AndOwnerBound()
    {
        var shouldDetach = FloatingOwnerBindingPolicy.ShouldDetachOverlayOwner(
            overlayVisible: false,
            ownerAlreadyOverlay: true);

        shouldDetach.Should().BeTrue();
    }

    [Fact]
    public void ShouldDetachOverlayOwner_ShouldReturnFalse_WhenOverlayVisible()
    {
        var shouldDetach = FloatingOwnerBindingPolicy.ShouldDetachOverlayOwner(
            overlayVisible: true,
            ownerAlreadyOverlay: true);

        shouldDetach.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnAttachOverlay_WhenOverlayVisible_AndOwnerNotBound()
    {
        var action = FloatingOwnerBindingPolicy.Resolve(
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void Resolve_ShouldReturnDetachOverlay_WhenOverlayHidden_AndOwnerBound()
    {
        var action = FloatingOwnerBindingPolicy.Resolve(
            overlayVisible: false,
            ownerAlreadyOverlay: true);

        action.Should().Be(FloatingOwnerBindingAction.DetachOverlay);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenNoOwnerChangeIsNeeded()
    {
        var action = FloatingOwnerBindingPolicy.Resolve(
            overlayVisible: true,
            ownerAlreadyOverlay: true);

        action.Should().Be(FloatingOwnerBindingAction.None);
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldReturnAttachOverlay_WhenVisibleAndNotOwned()
    {
        var context = new FloatingOwnerBindingContext(
            OverlayVisible: true,
            OwnerAlreadyOverlay: false);

        var action = FloatingOwnerBindingPolicy.Resolve(context);

        action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void ResolveDecision_ContextOverload_ShouldReturnAttachOverlay_WhenVisibleAndNotOwned()
    {
        var context = new FloatingOwnerBindingContext(
            OverlayVisible: true,
            OwnerAlreadyOverlay: false);

        var decision = FloatingOwnerBindingPolicy.ResolveDecision(context);

        decision.Action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        decision.Reason.Should().Be(FloatingOwnerBindingReason.AttachWhenOverlayVisible);
    }
}
