using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableBothFlags_WhenPhotoActiveBoardOffAndCrossPageEnabled()
    {
        var state = CrossPagePointerUpStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true);

        state.PhotoTransformActive.Should().BeTrue();
        state.CrossPageDisplayActive.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDisableCrossPage_WhenBoardActive()
    {
        var state = CrossPagePointerUpStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true);

        state.PhotoTransformActive.Should().BeFalse();
        state.CrossPageDisplayActive.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableCrossPage_WhenSwitchOff()
    {
        var state = CrossPagePointerUpStatePolicy.Resolve(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false);

        state.PhotoTransformActive.Should().BeTrue();
        state.CrossPageDisplayActive.Should().BeFalse();
    }
}
