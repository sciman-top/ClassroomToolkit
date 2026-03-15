using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageCurrentPageSeedSlotHidePolicyTests
{
    [Fact]
    public void ShouldHide_ShouldReturnFalse_ForBrush()
    {
        var shouldHide = CrossPageCurrentPageSeedSlotHidePolicy.ShouldHide(PaintToolMode.Brush);

        shouldHide.Should().BeFalse();
    }

    [Theory]
    [InlineData(PaintToolMode.Cursor)]
    [InlineData(PaintToolMode.Eraser)]
    [InlineData(PaintToolMode.Shape)]
    [InlineData(PaintToolMode.RegionErase)]
    public void ShouldHide_ShouldReturnTrue_ForNonBrushModes(PaintToolMode mode)
    {
        var shouldHide = CrossPageCurrentPageSeedSlotHidePolicy.ShouldHide(mode);

        shouldHide.Should().BeTrue();
    }
}
