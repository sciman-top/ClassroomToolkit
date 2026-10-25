using ClassroomToolkit.App.Windowing;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class FloatingOwnerBindingPolicyTests
{
    private static FloatingOwnerBindingAction Resolve(bool overlayVisible, bool ownerAlreadyOverlay)
    {
        return FloatingOwnerBindingPolicy.Resolve(overlayVisible, ownerAlreadyOverlay);
    }

    [Fact]
    public void Resolve_ReturnsAttachOverlay_WhenOverlayVisible_AndOwnerNotBound()
    {
        Resolve(overlayVisible: true, ownerAlreadyOverlay: false)
            .Should()
            .Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void Resolve_ReturnsDetachOverlay_WhenOverlayHidden_AndOwnerBound()
    {
        Resolve(overlayVisible: false, ownerAlreadyOverlay: true)
            .Should()
            .Be(FloatingOwnerBindingAction.DetachOverlay);
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenNoOwnerChangeIsNeeded()
    {
        Resolve(overlayVisible: true, ownerAlreadyOverlay: true)
            .Should()
            .Be(FloatingOwnerBindingAction.None);
        Resolve(overlayVisible: false, ownerAlreadyOverlay: false)
            .Should()
            .Be(FloatingOwnerBindingAction.None);
    }

    [Fact]
    public void Resolve_ContextOverload_AgreesWithValueOverload()
    {
        var context = new FloatingOwnerBindingContext(
            OverlayVisible: true,
            OwnerAlreadyOverlay: false);

        FloatingOwnerBindingPolicy
            .Resolve(context)
            .Should()
            .Be(FloatingOwnerBindingPolicy.Resolve(
                overlayVisible: true,
                ownerAlreadyOverlay: false));
    }

    [Fact]
    public void ShouldAttachAndDetach_MatchResolvedAction()
    {
        FloatingOwnerBindingPolicy
            .ShouldAttachOverlayOwner(overlayVisible: true, ownerAlreadyOverlay: false)
            .Should()
            .BeTrue();
        FloatingOwnerBindingPolicy
            .ShouldAttachOverlayOwner(overlayVisible: true, ownerAlreadyOverlay: true)
            .Should()
            .BeFalse();
        FloatingOwnerBindingPolicy
            .ShouldDetachOverlayOwner(overlayVisible: false, ownerAlreadyOverlay: true)
            .Should()
            .BeTrue();
        FloatingOwnerBindingPolicy
            .ShouldDetachOverlayOwner(overlayVisible: true, ownerAlreadyOverlay: true)
            .Should()
            .BeFalse();
    }
}
