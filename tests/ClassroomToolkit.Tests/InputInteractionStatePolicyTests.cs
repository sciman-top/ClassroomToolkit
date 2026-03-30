using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InputInteractionStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldExposePhotoAndBoardState()
    {
        var state = InputInteractionStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);

        state.PhotoModeActive.Should().BeTrue();
        state.BoardActive.Should().BeFalse();
        state.CrossPageDisplayEnabled.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldProjectPhotoOrBoardActive()
    {
        var state = InputInteractionStatePolicy.Resolve(
            photoModeActive: false,
            boardActive: true,
            crossPageDisplayEnabled: true);

        state.PhotoOrBoardActive.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldProjectPhotoNavigationEnabled()
    {
        var enabled = InputInteractionStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);
        var disabled = InputInteractionStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        enabled.PhotoNavigationEnabled.Should().BeTrue();
        disabled.PhotoNavigationEnabled.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldProjectCrossPageInputDisplayActive()
    {
        var active = InputInteractionStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);
        var inactive = InputInteractionStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        active.CrossPageDisplayActive.Should().BeTrue();
        inactive.CrossPageDisplayActive.Should().BeFalse();
        active.CrossPageInputDisplayActive.Should().BeTrue();
        inactive.CrossPageInputDisplayActive.Should().BeFalse();
        active.CrossPageInputDisplayActive.Should().Be(active.CrossPageDisplayActive);
        inactive.CrossPageInputDisplayActive.Should().Be(inactive.CrossPageDisplayActive);
    }
}
