using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowOverlayInteractionStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldMapFlagsToState()
    {
        var state = MainWindowOverlayInteractionStatePolicy.Resolve(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true);

        state.OverlayVisible.Should().BeTrue();
        state.PhotoModeActive.Should().BeFalse();
        state.WhiteboardActive.Should().BeTrue();
    }
}
