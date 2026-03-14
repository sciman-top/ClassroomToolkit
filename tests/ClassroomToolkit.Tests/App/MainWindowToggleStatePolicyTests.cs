using ClassroomToolkit.App.ViewModels;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowToggleStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldMapOverlayAndRollCallVisibility()
    {
        var state = MainWindowToggleStatePolicy.Resolve(
            overlayVisible: true,
            rollCallVisible: false);

        state.IsPaintActive.Should().BeTrue();
        state.IsRollCallVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnInactiveState_WhenNothingVisible()
    {
        var state = MainWindowToggleStatePolicy.Resolve(
            overlayVisible: false,
            rollCallVisible: false);

        state.IsPaintActive.Should().BeFalse();
        state.IsRollCallVisible.Should().BeFalse();
    }
}
