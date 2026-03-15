using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveCurrentInkRefreshPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush)]
    [InlineData((int)PaintToolMode.Eraser)]
    public void ShouldRequest_ShouldReturnTrue_ForInkMutationToolsDuringInteractivePhotoSwitch(int modeValue)
    {
        var result = CrossPageInteractiveCurrentInkRefreshPolicy.ShouldRequest(
            interactiveSwitch: true,
            photoInkModeActive: true,
            mode: (PaintToolMode)modeValue);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, (int)PaintToolMode.Brush)]
    [InlineData(true, false, (int)PaintToolMode.Brush)]
    [InlineData(true, true, (int)PaintToolMode.Cursor)]
    [InlineData(true, true, (int)PaintToolMode.Shape)]
    [InlineData(true, true, (int)PaintToolMode.RegionErase)]
    public void ShouldRequest_ShouldReturnFalse_WhenSwitchContextDoesNotNeedCurrentInkRefresh(
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var result = CrossPageInteractiveCurrentInkRefreshPolicy.ShouldRequest(
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        result.Should().BeFalse();
    }
}
