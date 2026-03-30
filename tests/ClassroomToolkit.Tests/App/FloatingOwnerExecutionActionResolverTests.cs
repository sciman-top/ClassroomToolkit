using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerExecutionActionResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnAttachOverlay_WhenOverlayVisibleAndOwnerNotOverlay()
    {
        var action = FloatingOwnerExecutionActionResolver.Resolve(
            overlayVisible: true,
            ownerAlreadyOverlay: false);

        action.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void Resolve_ShouldReturnDetachOverlay_WhenOverlayHiddenAndOwnerAlreadyOverlay()
    {
        var action = FloatingOwnerExecutionActionResolver.Resolve(
            overlayVisible: false,
            ownerAlreadyOverlay: true);

        action.Should().Be(FloatingOwnerBindingAction.DetachOverlay);
    }
}
