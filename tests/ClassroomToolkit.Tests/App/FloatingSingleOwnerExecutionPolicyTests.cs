using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingSingleOwnerExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenChildIsNull()
    {
        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: false,
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        action.Should().Be(FloatingOwnerBindingAction.None);
    }

    [Fact]
    public void Resolve_ShouldAttachOverlay_WhenVisibleAndNotOwned()
    {
        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: true,
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void Resolve_ShouldDetachOverlay_WhenInvisibleAndAlreadyOwned()
    {
        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: true,
            overlayVisible: false,
            ownerAlreadyOverlay: true);

        action.Should().Be(FloatingOwnerBindingAction.DetachOverlay);
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldReturnAttachOverlay_WhenVisibleAndNotOwned()
    {
        var context = new FloatingOwnerBindingContext(
            OverlayVisible: true,
            OwnerAlreadyOverlay: false);

        var action = FloatingSingleOwnerExecutionPolicy.Resolve(
            childExists: true,
            context);

        action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }
}
